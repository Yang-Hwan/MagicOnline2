using System;
using System.IO;
using System.Linq;
using Assets.Scripts.Often;
using Photon.Pun;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
public static class PracticeMatchTestBuild
{
    const string Menu = "Tools/Magic Online/New Match Test/Enable In Editor";
    const string Request = "Logs/practice-test-build.request";
    [Serializable] class Report { public string result, executable, error; public double seconds; public bool routeValidated, pointerValidated, recoveryValidated, resultValidated; }
    static PracticeMatchTestBuild() { EditorApplication.update += Tick; }
    [MenuItem(Menu)]
    static void Toggle() => EditorPrefs.SetBool("MagicOnline2.PracticeMatchTest", !PracticeMatchTestRoute.Enabled);
    [MenuItem(Menu, true)]
    static bool ValidateToggle()
    {
        UnityEditor.Menu.SetChecked(Menu, PracticeMatchTestRoute.Enabled);
        return !EditorApplication.isPlayingOrWillChangePlaymode && !PhotonNetwork.IsConnected && !EditorApplication.isCompiling;
    }
    static void Tick()
    {
        if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        File.Delete(Request);
        EditorApplication.delayCall += Build;
    }
    [MenuItem("Tools/Magic Online/New Match Test/Build Windows Test Player")]
    public static void Build()
    {
        var record = new Report();
        Directory.CreateDirectory("Logs");
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || PhotonNetwork.IsConnected)
                throw new InvalidOperationException("Stop play mode and disconnect before building.");
            SpinPersistenceValidation.Run();
            PracticeMatchRouteValidation.Run(); record.routeValidated = true;
            PracticePointerValidation.Run(); record.pointerValidated = true;
            MatchRecoveryValidation.Run(3); MatchRecoveryValidation.Run(4); record.recoveryValidated = true;
            MatchResultProtocolValidation.Run(3); MatchResultProtocolValidation.Run(4); record.resultValidated = true;
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (!scenes.Contains(PracticeSceneFlow.ScenePath)) throw new InvalidOperationException("Practice scene is not enabled in build settings.");
            string directory = Path.GetFullPath(Path.Combine("Builds", "PracticeMatchTest-" + DateTime.Now.ToString("yyyyMMdd-HHmmss")));
            Directory.CreateDirectory(directory);
            record.executable = Path.Combine(directory, "MagicOnline2-Test.exe");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = scenes,
                locationPathName = record.executable, target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development });
            record.result = report.summary.result.ToString(); record.seconds = report.summary.totalTime.TotalSeconds;
            if (report.summary.result == BuildResult.Succeeded)
            {
                File.WriteAllText(Path.Combine(directory, "Start-Target-Win-Test.cmd"),
                    "@echo off\r\nstart \"\" \"%~dp0MagicOnline2-Test.exe\" -practice-match-test -practice-match-target-win -screen-fullscreen 0\r\n");
                File.WriteAllText(Path.Combine(directory, "Start-New-Match-Test.cmd"),
                    "@echo off\r\nstart \"\" \"%~dp0MagicOnline2-Test.exe\" -practice-match-test -screen-fullscreen 0\r\n");
                File.WriteAllText(Path.Combine(directory, "Start-Cancel-Offer-Test.cmd"),
                    "@echo off\r\nstart \"\" \"%~dp0MagicOnline2-Test.exe\" -practice-match-test -practice-match-cancel-offer-test -screen-fullscreen 0\r\n");
                File.WriteAllText(Path.Combine(directory, "Start-Quick-Match-Test.cmd"),
                    "@echo off\r\nstart \"\" \"%~dp0MagicOnline2-Test.exe\" -practice-match-test -practice-match-quick -screen-fullscreen 0\r\n");
                File.WriteAllText(Path.Combine(directory, "Start-Recovery-Match-Test.cmd"),
                    "@echo off\r\nstart \"\" \"%~dp0MagicOnline2-Test.exe\" -practice-match-test -practice-match-recovery-test -screen-fullscreen 0\r\n");
                File.WriteAllText(Path.Combine(directory, "Start-Scored-Recovery-Test.cmd"),
                    "@echo off\r\nstart \"\" \"%~dp0MagicOnline2-Test.exe\" -practice-match-test -practice-match-scored-recovery-test -screen-fullscreen 0\r\n");
                File.WriteAllText(Path.Combine(directory, "Start-Recovery-Expiry-Test.cmd"),
                    "@echo off\r\nstart \"\" \"%~dp0MagicOnline2-Test.exe\" -practice-match-test -practice-match-recovery-expiry-test -screen-fullscreen 0\r\n");
                File.WriteAllText(Path.Combine(directory, "Start-Moving-Disconnect-Test.cmd"),
                    "@echo off\r\nstart \"\" \"%~dp0MagicOnline2-Test.exe\" -practice-match-test -practice-match-moving-disconnect-test -screen-fullscreen 0\r\n");
                File.WriteAllText(Path.Combine(directory, "Start-Moving-Packet-Loss-Test.cmd"),
                    "@echo off\r\nstart \"\" \"%~dp0MagicOnline2-Test.exe\" -practice-match-test -practice-match-moving-loss-test -screen-fullscreen 0\r\n");
            }
            else record.error = $"Build reported {report.summary.totalErrors} errors; see Unity Console.";
        }
        catch (Exception e) { record.result = "Failed"; record.error = e.Message; }
        File.WriteAllText("Logs/practice-test-build.json", JsonUtility.ToJson(record, true));
    }
}
