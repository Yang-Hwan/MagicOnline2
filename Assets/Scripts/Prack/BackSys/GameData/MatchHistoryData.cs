using System;

namespace Assets.Scripts.Prack.BackSys.GameData
{
    /// <summary>Client history reports are wired; authoritative server registration/settlement remain separate.</summary>
    public static class MatchHistoryTables
    {
        public const string MatchHistory = "MatchHistory";
        public const string UserMatchHistory = "UserMatchHistory";
    }

    [Serializable]
    public sealed class MatchHistoryData
    {
        public string MatchId = "";
        public int SchemaVersion = 1;
        public string MatchMode = "Online";
        public int MatchBall; // 3 or 4; matches SkillMatch.MatchBall.
        public int HallIdx;
        public string PlayerAUserId = ""; // BACKND user inDate, not Photon actor number.
        public string PlayerBUserId = "";
        public long StartedAt; // All timestamps are UTC Unix milliseconds.
        public long EndedAt;
        public int DurationSec;
        public string Status = "Preparing";
        public string EndReason = "";
        public string WinnerUserId = ""; // Empty for draws/cancelled matches.
        public int PlayerCount = 2;
        public string RulesSnapshot = ""; // JSON string of the rules below.
        public MatchRulesContract Rules;
        public string SettlementStatus = "Pending";
        public long HistoryExpiresAt; // Client proposes EndedAt + 6 months; server must validate before cleanup.
        public string CleanupStatus = "None";
    }

    [Serializable]
    public sealed class MatchRulesSnapshot
    {
        public int HallIdx;
        public string HallName = "";
        public int MatchBall;
        public int TargetHit; // Effective target actually used by the match runtime.
        public int MatchTotMin;
        public int MatchCushion;
        public int FinishMission;
        public long PrizeCoin;
        public long MaxCoin;
        public long EntryCoin; // Actual agreed entry fee; PrizeCoin alone is not a payout rule.
        public long WinRewardCoin;
        public int ReconnectGraceSec;
    }
}
