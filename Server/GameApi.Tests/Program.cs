using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MagicOnline.GameApi;
using MagicOnline.MatchSettlement;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Assets.Scripts.Prack.BackSys.ChartData;
using Assets.Scripts.Prack.BackSys.GameData;

// Real HTTP + PostgreSQL checks. Uses new, clearly prefixed development users; never deletes existing data.
var url = args.FirstOrDefault() ?? "http://127.0.0.1:5081";
if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !uri.IsLoopback) throw new Exception("Tests are restricted to a local API.");
using var http = new HttpClient { BaseAddress = uri };
int passed = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); passed++; }
async Task<(HttpStatusCode Status, JsonElement Body)> Call(string path, object? body = null, string? token = null, string method = "POST")
{
    using var request = new HttpRequestMessage(new HttpMethod(method), path);
    if (body != null) request.Content = JsonContent.Create(body, options: new JsonSerializerOptions(JsonSerializerDefaults.Web) { IncludeFields = true });
    if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    using var response = await http.SendAsync(request); string text = await response.Content.ReadAsStringAsync();
    return (response.StatusCode, string.IsNullOrEmpty(text) ? default : JsonSerializer.Deserialize<JsonElement>(text));
}
var suffix = Guid.NewGuid().ToString("N")[..12]; string password = "Test-" + Guid.NewGuid().ToString("N");
var health = await Call("/health", method: "GET"); Check(health.Status == HttpStatusCode.OK, "DB-backed health");
Check((await Call("/api/me", method: "GET")).Status == HttpStatusCode.Unauthorized, "anonymous private profile denied");
var a = await Call("/api/auth/register", new { LoginId = "verify_a_" + suffix, Password = password });
var b = await Call("/api/auth/register", new { LoginId = "verify_b_" + suffix, Password = password });
Check(a.Status == HttpStatusCode.OK && b.Status == HttpStatusCode.OK, "two new accounts created");
string at = a.Body.GetProperty("AccessToken").GetString()!, bt = b.Body.GetProperty("AccessToken").GetString()!;
string au = a.Body.GetProperty("UserId").GetString()!, bu = b.Body.GetProperty("UserId").GetString()!;
Check(au != bu, "server-generated distinct IDs");
Check((await Call("/api/auth/register", new { LoginId = "VERIFY_A_" + suffix, Password = password })).Status == HttpStatusCode.Conflict, "case-insensitive login uniqueness");
Check((await Call("/api/auth/login", new { LoginId = "verify_a_" + suffix, Password = "incorrect-password" })).Status == HttpStatusCode.Unauthorized, "wrong password rejected");
Check((await Call("/api/auth/login", new { LoginId = "verify_a_" + suffix, Password = password })).Status == HttpStatusCode.OK, "password login succeeds");
Check((await Call("/api/me", token: at, method: "GET")).Body.GetProperty("Coin").GetInt64() == 20, "initial grant exactly 20");
var opponent = (await Call("/api/players/" + bu, token: at, method: "GET")).Body;
Check(opponent.GetProperty("Coin").GetInt64() == 20, "opponent card can read game coin display");
Check(!opponent.TryGetProperty("Jem", out _) && !opponent.TryGetProperty("LoginId", out _), "opponent card excludes account-private fields");
Check((await Call("/api/me/profile", new { NickName = "Changed", AvatarIdx = 1, Coin = 9999 }, at, "PATCH")).Status == HttpStatusCode.BadRequest, "profile cannot set wallet fields");
var refreshed = await Call("/api/auth/refresh", new { RefreshToken = a.Body.GetProperty("RefreshToken").GetString() });
Check(refreshed.Status == HttpStatusCode.OK, "refresh rotates token");
Check((await Call("/api/me", token: at, method: "GET")).Status == HttpStatusCode.Unauthorized, "old access token invalid after rotation");
Check((await Call("/api/auth/refresh", new { RefreshToken = a.Body.GetProperty("RefreshToken").GetString() })).Status == HttpStatusCode.Unauthorized, "refresh reuse denied");
Check((await Call("/api/me", token: refreshed.Body.GetProperty("AccessToken").GetString(), method: "GET")).Status == HttpStatusCode.Unauthorized, "refresh reuse revokes latest session");
var login = await Call("/api/auth/login", new { LoginId = "verify_a_" + suffix, Password = password }); at = login.Body.GetProperty("AccessToken").GetString()!;
var halls = (await Call("/api/charts/SkillMatch", token: at, method: "GET")).Body.EnumerateArray().ToArray();
Check(halls.Any(h=>h.GetProperty("HallIdx").GetInt32()==1 && h.GetProperty("MatchBall").GetInt32()==3) &&
      halls.Any(h=>h.GetProperty("HallIdx").GetInt32()==2 && h.GetProperty("MatchBall").GetInt32()==4), "3/4-ball seed charts available");
