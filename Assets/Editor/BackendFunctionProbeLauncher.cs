#if BACKND_LEGACY_DIAGNOSTICS
using Assets.Scripts.Prack.BackSys;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BackendFunctionProbeLauncher
{
    const string PendingKey = "MagicOnline.FunctionProbe.StartPending";
    static BackendFunctionProbeLauncher() { EditorApplication.playModeStateChanged += Changed; }

    [MenuItem("Tools/Magic Online/Backend Function Probe/Run from Arise")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) { Show(); return; }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/Scenes/Arise.unity")
            EditorSceneManager.OpenScene("Assets/Scenes/Arise.unity");
        SessionState.SetBool(PendingKey, true);
        EditorApplication.isPlaying = true;
    }

    static void Changed(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false)) return;
        SessionState.SetBool(PendingKey, false); Show();
    }

    [MenuItem("Tools/Magic Online/Backend Function Probe/Show window in Play Mode")]
    public static void Show()
    {
        if (!EditorApplication.isPlaying) return;
        var runner = Object.FindFirstObjectByType<BackendFunctionProbeRunner>();
        if (runner == null)
        {
            var obj = new GameObject("BackendFunctionProbe (diagnostic only)");
            Object.DontDestroyOnLoad(obj); runner = obj.AddComponent<BackendFunctionProbeRunner>();
        }
        runner.ShowWindow = true;
    }
}

#endif
