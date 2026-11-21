using Assets.Scripts.Often;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{
    public partial class PhysicsMng
    {
        MatchReplayPresentation replayPresentation,parsedPresentation,replayPresentationBeforeLoad;
        MatchReplayDisplay replayDisplay;
        MatchReplayStateDisplay replayStateDisplay;
        Camera replayPresentationCamera;
        MatchReplayPresentation.View replaySavedView;
        float replaySavedAspect;
        void BeginRecordedPresentation()
        {
            if(replayPresentation==null||replayDisplay!=null||replayStateDisplay!=null)return;
            if(replayPresentation.presentationVersion==MatchReplayStateCapture.Format||replayPresentation.presentationVersion=="state-events-1")replayStateDisplay=new MatchReplayStateDisplay(transform,replayPresentation,replayPanel);
            else replayDisplay=new MatchReplayDisplay(transform,replayPresentation,replayPanel);
            replayPresentationCamera=InputOutput.usedCamera?InputOutput.usedCamera:Camera.main;
            if(replayPresentationCamera){replaySavedView=MatchReplayPresentation.View.Capture(replayPresentationCamera);replaySavedAspect=replayPresentationCamera.aspect;}
            if(replayPanel){
                var canvas=replayPanel.GetComponent<Canvas>()??replayPanel.gameObject.AddComponent<Canvas>();
                canvas.overrideSorting=true;canvas.sortingOrder=32001;
                if(!replayPanel.GetComponent<GraphicRaycaster>())replayPanel.gameObject.AddComponent<GraphicRaycaster>();
            }
            ApplyRecordedPresentation();
        }
        void ApplyRecordedPresentation(){
            if(replayDisplay==null&&replayStateDisplay==null)return;
            if(replayPresentationCamera){
                var view=replayPresentation.views[ReplayFrameIndex];view.Apply(replayPresentationCamera);
                float fit=Mathf.Min(Screen.width/(float)replayPresentation.width,Screen.height/(float)replayPresentation.height);
                float w=fit*replayPresentation.width/Screen.width,h=fit*replayPresentation.height/Screen.height;
                replayPresentationCamera.rect=new Rect((1-w)*.5f+view.rect.x*w,(1-h)*.5f+view.rect.y*h,view.rect.width*w,view.rect.height*h);
                replayPresentationCamera.aspect=replayPresentation.width*view.rect.width/(replayPresentation.height*view.rect.height);
            }
            replayDisplay?.Apply(ReplayFrameIndex);replayStateDisplay?.Apply(ReplayFrameIndex);
        }
        void StopRecordedPresentation(){replayDisplay?.Dispose();replayDisplay=null;replayStateDisplay?.Dispose();replayStateDisplay=null;if(replayPresentationCamera){replaySavedView.Apply(replayPresentationCamera);replayPresentationCamera.aspect=replaySavedAspect;}replayPresentationCamera=null;}
    }
}
