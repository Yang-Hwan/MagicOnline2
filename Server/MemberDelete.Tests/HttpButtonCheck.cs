using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using MagicOnline.GameApi;
using MagicOnline.GameApi.Admin;
using Npgsql;

internal static class HttpButtonCheck
{
    public static async Task Run(NpgsqlDataSource root, string adminUrl, string apiUrl)
    {
        var admin = new Uri(adminUrl.TrimEnd('/') + "/"); var api = new Uri(apiUrl.TrimEnd('/') + "/");
        if (!admin.IsLoopback || !api.IsLoopback) throw new Exception("Local servers only");
        using var handler = new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() };
        using var http = new HttpClient(handler) { BaseAddress = admin };
        using var game = new HttpClient { BaseAddress = api };
        Guid owner; string login;
        await using (var query = root.CreateCommand("SELECT id,login_id FROM admin_users WHERE role='Owner' AND NOT disabled ORDER BY created_at LIMIT 1"))
        await using (var r = await query.ExecuteReaderAsync())
        { if (!await r.ReadAsync()) throw new Exception("Existing development Owner required"); owner = r.GetGuid(0); login = r.GetString(1); }
        var actor = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, owner.ToString()), new Claim(ClaimTypes.Role, "Owner") }, "test"));
        var fixtures = new List<(Guid Id, string Login)>();
        int checks = 0;
        void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks++; Console.WriteLine("PASS " + label); }
        string Attr(string html, string name) => WebUtility.HtmlDecode(Regex.Match(html, $"\\b{Regex.Escape(name)}=\"([^\"]*)\"").Groups[1].Value);
        List<KeyValuePair<string,string>> Hidden(string form) => Regex.Matches(form, "<input\\b[^>]*>").Cast<Match>()
            .Where(m => Attr(m.Value,"type") == "hidden").Select(m => KeyValuePair.Create(Attr(m.Value,"name"),Attr(m.Value,"value"))).ToList();
        string Form(string html, string field) => Regex.Matches(html, "<form\\b[^>]*>.*?</form>", RegexOptions.Singleline).Cast<Match>().Single(m => m.Value.Contains("name=\"" + field + "\"", StringComparison.Ordinal)).Value;
        try
        {
            var signIn = Form(await http.GetStringAsync("admin/login"), "LoginId");
            var signInData = Hidden(signIn); signInData.Add(KeyValuePair.Create("LoginId",login));
            Check((await http.PostAsync("admin/login",new FormUrlEncodedContent(signInData))).StatusCode == HttpStatusCode.Redirect, "development Owner login");
            foreach (bool bulk in new[] { true, false })
            {
                string fixtureLogin = "delbtn_" + Guid.NewGuid().ToString("N")[..12];
                var registration = await game.PostAsJsonAsync("api/auth/register", new { LoginId=fixtureLogin,Password="Test-"+Guid.NewGuid().ToString("N") });
                registration.EnsureSuccessStatusCode();
                using var tokens = JsonDocument.Parse(await registration.Content.ReadAsStringAsync());
                Guid id = Guid.Parse(tokens.RootElement.GetProperty("UserId").GetString()!); fixtures.Add((id,fixtureLogin));
                Uri origin; string html;
                if (bulk)
                {
                    // The layout also has a logout form: select the member-list form rather than every hidden input.
                    var listForm = Form(await http.GetStringAsync("admin/members?q="+fixtureLogin), "Ids");
                    var previewData = Hidden(listForm); previewData.Add(KeyValuePair.Create("Ids",id.ToString()));
                    origin = new Uri(admin, Attr(listForm,"action"));
                    var preview = await http.PostAsync(origin,new FormUrlEncodedContent(previewData)); preview.EnsureSuccessStatusCode(); html = await preview.Content.ReadAsStringAsync();
                    Check(origin.Query.Contains("handler=Preview"), "regression starts from the actual bulk Preview URL");
                }
                else { origin = new Uri(admin,"admin/members/delete?id="+id); html = await http.GetStringAsync(origin); }
                var form = Form(html,"Confirmation"); var action = new Uri(origin,Attr(form,"action"));
                Check(action.AbsolutePath=="/admin/members/delete" && action.Query.Contains("handler=Delete") && !action.Query.Contains("handler=Preview"), "rendered final action explicitly selects Delete");
                var data = Hidden(form); data.Add(KeyValuePair.Create("Reason","삭제 버튼 회귀 검사")); data.Add(KeyValuePair.Create("Confirmation","wrong"));
                var invalid = await http.PostAsync(action,new FormUrlEncodedContent(data));
                Check(invalid.StatusCode==HttpStatusCode.BadRequest,"wrong confirmation reaches validation, not Preview");
                var retryForm = Form(await invalid.Content.ReadAsStringAsync(),"Confirmation"); var retryAction = new Uri(action,Attr(retryForm,"action"));
                Check(retryAction.Query.Contains("handler=Delete"),"validation retry keeps explicit Delete action");
                data=Hidden(retryForm); data.Add(KeyValuePair.Create("Reason","삭제 버튼 회귀 검사")); data.Add(KeyValuePair.Create("Confirmation","삭제"));
                var deleted=await http.PostAsync(retryAction,new FormUrlEncodedContent(data));
                Check(deleted.StatusCode==HttpStatusCode.Redirect && deleted.Headers.Location?.ToString()=="/admin/members","submitting rendered form deletes and redirects");
                await using var verify=root.CreateCommand("SELECT deleted_at IS NOT NULL AND disabled FROM users WHERE id=$1"); verify.Parameters.AddWithValue(id);
                Check(await verify.ExecuteScalarAsync() is true,"fixture actually deleted in database");
                var again=await http.PostAsync(retryAction,new FormUrlEncodedContent(data));
                Check(again.StatusCode==HttpStatusCode.Redirect,"repeat rendered submission remains safe");
            }
            Console.WriteLine($"Passed {checks} HTTP button checks; only self-created fixtures deleted.");
        }
        finally
        {
            if(fixtures.Count>0) await new AdminMemberService(new Database(root)).DeleteMembers(actor,fixtures.Select(f=>f.Id).ToArray(),fixtures.Select(f=>f.Login).ToArray(),"삭제","삭제 버튼 회귀 검사 정리","delete-button-test-cleanup");
        }
    }
}
