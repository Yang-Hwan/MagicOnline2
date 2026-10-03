using System;
using UnityEngine;

namespace Assets.Scripts.Often
{
    public enum MatchShotPhase { AwaitingInitialState, Ready, Moving, AwaitingResult, Finished }

    // Stroke intent only: the authority must calculate the impulse from its own table.
    [Serializable]
    public sealed class MatchShotCommand
    {
        public string matchId;
        public int shotId, seat, turnRevision;
        public float power, followThrough, yaw;
        public Vector2 contact;
        public MatchShotCommand Copy() => (MatchShotCommand)MemberwiseClone();
    }

    [Serializable]
    public struct MatchBallState
    {
        public Vector3 position, velocity, angularVelocity;
        public Quaternion rotation;
    }

    [Serializable]
    public sealed class MatchBallSnapshot
    {
        public string matchId;
        public int shotId, frame;
        public bool settled;
        // Array order is the agreed scene ball order, never a client-selected object ID.
        public MatchBallState[] balls;
        public MatchBallSnapshot Copy() => new MatchBallSnapshot {
            matchId = matchId, shotId = shotId, frame = frame, settled = settled,
            balls = balls == null ? null : (MatchBallState[])balls.Clone()
        };
    }

    // Transport-independent admission rules. This does not run physics or award points.
    public sealed class MatchShotSession
    {
        readonly MatchStartSession start;
        public MatchShotPhase Phase { get; private set; }
        public int ShotId { get; private set; }
        public int TurnSeat { get; private set; }
        public int TurnRevision { get; private set; }
        public int LastFrame { get; private set; } = -1;
        MatchBallSnapshot lastSettled;
        public MatchBallSnapshot LastSettled => lastSettled?.Copy();
        public event Action<MatchShotCommand> ShotCommitted;
        public event Action<MatchBallSnapshot> SnapshotApplied;
        public MatchShotSession(MatchStartSession start)
        {
            this.start = start ?? throw new ArgumentNullException(nameof(start));
            TurnSeat = start.Configuration.FirstSeat;
        }

        bool Active => start.Phase == MatchStartPhase.Started;
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Bounded(Vector3 value, float limit) => Finite(value.x) && Finite(value.y) &&
            Finite(value.z) && Mathf.Abs(value.x) <= limit && Mathf.Abs(value.y) <= limit && Mathf.Abs(value.z) <= limit;
        bool ValidCommand(MatchShotCommand shot) => Active && Phase == MatchShotPhase.Ready &&
            shot != null && shot.matchId == start.MatchId && ShotId < int.MaxValue &&
            shot.shotId == ShotId + 1 && shot.seat == TurnSeat && shot.turnRevision == TurnRevision &&
            Finite(shot.power) && shot.power > 0 && shot.power <= 1 &&
            Finite(shot.followThrough) && shot.followThrough >= 0 && shot.followThrough <= 1 &&
            Finite(shot.yaw) && shot.yaw >= 0 && shot.yaw < 360 &&
            Finite(shot.contact.x) && Finite(shot.contact.y) && shot.contact.sqrMagnitude <= 1;

        // sender must be Photon EventData.Sender (or the local actor for local input).
        public bool CanApprove(int sender, MatchShotCommand shot) =>
            start.LocalActor == start.AuthorityActor && ValidCommand(shot) && sender == start.ActorForSeat(TurnSeat);

        public bool CommitShot(int sender, MatchShotCommand shot)
        {
            if (sender != start.AuthorityActor || !ValidCommand(shot)) return false;
            ShotId = shot.shotId; LastFrame = -1; Phase = MatchShotPhase.Moving;
            ShotCommitted?.Invoke(shot.Copy());
            return true;
        }

        public bool ApplySnapshot(int sender, MatchBallSnapshot snapshot)
        {
            if (!Active || sender != start.AuthorityActor || snapshot == null || snapshot.matchId != start.MatchId ||
                snapshot.shotId != ShotId || snapshot.frame < 0 || snapshot.frame <= LastFrame ||
                snapshot.balls == null || snapshot.balls.Length != start.Configuration.BallCount ||
                (Phase != MatchShotPhase.AwaitingInitialState && Phase != MatchShotPhase.Moving)) return false;
            if (Phase == MatchShotPhase.AwaitingInitialState && (snapshot.shotId != 0 || !snapshot.settled)) return false;
            if (!ValidBalls(snapshot, start.Configuration.BallCount)) return false;
            LastFrame = snapshot.frame;
            if (snapshot.settled) lastSettled = snapshot.Copy();
            if (Phase == MatchShotPhase.AwaitingInitialState) Phase = MatchShotPhase.Ready;
            else if (snapshot.settled) Phase = MatchShotPhase.AwaitingResult;
            SnapshotApplied?.Invoke(snapshot.Copy());
            return true;
        }

        internal static bool ValidBalls(MatchBallSnapshot snapshot, int count)
        {
            if (snapshot == null || snapshot.balls == null || snapshot.balls.Length != count) return false;
            foreach (var ball in snapshot.balls)
            {
                float norm = ball.rotation.x * ball.rotation.x + ball.rotation.y * ball.rotation.y +
                    ball.rotation.z * ball.rotation.z + ball.rotation.w * ball.rotation.w;
                if (!Bounded(ball.position, 10000) || !Bounded(ball.velocity, 1000) ||
                    !Bounded(ball.angularVelocity, 10000) || !Finite(norm) || Mathf.Abs(norm - 1) > .01f) return false;
                // A terminal snapshot is the exact resting state, not a moving frame marked as settled.
                if (snapshot.settled && (ball.velocity.sqrMagnitude != 0 || ball.angularVelocity.sqrMagnitude != 0)) return false;
            }
            return true;
        }

        internal void RestoreCheckpoint(MatchRecoveryCheckpoint checkpoint)
        {
            ShotId = checkpoint.balls.shotId; LastFrame = checkpoint.balls.frame;
            TurnSeat = checkpoint.seat; TurnRevision = checkpoint.turnRevision;
            lastSettled = checkpoint.balls.Copy(); Phase = MatchShotPhase.Ready;
        }
        internal void NotifyRestored() => SnapshotApplied?.Invoke(lastSettled.Copy());

        // Only the later result/turn protocol may open the next shot, after the final state.
        public bool AdvanceTurn(int sender, string matchId, int completedShotId, int nextSeat)
        {
            if (!Active || sender != start.AuthorityActor || matchId != start.MatchId || completedShotId != ShotId ||
                Phase != MatchShotPhase.AwaitingResult || nextSeat < 0 || nextSeat > 1) return false;
            TurnSeat = nextSeat; TurnRevision++; Phase = MatchShotPhase.Ready;
            return true;
        }

        internal bool ApplyResultTransition(int sender, string id, int shotId, int nextSeat, bool idle, bool finished)
        {
            if (!Active || sender != start.AuthorityActor || id != start.MatchId || shotId != ShotId ||
                nextSeat < 0 || nextSeat > 1 || Phase != (idle ? MatchShotPhase.Ready : MatchShotPhase.AwaitingResult)) return false;
            TurnSeat = nextSeat; TurnRevision++;
            Phase = finished ? MatchShotPhase.Finished : MatchShotPhase.Ready;
            return true;
        }
    }
}