await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Call("/api/me/attendance", token: at)));
Check((await Call("/api/me", token: at, method: "GET")).Body.GetProperty("Coin").GetInt64() == 30, "concurrent attendance credited once");
string match = Guid.NewGuid().ToString("N");
var rules = MatchRulesContract.FromHall(JsonSerializer.Deserialize<SkillMatchData>(halls.Single(h => h.GetProperty("HallIdx").GetInt32() == 2).GetRawText(), Database.Json)!, MatchRulesContract.Pavilion);
var registration = new { RequestId = match, OpponentUserId = bu, HallId = 2, Rules = rules };
Check((await Call("/api/matches", registration, at)).Status == HttpStatusCode.OK, "match registration from server chart");
Check((await Call("/api/matches", registration, at)).Status == HttpStatusCode.OK, "registration retry idempotent");
Check(!(await Call($"/api/matches/{match}/ready", new { Rules = rules }, at)).Body.GetProperty("Playing").GetBoolean(), "one readiness cannot debit both users");
await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Call($"/api/matches/{match}/ready", new { Rules = rules }, bt)));
Check((await Call("/api/me", token: at, method: "GET")).Body.GetProperty("Coin").GetInt64() == 29, "entry debit A exactly once");
Check((await Call("/api/me", token: bt, method: "GET")).Body.GetProperty("Coin").GetInt64() == 19, "entry debit B exactly once");
var report = new { ScoreA = 3, ScoreB = 0, WinnerSeat = 0 };
Check((await Call($"/api/matches/{match}/result", new { ScoreA=0, ScoreB=3, WinnerSeat=0 },at)).Status == HttpStatusCode.Conflict, "winner contradicting score rejected");
Check((await Call($"/api/matches/{match}/result", new { ScoreA=0, ScoreB=0, WinnerSeat=1 },at)).Status == HttpStatusCode.Conflict, "tie with a winner rejected");
Check((await Call($"/api/matches/{match}/result", new { ScoreA=1, ScoreB=0, WinnerSeat=-1 },at)).Status == HttpStatusCode.Conflict, "unequal-score draw rejected");
Check((await Call($"/api/matches/{match}",token:at,method:"GET")).Body.GetProperty("Phase").GetString() == "Playing" &&
    (await Call("/api/me",token:at,method:"GET")).Body.GetProperty("Coin").GetInt64() == 29, "invalid results preserve active match and wallet");
await Call($"/api/matches/{match}/result", report, at);
var settles = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Call($"/api/matches/{match}/result", report, bt)));
Check(settles.All(r => r.Status == HttpStatusCode.OK), "parallel settlement retries all return receipt");
var pa = (await Call("/api/me", token: at, method: "GET")).Body; var pb = (await Call("/api/me", token: bt, method: "GET")).Body;
Check(pa.GetProperty("Coin").GetInt64() == 31 && pb.GetProperty("Coin").GetInt64() == 19, "winner paid entry pool once");
Check(pa.GetProperty("Ball_4_Win").GetInt32() == 1 && pb.GetProperty("Ball_4_Lose").GetInt32() == 1 && pa.GetProperty("Ball_3_Win").GetInt32() == 0, "4-ball and 3-ball stats separated");
Check((await Call("/api/me/history", token: at, method: "GET")).Body.GetArrayLength() == 1, "one personal history");
// Direct adapter failure injection verifies rollback against the actual same local database.
var config = new ConfigurationBuilder().AddUserSecrets<Accounts>().AddEnvironmentVariables().Build();
await using var source = NpgsqlDataSource.Create(config.GetConnectionString("GameDb")!);
var store = new PostgresSettlementStore(new Database(source));
try { store.Execute<bool>(match, c => { c.UserA.Coin += 100; throw new InvalidOperationException("injected"); }); } catch (InvalidOperationException) { }
Check((await Call("/api/me", token: at, method: "GET")).Body.GetProperty("Coin").GetInt64() == 31, "exception rolls back wallet");
// Exercise remaining domain policies using real PostgreSQL with a controllable server clock.
var clock = new TestClock { UtcMilliseconds = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeMilliseconds() };
var service = new SettlementService(store, clock);
string Register(int ball, long fee = 1)
{
    var id = Guid.NewGuid().ToString("N");
    service.Register(new VerifiedRegistration(id, au, bu, "A", "B", ball, ball == 3 ? 1 : 2, fee, CoinPolicy.EntryPool, "{}")); return id;
}
string draw = Register(3); service.Ready(draw, au); service.Ready(draw, bu);
service.ConfirmResult(draw, au, new ResultReport(2,2,-1)); service.ConfirmResult(draw, bu, new ResultReport(2,2,-1));
pa = (await Call("/api/me", token: at, method: "GET")).Body;
Check(pa.GetProperty("Coin").GetInt64() == 31 && pa.GetProperty("Ball_3_Draw").GetInt32() == 1, "3-ball draw refunds fees and counts separately");
string abandon = Register(3); service.Ready(abandon, au); service.Ready(abandon, bu);
clock.UtcMilliseconds += 46000; service.Heartbeat(abandon, au);
Check(service.EvaluateDisconnect(abandon, au)?.WinnerUserId == au, "server heartbeat timeout forfeits disconnected user");
string both = Register(4); service.Ready(both, au); service.Ready(both, bu); clock.UtcMilliseconds += 46000;
Check(service.EvaluateDisconnect(both, au)?.Reason == "BothDisconnected", "both disconnected refunds without a loss");
var third = await new Accounts(new Database(source)).Register(new Credentials("verify_c_"+suffix,password));
string invite = Guid.NewGuid().ToString("N"), otherGame = Guid.NewGuid().ToString("N");
service.Register(new VerifiedRegistration(invite,third.UserId,bu,"C","B",3,1,1,CoinPolicy.EntryPool,"{}"));
service.Register(new VerifiedRegistration(otherGame,bu,au,"B","A",3,1,1,CoinPolicy.EntryPool,"{}"));
Check(service.Ready(otherGame,bu) == false && service.Ready(otherGame,au), "unaccepted invitation does not reserve recipient slot");
service.Ready(invite,third.UserId);
bool busyRejected = false;
try { service.Ready(invite,bu); } catch(PostgresException e) when(e.SqlState == PostgresErrorCodes.UniqueViolation) { busyRejected = true; }
Check(busyRejected,"recipient cannot accept two active matches");
Check((await new GameData(new Database(source)).Profile(Guid.Parse(third.UserId))).Coin == 20,"failed acceptance rolls back inviter entry fee");
service.Surrender(invite,third.UserId); service.Surrender(otherGame,au);
string insufficient = Register(4, 999999); service.Ready(insufficient, au);
bool rejected = false; try { service.Ready(insufficient, bu); } catch(InvalidOperationException) { rejected = true; }
Check(rejected, "insufficient funds rejects entire start transaction");
service.Surrender(insufficient, au);
string surrender = Register(3); service.Ready(surrender, au); service.Ready(surrender, bu);
Check(service.Surrender(surrender, bu)?.WinnerUserId == au, "authenticated surrender awards opponent");
string dispute = Register(4); service.Ready(dispute, au); service.Ready(dispute, bu);
service.ConfirmResult(dispute, au,new ResultReport(3,0,0));
Check(service.ConfirmResult(dispute,bu,new ResultReport(0,3,1)) == null, "conflicting results are not paid");
// Check a constraint failure AFTER wallet writes rolls those writes back as well.
long beforeRollback = (await Call("/api/me", token: at, method:"GET")).Body.GetProperty("Coin").GetInt64();
try { store.Execute<bool>(dispute,c => { c.UserA.Coin += 99; c.UserB.Coin = -1; return true; }); } catch(PostgresException) { }
Check((await Call("/api/me", token: at, method:"GET")).Body.GetProperty("Coin").GetInt64() == beforeRollback, "DB constraint rolls back prior user write");
await using (var privateCheck = source.CreateCommand("SELECT count(*) FROM settlement_receipts WHERE data ? 'Match' AND data->>'Match' IS NOT NULL"))
    Check((long)(await privateCheck.ExecuteScalarAsync())! == 0, "permanent receipts exclude expiring detail");
