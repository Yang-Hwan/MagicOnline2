using System;
using Assets.Scripts.Often;
using Assets.Scripts.Prack.BackSys.ChartData;
using UnityEngine;

// Runs without Photon or a physics scene; exercises two independent protocol replicas.
public static class MatchShotProtocolValidation
{
    public static void Run(int ballCount)
    {
        void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("Shot protocol: " + message); }
        var hall = new SkillMatchData { HallIdx = 1, MatchBall = ballCount, TargetHit = 7,
            MatchCushion = ballCount == 3 ? 3 : 0, MatchTotMin = 5 };
        MatchStartSession Make(int seat) => new MatchStartSession("match", "rules",
            MatchConfiguration.Online(hall, "Host", "Guest", 0, seat, 20), 11, 22, 0);
        var hostStart = Make(0); var guestStart = Make(1);
        var host = new MatchShotSession(hostStart); var guest = new MatchShotSession(guestStart);
        var snapshot = new MatchBallSnapshot { matchId = "match", shotId = 0, frame = 0,
            settled = true, balls = new MatchBallState[ballCount] };
        for (int i = 0; i < ballCount; i++) snapshot.balls[i].rotation = Quaternion.identity;
        Require(!host.ApplySnapshot(11, snapshot), "state accepted before start");
        foreach (var start in new[] { hostStart, guestStart })
        {
            start.AcceptConfiguration(11, "match", "rules"); start.AcceptConfiguration(22, "match", "rules");
            start.MarkSceneReady(11, "match", "rules"); start.MarkSceneReady(22, "match", "rules");
            Require(start.CommitStart(11, "match"), "start failed");
        }
        var shot = new MatchShotCommand { matchId = "match", shotId = 1, seat = 0,
            power = .6f, followThrough = .8f, yaw = 90, contact = new Vector2(.3f, -.4f) };
        Require(!host.CanApprove(11, shot), "shot accepted before initial state");
        Require(!guest.ApplySnapshot(22, snapshot), "guest injected a state");
        var malformed = snapshot.Copy(); malformed.balls[ballCount - 1].position.x = float.NaN;
        Require(!host.ApplySnapshot(11, malformed) && host.LastFrame == -1, "invalid state partially applied");
        malformed = snapshot.Copy(); malformed.balls = new MatchBallState[2];
        Require(!host.ApplySnapshot(11, malformed), "wrong ball count accepted");
        malformed = snapshot.Copy(); malformed.balls[0].rotation = default;
        Require(!host.ApplySnapshot(11, malformed), "invalid rotation accepted");
        malformed = snapshot.Copy(); malformed.balls[0].velocity = Vector3.right;
        Require(!host.ApplySnapshot(11, malformed), "moving initial state accepted");
        // Exercise the same JSON format used by Photon, including stroke metadata.
        shot = JsonUtility.FromJson<MatchShotCommand>(JsonUtility.ToJson(shot));
        Require(shot.power == .6f && shot.followThrough == .8f && shot.contact == new Vector2(.3f, -.4f), "stroke serialization changed values");
        snapshot = JsonUtility.FromJson<MatchBallSnapshot>(JsonUtility.ToJson(snapshot));
        Require(host.ApplySnapshot(11, snapshot) && guest.ApplySnapshot(11, snapshot), "initial state rejected");
        Require(!host.ApplySnapshot(11, snapshot), "initial state applied twice");
        Require(!host.CanApprove(99, shot) && !host.CanApprove(22, shot) && !guest.CanApprove(11, shot), "wrong actor could approve");
        foreach (float power in new[] { 0f, -1f, 1.01f, float.NaN, float.PositiveInfinity })
        { var bad = shot.Copy(); bad.power = power; Require(!host.CanApprove(11, bad), "invalid power accepted"); }
        var invalid = shot.Copy(); invalid.contact = Vector2.one;
        Require(!host.CanApprove(11, invalid), "outside contact accepted");
        invalid = shot.Copy(); invalid.followThrough = float.NaN;
        Require(!host.CanApprove(11, invalid), "invalid follow-through accepted");
        invalid = shot.Copy(); invalid.yaw = 360;
        Require(!host.CanApprove(11, invalid), "invalid angle accepted");
        invalid = shot.Copy(); invalid.matchId = "old";
        Require(!host.CanApprove(11, invalid), "old match accepted");
        invalid = shot.Copy(); invalid.shotId = 2;
        Require(!host.CanApprove(11, invalid), "future shot accepted");
        int hostShots = 0, guestShots = 0;
        host.ShotCommitted += _ => hostShots++; guest.ShotCommitted += _ => guestShots++;
        Require(!guest.CommitShot(22, shot), "non-authority commit accepted");
        Require(host.CanApprove(11, shot) && host.CommitShot(11, shot) && guest.CommitShot(11, shot), "valid shot rejected");
        Require(!host.CommitShot(11, shot) && !guest.CommitShot(11, shot), "duplicate shot replayed");
        var next = shot.Copy(); next.shotId = 2;
        Require(!host.CanApprove(11, next), "shot during motion accepted");
        Require(!host.AdvanceTurn(11, "match", 1, 1), "turn advanced while moving");
        snapshot.shotId = 1; snapshot.frame = 5; snapshot.settled = false;
        snapshot.balls[0].velocity = Vector3.right;
        Require(host.ApplySnapshot(11, snapshot) && guest.ApplySnapshot(11, snapshot), "motion rejected");
        snapshot.frame = 4;
        Require(!guest.ApplySnapshot(11, snapshot), "out-of-order frame accepted");
        snapshot.frame = 6; snapshot.shotId = 0;
        Require(!guest.ApplySnapshot(11, snapshot), "previous shot frame accepted");
        snapshot.shotId = 1; snapshot.settled = true;
        Require(!guest.ApplySnapshot(11, snapshot), "moving final state accepted");
        snapshot.balls[0].velocity = Vector3.zero;
        Require(host.ApplySnapshot(11, snapshot) && guest.ApplySnapshot(11, snapshot), "terminal state rejected");
        Require(host.Phase == MatchShotPhase.AwaitingResult && !host.CanApprove(11, next), "next shot opened before result");
        snapshot.frame++;
        Require(!guest.ApplySnapshot(11, snapshot), "state changed after final state");
        Require(!host.AdvanceTurn(22, "match", 1, 1) && !host.AdvanceTurn(11, "match", 0, 1), "unauthorized/stale result accepted");
        Require(host.AdvanceTurn(11, "match", 1, 1) && guest.AdvanceTurn(11, "match", 1, 1), "next turn rejected");
        Require(!host.AdvanceTurn(11, "match", 1, 0), "result replay changed seat");
        next.seat = 1;
        next.turnRevision = host.TurnRevision;
        Require(!host.CanApprove(11, next) && host.CanApprove(22, next), "guest turn actor mapping failed");
        Require(host.CommitShot(11, next) && guest.CommitShot(11, next) && hostShots == 2 && guestShots == 2, "guest shot diverged");
        hostStart.Abort("test"); guestStart.Abort("test");
        snapshot.shotId = 2;
        Require(!guest.ApplySnapshot(11, snapshot) && !host.CommitShot(11, next), "aborted match revived");
    }
}
