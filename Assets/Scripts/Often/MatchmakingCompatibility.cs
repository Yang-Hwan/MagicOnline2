using System;
using System.Globalization;
using Assets.Scripts.Prack.BackSys.ChartData;

namespace Assets.Scripts.Often
{
    public static class MatchmakingCompatibility
    {
        // Isolate upgraded clients from older clients that join rooms by name only.
        public static readonly string Protocol = "pavilion-practice-physics-6-rules2-timeout2-charttarget1-contacts1-startorder1-cushionfx1-scorefx1-coins1-finishfoul1-equipment1" +
            Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.FollowThroughProfile.ProtocolSuffix;
        public static string GameVersion(string appVersion) => appVersion + "." + Protocol;

        public static int SelectStartingSeat(bool rematch, int previousLoserSeat, int randomSeat)
        {
            if (randomSeat != 0 && randomSeat != 1) throw new ArgumentOutOfRangeException(nameof(randomSeat));
            return rematch && (previousLoserSeat == 0 || previousLoserSeat == 1)
                ? previousLoserSeat : randomSeat;
        }

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
