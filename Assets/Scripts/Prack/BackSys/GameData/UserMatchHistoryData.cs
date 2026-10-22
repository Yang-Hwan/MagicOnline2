using System;

namespace Assets.Scripts.Prack.BackSys.GameData
{
    [Serializable]
    public sealed class UserMatchHistoryData
    {
        public string MatchId = "";
        public string UserId = "";
        public string OpponentUserId = "";
        public string NicknameSnapshot = "";
        public string OpponentNicknameSnapshot = "";
        public int SeatNo; // 0 or 1, independent of later Master Client changes.
        public int MatchBall; // 3 or 4.
        public int HallIdx;
        public long EndedAt; // UTC Unix milliseconds.
        public string Result = ""; // Win, Lose, Draw, Void; empty before confirmation.
        public int Score;
        public int OpponentScore;
        public int TurnCount;
        public int BestRun;
        public long EntryCoin;
        public long RewardCoin;
        public long CoinDelta; // RewardCoin - EntryCoin for the agreed fee/reward policy.
        public string SettlementStatus = "Pending";
        public long SettledAt;
        public long HistoryExpiresAt;
    }
}
