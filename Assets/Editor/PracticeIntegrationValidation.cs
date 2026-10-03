using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using NetworkManager = Assets.Scripts.Exert.Network.NetworkManager;
using Assets.Scripts.Often;
using Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital;
using Assets.TutorialInfo.Scripts.TableSet06.Excert.Match;

// Opt-in local smoke test. Does not log in, contact the backend, or save scenes.
[InitializeOnLoad]
public static class PracticeIntegrationValidation
{
    const string Key = "PracticeIntegrationValidation.Running";
    const string Request = "Logs/practice-validation.request";
    const string ReportPath = "Logs/practice-validation.json";
    [Serializable] class Report
    {
        public bool completed, passed, secondButtonVerified, physicsRestored;
        public int visits, shots, missingScripts, missingReferences, unsupportedMaterials;
        public bool threeBalls, fourBalls, submenuReturn, replayBrowser;
        public int replaySlotsPlayed;
        public int motionVisualChecks;
        public int matchRulesChecks;
        public int finishReturnChecks;
        public int replayStrokeChecks;
        public int replayOverlapChecks;
        public bool replayPanelDrag;
        public int replayFolderChecks;
        public int savePanelDragChecks, replayImeChecks;
        public int eggAnimationChecks;
        public bool networkCleanup;
        public int matchConfigurationChecks;
        public int matchmakingCompatibilityChecks;
        public int onlineBootstrapChecks;
        public int shotProtocolChecks;
        public int onlinePhysicsChecks;
        public int resultProtocolChecks;
        public int recoveryChecks;
        public int rematchChecks, onlineReplayChecks, testRouteChecks;
        public bool practiceAfterOnline;
        public List<string> errors = new List<string>();
    }
    static Report report;
    static int stage;
    static double changed;
    static PhysicsMng physics;
    static Vector3 beforeShot;
    static float fixedStep, bounce, sleep, contact;
    static int solver, velocitySolver;
    static SimulationMode simulation;
    static int replaySlot;
    static int[] savedSlots;
    static MatchStartSession bootstrap;
    static PracticeOnlineShotValidation onlineShot;

