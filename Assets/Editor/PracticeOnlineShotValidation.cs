using System;
using Assets.Scripts.Often;
using Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital;
using UnityEngine;

// One actual physics authority scene, or one kinematic follower scene, with a local wire.
public sealed class PracticeOnlineShotValidation
{
    readonly PhysicsMng physics;
    readonly MatchStartSession session;
    readonly MatchShotSession shots;
    readonly MatchResultSession results;
    readonly bool authority;
    Vector3 before;
    MatchBallSnapshot last;
    int motionFrames;
    float began;
    bool moved, verified, capturedResult;
    float resultShownAt;
    double clockTime = 100;

    void VerifySceneRecovery()
    {
        var recovery = new MatchRecoverySession(session, shots, results);
        Require(recovery.Suspend(clockTime, 1000) && !physics.OnlineAdapter.CanSubmit && !ShotCtrl.canControl,
            "suspension did not lock scene input");
        string nonce = recovery.RequestId;
        var checkpoint = authority ? recovery.Capture(session.ActorForSeat(1), nonce, clockTime + 5, 1005) :
            new MatchRecoveryCheckpoint { matchId = session.MatchId, key = session.ConfigurationKey, requestId = nonce,
                seat = shots.TurnSeat, turnRevision = shots.TurnRevision + 1, sequence = results.Sequence,
                score0 = results.Score0, score1 = results.Score1, time = clockTime + 5,
                matchDeadline = results.MatchDeadline + 5, turnDeadline = results.TurnDeadline + 5, balls = shots.LastSettled };
        var before = physics.ballcs[0].body.position;
        clockTime += 5;
        Require(recovery.Restore(session.AuthorityActor, checkpoint, nonce, 1005) &&
            session.Phase == MatchStartPhase.Started && physics.ballcs[0].body.position == before,
            "scene state did not restore after suspension");
        Require(physics.OnlineAdapter.CanSubmit == authority, "wrong seat unlocked after recovery");
    }
    static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException("Online scene shot: " + message); }

    public PracticeOnlineShotValidation(PhysicsMng physics, MatchStartSession session)
    {
        this.physics = physics; this.session = session;
        authority = session.LocalActor == session.AuthorityActor;
        shots = new MatchShotSession(session);
        results = new MatchResultSession(session, shots);
        var adapter = physics.OnlineAdapter;
        adapter.BindChannel(shots,
            command => results.CanShoot(clockTime) && shots.CanApprove(session.LocalActor, command) && shots.CommitShot(session.AuthorityActor, command),
            state => {
                last = JsonUtility.FromJson<MatchBallSnapshot>(JsonUtility.ToJson(state));
                if (!state.settled) motionFrames++;
                return shots.ApplySnapshot(session.AuthorityActor, last);
            });
        adapter.BindResults(results, message => results.Apply(session.AuthorityActor,
            JsonUtility.FromJson<MatchResultMessage>(JsonUtility.ToJson(message))), () => clockTime);
        var command = new MatchShotCommand { matchId = session.MatchId, shotId = 1, seat = 0,
            power = .15f, followThrough = .8f, yaw = 40, contact = new Vector2(.2f, -.3f) };
        if (authority)
        {
            Require(last != null && last.settled && shots.Phase == MatchShotPhase.Ready && adapter.CanSubmit,
                "initial authority layout did not open input");
            VerifySceneRecovery();
            var controller = physics.shotController;
            var aim = controller.CapturePracticeAim();
            controller.cuePivot.localRotation = Quaternion.Euler(0, 37, 0);
            controller.cueDisplacement.localPosition = new Vector3(.005f, -.006f, 0);
            controller.SetFollowThrough(.8f);
            typeof(ShotCtrl).GetProperty("force").SetValue(controller, .15f);
            var localImpulse = controller.BuildShotImpulse();
            var captured = controller.CaptureOnlineStroke(); captured.seat = 0;
            var networkImpulse = controller.BuildOnlineImpulse(captured);
            var centre = physics.ballcs[0].body.worldCenterOfMass;
            Require(Vector3.Distance(localImpulse.impulse, networkImpulse.impulse) < .00001f &&
                Vector3.Distance(Vector3.Cross(localImpulse.point - centre, localImpulse.impulse),
                    Vector3.Cross(networkImpulse.point - centre, networkImpulse.impulse)) < .00001f &&
                Mathf.Abs(localImpulse.spinPersistence - networkImpulse.spinPersistence) < .00001f,
                "network stroke differs from practice force/spin model");
            controller.RestorePracticeAim(aim);
            before = physics.ballcs[0].body.position;
            // Force a real cue/object collision to verify event routing, not only message arithmetic.
            var targetPosition = before + (Quaternion.Euler(0, command.yaw, 0) * Vector3.forward) * .09f;
            physics.ballcs[1].body.position = targetPosition;
            physics.ballcs[1].transform.position = targetPosition;
            Physics.SyncTransforms();
            Require(adapter.Submit(command) && physics.inMove && !adapter.CanSubmit, "approved authority shot did not start");
            Require(!adapter.Submit(command), "second shot accepted while moving");
        }
        else
        {
            last = new MatchBallSnapshot { matchId = session.MatchId, frame = 0, settled = true,
                balls = new MatchBallState[physics.ballcs.Length] };
            for (int i = 0; i < last.balls.Length; i++)
            {
                last.balls[i].position = physics.ballcs[i].body.position;
                last.balls[i].rotation = physics.ballcs[i].body.rotation;
                Require(physics.ballcs[i].body.isKinematic, "follower body is dynamic");
            }
            Require(shots.ApplySnapshot(session.AuthorityActor, last), "follower rejected initial state");
            Require(results.Apply(session.AuthorityActor, results.Create(MatchResultKind.Begin, MatchShotOutcome.Miss, 100)),
                "follower clock initialization failed");
            VerifySceneRecovery();
            command.turnRevision = shots.TurnRevision;
            Require(!adapter.CanSubmit && !adapter.Submit(command), "follower could play opponent's turn");
            Require(shots.CommitShot(session.AuthorityActor, command), "follower rejected approved shot");
            before = physics.ballcs[0].body.position;
            last.shotId = 1; last.frame = 1; last.settled = false;
            last.balls[0].position += Vector3.right * .1f;
            last.balls[0].velocity = Vector3.right;
            Require(shots.ApplySnapshot(session.AuthorityActor, last), "follower rejected motion frame");
            Require(physics.ballcs[0].body.position == before && !physics.inMove,
                "follower teleported or simulated upon motion receipt");
        }
        began = Time.unscaledTime;
    }

    public bool Tick()
    {
        if (verified)
        {
            if (Time.unscaledTime - resultShownAt < .6f) return false;
            var view = UnityEngine.Object.FindAnyObjectByType<Assets.Scripts.Sight.Surface.Pavilion.PopMatchFinish>();
            Require(view && view.transform.Find("Rematch") && view.transform.Find("Archive") && view.transform.Find("Leave"),
                "online result actions missing");
            Require(view.transform.Find("Archive").GetComponent<UnityEngine.UI.Button>().interactable &&
                !view.transform.Find("Rematch").GetComponent<UnityEngine.UI.Button>().interactable,
                "archive unavailable or unbound network rematch enabled");
            if (!capturedResult)
            {
                ScreenCapture.CaptureScreenshot($"Logs/online-result-{physics.ballcs.Length}.png");
                capturedResult = true; resultShownAt = Time.unscaledTime;
                return false;
            }
            return true;
        }
        Require(session.Phase == MatchStartPhase.Started, "session aborted: " + session.AbortReason);
        moved |= Vector3.Distance(physics.ballcs[0].body.position, before) > .001f;
        if (authority)
        {
            if (results.Sequence < 2) return false;
            Require(moved && motionFrames > 0 && !physics.inMove && last.settled,
                "authority did not simulate and publish a terminal state");
            Require(physics.OnlineAdapter.RecordedBallContacts > 0, "actual collision never reached authoritative adjudication");
            for (int i = 0; i < last.balls.Length; i++)
                Require(Vector3.Distance(last.balls[i].position, physics.ballcs[i].body.position) < .00001f &&
                    last.balls[i].velocity == Vector3.zero, "terminal state differs from physical state");
            Require(shots.Phase == MatchShotPhase.Ready, "result did not reopen next turn");
            Require(results.Apply(session.AuthorityActor, results.Create(MatchResultKind.MatchTimeout,
                MatchShotOutcome.Miss, results.MatchDeadline + 1)) && results.Finished,
                "authoritative match timeout did not finish scene");
        }
        else
        {
            if (Time.unscaledTime - began < .15f) return false;
            Require(moved && Vector3.Distance(physics.ballcs[0].body.position, last.balls[0].position) < .0001f,
                "follower interpolation did not reach the sample");
            Require(physics.line.positionCount >= 2 &&
                Vector3.Distance(physics.line.GetPosition(physics.line.positionCount - 1),
                    Vector3.ProjectOnPlane(last.balls[0].position, Vector3.up)) < .0001f,
                "follower cue trail did not follow the displayed sample");
            Require(Mathf.Abs(physics.shotController.force - .15f) < .00001f &&
                Mathf.Abs(physics.shotController.pull - .8f) < .00001f,
                "follower gauges did not show the approved stroke");
            last.frame++; last.settled = true; last.balls[0].velocity = Vector3.zero;
            last.balls[0].position += Vector3.forward * .02f;
            Require(shots.ApplySnapshot(session.AuthorityActor, last) &&
                Vector3.Distance(physics.ballcs[0].body.position, last.balls[0].position) < .00001f,
                "follower final correction failed");
            Require(!physics.OnlineAdapter.CanSubmit, "turn opened before result");
            Require(results.Apply(session.AuthorityActor, results.Create(MatchResultKind.Shot, MatchShotOutcome.Foul, clockTime)) &&
                physics.OnlineAdapter.CanSubmit, "guest turn did not open after remote foul");
            for (int i = 0; i < session.Configuration.TargetScore; i++)
            {
                var command = new MatchShotCommand { matchId = session.MatchId, shotId = shots.ShotId + 1,
                    seat = 1, turnRevision = shots.TurnRevision, power = .1f };
                Require(shots.CommitShot(session.AuthorityActor, command), "guest scoring shot rejected");
                last.shotId = command.shotId; last.frame = 0;
                Require(shots.ApplySnapshot(session.AuthorityActor, last), "guest terminal state rejected");
                Require(results.Apply(session.AuthorityActor, results.Create(MatchResultKind.Shot, MatchShotOutcome.Point, clockTime)),
                    "guest point result rejected");
            }
            Require(results.Finished && results.Winner == 1 && results.Score1 == session.Configuration.TargetScore,
                "guest target win failed");
            var texts = UnityEngine.Object.FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None);
            Require(Array.Exists(texts, text => text.text == "승리하였습니다"), "guest victory popup missing");
        }
        Require(!physics.OnlineAdapter.CanSubmit && !ShotCtrl.canControl,
            "next turn opened before result approval");
        VerifyArchive(); verified = true; resultShownAt = Time.unscaledTime;
        return false;
    }
    void VerifyArchive()
    {
        var archive = physics.OnlineAdapter.ReplayArchive;
        Require(archive.Ready, "completed shot was not recorded");
        string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MagicOnlineReplay-" + Guid.NewGuid().ToString("N"));
        try
        {
            System.IO.Directory.CreateDirectory(directory);
            string occupied = System.IO.Path.Combine(directory, "slot01.bin");
            System.IO.File.WriteAllText(occupied, "existing-user-record");
            Require(archive.SaveToEmptySlot(directory) == 1 && System.IO.File.ReadAllText(occupied) == "existing-user-record",
                "archive overwrote an occupied slot");
            using (var reader = new System.IO.BinaryReader(System.IO.File.OpenRead(System.IO.Path.Combine(directory, "slot02.bin"))))
            {
                Require(reader.ReadInt32() == 0x4D525031 && reader.ReadInt32() == 3 &&
                    reader.ReadInt32() == physics.ballcs.Length, "wrong replay format/ball count");
                int cue = reader.ReadInt32(); float step = reader.ReadSingle(); int count = reader.ReadInt32();
                Require(cue == (authority ? 0 : 1) && Mathf.Approximately(step, Time.fixedDeltaTime) && count >= 2 && reader.ReadBoolean(), "missing replay stroke");
                float power = reader.ReadSingle(), follow = reader.ReadSingle();
                float x = reader.ReadSingle(), y = reader.ReadSingle();
                for (int i = 0; i < 4; i++) reader.ReadSingle();
                Require(Mathf.Approximately(power, authority ? .15f : .1f) && Mathf.Approximately(follow, authority ? .8f : 0),
                    "approved power/follow were not saved");
                Require(Mathf.Approximately(x, authority ? 80f : 0) && Mathf.Approximately(y, authority ? -120f : 0), "contact not saved");
                Require(reader.BaseStream.Length == reader.BaseStream.Position + count * (4L + 52L * physics.ballcs.Length + 54L), "invalid replay frame length");
                for (int frame = 0; frame < count; frame++)
                {
                    Require(Mathf.Abs(reader.ReadSingle() - frame * step) < .0001f, "replay timing is not uniform");
                    for (int ball = 0; ball < physics.ballcs.Length; ball++)
                    {
                        var position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                        if (frame == count - 1) Require(Vector3.Distance(position, last.balls[ball].position) < .00001f, "replay final state differs from authority");
                        for (int component = 0; component < 10; component++) reader.ReadSingle();
                    }
                    Require(!reader.ReadBoolean() && !reader.ReadBoolean(), "network replay unexpectedly contains local cue animation");
                    for (int component = 0; component < 3 + 4 + 3 + 3; component++) reader.ReadSingle();
                }
            }
            for (int i = 2; i < 20; i++) System.IO.File.WriteAllText(System.IO.Path.Combine(directory, $"slot{i + 1:00}.bin"), "occupied");
            Require(archive.SaveToEmptySlot(directory) == -1, "full archive replaced a slot");
            Require(!physics.CanReplayShot && !physics.IsPracticeReplay, "archive enabled live match playback");
        }
        finally
        {
            // This directory is a fixture-created unique directory, never a user replay directory.
            if (System.IO.Directory.Exists(directory)) System.IO.Directory.Delete(directory, true);
        }
    }
}
