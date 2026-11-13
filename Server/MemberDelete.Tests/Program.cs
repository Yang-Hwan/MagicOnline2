using System.Security.Claims;
using MagicOnline.GameApi;
using MagicOnline.GameApi.Admin;
using MagicOnline.MatchSettlement;
using Microsoft.Extensions.Configuration;
using Npgsql;

var config = new ConfigurationBuilder().AddUserSecrets<Database>().Build();
var cs = new NpgsqlConnectionStringBuilder(config.GetConnectionString("GameDb"));
if (cs.Host is not ("localhost" or "127.0.0.1")) throw new Exception("Local DB only");
await using var root = NpgsqlDataSource.Create(cs.ConnectionString);
if (args.Length == 3 && args[0] == "--http") { await HttpButtonCheck.Run(root, args[1], args[2]); return; }
string schema = "member_delete_test_" + Guid.NewGuid().ToString("N");
await using (var create = root.CreateCommand($"CREATE SCHEMA {schema}")) await create.ExecuteNonQueryAsync();
try
{
    cs.SearchPath = schema;
    await using var source = NpgsqlDataSource.Create(cs.ConnectionString);
    async Task Sql(string sql) { await using var cmd = source.CreateCommand(sql); await cmd.ExecuteNonQueryAsync(); }
    async Task<long> Scalar(string sql) { await using var cmd = source.CreateCommand(sql); return Convert.ToInt64(await cmd.ExecuteScalarAsync()); }
    foreach (var migration in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Migrations"), "*.sql").Order()) await Sql(await File.ReadAllTextAsync(migration));
    Guid admin = Guid.NewGuid(), a = Guid.NewGuid(), b = Guid.NewGuid(), busy = Guid.NewGuid(), peer = Guid.NewGuid();
    await Sql($"INSERT INTO admin_users(id,login_id,normalized_login,password_hash,role) VALUES('{admin}','owner','OWNER','unused','Owner')");
    foreach (var (id, login) in new[] { (a,"delete_a"),(b,"delete_b"),(busy,"busy"),(peer,"peer") })
    {
        await Sql($"INSERT INTO users(id,login_id,normalized_login,password_hash,nickname) VALUES('{id}','{login}',upper('{login}'),'unused','{login}'); INSERT INTO wallets(user_id,coin,jem) VALUES('{id}',100,50); INSERT INTO user_stats(user_id) VALUES('{id}'); INSERT INTO auth_sessions(id,user_id,access_hash,refresh_hash,access_expires,refresh_expires) VALUES('{Guid.NewGuid()}','{id}','{Guid.NewGuid()}','{Guid.NewGuid()}',now()+interval '1 day',now()+interval '2 days'); INSERT INTO friend_presence VALUES('{id}',true,now());");
    }
    await Sql($"INSERT INTO friend_links VALUES('{a}','{peer}',true); INSERT INTO friend_challenges VALUES('{Guid.NewGuid()}','{a}','{peer}',1,'pending',now()+interval '1 hour'); INSERT INTO matches(id,user_a,user_b,state,phase) VALUES('busy','{busy}','{peer}','{{}}',0)");
    await Sql($"INSERT INTO shop_content_assets(asset_key) VALUES('fixture'); INSERT INTO shop_products(id,kind,name,price_gem) OVERRIDING SYSTEM VALUE VALUES(1,'Cue','owned cue',10); INSERT INTO shop_cues VALUES(1,'fixture',0,0,0,0); INSERT INTO shop_user_inventory(user_id,product_id) VALUES('{a}',1); INSERT INTO shop_user_equipment VALUES('{a}','Cue',1); INSERT INTO shop_purchases(user_id,product_id,paid_gem,request_id) VALUES('{a}',1,10,'{Guid.NewGuid()}'); INSERT INTO wallet_ledger(user_id,request_key,coin_delta,jem_delta) VALUES('{a}','fixture',0,-10)");
    var db = new Database(source); var service = new AdminMemberService(db); var read = new AdminReadService(db);
    ClaimsPrincipal Actor(string role) => new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, admin.ToString()), new Claim(ClaimTypes.Role, role) }, "test"));
    var owner = Actor("Owner"); int checks = 0;
    void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks++; Console.WriteLine("PASS " + label); }
    async Task Reject(Func<Task> action, int status, string label)
    { try { await action(); throw new Exception("Expected rejection: " + label); } catch (ApiFault e) { Check(e.Status == status, label); } }
    var candidates = await service.DeletePreview(owner, [a,b]);
    Check(candidates.Count == 2 && candidates.All(x => !x.Deleted && !x.HasMatch), "preview returns exact members without deleting");
    await Reject(async () => { await service.DeletePreview(Actor("Viewer"), [a]); }, 403, "viewer cannot preview deletion");
    await Reject(async () => { await service.DeleteMembers(Actor("Operator"), [a], ["delete_a"], "삭제", "test", "test"); }, 403, "operator cannot delete members");
    await Reject(async () => { await service.DeleteMembers(owner, [], [], "삭제", "test", "test"); }, 400, "empty selection rejected");
    await Reject(async () => { await service.DeletePreview(owner, Enumerable.Range(0,31).Select(_=>Guid.NewGuid()).ToArray()); }, 400, "bulk limit enforced");
    await Reject(async () => { await service.DeleteMembers(owner, [a], ["delete_a"], "yes", "test", "test"); }, 400, "explicit confirmation required");
    await Reject(async () => { await service.DeleteMembers(owner, [a], ["delete_a"], "삭제", "", "test"); }, 400, "reason required");
    await Reject(async () => { await service.DeleteMembers(owner, [a], ["other"], "삭제", "test", "test"); }, 409, "changed target identity rejected");
    await Reject(async () => { await service.DeletePreview(owner, [Guid.NewGuid()]); }, 404, "unknown target rejected");
    foreach (int phase in new[] { 0,1 })
    {
        await Sql($"UPDATE matches SET phase={phase} WHERE id='busy'");
        await Reject(async () => { await service.DeleteMembers(owner, [a,busy], ["delete_a","busy"], "삭제", "test", "test"); }, 409, "unfinished match blocks entire batch phase " + phase);
        Check(await Scalar("SELECT count(*) FROM users WHERE deleted_at IS NOT NULL") == 0, "blocked batch leaves all members intact");
    }
    await Sql($"CREATE FUNCTION reject_delete_audit() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.action='MemberDeleted' AND NEW.target='{b}' THEN RAISE EXCEPTION 'fixture failure'; END IF; RETURN NEW; END $$; CREATE TRIGGER reject_delete_audit BEFORE INSERT ON admin_audit_logs FOR EACH ROW EXECUTE FUNCTION reject_delete_audit()");
    try { await service.DeleteMembers(owner, [a,b], ["delete_a","delete_b"], "삭제", "test", "test"); throw new Exception("Expected audit failure"); }
    catch (PostgresException e) when (e.SqlState == "P0001") { }
    Check(await Scalar("SELECT count(*) FROM users WHERE deleted_at IS NOT NULL") == 0 && await Scalar("SELECT count(*) FROM auth_sessions WHERE revoked") == 0, "audit failure rolls back account and session changes");
    Check(await Scalar("SELECT count(*) FROM friend_challenges WHERE state='pending'") == 1, "audit failure preserves friend state");
    await Sql("DROP TRIGGER reject_delete_audit ON admin_audit_logs");
    await using (var auth = await source.OpenConnectionAsync())
    await using (var rotation = await auth.BeginTransactionAsync())
    {
        await using var locked = Database.Command(auth,"SELECT id FROM auth_sessions WHERE user_id=$1 FOR UPDATE",a);
        await locked.ExecuteScalarAsync();
        await Reject(async () => { await service.DeleteMembers(owner,[a],["delete_a"],"삭제","test","auth busy"); },409,"concurrent authentication yields retry instead of waiting for locks");
        Check(await Scalar("SELECT count(*) FROM users WHERE deleted_at IS NOT NULL") == 0, "busy authentication preserves account");
    }
    var results = await Task.WhenAll(service.DeleteMembers(owner,[a,b],["delete_a","delete_b"],"삭제","실수로 생성","one"), service.DeleteMembers(owner,[a,b],["delete_a","delete_b"],"삭제","실수로 생성","retry"));
    Check(results.Sum() == 2 && results.Contains(0), "concurrent repeats delete once");
    Check(await Scalar("SELECT count(*) FROM users WHERE deleted_at IS NOT NULL AND disabled") == 2, "members are removed and disabled");
    Check(await Scalar($"SELECT count(*) FROM auth_sessions WHERE user_id IN ('{a}','{b}') AND revoked") == 2, "all selected sessions revoked");
    Check(await Scalar($"SELECT count(*) FROM auth_sessions WHERE user_id='{peer}' AND NOT revoked") == 1, "other member session unchanged");
    Check(await Scalar($"SELECT count(*) FROM friend_presence WHERE user_id IN ('{a}','{b}')") == 0 && await Scalar("SELECT count(*) FROM friend_challenges WHERE state='closed'") == 1, "presence cleared and challenges closed");
    Check(await Scalar("SELECT count(*) FROM admin_audit_logs WHERE action='MemberDeleted'") == 2, "one audit per deleted member");
    Check((await read.Members("delete_a", null, 1)).Rows.Count == 0 && (await read.Members(a.ToString(), "disabled", 1)).Rows.Count == 0, "deleted members excluded from every list filter");
    await Reject(async () => { await read.Member(a); }, 404, "deleted detail cannot be edited");
    await Reject(async () => { await new GameData(db).Profile(a); }, 404, "deleted public profile unavailable");
    Check(await Scalar($"SELECT coin FROM wallets WHERE user_id='{a}'") == 100 && await Scalar($"SELECT jem FROM wallets WHERE user_id='{a}'") == 50, "balances preserved");
    Check(await Scalar($"SELECT count(*) FROM shop_user_inventory WHERE user_id='{a}'") == 1 && await Scalar($"SELECT count(*) FROM shop_purchases WHERE user_id='{a}'") == 1 && await Scalar($"SELECT count(*) FROM wallet_ledger WHERE user_id='{a}'") == 1, "owned items, purchases and financial history preserved");
    Check((await new Friends(db,new GameData(db)).List(peer)).Rows.Count() == 0, "deleted friend hidden from other member");
    await Reject(() => service.Rename(owner,a,"renamed","delete_a",null,"rename"),404,"cannot rename deleted member");
    await Reject(() => service.AdjustWallet(owner,a,"Coin","credit",1,Guid.NewGuid(),null),404,"cannot credit deleted member");
    Guid replacement = Guid.NewGuid();
    await Sql($"INSERT INTO users(id,login_id,normalized_login,password_hash,nickname) VALUES('{replacement}','delete_a','DELETE_A','unused','replacement')");
    Check(await Scalar("SELECT count(*) FROM users WHERE normalized_login='DELETE_A'") == 1, "original login ID available for a new account");
    try
    {
        new PostgresSettlementStore(db).Create(new RegisteredMatch { Registration = new(Guid.NewGuid().ToString("N"),a.ToString(),peer.ToString(),"deleted","peer",3,1,10,CoinPolicy.EntryPool,"{}") });
        throw new Exception("Expected deleted participant denial");
    }
    catch(ApiFault e) { Check(e.Status == 409,"new match creation rejects removed participants under user locks"); }
    Console.WriteLine($"Passed {checks} checks; no real members deleted.");
}
finally { await using var drop = root.CreateCommand($"DROP SCHEMA {schema} CASCADE"); await drop.ExecuteNonQueryAsync(); }
