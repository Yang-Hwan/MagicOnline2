using System;
using System.IO;
using System.Linq;
using Assets.Scripts.Sight.Surface.Consist;
using Assets.Scripts.Often;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using StagePhysics=Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.PhysicsMng;
using Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital;

// Opt-in local smoke test. Uses no login, HTTP writes, matchmaking or purchases.
[InitializeOnLoad]
public static class BottomMenuRuntimeValidation
{
    const string Key = "BottomMenuRuntimeValidation.";
    [Serializable] sealed class Setup { public SceneSetup[] scenes; }
    static double next, deadline;
    static BottomMenuRuntimeValidation()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetInt(Key + "stage", 0) == 1)
            { next = EditorApplication.timeSinceStartup + 2; deadline = next + 180; }
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetInt(Key + "stage", 0) != 0)
            {
                RestoreStageProgress();
                SessionState.SetInt(Key + "stage", 0);
                var saved = SessionState.GetString(Key + "setup", "");
                if (!string.IsNullOrEmpty(saved)) EditorSceneManager.RestoreSceneManagerSetup(JsonUtility.FromJson<Setup>(saved).scenes);
            }
        };
        Application.logMessageReceived += (message, trace, type) =>
        {
            if (SessionState.GetInt(Key + "stage", 0) > 0 && EditorApplication.isPlaying && (type == LogType.Exception || type == LogType.Error))
                File.AppendAllText("Logs/BottomMenu/runtime-errors.txt", message + "\n" + trace + "\n");
        };
    }
    static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        const string request = "Logs/bottom-menu-runtime.request";
        if (File.Exists(request) && !EditorApplication.isPlayingOrWillChangePlaymode && SessionState.GetInt(Key + "stage", 0) == 0)
        {
            File.Delete(request);
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) { File.WriteAllText("Logs/BottomMenu/runtime-blocked.txt", "Unsaved scene: runtime validation did not run."); return; }
            SessionState.SetString(Key + "setup", JsonUtility.ToJson(new Setup { scenes = EditorSceneManager.GetSceneManagerSetup() }));
            SessionState.SetInt(Key + "stage", 1); SessionState.SetInt(Key + "mode", 0);
            EditorSceneManager.OpenScene("Assets/Scenes/Consist.unity");
            EditorApplication.isPlaying = true;
            return;
        }
        int stage = SessionState.GetInt(Key + "stage", 0);
        if (!EditorApplication.isPlaying || stage == 0 || EditorApplication.timeSinceStartup < next) return;
        try
        {
            if (deadline > 0 && EditorApplication.timeSinceStartup > deadline) throw new Exception("Runtime navigation timed out");
            int mode = SessionState.GetInt(Key + "mode", 0);
            if (stage == 1 || stage == 3)
            {
                var view = UnityEngine.Object.FindAnyObjectByType<BottomMenuView>();
                if (!view) return;
                if (stage == 3 && view.CurrentPage != (mode==4?0:1)) throw new Exception("Incorrect return page");
                if (mode == 5)
                {
                    if (!view.DesignRoot.Find("StageList")) throw new Exception("Missing stage list after returning");
                    File.WriteAllText("Logs/BottomMenu/runtime-result.json", "{\"passed\":true,\"nativeNavigation\":5,\"practiceAndReplayRoutes\":4,\"stageCards\":100,\"stageDemoAndAim\":true,\"sixFailuresAndRefill\":true,\"stageUnlock\":true}");
                    File.Delete("Logs/BottomMenu/runtime-failure.txt");
                    EditorApplication.isPlaying = false; return;
                }
                if (stage == 1)
                {
                    for (int i = 0; i < 5; i++)
                    { view.DesignRoot.Find("Nav" + i).GetComponent<Button>().onClick.Invoke(); if (view.CurrentPage != i) throw new Exception("Runtime tab mismatch"); }
                    view.SelectPage(0);
                    view.GetComponentsInChildren<Button>().Single(b => b.name == "HomeAction3").onClick.Invoke();
                    if (!view.DesignRoot.Find("Attendance")) throw new Exception("Attendance dialog missing");
                    view.SelectPage(0);
                    view.DesignRoot.Find("Profile").GetComponent<Button>().onClick.Invoke();
                    if (!view.GetComponentInChildren<TMPro.TMP_InputField>()) throw new Exception("Profile input missing");
                    view.SelectPage(2);
                    view.transform.parent.gameObject.SetActive(false); view.transform.parent.gameObject.SetActive(true);
                    if (view.CurrentPage != 2) throw new Exception("Waiting-room return reset page");
                }
                view.SelectPage(mode==3?0:1);
                string buttonName=mode==3?"HomeAction2":"Practice"+Math.Min(mode,3);
                view.GetComponentsInChildren<Button>().Single(b => b.name == buttonName).onClick.Invoke();
                if(mode==4)
                {
                    if(!string.IsNullOrEmpty(Assets.Scripts.Exert.BackSys.GameApiClient.UserId)) throw new Exception("Stage test requires the guest profile");
                    SessionState.SetInt(Key+"savedGuest",PlayerPrefs.GetInt(GuestStage,-1));
                    SessionState.SetBool(Key+"restoreGuest",true);
                    PlayerPrefs.DeleteKey(GuestStage);
                    var cards=view.DesignRoot.Find("StageList/Viewport/Cards");
                    if(!cards || cards.childCount!=100)throw new Exception("Stage catalog missing");
                    var strip=cards.parent.GetComponent<ScrollRect>();
                    strip.horizontalNormalizedPosition=1;
                    strip.onValueChanged.Invoke(Vector2.one);
                    var firstNumber=cards.Find("Stage1/Number");
                    if(!cards.Find("Stage100/Number") || (firstNumber && firstNumber.gameObject.activeInHierarchy))
                        throw new Exception("Scrolling did not reach the final stage");
                    strip.horizontalNormalizedPosition=0;
                    strip.onValueChanged.Invoke(Vector2.zero);
                    ScreenCapture.CaptureScreenshot("Logs/BottomMenu/stages.png");
                    SessionState.SetInt(Key+"stage",4);next=EditorApplication.timeSinceStartup+.5;
                    return;
                }
                SessionState.SetInt(Key + "stage", 2); next = EditorApplication.timeSinceStartup + 3;
            }
            else if (stage == 2)
            {
                if (SceneManager.GetActiveScene().path != PracticeSceneFlow.ScenePath) return;
                var expected = new[] { PracticeSceneFlow.Mode.ThreeBall, PracticeSceneFlow.Mode.FourBall, PracticeSceneFlow.Mode.Replay, PracticeSceneFlow.Mode.MatchReplay, PracticeSceneFlow.Mode.Stage };
                if (PracticeSceneFlow.SelectedMode != expected[mode]) throw new Exception("Practice/replay mode mismatch");
                if(mode==4)
                {
                    var physics=UnityEngine.Object.FindAnyObjectByType<Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.PhysicsMng>();
                    if(!physics || !physics.IsPracticeReplay) throw new Exception("Stage demo did not start");
                    SessionState.SetInt(Key+"stage",5);return;
                }
                PracticeSceneFlow.ReturnToMenu(); SessionState.SetInt(Key + "mode", mode + 1);
                SessionState.SetInt(Key + "stage", 3); next = EditorApplication.timeSinceStartup + 2;
            }
            else if(stage==4)
            {
                var view=UnityEngine.Object.FindAnyObjectByType<BottomMenuView>();
                view.GetComponentsInChildren<Button>().Single(b=>b.name=="Challenge1").onClick.Invoke();
                SessionState.SetInt(Key+"stage",2);next=EditorApplication.timeSinceStartup+2;
            }
            else if(stage==5)
            {
                var physics=UnityEngine.Object.FindAnyObjectByType<Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.PhysicsMng>();
                if(physics.IsPracticeReplay)return;
                if(!Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.ShotCtrl.canControl)throw new Exception("No input after stage demo");
                var layout=StageMissions.Get(1).layout;
                for(int i=0;i<layout.Length;i++)
                {
                    var p=physics.ballcs[i].body.position;
                    if(Mathf.Abs((p.x+1.304f)/2.606f-layout[i].x)>.001f || Mathf.Abs((.473f-p.z)/1.265f-layout[i].y)>.001f)
                        throw new Exception("Stage starting position mismatch");
                }
                TapCueBall(physics);
                SessionState.SetInt(Key+"stage",6);
            }
            else if(stage==6)
            {
                var physics=UnityEngine.Object.FindAnyObjectByType<StagePhysics>();
                var button=physics.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.name=="StageAction");
                if(!button || physics.inMove)return;
                int spent=StageMissions.Spent(1);
                string action=button.GetComponentInChildren<TMPro.TMP_Text>().text;
                if(spent<6)
                {
                    if(action!="다시 도전" || spent<1)throw new Exception("Failed shot did not consume half a star");
                    button.onClick.Invoke();
                    TapCueBall(physics);
                }
                else
                {
                    if(!action.Contains("15초"))throw new Exception("No penalty after six failed shots");
                    button.onClick.Invoke();
                    SessionState.SetInt(Key+"stage",7);
                }
            }
            else if(stage==7)
            {
                var physics=UnityEngine.Object.FindAnyObjectByType<StagePhysics>();
                if(StageMissions.Spent(1)!=0 || physics.IsPracticeReplay || !ShotCtrl.canControl)return;
                TapCueBall(physics);
                // Feed the recorded contact order through the same receiver used by the board colliders.
                var rail=typeof(StagePhysics).GetMethod("RecordStageRail",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                foreach(char side in StageMissions.Get(1).order)rail.Invoke(physics,new object[]{0,"Board"+side});
                SessionState.SetInt(Key+"stage",8);
            }
            else if(stage==8)
            {
                if(StageMissions.Best(1)!=6 || !StageMissions.Open(2))return;
                for(int attempt=1;attempt<=6;attempt++)
                {
                    StageMissions.Refill();
                    int award=0;
                    for(int i=0;i<attempt;i++)award=StageMissions.BeginAttempt();
                    if(award!=7-attempt)throw new Exception("Wrong half-star award at attempt "+attempt);
                    StageMissions.Win(award);
                    if(StageMissions.Best(1)!=6 || StageMissions.Spent(1)!=0)throw new Exception("Replay lost best rating");
                }
                PracticeSceneFlow.ReturnToMenu();SessionState.SetInt(Key+"mode",5);
                SessionState.SetInt(Key+"stage",3);next=EditorApplication.timeSinceStartup+2;
            }
        }
        catch (Exception e)
        { File.WriteAllText("Logs/BottomMenu/runtime-failure.txt", e.ToString()); EditorApplication.isPlaying = false; }
    }
    const string GuestStage="StageMissions.v1..1";
    static void RestoreStageProgress()
    {
        if(!SessionState.GetBool(Key+"restoreGuest",false))return;
        int saved=SessionState.GetInt(Key+"savedGuest",-1);
        if(saved<0)PlayerPrefs.DeleteKey(GuestStage);else PlayerPrefs.SetInt(GuestStage,saved);
        PlayerPrefs.Save();
        SessionState.SetBool(Key+"restoreGuest",false);
    }
    static void TapCueBall(StagePhysics physics)
    {
        var body=physics.ballcs[0].body;
        _=physics.StartShot(new Impulse(body.position,Vector3.left*.002f,Vector3.zero));
    }
}
