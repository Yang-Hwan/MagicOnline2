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
                    if(!physics) return;
                    var start=physics.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.name=="StageStart");
                    if(!start) return;
                    var panel=UnityEngine.Object.FindAnyObjectByType<Assets.TutorialInfo.Scripts.TableSet06.Sight.Surface.PnlMatch>();
                    var players=panel.transform.Find("PlayerBoxs").GetComponent<CanvasGroup>();
                    if(physics.IsPracticeReplay || ShotCtrl.canControl || !players || players.alpha!=0 || players.blocksRaycasts || GameObject.Find("Table/UI_TOP"))
                        throw new Exception("Stage intro did not lock input and hide the match HUD");
                    CheckStageLayout(physics);
                    start.onClick.Invoke();
                    SessionState.SetInt(Key+"guideNumber",0);
                    SessionState.SetInt(Key+"stage",9);return;
                }
                PracticeSceneFlow.ReturnToMenu(); SessionState.SetInt(Key + "mode", mode + 1);
                SessionState.SetInt(Key + "stage", 3); next = EditorApplication.timeSinceStartup + 2;
            }
            else if(stage==4)
            {
                var view=UnityEngine.Object.FindAnyObjectByType<BottomMenuView>();
                view.GetComponentsInChildren<Button>().Single(b=>b.name=="Challenge1").onClick.Invoke();
                SessionState.SetInt(Key+"stage",2);next=EditorApplication.timeSinceStartup;
            }
            else if(stage==9)
            {
                var physics=UnityEngine.Object.FindAnyObjectByType<StagePhysics>();
                var marker=physics.GetComponentsInChildren<TMPro.TMP_Text>().FirstOrDefault(t=>t.name=="Stage Cushion Number");
                int seen=SessionState.GetInt(Key+"guideNumber",0);
                if(marker)
                {
                    if(ShotCtrl.canControl || physics.IsPracticeReplay) throw new Exception("Input enabled during cushion guide");
                    if(marker.text==(seen+1).ToString())
                    {
                        var line=physics.cushionHit.transform.Find(seen==1?"lineT":"lineR").GetComponent<ParticleSystem>();
                        if(!line.isPlaying) throw new Exception("Stage cushion did not light up with its number");
                        var badge=marker.transform.parent;
                        var camera=InputOutput.usedCamera;
                        Vector3 railCenter=camera.WorldToScreenPoint(line.transform.position);
                        Vector2 badgeCenter=RectTransformUtility.WorldToScreenPoint(null,badge.position);
                        float error=seen==1?Mathf.Abs(badgeCenter.x-railCenter.x):Mathf.Abs(badgeCenter.y-railCenter.y);
                        if(error>Screen.height*.05f) throw new Exception("Stage number was not centered along its cushion");
                        SessionState.SetInt(Key+"guideNumber",seen+1);
                    }
                    return;
                }
                if(seen!=3 || !ShotCtrl.canControl || StageMissions.Spent(1)!=0) throw new Exception("Cushion guide did not finish in aim mode");
                var action=physics.GetComponentsInChildren<Button>().Single(b=>b.name=="StageAction");
                if(((RectTransform)action.transform.parent).anchorMax.y!=1) throw new Exception("Mission bar not at top");
                action.onClick.Invoke();
                if(!physics.IsPracticeReplay) throw new Exception("Manual stage preview did not start");
                SessionState.SetInt(Key+"stage",5);
            }
            else if(stage==5)
            {
                var physics=UnityEngine.Object.FindAnyObjectByType<Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.PhysicsMng>();
                if(physics.IsPracticeReplay)return;
                if(!Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.ShotCtrl.canControl)throw new Exception("No input after stage demo");
                CheckStageLayout(physics);
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
                CheckStageScores();
                var rail=typeof(StagePhysics).GetMethod("RecordStageRail",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                var ball=typeof(StagePhysics).GetMethod("RecordStageBall",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                int red=2;
                foreach(char contact in StageMissions.Get(1).order)
                    if(contact=='O') ball.Invoke(physics,new object[]{0,red++});
                    else rail.Invoke(physics,new object[]{0,"Board"+contact});
                rail.Invoke(physics,new object[]{0,"BoardB"});
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
    static void CheckStageLayout(StagePhysics physics)
    {
        var layout=StageMissions.Get(1).layout;
        for(int i=0;i<layout.Length;i++)
        {
            var p=physics.ballcs[i].body.position;
            if(Mathf.Abs((p.x+1.304f)/2.606f-layout[i].x)>.001f || Mathf.Abs((.473f-p.z)/1.265f-layout[i].y)>.001f)
                throw new Exception("Stage starting position mismatch");
        }
    }
    const string GuestStage="StageMissions.v1..1";
    static void CheckStageScores()
    {
        void Check(StageMissions.Mission m,string contacts,bool scored,bool passed)
        {
            var shot=new StageMissions.Shot(m);
            foreach(char c in contacts)
                if(char.IsDigit(c)) shot.Ball(c-'0'); else shot.Rail(c);
            if(shot.Scored!=scored || shot.Passed!=passed)throw new Exception("Stage score mismatch: "+contacts);
        }
        var first=StageMissions.Get(1);
        Check(first,"2RTR3BL",true,true);
        Check(first,"3RTR2",true,true);
        Check(first,"2TRR3",true,false);
        Check(first,"2RTR2",false,false);
        Check(first,"2R1TR3",false,false);
        var three=new StageMissions.Mission{balls=3,cue=0,order="ORLTO"};
        Check(three,"1RLT2B",true,true);
        Check(three,"1RL2T",false,false);
        for(int i=1;i<=StageMissions.Count;i++)
        {
            var m=StageMissions.Get(i);
            string contacts=m.order;
            int a=m.balls==4?2:1-m.cue, b=m.balls==4?3:2;
            int at=contacts.IndexOf('O');
            contacts=contacts.Substring(0,at)+a+contacts.Substring(at+1).Replace('O',(char)('0'+b));
            Check(m,contacts+"BL",true,true);
        }
    }
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
