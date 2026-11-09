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
            { next = EditorApplication.timeSinceStartup + 2; deadline = next + 90; }
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetInt(Key + "stage", 0) != 0)
            {
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
                if (stage == 3 && view.CurrentPage != 1) throw new Exception("Practice return did not select single page");
                if (mode == 4)
                {
                    File.WriteAllText("Logs/BottomMenu/runtime-result.json", "{\"passed\":true,\"nativeNavigation\":5,\"practiceAndReplayRoutes\":4,\"returnedToSingle\":true}");
                    EditorApplication.isPlaying = false; return;
                }
                if (stage == 1)
                {
                    for (int i = 0; i < 5; i++)
                    { view.DesignRoot.Find("Nav" + i).GetComponent<Button>().onClick.Invoke(); if (view.CurrentPage != i) throw new Exception("Runtime tab mismatch"); }
                    view.SelectPage(0);
                    view.GetComponentsInChildren<Button>().Single(b => b.name == "HomeAction3").onClick.Invoke();
                    if (!view.DesignRoot.Find("Modal")) throw new Exception("Attendance dialog missing");
                    view.SelectPage(0);
                    view.DesignRoot.Find("Profile").GetComponent<Button>().onClick.Invoke();
                    if (!view.GetComponentInChildren<TMPro.TMP_InputField>()) throw new Exception("Profile input missing");
                    view.SelectPage(2);
                    view.transform.parent.gameObject.SetActive(false); view.transform.parent.gameObject.SetActive(true);
                    if (view.CurrentPage != 2) throw new Exception("Waiting-room return reset page");
                }
                view.SelectPage(1);
                view.GetComponentsInChildren<Button>().Single(b => b.name == "Practice" + mode).onClick.Invoke();
                SessionState.SetInt(Key + "stage", 2); next = EditorApplication.timeSinceStartup + 3;
            }
            else if (stage == 2)
            {
                if (SceneManager.GetActiveScene().path != PracticeSceneFlow.ScenePath) return;
                var expected = new[] { PracticeSceneFlow.Mode.ThreeBall, PracticeSceneFlow.Mode.FourBall, PracticeSceneFlow.Mode.Replay, PracticeSceneFlow.Mode.MatchReplay };
                if (PracticeSceneFlow.SelectedMode != expected[mode]) throw new Exception("Practice/replay mode mismatch");
                PracticeSceneFlow.ReturnToMenu(); SessionState.SetInt(Key + "mode", mode + 1);
                SessionState.SetInt(Key + "stage", 3); next = EditorApplication.timeSinceStartup + 2;
            }
        }
        catch (Exception e)
        { File.WriteAllText("Logs/BottomMenu/runtime-failure.txt", e.ToString()); EditorApplication.isPlaying = false; }
    }
}
