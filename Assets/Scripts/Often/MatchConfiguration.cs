using System;
using Assets.Scripts.Prack.BackSys.ChartData;

namespace Assets.Scripts.Often
{
    public enum MatchExecutionMode { LocalPractice, OnlineMatch, Replay }

    // Immutable settings. Transport validation and authority approval remain separate.
    public sealed class MatchConfiguration
    {
        public MatchExecutionMode Execution { get; }
        public int BallCount { get; }
        public int HallId { get; }
        public int TargetScore { get; }
        public int CushionCount { get; }
        public int MatchSeconds { get; }
        public int TurnSeconds { get; }
        public int FirstSeat { get; }
        public int LocalSeat { get; }
        public string Seat0Name { get; }
        public string Seat1Name { get; }
        public bool AllowsPracticeTools => Execution == MatchExecutionMode.LocalPractice;

        MatchConfiguration(MatchExecutionMode execution, int ballCount, int hallId,
            int targetScore, int cushionCount, int matchSeconds, int turnSeconds,
            int firstSeat, int localSeat, string seat0Name, string seat1Name)
        {
            if (ballCount != 3 && ballCount != 4) throw new ArgumentOutOfRangeException(nameof(ballCount));
            if (targetScore < 1 || targetScore > 1000) throw new ArgumentOutOfRangeException(nameof(targetScore));
            if (cushionCount < 0 || cushionCount > 3 || (ballCount == 4 && cushionCount != 0))
                throw new ArgumentOutOfRangeException(nameof(cushionCount));
            if (matchSeconds < 1 || turnSeconds < 1) throw new ArgumentOutOfRangeException(nameof(matchSeconds));
            if (firstSeat < 0 || firstSeat > 1 || localSeat < 0 || localSeat > 1)
                throw new ArgumentOutOfRangeException(nameof(firstSeat));
            if (string.IsNullOrWhiteSpace(seat0Name) || string.IsNullOrWhiteSpace(seat1Name))
                throw new ArgumentException("Both participants must have display names.");
            Execution = execution; BallCount = ballCount; HallId = hallId;
            TargetScore = targetScore; CushionCount = cushionCount;
            MatchSeconds = matchSeconds; TurnSeconds = turnSeconds;
            FirstSeat = firstSeat; LocalSeat = localSeat;
            Seat0Name = seat0Name; Seat1Name = seat1Name;
        }

        public static MatchConfiguration Practice(int ballCount, bool replay = false) =>
            new MatchConfiguration(replay ? MatchExecutionMode.Replay : MatchExecutionMode.LocalPractice,
                ballCount, 0, 7, ballCount == 3 ? 3 : 0, 300, ballCount == 4 && !replay ? 60 : 20,
                0, 0, "Player 1", "Player 2");

        public static MatchConfiguration Online(SkillMatchData hall, string seat0Name, string seat1Name,
            int firstSeat, int localSeat, int turnSeconds)
        {
            if (hall == null) throw new ArgumentNullException(nameof(hall));
            if (hall.HallIdx < 1) throw new ArgumentOutOfRangeException(nameof(hall.HallIdx));
            // Do not silently change an unsupported room's agreed rules.
            if (hall.FinishMission != 0) throw new NotSupportedException("Finish missions are not yet supported by the new match runtime.");
            return new MatchConfiguration(MatchExecutionMode.OnlineMatch, hall.MatchBall, hall.HallIdx,
                hall.MatchBall == 4 ? 3 : hall.TargetHit, hall.MatchCushion, checked(hall.MatchTotMin * 60), turnSeconds,
                firstSeat, localSeat, seat0Name, seat1Name);
        }
    }

    public enum FourBallShotOutcome { Miss, Point, Foul }

    public static class FourBallRules
    {
        // Called only after all balls stop. A late opponent contact overrides both reds.
        public static FourBallShotOutcome Resolve(bool hitFirstRed, bool hitSecondRed, bool hitOpponent)
        {
            if (hitOpponent || (!hitFirstRed && !hitSecondRed)) return FourBallShotOutcome.Foul;
            return hitFirstRed && hitSecondRed ? FourBallShotOutcome.Point : FourBallShotOutcome.Miss;
        }
    }
}
