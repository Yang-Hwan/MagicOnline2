using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Assets.Scripts.Often;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using StagePhysics=Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.PhysicsMng;

// Explicit real-scene regression test. It never shoots or changes mission progress.
[InitializeOnLoad]
public static class StageGreenReplayValidation
{
    const string Root="Logs/StageGreen/",Key="StageGreenValidation";
    const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
    static double stable=EditorApplication.timeSinceStartup+1;
    static int captures;
    static StageGreenReplayValidation()
    {
        EditorApplication.update+=Tick;
        EditorApplication.playModeStateChanged+=_=>stable=EditorApplication.timeSinceStartup+1;
    }
    static void Tick()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup<stable)return;
        try
        {
            if(File.Exists(Root+"run.request") && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if(Enumerable.Range(0,UnityEngine.SceneManagement.SceneManager.sceneCount).Any(i=>UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))return;
                EditorSceneManager.OpenScene("Assets/Scenes/Consist.unity");File.Delete(Root+"run.request");
                SessionState.SetInt(Key,1);EditorApplication.isPlaying=true;return;
            }
            int step=SessionState.GetInt(Key,0);if(!EditorApplication.isPlaying || step==0)return;
            if(step==1)
            {
                StageMissions.Enter(1);SessionState.SetInt(Key,2);return;
            }
            var physics=UnityEngine.Object.FindAnyObjectByType<StagePhysics>();if(!physics)return;
            if(step==2)
            {
                var start=physics.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.name=="StageStart");if(!start)return;
                start.onClick.Invoke();SessionState.SetInt(Key,3);return;
            }
            if(step==3)
            {
                if(physics.IsPracticeReplay || !Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.ShotCtrl.canControl)return;
                physics.GetComponentsInChildren<Button>().First(b=>b.name=="StageAction").onClick.Invoke();
                captures=0;SessionState.SetInt(Key,4);return;
            }
            if(step==4)
            {
                if(GameObject.Find("Stage Cushion Light"))throw new Exception("Yellow replacement glow still exists");
                var line=(ParticleSystem)typeof(StagePhysics).GetField("guideLine",Flags).GetValue(physics);
                int cursor=(int)typeof(StagePhysics).GetField("demoCushionCursor",Flags).GetValue(physics);
                if(line && line.time>.08f && line.time<.3f && (captures&(1<<cursor))==0)
                {
                    var all=line.GetComponentsInChildren<ParticleSystem>(true);
                    if(Time.timeScale!=0 || all.Length<2 || all.Any(p=>!p.main.useUnscaledTime || p.time<=0 || p.particleCount<=0))
                        throw new Exception("Original green child effect did not animate during frozen-time demo");
                    captures|=1<<cursor;
                    File.AppendAllText(Root+"particles.txt",cursor+":"+string.Join(",",all.Select(p=>p.name+"="+p.particleCount))+"\n");
                    ScreenCapture.CaptureScreenshot(Root+"contact-"+cursor+".png");
                }
                if(!physics.IsPracticeReplay)
                {
                    if(captures==0)throw new Exception("No original green-effect contact was observed");
                    File.WriteAllText(Root+"result.json","{\"passed\":true,\"nativeStageDemo\":true,\"childParticlesAtTimeScaleZero\":true,\"yellowGlowAbsent\":true}");
                    SessionState.SetInt(Key,0);
                }
            }
        }
        catch(Exception ex){Directory.CreateDirectory(Root);File.WriteAllText(Root+"error.txt",ex.ToString());SessionState.SetInt(Key,0);}
    }
}
