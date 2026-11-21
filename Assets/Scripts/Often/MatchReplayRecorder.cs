using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.Often
{
    // Lives independently of the match panel, which is disabled by the result UI.
    // Canvas geometry is captured after HUD layout, including result animations.
    public sealed class MatchReplayRecorder : MonoBehaviour
    {
        MatchReplayArchive archive;
        Func<MatchBallState[]> balls;
        Func<bool> finished;
        Func<Camera> camera;
        MatchReplayStateCapture visuals;
        float started,next,last=-1,finishAt=-1;
        bool stopped,capturing;
        readonly System.Diagnostics.Stopwatch captureClock=new System.Diagnostics.Stopwatch();
        public static MatchReplayRecorder Install(MonoBehaviour owner,MatchReplayArchive archive,Func<MatchBallState[]> balls,Func<bool> finished,Func<Camera> camera)
        {
            var go=new GameObject("Match presentation recorder");SceneManager.MoveGameObjectToScene(go,owner.gameObject.scene);
            var r=go.AddComponent<MatchReplayRecorder>();r.archive=archive;r.balls=balls;r.finished=finished;r.camera=camera;r.started=Time.unscaledTime;
            r.visuals=new MatchReplayStateCapture(archive.Presentation);Canvas.willRenderCanvases+=r.OnCanvas;return r;
        }
        void OnCanvas(){if(!stopped&&!capturing&&Time.unscaledTime>=next)Capture();}
        void Capture()
        {
            if(archive==null||visuals==null||stopped)return;capturing=true;
            try{
                float now=Time.unscaledTime;if(now-started<=last)return;
                var state=balls();if(state==null)return;
                if(finished()){
                    archive.TimelineComplete();
                    if(finishAt<0){finishAt=now;archive.RecordEvent("finished",archive.Presentation.result);}
                }
                if(finishAt>=0&&now-finishAt>8){Stop();return;}
                last=now-started;captureClock.Restart();
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"-replay-recording-off")<0)visuals.Capture(gameObject.scene,camera?.Invoke()??Camera.main,last);
                else {archive.Presentation.frames.Add(Array.Empty<int>());archive.Presentation.views.Add(MatchReplayPresentation.View.Capture(camera?.Invoke()??Camera.main));}
                archive.TimelineFrame(last,state);
                captureClock.Stop();
                MatchReplayPerformanceProbe.Captures++;double milliseconds=captureClock.Elapsed.TotalMilliseconds;MatchReplayPerformanceProbe.CaptureMilliseconds+=milliseconds;MatchReplayPerformanceProbe.MaxCaptureMilliseconds=Math.Max(MatchReplayPerformanceProbe.MaxCaptureMilliseconds,milliseconds);
                next=Time.unscaledTime+1f/30f;
            }
            catch(Exception error){archive.Failed(error);Stop();}
            finally{capturing=false;}
        }
        public void Seal(){if(stopped)return;Canvas.ForceUpdateCanvases();Capture();Stop();}
        public void Stop(){if(stopped)return;stopped=true;Canvas.willRenderCanvases-=OnCanvas;try{if(archive!=null&&archive.Ready)visuals?.CompleteAssets();}catch(Exception error){archive.Failed(error);}visuals?.Dispose();visuals=null;MatchReplayDisplay.Release(gameObject);}
        void OnDestroy()=>Stop();
    }
}
