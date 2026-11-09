using System.Text.Json;
using Assets.Scripts.Prack.BackSys;
using MagicOnline.MatchSettlement;
using Assets.Scripts.Prack.BackSys.GameData;

// Simulates the REQUIRED transaction semantics. This is not a production storage adapter.
sealed class TestStore : ISettlementStore
{
    readonly object gate = new();
    readonly Dictionary<string, RegisteredMatch> matches = new();
    readonly Dictionary<string, SettlementReceipt> receipts = new();
    public readonly Dictionary<string, UserMainData> Users = new() {
        ["A"] = new() { Coin = 100 }, ["B"] = new() { Coin = 100 }, ["C"] = new() { Coin = 100 }
    };
    public bool FailCommit;
    public void DeleteHistoryProjections(string id)
    {
        lock (gate) {
            receipts[id].Match = null; receipts[id].PlayerA = null; receipts[id].PlayerB = null;
            matches[id].Registration = matches[id].Registration with { NameA = "", NameB = "", RulesSnapshot = "" };
        }
    }
    static readonly JsonSerializerOptions json = new() { IncludeFields = true };
    static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, json), json);
    public void Create(RegisteredMatch match)
    {
        lock (gate) {
            if (!Users.ContainsKey(match.Registration.UserA) || !Users.ContainsKey(match.Registration.UserB)) throw new InvalidOperationException();
            matches.Add(match.Registration.MatchId, Copy(match));
        }
    }
    public T Execute<T>(string matchId, Func<SettlementContext, T> operation)
    {
        lock (gate) {
            var match = Copy(matches[matchId]); var r = match.Registration;
            var context = new SettlementContext { Match = match, UserA = Copy(Users[r.UserA]), UserB = Copy(Users[r.UserB]),
                Receipt = receipts.TryGetValue(matchId, out var receipt) ? Copy(receipt) : null };
            var result = operation(context);
            if (FailCommit) { FailCommit = false; throw new IOException("Injected atomic commit failure"); }
            matches[matchId] = context.Match;
            Users[r.UserA] = context.UserA; Users[r.UserB] = context.UserB;
            if (context.Receipt != null) receipts[matchId] = context.Receipt;
            return result;
        }
    }
}
sealed class TestClock : IServerClock
{
    public long Now = new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
    public long UtcMilliseconds => Now;
}
static class Program
{
    static int passed;
    static void Check(bool condition, string label) { if (!condition) throw new Exception(label); passed++; Console.WriteLine("PASS " + label); }
    static void Throws<T>(Action operation, string label) where T : Exception
    {
        try { operation(); } catch (T) { Check(true, label); return; }
        throw new Exception("Expected " + typeof(T).Name + ": " + label);
    }
    static (SettlementService service, TestStore store, TestClock clock, string id) Match(int ball = 3, bool start = true)
    {
        var store = new TestStore(); var clock = new TestClock(); var service = new SettlementService(store, clock);
        string id = Guid.NewGuid().ToString("N");
        service.Register(Registration(id, ball));
        if (start) { service.Ready(id, "A"); service.Ready(id, "B"); }
        return (service, store, clock, id);
    }
    static VerifiedRegistration Registration(string id, int ball = 3, string other = "B") =>
        new(id, "A", other, "Alice", "Bob", ball, 1, 10, CoinPolicy.EntryPool, "{\"TargetHit\":3}");
    static void Main() { ScoreCoins(); Console.WriteLine($"{passed} score coin checks passed"); }
    static void ScoreCoins()
    {
        var ledger = new ScoreCoinLedger(10000, 1800, 1000, 5);
        var expected = new long[,] { {9200,600,200}, {9000,800,0}, {8600,1200,-400}, {8800,1000,-200}, {9200,600,200}, {9800,0,800} };
        int[] seats = {0,1,1,0,0,0}, streaks = {1,1,2,1,2,3};
        for(int i=0;i<6;i++) {
            ledger.Add(i+1,seats[i],streaks[i],1);
            Check(ledger.BalanceA==expected[i,0] && ledger.BalanceB==expected[i,1] && ledger.NetA==expected[i,2], "score coin example step "+(i+1));
        }
        Check(ledger.BustWinner==0, "partial third combo causes bankruptcy");
        Throws<ArgumentException>(()=>ledger.Add(7,0,4,1), "no coins after bankruptcy");
        var streak = new ScoreCoinLedger(10000,10000,1000,5);
        streak.Add(1,0,1,1);streak.Add(2,0,2,1);streak.Add(3,0,3,1);
        Check(streak.NetA==1400,"three combo pays 200+400+800");
        streak.Add(5,0,1,1); Check(streak.NetA==1600,"new turn restarts combo");
        var large = new ScoreCoinLedger(100000000000,100000000000,10000000000,5);
        for(int i=1;i<=6 && large.BustWinner<0;i++) large.Add(i,0,i,1);
        Check(large.BalanceB==0 && large.BalanceA==180000000000,"large wallets saturate safely");
        (SettlementService service,TestStore store,TestClock clock,string id) Start() {
            var store=new TestStore();store.Users["A"].Coin=10000;store.Users["B"].Coin=1800;
            var clock=new TestClock();var service=new SettlementService(store,clock);string id=Guid.NewGuid().ToString("N");
            var rules=new MatchRulesContract { Version=MatchRulesContract.CurrentVersion,TargetScore=5,CoinPolicy=2,EntryCoin=1000 };
            service.Register(new(id,"A","B","Alice","Bob",3,1,1000,CoinPolicy.ScoreTransfer,"{}",rules));
            service.Ready(id,"A",rules);service.Ready(id,"B",rules);
            Check(store.Users["A"].Coin==10000 && store.Users["B"].Coin==1800,"start reserves without wallet mutation");
            return (service,store,clock,id);
        }
        var t=Start();
        t.service.CheckpointCoins(t.id,"A",new(ledger.Events));t.service.CheckpointCoins(t.id,"B",new(ledger.Events));
        Check(t.store.Users["A"].Coin==10000,"score checkpoint does not update wallet");
        // Bankruptcy can win even when this player's points are lower.
        var report=new ResultReport(4,5,0,ledger.Events);
        Check(t.service.ConfirmResult(t.id,"A",report)==null,"one report cannot settle new coin policy");
        var receipt=t.service.ConfirmResult(t.id,"B",report);
        Check(t.store.Users["A"].Coin==11710 && t.store.Users["B"].Coin==0,"example settles 11710 / 0 with 90 fee");
        t.service.ConfirmResult(t.id,"A",report);t.service.Surrender(t.id,"B");
        Check(t.store.Users["A"].Coin==11710 && receipt.PlayerA.CoinDelta==1710,"duplicate settlement remains idempotent");
        t=Start();var one="1,0,1,1;";
        t.service.CheckpointCoins(t.id,"A",new(one));t.service.CheckpointCoins(t.id,"B",new(one));
        t.service.ConfirmResult(t.id,"A",new(1,1,-1,one));t.service.ConfirmResult(t.id,"B",new(1,1,-1,one));
        Check(t.store.Users["A"].Coin==9950 && t.store.Users["B"].Coin==1750,"draw rolls back transfers and charges each entry 5 percent");
        t=Start();t.service.CheckpointCoins(t.id,"A",new(one));t.service.CheckpointCoins(t.id,"B",new(one));
        t.service.Surrender(t.id,"B");
        Check(t.store.Users["A"].Coin==11140 && t.store.Users["B"].Coin==600,"surrender settles agreed gains and normal loss");
        t=Start();t.service.CheckpointCoins(t.id,"A",new(one));t.service.CheckpointCoins(t.id,"B",new(one));
        t.clock.Now+=46000;t.service.Heartbeat(t.id,"A");t.service.EvaluateDisconnect(t.id,"A");
        Check(t.store.Users["A"].Coin==11140 && t.store.Users["B"].Coin==600,"disconnect uses durable agreed scoring checkpoint");
        t=Start();t.service.CheckpointCoins(t.id,"A",new(one));
        Throws<ArgumentException>(()=>t.service.CheckpointCoins(t.id,"A",new("1,1,1,1;")),"changing coin history rejected");
        Check(!t.service.CheckpointCoins(t.id,"B",new("1,1,1,1;")),"different client coin ledgers held for review");
        Check(t.store.Users["A"].Coin==10000 && t.store.Users["B"].Coin==1800,"disputed coins not applied");
    }
}
