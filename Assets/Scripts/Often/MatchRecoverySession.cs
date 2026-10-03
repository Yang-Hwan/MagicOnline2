using System;

namespace Assets.Scripts.Often
{
    [Serializable]
    public sealed class MatchRecoveryCheckpoint
    {
        public string matchId, key, requestId;
        public int seat, turnRevision, score0, score1, sequence;
        public double time, matchDeadline, turnDeadline;
        public MatchBallSnapshot balls;
    }

    // Only idle, guest-only disconnects are recoverable. Authority migration is not supported.
    public sealed class MatchRecoverySession
    {
        public const double GraceSeconds = 30;
        readonly MatchStartSession start;
        readonly MatchShotSession shots;
        readonly MatchResultSession results;
        double pausedAt, expiresAt;
        public string RequestId { get; private set; }
        public MatchRecoverySession(MatchStartSession start, MatchShotSession shots, MatchResultSession results)
        { this.start = start; this.shots = shots; this.results = results; }
        static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
        public bool Suspend(double now, double realtime)
        {
            if (!Finite(now) || !Finite(realtime) || !results.Initialized || results.Finished ||
                start.Phase != MatchStartPhase.Started || shots.Phase == MatchShotPhase.Moving) return false;
            if (start.LocalActor == start.AuthorityActor && (!results.CanShoot(now) || shots.LastSettled == null)) return false;
            pausedAt = now; expiresAt = realtime + GraceSeconds;
            RequestId = Guid.NewGuid().ToString("N");
            return start.Suspend();
        }
        public void Tick(double realtime)
        {
            if (start.Phase == MatchStartPhase.Suspended && realtime >= expiresAt)
                start.Abort("재접속 대기 시간이 초과되었습니다. 나가기를 눌러 다시 입장해 주세요.");
        }
        public MatchRecoveryCheckpoint Capture(int requester, string requestId, double now, double realtime)
        {
            Tick(realtime);
            if (start.LocalActor != start.AuthorityActor || requester != start.ActorForSeat(1) ||
                start.Phase != MatchStartPhase.Suspended || string.IsNullOrEmpty(requestId) || requestId.Length != 32 ||
                !Finite(now) || now < pausedAt || shots.Phase != MatchShotPhase.Ready) return null;
            return new MatchRecoveryCheckpoint { matchId = start.MatchId, key = start.ConfigurationKey, requestId = requestId,
                seat = shots.TurnSeat, turnRevision = shots.TurnRevision + 1, score0 = results.Score0, score1 = results.Score1,
                sequence = results.Sequence, time = now, matchDeadline = results.MatchDeadline + now - pausedAt,
                turnDeadline = results.TurnDeadline + now - pausedAt, balls = shots.LastSettled };
        }
        public bool Restore(int sender, MatchRecoveryCheckpoint checkpoint, string expectedRequest, double realtime)
        {
            Tick(realtime);
            if (start.Phase != MatchStartPhase.Suspended || sender != start.AuthorityActor || checkpoint == null ||
                checkpoint.matchId != start.MatchId || checkpoint.key != start.ConfigurationKey ||
                string.IsNullOrEmpty(expectedRequest) || checkpoint.requestId != expectedRequest ||
                checkpoint.seat < 0 || checkpoint.seat > 1 || checkpoint.turnRevision <= shots.TurnRevision ||
                checkpoint.sequence < results.Sequence || checkpoint.score0 < 0 || checkpoint.score1 < 0 ||
                checkpoint.score0 >= start.Configuration.TargetScore || checkpoint.score1 >= start.Configuration.TargetScore ||
                !Finite(checkpoint.time) || !Finite(checkpoint.matchDeadline) || !Finite(checkpoint.turnDeadline) ||
                checkpoint.matchDeadline <= checkpoint.time || checkpoint.turnDeadline <= checkpoint.time ||
                checkpoint.matchDeadline - checkpoint.time > start.Configuration.MatchSeconds + .001 ||
                checkpoint.turnDeadline - checkpoint.time > start.Configuration.TurnSeconds + .001 ||
                checkpoint.balls == null || checkpoint.balls.matchId != start.MatchId || !checkpoint.balls.settled ||
                checkpoint.balls.shotId < shots.ShotId || checkpoint.balls.frame < 0 ||
                !MatchShotSession.ValidBalls(checkpoint.balls, start.Configuration.BallCount)) return false;
            // Validate the entire checkpoint before mutating either replica.
            shots.RestoreCheckpoint(checkpoint); results.RestoreCheckpoint(checkpoint);
            start.Resume(sender, checkpoint.matchId);
            shots.NotifyRestored(); results.NotifyRestored();
            RequestId = null;
            return true;
        }
    }
}
