using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Assets.Scripts.Often;
namespace Assets.Scripts.Sight.Surface.Consist
{
    public sealed partial class BottomMenuView
    {
        int practiceBallSelection;
        bool? practiceComputerSelection;
        Button practiceStart;
        readonly GameObject[] practiceHighlights=new GameObject[4];
        public int PracticeBallSelection=>practiceBallSelection;
        public bool? PracticeComputerSelection=>practiceComputerSelection;
        public bool PracticeSelectionReady=>practiceBallSelection!=0&&practiceComputerSelection.HasValue;
        void BuildPracticeMenu()
        {
            // Only this page changes: shared live header/footer remain above the art.
            var artwork=Resources.Load<Texture2D>("Menu/single-three");
            if(artwork)backplate.texture=artwork;
            UnityEngine.Events.UnityAction[] actions={OpenPracticeSettings,OpenStages,()=>PracticeSceneFlow.Enter(PracticeSceneFlow.Mode.Replay)};
            Rect[] cards={new Rect(26,248,535,558),new Rect(575,248,515,558),new Rect(1097,248,538,558)};
            for(int i=0;i<cards.Length;i++) {
                var r=cards[i];Hit("Single"+i,body,r.x,r.y,r.width,r.height,actions[i]);
            }
        }
        public void OpenPracticeSettings()
        {
            CloseModal();practiceBallSelection=4;practiceComputerSelection=false;
            GameAudio.Play(SoundCue.Open,.4f);
            modal=Node("Practice Settings Modal",design,0,0,Width,Height);
            var shade=Box("Shade",modal,0,0,Width,Height,new Color(0,0,0,.90f));shade.raycastTarget=true;
            const float zoom=.88f;const float cropX=158,cropY=45,cropW=1215,cropH=904;
            var panel=Node("Practice Settings Panel",modal,(Width-cropW*zoom)/2,(Height-cropH*zoom)/2-12,cropW*zoom,cropH*zoom);
            var art=Art("Practice settings artwork",panel,Resources.Load<Texture2D>("PracticeMenu/PracticeSettings"),0,0,cropW*zoom,cropH*zoom);
            art.uvRect=new Rect(cropX/1536,1-(cropY+cropH)/1024,cropW/1536,cropH/1024);
            ModalBackdrop.Attach(shade,panel,CloseModal);shade.color=new Color(0,0,0,.90f);
            Rect[] choices={new Rect(446,252,385,211),new Rect(852,252,405,211),new Rect(444,490,387,210),new Rect(848,490,414,210)};
            for(int i=0;i<4;i++) {
                int selected=i;var r=choices[i];
                var hit=Hit("Practice option "+i,panel,(r.x-cropX)*zoom,(r.y-cropY)*zoom,r.width*zoom,r.height*zoom,()=>SelectPracticeOption(selected));
                float cw=r.width*zoom,ch=r.height*zoom;
                var highlight=Node("Selected",hit.transform,0,0,cw,ch);
                Box("Top",highlight,18,0,cw-36,5,Yellow);
                Box("Bottom",highlight,18,ch-5,cw-36,5,Yellow);
                Box("Left",highlight,0,18,5,ch-36,Yellow);
                Box("Right",highlight,cw-5,18,5,ch-36,Yellow);
                Box("Check badge",highlight,cw-50,6,42,36,Yellow);
                PracticeCheckStroke(highlight,new Vector2(cw-43,21),new Vector2(cw-33,30));
                PracticeCheckStroke(highlight,new Vector2(cw-33,30),new Vector2(cw-16,12));
                practiceHighlights[i]=highlight.gameObject;highlight.gameObject.SetActive(false);
            }
            var start=new Rect(447,730,615,173);
            practiceStart=Hit("Start Practice",panel,(start.x-cropX)*zoom,(start.y-cropY)*zoom,start.width*zoom,start.height*zoom,StartSelectedPractice);
            Text("Dismiss hint",modal,"바깥 영역을 누르면 닫힙니다",400,Height-40,872,32,23,Color.white);
            RefreshPracticeOptions();
        }
        static void PracticeCheckStroke(Transform parent,Vector2 a,Vector2 b)
        {
            var rect=Box("Check stroke",parent,0,0,1,1,Color.black).rectTransform;
            rect.pivot=Vector2.one*.5f;rect.anchoredPosition=new Vector2((a.x+b.x)*.5f,-(a.y+b.y)*.5f);
            rect.sizeDelta=new Vector2(Vector2.Distance(a,b),6);
            rect.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(a.y-b.y,b.x-a.x)*Mathf.Rad2Deg);
        }
        public void SelectPracticeOption(int index)
        {
            if(index<0||index>3)throw new ArgumentOutOfRangeException(nameof(index));
            if(index<2)practiceBallSelection=index==0?3:4;else practiceComputerSelection=index==3;
            GameAudio.Play(SoundCue.Click,.45f);RefreshPracticeOptions();
        }
        void RefreshPracticeOptions()
        {
            for(int i=0;i<4;i++)if(practiceHighlights[i])practiceHighlights[i].SetActive(
                i<2?practiceBallSelection==(i==0?3:4):practiceComputerSelection.HasValue&&practiceComputerSelection.Value==(i==3));
            if(practiceStart){practiceStart.interactable=PracticeSelectionReady;
                var colors=practiceStart.colors;colors.disabledColor=new Color(0,0,0,.65f);practiceStart.colors=colors;}
        }
        void StartSelectedPractice()
        {
            if(!PracticeSelectionReady)return;
            int balls=practiceBallSelection;bool computer=practiceComputerSelection.Value;
            CloseModal();PracticeSceneFlow.Enter(balls==3?PracticeSceneFlow.Mode.ThreeBall:PracticeSceneFlow.Mode.FourBall,computer);
        }
    }
}
