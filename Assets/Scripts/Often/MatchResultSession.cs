using System;

namespace Assets.Scripts.Often
{
    public enum MatchResultKind { Begin, Shot, TurnTimeout, MatchTimeout }
    public enum MatchShotOutcome { Miss, Point, Foul }
    [Serializable]
    public sealed class MatchResultMessage
    {
        public string matchId;
        public int sequence, shotId;
        public MatchResultKind kind;
        public MatchShotOutcome outcome;
        public double time;
    }

    // Collision facts only. Scoring is deferred until the authoritative terminal snapshot.
    public sealed class MatchContactRecord
    {
        readonly int balls, cue, requiredCushions;
        bool first, second, opponent;
        int firstId = -1, cushions;
        bool threePoint;
        public int BallContacts { get; private set; }
        public MatchContactRecord(int balls, int cue, int requiredCushions)
        { this.balls = balls; this.cue = cue; this.requiredCushions = requiredCushions; }
        public void Cushion() { if (!second) cushions++; }
        public void Ball(int id)
        {
            if (id < 0 || id >= balls || id == cue) return;
            BallContacts++;
            if (balls == 4)
            {
                if (id == 1 - cue) opponent = true;
                if (id == 2) first = true;
                if (id == 3) second = true;
            }
            else if (firstId == -1) { firstId = id; first = true; }
            else if (id != firstId && !second) { second = true; threePoint = cushions >= requiredCushions; }
        }
        public MatchShotOutcome Resolve()
        {
            if (balls == 3) return threePoint ? MatchShotOutcome.Point : MatchShotOutcome.Miss;
            var result = FourBallRules.Resolve(first, second, opponent);
            return result == FourBallShotOutcome.Foul ? MatchShotOutcome.Foul :
                result == FourBallShotOutcome.Point ? MatchShotOutcome.Point : MatchShotOutcome.Miss;
        }
    }

    // Receivers derive scores/turn/winner from the same ordered outcome stream.
    // No wallet or legacy local-game side effects occur here.
    public sealed class MatchResultSession
    {
        readonly MatchStartSession start;
        readonly MatchShotSession shots;
        public bool Initialized { get; private set; }
        public bool Finished => shots.Phase == MatchShotPhase.Finished;
        public int Score0 { get; private set; }
        public int Score1 { get; private set; }
        public int Sequence { get; private set; }
        public int Winner { get; private set; } = -1; // -1 ongoing, -2 draw
        public double MatchDeadline { get; private set; }
        public double TurnDeadline { get; private set; }
        double lastTime;
        public event Action<MatchResultMessage> Applied;
        internal void RestoreCheckpoint(MatchRecoveryCheckpoint checkpoint)
        {
            Score0 = checkpoint.score0; Score1 = checkpoint.score1; Sequence = checkpoint.sequence;
            Winner = -1; Initialized = true; lastTime = checkpoint.time;
            MatchDeadline = checkpoint.matchDeadline; TurnDeadline = checkpoint.turnDeadline;
        }
        internal void NotifyRestored()
        {
            var notification = Create(MatchResultKind.Begin, MatchShotOutcome.Miss, lastTime);
            notification.sequence = Sequence;
            Applied?.Invoke(notification);
        }
        public MatchResultSession(MatchStartSession start, MatchShotSession shots)
        { this.start = start; this.shots = shots; }
        static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
        public bool CanShoot(double now) => Initialized && !Finished && Finite(now) &&
            start.Phase == MatchStartPhase.Started && shots.Phase == MatchShotPhase.Ready &&
            now < TurnDeadline && now < MatchDeadline;

        public MatchResultMessage Create(MatchResultKind kind, MatchShotOutcome outcome, double now) =>
            new MatchResultMessage { matchId = start.MatchId, sequence = Sequence + 1,
                shotId = shots.ShotId, kind = kind, outcome = outcome, time = now };

        public MatchResultMessage Due(double now)
        {
            if (start.LocalActor != start.AuthorityActor || !Initialized || Finished || shots.Phase != MatchShotPhase.Ready) return null;
            if (now >= MatchDeadline) return Create(MatchResultKind.MatchTimeout, MatchShotOutcome.Miss, now);
            if (now >= TurnDeadline) return Create(MatchResultKind.TurnTimeout, MatchShotOutcome.Miss, now);
            return null;
        }

        public bool Apply(int sender, MatchResultMessage message)
        {
            if (start.Phase != MatchStartPhase.Started || sender != start.AuthorityActor || message == null ||
                message.matchId != start.MatchId || message.sequence != Sequence + 1 || message.shotId != shots.ShotId ||
                !Finite(message.time) || message.time < lastTime || Finished) return false;
            if (message.kind == MatchResultKind.Begin)
            {
                if (Initialized || shots.Phase != MatchShotPhase.Ready || shots.ShotId != 0 ||
                    message.outcome != MatchShotOutcome.Miss) return false;
                MatchDeadline = message.time + start.Configuration.MatchSeconds;
                TurnDeadline = message.time + start.Configuration.TurnSeconds;
                Initialized = true;
            }
            else
            {
                if (!Initialized) return false;
                int score0 = Score0, score1 = Score1, seat = shots.TurnSeat;
                bool idle = message.kind != MatchResultKind.Shot;
                if (!idle)
                {
                    if (shots.Phase != MatchShotPhase.AwaitingResult ||
                        !Enum.IsDefined(typeof(MatchShotOutcome), message.outcome) ||
                        (start.Configuration.BallCount == 3 && message.outcome == MatchShotOutcome.Foul)) return false;
                    int delta = message.outcome == MatchShotOutcome.Point ? 1 : message.outcome == MatchShotOutcome.Foul ? -1 : 0;
                    if (seat == 0) score0 = Math.Max(0, score0 + delta); else score1 = Math.Max(0, score1 + delta);
                }
                else if (message.kind == MatchResultKind.TurnTimeout)
                {
                    if (message.time < TurnDeadline || message.time >= MatchDeadline || message.outcome != MatchShotOutcome.Miss) return false;
                }
                else if (message.kind == MatchResultKind.MatchTimeout)
                {
                    if (message.time < MatchDeadline || message.outcome != MatchShotOutcome.Miss) return false;
                }
                else return false;
                int winner = score0 >= start.Configuration.TargetScore ? 0 : score1 >= start.Configuration.TargetScore ? 1 :
                    message.time >= MatchDeadline ? (score0 == score1 ? -2 : score0 > score1 ? 0 : 1) : -1;
                int next = !idle && message.outcome == MatchShotOutcome.Point ? seat : 1 - seat;
                if (!shots.ApplyResultTransition(sender, message.matchId, message.shotId, next, idle, winner != -1)) return false;
                Score0 = score0; Score1 = score1; Winner = winner;
                TurnDeadline = message.time + start.Configuration.TurnSeconds;
            }
            Sequence = message.sequence; lastTime = message.time;
            Applied?.Invoke(new MatchResultMessage { matchId = message.matchId, sequence = message.sequence,
                shotId = message.shotId, kind = message.kind, outcome = message.outcome, time = message.time });
            return true;
        }
    }
}
