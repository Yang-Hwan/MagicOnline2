using UnityEngine;
using UnityEngine.UI;
using Assets.Scripts.Often;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{
    public partial class PhysicsMng
    {
        const string StageReplayFolderId = "stage-reference";
        ScrollRect stageReplayScroll;
        TMPro.TMP_Text stageReplayHint;
        void UpdateStageReplaySlotLayout()
        {
            if (replaySlotButtons == null) return;
            if (!stageReplayScroll)
            {
                var viewport = new GameObject("Replay Slots Scroll", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect)).GetComponent<RectTransform>();
                viewport.SetParent(replayPanel, false);
                viewport.GetComponent<Image>().color = new Color(0,0,0,.15f);
                stageReplayScroll = viewport.GetComponent<ScrollRect>();
                var content = new GameObject("Slots", typeof(RectTransform)).GetComponent<RectTransform>();
                content.SetParent(viewport, false);
                content.anchorMin = content.anchorMax = content.pivot = new Vector2(0,.5f);
                stageReplayScroll.viewport = viewport; stageReplayScroll.content = content;
                stageReplayScroll.horizontal = true; stageReplayScroll.vertical = false;
                stageReplayScroll.movementType = ScrollRect.MovementType.Clamped;
                stageReplayScroll.scrollSensitivity = 50;
                foreach (var button in replaySlotButtons) button.transform.SetParent(content,false);
            }
            var view = (RectTransform)stageReplayScroll.transform;
            var parent = IsReplayBrowser && matchReplayList ? matchReplayList : replayPanel;
            if (view.parent != parent) view.SetParent(parent,false);
            view.anchoredPosition = new Vector2(0,-4); view.sizeDelta = new Vector2(680,90);
            if (!stageReplayHint)
            {
                stageReplayHint = CreateReplayControlLabel("",new Vector2(-100,-80),new Vector2(450,32));
                stageReplayHint.fontSize=15;
            }
            if(stageReplayHint.transform.parent!=parent)stageReplayHint.transform.SetParent(parent,false);
            stageReplayHint.text=(selectedReplayFolder==StageReplayFolderId ? "100칸 · " : "") + "좌우 스크롤 / 기록을 길게 눌러 순서 이동";
            stageReplayScroll.content.sizeDelta = new Vector2(Mathf.Max(680,ActiveReplaySlotCount/2*68),84);
            for(int i=0;i<replaySlotButtons.Length;i++)
            {
                var rect=(RectTransform)replaySlotButtons[i].transform;
                rect.anchorMin=rect.anchorMax=new Vector2(0,.5f);
                rect.anchoredPosition=new Vector2(34+i/2*68,21-i%2*42);
            }
        }
        bool CanMoveStageReplaySlot(int slot) => !IsMatchReplayLayout && ReplaySlotReady && slot >= 0 && slot < ActiveReplaySlotCount && replaySlotFilled[slot];
        public void MoveStageReplaySlot(int from,int to)
        {
            if (!CanMoveStageReplaySlot(from) || to<0 || to>=ActiveReplaySlotCount || from==to) return;
            CommitReplayTitle(); StopReplayView();
            try
            {
                ReplaySlotOrder.Move(ReplaySlotDirectory,from,to,ActiveReplaySlotCount);
                selectedReplaySlot=to; replayTitleSlot=-1;
                if(replayLoadMode){replayFrames.Clear();replayStroke=default;ReplayFrameIndex=0;}
                RefreshReplaySlots(); UpdateReplayTitle();
                replaySlotMessage=$"{from+1}번 → {to+1}번 이동 · 중간 기록의 순서도 변경했습니다";
            }
            catch(System.Exception e) when(e is System.IO.IOException || e is System.UnauthorizedAccessException)
            { replaySlotMessage="이동 실패 · 원본 백업에서 복구 후 다시 시도하세요"; Debug.LogException(e); }
        }
    }
}
