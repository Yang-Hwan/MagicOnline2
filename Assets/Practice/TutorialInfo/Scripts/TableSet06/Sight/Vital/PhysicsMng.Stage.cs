using System.IO;
using Assets.Scripts.Often;
using Assets.TutorialInfo.Scripts.TableSet06.Excert.Match;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{
    public partial class PhysicsMng
    {
        enum StagePhase { Loading, Intro, Guide, Demo, Aim, Shooting, Failed, Won, Exhausted, Waiting, Error }
        StagePhase stagePhase;
        StageMissions.Mission mission;
        StageMissions.Shot stageShot;
        int stageAward;
        float penaltySeconds;
        RectTransform stageBar, introCard;
        TMP_Text guideMarker;
        RectTransform guideBadge;
        CanvasGroup guideOpacity;
        ParticleSystem guideLine;
        Vector2 guideCenter;
        int guideContact, guideNumber;
        float markerAge;
        TMP_Text stageMessage, stageActionLabel;
        Button stageAction;
        public bool IsStage => PracticeSceneFlow.SelectedMode == PracticeSceneFlow.Mode.Stage && IsLocalPractice;

        public async UniTask OpenStage()
        {
            ShotCtrl.canControl=false;
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate,this.GetCancellationTokenOnDestroy());
            inMove=false;
            foreach(var ball in ballcs)
            {
                ball.body.linearVelocity=Vector3.zero;
                ball.body.angularVelocity=Vector3.zero;
                ball.body.Sleep();
            }
            undoButton.gameObject.SetActive(false);
            optionsRect.gameObject.SetActive(false);
            CreateStageBar();
            mission=StageMissions.Get(StageMissions.Current);
            var source=Resources.Load<TextAsset>(mission.source);
            if (!source) { SetStagePhase(StagePhase.Error,"시범 기록을 찾을 수 없습니다."); return; }
            replayFrames.Clear();
            try
            {
                using(var reader=new BinaryReader(new MemoryStream(source.bytes,false)))
                    ReadReplay(reader,replayFrames,out replayCueId,out replayStep,out replayStroke);
                int end=mission.scoreFrame+1;
                if(end>replayFrames.Count || mission.first>=mission.scoreFrame) throw new InvalidDataException();
                replayFrames.RemoveRange(end,replayFrames.Count-end);
                if(mission.first>0) replayFrames.RemoveRange(0,mission.first);
                foreach(var frame in replayFrames)
                {
                    for(int i=0;i<frame.positions.Length;i++)
                    {
                        if(mission.mirrorX) frame.positions[i].x=-.002f-frame.positions[i].x;
                        if(mission.mirrorZ) frame.positions[i].z=-.319f-frame.positions[i].z;
                        var rotation=frame.rotations[i];
                        if(mission.mirrorX) { rotation.y=-rotation.y; rotation.z=-rotation.z; }
                        if(mission.mirrorZ) { rotation.x=-rotation.x; rotation.y=-rotation.y; }
                        frame.rotations[i]=rotation;
                    }
                    if(mission.mirrorX || mission.mirrorZ) frame.cueRecorded=false;
                }
                if(mission.mirrorX || mission.mirrorZ) replayStroke=default;
            }
            catch(System.Exception error) when(error is IOException || error is InvalidDataException)
            {
                SetStagePhase(StagePhase.Error,"시범 기록을 읽을 수 없습니다.");
                return;
            }
            if(StageMissions.Spent(StageMissions.Current)>=StageMissions.Chances) ExhaustStage();
            else
            {
                PlaceStageBalls();
                shotController.cueVertical.gameObject.SetActive(false);
                introCard.gameObject.SetActive(true);
                SetStagePhase(StagePhase.Intro,StageDirections(mission.order));
                PositionOnTable(introCard,new Vector2(.5f,.5f));
            }
        }
        void PlayStageDemo()
        {
            ResetPracticeReplay();
            if(!IsPracticeReplay) { SetStagePhase(StagePhase.Error,"시범을 시작할 수 없습니다."); return; }
            replayPlaying=true;
            SetStagePhase(StagePhase.Demo,"시범 리플레이 · "+StageDirections(mission.order));
        }
        static string StageDirections(string order)
        {
            var labels=new string[order.Length];
            for(int i=0;i<labels.Length;i++)
                labels[i]=order[i]=='O'?"적구":order[i]=='T'?"위":order[i]=='B'?"아래":order[i]=='L'?"왼쪽":"오른쪽";
            return string.Join(" → ",labels);
        }
        void PlaceStageBalls()
        {
            var start=replayFrames[0];
            ApplyReplayFrame(start);
            foreach(var ball in ballcs)
            {
                ball.body.linearVelocity=Vector3.zero;
                ball.body.angularVelocity=Vector3.zero;
                ball.body.Sleep();
                ball.OnState(BallState.SetState);
            }
            line.positionCount=0;
        }
        void AimStage()
        {
            StopReplayView();
            PlaceStageBalls();
            PoolLogic.Instance.ShotEnded();
            PoolPlayer.SetTurn(replayCueId);
            PoolCoach.Instance.RestorePracticeTurn(0,30);
            shotController.cueVertical.gameObject.SetActive(true);
            SetStagePhase(StagePhase.Aim,"★ "+StageMissions.Stars(6-StageMissions.Spent(StageMissions.Current))+"   "+StageDirections(mission.order),"시범 보기");
        }
        bool BeginStageShot()
        {
            if(stagePhase!=StagePhase.Aim) return false;
            stageAward=StageMissions.BeginAttempt();
            if(stageAward<=0) { ExhaustStage(); return false; }
            stageShot=new StageMissions.Shot(mission);
            SetStagePhase(StagePhase.Shooting,"샷 판정 중");
            return true;
        }
        internal void RecordStageRail(int ballId,string rail)
        {
            // Table colliders use BoardT/B/L/R, the same names used by BallC's cushion callback.
            if(stagePhase==StagePhase.Shooting && ballId==replayCueId && rail.Length>5 && "TBLR".IndexOf(rail[5])>=0)
                stageShot.Rail(rail[5]);
        }
        internal void RecordStageBall(int cueId,int targetId)
        {
            if(stagePhase!=StagePhase.Shooting || cueId!=replayCueId || stageShot.Decided) return;
            stageShot.Ball(targetId);
            if(stageShot.Passed)
            {
                StageMissions.Win(stageAward);
                stageMessage.text="미션 성공!   ★ "+StageMissions.Stars(stageAward);
            }
        }
        void FinishStageShot()
        {
            if(stagePhase!=StagePhase.Shooting) return;
            if(stageShot.Passed)
            {
                SetStagePhase(StagePhase.Won,"성공!   ★ "+StageMissions.Stars(stageAward),
                    StageMissions.Current<StageMissions.Count?"다음 단계":"목록으로");
            }
            else if(stageAward==1) ExhaustStage();
            else SetStagePhase(StagePhase.Failed,(stageShot.Scored?"득점 · 미션 순서 불일치":"미션 실패")+" · 남은 별 "+StageMissions.Stars(stageAward-1),"다시 도전");
        }
        void ExhaustStage() => SetStagePhase(StagePhase.Exhausted,"도전 기회를 모두 사용했습니다.","15초 대기 후 충전");
        void SetStagePhase(StagePhase phase,string message,string action=null)
        {
            stagePhase=phase;
            ShotCtrl.canControl=phase==StagePhase.Aim;
            stageMessage.text=$"스테이지 {StageMissions.Current:00}    {message}";
            stageAction.gameObject.SetActive(action!=null);
            stageActionLabel.text=action;
        }
        void OnStageAction()
        {
            switch(stagePhase)
            {
                case StagePhase.Aim:
                    if(!shotController.IsShotAnimating) PlayStageDemo();
                    break;
                case StagePhase.Failed: AimStage(); break;
                case StagePhase.Won:
                    if(StageMissions.Current==StageMissions.Count) PracticeSceneFlow.ReturnToMenu();
                    else StageMissions.Enter(StageMissions.Current+1);
                    break;
                case StagePhase.Exhausted:
                    penaltySeconds=15;
                    SetStagePhase(StagePhase.Waiting,"기회 충전 중 · 15초");
                    break;
            }
        }
        void PositionOnTable(RectTransform rect, Vector2 point)
        {
            var world = new Vector3(Mathf.Lerp(-1.304f,1.302f,point.x),
                ballcs[replayCueId].body.position.y, Mathf.Lerp(.473f,-.792f,point.y));
            var screen = PlacementCamera.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)rect.parent,screen,null,out var local);
            rect.anchoredPosition = local;
        }
        void StartStageGuide()
        {
            if (stagePhase != StagePhase.Intro) return;
            introCard.gameObject.SetActive(false);
            SetStagePhase(StagePhase.Guide,StageDirections(mission.order));
            guideContact = -1;
            guideNumber = 0;
            NextGuideCushion();
        }
        void NextGuideCushion()
        {
            if (guideLine)
                guideLine.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            do { guideContact++; }
            while (guideContact < mission.order.Length && mission.order[guideContact] == 'O');
            if (guideContact == mission.order.Length)
            {
                guideBadge.gameObject.SetActive(false);
                AimStage();
                return;
            }
            markerAge = 0;
            CushionDir side;
            switch (mission.order[guideContact])
            {
                case 'T': side = CushionDir.T; guideCenter = new Vector2(.5f,.065f); break;
                case 'B': side = CushionDir.B; guideCenter = new Vector2(.5f,.935f); break;
                case 'L': side = CushionDir.L; guideCenter = new Vector2(.035f,.5f); break;
                case 'R': side = CushionDir.R; guideCenter = new Vector2(.965f,.5f); break;
                default: throw new InvalidDataException("Unknown stage cushion");
            }
            guideLine = cushionHit.PlayLine(side);
            guideMarker.text = (++guideNumber).ToString();
            guideBadge.gameObject.SetActive(true);
            DrawGuideMarker();
        }
        void DrawGuideMarker()
        {
            PositionOnTable(guideBadge,guideCenter);
            float scale = markerAge < .12f
                ? Mathf.SmoothStep(.78f,1.12f,markerAge/.12f)
                : Mathf.SmoothStep(1.12f,1f,(markerAge-.12f)/.15f);
            guideBadge.localScale = Vector3.one * scale;
            guideOpacity.alpha = 1f - Mathf.Clamp01((markerAge-.88f)/.12f);
        }
        void UpdateStage()
        {
            if(stagePhase==StagePhase.Intro)
                PositionOnTable(introCard,new Vector2(.5f,.5f));
            else if(stagePhase==StagePhase.Guide)
            {
                markerAge += Time.unscaledDeltaTime;
                if(markerAge >= 1f) NextGuideCushion();
                else DrawGuideMarker();
            }
            else if(stagePhase==StagePhase.Demo)
            {
                replayClock+=Time.unscaledDeltaTime;
                int advance=Mathf.FloorToInt(replayClock/replayStep);
                if(advance<=0) return;
                replayClock-=advance*replayStep;
                ReplayFrameIndex=Mathf.Min(ReplayFrameIndex+advance,replayFrames.Count-1);
                ApplyReplayFrame(replayFrames[ReplayFrameIndex]);
                if(ReplayFrameIndex==replayFrames.Count-1) AimStage();
            }
            else if(stagePhase==StagePhase.Waiting)
            {
                penaltySeconds-=Mathf.Min(Time.unscaledDeltaTime,.25f);
                stageMessage.text=$"기회 충전 중 · {Mathf.Max(0,Mathf.CeilToInt(penaltySeconds))}초";
                if(penaltySeconds<=0) { StageMissions.Refill(); AimStage(); }
            }
        }
        void CreateStageBar()
        {
            var go=new GameObject("Stage Mission",typeof(RectTransform),typeof(Image));
            stageBar=(RectTransform)go.transform;
            stageBar.SetParent(undoButtonRect.parent,false);
            stageBar.anchorMin=stageBar.anchorMax=stageBar.pivot=new Vector2(.5f,1);
            stageBar.anchoredPosition=new Vector2(0,-12); stageBar.sizeDelta=new Vector2(1120,66);
            go.GetComponent<Image>().color=new Color(.035f,.045f,.055f,.97f);
            TMP_Text Label(Transform parent,float x,float width)
            {
                var label=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                label.transform.SetParent(parent,false);
                label.rectTransform.anchoredPosition=new Vector2(x,0);label.rectTransform.sizeDelta=new Vector2(width,58);
                label.font=undoButton.GetComponentInChildren<TMP_Text>(true).font;
                label.alignment=TextAlignmentOptions.Center;label.fontSize=26;label.enableAutoSizing=true;
                label.fontSizeMin=16; label.fontSizeMax=26;label.raycastTarget=false;
                return label;
            }
            Button Control(Transform parent,string name,float x,float width,UnityEngine.Events.UnityAction click)
            {
                var button=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button)).GetComponent<Button>();
                button.transform.SetParent(parent,false);
                var rect=(RectTransform)button.transform;rect.anchoredPosition=new Vector2(x,0);rect.sizeDelta=new Vector2(width,52);
                button.GetComponent<Image>().color=new Color(.19f,.23f,.27f);
                button.onClick.AddListener(click);
                return button;
            }
            stageMessage=Label(stageBar,-148,792);
            stageAction=Control(stageBar,"StageAction",335,215,OnStageAction);
            stageActionLabel=Label(stageAction.transform,0,211);
            var back=Control(stageBar,"StageList",497,109,PracticeSceneFlow.ReturnToMenu);
            Label(back.transform,0,105).text="목록";

            var overlay = stageBar.parent;
            var card = new GameObject("Stage Intro",typeof(RectTransform),typeof(Image));
            introCard = (RectTransform)card.transform;
            introCard.SetParent(overlay,false);
            introCard.sizeDelta = new Vector2(240,196);
            card.GetComponent<Image>().color = new Color(.035f,.045f,.055f,.94f);
            var title = Label(introCard,0,220);
            title.text = "스테이지";
            title.rectTransform.anchoredPosition = new Vector2(0,65);
            var number = Label(introCard,0,220);
            number.text = StageMissions.Current.ToString("00");
            number.enableAutoSizing = false;
            number.fontSize = 48;
            number.rectTransform.anchoredPosition = new Vector2(0,10);
            var start = Control(introCard,"StageStart",0,96,StartStageGuide);
            ((RectTransform)start.transform).anchoredPosition = new Vector2(0,-58);
            Label(start.transform,0,90).text = ">";
            introCard.gameObject.SetActive(false);

            var badge = new GameObject("Stage Cushion Badge",typeof(RectTransform),typeof(CanvasGroup),typeof(StageCushionBadge));
            guideBadge = (RectTransform)badge.transform;
            guideBadge.SetParent(overlay,false);
            guideBadge.sizeDelta = new Vector2(88,88);
            guideOpacity = badge.GetComponent<CanvasGroup>();
            guideOpacity.blocksRaycasts = false;
            badge.GetComponent<StageCushionBadge>().raycastTarget = false;
            guideMarker = Label(guideBadge,0,78);
            guideMarker.rectTransform.sizeDelta = new Vector2(78,80);
            guideMarker.name = "Stage Cushion Number";
            guideMarker.enableAutoSizing = false;
            guideMarker.fontSize = 54;
            guideMarker.fontStyle = FontStyles.Bold;
            guideMarker.color = Color.white;
            guideMarker.outlineColor = new Color32(26,37,33,255);
            guideMarker.outlineWidth = .24f;
            guideBadge.gameObject.SetActive(false);
        }
    }

    public sealed class StageCushionBadge : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            const int segments = 64;
            float radius = rectTransform.rect.width * .5f;
            int previous = -1;
            void Ring(float fraction, Color32 color)
            {
                int first = mesh.currentVertCount;
                for (int i = 0; i < segments; i++)
                {
                    float angle = i * (2f * Mathf.PI / segments);
                    mesh.AddVert(new Vector3(Mathf.Cos(angle),Mathf.Sin(angle)) * (radius*fraction),color,Vector2.zero);
                }
                if (previous >= 0)
                    for (int i = 0; i < segments; i++)
                    {
                        int j = (i+1) % segments;
                        mesh.AddTriangle(previous+i,previous+j,first+j);
                        mesh.AddTriangle(previous+i,first+j,first+i);
                    }
                previous = first;
            }
            Ring(1f,new Color32(255,198,65,0));
            Ring(.82f,new Color32(255,204,76,140));
            Ring(.81f,new Color32(255,243,177,255));
            Ring(.72f,new Color32(255,189,56,255));
            Ring(.71f,new Color32(15,33,42,255));
            int center = mesh.currentVertCount;
            mesh.AddVert(Vector3.zero,new Color32(30,61,72,255),Vector2.zero);
            for (int i=0;i<segments;i++) mesh.AddTriangle(previous+i,previous+(i+1)%segments,center);
        }
    }
}
