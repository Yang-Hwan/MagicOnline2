using System;
using Assets.Scripts.Often;
using Assets.Scripts.Prack.BackSys.ChartData;
using UnityEngine;

public static class MatchRecoveryValidation
{
    static void Require(bool value, string message) { if (!value) throw new Exception("Recovery: " + message); }
    public static void Run(int count)
    {
        var starts = new MatchStartSession[2]; var shots = new MatchShotSession[2];
        var results = new MatchResultSession[2]; var recovery = new MatchRecoverySession[2];
        var hall = new SkillMatchData { HallIdx = 1, MatchBall = count, MatchCushion = count == 3 ? 3 : 0, TargetHit = 7, MatchTotMin = 5 };
        var state = new MatchBallSnapshot { matchId = "recover", frame = 0, settled = true, balls = new MatchBallState[count] };
        for (int i = 0; i < count; i++) state.balls[i].rotation = Quaternion.identity;
        for (int i = 0; i < 2; i++)
        {
            var start = starts[i] = new MatchStartSession("recover", "key", MatchConfiguration.Online(hall, "A", "B", 0, i, 20), 11, 22, 0);
            start.AcceptConfiguration(11, "recover", "key"); start.AcceptConfiguration(22, "recover", "key");
            start.MarkSceneReady(11, "recover", "key"); start.MarkSceneReady(22, "recover", "key"); start.CommitStart(11, "recover");
            shots[i] = new MatchShotSession(start); results[i] = new MatchResultSession(start, shots[i]);
            recovery[i] = new MatchRecoverySession(start, shots[i], results[i]);
            shots[i].ApplySnapshot(11, state);
            results[i].Apply(11, results[i].Create(MatchResultKind.Begin, MatchShotOutcome.Miss, 100));
            shots[i].CommitShot(11, new MatchShotCommand { matchId = "recover", shotId = 1, seat = 0, power = .5f });
        }
        Require(!recovery[0].Suspend(100, 1000) && starts[0].Phase == MatchStartPhase.Started, "moving authority paused");
        Require(!recovery[1].Suspend(100, 1000) && starts[1].Phase == MatchStartPhase.Started,
            "moving guest entered reconnect flow");
        state.shotId = 1;
        foreach (var shot in shots) shot.ApplySnapshot(11, state);
        // The guest loses the point result; recovery must restore its missing score and turn revision.
        results[0].Apply(11, results[0].Create(MatchResultKind.Shot, MatchShotOutcome.Point, 101));
        Require(recovery[0].Suspend(102, 1000) && recovery[1].Suspend(102, 1000), "idle suspension failed");
        starts[0].Tick(9999); Require(starts[0].Phase == MatchStartPhase.Suspended, "old setup timeout aborted recovery");
        Require(!results[0].CanShoot(103) && !shots[0].CommitShot(11, new MatchShotCommand()), "suspended input accepted");
        Require(recovery[0].Capture(99, recovery[1].RequestId, 107, 1005) == null, "foreign recovery request accepted");
        string nonce = recovery[1].RequestId;
        var checkpoint = recovery[0].Capture(22, nonce, 107, 1005);
        checkpoint = JsonUtility.FromJson<MatchRecoveryCheckpoint>(JsonUtility.ToJson(checkpoint));
        Require(checkpoint != null && Math.Abs(checkpoint.turnDeadline - 126) < .001, "pause consumed turn time");
        Require(!recovery[1].Restore(22, checkpoint, nonce, 1005) && !recovery[1].Restore(11, checkpoint, "old", 1005), "invalid sender/nonce accepted");
        var invalid = JsonUtility.FromJson<MatchRecoveryCheckpoint>(JsonUtility.ToJson(checkpoint));
        invalid.balls.balls[count - 1].position.x = float.NaN;
        Require(!recovery[1].Restore(11, invalid, nonce, 1005) && results[1].Score0 == 0 && starts[1].Phase == MatchStartPhase.Suspended,
            "malformed checkpoint partially restored state");
        Require(recovery[0].Restore(11, checkpoint, nonce, 1005) && recovery[1].Restore(11, checkpoint, nonce, 1005), "checkpoint restore failed");
        Require(results[1].Score0 == 1 && results[0].Sequence == results[1].Sequence &&
            shots[0].TurnRevision == shots[1].TurnRevision && results[1].CanShoot(108), "restored replicas diverged");
        Require(!recovery[1].Restore(11, checkpoint, nonce, 1006), "checkpoint replayed");
        var stale = new MatchShotCommand { matchId = "recover", shotId = 2, seat = 0, power = .5f, turnRevision = 1 };
        Require(!shots[0].CanApprove(11, stale), "pre-disconnect shot accepted");
        // Exercise the quit callback while a live session can still emit Changed.
        // Keep the recovery pair available for the exact deadline boundary below.
        var quitSession = new MatchStartSession("quit", "key", starts[1].Configuration, 11, 22, 0);
        ValidateQuitBeforeTransportAbort(quitSession);
        Require(recovery[0].Suspend(108, 2000) && recovery[1].Suspend(108, 2000), "second suspension failed");
        var deadlineCheckpoint = recovery[0].Capture(22, recovery[1].RequestId, 137.999, 2029.999);
        Require(deadlineCheckpoint != null, "checkpoint rejected before grace deadline");
        foreach (var item in recovery) item.Tick(2029.999);
        Require(starts[0].Phase == MatchStartPhase.Suspended && starts[1].Phase == MatchStartPhase.Suspended,
            "recovery expired before 30 seconds");
        int previousSequence = results[1].Sequence;
        int previousRevision = shots[1].TurnRevision;
        Require(!recovery[1].Restore(11, deadlineCheckpoint, recovery[1].RequestId, 2030),
            "checkpoint restored at expired deadline");
        recovery[0].Tick(2030);
        Require(starts[0].Phase == MatchStartPhase.Aborted && starts[1].Phase == MatchStartPhase.Aborted &&
            recovery[0].Capture(22, nonce, 138, 2030) == null &&
            results[1].Sequence == previousSequence && shots[1].TurnRevision == previousRevision,
            "expired recovery revived or mutated session");
        Require(!results[0].CanShoot(138) && !results[1].CanShoot(138) &&
            !shots[0].CommitShot(11, stale) && !shots[1].CommitShot(11, stale), "expired match accepted input");
        Require(!recovery[1].Suspend(138, 2031), "aborted session reconnected");
    }

    static void ValidateQuitBeforeTransportAbort(MatchStartSession session)
    {
        // Reproduce transport teardown after the scene's cue/physics are gone.
        var owner = new GameObject("RecoveryQuitValidation");
        bool previousControl = Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.ShotCtrl.canControl;
        try
        {
            var adapter = owner.AddComponent<Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.PracticeMatchAdapter>();
            var type = adapter.GetType();
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            type.GetField("session", flags).SetValue(adapter, session);
            type.GetField("initialized", flags).SetValue(adapter, true);
            var showStatus = (Action)Delegate.CreateDelegate(typeof(Action), adapter, type.GetMethod("ShowStatus", flags));
            session.Changed += showStatus;
            owner.SendMessage("OnApplicationQuit");
            session.Abort("leave");
            Require(session.Phase == MatchStartPhase.Aborted &&
                !Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.ShotCtrl.canControl,
                "quit did not stop control before transport abort");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
            Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.ShotCtrl.canControl = previousControl;
        }
    }
}
