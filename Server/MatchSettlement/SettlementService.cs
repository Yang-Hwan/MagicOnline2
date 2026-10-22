using Assets.Scripts.Prack.BackSys;
using Assets.Scripts.Prack.BackSys.GameData;

namespace MagicOnline.MatchSettlement;

// Server-only domain logic, used by the authenticated local GameApi adapter.
public sealed class SettlementService
{
    readonly ISettlementStore store;
    readonly IServerClock clock;
    readonly long heartbeatTimeoutMs, reconnectGraceMs;
    const long MaxExactCoin = 9_000_000_000_000_000L;

    public SettlementService(ISettlementStore store, IServerClock clock,
        int heartbeatTimeoutSeconds = 15, int reconnectGraceSeconds = 30)
    {
        if (heartbeatTimeoutSeconds < 1 || reconnectGraceSeconds < 1) throw new ArgumentOutOfRangeException();
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        heartbeatTimeoutMs = heartbeatTimeoutSeconds * 1000L;
        reconnectGraceMs = reconnectGraceSeconds * 1000L;
    }

    public void Register(VerifiedRegistration r)
    {
        if (r == null || !Guid.TryParseExact(r.MatchId, "N", out _) || string.IsNullOrWhiteSpace(r.UserA) ||
            string.IsNullOrWhiteSpace(r.UserB) || r.UserA == r.UserB || r.Ball is not (3 or 4) || r.HallId < 1 ||
            r.CoinPolicy == CoinPolicy.Unconfigured || !Enum.IsDefined(r.CoinPolicy) ||
            r.EntryCoin < 0 || r.EntryCoin > MaxExactCoin / 2 || string.IsNullOrWhiteSpace(r.RulesSnapshot))
            throw new ArgumentException("Invalid verified registration or unconfigured coin policy.");
        store.Create(new RegisteredMatch { Registration = r, RegisteredAt = clock.UtcMilliseconds });
    }

    static int Seat(RegisteredMatch m, string authenticatedUser)
    {
        if (authenticatedUser == m.Registration.UserA) return 0;
        if (authenticatedUser == m.Registration.UserB) return 1;
        throw new UnauthorizedAccessException("Authenticated caller is not a participant.");
    }

    // Both authenticated participants must accept; repeated calls cannot charge again.
    public bool Ready(string matchId, string authenticatedUser, MatchRulesContract rules = null) => store.Execute(matchId, c => {
        int seat = Seat(c.Match, authenticatedUser);
        if (c.Receipt != null) return false;
        if ((c.Match.Registration.Rules != null || rules != null) &&
            (c.Match.Registration.Rules == null || !c.Match.Registration.Rules.SameAs(rules)))
            throw new MatchRulesMismatchException();
        if (c.Match.Phase == ServerMatchPhase.Playing) return true;
        if (seat == 0) c.Match.ReadyA = true; else c.Match.ReadyB = true;
        if (!c.Match.ReadyA || !c.Match.ReadyB) return false;
        var r = c.Match.Registration;
        ValidateWallet(c.UserA); ValidateWallet(c.UserB);
        if (c.UserA.Coin < r.EntryCoin || c.UserB.Coin < r.EntryCoin)
            throw new InvalidOperationException("Insufficient balance to start the match.");
        c.UserA.Coin = checked(c.UserA.Coin - r.EntryCoin);
        c.UserB.Coin = checked(c.UserB.Coin - r.EntryCoin);
        c.Match.StartedAt = c.Match.LastSeenA = c.Match.LastSeenB = clock.UtcMilliseconds;
        c.Match.Phase = ServerMatchPhase.Playing;
        return true;
    });

    public void Heartbeat(string matchId, string authenticatedUser) => store.Execute(matchId, c => {
        int seat = Seat(c.Match, authenticatedUser);
        if (c.Receipt != null || c.Match.Phase != ServerMatchPhase.Playing) return false;
        if (seat == 0) c.Match.LastSeenA = clock.UtcMilliseconds; else c.Match.LastSeenB = clock.UtcMilliseconds;
        return true;
    });

    // Requires BOTH authenticated reports to agree. It is not a server physics proof;
    // an authoritative simulation can replace this acceptance policy in the adapter.
    public SettlementReceipt ConfirmResult(string matchId, string authenticatedUser, ResultReport report) => store.Execute(matchId, c => {
        int seat = Seat(c.Match, authenticatedUser);
        if (c.Receipt != null) return c.Receipt;
        if (c.Match.Phase != ServerMatchPhase.Playing || report == null || report.ScoreA < 0 || report.ScoreB < 0 ||
            report.ScoreA > 100000 || report.ScoreB > 100000 || report.WinnerSeat is < -1 or > 1 ||
            report.WinnerSeat != (report.ScoreA == report.ScoreB ? -1 : report.ScoreA > report.ScoreB ? 0 : 1))
            throw new ArgumentException("Invalid result report.");
        var previous = seat == 0 ? c.Match.ReportA : c.Match.ReportB;
        if (previous != null && previous != report) throw new InvalidOperationException("A submitted report cannot be changed.");
        if (seat == 0) c.Match.ReportA = report; else c.Match.ReportB = report;
        if (c.Match.ReportA == null || c.Match.ReportB == null) return null;
        if (c.Match.ReportA != c.Match.ReportB) { c.Match.Disputed = true; return null; }
        return Apply(c, report.WinnerSeat, "MutualConfirmation", report.ScoreA, report.ScoreB, true);
    });

