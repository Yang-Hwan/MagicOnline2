using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
public static class TimeoutRecoveryBuild
{
    const string Request = "Logs/timeout-recovery-build.request";
    static TimeoutRecoveryBuild() { EditorApplication.update += Tick; }
    static void Tick()
    {
        if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        File.Delete(Request);
        EditorApplication.delayCall += Build;
    }
    [MenuItem("Tools/Magic Online/Build Timeout Recovery Player")]
    public static void Build()
    {
        try
        {
            string output = Path.GetFullPath("Builds/TimeoutRecovery/MagicOnline2.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = output, target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            File.WriteAllText("Logs/timeout-recovery-build.txt", $"{report.summary.result}\n{output}\nErrors: {report.summary.totalErrors}");
            if (report.summary.result != BuildResult.Succeeded) Debug.LogError("Timeout recovery build failed.");
        }
        catch (Exception e) { File.WriteAllText("Logs/timeout-recovery-build.txt", e.ToString()); Debug.LogException(e); }
    }
}
