using System;
using System.Globalization;
using Assets.Scripts.Prack.BackSys.ChartData;

namespace Assets.Scripts.Often
{
    public static class MatchmakingCompatibility
    {
        // Isolate upgraded clients from older clients that join rooms by name only.
        public const string Protocol = "pavilion-practice-physics-4";
        public static string GameVersion(string appVersion) => appVersion + "." + Protocol;

        public static string RulesKey(SkillMatchData hall)
        {
            if (hall == null) throw new ArgumentNullException(nameof(hall));
            if (hall.HallIdx < 1 || (hall.MatchBall != 3 && hall.MatchBall != 4) ||
                hall.TargetHit < 1 || hall.MatchTotMin < 1 || hall.MatchCushion < 0 || hall.MatchCushion > 3)
                throw new ArgumentException("Invalid matchmaking room settings.");
            return string.Join(":", Array.ConvertAll(new long[] { hall.HallIdx, hall.MatchBall,
                hall.TargetHit, hall.MatchCushion, hall.MatchTotMin, hall.FinishMission, hall.PrizeCoin, hall.MaxCoin },
                value => value.ToString(CultureInfo.InvariantCulture)));
        }
    }
}