    public SettlementReceipt Surrender(string matchId, string authenticatedUser) => store.Execute(matchId, c => {
        int seat = Seat(c.Match, authenticatedUser);
        if (c.Receipt != null) return c.Receipt;
        if (c.Match.Disputed) throw new InvalidOperationException("Disputed results require server review.");
        return Apply(c, 1 - seat, c.Match.Phase == ServerMatchPhase.Playing ? "Surrender" : "Cancelled", 0, 0,
            c.Match.Phase == ServerMatchPhase.Playing);
    });

    // Can be triggered by the remaining participant; the decision uses server heartbeats,
    // never a client-supplied disconnect time or a client's claim about their opponent.
    public SettlementReceipt EvaluateDisconnect(string matchId, string authenticatedUser) => store.Execute(matchId, c => {
        Seat(c.Match, authenticatedUser);
        if (c.Receipt != null) return c.Receipt;
        if (c.Match.Phase != ServerMatchPhase.Playing || c.Match.Disputed) return null;
        long now = clock.UtcMilliseconds, ageA = now - c.Match.LastSeenA, ageB = now - c.Match.LastSeenB;
        long expired = heartbeatTimeoutMs + reconnectGraceMs;
        if (ageA >= expired && ageB >= expired) return Apply(c, -1, "BothDisconnected", 0, 0, false);
        if (ageA >= expired && ageB <= heartbeatTimeoutMs) return Apply(c, 1, "DisconnectForfeit", 0, 0, true);
        if (ageB >= expired && ageA <= heartbeatTimeoutMs) return Apply(c, 0, "DisconnectForfeit", 0, 0, true);
        return null;
    });

    static void ValidateWallet(UserMainData user)
    {
        if (user == null || user.Coin < 0 || user.Coin > MaxExactCoin) throw new InvalidOperationException("Invalid server wallet.");
    }

    static void Increment(UserMainData u, int ball, string result)
    {
        checked {
            if (ball == 3) {
                if (result == "Win") u.Ball_3_Win++; else if (result == "Lose") u.Ball_3_Lose++; else u.Ball_3_Draw++;
            } else {
                if (result == "Win") u.Ball_4_Win++; else if (result == "Lose") u.Ball_4_Lose++; else u.Ball_4_Draw++;
            }
        }
    }

    SettlementReceipt Apply(SettlementContext c, int winner, string reason, int scoreA, int scoreB, bool countStats)
    {
        var m = c.Match; var r = m.Registration;
        long now = clock.UtcMilliseconds, fee = r.EntryCoin;
        bool began = m.Phase == ServerMatchPhase.Playing;
        string resultA = !countStats ? "Void" : winner == -1 ? "Draw" : winner == 0 ? "Win" : "Lose";
        string resultB = !countStats ? "Void" : winner == -1 ? "Draw" : winner == 1 ? "Win" : "Lose";
        long entryA = 0, entryB = 0, rewardA = 0, rewardB = 0;
        ValidateWallet(c.UserA); ValidateWallet(c.UserB);
        if (began) {
            entryA = entryB = fee;
            if (!countStats || winner == -1) rewardA = rewardB = fee;
            else if (winner == 0) rewardA = checked(fee * 2); else rewardB = checked(fee * 2);
        }
        c.UserA.Coin = checked(c.UserA.Coin + rewardA);
        c.UserB.Coin = checked(c.UserB.Coin + rewardB);
        ValidateWallet(c.UserA); ValidateWallet(c.UserB);
        if (countStats) { Increment(c.UserA, r.Ball, resultA); Increment(c.UserB, r.Ball, resultB); }
        long expires = DateTimeOffset.FromUnixTimeMilliseconds(now).AddMonths(6).ToUnixTimeMilliseconds();
        UserMatchHistoryData History(int seat, string result, long entry, long reward) => new() {
            MatchId = r.MatchId, UserId = seat == 0 ? r.UserA : r.UserB, OpponentUserId = seat == 0 ? r.UserB : r.UserA,
            NicknameSnapshot = seat == 0 ? r.NameA : r.NameB, OpponentNicknameSnapshot = seat == 0 ? r.NameB : r.NameA,
            SeatNo = seat, MatchBall = r.Ball, HallIdx = r.HallId, EndedAt = now,
            Result = result, Score = seat == 0 ? scoreA : scoreB, OpponentScore = seat == 0 ? scoreB : scoreA,
            EntryCoin = entry, RewardCoin = reward, CoinDelta = checked(reward - entry),
            SettlementStatus = "Applied", SettledAt = now, HistoryExpiresAt = expires
        };
        var receipt = new SettlementReceipt {
            MatchId = r.MatchId, Reason = reason, SettledAt = now,
            UserA = r.UserA, UserB = r.UserB, Ball = r.Ball, ResultA = resultA, ResultB = resultB,
            EntryA = entryA, EntryB = entryB, RewardA = rewardA, RewardB = rewardB,
            WinnerUserId = countStats && winner >= 0 ? (winner == 0 ? r.UserA : r.UserB) : "",
            PlayerA = History(0, resultA, entryA, rewardA), PlayerB = History(1, resultB, entryB, rewardB),
            Match = new MatchHistoryData {
                MatchId = r.MatchId, PlayerAUserId = r.UserA, PlayerBUserId = r.UserB, MatchBall = r.Ball, HallIdx = r.HallId,
                StartedAt = m.StartedAt, EndedAt = now, DurationSec = began ? checked((int)Math.Max(0, (now - m.StartedAt) / 1000)) : 0,
                Status = countStats ? "Completed" : "Cancelled", EndReason = reason, RulesSnapshot = r.RulesSnapshot,
                SettlementStatus = "Applied", HistoryExpiresAt = expires
            }
        };
        receipt.Match.WinnerUserId = receipt.WinnerUserId;
        c.Receipt = receipt; m.Phase = ServerMatchPhase.Applied;
        return receipt;
    }
}
