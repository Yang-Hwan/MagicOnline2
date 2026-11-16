using System.IO;
using System.Text;
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
        enum StagePhase { Loading, Demo, Aim, Shooting, Failed, Won, Exhausted, Waiting, Error }
        StagePhase stagePhase;
        StageMissions.Mission mission;
        readonly StringBuilder stageContacts = new StringBuilder();
        int stageAward;
        float penaltySeconds;
        RectTransform stageBar;
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
            else PlayStageDemo();
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
                labels[i]=order[i]=='T'?"위":order[i]=='B'?"아래":order[i]=='L'?"왼쪽":"오른쪽";
            return string.Join(" → ",labels);
        }
        void AimStage()
        {
            StopReplayView();
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
            PoolLogic.Instance.ShotEnded();
            PoolPlayer.SetTurn(replayCueId);
            PoolCoach.Instance.RestorePracticeTurn(0,30);
            shotController.cueVertical.gameObject.SetActive(true);
            stageContacts.Clear();
            SetStagePhase(StagePhase.Aim,"★ "+StageMissions.Stars(6-StageMissions.Spent(StageMissions.Current))+"   "+StageDirections(mission.order),"시범 다시 보기");
        }
        bool BeginStageShot()
        {
            if(stagePhase!=StagePhase.Aim) return false;
            stageAward=StageMissions.BeginAttempt();
            if(stageAward<=0) { ExhaustStage(); return false; }
            SetStagePhase(StagePhase.Shooting,"샷 판정 중");
            return true;
        }
        internal void RecordStageRail(int ballId,string rail)
        {
            // Table colliders use BoardT/B/L/R, the same names used by BallC's cushion callback.
            if(stagePhase==StagePhase.Shooting && ballId==replayCueId && rail.Length>5 && "TBLR".IndexOf(rail[5])>=0)
                stageContacts.Append(rail[5]);
        }
        void FinishStageShot()
        {
            if(stagePhase!=StagePhase.Shooting) return;
            if(stageContacts.ToString()==mission.order)
            {
                StageMissions.Win(stageAward);
                SetStagePhase(StagePhase.Won,"성공!   ★ "+StageMissions.Stars(stageAward),
                    StageMissions.Current<StageMissions.Count?"다음 단계":"목록으로");
            }
            else if(stageAward==1) ExhaustStage();
            else SetStagePhase(StagePhase.Failed,"순서 불일치 · 남은 별 "+StageMissions.Stars(stageAward-1),"다시 도전");
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
        void UpdateStage()
        {
            if(stagePhase==StagePhase.Demo)
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
                if(penaltySeconds<=0) { StageMissions.Refill(); PlayStageDemo(); }
            }
        }
        void CreateStageBar()
        {
            var go=new GameObject("Stage Mission",typeof(RectTransform),typeof(Image));
            stageBar=(RectTransform)go.transform;
            stageBar.SetParent(undoButtonRect.parent,false);
            stageBar.anchorMin=stageBar.anchorMax=stageBar.pivot=new Vector2(.5f,0);
            stageBar.anchoredPosition=new Vector2(0,12); stageBar.sizeDelta=new Vector2(1120,66);
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
            Button Control(string name,float x,float width,UnityEngine.Events.UnityAction click)
            {
                var button=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button)).GetComponent<Button>();
                button.transform.SetParent(stageBar,false);
                var rect=(RectTransform)button.transform;rect.anchoredPosition=new Vector2(x,0);rect.sizeDelta=new Vector2(width,52);
                button.GetComponent<Image>().color=new Color(.19f,.23f,.27f);
                button.onClick.AddListener(click);
                return button;
            }
            stageMessage=Label(stageBar,-148,792);
            stageAction=Control("StageAction",335,215,OnStageAction);
            stageActionLabel=Label(stageAction.transform,0,211);
            var back=Control("StageList",497,109,PracticeSceneFlow.ReturnToMenu);
            Label(back.transform,0,105).text="목록";
        }
    }
}
