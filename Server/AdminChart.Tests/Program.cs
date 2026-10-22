using System.Security.Claims;
using MagicOnline.GameApi;
using MagicOnline.GameApi.Admin;
using Microsoft.Extensions.Configuration;
using Npgsql;

// Exercise real PostgreSQL locks and audit writes in an isolated schema; leave live charts unchanged.
var config=new ConfigurationBuilder().AddUserSecrets<AdminChartService>().Build();
var cs=new NpgsqlConnectionStringBuilder(config.GetConnectionString("GameDb") ?? throw new Exception("Local DB settings required"));
if(cs.Host is not ("localhost" or "127.0.0.1")) throw new Exception("Local DB only");
await using var root=NpgsqlDataSource.Create(cs.ConnectionString);
string schema="chart_test_"+Guid.NewGuid().ToString("N");
async Task Sql(string sql){await using var cmd=root.CreateCommand(sql); await cmd.ExecuteNonQueryAsync();}
int count=0;
void Check(bool ok,string label){if(!ok)throw new Exception(label); Console.WriteLine("PASS: "+label);count++;}
var owner=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,Guid.NewGuid().ToString()),new Claim(ClaimTypes.Role,"Owner")},"test"));
var viewer=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,Guid.NewGuid().ToString()),new Claim(ClaimTypes.Role,"Viewer")},"test"));
await Sql($"CREATE SCHEMA {schema}; CREATE TABLE {schema}.charts AS SELECT * FROM public.charts; CREATE TABLE {schema}.admin_audit_logs (admin_id uuid,action text,target text,request_id text,before_value jsonb,after_value jsonb,reason text);");
try
{
    cs.SearchPath=schema;
    await using var source=NpgsqlDataSource.Create(cs.ConnectionString);
    var service=new AdminChartService(new Database(source));
    async Task Reject(string name,int? row,string revision,Dictionary<string,string> values,int status,ClaimsPrincipal? actor=null)
    {
        try{await service.Save(actor??owner,name,row,revision,values,"test","test");throw new Exception("Expected rejection");}
        catch(ApiFault e){Check(e.Status==status,$"{name} rejected ({status}): {e.Message}");}
    }
    foreach(string name in new[]{"SkillMatch","Attendance","CommonCode"})
    {
        var doc=await service.Load(name);
        var values=AdminChartService.Fields(name).ToDictionary(f=>f.Key,f=>doc.Rows[0]![f.Key]!.ToString());
        if(name=="SkillMatch"){values["HallIdx"]="2147483646";values["HallName"]="차트 추가 검사";}
        if(name=="Attendance")values["DayNum"]=(doc.Rows.Count+1).ToString();
        if(name=="CommonCode"){values["GrpCd"]="ChartTest";values["ComCd"]="Added";}
        await Reject(name,null,doc.Revision,values,403,viewer);
        await service.Save(owner,name,null,doc.Revision,values,"추가 검사","add");
        var added=await service.Load(name);
        Check(added.Rows.Count==doc.Rows.Count+1,name+" add persisted");
        await Reject(name,null,doc.Revision,values,409);
        await Reject(name,null,added.Revision,values,400);
        string key=name=="SkillMatch" ? "HallName" : name=="Attendance" ? "RewardVal" : "Info";
        values[key]=name=="Attendance" ? "123" : "수정 검사";
        await service.Save(owner,name,added.Rows.Count-1,added.Revision,values,"수정 검사","edit");
        var edited=await service.Load(name);
        Check(edited.Rows.Last()![key]!.ToString()==values[key],name+" edit persisted");
        values[AdminChartService.Fields(name).First(f=>f.Numeric).Key]="1.5";
        await Reject(name,edited.Rows.Count-1,edited.Revision,values,400);
    }
    var attendance=await service.Load("Attendance");
    var invalid=new Dictionary<string,string>{{"DayNum",(attendance.Rows.Count+2).ToString()},{"RewardType","1"},{"RewardVal","10"}};
    await Reject("Attendance",null,attendance.Revision,invalid,400);
    invalid["DayNum"]=(attendance.Rows.Count+1).ToString(); invalid["RewardVal"]="-1";
    await Reject("Attendance",null,attendance.Revision,invalid,400);
    await using var audit=source.CreateCommand("SELECT count(*) FROM admin_audit_logs WHERE before_value IS NOT NULL AND after_value IS NOT NULL");
    Check((long)(await audit.ExecuteScalarAsync())! == 6,"six successful changes audited; rejected writes leave no audit");
    Console.WriteLine($"Passed {count} checks; live charts unchanged.");
}
finally{await Sql($"DROP SCHEMA {schema} CASCADE");}

if(args.FirstOrDefault() is string url)
{
    if(!Uri.TryCreate(url,UriKind.Absolute,out var uri) || !uri.IsLoopback) throw new Exception("Local HTTP only");
    using var handler=new HttpClientHandler{AllowAutoRedirect=false, CookieContainer=new System.Net.CookieContainer()};
    using var http=new HttpClient(handler){BaseAddress=uri};
    Check((await http.GetAsync("/admin/charts/edit?name=Attendance")).StatusCode==System.Net.HttpStatusCode.Redirect,"anonymous editor requires login");
    await using var account=root.CreateCommand("SELECT login_id FROM admin_users WHERE NOT disabled AND role IN ('Owner','Operator') ORDER BY created_at LIMIT 1");
    string login=(string)(await account.ExecuteScalarAsync() ?? throw new Exception("Existing local administrator required"));
    string Token(string html)
    {
        var tag=System.Text.RegularExpressions.Regex.Matches(html,"<input[^>]*>").Select(m=>m.Value).First(t=>t.Contains("name=\"__RequestVerificationToken\""));
        return System.Net.WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Match(tag,"value=\"([^\"]*)\"").Groups[1].Value);
    }
    var loginHtml=await http.GetStringAsync("/admin/login");
    var signed=await http.PostAsync("/admin/login",new FormUrlEncodedContent(new Dictionary<string,string>{{"LoginId",login},{"__RequestVerificationToken",Token(loginHtml)}}));
    Check(signed.StatusCode==System.Net.HttpStatusCode.Redirect,"existing development admin login");
    foreach(string name in new[]{"SkillMatch","Attendance","CommonCode"})
    {
        string list=await http.GetStringAsync("/admin/charts?name="+name);
        Check(list.Contains("항목 추가") && list.Contains("row=0"),name+" list exposes add and edit");
        foreach(string suffix in new[]{"","&row=0"})
        {
            string html=await http.GetStringAsync("/admin/charts/edit?name="+name+suffix);
            Check(AdminChartService.Fields(name).All(f=>html.Contains("name=\"Values["+f.Key+"]\"")),name+" form renders all fields "+suffix);
        }
        var form=await http.GetStringAsync("/admin/charts/edit?name="+name);
        var invalidPost=await http.PostAsync("/admin/charts/edit",new FormUrlEncodedContent(new Dictionary<string,string>{{"Name",name},{"__RequestVerificationToken",Token(form)},{"Values["+AdminChartService.Fields(name).First(f=>f.Numeric).Key+"]","bad"}}));
        Check(invalidPost.StatusCode==System.Net.HttpStatusCode.BadRequest,name+" invalid POST renders validation error");
    }
    Check((await http.PostAsync("/admin/charts/edit",new FormUrlEncodedContent(new Dictionary<string,string>{{"Name","Attendance"}}))).StatusCode==System.Net.HttpStatusCode.BadRequest,"POST without antiforgery rejected");
    Console.WriteLine($"Total passed: {count}");
}
