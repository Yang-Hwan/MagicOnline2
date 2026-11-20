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
        StageCushionNumber guideMarker;
        RectTransform guideBadge;
        CanvasGroup guideOpacity;
        ParticleSystem guideLine;
        Vector2 guideCenter;
        int guideContact, guideNumber;
        StageMissions.DemoCushion[] demoCushions;
        int demoCushionCursor;
        float markerAge;
        TMP_Text stageMessage, stageActionLabel;
        Button stageAction;
        public bool IsStage => PracticeSceneFlow.SelectedMode == PracticeSceneFlow.Mode.Stage && IsLocalPractice;

        public async UniTask OpenStage()
        {
            ShotCtrl.canControl=false;
            ResetStageShotState();
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
                if(mission.scoreFrame>=replayFrames.Count || mission.first<0 || mission.first>=mission.scoreFrame)
                    throw new InvalidDataException();
                // Keep the recorded follow-through after scoring for the complete demonstration.
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
                // Mission contact guides describe only the path up to the scoring contact.
                var cuePositions=new Vector2[mission.scoreFrame-mission.first+1];
                for(int f=0;f<cuePositions.Length;f++)
                {
                    var p=replayFrames[f].positions[replayCueId];
                    cuePositions[f]=new Vector2((p.x+1.304f)/2.606f,(.473f-p.z)/1.265f);
                }
                demoCushions=StageMissions.FindDemoCushions(mission,cuePositions);
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
            demoCushionCursor=0;
            ClearStageCushion();
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
            ResetStageShotState();
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
            ResetStageShotState();
            stageShot=new StageMissions.Shot(mission);
            SetStagePhase(StagePhase.Shooting,"");
            return true;
        }
        void ResetStageShotState()
        {
            // Stage completion bypasses the normal match turn lifecycle, and PoolLogic
            // survives scene changes. Never inherit an interrupted shot or demo's contacts.
            PoolLogic.Instance.RessetTurnState();
            Assets.Scripts.Sight.Surface.Pavilion.CushionCountMotion.Hide();
            stageShot=null;
        }
        internal void RecordStageRail(int ballId,string rail)
        {
            // Table colliders use BoardT/B/L/R, the same names used by BallC's cushion callback.
            if(stagePhase!=StagePhase.Shooting || ballId!=replayCueId || string.IsNullOrEmpty(rail) ||
                rail.Length<=5 || "TBLR".IndexOf(rail[5])<0)return;
            int before=stageShot.Cushions;
            stageShot.Rail(rail[5]);
            RefreshStageRoute();
            if(stageShot.Cushions>before)
                Assets.Scripts.Sight.Surface.Pavilion.CushionCountMotion.Show(stageShot.Cushions);
            var side=rail[5]=='T'?CushionDir.T:rail[5]=='B'?CushionDir.B:rail[5]=='L'?CushionDir.L:CushionDir.R;
            LightStageCushion(side);
        }
        internal void RecordStageBall(int cueId,int targetId)
        {
            if(stagePhase!=StagePhase.Shooting || cueId!=replayCueId || stageShot.Decided) return;
            stageShot.Ball(targetId);
            RefreshStageRoute();
            if(stageShot.Decided) Assets.Scripts.Sight.Surface.Pavilion.CushionCountMotion.Hide();
            if(stageShot.Passed)
            {
                StageMissions.Win(stageAward);
                stageMessage.text="미션 성공 · 공이 멈추면 결과를 표시합니다";
            }
        }
        void FinishStageShot()
        {
            if(stagePhase!=StagePhase.Shooting) return;
            Assets.Scripts.Sight.Surface.Pavilion.CushionCountMotion.Hide();
            if(stageShot.Passed)
            {
                SetStagePhase(StagePhase.Won,"성공!   ★ "+StageMissions.Stars(stageAward),
                    "다시하기");
            }
            else if(stageAward==1) ExhaustStage();
            else SetStagePhase(StagePhase.Failed,(stageShot.Scored?"득점 · 미션 순서 불일치":"미션 실패")+" · 남은 별 "+StageMissions.Stars(stageAward-1),"다시하기");
        }
        void ExhaustStage() => SetStagePhase(StagePhase.Exhausted,"도전 기회를 모두 사용했습니다.","15초 대기 후 충전");
        void SetStagePhase(StagePhase phase,string message,string action=null)
        {
            if(phase!=StagePhase.Guide && phase!=StagePhase.Demo)ClearStageCushion();
            stagePhase=phase;
            ShotCtrl.canControl=phase==StagePhase.Aim;
            stageMessage.text=message;
            stageAction.gameObject.SetActive(action!=null);
            stageActionLabel.text=action;
            RefreshStageComicUI();
        }
        void OnStageAction()
        {
            if(stageModalDismissed){RefreshStageComicUI();return;}
            switch(stagePhase)
            {
                case StagePhase.Aim:
                    if(!shotController.IsShotAnimating) PlayStageDemo();
                    break;
                case StagePhase.Failed: AimStage(); break;
                case StagePhase.Won: AimStage(); break;
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
            markerAge = 0;
            ClearStageCushion();
        }
        void NextGuideCushion()
        {
            do { guideContact++; }
            while (guideContact < mission.order.Length && mission.order[guideContact] == 'O');
            if (guideContact == mission.order.Length)
            {
                guideBadge.gameObject.SetActive(false);
                AimStage();
                return;
            }
            ShowStageCushion(mission.order[guideContact],++guideNumber);
            RefreshStageRoute();
        }
        void ClearStageCushion()
        {
            if(guideLine)guideLine.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            guideLine=null;
            if(guideBadge)guideBadge.gameObject.SetActive(false);
        }
        void LightStageCushion(CushionDir side)
        {
            if(guideLine)guideLine.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            guideLine=cushionHit.PlayLine(side,true);
        }
        void ShowStageCushion(char contact,int number)
        {
            ClearStageCushion();
            markerAge=0;
            CushionDir side;
            switch (contact)
            {
                case 'T': side = CushionDir.T; guideCenter = new Vector2(.5f,.065f); break;
                case 'B': side = CushionDir.B; guideCenter = new Vector2(.5f,.935f); break;
                case 'L': side = CushionDir.L; guideCenter = new Vector2(.035f,.5f); break;
                case 'R': side = CushionDir.R; guideCenter = new Vector2(.965f,.5f); break;
                default: throw new InvalidDataException("Unknown stage cushion");
            }
            // Both numbered introduction and recorded demo contacts illuminate their cushion.
            LightStageCushion(side);
            guideMarker.SetNumber(number);
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
                // The first second after Start is an input-locked pause without a light or badge.
                else if(guideContact>=0) DrawGuideMarker();
            }
            else if(stagePhase==StagePhase.Demo)
            {
                markerAge+=Time.unscaledDeltaTime;
                if(guideBadge.gameObject.activeSelf)
                {
                    if(markerAge>=1f)guideBadge.gameObject.SetActive(false);
                    else DrawGuideMarker();
                }
                replayClock+=Time.unscaledDeltaTime;
                int advance=Mathf.FloorToInt(replayClock/replayStep);
                if(advance<=0) return;
                replayClock-=advance*replayStep;
                ReplayFrameIndex=Mathf.Min(ReplayFrameIndex+advance,replayFrames.Count-1);
                ApplyReplayFrame(replayFrames[ReplayFrameIndex]);
                while(demoCushionCursor<demoCushions.Length && demoCushions[demoCushionCursor].frame<=ReplayFrameIndex)
                {
                    var contact=demoCushions[demoCushionCursor++];
                    ShowStageCushion(contact.side,contact.number);
                    RefreshStageRoute();
                }
                if(ReplayFrameIndex==replayFrames.Count-1) AimStage();
            }
            else if(stagePhase==StagePhase.Waiting)
            {
                penaltySeconds-=Mathf.Min(Time.unscaledDeltaTime,.25f);
                stageMessage.text=$"기회 충전 중 · {Mathf.Max(0,Mathf.CeilToInt(penaltySeconds))}초";
                if(resultDetail)resultDetail.text=stageMessage.text;
                if(penaltySeconds<=0) { StageMissions.Refill(); AimStage(); }
            }
        }
        void CreateStageBar()
        {
            BuildStageComicUI();
            var overlay = stageBar.parent;
            var badge = new GameObject("Stage Cushion Badge",typeof(RectTransform),typeof(CanvasGroup));
            guideBadge = (RectTransform)badge.transform;
            guideBadge.SetParent(overlay,false);
            guideBadge.sizeDelta = new Vector2(88,88);
            guideOpacity = badge.GetComponent<CanvasGroup>();
            guideOpacity.blocksRaycasts = false;
            guideMarker = StageNode(guideBadge,"Stage Cushion Number",Vector2.zero,new Vector2(88,88))
                .gameObject.AddComponent<StageCushionNumber>();
            guideBadge.gameObject.SetActive(false);
        }
    }

}
