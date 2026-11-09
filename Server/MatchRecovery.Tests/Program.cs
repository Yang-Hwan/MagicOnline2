using MagicOnline.GameApi;
using MagicOnline.MatchSettlement;
using Microsoft.Extensions.Configuration;
using Npgsql;

var config = new ConfigurationBuilder().AddUserSecrets<Database>().Build();
var cs = new NpgsqlConnectionStringBuilder(config.GetConnectionString("GameDb"));
if (cs.Host is not ("localhost" or "127.0.0.1")) throw new Exception("Local DB only");
await using var root = NpgsqlDataSource.Create(cs.ConnectionString);
string schema = "match_recovery_test_" + Guid.NewGuid().ToString("N");
await using (var create = root.CreateCommand($"CREATE SCHEMA {schema}")) await create.ExecuteNonQueryAsync();
try
{
    cs.SearchPath = schema;
    await using var source = NpgsqlDataSource.Create(cs.ConnectionString);
    async Task Sql(string sql) { await using var cmd = source.CreateCommand(sql); await cmd.ExecuteNonQueryAsync(); }
    async Task<long> Count(string sql) { await using var cmd = source.CreateCommand(sql); return Convert.ToInt64(await cmd.ExecuteScalarAsync()); }
    await Sql(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Migrations", "001_initial.sql")));
    await Sql(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Migrations", "006_registration_reward.sql")));
    await Sql(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Migrations", "008_user_post_reward.sql")));
    string a = Guid.NewGuid().ToString(), b = Guid.NewGuid().ToString();
    foreach (var id in new[] { a, b })
        await Sql($"INSERT INTO users(id,login_id,normalized_login,password_hash,nickname) VALUES('{id}','{id}','{id}','unused-test-hash','test'); INSERT INTO wallets(user_id,coin) VALUES('{id}',100); INSERT INTO user_stats(user_id) VALUES('{id}');");
    var store = new PostgresSettlementStore(new Database(source));
    var service = new SettlementService(store, new Clock());
    int checks = 0;
    void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; Console.WriteLine("PASS: " + message); }
    foreach (int ball in new[] {3,4})
    foreach (int target in new[] {5,9})
    {
        var hall = new Assets.Scripts.Prack.BackSys.ChartData.SkillMatchData {
            HallIdx=1, MatchBall=ball, TargetHit=target, MatchTotMin=5, MatchCushion=ball==3 ? 3 : 0
        };
        var matchConfig = Assets.Scripts.Often.MatchConfiguration.Online(hall,"A","B",0,0,30);
        var rules = Assets.Scripts.Prack.BackSys.GameData.MatchRulesContract.FromHall(hall,0);
        Check(matchConfig.TargetScore == target && rules.TargetScore == target,
            $"{ball}-ball runtime and server use chart TargetHit={target}");
    }
    string Register()
    {
        string id = Guid.NewGuid().ToString("N");
        service.Register(new VerifiedRegistration(id,a,b,"A","B",4,1,10,CoinPolicy.EntryPool,"{}"));
        return id;
    }
    string disputed = Register(); service.Ready(disputed,a); service.Ready(disputed,b);
    service.ConfirmResult(disputed,a,new ResultReport(3,0,0));
    Check(await Count("SELECT count(*) FROM match_players WHERE active") == 2,"one-sided report keeps live reservation");
    Check(service.ConfirmResult(disputed,b,new ResultReport(2,0,0)) == null,"reproduced 3:0 versus 2:0 dispute has no payout");
    Check(await Count("SELECT count(*) FROM match_players WHERE active") == 0,"both participants released atomically on dispute");
    Check(await Count("SELECT count(*) FROM matches WHERE (state->>'Disputed')::boolean AND state->'ReportA' IS NOT NULL AND state->'ReportB' IS NOT NULL") == 1,"reports retained for review");
    Check(await Count("SELECT sum(coin) FROM wallets") == 180 && await Count("SELECT count(*) FROM settlement_receipts") == 0,"entry funds held without fabricated settlement");
    string next = Register(); service.Ready(next,a); service.Ready(next,b);
    Check(await Count($"SELECT count(*) FROM match_players WHERE active AND match_id='{next}'") == 2,"same pair can start next match");
    service.Heartbeat(disputed,a);
    Check(!service.Ready(disputed,b),"late ready cannot restart disputed match");
    service.ConfirmResult(disputed,a,new ResultReport(3,0,0));
    service.EvaluateDisconnect(disputed,b);
    Check(await Count($"SELECT count(*) FROM match_players WHERE active AND match_id='{disputed}'") == 0,"late heartbeat/report/disconnect cannot reacquire slots");
    Check(await Count($"SELECT count(*) FROM match_players WHERE active AND match_id='{next}'") == 2,"old retries do not affect new match");
    service.ConfirmResult(next,a,new ResultReport(3,1,0)); service.ConfirmResult(next,b,new ResultReport(3,1,0));
    Check(await Count("SELECT count(*) FROM settlement_receipts") == 1 && await Count("SELECT sum(coin) FROM wallets") == 180,"next match settles once while disputed funds remain held");
    string timeout = Register(); service.Ready(timeout,a); service.Ready(timeout,b);
    bool invalidDrawRejected = false;
    try { service.ConfirmResult(timeout,a,new ResultReport(6,0,-1)); }
    catch (ArgumentException) { invalidDrawRejected = true; }
    Check(invalidDrawRejected,"unequal scores cannot be settled as a draw");
    Check(service.ConfirmResult(timeout,a,new ResultReport(3,2,0)) == null,"timeout victory is not final before peer confirmation");
    Check(service.ConfirmResult(timeout,b,new ResultReport(3,3,-1)) == null,"3:2 victory versus 3:3 draw cannot produce a receipt");
    Check(await Count($"SELECT count(*) FROM matches WHERE id='{timeout}' AND (state->>'Disputed')::boolean") == 1,
        "both clients read the same disputed state");
    Check(await Count($"SELECT count(*) FROM match_players WHERE match_id='{timeout}' AND active") == 0,
        "timeout dispute does not block the next room");
    string draw = Register(); service.Ready(draw,a); service.Ready(draw,b);
    service.ConfirmResult(draw,a,new ResultReport(3,3,-1));
    Check(service.ConfirmResult(draw,b,new ResultReport(3,3,-1)) != null,"matching timeout draw is confirmed");
    string latestDispute = Register(); service.Ready(latestDispute,a); service.Ready(latestDispute,b);
    service.ConfirmResult(latestDispute,a,new ResultReport(4,2,0));
    Check(service.ConfirmResult(latestDispute,b,new ResultReport(3,2,0)) == null,
        "4:2 versus 3:2 remains a dispute even when both name the same winner");
    async Task<string> CoinRound() {
        await Sql($"UPDATE wallets SET coin=CASE WHEN user_id='{a}' THEN 10000 ELSE 1800 END;");
        var id=Guid.NewGuid().ToString("N");
        var rules=new Assets.Scripts.Prack.BackSys.GameData.MatchRulesContract { Version=2,TargetScore=5,EntryCoin=1000,CoinPolicy=2 };
        service.Register(new(id,a,b,"A","B",3,1,1000,CoinPolicy.ScoreTransfer,"{}",rules));
        service.Ready(id,a,rules);service.Ready(id,b,rules);
        Check(await Count($"SELECT coin FROM wallets WHERE user_id='{a}'")==10000,"new policy reserves without changing DB wallet");
        Check(await Count($"SELECT count(*) FROM wallet_ledger WHERE request_key='{id}:Entry'")==0,"no entry wallet write for new policy");
        return id;
    }
    string coinMatch=await CoinRound();
    string coinEvents="1,0,1,1;2,1,1,1;3,1,2,1;4,0,1,1;5,0,2,1;6,0,3,1;";
    service.CheckpointCoins(coinMatch,a,new(coinEvents));service.CheckpointCoins(coinMatch,b,new(coinEvents));
    Check(await Count($"SELECT coin FROM wallets WHERE user_id='{b}'")==1800,"checkpoints leave DB balance unchanged");
    var restarted=new SettlementService(new PostgresSettlementStore(new Database(source)),new Clock());
    var coinsReport=new ResultReport(4,2,0,coinEvents);
    await Task.WhenAll(Task.Run(()=>restarted.ConfirmResult(coinMatch,a,coinsReport)),Task.Run(()=>restarted.ConfirmResult(coinMatch,b,coinsReport)));
    restarted.ConfirmResult(coinMatch,a,coinsReport);
    Check(await Count($"SELECT coin FROM wallets WHERE user_id='{a}'")==11710 && await Count($"SELECT coin FROM wallets WHERE user_id='{b}'")==0,"DB atomic bankruptcy payout 11710 / 0 after restart");
    Check(await Count($"SELECT count(*) FROM wallet_ledger WHERE request_key='{coinMatch}:Settlement'")==2,"one settlement ledger per player despite retries");
    coinMatch=await CoinRound();
    service.CheckpointCoins(coinMatch,a,new("1,0,1,1;"));service.CheckpointCoins(coinMatch,b,new("1,0,1,1;"));
    restarted.ConfirmResult(coinMatch,a,new(1,1,-1,"1,0,1,1;"));restarted.ConfirmResult(coinMatch,b,new(1,1,-1,"1,0,1,1;"));
    Check(await Count($"SELECT coin FROM wallets WHERE user_id='{a}'")==9950 && await Count($"SELECT coin FROM wallets WHERE user_id='{b}'")==1750,"DB draw cancels scoring transfers and charges entry commission");
    coinMatch=await CoinRound();
    service.CheckpointCoins(coinMatch,a,new("1,0,1,1;"));service.CheckpointCoins(coinMatch,b,new("1,0,1,1;"));
    restarted.Surrender(coinMatch,b);
    Check(await Count($"SELECT coin FROM wallets WHERE user_id='{a}'")==11140 && await Count($"SELECT coin FROM wallets WHERE user_id='{b}'")==600,"DB surrender retains agreed scoring transfer after restart");
    Console.WriteLine($"Passed {checks} checks; live accounts unchanged.");
}
finally { await using var drop = root.CreateCommand($"DROP SCHEMA {schema} CASCADE"); await drop.ExecuteNonQueryAsync(); }
sealed class Clock : IServerClock { public long UtcMilliseconds => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); }
