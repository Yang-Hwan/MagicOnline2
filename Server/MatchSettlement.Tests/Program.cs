using System.Text.Json;
using Assets.Scripts.Prack.BackSys;
using MagicOnline.MatchSettlement;

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
    static void Main()
    {
        var t = Match(start: false);
        Check(!t.service.Ready(t.id, "A") && t.store.Users["A"].Coin == 100, "one ready participant cannot debit coins");
        Check(t.service.Ready(t.id, "B") && t.store.Users["A"].Coin == 90 && t.store.Users["B"].Coin == 90, "both entry fees debited atomically at start");
        t.service.Ready(t.id, "A"); t.service.Ready(t.id, "B");
        Check(t.store.Users["A"].Coin == 90 && t.store.Users["B"].Coin == 90, "repeated start does not debit again");
        Check(t.service.ConfirmResult(t.id, "A", new(3, 1, 0)) == null && t.store.Users["A"].Ball_3_Win == 0, "one result report cannot settle");
        var receipt = t.service.ConfirmResult(t.id, "B", new(3, 1, 0));
        Check(t.store.Users["A"].Coin == 110 && t.store.Users["B"].Coin == 90 && t.store.Users["A"].Ball_3_Win == 1 &&
            t.store.Users["B"].Ball_3_Lose == 1 && t.store.Users["A"].Ball_4_Win == 0, "three-ball pot and separate stats applied together");
        Check(receipt.PlayerA.EntryCoin == 10 && receipt.PlayerA.RewardCoin == 20 && receipt.PlayerA.CoinDelta == 10 &&
            receipt.PlayerB.CoinDelta == -10 && receipt.Match.SettlementStatus == "Applied", "receipt and both private history projections record exact movements");
        Parallel.For(0, 32, _ => t.service.Surrender(t.id, "A"));
        Check(t.store.Users["A"].Coin == 110 && t.store.Users["A"].Ball_3_Win == 1, "parallel retries cannot pay or increment twice");
        t = Match(4); t.service.ConfirmResult(t.id, "A", new(2, 2, -1)); receipt = t.service.ConfirmResult(t.id, "B", new(2, 2, -1));
        Check(t.store.Users["A"].Coin == 100 && t.store.Users["B"].Coin == 100 && t.store.Users["A"].Ball_4_Draw == 1 &&
            t.store.Users["B"].Ball_4_Draw == 1 && t.store.Users["A"].Ball_3_Draw == 0, "four-ball draw refunds both fees and counts only four-ball draws");
        t = Match(); t.clock.Now += 44999; t.service.Heartbeat(t.id, "A");
        Check(t.service.EvaluateDisconnect(t.id, "A") == null, "disconnect cannot settle before heartbeat timeout plus reconnect grace");
        t.clock.Now++; receipt = t.service.EvaluateDisconnect(t.id, "A");
        Check(receipt.WinnerUserId == "A" && t.store.Users["B"].Ball_3_Lose == 1 && t.store.Users["A"].Coin == 110, "present player wins after server grace expiry");
        t = Match(); t.clock.Now += 30000; t.service.Heartbeat(t.id, "B"); t.clock.Now += 16000; t.service.Heartbeat(t.id, "A");
        Check(t.service.EvaluateDisconnect(t.id, "A") == null, "reconnected opponent resets disconnect deadline");
        t = Match(); t.clock.Now += 45000; receipt = t.service.EvaluateDisconnect(t.id, "A");
        Check(receipt.PlayerA.Result == "Void" && t.store.Users["A"].Coin == 100 && t.store.Users["B"].Coin == 100 &&
            t.store.Users["A"].Ball_3_Draw == 0, "both missing refunds fees without giving either player a win or draw");
        t = Match(); receipt = t.service.Surrender(t.id, "A");
        Check(receipt.WinnerUserId == "B" && t.store.Users["B"].Coin == 110, "authenticated surrender makes surrendering player lose");
        t = Match(start: false); receipt = t.service.Surrender(t.id, "B");
        Check(receipt.PlayerA.Result == "Void" && t.store.Users["A"].Coin == 100 && t.store.Users["A"].Ball_3_Win == 0, "cancel before start does not debit coins or count stats");
        t = Match();
        Throws<UnauthorizedAccessException>(() => t.service.Surrender(t.id, "C"), "nonparticipant cannot settle another match");
        t.service.ConfirmResult(t.id, "A", new(3, 1, 0)); t.service.ConfirmResult(t.id, "B", new(1, 3, 1));
        t.clock.Now += 45000; t.service.Heartbeat(t.id, "A");
        Check(t.service.EvaluateDisconnect(t.id, "A") == null && t.store.Users["A"].Ball_3_Win == 0, "conflicting reports stay unsettled for review");
        Throws<InvalidOperationException>(() => t.service.ConfirmResult(t.id, "A", new(1, 3, 1)), "previously submitted result cannot be replaced");
        t = Match(); t.service.ConfirmResult(t.id, "A", new(3, 1, 0)); t.store.FailCommit = true;
        Throws<IOException>(() => t.service.ConfirmResult(t.id, "B", new(3, 1, 0)), "injected commit failure is surfaced");
        Check(t.store.Users["A"].Coin == 90 && t.store.Users["B"].Coin == 90 && t.store.Users["A"].Ball_3_Win == 0, "failed commit rolls back rewards and stats together");
        receipt = t.service.ConfirmResult(t.id, "B", new(3, 1, 0));
        Check(t.store.Users["A"].Coin == 110 && t.store.Users["A"].Ball_3_Win == 1, "retry after rollback applies once");
        t = Match(); t.store.Users["A"].Ball_3_Win = int.MaxValue; t.service.ConfirmResult(t.id, "A", new(3, 1, 0));
        Throws<OverflowException>(() => t.service.ConfirmResult(t.id, "B", new(3, 1, 0)), "stat overflow rejects settlement");
        Check(t.store.Users["A"].Coin == 90 && t.store.Users["B"].Coin == 90, "overflow cannot partially pay pot");
        t = Match(start: false); t.store.Users["B"].Coin = 5; t.service.Ready(t.id, "A");
        Throws<InvalidOperationException>(() => t.service.Ready(t.id, "B"), "insufficient entry balance rejects start");
        Check(t.store.Users["A"].Coin == 100 && t.store.Users["B"].Coin == 5, "failed entry cannot debit only one player");
        t = Match(); string otherMatch = Guid.NewGuid().ToString("N"); t.service.Register(Registration(otherMatch, other: "C"));
        t.service.Ready(otherMatch, "A"); t.service.Ready(otherMatch, "C");
        Parallel.Invoke(() => t.service.Surrender(t.id, "B"), () => t.service.Surrender(otherMatch, "C"));
        Check(t.store.Users["A"].Coin == 120 && t.store.Users["A"].Ball_3_Win == 2, "different matches sharing a user do not lose wallet or stat updates");
        Throws<ArgumentException>(() => t.service.Register(Registration(Guid.NewGuid().ToString("N")) with { CoinPolicy = CoinPolicy.Unconfigured }), "unconfigured coin policy cannot register");
        Throws<ArgumentException>(() => t.service.Register(Registration(t.id)), "duplicate match identity is rejected by store");
        receipt = t.service.Surrender(t.id, "A"); long previousCoin = t.store.Users["A"].Coin;
        t.clock.Now += (long)TimeSpan.FromDays(200).TotalMilliseconds;
        t.store.DeleteHistoryProjections(t.id);
        t.service.Surrender(t.id, "B");
        Check(t.store.Users["A"].Coin == previousCoin && t.store.Users["A"].Ball_3_Win == 2, "permanent receipt prevents old requests from reapplying after history retention");
        Check(FunctionRequestGate.Handle("", "{\"Action\":\"Health\"}").Contains("Unauthenticated"), "function rejects missing SDK caller identity");
        string health = FunctionRequestGate.Handle("A", "{\"Action\":\"Health\"}");
        Check(health.Contains("\"SettlementEnabled\":false") && health.Contains("\"StorageConnected\":false") && !health.Contains("\"A\""), "health reports disabled settlement without exposing caller identity");
        Check(FunctionRequestGate.Handle("A", "{\"Action\":\"Surrender\"}").Contains("SettlementStorageNotConfigured"), "unconnected function refuses money-changing commands");
        Check(FunctionRequestGate.Handle("A", "{\"Action\":\"Health\",\"UserId\":\"B\"}").Contains("InvalidRequest"), "request cannot override SDK caller identity");
        Check(FunctionRequestGate.Handle("A", "{\"Action\":\"Health\",\"Action\":\"Ready\"}").Contains("InvalidRequest"), "duplicate action is rejected");
        Check(FunctionRequestGate.Handle("A", "[]").Contains("InvalidRequest") && FunctionRequestGate.Handle("A", "bad-json").Contains("InvalidRequest"), "nonobject and malformed requests are rejected");
        Check(FunctionRequestGate.Handle("A", new string('x', 4097)).Contains("InvalidRequest"), "oversized function request is rejected");
        Check(FunctionRequestGate.Handle("A", "{\"Action\":\"Pay\"}").Contains("UnsupportedAction"), "unknown function command is rejected");
        Check(BackendFunction.GameDataProbe.Handle("A", "{\"Action\":\"ProbeSeed\"}").Contains("ProbeCallerDenied"), "unlisted caller cannot contact diagnostic storage");
        Check(!BackendFunction.GameDataProbe.IsProbeRequest(new string('x', 4097)) && !BackendFunction.GameDataProbe.IsProbeRequest("bad-json"), "invalid oversized diagnostic input cannot enter the probe route");
        var dateRows = "{\"rows\":[{\"inDate\":{\"S\":\"2026-10-08T13:57:58.120Z\"}},{\"inDate\":\"2026-10-08T13:57:58.121Z\"}]}";
        Check(BackendFunction.GameDataProbe.ReadStorageRowId(dateRows, 0) == "2026-10-08T13:57:58.120Z" && BackendFunction.GameDataProbe.ReadStorageRowId(dateRows, 1) == "2026-10-08T13:57:58.121Z", "storage row ids preserve ISO text and distinct milliseconds");
        Check(BackendFunction.GameDataProbe.ReadProbePeerId("{\"OtherUserId\":\"2026-09-18T09:01:47.894Z\"}") == "2026-09-18T09:01:47.894Z", "probe peer ids preserve exact ISO request text");
        Check(BackendFunction.GameDataProbe.ReadSingleStorageRowId("{\"row\":{\"inDate\":{\"S\":\"2026-10-09T04:31:29.247Z\"}}}") == "2026-10-09T04:31:29.247Z", "GetV2 single row response is accepted");
        Console.WriteLine($"{passed} checks passed; production settlement storage remains unconfigured.");
    }
}
