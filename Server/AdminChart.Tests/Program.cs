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
var ordered=AdminReadService.ChartTable("CommonCode", """
[{"GrpCd":"B","ComCd":"b","Ord":1},{"GrpCd":"A","ComCd":"a10","Ord":10},{"GrpCd":"#","ComCd":"header"},{"GrpCd":"A","ComCd":"legacy"},{"GrpCd":"A","ComCd":"a2","Ord":2},{"GrpCd":"A","ComCd":"tie","Ord":2}]
""");
Check(ordered.SourceRows!.SequenceEqual(new[]{2,3,4,5,1,0}), "grouped numeric Ord sort preserves original edit/delete indices and ties");
Check(ordered.GroupRows!.SequenceEqual(new[]{true,false,false,false,false,false}), "only # group rows highlighted");
Check(ordered.Rows[1][Array.IndexOf(ordered.Columns,"Ord")]=="0", "legacy Ord defaults to zero");
Check(AdminReadService.ChartTable("CommonCode", "[]").Columns.Contains("Ord"), "empty chart exposes Ord column");
var grouped = AdminReadService.ChartTable("CommonCode", """
[{"GrpCd":"Wealth","ComCd":"Gold","Ord":4},
 {"GrpCd":"#","ComCd":"MsgCha","Ord":2},
 {"GrpCd":"Bust ","ComCd":"BustHelpCoin","Ord":2},
 {"GrpCd":"Wealth","ComCd":"Coin","Ord":1},
 {"GrpCd":"#","ComCd":"Wealth","Ord":3},
 {"GrpCd":"MsgCha","ComCd":"SlyGuy","Ord":2},
 {"GrpCd":"#","ComCd":"Bust","Ord":1},
 {"GrpCd":"Wealth","ComCd":"Cash","Ord":3},
 {"GrpCd":"Bust","ComCd":"Bust1DayHelpCnt","Ord":1},
 {"GrpCd":"MsgCha","ComCd":"Beginner","Ord":1},
 {"GrpCd":"Wealth","ComCd":"Jem","Ord":2}]
""");
Check(grouped.SourceRows!.SequenceEqual(new[]{6,8,2,1,9,5,4,3,10,7,0}), "headers order groups and child Ord orders members, preserving source indices");
Check(grouped.GroupRows!.SequenceEqual(new[]{true,false,false,true,false,false,true,false,false,false,false}), "pink headers occur before each group");
Check(grouped.Rows.Select(row => row[Array.IndexOf(grouped.Columns,"ComCd")]).SequenceEqual(new[]{"Bust","Bust1DayHelpCnt","BustHelpCoin","MsgCha","Beginner","SlyGuy","Wealth","Coin","Jem","Cash","Gold"}), "user example grouping matches exactly");
var owner=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,Guid.NewGuid().ToString()),new Claim(ClaimTypes.Role,"Owner")},"test"));
var viewer=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,Guid.NewGuid().ToString()),new Claim(ClaimTypes.Role,"Viewer")},"test"));
await Sql($"CREATE SCHEMA {schema}; CREATE TABLE {schema}.charts AS SELECT * FROM public.charts; CREATE TABLE {schema}.admin_audit_logs (admin_id uuid,action text,target text,request_id text,before_value jsonb,after_value jsonb,reason text);");
try
{
    cs.SearchPath=schema;
    await using var source=NpgsqlDataSource.Create(cs.ConnectionString);
    if (args.Contains("--wallet"))
    {
        var member = Guid.NewGuid();
        await Sql($"CREATE TABLE {schema}.wallets (user_id uuid PRIMARY KEY,coin bigint,jem integer); CREATE TABLE {schema}.wallet_ledger (user_id uuid,request_key text,coin_delta bigint,jem_delta integer,UNIQUE(user_id,request_key)); INSERT INTO {schema}.wallets VALUES('{member}',100,5)");
        var members = new AdminMemberService(new Database(source));
        var key = Guid.NewGuid();
        Task Credit() => members.AdjustWallet(owner,member,"Coin","credit",10,key,null);
        await Task.WhenAll(Credit(), Credit());
        await members.AdjustWallet(owner,member,"Jem","debit",3,Guid.NewGuid(),null);
        try { await members.AdjustWallet(owner,member,"Jem","debit",3,Guid.NewGuid(),null); throw new Exception("Expected insufficient balance"); }
        catch (ApiFault e) { Check(e.Status == 400,"insufficient balance rejected"); }
        await using var verify = source.CreateCommand("SELECT coin=110 AND jem=2 AND (SELECT count(*) FROM wallet_ledger)=2 AND (SELECT count(*) FROM admin_audit_logs)=2 FROM wallets");
        Check(await verify.ExecuteScalarAsync() is true,"concurrent retries apply once, debit and audit commit together");
        return;
    }
    var service=new AdminChartService(new Database(source));
    if (args.Contains("--transfer"))
    {
        async Task ShopSql(string sql)
        {
            await using var cmd=source.CreateCommand(sql);
            await cmd.ExecuteNonQueryAsync();
        }
        await ShopSql("CREATE TABLE users(id uuid PRIMARY KEY)");
        await ShopSql(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"Migrations","010_shop.sql")));
        await ShopSql("""
            INSERT INTO shop_content_assets(asset_key) VALUES('cue/red'),('face/base');
            INSERT INTO shop_products(id,kind,name,price_gem) OVERRIDING SYSTEM VALUE VALUES
                (10,'Cue','빨간 큐',100),(20,'GemPack','보석',NULL),(30,'Character','선수',50),(40,'MessagePack','응원',5);
            INSERT INTO shop_cues VALUES(10,'cue/red',20,-40,10,-5);
            INSERT INTO shop_gem_packs VALUES(20,100,5);
            INSERT INTO shop_character_emotions SELECT 30,unnest(ARRAY['기본','감사','미안','미소','환희','짜증','우울','좌절','분노']),'face/base';
            INSERT INTO shop_message_packs VALUES(40,'기본');
            INSERT INTO shop_messages VALUES(40,1,'안녕!'),(40,2,'좋았어!');
            """);
        var backup = await service.ExportCharts();
        async Task Import(System.Text.Json.Nodes.JsonObject bundle, ClaimsPrincipal actor)
        {
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(bundle.ToJsonString()));
            await service.ImportCharts(actor, stream, "test-transfer");
        }
        await Sql($"UPDATE {schema}.charts SET rows='[]'");
        await ShopSql("TRUNCATE shop_products,shop_content_assets CASCADE");
        await Import(backup, owner);
        Check(System.Text.Json.Nodes.JsonNode.DeepEquals(backup,await service.ExportCharts()),"charts, products and all details round-trip exactly");
        var legacy=backup.DeepClone().AsObject();
        legacy.Remove("shop");
        legacy["version"]=1;
        await Import(legacy,owner);
        Check(System.Text.Json.Nodes.JsonNode.DeepEquals(backup,await service.ExportCharts()),"v1 keeps existing shop");
        await using(var next=source.CreateCommand("SELECT nextval('shop_products_id_seq')"))
            Check((long)(await next.ExecuteScalarAsync())!>40,"new product IDs follow restored IDs");
        var broken=backup.DeepClone().AsObject();
        broken["shop"]!["character_emotions"]!.AsArray().RemoveAt(0);
        broken["charts"]!["SkillMatch"]![0]!["HallName"]="rollback";
        try { await Import(broken,owner); throw new Exception("Expected missing emotion"); }
        catch(ApiFault error){Check(error.Status==400,"bad shop rejects entire bundle");}
        var invalidBundle = backup.DeepClone().AsObject();
        invalidBundle["charts"]!["PostReward"]![0]!["TypeCode"] = -1000;
        try { await Import(invalidBundle, owner); throw new Exception("Expected invalid reference"); }
        catch (ApiFault error) { Check(error.Status==400,"invalid reward reference rejected"); }
        try { await Import(backup, viewer); throw new Exception("Expected access denial"); }
        catch (ApiFault error) { Check(error.Status==403,"viewer cannot replace charts"); }
        Check(System.Text.Json.Nodes.JsonNode.DeepEquals(backup,await service.ExportCharts()),"failed imports preserve all chart data");
        return;
    }
    async Task Reject(string name,int? row,string revision,Dictionary<string,string> values,int status,ClaimsPrincipal? actor=null)
    {
        try{await service.Save(actor??owner,name,row,revision,values,"test","test");throw new Exception("Expected rejection");}
        catch(ApiFault e){Check(e.Status==status,$"{name} rejected ({status}): {e.Message}");}
    }
    foreach(string name in new[]{"SkillMatch","Attendance","CommonCode"})
    {
        var doc=await service.Load(name);
        var values=AdminChartService.Fields(name).ToDictionary(f=>f.Key,f=>doc.Rows[0]![f.Key]?.ToString() ?? (f.Key == "Ord" ? "0" : ""));
        if(name=="SkillMatch"){values["HallIdx"]="2147483646";values["HallName"]="차트 추가 검사";}
        if(name=="Attendance")values["DayNum"]=(doc.Rows.Count+1).ToString();
        if(name=="CommonCode"){values["GrpCd"]="ChartTest";values["ComCd"]="Added";values["Ord"]="12";}
        await Reject(name,null,doc.Revision,values,403,viewer);
        await service.Save(owner,name,null,doc.Revision,values,"추가 검사","add");
        var added=await service.Load(name);
        Check(added.Rows.Count==doc.Rows.Count+1,name+" add persisted");
        if(name=="CommonCode") Check(added.Rows.Last()!["Ord"]!.ToString()=="12", "Ord persisted as integer");
        await Reject(name,null,doc.Revision,values,409);
        await Reject(name,null,added.Revision,values,400);
        string key=name=="SkillMatch" ? "HallName" : name=="Attendance" ? "RewardVal" : "Info";
        values[key]=name=="Attendance" ? "123" : "수정 검사";
        await service.Save(owner,name,added.Rows.Count-1,added.Revision,values,"수정 검사","edit");
        var edited=await service.Load(name);
        Check(edited.Rows.Last()![key]!.ToString()==values[key],name+" edit persisted");
        values[AdminChartService.Fields(name).First(f=>f.Numeric).Key]="1.5";
        await Reject(name,edited.Rows.Count-1,edited.Revision,values,400);
        async Task RejectDelete(int row, string revision, int status, ClaimsPrincipal? actor = null)
        {
            try { await service.Delete(actor ?? owner, name, row, revision, "삭제 검사", "delete"); throw new Exception("Expected deletion rejection"); }
            catch (ApiFault e) { Check(e.Status == status, name + " delete rejected " + status); }
        }
        await RejectDelete(edited.Rows.Count-1, edited.Revision, 403, viewer);
        await RejectDelete(edited.Rows.Count-1, doc.Revision, 409);
        await RejectDelete(-1, edited.Revision, 404);
        await RejectDelete(edited.Rows.Count, edited.Revision, 404);
        if (name == "Attendance") await RejectDelete(0, edited.Revision, 400);
        var operatorActor = new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role,"Operator")}, "test"));
        await service.Delete(operatorActor, name, edited.Rows.Count-1, edited.Revision, "삭제 검사", "delete");
        var deleted = await service.Load(name);
        Check(deleted.Rows.ToJsonString() == doc.Rows.ToJsonString(), name + " delete removes only selected row");
        await RejectDelete(edited.Rows.Count-1, edited.Revision, 409);
    }
    var attendance=await service.Load("Attendance");
    var invalid=new Dictionary<string,string>{{"DayNum",(attendance.Rows.Count+2).ToString()},{"RewardType","1"},{"RewardVal","10"}};
    await Reject("Attendance",null,attendance.Revision,invalid,400);
    invalid["DayNum"]=(attendance.Rows.Count+1).ToString(); invalid["RewardVal"]="-1";
    await Reject("Attendance",null,attendance.Revision,invalid,400);
    await using var audit=source.CreateCommand("SELECT count(*) FROM admin_audit_logs WHERE before_value IS NOT NULL AND after_value IS NOT NULL");
    Check((long)(await audit.ExecuteScalarAsync())! == 9,"nine successful changes audited; rejected writes leave no audit");
    await using var deletions = source.CreateCommand("SELECT count(*) FROM admin_audit_logs WHERE action='ChartRowDeleted' AND reason='삭제 검사'");
    Check((long)(await deletions.ExecuteScalarAsync())! == 3, "deletions record action and reason");
    await using (var singleDay = source.CreateCommand("UPDATE charts SET rows='[{\"DayNum\":1,\"RewardType\":1,\"RewardVal\":10}]'::jsonb WHERE name='Attendance'")) await singleDay.ExecuteNonQueryAsync();
    var lastDay = await service.Load("Attendance");
    try { await service.Delete(owner, "Attendance", 0, lastDay.Revision, null, "last"); throw new Exception("Expected last day rejection"); }
    catch (ApiFault e) { Check(e.Status == 400, "last attendance day cannot be deleted"); }
    Check((await service.Load("Attendance")).Revision == lastDay.Revision, "rejected deletion leaves chart unchanged");
    Console.WriteLine($"Passed {count} checks; live charts unchanged.");
    var common=(await service.Load("CommonCode")).Rows;
    var sample=PostRewardChart.Examples(common);
    Check(sample.Count==11,"all eleven post reward examples resolve against actual common codes");
    await using(var reset=source.CreateCommand("DELETE FROM charts WHERE name='PostReward'"))await reset.ExecuteNonQueryAsync();
    await using(var insert=source.CreateCommand("INSERT INTO charts(name,rows) VALUES('PostReward',$1::jsonb)")){
        insert.Parameters.AddWithValue(sample.ToJsonString());await insert.ExecuteNonQueryAsync();
    }
    var read=new AdminReadService(new Database(source));
    var view=await read.Chart("PostReward");
    Check(view.Rows[1][1]=="출석" && view.Rows[1][6]=="" && view.Rows[1][4]=="?일차 출석보상","display names resolved and dynamic attendance amounts blank");
    var docPost=await service.Load("PostReward");
    var combo=AdminChartService.Fields("PostReward").ToDictionary(f=>f.Key,f=>docPost.Rows[4]![f.Key]?.ToString()??"");
    combo["RewardId"]="12";await Reject("PostReward",null,docPost.Revision,combo,400);
    combo["RangeMin"]="51";combo["RangeMax"]="60";
    await service.Save(owner,"PostReward",null,docPost.Revision,combo,"test","post-add");
    docPost=await service.Load("PostReward");
    combo["RewardVal"]="40";
    await service.Save(owner,"PostReward",11,docPost.Revision,combo,"test","post-edit");
    docPost=await service.Load("PostReward");
    Check((long)docPost.Rows[11]!["RewardVal"]! ==40,"post reward add/edit");
    await service.Delete(owner,"PostReward",11,docPost.Revision,"test","post-delete");
    Check((await service.Load("PostReward")).Rows.Count==11,"post reward delete");
}
finally{await Sql($"DROP SCHEMA {schema} CASCADE");}

