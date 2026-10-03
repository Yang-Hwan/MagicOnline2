using System;
using Cysharp.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Assets.Scripts.Exert.Match;
using Assets.Scripts.Exert.Network;
using Assets.Scripts.Often;
using Assets.Scripts.Sight.Vital.Pavilion;
using Shared = Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital;

// Offline fixture using the real Pavilion geometry/UI. No login, match, or coin writes.
[InitializeOnLoad]
public static class MultiplayerPresentationValidation
{
    const string Key = "MultiplayerPresentationValidation";
    const string Request = "Logs/multiplayer-presentation-validation.request";
    const string ReportPath = "Logs/multiplayer-presentation-validation.json";
    [Serializable] sealed class Report
    {
        public bool completed, passed, gauges, thickness, guides, physics, wire, followOnlySync, trail, openingThree, openingFour;
        public float cueEndError, targetEndError;
        public bool exitCancellation;
        public string error = "";
    }
    static Report report;
    static PhysicsMng physics;
    static ShotCtrl shot;
    static int stage;
    static double changed;
    static Vector3 cueEnd, targetEnd;
    static bool sawMotion;
    static bool running;
    static int callbacksAfterExit;
    static System.Threading.CancellationToken exitToken;
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static MultiplayerPresentationValidation()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += State;
        Application.logMessageReceived += Log;
        if (SessionState.GetBool(Key, false) && File.Exists(ReportPath))
            report = JsonUtility.FromJson<Report>(File.ReadAllText(ReportPath));
    }

    [MenuItem("Tools/Magic Online/Validate Multiplayer Practice Presentation")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Photon.Pun.PhotonNetwork.IsConnected) return;
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save the current scene before validation.");
        SessionState.SetString(Key + ".Scene", SceneManager.GetActiveScene().path);
        SessionState.SetBool(Key + ".Background", Application.runInBackground);
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Pavilion.unity");
        foreach (var root in scene.GetRootGameObjects())
        foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (!behaviour) continue;
            var type = behaviour.GetType();
            if (behaviour is PhysicsMng || behaviour is Ball || behaviour is ShotCtrl || behaviour is RingBall) continue;
            if (behaviour is DrawStuffPos) { behaviour.enabled = false; continue; }
            if (type.Namespace != null && (type.Namespace.StartsWith("UnityEngine") || type.Namespace.StartsWith("TMPro"))) continue;
            UnityEngine.Object.DestroyImmediate(behaviour);
        }
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void PreparePlayers()
    {
        if (!SessionState.GetBool(Key, false)) return;
        NetworkManager.initialized = true;
        PoolPlayer.players = new[] { new PoolPlayer(0, "검증 1", 0, "fixture-0", null, ""),
            new PoolPlayer(1, "검증 2", 0, "fixture-1", null, "") };
        PoolPlayer.SetTurn(0);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void OpeningBeforeStart()
    {
        if (!SessionState.GetBool(Key, false)) return;
        physics = UnityEngine.Object.FindAnyObjectByType<PhysicsMng>();
        physics.SetBalls(3);
        File.WriteAllText("Logs/ball-startup-before.txt", string.Join("\n", physics.balls.Select(b => b.name + " body=" + b.body.position.ToString("F5") + " transform=" + b.transform.position.ToString("F5"))));
    }

    static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            report = new Report(); running = true; sawMotion = false; stage = 0; changed = EditorApplication.timeSinceStartup;
            Application.runInBackground = true;
            physics = UnityEngine.Object.FindAnyObjectByType<PhysicsMng>();
            physics.OnBallMove += (id, position, velocity, spin) => physics.balls[id].OnState(BallState.Move);
            physics.InitRandPosFlutter();
            File.WriteAllText("Logs/ball-scatter-before-step.txt", string.Join("\n", physics.balls.Select(b => b.name + " body=" + b.body.position.ToString("F5") + " transform=" + b.transform.position.ToString("F5"))));
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            Application.runInBackground = SessionState.GetBool(Key + ".Background", false);
            report = report ?? new Report { error = "Fixture did not initialize" };
            report.completed = true;
            report.passed = report.error == "" && report.gauges && report.thickness && report.guides &&
                report.physics && report.wire && report.followOnlySync && report.trail && report.openingThree && report.openingFour && report.exitCancellation;
            File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
            SessionState.SetBool(Key, false);
            string original = SessionState.GetString(Key + ".Scene", "");
            EditorSceneManager.OpenScene(string.IsNullOrEmpty(original) ? "Assets/Scenes/Pavilion.unity" : original);
        }
        if (state == PlayModeStateChange.ExitingPlayMode && report != null)
            File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
    }

    static void Log(string message, string stack, LogType type)
    {
        if (!SessionState.GetBool(Key, false) || report == null) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            report.error += message + "\n" + stack + "\n";
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void Call(object target, string name) => target.GetType().GetMethod(name, Flags).Invoke(target, null);
    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Flags).GetValue(target);

    static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!SessionState.GetBool(Key, false))
        {
            if (File.Exists(Request) && !EditorApplication.isPlayingOrWillChangePlaymode)
            { File.Delete(Request); Run(); }
            return;
        }
        if (!EditorApplication.isPlaying || !running || report == null) return;
        try
        {
            if (report.error != "") { EditorApplication.isPlaying = false; return; }
            double elapsed = EditorApplication.timeSinceStartup - changed;
            Require(elapsed < 65, "Timed out in stage " + stage);
            if (stage == 0 && elapsed > 1)
            {
                physics = UnityEngine.Object.FindAnyObjectByType<PhysicsMng>();
                shot = UnityEngine.Object.FindAnyObjectByType<ShotCtrl>();
                Require(physics && shot && physics.balls.All(b => b.body), "Pavilion bodies failed to initialize");
                File.WriteAllText("Logs/ball-startup-poses.txt", string.Join("\n", physics.balls.Select(b => b.name + " body=" + b.body.position.ToString("F5") + " transform=" + b.transform.position.ToString("F5") + " velocity=" + b.body.linearVelocity.ToString("F5"))));
                for (int i = 0; i < (report.openingThree ? 4 : 3); i++)
                    Require(Mathf.Abs(physics.balls[i].body.position.x) < 1.5f && Mathf.Abs(physics.balls[i].body.position.z) < 1.5f,
                        "Opening scatter lost ball " + i + ": " + physics.balls[i].body.position);
                if (physics.inMove) return;
                if (!report.openingThree)
                {
                    report.openingThree = true;
                    physics.SetBalls(4);
                    for (int i = 0; i < 4; i++)
                        Require(Vector3.Distance(physics.balls[i].body.position, physics.balls[i].transform.position) < .0001f,
                            "Opening placement left a stale Rigidbody pose");
                    physics.InitRandPosFlutter();
                    changed = EditorApplication.timeSinceStartup;
                    return;
                }
                report.openingFour = true;
                physics.SetBalls(4);
                foreach (var ball in physics.balls)
                    Require(!ball.body.useGravity && ball.body.linearDamping == 0 && ball.body.angularDamping == 0 &&
                        (ball.body.constraints & RigidbodyConstraints.FreezePositionY) != 0, "Cloth model not configured");
                var positions = new[] { new Vector3(-.15f,.0285f,-.3f), new Vector3(-.12f,.0285f,0),
                    new Vector3(.3f,.0285f,.6f), new Vector3(-.3f,.0285f,.6f) };
                for (int i = 0; i < physics.balls.Length; i++)
                {
                    physics.balls[i].body.position = positions[i]; physics.balls[i].transform.position = positions[i];
                    physics.balls[i].body.linearVelocity = Vector3.zero; physics.balls[i].body.angularVelocity = Vector3.zero;
                }
                Physics.SyncTransforms();
                shot.CueReadyShot(); shot.cuePivot.rotation = Quaternion.identity;
                shot.cueDisplacement.localPosition = new Vector3(.003f,.006f,0);
                shot.SetFollowThrough(.6f);
                typeof(ShotCtrl).GetProperty("force").SetValue(shot, .15f);
                InputOutput.usedCamera = Camera.main;
                typeof(PoolCoach).GetProperty("isMatchTimePlay").SetValue(PoolCoach.Instance, true);
                var font = UnityEngine.Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None)
                    .FirstOrDefault(t => t.name == "TxtName")?.font;
                if (font) foreach (var text in shot.GetComponentsInChildren<TMPro.TMP_Text>(true)) text.font = font;
                stage = 1; changed = EditorApplication.timeSinceStartup;
            }
            else if (stage == 1 && elapsed > 1 && !shot.PreviewIsCalculating && shot.PreviewCompleted > 0)
            {
                Call(shot, "UpdateGaugeValueLabels");
                Require(Field<TMPro.TextMeshProUGUI>(shot,"powerValueLabel").text == "파워 0.15" &&
                    Field<TMPro.TextMeshProUGUI>(shot,"followValueLabel").text == "팔로 0.60", "Gauge values are incorrect");
                report.gauges = true;
                Require(Field<RectTransform>(shot,"overlapPanel").gameObject.activeSelf &&
                    Field<TMPro.TextMeshProUGUI>(shot,"overlapLabel").text == "예상 두께 47%", "Overlap UI incorrect");
                report.thickness = true;
                Require(shot.cueBallLines[0].positionCount > 2 && Field<LineRenderer[]>(shot,"targetBallLines")[0].positionCount == 2,
                    "Simple cue/first-object guides missing"); report.guides = true;
                var impulse = shot.BuildShotImpulse();
                var decoded = Assets.Scripts.Often.DataManager.ImpulseFromString(Assets.Scripts.Often.DataManager.ImpulseToString(impulse));
                Require(decoded.followThrough == impulse.followThrough && decoded.spinPersistence == impulse.spinPersistence &&
                    Vector3.Distance(decoded.impulse, impulse.impulse) < .001f, "Stroke wire lost follow/spin parameters"); report.wire = true;
                bool unused = shot.cueChanged; shot.SetFollowThrough(.7f);
                Require(shot.cueChanged, "Follow-only change was not synchronized");
                shot.CueControlFromNetwork(shot.cuePivotLocalRotationY, shot.cueVerticalLocalRotationX,
                    shot.cueDisplacementLocalPositionXY, shot.cueSliderLocalPositionZ, .15f, .25f);
                Require(shot.pullBarHandleLocalPositionY == .25f, "Remote follow-only change was dropped"); report.followOnlySync = true;
                shot.SetFollowThrough(.6f);
                ScreenCapture.CaptureScreenshot("Logs/pavilion-practice-aim.png");
                stage = 2; changed = EditorApplication.timeSinceStartup;
            }
            else if (stage == 2 && elapsed > .3)
            {
                var impulse = shot.BuildShotImpulse();
                using (var prediction = new Shared.ShotPrediction(physics))
                {
                    prediction.Calculate(0, new Shared.Impulse(impulse.point, impulse.impulse, Vector3.zero,
                        impulse.followThrough, impulse.spinPersistence), 45, 100);
                    Require(prediction.targetIndex == 1, "Fixture did not hit first object ball");
                    cueEnd = prediction.cuePath.Last(); targetEnd = prediction.targetPath.Last();
                }
                physics.StartShot(physics.balls[0], impulse, ""); shot.CuePutAside();
                stage = 3; changed = EditorApplication.timeSinceStartup;
            }
            else if (stage == 3)
            {
                sawMotion |= physics.inMove;
                if (physics.line.positionCount > 2) report.trail = true;
                if (sawMotion && !physics.inMove)
                {
                    report.cueEndError = Vector3.Distance(cueEnd, physics.balls[0].body.position);
                    report.targetEndError = Vector3.Distance(targetEnd, physics.balls[1].body.position);
                    Require(report.cueEndError < .02f && report.targetEndError < .02f,
                        "Actual Pavilion motion differs from shared prediction: " + report.cueEndError + ", " + report.targetEndError);
                    report.physics = true;
                    ScreenCapture.CaptureScreenshot("Logs/pavilion-practice-trail.png");
                    stage = 4; changed = EditorApplication.timeSinceStartup;
                }
            }
            else if (stage == 4 && elapsed > .5)
            {
                // Leave while stop/effect/trail and remote-message waits are still pending.
                callbacksAfterExit = 0;
                physics.OnEndShot += _ => callbacksAfterExit++;
                physics.OnChoiceStory += (_, __) => callbacksAfterExit++;
                exitToken = Field<System.Threading.CancellationToken>(physics, "lifetimeToken");
                Call(physics, "StopMove");
                Call(physics, "FadeLinePath");
                physics.WaitAndStopMoveFromNetwork(float.MaxValue).Forget();
                physics.endFromNetwork = false;
                physics.SetBallMovStoryFromNetwork(float.MaxValue, "pending exit fixture").Forget();
                var oldScene = physics.gameObject.scene;
                SceneManager.SetActiveScene(SceneManager.CreateScene("Exit Cancellation Validation"));
                SceneManager.UnloadSceneAsync(oldScene);
                stage = 5; changed = EditorApplication.timeSinceStartup;
            }
            else if (stage == 5 && elapsed > 4)
            {
                Require(!physics && exitToken.IsCancellationRequested, "Leaving did not cancel the Pavilion lifetime");
                Require(callbacksAfterExit == 0, "A Pavilion callback ran after leaving the scene");
                report.exitCancellation = true;
                EditorApplication.isPlaying = false;
            }
        }
        catch (Exception e)
        {
            report.error += e.ToString();
            EditorApplication.isPlaying = false;
        }
    }
}
