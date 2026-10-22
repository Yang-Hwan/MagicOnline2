using System;
using Assets.Scripts.Prack.BackSys.ChartData;

namespace Assets.Scripts.Prack.BackSys.GameData
{
    [Serializable]
    public sealed class MatchRulesContract
    {
        public const int CurrentVersion = 1;
        public const int Pavilion = 0, Practice = 1;
        // No version initializer: an omitted version must fail validation.
        public int Version, Runtime, HallId, Ball, TargetScore, Cushions, MatchSeconds, TurnSeconds;
        public int FinishMission, HeartbeatSeconds, ReconnectGraceSeconds, CoinPolicy;
        public long EntryCoin, MaxCoin;

        public static MatchRulesContract FromHall(SkillMatchData hall, int runtime)
        {
            if (hall == null) throw new ArgumentNullException(nameof(hall));
            if (runtime != Pavilion && runtime != Practice) throw new ArgumentException("Unsupported match runtime.");
            return new MatchRulesContract {
                Version = CurrentVersion, Runtime = runtime, HallId = hall.HallIdx, Ball = hall.MatchBall,
                TargetScore = hall.MatchBall == 4 ? 3 : hall.TargetHit, Cushions = hall.MatchCushion,
                MatchSeconds = checked(hall.MatchTotMin * 60), TurnSeconds = runtime == Practice ? 20 : 30,
                FinishMission = hall.FinishMission, HeartbeatSeconds = 15, ReconnectGraceSeconds = 30,
                CoinPolicy = 1, EntryCoin = hall.PrizeCoin, MaxCoin = hall.MaxCoin
            };
        }

        public bool SameAs(MatchRulesContract other) => other != null &&
            Version == other.Version && Runtime == other.Runtime && HallId == other.HallId && Ball == other.Ball &&
            TargetScore == other.TargetScore && Cushions == other.Cushions && MatchSeconds == other.MatchSeconds &&
            TurnSeconds == other.TurnSeconds && FinishMission == other.FinishMission &&
            HeartbeatSeconds == other.HeartbeatSeconds && ReconnectGraceSeconds == other.ReconnectGraceSeconds &&
            CoinPolicy == other.CoinPolicy && EntryCoin == other.EntryCoin && MaxCoin == other.MaxCoin;
    }
}
