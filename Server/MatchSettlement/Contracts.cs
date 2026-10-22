using Assets.Scripts.Prack.BackSys;
using Assets.Scripts.Prack.BackSys.GameData;

namespace MagicOnline.MatchSettlement;

public enum CoinPolicy { Unconfigured, EntryPool }
public enum ServerMatchPhase { Registered, Playing, Applied }

// Construct ONLY after token authentication, opponent consent and server chart lookup.
// Existing client-authored MatchHistory rows must not be used as this authority.
public sealed record VerifiedRegistration(
    string MatchId, string UserA, string UserB, string NameA, string NameB,
    int Ball, int HallId, long EntryCoin, CoinPolicy CoinPolicy, string RulesSnapshot, MatchRulesContract Rules = null);

public sealed class MatchRulesMismatchException : InvalidOperationException
{ public MatchRulesMismatchException() : base("MatchRulesMismatch") { } }

public sealed record ResultReport(int ScoreA, int ScoreB, int WinnerSeat); // -1 draw, 0 A, 1 B

public sealed class RegisteredMatch
{
    public VerifiedRegistration Registration;
    public ServerMatchPhase Phase;
    public long RegisteredAt, StartedAt, LastSeenA, LastSeenB;
    public bool ReadyA, ReadyB, Disputed;
    public ResultReport ReportA, ReportB;
}

public sealed class SettlementReceipt
{
    public string MatchId, Reason, WinnerUserId;
    // Compact permanent ledger; retain even when the projections below expire.
    public string UserA, UserB, ResultA, ResultB;
    public int Ball;
    public long EntryA, EntryB, RewardA, RewardB;
    public long SettledAt;
    // Retained only for six months; production stores these in the history tables.
    public MatchHistoryData Match;
    public UserMatchHistoryData PlayerA, PlayerB;
}

public sealed class SettlementContext
{
    public RegisteredMatch Match;
    public UserMainData UserA, UserB;
    public SettlementReceipt Receipt;
}

public interface IServerClock { long UtcMilliseconds { get; } }

// A PRODUCTION adapter must implement a durable serializable transaction across
// the match, both UserMain rows, the permanent receipt and both private histories.
// Roll back ALL mutations on exception, CAS/lock shared users across DIFFERENT matches,
// enforce unique MatchId, and atomically persist the receipt with the balance/stat changes.
// Get-then-Insert or a process-local lock in a serverless function does NOT satisfy this.
public interface ISettlementStore
{
    void Create(RegisteredMatch match);
    T Execute<T>(string matchId, Func<SettlementContext, T> operation);
}
