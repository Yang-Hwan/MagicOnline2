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
        float started,next,last=-1,finishAt=-1,resultEndAt=-1;
        Assets.Scripts.Sight.Surface.Pavilion.InningScoreView inningView;InningScoreHistory inningHistory;int inningRevision=-1;
        bool stopped,capturing;
        SpriteRenderer tableBackground; Sprite lastTableSprite;
        readonly System.Diagnostics.Stopwatch captureClock=new System.Diagnostics.Stopwatch();
        public static MatchReplayRecorder Install(MonoBehaviour owner,MatchReplayArchive archive,Func<MatchBallState[]> balls,Func<bool> finished,Func<Camera> camera)
        {
            var go=new GameObject("Match presentation recorder");SceneManager.MoveGameObjectToScene(go,owner.gameObject.scene);
            var r=go.AddComponent<MatchReplayRecorder>();r.archive=archive;r.balls=balls;r.finished=finished;r.camera=camera;r.started=Time.unscaledTime;
            r.visuals=new MatchReplayStateCapture(archive.Presentation);Canvas.willRenderCanvases+=r.OnCanvas;return r;
        }
        // Start the tail from the visible result, not from the scoring/network event.
        public static void ResultShown(Scene scene)
        {
            foreach (var recorder in FindObjectsByType<MatchReplayRecorder>(FindObjectsSortMode.None))
                if (recorder.gameObject.scene == scene && !recorder.stopped && recorder.resultEndAt < 0)
                    recorder.resultEndAt = Time.unscaledTime + 3f;
        }
        public static void RecordShotClockBell(Scene scene,int stage,float volume)
        {
            if(!Application.isPlaying)return;
            foreach(var recorder in FindObjectsByType<MatchReplayRecorder>(FindObjectsSortMode.None))
                if(recorder.gameObject.scene==scene&&!recorder.stopped&&recorder.archive!=null)
                    recorder.archive.RecordEvent("shot-clock-bell",JsonUtility.ToJson(new Assets.Scripts.Sight.Surface.Pavilion.ReplayShotClockBell{stage=stage,volume=volume}));
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
                if(resultEndAt>=0&&now>=resultEndAt){Stop();return;}
                last=now-started;captureClock.Restart();
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"-replay-recording-off")<0)visuals.Capture(gameObject.scene,camera?.Invoke()??Camera.main,last);
                else {archive.Presentation.frames.Add(Array.Empty<int>());archive.Presentation.views.Add(MatchReplayPresentation.View.Capture(camera?.Invoke()??Camera.main));}
                archive.TimelineFrame(last,state);
                if(!tableBackground)foreach(var root in gameObject.scene.GetRootGameObjects())foreach(var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))if(renderer.name=="TableBg")tableBackground=renderer;
                if(tableBackground && tableBackground.sprite!=lastTableSprite){
                    lastTableSprite=tableBackground.sprite;string key=ArenaTableSkins.ReplayKey(lastTableSprite);
                    if(!string.IsNullOrEmpty(key))archive.RecordEvent("arena-table",key);
                }
                if(!inningView)inningView=Assets.Scripts.Sight.Surface.Pavilion.InningScoreView.RecordingSource(gameObject.scene);
                var history=inningView?inningView.History:null;
                if(history!=null&&history.Columns>0&&(!ReferenceEquals(history,inningHistory)||history.Revision!=inningRevision)){
                    inningHistory=history;inningRevision=history.Revision;
                    archive.RecordEvent("inning-score",JsonUtility.ToJson(new ReplayInningEvent{frame=archive.Presentation.frames.Count-1,scores=history.Capture(),name0=inningView.PlayerName(0),name1=inningView.PlayerName(1)}));
                }
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