    static PracticeIntegrationValidation()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += State;
        Application.logMessageReceived += Log;
        if (SessionState.GetBool(Key, false) && File.Exists(ReportPath))
            report = JsonUtility.FromJson<Report>(File.ReadAllText(ReportPath));
    }

    [MenuItem("Tools/Magic Online/Validate Practice Integration")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save the current scene before validation.");
        SessionState.SetString(Key + ".Scene", SceneManager.GetActiveScene().path);
        SessionState.SetBool(Key + ".Background", Application.runInBackground);
        // Apply through the editor as well: an open editor may cache the old
        // scene list even after EditorBuildSettings.asset was changed on disk.
        var scenes = EditorBuildSettings.scenes.ToList();
        var practice = scenes.Find(s => s.path == PracticeSceneFlow.ScenePath);
        if (practice == null) scenes.Add(new EditorBuildSettingsScene(PracticeSceneFlow.ScenePath, true));
        else practice.enabled = true;
        EditorBuildSettings.scenes = scenes.ToArray();
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tagManager.FindProperty("layers");
        string[] practiceLayers = { "Cloth", "CueBall", "Ball", "Board", "Pocket", "NoVisible",
            "Ceiling", "CueControl", "Stuff", "Piece", "Item", "EffectItem" };
        for (int i = 0; i < practiceLayers.Length; i++)
        {
            var layer = layers.GetArrayElementAtIndex(i + 8);
            if (!string.IsNullOrEmpty(layer.stringValue) && layer.stringValue != practiceLayers[i])
                throw new InvalidOperationException("Layer conflict at " + (i + 8));
            layer.stringValue = practiceLayers[i];
        }
        tagManager.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        SpinPersistenceValidation.Run();
        report = new Report();
        var scene = EditorSceneManager.OpenScene(PracticeSceneFlow.ScenePath);
        foreach (var root in scene.GetRootGameObjects())
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            report.missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            foreach (var c in t.GetComponents<Component>())
            {
                if (!c) continue;
                using (var so = new SerializedObject(c))
                {
                    var p = so.GetIterator();
                    while (p.NextVisible(true))
                        if (p.propertyType == SerializedPropertyType.ObjectReference &&
                            p.objectReferenceValue == null && p.objectReferenceEntityIdValue != default)
                        {
                            report.missingReferences++;
                            report.errors.Add(t.name + ": " + p.propertyPath);
                        }
                }
            }
            foreach (var renderer in t.GetComponents<Renderer>())
            foreach (var mat in renderer.sharedMaterials)
                if (mat && (!mat.shader || !mat.shader.isSupported)) report.unsupportedMaterials++;
        }
        EditorSceneManager.OpenScene("Assets/Scenes/Consist.unity");
        Save();
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }

    static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            Application.runInBackground = true;
            fixedStep = Time.fixedDeltaTime; simulation = Physics.simulationMode;
            bounce = Physics.bounceThreshold; sleep = Physics.sleepThreshold;
            contact = Physics.defaultContactOffset;
            solver = Physics.defaultSolverIterations; velocitySolver = Physics.defaultSolverVelocityIterations;
            stage = 0; changed = EditorApplication.timeSinceStartup;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            Application.runInBackground = SessionState.GetBool(Key + ".Background", false);
            report.completed = true;
            report.passed = report.errors.Count == 0 && report.visits == 3 && report.shots == 2 &&
                report.threeBalls && report.fourBalls && report.submenuReturn && report.replayBrowser &&
                report.replaySlotsPlayed == savedSlots.Length &&
                report.motionVisualChecks == 2 &&
                report.matchRulesChecks == 2 &&
                report.finishReturnChecks == 2 &&
                report.replayStrokeChecks == 2 &&
                report.replayPanelDrag &&
                report.eggAnimationChecks > 0 &&
                report.networkCleanup &&
                report.matchConfigurationChecks == 2 &&
                report.matchmakingCompatibilityChecks == 2 &&
                report.onlineBootstrapChecks == 2 && report.shotProtocolChecks == 2 && report.onlinePhysicsChecks == 2 && report.resultProtocolChecks == 2 && report.recoveryChecks == 2 && report.rematchChecks == 2 && report.onlineReplayChecks == 2 && report.testRouteChecks == 2 && report.practiceAfterOnline &&
                report.physicsRestored && report.secondButtonVerified && report.missingScripts == 0 &&
                report.missingReferences == 0 && report.unsupportedMaterials == 0;
            Save(); SessionState.SetBool(Key, false);
            var path = SessionState.GetString(Key + ".Scene", "");
            if (!string.IsNullOrEmpty(path)) EditorSceneManager.OpenScene(path);
            Debug.Log("Practice integration " + (report.passed ? "PASSED" : "FAILED") + ": " + ReportPath);
        }
    }

    static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!SessionState.GetBool(Key, false))
        {
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Request); Run(); return;
        }
        if (!EditorApplication.isPlaying) return;
        try
        {
            double elapsed = EditorApplication.timeSinceStartup - changed;
            if (report.errors.Count > 0) { EditorApplication.isPlaying = false; return; }
            if (elapsed > 75) throw new Exception("Timed out in validation stage " + stage);
            if (stage == 0 && elapsed > 1 && Time.timeSinceLevelLoad > .5f)
            {
                if (!report.networkCleanup)
                {
                    VerifyNetworkCleanup();
                    report.networkCleanup = true;
                }
                var buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                    .Where(b => b.transform.parent && b.transform.parent.name == "PnlNav")
                    .OrderByDescending(b => b.transform.position.y).ToArray();
                if (buttons.Length != 5 || buttons[1].name != "Nav00 (1)") throw new Exception("Unexpected menu ordering");
                report.secondButtonVerified = true;
                buttons[1].onClick.Invoke(); Next(6);
            }
            else if (stage == 6 && elapsed > 1)
            {
                if (SceneManager.GetActiveScene().name != "Consist") throw new Exception("Navigation skipped the submenu");
                var cards = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                    .Where(b => b.transform.parent && b.transform.parent.name == "Practice Menu").ToArray();
                if (cards.Length != 3) throw new Exception("Expected three practice cards; found " + cards.Length + "; " +
                    string.Join(", ", UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                        .Where(b => b.name.Contains("Practice") || b.name == "Replay Browser")
                        .Select(b => b.name + ": active=" + b.gameObject.activeInHierarchy + ", parent=" + b.transform.parent.name)));
                var page = (RectTransform)cards[0].transform.parent.parent;
                var homePage = (RectTransform)page.parent.GetChild(0);
                if (Mathf.Abs(page.rect.height - homePage.rect.height) > .5f)
                    throw new Exception("Practice page height must match the other menu pages");
                ScreenCapture.CaptureScreenshot("Logs/practice-submenu.png");
                Next(9);
            }
            else if (stage == 9 && elapsed > .5)
            {
                string[] names = { "Three Ball Practice", "Four Ball Practice", "Replay Browser" };
                UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                    .Single(b => b.name == names[report.visits]).onClick.Invoke();
                Next(1);
            }
            else if (stage == 1 && SceneManager.GetActiveScene().path == PracticeSceneFlow.ScenePath)
            {
                physics = UnityEngine.Object.FindAnyObjectByType<PhysicsMng>();
                int expected = report.visits == 0 ? 3 : 4;
                if (!physics || physics.ballcs.Length != expected ||
                    UnityEngine.Object.FindObjectsByType<BallC>(FindObjectsSortMode.None).Length != expected)
                    throw new Exception("Incorrect practice ball count");
                if (expected == 3) report.threeBalls = true; else report.fourBalls = true;
                report.visits++;
                if (Mathf.Abs(Time.fixedDeltaTime - 0.01f) > 0.00001f || Physics.simulationMode != SimulationMode.Script)
                    throw new Exception("Practice physics settings were not applied");
                Next(report.visits == 3 ? 7 : 2);
            }
            else if (stage == 2 && physics && PoolCoach.Instance.isMatchTimePlay && !physics.inMove)
            {
                VerifyMatchConfiguration();
                report.matchConfigurationChecks++;
                VerifyPracticeMatch();
                report.matchRulesChecks++;
                var ball = physics.ballcs[0].body;
                beforeShot = ball.position;
                var shot = UnityEngine.Object.FindAnyObjectByType<ShotCtrl>();
                shot.CueReadyShot();
                // Simulate a long abandoned stroke. The real stroke must discard these frames.
                shot.cueSlider.localPosition = new Vector3(0, 0, -.8f);
                physics.BeginReplayCueStroke();
                for (int i = 0; i < 200; i++)
                    typeof(PhysicsMng).GetMethod("RecordCuePreparation", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(physics, null);
                shot.cuePivot.rotation = Quaternion.LookRotation(Vector3.right);
                shot.cueSlider.localPosition = new Vector3(0, 0, -.2f);
                typeof(PhysicsMng).GetMethod("RecordCuePreparation", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(physics, null);
                typeof(ShotCtrl).GetProperty("force").SetValue(shot, .63f);
                shot.SetFollowThrough(.77f);
                UnityEngine.Object.FindAnyObjectByType<Assets.TutorialInfo.Scripts.TableSet06.Sight.Surface.BallPointBig>(FindObjectsInactive.Include)
                    .RestorePracticePoint(new Vector2(84, -116));
                typeof(ShotCtrl).GetMethod("WaitAndStartShot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(shot, null);
                Next(3);
            }
            else if (stage == 3 && physics && Vector3.Distance(physics.ballcs[0].body.position, beforeShot) > 0.02f)
            {
                VerifyMotionVisual(physics.ballcs[0]);
                report.motionVisualChecks++;
                report.shots++;
                ScreenCapture.CaptureScreenshot("Logs/practice-" + physics.ballcs.Length + "-balls.png");
                Next(4);
            }
            else if (stage == 4 && elapsed > 2)
            {
                if (report.visits < 3)
                {
                    if (!physics.CanReplayShot) return;
                    if (report.replayStrokeChecks < report.visits)
                    {
                        VerifyReplayStroke();
                        report.replayStrokeChecks++;
                        physics.ResetPracticeReplay();
                        typeof(PhysicsMng).GetMethod("OpenReplaySavePanel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(physics, null);
                        physics.SelectReplaySlot(0);
                        ScreenCapture.CaptureScreenshot("Logs/practice-replay-stroke-" + physics.ballcs.Length + "-balls.png");
                        Next(11);
                        return;
                    }
                    var coach = PoolCoach.Instance;
                    var cue = physics.ballcs[PoolPlayer.turnId];
                    for (int attempt = 0; !PoolLogic.gameState.gameIsComplete && attempt <= coach.targetHit; attempt++)
                    {
                        if (physics.ballcs.Length == 3)
                            for (int i = 0; i < 3; i++)
                                PoolLogic.Instance.OnBallHitBoard(cue, Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.CushionDir.T);
                        PoolLogic.Instance.OnCueBallHitBall(cue, physics.ballcs[physics.ballcs.Length == 3 ? 1 - PoolPlayer.turnId : 3]);
                        PoolLogic.Instance.OnCueBallHitBall(cue, physics.ballcs[2]);
                        typeof(PoolCoach).GetMethod("PhysicsManager_OnBallAllStop",
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(coach, new object[] { "" });
                    }
                    if (!PoolLogic.gameState.gameIsComplete) throw new Exception("Synthetic winning shot did not complete");
                    VerifyFinishPopup();
                    ScreenCapture.CaptureScreenshot("Logs/practice-victory-" + physics.ballcs.Length + "-balls.png");
                    Next(10);
                }
                else
                {
                    UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b => b.name == "BtnMatchEnd").onClick.Invoke();
                    Next(5);
                }
            }
            else if (stage == 11 && elapsed > .5)
            {
                VerifyReplayPanelDrag(true);
                physics.ExitPracticeReplay();
                Next(4);
            }
            else if (stage == 10)
            {
                if (SceneManager.GetActiveScene().name == "Consist")
                {
                    if (elapsed < 2.9) throw new Exception("Victory popup closed before three seconds");
                    report.finishReturnChecks++;
                    Next(5);
                }
                else
                {
                    VerifyFinishPopup();
                    if (elapsed > 10) throw new Exception("Victory popup did not return to the menu");
                }
            }
            else if (stage == 5 && elapsed > 1 && Time.timeSinceLevelLoad > .5f && SceneManager.GetActiveScene().name == "Consist")
            {
                report.physicsRestored = Mathf.Approximately(Time.fixedDeltaTime, fixedStep) && Physics.simulationMode == simulation &&
                    Mathf.Approximately(Physics.bounceThreshold, bounce) && Mathf.Approximately(Physics.sleepThreshold, sleep) &&
                    Mathf.Approximately(Physics.defaultContactOffset, contact) && Physics.defaultSolverIterations == solver &&
                    Physics.defaultSolverVelocityIterations == velocitySolver;
                if (!report.physicsRestored) throw new Exception("Physics settings leaked into the menu");
                if (!GameObject.Find("Practice Menu")) throw new Exception("Did not return to practice submenu");
                report.submenuReturn = true;
                if (report.visits < 3) Next(0);
                else { BeginOnlineBootstrap(3); Next(12); }
            }
            else if (stage == 7 && elapsed > 1 && physics.ReplayLoadMode)
            {
                var slots = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                    .Where(b => b.name.StartsWith("Replay Slot ")).ToArray();
                if (slots.Length != 20) throw new Exception("Expected 20 replay slots");
                savedSlots = Enumerable.Range(0, 20).Where(i => File.Exists(Path.Combine(physics.ReplaySlotDirectory, $"slot{i + 1:00}.bin"))).ToArray();
                report.replayBrowser = true;
                if (savedSlots.Length == 0) { Next(4); return; }
                replaySlot = 0;
                physics.SelectReplaySlot(savedSlots[replaySlot]); Next(8);
            }
            else if (stage == 8 && elapsed > .5)
            {
                if (!physics.IsPracticeReplay || physics.ReplayFrameIndex == 0) throw new Exception("Replay did not advance");
                if (!report.replayPanelDrag)
                {
                    VerifyReplayPanelDrag();
                    report.replayPanelDrag = true;
                }
                report.replaySlotsPlayed++;
                ScreenCapture.CaptureScreenshot("Logs/practice-replay.png");
                replaySlot++;
                if (replaySlot >= savedSlots.Length) Next(4);
                else { physics.SelectReplaySlot(savedSlots[replaySlot]); Next(8); }
            }
            else if (stage == 12 && elapsed > 1 && SceneManager.GetActiveScene().name == "TableSet07")
            {
                if (bootstrap.Phase == MatchStartPhase.Aborted) throw new Exception("Online bootstrap aborted: " + bootstrap.AbortReason);
                if (!bootstrap.IsSceneReady(bootstrap.LocalActor)) return;
                physics = UnityEngine.Object.FindAnyObjectByType<PhysicsMng>();
                if (!physics || !physics.GetComponent<PracticeMatchAdapter>() ||
                    PoolCoach.Instance.Configuration != bootstrap.Configuration || physics.inMove ||
                    PoolCoach.Instance.isMatchTimePlay || ShotCtrl.canControl || physics.IsLocalPractice)
                    throw new Exception("Online scene did not initialize in a locked state");
                if (bootstrap.Phase != MatchStartPhase.Loading) throw new Exception("Scene started before both peers were ready");
                var before = physics.ballcs[0].body.position;
                physics.StartBallScatter();
                physics.StartShot(new Impulse(before, Vector3.right, Vector3.zero)).Forget();
                if (physics.inMove || physics.ballcs[0].body.position != before)
                    throw new Exception("Unapproved shot or scatter changed online state");
                if (!bootstrap.MarkSceneReady(bootstrap.ActorForSeat(1 - bootstrap.Configuration.LocalSeat), bootstrap.MatchId, bootstrap.ConfigurationKey) ||
                    bootstrap.Phase != MatchStartPhase.Ready ||
                    !bootstrap.CommitStart(11, bootstrap.MatchId)) throw new Exception("Both ready peers could not approve start");
                if (ShotCtrl.canControl) throw new Exception("Bootstrap unlocked unfinished shot transport");
                report.onlineBootstrapChecks++;
                onlineShot = new PracticeOnlineShotValidation(physics, bootstrap);
                Next(16);
            }
            else if (stage == 16 && onlineShot.Tick())
            {
                report.onlinePhysicsChecks++; report.onlineReplayChecks++;
                onlineShot = null;
                PracticeSceneFlow.ReturnToMenu(); Next(13);
            }
            else if (stage == 13 && elapsed > 1 && SceneManager.GetActiveScene().name == "Consist")
            {
                if (PracticeSceneFlow.OnlineSession != null ||
                    PracticeSceneFlow.Configuration.Execution != MatchExecutionMode.LocalPractice ||
                    bootstrap.Phase != MatchStartPhase.Aborted) throw new Exception("Online session leaked after exit");
                if (report.onlineBootstrapChecks == 1) { BeginOnlineBootstrap(4); Next(12); }
                else { PracticeSceneFlow.Enter(PracticeSceneFlow.Mode.ThreeBall); Next(14); }
            }
            else if (stage == 14 && elapsed > 1 && SceneManager.GetActiveScene().name == "TableSet07")
            {
                physics = UnityEngine.Object.FindAnyObjectByType<PhysicsMng>();
                if (!physics || physics.inMove || !PoolCoach.Instance.isMatchTimePlay) return;
                if (!physics.IsLocalPractice || !PoolCoach.Instance.isRulePractice || !ShotCtrl.canControl || physics.ballcs.Length != 3)
                    throw new Exception("Practice controls were not restored after online exit");
                report.practiceAfterOnline = true;
                PracticeSceneFlow.ReturnToMenu(); Next(15);
            }
            else if (stage == 15 && elapsed > 1 && SceneManager.GetActiveScene().name == "Consist")
            {
                if (!Mathf.Approximately(Time.fixedDeltaTime, fixedStep) || Physics.simulationMode != simulation)
                    throw new Exception("Physics settings leaked after online/practice transitions");
                Save(); EditorApplication.isPlaying = false;
            }
        }
        catch (Exception e) { report.errors.Add(e.ToString()); Save(); EditorApplication.isPlaying = false; }
    }
    static void Next(int value) { stage = value; changed = EditorApplication.timeSinceStartup; Save(); }
    static void BeginOnlineBootstrap(int ballCount)
    {
        MatchShotProtocolValidation.Run(ballCount);
        MatchResultProtocolValidation.Run(ballCount);
        MatchRecoveryValidation.Run(ballCount);
        PracticeMatchRouteValidation.Run(); report.testRouteChecks++;
        report.recoveryChecks++;
        report.resultProtocolChecks++; report.rematchChecks++;
        report.shotProtocolChecks++;
        var hall = new Assets.Scripts.Prack.BackSys.ChartData.SkillMatchData { HallIdx = 1,
            MatchBall = ballCount, TargetHit = 11, MatchCushion = ballCount == 3 ? 3 : 0, MatchTotMin = 17 };
        var config = MatchConfiguration.Online(hall, "Authority", "Guest", 0, ballCount == 3 ? 0 : 1, 35);
        MatchStartSession Make() => new MatchStartSession(Guid.NewGuid().ToString("N"), "verified-config", config, 11, 22, Time.realtimeSinceStartupAsDouble);
        void Require(bool value, string error) { if (!value) throw new Exception(error); }
        var probe = Make();
        Require(!probe.MarkSceneReady(11, probe.MatchId, probe.ConfigurationKey), "Ready accepted before configuration agreement");
        Require(!probe.AcceptConfiguration(99, probe.MatchId, probe.ConfigurationKey), "Foreign actor accepted");
        Require(!probe.AcceptConfiguration(11, "old-match", probe.ConfigurationKey), "Old match accepted");
        Require(!probe.AcceptConfiguration(11, probe.MatchId, "different-config"), "Wrong rules accepted");
        probe.AcceptConfiguration(11, probe.MatchId, probe.ConfigurationKey);
        probe.AcceptConfiguration(11, probe.MatchId, probe.ConfigurationKey);
        Require(probe.Phase == MatchStartPhase.Configuring, "One peer counted twice");
        probe.AcceptConfiguration(22, probe.MatchId, probe.ConfigurationKey);
        probe.MarkSceneReady(11, probe.MatchId, probe.ConfigurationKey);
        Require(!probe.CommitStart(11, probe.MatchId), "One loaded peer started the match");
        probe.MarkSceneReady(22, probe.MatchId, probe.ConfigurationKey);
        Require(!probe.CommitStart(22, probe.MatchId) && !probe.CommitStart(11, "old-match"), "Unauthorized start accepted");
        Require(probe.CommitStart(11, probe.MatchId) && !probe.CommitStart(11, probe.MatchId), "Start was not applied exactly once");
        var expired = Make(); expired.Tick(Time.realtimeSinceStartupAsDouble + 31);
        Require(expired.Phase == MatchStartPhase.Aborted && !expired.AcceptConfiguration(11, expired.MatchId, expired.ConfigurationKey), "Expired session revived");
        var left = Make(); left.ParticipantLeft(99);
        Require(left.Phase == MatchStartPhase.Configuring, "Unrelated departure aborted match");
        left.ParticipantLeft(22); Require(left.Phase == MatchStartPhase.Aborted, "Participant departure did not abort");
        bootstrap = Make();
        Require(!PracticeSceneFlow.EnterOnline(bootstrap), "Scene loaded before settings were agreed");
        bootstrap.AcceptConfiguration(11, bootstrap.MatchId, bootstrap.ConfigurationKey);
        bootstrap.AcceptConfiguration(22, bootstrap.MatchId, bootstrap.ConfigurationKey);
        Require(PracticeSceneFlow.EnterOnline(bootstrap), "Agreed online setup could not load the scene");
    }
    static void VerifyMatchConfiguration()
    {
        void Require(bool value, string message) { if (!value) throw new Exception(message); }
        var coach = PoolCoach.Instance;
        var previous = coach.Configuration;
        var hall = new Assets.Scripts.Prack.BackSys.ChartData.SkillMatchData {
            HallIdx = 91, MatchBall = physics.ballcs.Length, TargetHit = 11,
            MatchCushion = physics.ballcs.Length == 3 ? 3 : 0, MatchTotMin = 17
        };
        var config = MatchConfiguration.Online(hall, "Seat A", "Seat B", 1, 1, 35);
        string compatibility = MatchmakingCompatibility.RulesKey(hall);
        hall.TargetHit = 99;
        Require(config.TargetScore == 11, "Chart mutation changed agreed settings");
        Require(compatibility != MatchmakingCompatibility.RulesKey(hall), "Different rules share a matching key");
        hall.TargetHit = 11;
        Require(compatibility == MatchmakingCompatibility.RulesKey(hall), "Identical rooms have incompatible matching keys");
        hall.HallIdx++;
        Require(compatibility != MatchmakingCompatibility.RulesKey(hall), "Different halls share a matching key");
        hall.HallIdx--;
        Require(MatchmakingCompatibility.GameVersion("1.0") != "1.0", "Old clients were not isolated");
        report.matchmakingCompatibilityChecks++;
        try
        {
            coach.MatchReset(config);
            Require(coach.targetHit == 11 && coach.timeMatchSec == 1020 && coach.timeInningSec == 35,
                "Online settings were overwritten by practice defaults");
            Require(PoolPlayer.players[0].name == "Seat A" && PoolPlayer.players[1].name == "Seat B" && PoolPlayer.turnId == 1,
                "Online roster or first seat was overwritten");
            Require(!coach.isRulePractice && !ShotCtrl.canControl && !physics.IsLocalPractice &&
                !physics.CanPlaceBalls && !physics.CanUndoShot && !physics.CanReplayShot,
                "Practice controls leaked into online initialization");
            Require(coach.playType == Assets.TutorialInfo.Scripts.TableSet06.Often.PlayType.OnLine,
                "Online configuration retained Single mode");
        }
        finally
        {
            coach.MatchReset(previous);
            coach.RestorePracticeTurn(0, previous.TurnSeconds);
        }
        Require(coach.isRulePractice && physics.IsLocalPractice && coach.targetHit == 7 && PoolPlayer.turnId == 0,
            "Returning to practice retained online state");
        bool rejected = false;
        hall.MatchBall = 5;
        try { MatchConfiguration.Online(hall, "A", "B", 0, 0, 20); }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        Require(rejected, "Unsupported ball count was accepted");
        hall.MatchBall = physics.ballcs.Length; hall.FinishMission = 1; rejected = false;
        try { MatchConfiguration.Online(hall, "A", "B", 0, 0, 20); }
        catch (NotSupportedException) { rejected = true; }
        Require(rejected, "Unsupported finish mission was silently changed");
        var outcomes = new[] { FourBallShotOutcome.Foul, FourBallShotOutcome.Miss,
            FourBallShotOutcome.Miss, FourBallShotOutcome.Point, FourBallShotOutcome.Foul,
            FourBallShotOutcome.Foul, FourBallShotOutcome.Foul, FourBallShotOutcome.Foul };
        for (int mask = 0; mask < 8; mask++)
        {
            bool first = (mask & 1) != 0, second = (mask & 2) != 0, opponent = (mask & 4) != 0;
            Require(FourBallRules.Resolve(first, second, opponent) == outcomes[mask], "Four-ball final judgement mismatch");
        }
    }

    static void VerifyNetworkCleanup()
    {
        var listeners = UnityEngine.Object.FindObjectsByType<Assets.Scripts.Sight.Surface.Consist.RoomSwipe>(FindObjectsInactive.Include);
        if (listeners.Length == 0) throw new Exception("Expected room-list cleanup callbacks in the menu scene");
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        for (int pass = 0; pass < 2; pass++)
        {
            var engine = NetworkManager.network;
            // Reproduce the shutdown order from the warning: network dies before its UI subscribers.
            UnityEngine.Object.DestroyImmediate(engine.gameObject);
            foreach (var listener in listeners)
                listener.GetType().GetMethod("OnDisable", flags).Invoke(listener, null);
            if (NetworkManager.TryGetExistingNetwork(out _) ||
                UnityEngine.Object.FindAnyObjectByType<Assets.Scripts.Exert.Network.NetworkEngine>())
                throw new Exception("Unsubscribing after network destruction recreated the Network object");
            // Normal use must still be able to initialize networking in a later scene/session.
            var replacement = NetworkManager.network;
            if (!replacement || ReferenceEquals(replacement, engine)) throw new Exception("Network could not initialize again after cleanup");
            foreach (var listener in listeners.Where(listener => listener.isActiveAndEnabled))
                listener.GetType().GetMethod("OnEnable", flags).Invoke(listener, null);
        }
    }
    static void VerifyReplayPanelDrag(bool savePanel = false)
    {
        void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        var header = UnityEngine.Object.FindAnyObjectByType<Assets.TutorialInfo.Scripts.TableSet06.Sight.Surface.ReplayPanelDrag>();
        Require(header && (savePanel || (physics.IsPracticeReplay && Time.timeScale == 0)), "Expected the drag header during paused-time replay");
        var grip = (RectTransform)header.transform;
        Require(grip.rect.width < 30 && grip.rect.height > 180 && grip.anchoredPosition.x < -330, "Replay grip must be vertical on the left edge");
        var panel = (RectTransform)header.transform.parent;
        var canvas = (RectTransform)panel.parent;
        var children = panel.Cast<RectTransform>().ToArray();
        var offsets = children.Select(child => child.anchoredPosition).ToArray();
        var start = panel.anchoredPosition;
        var screen = RectTransformUtility.WorldToScreenPoint(null, header.transform.position);
        var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
        { position = screen, pointerId = -1, button = UnityEngine.EventSystems.PointerEventData.InputButton.Left };
        var hits = new List<UnityEngine.EventSystems.RaycastResult>();
        UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer, hits);
        Require(hits.Count > 0 && hits[0].gameObject == header.gameObject, "Replay header must receive pointer input: " + string.Join(", ", hits.Select(hit => hit.gameObject.name)));
        pointer.pointerPressRaycast = hits[0];
        UnityEngine.EventSystems.ExecuteEvents.Execute(header.gameObject, pointer, UnityEngine.EventSystems.ExecuteEvents.beginDragHandler);
        void Drag(Vector2 position)
        {
            pointer.position = position;
            UnityEngine.EventSystems.ExecuteEvents.Execute(header.gameObject, pointer, UnityEngine.EventSystems.ExecuteEvents.dragHandler);
            var corners = new Vector3[4]; panel.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                Vector2 local = canvas.InverseTransformPoint(corner);
                Require(local.x >= canvas.rect.xMin - .1f && local.x <= canvas.rect.xMax + .1f &&
                    local.y >= canvas.rect.yMin - .1f && local.y <= canvas.rect.yMax + .1f, "Dragged replay controls left the viewport");
            }
        }
        Drag(screen + new Vector2(-300, 250));
        Require(Vector2.Distance(panel.anchoredPosition, start) > 50, "Replay panel did not move");
        Drag(new Vector2(100000, 100000));
        Drag(new Vector2(-100000, -100000));
        Drag(screen + new Vector2(-300, 250));
        UnityEngine.EventSystems.ExecuteEvents.Execute(header.gameObject, pointer, UnityEngine.EventSystems.ExecuteEvents.endDragHandler);
        for (int i = 0; i < children.Length; i++)
            Require(children[i].anchoredPosition == offsets[i], "Dragging changed a child control's layout");
        Require(children.Count(child => child.name.StartsWith("Replay Slot ")) == 20,
            "All twenty replay slots must move with the panel");
        var slot = panel.Find("Replay Slot 01").gameObject;
        Require(UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IDragHandler>(slot) == null,
            "A slot button must not initiate panel dragging");
        var title = panel.Find("Replay Title").GetComponent<TMPro.TMP_InputField>();
        Require(title.interactable && UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IDragHandler>(title.gameObject) == title.gameObject,
            "Title editing must retain its own drag input");
        if (savePanel) { panel.anchoredPosition = start; report.savePanelDragChecks++; return; }
        var next = panel.Find("Replay Next").GetComponent<Button>();
        int before = physics.ReplayFrameIndex;
        next.onClick.Invoke();
        Require(physics.ReplayFrameIndex == Mathf.Min(before + 1, physics.ReplayFrameCount - 1), "Moved replay button did not work");
        var reset = panel.Find("Replay Reset").GetComponent<Button>(); reset.onClick.Invoke();
        Require(physics.ReplayFrameIndex == 0, "Moved replay reset did not work");
        panel.Find("Replay Play").GetComponent<Button>().onClick.Invoke();
    }
    static void VerifyFinishPopup()
    {
        var panel = UnityEngine.Object.FindAnyObjectByType<Assets.TutorialInfo.Scripts.TableSet06.Sight.Surface.PnlMatch>();
        var popup = panel.transform.Find("Pops/MatchFinish");
        if (!popup.gameObject.activeInHierarchy || ((RectTransform)popup).anchoredPosition != Vector2.zero ||
            popup.Find("Result").GetComponent<TMPro.TMP_Text>().text != "승리하였습니다")
            throw new Exception("Expected the centered practice victory popup");
    }
    static void VerifyReplayStroke()
    {
        void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        var shot = UnityEngine.Object.FindAnyObjectByType<ShotCtrl>();
        var point = UnityEngine.Object.FindAnyObjectByType<Assets.TutorialInfo.Scripts.TableSet06.Sight.Surface.BallPointBig>(FindObjectsInactive.Include);
        string Label(string name) => shot.transform.Find("Gauge Values/" + name + " Value/Label").GetComponent<TMPro.TMP_Text>().text;
        // Change the next-shot controls before saving: the recording must retain the original stroke.
        typeof(ShotCtrl).GetProperty("force").SetValue(shot, .11f);
        shot.SetFollowThrough(.12f);
        point.RestorePracticePoint(new Vector2(-32, 48));
        var powerHandle = shot.forceHandle.localPosition;
        var followHandle = shot.pullBarHandle.localPosition;
        var gaugeSize = shot.forceGage.size;
        var cueReturnPosition = shot.cueVertical.position;
        var cueReturnRotation = shot.cueVertical.rotation;
        var cueReturnSlide = shot.cueSlider.localPosition;
        const int slot = PhysicsMng.ReplaySlotCount - 1;
        var path = Path.Combine(physics.ReplaySlotDirectory, $"slot{slot + 1:00}.bin");
        byte[] original = File.Exists(path) ? File.ReadAllBytes(path) : null;
        try
        {
            Require(physics.SaveReplaySlot(slot), "Could not save stroke metadata");
            var bytes = File.ReadAllBytes(path);
            Require(BitConverter.ToInt32(bytes, 4) == 3 && bytes[24] == 1 &&
                Mathf.Approximately(BitConverter.ToSingle(bytes, 25), .63f) &&
                Mathf.Approximately(BitConverter.ToSingle(bytes, 29), .77f), "Saved settings came from the wrong shot");
            int count = BitConverter.ToInt32(bytes, 20);
            int ballFrameBytes = 4 + 52 * physics.ballcs.Length;
            int frameBytes = ballFrameBytes + 54;
            Vector3 VectorAt(int offset) => new Vector3(BitConverter.ToSingle(bytes, offset), BitConverter.ToSingle(bytes, offset + 4), BitConverter.ToSingle(bytes, offset + 8));
            Require(Mathf.Abs(VectorAt(57 + ballFrameBytes + 42).z + .2f) < .0001f,
                "Replay must start at the actual forward stroke, not an abandoned attempt");
            int firstMovingFrame = -1;
            for (int f = 0; f < count; f++)
                if (VectorAt(57 + f * frameBytes + 4 + BitConverter.ToInt32(bytes, 12) * 52 + 28).sqrMagnitude > .00001f)
                { firstMovingFrame = f; break; }
            Require(firstMovingFrame >= 0 && firstMovingFrame <= 129,
                "Replay exceeded the bounded pre-impact stroke buffer");
            float minSlide = 0, maxSlide = 0;
            for (int f = 0; f < count; f++)
            {
                float slide = VectorAt(57 + f * frameBytes + ballFrameBytes + 42).z;
                minSlide = Mathf.Min(minSlide, slide); maxSlide = Mathf.Max(maxSlide, slide);
            }
            Require(minSlide < -.1f && maxSlide > .02f, "Replay did not record pullback and follow-through motion");
            void CheckCue(int frame)
            {
                int offset = 57 + frame * frameBytes + ballFrameBytes;
                Require(bytes[offset] == 1 && shot.cueVertical.gameObject.activeSelf == (bytes[offset + 1] != 0), "Cue visibility was not replayed");
                Require(Vector3.Distance(shot.cueVertical.position, VectorAt(offset + 2)) < .0001f &&
                    Vector3.Distance(shot.cueSlider.localPosition, VectorAt(offset + 42)) < .0001f,
                    "Cue pose did not follow the selected replay frame");
            }
            physics.OpenReplaySlots();
            Require(physics.LoadReplaySlot(slot), "Could not read the new replay format");
            void CheckStroke()
            {
                Require(Label("Power") == "파워 0.63" && Label("Follow") == "팔로 0.77" &&
                    point.CapturePracticePoint() == new Vector2(84, -116), "Replay controls did not show the recorded stroke");
                Require(Mathf.Approximately(shot.force, .11f) && Mathf.Approximately(shot.pull, .12f) && !ShotCtrl.canControl,
                    "Replay presentation changed live shot settings or enabled shooting");
            }
            CheckStroke(); CheckCue(0);
            physics.NextPracticeReplayFrame(); CheckStroke(); CheckCue(1);
            physics.ResetPracticeReplay(); CheckStroke(); CheckCue(0);
            // Persist a known half-ball aim, then verify the UI uses the saved start pose.
            var overlapFixture = (byte[])bytes.Clone();
            float radius = physics.ballcs[0].GetComponent<SphereCollider>().radius * Mathf.Abs(physics.ballcs[0].transform.lossyScale.x);
            void SetPosition(int ball, Vector3 position)
            {
                int offset = 57 + 4 + ball * 52;
                Array.Copy(BitConverter.GetBytes(position.x), 0, overlapFixture, offset, 4);
                Array.Copy(BitConverter.GetBytes(position.y), 0, overlapFixture, offset + 4, 4);
                Array.Copy(BitConverter.GetBytes(position.z), 0, overlapFixture, offset + 8, 4);
            }
            int recordedCue = BitConverter.ToInt32(bytes, 12);
            for (int ball = 0; ball < physics.ballcs.Length; ball++) SetPosition(ball, new Vector3(-1, beforeShot.y, -1));
            SetPosition(recordedCue, new Vector3(0, beforeShot.y, 0));
            SetPosition(2, new Vector3(.5f, beforeShot.y, -radius));
            File.WriteAllBytes(path, overlapFixture);
            Require(physics.LoadReplaySlot(slot), "Cannot load saved overlap fixture");
            var overlap = shot.transform.Find("Gauge Values/Aim Overlap Preview");
            Require(overlap, "Missing replay overlap panel");
            void CheckOverlap()
            {
                typeof(ShotCtrl).GetMethod("UpdateOverlapPreview", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(shot, null);
                Require(overlap.gameObject.activeSelf && overlap.Find("Thickness").GetComponent<TMPro.TMP_Text>().text == "예상 두께 50%",
                    "Replay did not retain the recorded half-ball aim");
                Require(Mathf.Abs(((RectTransform)overlap.Find("Cue Ball")).anchoredPosition.x + 24) < .01f,
                    "Replay overlap discs do not match recorded thickness");
            }
            CheckOverlap(); physics.NextPracticeReplayFrame(); CheckOverlap(); physics.ResetPracticeReplay(); CheckOverlap();
            report.replayOverlapChecks++;
            var invalid = (byte[])bytes.Clone();
            Array.Copy(BitConverter.GetBytes(float.NaN), 0, invalid, 25, 4);
            File.WriteAllBytes(path, invalid);
            Require(!physics.LoadReplaySlot(slot), "Non-finite stroke metadata must be rejected");
            CheckStroke();
            var versionTwo = new byte[57 + count * ballFrameBytes];
            Array.Copy(bytes, versionTwo, 57);
            Array.Copy(BitConverter.GetBytes(2), 0, versionTwo, 4, 4);
            for (int f = 0; f < count; f++)
                Array.Copy(bytes, 57 + f * frameBytes, versionTwo, 57 + f * ballFrameBytes, ballFrameBytes);
            File.WriteAllBytes(path, versionTwo);
            Require(physics.LoadReplaySlot(slot) && !shot.cueVertical.gameObject.activeSelf, "Version 2 must play without invented cue motion");
            CheckStroke();
            // Version 1 contains the same ball frames without the stroke header.
            var legacy = new byte[versionTwo.Length - 33];
            Array.Copy(bytes, 0, legacy, 0, 24);
            Array.Copy(BitConverter.GetBytes(1), 0, legacy, 4, 4);
            Array.Copy(versionTwo, 57, legacy, 24, versionTwo.Length - 57);
            File.WriteAllBytes(path, legacy);
            Require(physics.LoadReplaySlot(slot) && Label("Power") == "파워 —" && Label("Follow") == "팔로 —" &&
                point.CapturePracticePoint() == Vector2.zero, "Legacy replay must not show stale stroke settings");
            Require(!overlap.gameObject.activeSelf, "Legacy replay retained another recording's overlap UI");
            physics.ExitPracticeReplay();
            Require(!overlap.gameObject.activeSelf, "Closing replay retained replay overlap UI");
            Require(point.CapturePracticePoint() == new Vector2(-32, 48) && shot.forceHandle.localPosition == powerHandle &&
                shot.pullBarHandle.localPosition == followHandle && shot.forceGage.size == gaugeSize,
                "Closing replay must restore the original control presentation");
            Require(Vector3.Distance(shot.cueVertical.position, cueReturnPosition) < .0001f &&
                Quaternion.Angle(shot.cueVertical.rotation, cueReturnRotation) < .01f && shot.cueSlider.localPosition == cueReturnSlide,
                "Closing replay must restore the live cue pose");
            VerifyReplayFolders();
            physics.ResetPracticeReplay(); CheckStroke();
        }
        finally
        {
            physics.ExitPracticeReplay();
            if (original != null) File.WriteAllBytes(path, original);
            else if (File.Exists(path)) File.Delete(path);
        }
    }
    static void VerifyReplayIme(TMPro.TMP_InputField field)
    {
        var module = UnityEngine.EventSystems.EventSystem.current.currentInputModule;
        var previous = module.inputOverride;
        var obj = new GameObject("Replay IME validation");
        var input = obj.AddComponent<ReplayValidationInput>();
        string original = field.text;
        try
        {
            field.SetTextWithoutNotify("");
            field.caretPosition = 0;
            module.inputOverride = input;
            input.Composition = "한";
            field.ForceLabelUpdate();
            string shown = field.textComponent.text.Replace("\u200B", "");
            if (shown != "한" || field.text != "")
                throw new Exception("IME composition leaked markup into the replay input: " + shown);
            input.Composition = "";
            field.SetTextWithoutNotify("한글 제목");
            field.ForceLabelUpdate();
            if (field.textComponent.text.Replace("\u200B", "") != "한글 제목")
                throw new Exception("Committed Korean replay text was not rendered correctly");
            report.replayImeChecks++;
        }
        finally
        {
            input.Composition = ""; module.inputOverride = previous;
            field.SetTextWithoutNotify(original);
            UnityEngine.Object.DestroyImmediate(obj);
        }
    }
    static void VerifyReplayFolders()
    {
        void Require(bool value, string message) { if (!value) throw new Exception(message); }
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        void Call(string method) => typeof(PhysicsMng).GetMethod(method, flags).Invoke(physics, null);
        string originalDirectory = physics.ReplaySlotDirectory;
        string first = null, second = null;
        int frames = physics.ReplayFrameCount;
        try
        {
            string name = "검증 " + Guid.NewGuid().ToString("N").Substring(0, 8);
            Require(!physics.DeleteReplayFolder("") && !physics.DeleteReplayFolder("../") &&
                !physics.CreateReplayFolder("  "), "Unsafe or blank folders accepted");
            Call("OpenReplaySavePanel");
            Call("UpdateReplayFolders");
            var panel = (RectTransform)typeof(PhysicsMng).GetField("replayPanel", flags).GetValue(physics);
            panel.Find("Replay Folder Add").GetComponent<Button>().onClick.Invoke();
            var dialog = panel.Find("Replay Folder Dialog");
            Require(dialog.gameObject.activeSelf, "Folder add dialog did not open");
            VerifyReplayIme(dialog.Find("Replay Folder Name").GetComponent<TMPro.TMP_InputField>());
            VerifyReplayIme(panel.Find("Replay Title").GetComponent<TMPro.TMP_InputField>());
            dialog.Find("Replay Folder Name").GetComponent<TMPro.TMP_InputField>().SetTextWithoutNotify(name);
            dialog.Find("Replay Folder Confirm").GetComponent<Button>().onClick.Invoke();
            Require(!dialog.gameObject.activeSelf && physics.SelectedReplayFolder != "", "Folder creation UI failed");
            panel.Find("Replay Folder Delete").GetComponent<Button>().onClick.Invoke();
            Require(dialog.gameObject.activeSelf, "Folder delete must ask for confirmation");
            dialog.Find("Replay Folder Cancel").GetComponent<Button>().onClick.Invoke();
            Require(physics.GetReplayFolders().Exists(f => f.Id == physics.SelectedReplayFolder), "Cancelling deleted a folder");
            first = physics.SelectedReplayFolder;
            string firstPath = physics.ReplaySlotDirectory;
            Require(!physics.CreateReplayFolder(name) && physics.GetReplayFolders().Exists(f => f.Id == first && f.Name == name),
                "Folder names must persist and duplicates must be rejected");
            Require(physics.ReplayFrameCount == frames && !Directory.GetFiles(firstPath, "slot*.bin").Any(),
                "Changing save folder lost current shot or seeded a new folder");
            Require(physics.SaveReplaySlot(0), "Cannot save current cue/ball recording in new folder");
            Call("UpdateReplayTitle");
            var title = (TMPro.TMP_InputField)typeof(PhysicsMng).GetField("replayTitle", flags).GetValue(physics);
            title.SetTextWithoutNotify("첫 폴더 제목");
            Require(physics.CreateReplayFolder(name + " 두번째"), "Second folder creation failed");
            second = physics.SelectedReplayFolder;
            Require(File.ReadAllText(Path.Combine(firstPath, "slot01.title.txt")) == "첫 폴더 제목" &&
                physics.GetReplaySlotTitle(0) == "", "Title edit leaked into the next folder");
            Require(physics.SaveReplaySlot(0) && physics.SetReplaySlotTitle(0, "두번째 제목"), "Second folder save failed");
            Require(physics.SelectReplayFolder(first) && physics.GetReplaySlotTitle(0) == "첫 폴더 제목", "Folder slots are not independent");
            physics.OpenReplaySlots();
            Require(physics.LoadReplaySlot(0) && physics.IsPracticeReplay, "Folder recording cannot play");
            Require(physics.SelectReplayFolder(second) && !physics.IsPracticeReplay && physics.ReplayFrameCount == 0 && Time.timeScale > 0,
                "Folder switch did not clear old playback and restore time");
            Require(physics.LoadReplaySlot(0) && physics.DeleteReplayFolder(second) && physics.SelectedReplayFolder == "" &&
                physics.ReplaySlotDirectory == originalDirectory && !physics.IsPracticeReplay, "Deleting selected playing folder failed");
            second = null;
            physics.ExitPracticeReplay();
            Require(physics.ReplayFrameCount == frames, "Folder browsing lost the unsaved live recording");
            Require(physics.DeleteReplayFolder(first) && !Directory.Exists(firstPath), "Folder contents were not removed");
            first = null;
            report.replayFolderChecks++;
        }
        finally
        {
            physics.ExitPracticeReplay();
            physics.SelectReplayFolder("");
            if (first != null) physics.DeleteReplayFolder(first);
            if (second != null) physics.DeleteReplayFolder(second);
        }
    }
    static void VerifyPracticeMatch()
    {
        var coach = PoolCoach.Instance;
        var panel = UnityEngine.Object.FindAnyObjectByType<Assets.TutorialInfo.Scripts.TableSet06.Sight.Surface.PnlMatch>();
        var shot = physics.GetComponent<ShotCtrl>();
        void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        string Text(int player, string name) => panel.transform.Find("PlayerBoxs/" +
            (player == 0 ? "PlayerSelf/" : "PlayerOther/") + name).GetComponent<TMPro.TMP_Text>().text;
        void CheckDisplay(int player)
        {
            var data = PoolPlayer.players[player];
            Require(Text(player, "TxtName") == data.name, "Practice nickname mismatch");
            Require(Text(player, "HitCountBox/HitCnt/Txt") == data.hitCnt.ToString(), "Practice score display mismatch");
            Require(Text(player, "TxtCoin") == Assets.TutorialInfo.Scripts.TableSet06.Often.Utility.CoinNumToStr(data.coin), "Wallet display mismatch");
            Require(Text(player, "TxtMatchCoin") == Assets.TutorialInfo.Scripts.TableSet06.Often.Utility.CoinNumToStr(data.matchCoin), "Inning coin display mismatch");
        }
        void FinishShot() => typeof(PoolCoach).GetMethod("PhysicsManager_OnBallAllStop",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(coach, new object[] { "" });
        void Score()
        {
            int player = PoolPlayer.turnId;
            var cue = physics.ballcs[player];
            int before = PoolPlayer.currentPlayer.hitCnt;
            var eggBox = panel.transform.Find("PlayerBoxs/" + (player == 0 ? "PlayerSelf/" : "PlayerOther/") + "HitCountBox/EggBox");
            var egg = (RectTransform)eggBox.GetChild(coach.targetHit - before - 1);
            Vector2 eggStart = egg.anchoredPosition;
            if (physics.ballcs.Length == 3)
            {
                PoolLogic.Instance.OnBallHitBoard(cue, Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.CushionDir.T);
                PoolLogic.Instance.OnBallHitBoard(cue, Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.CushionDir.R);
                PoolLogic.Instance.OnBallHitBoard(cue, Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.CushionDir.B);
            }
            PoolLogic.Instance.OnCueBallHitBall(cue, physics.ballcs[physics.ballcs.Length == 3 ? 1 - player : 3]);
            PoolLogic.Instance.OnCueBallHitBall(cue, physics.ballcs[2]);
            PoolLogic.Instance.OnCueBallHitBall(cue, physics.ballcs[2]);
            Require(PoolPlayer.currentPlayer.hitCnt == before + (physics.ballcs.Length == 4 ? 0 : 1),
                "Four-ball points must wait for rest; three-ball points must remain immediate");
            CheckDisplay(player);
            FinishShot();
            Require(PoolPlayer.turnId == player && PoolPlayer.currentPlayer.hitCnt == before + 1, "Scoring must retain turn without double counting");
            CheckDisplay(player);
            var tweens = DOTween.TweensByTarget(egg, false);
            Require(tweens != null && tweens.Count == 1 && Mathf.Approximately(tweens[0].Duration(), .5f) &&
                egg.anchoredPosition == eggStart, "Scored egg must animate for 0.5 seconds instead of jumping");
            var tween = tweens[0];
            Vector2 destination = new Vector2(player == 0 ? 230 - before * 15 : before * 15, 0);
            tween.Goto(.25f, true);
            Require(Vector2.Distance(egg.anchoredPosition, Vector2.Lerp(eggStart, destination, .5f)) < .01f,
                "Scored egg must visibly pass through its midpoint");
            Vector2 midpoint = egg.anchoredPosition;
            typeof(Assets.TutorialInfo.Scripts.TableSet06.Sight.Surface.PnlMatch).GetMethod("RefreshPracticeScoreboard",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(panel, null);
            Require(egg.anchoredPosition == midpoint && DOTween.TweensByTarget(egg, false).Single() == tween,
                "Repeated score refresh must not snap or restart the moving egg");
            tween.Goto(.5f);
            Require(Vector2.Distance(egg.anchoredPosition, destination) < .01f, "Scored egg ended at the wrong position");
            report.eggAnimationChecks++;
        }
        Require(coach.targetHit == 7 && PoolPlayer.turnId == 0, "Expected Player 1 and target 7");
        Require(Assets.TutorialInfo.Scripts.TableSet06.Often.Utility.CoinNumToStr(0) == "0", "Zero coins must be visible");
        for (int i = 0; i < 2; i++)
        {
            Require(PoolPlayer.players[i].name == $"Player {i + 1}" && PoolPlayer.players[i].coin == 0 &&
                PoolPlayer.players[i].hitCnt == 0 && PoolPlayer.players[i].matchCoin == 0, "Practice initial values are stale");
            CheckDisplay(i);
        }
        Score();
        // Existing achievement/item effects may add a bonus on top of the base reward.
        Require(PoolPlayer.mainPlayer.matchCoin >= coach.hitReward, "Score reward missing");
        coach.Update(coach.maxPlayTime);
        Require(PoolPlayer.turnId == 1 && shot.cueBall.id == 1 && coach.playTime == 0 && coach.calculateTime,
            "Player 1 timeout must start Player 2 with yellow cue and a fresh clock");
        string expectedTurnClock = physics.ballcs.Length == 4 ? "01:00" : "00:20";
        Require(panel.transform.Find("TopCenter/TxtTime").GetComponent<TMPro.TMP_Text>().text == expectedTurnClock,
            "Turn clock display did not reset to " + expectedTurnClock);
        Score();
        coach.Update(coach.maxPlayTime);
        Require(PoolPlayer.turnId == 0 && shot.cueBall.id == 0 && PoolPlayer.mainPlayer.matchCoin == 0 &&
            PoolPlayer.mainPlayer.hitCnt == 1, "Player 2 timeout must reset the next inning coins, not the match score");
        CheckDisplay(0); CheckDisplay(1);
        if (physics.ballcs.Length == 4)
        {
            for (int player = 0; player < 2; player++)
            {
                var cue = physics.ballcs[player];
                PoolLogic.Instance.OnCueBallHitBall(cue, physics.ballcs[2]);
                PoolLogic.Instance.OnCueBallHitBall(cue, physics.ballcs[3]);
                Require(PoolPlayer.currentPlayer.hitCnt == 1 && !PoolLogic.gameState.shotAchieve,
                    "Two reds must only create a pending point");
                PoolLogic.Instance.OnCueBallHitBall(cue, physics.ballcs[1 - player]);
                PoolLogic.Instance.OnCueBallHitBall(cue, physics.ballcs[1 - player]);
                Require(PoolLogic.gameState.shotFailed && PoolPlayer.currentPlayer.hitCnt == 1,
                    "Late opponent contact must cancel the pending point until rest");
                FinishShot();
                Require(PoolPlayer.players[player].hitCnt == 0 && PoolPlayer.turnId == 1 - player,
                    "Late/repeated fouls must deduct once and pass the turn");
                CheckDisplay(player);
            }
            Score(); // Give Player 1 a point to deduct on a complete miss.
            FinishShot();
            Require(PoolPlayer.mainPlayer.hitCnt == 0 && PoolPlayer.turnId == 1,
                "No red contact must deduct a point");
            FinishShot();
            Require(PoolPlayer.otherPlayer.hitCnt == 0 && PoolPlayer.turnId == 0,
                "A foul at zero must retain the existing score floor");
            Score();
            PoolLogic.Instance.OnCueBallHitBall(physics.ballcs[0], physics.ballcs[2]);
            PoolLogic.Instance.OnCueBallHitBall(physics.ballcs[0], physics.ballcs[2]);
            FinishShot();
            Require(PoolPlayer.mainPlayer.hitCnt == 1 && PoolPlayer.turnId == 1,
                "Hitting only one red, even repeatedly, is a miss without a deduction");
            Score();
            PoolLogic.Instance.OnCueBallHitBall(physics.ballcs[1], physics.ballcs[3]);
            FinishShot();
        }
        else
        {
            FinishShot(); // A miss passes the turn in both directions.
            Require(PoolPlayer.turnId == 1, "Miss must pass the turn");
            FinishShot();
        }
        Require(PoolPlayer.turnId == 0 && PoolPlayer.mainPlayer.hitCnt == 1 && PoolPlayer.otherPlayer.hitCnt == 1,
            "Miss/foul changed scores or selected the wrong player");
        for (int i = 0; i < 6; i++) Score();
        Require(PoolLogic.gameState.gameIsComplete && PoolPlayer.mainPlayer.isWinner &&
            !coach.calculateTime && !ShotCtrl.canControl && PoolPlayer.mainPlayer.coin == 0 && PoolPlayer.otherPlayer.coin == 0,
            "Seven points must finish the practice match without wallet settlement");
        VerifyFinishPopup();
        coach.Update(coach.maxPlayTime * 2);
        Require(PoolPlayer.turnId == 0, "Completed match must not time out");
        coach.MatchReset();
        coach.RestorePracticeTurn(0, coach.timeInningSec);
        panel.RefreshAfterPracticeUndo(); // Cancel feedback created by synthetic scoring.
        Require(((RectTransform)panel.transform.Find("Pops/MatchFinish")).anchoredPosition.y == 1000,
            "Reset/undo must hide the result and cancel the pending menu return");
        CheckDisplay(0); CheckDisplay(1);
    }
    static void VerifyMotionVisual(BallC ball)
    {
        var visual = ball.transform.Find("Interpolated Ball");
        if (!visual || !visual.GetComponent<MeshRenderer>().enabled || ball.GetComponent<MeshRenderer>().enabled)
            throw new Exception("Expected exactly one visible ball mesh");
        var position = ball.body.position;
        var rotation = ball.body.rotation;
        var velocity = ball.body.linearVelocity;
        ball.UpdateMotionVisual(true, 0);
        var start = ball.VisualPosition;
        ball.UpdateMotionVisual(true, 1);
        var end = ball.VisualPosition;
        ball.UpdateMotionVisual(true, .5f);
        if (Vector3.Distance(end, position) > .00001f ||
            Vector3.Distance(ball.VisualPosition, (start + end) * .5f) > .00001f)
            throw new Exception("Render interpolation does not bracket the physics step");
        if (ball.body.position != position || ball.body.rotation != rotation || ball.body.linearVelocity != velocity)
            throw new Exception("Rendering changed authoritative physics state");
        ball.UpdateMotionVisual(false, 0);
        if (Vector3.Distance(ball.VisualPosition, position) > .00001f)
            throw new Exception("Stopped/placement/replay visual did not snap to the ball");
    }
    static void Log(string msg, string trace, LogType type)
    {
        if (!SessionState.GetBool(Key, false) || report == null ||
            (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)) return;
        report.errors.Add(msg + "\n" + trace); Save();
    }
    static void Save() { Directory.CreateDirectory("Logs"); File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true)); }
}

// Supplies deterministic Korean composition without changing the user's keyboard or IME.
public sealed class ReplayValidationInput : UnityEngine.EventSystems.BaseInput
{
    public string Composition = "";
    public override string compositionString => Composition;
}

public static class SpinPersistenceValidation
{
    public static void Run()
    {
        void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        for (int p = 0; p <= 100; p++)
        for (int t = 0; t <= 10; t++)
        {
            float power = p / 100f, tip = t / 10f, baseline = .4f + .6f * power * tip;
            float previous = float.PositiveInfinity;
            for (int f = 0; f <= 10; f++)
            {
                float value = Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.FollowThroughProfile.Persistence(power, tip, f / 10f);
                Require(value >= baseline - .000001f && value <= Mathf.Min(1, baseline * 1.2f) + .000001f,
                    "Persistence bonus reduced baseline or exceeded its cap");
                Require(value <= previous + .000001f, "Increasing follow strengthened the bonus");
                previous = value;
            }
            Require(Mathf.Abs(Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.FollowThroughProfile.Persistence(power, tip, 1) - baseline) < .000001f,
                "Full follow must retain the original persistence");
            if (p == 0 || p == 100 || t == 0)
                Require(Mathf.Abs(Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.FollowThroughProfile.Persistence(power, tip, 0) - baseline) < .000001f,
                    "Power endpoints and centre tip must retain original persistence");
            float bonus = Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.FollowThroughProfile.Persistence(power, tip, 0) / baseline - 1;
            float peakBonus = Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.FollowThroughProfile.Persistence(.4f, tip, 0) / (.4f + .24f * tip) - 1;
            Require(bonus <= peakBonus + .000001f, "Bonus peak moved away from 40% power");
        }
        Require(Mathf.Abs(Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.FollowThroughProfile.Persistence(.4f, 1, 0) - .768f) < .000001f, "Peak must add 20 percent");
        Require(Mathf.Abs(Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.FollowThroughProfile.Persistence(.39999f, .8f, 0) - Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.FollowThroughProfile.Persistence(.40001f, .8f, 0)) < .0001f,
            "Persistence is discontinuous around the peak");
        var original = new Vector3(20, 30, -10); var boosted = original; var unchanged = Vector3.zero;
        for (int i = 0; i < 100; i++)
        {
            Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.FollowThroughProfile.ApplyPersistence(Vector3.forward, ref original, .03f, .01f, .592f, .01f);
            Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.FollowThroughProfile.ApplyPersistence(Vector3.forward, ref boosted, .03f, .01f, Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.FollowThroughProfile.Persistence(.4f, .8f, 0), .01f);
            Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.FollowThroughProfile.ApplyPersistence(Vector3.forward, ref unchanged, .03f, .01f, Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.FollowThroughProfile.Persistence(.4f, 0, 0), .01f);
        }
        Require(boosted.magnitude > original.magnitude && boosted.magnitude < new Vector3(20, 30, -10).magnitude,
            "Bonus must reduce damping without creating spin");
        Require(unchanged == Vector3.zero, "Centre hit bonus created spin");
    }
}