if(args.FirstOrDefault() is string url)
{
    if(!Uri.TryCreate(url,UriKind.Absolute,out var uri) || !uri.IsLoopback) throw new Exception("Local HTTP only");
    using var handler=new HttpClientHandler{AllowAutoRedirect=false, CookieContainer=new System.Net.CookieContainer()};
    using var http=new HttpClient(handler){BaseAddress=uri};
    Check((await http.GetAsync("/admin/charts/edit?name=Attendance")).StatusCode==System.Net.HttpStatusCode.Redirect,"anonymous editor requires login");
    Check((await http.GetAsync("/admin/charts/delete?name=Attendance&row=0")).StatusCode==System.Net.HttpStatusCode.Redirect,"anonymous deletion requires login");
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
        Check(list.Contains("/admin/charts/delete?name="), name + " list exposes deletion");
        var deleteForm = await http.GetStringAsync("/admin/charts/delete?name="+name+"&row=0");
        Check(deleteForm.Contains("name=\"Revision\"") && deleteForm.Contains("name=\"Row\"") && Token(deleteForm).Length > 0, name+" delete confirmation has revision and CSRF token");
        var staleDelete = await http.PostAsync("/admin/charts/delete", new FormUrlEncodedContent(new Dictionary<string,string>{{"Name",name},{"Row","0"},{"Revision","stale"},{"__RequestVerificationToken",Token(deleteForm)}}));
        Check(staleDelete.StatusCode == System.Net.HttpStatusCode.Conflict, name+" stale HTTP deletion rejected");
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
    Check((await http.PostAsync("/admin/charts/delete",new FormUrlEncodedContent(new Dictionary<string,string>{{"Name","Attendance"},{"Row","0"}}))).StatusCode==System.Net.HttpStatusCode.BadRequest,"DELETE POST without antiforgery rejected");
    Console.WriteLine($"Total passed: {count}");
}