// Only expire rows created by THIS test run, then let the actual server maintenance remove details.
foreach (var table in new[] { "match_history", "user_match_history" })
{
    await using var expire = source.CreateCommand($"UPDATE {table} SET expires_at=now()-interval '1 day' WHERE match_id=$1");
    expire.Parameters.AddWithValue(draw); await expire.ExecuteNonQueryAsync();
}
await using (var age = source.CreateCommand("UPDATE settlement_receipts SET settled_at=now()-interval '7 months' WHERE match_id=$1")) { age.Parameters.AddWithValue(draw); await age.ExecuteNonQueryAsync(); }
bool removed = false;
for (int i=0;i<15;i++)
{
    await using var query = source.CreateCommand("SELECT NOT EXISTS(SELECT 1 FROM match_history WHERE match_id=$1) AND NOT EXISTS(SELECT 1 FROM user_match_history WHERE match_id=$1) AND NOT EXISTS(SELECT 1 FROM matches WHERE id=$1 AND state->'Registration' ? 'RulesSnapshot')");
    query.Parameters.AddWithValue(draw); removed = (bool)(await query.ExecuteScalarAsync())!;
    if(removed) break; await Task.Delay(1000);
}
Check(removed, "maintenance removes expired detail and scrubs rules/names");
Check(service.ConfirmResult(draw,au,new ResultReport(2,2,-1))?.Reason == "MutualConfirmation", "old receipt prevents re-settlement after history cleanup");
await using (var query = source.CreateCommand("SELECT coin=(SELECT sum(coin_delta) FROM wallet_ledger WHERE user_id=$1) FROM wallets WHERE user_id=$1"))
{ query.Parameters.AddWithValue(Guid.Parse(au)); Check((bool)(await query.ExecuteScalarAsync())!, "wallet equals persistent ledger"); }
Check((await Call("/api/auth/logout", token: at)).Status == HttpStatusCode.NoContent, "logout succeeds");
Check((await Call("/api/me", token: at, method: "GET")).Status == HttpStatusCode.Unauthorized, "logout revokes access");
Console.WriteLine($"{passed} checks passed. Development test users: verify_a_{suffix}, verify_b_{suffix}. Passwords/tokens not logged.");
sealed class TestClock : IServerClock { public long UtcMilliseconds { get; set; } }
