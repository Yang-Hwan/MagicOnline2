using Assets.Scripts.Often;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{
    public partial class PhysicsMng
    {
        struct MatchTrailShot { public int frame, cue; }
        readonly System.Collections.Generic.List<MatchTrailShot> matchTrailShots = new System.Collections.Generic.List<MatchTrailShot>();
        readonly System.Collections.Generic.List<Vector3> matchTrailPoints = new System.Collections.Generic.List<Vector3>();
        int matchTrailStart=-1;
        SpriteRenderer replayTableBackground;
        Sprite replayOriginalTable;
        string replayTableKey;
        readonly System.Collections.Generic.List<MatchReplayPresentation.Event> replayTableChanges=new System.Collections.Generic.List<MatchReplayPresentation.Event>();
        void BuildMatchReplayEnvironment()
        {
            replayTableChanges.Clear();
            foreach(var e in replayPresentation.events)if(e.kind=="arena-table")replayTableChanges.Add(e);
            matchTrailShots.Clear();matchTrailPoints.Clear();matchTrailStart=-1;replayTrailFrame=-1;
            foreach(var e in replayPresentation.events)if(e.kind=="shot"&&!string.IsNullOrEmpty(e.value)){
                var shot=JsonUtility.FromJson<MatchShotCommand>(e.value);if(shot==null||shot.seat<0||shot.seat>=ballcs.Length)continue;
                int low=0,high=replayFrames.Count;
                while(low<high){int mid=(low+high)/2;if(replayFrames[mid].time<e.time)low=mid+1;else high=mid;}
                if(low<replayFrames.Count)matchTrailShots.Add(new MatchTrailShot{frame=low,cue=shot.seat});
            }
            matchTrailShots.Sort((a,b)=>a.frame.CompareTo(b.frame));
            foreach(var root in gameObject.scene.GetRootGameObjects())foreach(var sprite in root.GetComponentsInChildren<SpriteRenderer>(true))if(sprite.name=="TableBg")replayTableBackground=sprite;
            if(replayTableBackground)replayOriginalTable=replayTableBackground.sprite;
            replayTableKey=null;
        }
        void ApplyMatchReplayTable()
        {
            if(!replayTableBackground||replayFrames.Count==0)return;
            string key=null;float time=replayFrames[ReplayFrameIndex].time;
            foreach(var e in replayTableChanges)if(e.time<=time)key=e.value;
            if(key==replayTableKey)return;replayTableKey=key;
            replayTableBackground.sprite=ArenaTableSkins.ReplaySprite(key)??replayOriginalTable;
        }
        void UpdateMatchCueTrail()
        {
            int low=0,high=matchTrailShots.Count;
            while(low<high){int mid=(low+high)/2;if(matchTrailShots[mid].frame<=ReplayFrameIndex)low=mid+1;else high=mid;}
            if(low==0){matchTrailPoints.Clear();matchTrailStart=-1;replayTrailFrame=-1;replayTrail.positionCount=0;replayTrail.enabled=false;return;}
            var shot=matchTrailShots[low-1];bool pathChanged=false;
            if(shot.frame!=matchTrailStart||ReplayFrameIndex<replayTrailFrame){pathChanged=true;matchTrailStart=shot.frame;matchTrailPoints.Clear();replayTrailFrame=shot.frame-1;replayTrail.positionCount=0;}
            for(int frame=replayTrailFrame+1;frame<=ReplayFrameIndex;frame++){
                var point=replayFrames[frame].positions[shot.cue];point.y=.002f;
                if(matchTrailPoints.Count>0&&(point-matchTrailPoints[matchTrailPoints.Count-1]).sqrMagnitude<.000001f)continue;
                if(matchTrailPoints.Count>=4096){for(int i=matchTrailPoints.Count-2;i>0;i-=2)matchTrailPoints.RemoveAt(i);}
                matchTrailPoints.Add(point);pathChanged=true;
            }
            if(pathChanged){replayTrail.positionCount=matchTrailPoints.Count;for(int i=0;i<matchTrailPoints.Count;i++)replayTrail.SetPosition(i,matchTrailPoints[i]);}
            replayTrailFrame=ReplayFrameIndex;replayTrail.enabled=matchTrailPoints.Count>1;
        }

        RectTransform matchReplayOperations, matchReplayList;
        Button matchReplayOperationsToggle, matchReplayListToggle, matchReplayJumpToggle;
        bool matchReplayOperationsShown = true, matchReplayListShown, matchReplayJumpShown;

        RectTransform ReplaySection(string name, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rect.SetParent(replayPanel, false); rect.sizeDelta = size;
            rect.GetComponent<Image>().color = new Color(.025f, .07f, .085f, .94f);
            ReplayChrome.Apply(rect.gameObject, ReplayChromeGraphic.Finish.Charcoal, true);
            return rect;
        }
        void PlaceReplayControl(Transform target, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)target; rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = position; rect.sizeDelta = size;
            if (target.TryGetComponent<Button>(out var button))
            {
                var label = button.GetComponentInChildren<TMPro.TMP_Text>(true);
                if (label) label.rectTransform.sizeDelta = size;
            }
        }
        void UpdateMatchReplayControls()
        {
            if (!IsReplayBrowser || !replayPanel) return;
            if (!matchReplayOperations)
            {
                // Keep the root alive even when every optional panel is closed.
                replayPanel.localScale = Vector3.one;
                replayPanel.anchorMin = Vector2.zero; replayPanel.anchorMax = Vector2.one;
                replayPanel.offsetMin = replayPanel.offsetMax = Vector2.zero;
                replayPanel.GetComponent<Image>().enabled = false;
                matchReplayOperations = ReplaySection("Match Replay Operations", new Vector2(1200, 76));
                matchReplayOperations.anchorMin = matchReplayOperations.anchorMax = new Vector2(.5f, 0);
                matchReplayOperations.anchoredPosition = new Vector2(0, 48);
                matchReplayList = ReplaySection("Match Replay List", new Vector2(730, 224));
                matchReplayList.anchorMin = matchReplayList.anchorMax = new Vector2(1, 1);
                matchReplayList.pivot = new Vector2(1, 1);
                matchReplayList.anchoredPosition = new Vector2(-140, -178);
                // Existing folder/slot widgets keep their bindings and relative layout.
                var children = new System.Collections.Generic.List<Transform>();
                foreach (Transform child in replayPanel) children.Add(child);
                foreach (var child in children)
                {
                    if (child == matchReplayOperations || child == matchReplayList || child == replayExit.transform) continue;
                    child.SetParent(matchReplayList, false);
                }
                PlaceReplayControl(matchReplayProgressLabel.transform, matchReplayOperations, new Vector2(-490, 0), new Vector2(180, 38));
                PlaceReplayControl(replaySeek.transform, matchReplayOperations, new Vector2(-245, 0), new Vector2(280, 10));
                PlaceReplayControl(replaySlower.transform, matchReplayOperations, new Vector2(-55, 0), new Vector2(44, 38));
                PlaceReplayControl(replaySpeedText.transform, matchReplayOperations, new Vector2(0, 0), new Vector2(60, 38));
                PlaceReplayControl(replayFaster.transform, matchReplayOperations, new Vector2(55, 0), new Vector2(44, 38));
                PlaceReplayControl(replayPlay.transform, matchReplayOperations, new Vector2(190, 0), new Vector2(124, 38));
                PlaceReplayControl(replayNext.transform, matchReplayOperations, new Vector2(345, 0), new Vector2(124, 38));
                PlaceReplayControl(replayReset.transform, matchReplayOperations, new Vector2(490, 0), new Vector2(124, 38));
                matchReplayHeading.gameObject.SetActive(true);
                PlaceReplayControl(matchReplayHeading.transform, matchReplayList, new Vector2(-225, 88), new Vector2(210, 38));
                matchReplayHeading.text = IsMatchReplayBrowser ? "시합 리플레이" : "개별 리플레이";matchReplayHeading.fontSize=24;matchReplayHeading.fontStyle=TMPro.FontStyles.Bold;
                matchReplayHeading.color=new Color(.96f,.94f,.84f);
                PlaceReplayControl(replayFolderScroll.transform,matchReplayList,new Vector2(70,88),new Vector2(320,32));
                PlaceReplayControl(replayFolderAdd.transform,matchReplayList,new Vector2(268,88),new Vector2(52,32));
                replayFolderAdd.GetComponentInChildren<TMPro.TMP_Text>(true).text="+";
                PlaceReplayControl(replayFolderDelete.transform,matchReplayList,new Vector2(323,88),new Vector2(50,32));
                matchReplayTitleLabel.gameObject.SetActive(false);
                PlaceReplayControl(replayTitle.transform,matchReplayList,new Vector2(-173,44),new Vector2(300,32));
                PlaceReplayControl(replayInfo.transform,matchReplayList,new Vector2(165,44),new Vector2(340,32));
                replayInfo.color=new Color(.8f,.8f,.73f);
                replayTitle.targetGraphic=ReplayChrome.Apply(replayTitle.gameObject);
                if(replayTitle.textComponent)replayTitle.textComponent.color=new Color(.96f,.94f,.84f);
                if(replayFolderScroll.TryGetComponent<Image>(out var folderBackdrop))folderBackdrop.color=Color.clear;
                replayDragHeader.GetComponent<Image>().color=Color.clear;
                replayDragHeader.GetComponentInChildren<TMPro.TMP_Text>(true).color=new Color(.7f,.65f,.45f,.65f);
                foreach(var control in new[]{replaySlower,replayFaster,replayPlay,replayNext,replayReset,replayFolderAdd,replayFolderDelete}) ReplayChrome.Apply(control.gameObject);
                ReplayChrome.Apply(replaySeek.gameObject);
                replaySeek.targetGraphic=ReplayChrome.Apply(replaySeek.handleRect.gameObject,ReplayChromeGraphic.Finish.Amber);
                ReplayChrome.Apply(replaySeek.fillRect.gameObject,ReplayChromeGraphic.Finish.Amber);
                replaySeek.handleRect.sizeDelta=new Vector2(22,26);
                replaySpeedText.color=new Color(.96f,.94f,.84f);
                matchReplayProgressLabel.color=new Color(.96f,.94f,.84f);matchReplayProgressLabel.fontSize=20;

                replayDragHeader.GetComponent<Assets.TutorialInfo.Scripts.TableSet06.Sight.Surface.ReplayPanelDrag>().Initialize(matchReplayList,.82f);
                replayExit.onClick.RemoveAllListeners();
                replayExit.onClick.AddListener(() => { ExitPracticeReplay(); Assets.Scripts.Often.PracticeSceneFlow.ReturnToMenu(); });
                matchReplayOperationsToggle = ReplayButton("Match Replay Operations Toggle", "작동", 0, () => { matchReplayOperationsShown = !matchReplayOperationsShown; RefreshMatchReplayPanels(); });
                matchReplayListToggle = ReplayButton("Match Replay List Toggle", "목록", 0, () => {
                    matchReplayListShown = !matchReplayListShown;
                    if (!matchReplayListShown && replayFolderDialog) replayFolderDialog.SetActive(false);
                    RefreshMatchReplayPanels();
                });
                matchReplayJumpToggle = ReplayButton("Match Replay Jump Toggle", "점프", 0, () => { matchReplayJumpShown = !matchReplayJumpShown; RefreshMatchReplayPanels(); });
                var toggles = new[] { matchReplayOperationsToggle, matchReplayListToggle, matchReplayJumpToggle };
                for (int i = 0; i < toggles.Length; i++)
                {
                    var rect = (RectTransform)toggles[i].transform;
                    PlaceReplayControl(rect, replayPanel, Vector2.zero, new Vector2(104, 48));
                    rect.anchorMin = rect.anchorMax = new Vector2(1, 1);
                    rect.anchoredPosition = new Vector2(-72, -176 - i * 60);
                    var iconObject=new GameObject("Control Icon",typeof(RectTransform),typeof(CanvasRenderer),typeof(ReplayControlIcon));
                    var iconRect=(RectTransform)iconObject.transform;iconRect.SetParent(rect,false);iconRect.sizeDelta=new Vector2(22,22);iconRect.anchoredPosition=new Vector2(-32,0);
                    var icon=iconObject.GetComponent<ReplayControlIcon>();icon.Kind=i;icon.raycastTarget=false;
                    var toggleLabel=toggles[i].GetComponentInChildren<TMPro.TMP_Text>(true);toggleLabel.rectTransform.anchoredPosition=new Vector2(12,0);toggleLabel.rectTransform.sizeDelta=new Vector2(70,48);
                }
            }
            matchReplayJumpToggle.gameObject.SetActive(IsMatchReplayBrowser);
            if (!IsMatchReplayBrowser) UpdateStageReplaySlotLayout();
            replayPanel.gameObject.SetActive(true);
            replayExit.gameObject.SetActive(true); replayExit.interactable = true;
            replayExit.GetComponentInChildren<TMPro.TMP_Text>(true).text = "‹  나감";
            ReplayChrome.Apply(replayExit.gameObject);
            PlaceReplayControl(replayExit.transform, replayPanel, Vector2.zero, new Vector2(104, 54));
            var exitRect = (RectTransform)replayExit.transform;
            exitRect.anchorMin = exitRect.anchorMax = new Vector2(0, 1);
            exitRect.anchoredPosition = new Vector2(72, -56);
            RefreshMatchReplayPanels();
        }
        void RefreshMatchReplayPanels()
        {
            if (!matchReplayOperations) return;
            matchReplayOperations.gameObject.SetActive(matchReplayOperationsShown);
            matchReplayList.gameObject.SetActive(matchReplayListShown);
            if (replayInningView)
            {
                replayInningView.SetReplayTopLayout();
                if (replayInningView.IsOpen != matchReplayJumpShown) replayInningView.SetOpen(matchReplayJumpShown);
            }
            matchReplayJumpToggle.interactable = replayInningView != null;
            var buttons = new[] { matchReplayOperationsToggle, matchReplayListToggle, matchReplayJumpToggle };
            var selected = new[] { matchReplayOperationsShown, matchReplayListShown, matchReplayJumpShown && replayInningView != null };
            for (int i = 0; i < buttons.Length; i++) ReplayChrome.Apply(buttons[i].gameObject, selected[i] ? ReplayChromeGraphic.Finish.Emerald : ReplayChromeGraphic.Finish.Charcoal);
            ReplayChrome.Apply(replayPlay.gameObject,ReplayChromeGraphic.Finish.Emerald);
            replayPlayText.text = replayPlaying ? "Ⅱ  일시정지" : "▶  재생";
            replayNext.GetComponentInChildren<TMPro.TMP_Text>(true).text="›  다음";
            replayReset.GetComponentInChildren<TMPro.TMP_Text>(true).text="‹‹  처음";
            float current=IsPracticeReplay && replayFrames.Count>0 ? replayFrames[Mathf.Clamp(ReplayFrameIndex,0,replayFrames.Count-1)].time : 0;
            float duration=replayFrames.Count>0 ? replayFrames[replayFrames.Count-1].time : 0;
            matchReplayProgressLabel.text=$"{(int)current/60:00}:{(int)current%60:00} / {(int)duration/60:00}:{(int)duration%60:00}";
            if(replaySlotButtons!=null)for(int i=0;i<replaySlotButtons.Length;i++)ReplayChrome.Apply(replaySlotButtons[i].gameObject,i==selectedReplaySlot ? ReplayChromeGraphic.Finish.Amber : replaySlotFilled[i] ? ReplayChromeGraphic.Finish.Emerald : ReplayChromeGraphic.Finish.Charcoal);
            foreach(var folder in replayFolderButtons)ReplayChrome.Apply(folder.gameObject,folder.name=="Replay Folder "+selectedReplayFolder ? ReplayChromeGraphic.Finish.Emerald : ReplayChromeGraphic.Finish.Charcoal);
        }

        MatchReplayPresentation replayPresentation,parsedPresentation,replayPresentationBeforeLoad;
        MatchReplayDisplay replayDisplay;
        MatchReplayStateDisplay replayStateDisplay;
        Camera replayPresentationCamera;
        MatchReplayPresentation.View replaySavedView;
        float replaySavedAspect;
        RectTransform replayInningRoot;
        Assets.Scripts.Sight.Surface.Pavilion.InningScoreView replayInningView;
        MatchReplayInnings replayInnings;
        void BuildReplayInnings(){
            replayInnings=new MatchReplayInnings(replayPresentation);
            if(!replayInnings.Available||!replayPanel)return;
            replayInningRoot=new GameObject("Replay Inning Controls",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster)).GetComponent<RectTransform>();
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(replayInningRoot.gameObject,gameObject.scene);replayInningRoot.anchorMin=replayInningRoot.anchorMax=Vector2.one*.5f;replayInningRoot.pivot=Vector2.one*.5f;
            var canvas=replayInningRoot.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.overrideSorting=true;canvas.sortingOrder=32002;
            var scaler=replayInningRoot.gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            if(replayStateDisplay!=null)replayStateDisplay.AdditionalControls=replayInningRoot;
            if(replayDisplay!=null)replayDisplay.AdditionalControls=replayInningRoot;
            replayInningView=Assets.Scripts.Sight.Surface.Pavilion.InningScoreView.Install(replayInningRoot,()=>replayInnings.Scores,seat=>replayInnings.Names[seat],()=>replayPresentation.localSeat);
            replayInningView.SetReplayNavigation((seat,column)=>{
                var visit=replayInnings.Find(seat,column);if(visit==null)return;
                replayPlaying=false;replayClock=0;ReplayFrameIndex=visit.frame;ApplyReplayFrame(replayFrames[ReplayFrameIndex]);
            },(seat,column)=>replayInnings.Find(seat,column)!=null,(seat,column)=>{var visit=replayInnings.At(ReplayFrameIndex);return visit!=null&&visit.seat==seat&&visit.column==column;});
            if (IsMatchReplayBrowser) { replayInningView.SetReplayTopLayout(); replayInningView.SetOpen(matchReplayJumpShown); }
            LayoutReplayInnings();
        }
        void LayoutReplayInnings(){
            if(!replayInningRoot||!replayPanel)return;
            // Screen-space root has its own raycaster and viewport; toolbar movement cannot clip it.
            replayInningView.RefreshReplaySelection();
        }
        void EnsureReplayControlCanvas()
        {
            if (!replayPanel) return;
            // Unity's missing/destroyed component wrapper is not necessarily CLR null.
            // Use Unity object validity rather than ?? before accessing native properties.
            var canvas = replayPanel.GetComponent<Canvas>();
            if (!canvas) canvas = replayPanel.gameObject.AddComponent<Canvas>();
            canvas.enabled = true;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32001;
            var raycaster = replayPanel.GetComponent<GraphicRaycaster>();
            if (!raycaster) raycaster = replayPanel.gameObject.AddComponent<GraphicRaycaster>();
            raycaster.enabled = true;
        }
        void BeginRecordedPresentation()
        {
            if(replayPresentation==null||replayDisplay!=null||replayStateDisplay!=null)return;
            EnsureReplayControlCanvas();
            if(replayPresentation.presentationVersion==MatchReplayStateCapture.Format||replayPresentation.presentationVersion=="state-events-1")replayStateDisplay=new MatchReplayStateDisplay(transform,replayPresentation,replayPanel);
            else replayDisplay=new MatchReplayDisplay(transform,replayPresentation,replayPanel);
            replayPresentationCamera=InputOutput.usedCamera?InputOutput.usedCamera:Camera.main;
            if(replayPresentationCamera){replaySavedView=MatchReplayPresentation.View.Capture(replayPresentationCamera);replaySavedAspect=replayPresentationCamera.aspect;}
            BuildMatchReplayEnvironment();
            BuildReplayInnings();
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
            replayDisplay?.Apply(ReplayFrameIndex);replayStateDisplay?.Apply(ReplayFrameIndex);ApplyMatchReplayTable();LayoutReplayInnings();
        }
        void StopRecordedPresentation(){if(replayTableBackground)replayTableBackground.sprite=replayOriginalTable;replayTableBackground=null;replayOriginalTable=null;replayTableKey=null;matchTrailShots.Clear();matchTrailPoints.Clear();matchTrailStart=-1;if(replayInningRoot)Destroy(replayInningRoot.gameObject);replayInningRoot=null;replayInningView=null;replayInnings=null;replayDisplay?.Dispose();replayDisplay=null;replayStateDisplay?.Dispose();replayStateDisplay=null;if(replayPresentationCamera){replaySavedView.Apply(replayPresentationCamera);replayPresentationCamera.aspect=replaySavedAspect;}replayPresentationCamera=null;}
    }
}
