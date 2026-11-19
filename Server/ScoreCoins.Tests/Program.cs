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
        var bank = new ScoreCoinLedger(10000,10000,1000,5);
        bank.Add(1,0,1,2,true);Check(bank.NetA==400,"bank pays exactly twice the normal 200");
        bank.Add(2,0,3,1);Check(bank.NetA==1200,"next streak follows two-point bank");
        var replayBank=ScoreCoinLedger.Replay(10000,10000,1000,5,bank.Events);
        Check(replayBank.NetA==bank.NetA && replayBank.Events==bank.Events,"bank event replay diverged");
        var oldDouble=new ScoreCoinLedger(10000,10000,1000,5);oldDouble.Add(1,0,1,2);
        Check(oldDouble.NetA==600,"legacy four-ball two-point coins changed");
        Throws<ArgumentException>(()=>bank.Add(3,0,4,1,true),"bank with one point rejected");
        var poor=new ScoreCoinLedger(10000,1250,1000,5);poor.Add(1,0,1,2,true);
        Check(poor.NetA==250 && poor.BustWinner==0 && ScoreCoinLedger.Replay(10000,1250,1000,5,poor.Events).NetA==250,"bank bankruptcy replay incorrect");
        Check(ScoreCoinLedger.QualifiesQuickShot(0,30000) && ScoreCoinLedger.QualifiesQuickShot(9000,30000),"quick-shot start and exact 30% boundary accepted");
        Check(!ScoreCoinLedger.QualifiesQuickShot(9001,30000) && !ScoreCoinLedger.QualifiesQuickShot(-1,30000) && !ScoreCoinLedger.QualifiesQuickShot(0,0),"late/invalid quick-shot timing rejected");
        var quick=new ScoreCoinLedger(10000,10000,1000,5);
        quick.Add(1,0,1,1,false,9000,30000);
        Check(quick.NetA==240 && quick.LastQuickBonus==40 && quick.LastAward==240,"normal quick shot pays 200 + 40");
        quick.Add(2,0,2,2,true,0,30000);
        Check(quick.LastAward==960 && quick.LastQuickBonus==160 && quick.NetA==1200,"bank combo is calculated before adding 20 percent");
        var quickPeer=ScoreCoinLedger.Replay(10000,10000,1000,5,quick.Events);
        Check(quickPeer.NetA==quick.NetA && quickPeer.LastQuickBonus==quick.LastQuickBonus && quickPeer.Events==quick.Events,"quick shot peer/server replay identical");
        string acceptedQuick=quick.Events;
        Throws<ArgumentException>(()=>quick.Add(2,0,4,1,false,5000,30000),"duplicate quick-shot payout rejected");
        Check(quick.Events==acceptedQuick,"duplicate does not alter quick history");
        Throws<ArgumentException>(()=>quick.Add(3,0,4,1,false,9001,30000),"late quick bonus rejected");
        Throws<ArgumentException>(()=>ScoreCoinLedger.Replay(10000,10000,1000,5,quick.Events,true,false),"old/excluded match cannot use quick coins");
        var quickFour=new ScoreCoinLedger(10000,10000,1000,5);quickFour.Add(1,0,1,2,false,3000,30000);
        Check(quickFour.NetA==720 && quickFour.LastQuickBonus==120,"four-ball quick bonus is 20 percent of complete scoring reward");
        var quickPoor=new ScoreCoinLedger(10000,1225,1000,5);quickPoor.Add(1,0,1,1,false,9000,30000);
        Check(quickPoor.LastQuickBonus==25 && quickPoor.BalanceB==0 && quickPoor.BustWinner==0,"quick bonus caps at payer balance");
        Check(ScoreCoinLedger.Replay(10000,1225,1000,5,quickPoor.Events).NetA==225,"partial quick bonus replays canonically");
        Check(ScoreCoinLedger.QuickBonus(9)==1 && ScoreCoinLedger.QuickBonus(4)==0,"fractional quick coins round down");
        var mixed=new ScoreCoinLedger(10000,10000,1000,5);mixed.Add(1,0,1,1);mixed.Add(2,0,2,2,true);mixed.Add(3,1,1,1,false,5000,30000);
        Check(ScoreCoinLedger.Replay(10000,10000,1000,5,mixed.Events).Events==mixed.Events,"legacy/ bank/ quick formats coexist");
        var large = new ScoreCoinLedger(100000000000,100000000000,10000000000,5);
        for(int i=1;i<=6 && large.BustWinner<0;i++) large.Add(i,0,i,1);
        Check(large.BalanceB==0 && large.BalanceA==180000000000,"large wallets saturate safely");
        (SettlementService service,TestStore store,TestClock clock,string id) Start(int target=5) {
            var store=new TestStore();store.Users["A"].Coin=10000;store.Users["B"].Coin=1800;
            var clock=new TestClock();var service=new SettlementService(store,clock);string id=Guid.NewGuid().ToString("N");
            var rules=new MatchRulesContract { Version=MatchRulesContract.CurrentVersion,TargetScore=target,CoinPolicy=2,EntryCoin=1000 };
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
        var over=Start(7);var overCoins=new ScoreCoinLedger(10000,1800,1000,7);
        overCoins.Add(1,0,1,2,true);
        var overReport=new ResultReport(8,0,0,overCoins.Events);
        Check(over.service.ConfirmResult(over.id,"A",overReport)==null,"overshoot waits for both reports");
        Check(over.service.ConfirmResult(over.id,"B",overReport)!=null && over.store.Users["B"].Coin==516,"8/7 settlement preserves full bank reward");
        Throws<ArgumentException>(()=>ScoreCoinLedger.Replay(10000,1800,1000,7,overCoins.Events,false),"four-ball cannot claim bank bonus");
        var quickMatch=Start();var quickCoins=new ScoreCoinLedger(10000,1800,1000,5);quickCoins.Add(1,0,1,1,false,9000,30000);
        quickMatch.service.CheckpointCoins(quickMatch.id,"A",new(quickCoins.Events));quickMatch.service.CheckpointCoins(quickMatch.id,"B",new(quickCoins.Events));
        var quickReport=new ResultReport(5,1,0,quickCoins.Events);
        Check(quickMatch.service.ConfirmResult(quickMatch.id,"A",quickReport)==null,"quick bonus waits for both authenticated results");
        Check(quickMatch.service.ConfirmResult(quickMatch.id,"B",quickReport)!=null && quickMatch.store.Users["B"].Coin==560,"server settlement includes quick-shot bonus");
        quickMatch.service.ConfirmResult(quickMatch.id,"A",quickReport);
        Check(quickMatch.store.Users["B"].Coin==560,"quick-shot result retry cannot pay twice");
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
