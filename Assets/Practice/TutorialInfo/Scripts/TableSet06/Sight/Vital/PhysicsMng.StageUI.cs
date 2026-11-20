using System.Collections.Generic;
using Assets.Scripts.Often;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{
    public partial class PhysicsMng
    {
        RectTransform stageShade, resultCard;
        bool stageModalDismissed;
        TMP_Text resultDetail, resultRetryLabel;
        Button resultRetry;
        Image resultArtwork;
        StageStarStamp starStamp;
        readonly List<StageRatingStar> hudStars=new List<StageRatingStar>();
        readonly List<TMP_Text> routeLabels=new List<TMP_Text>();
        readonly List<StageGoldPanel> routePanels=new List<StageGoldPanel>();

        RectTransform StageNode(Transform parent,string name,Vector2 position,Vector2 size)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent,false);rect.anchoredPosition=position;rect.sizeDelta=size;
            return rect;
        }
        TMP_Text StageLabel(Transform parent,string name,Vector2 position,Vector2 size,string text,float fontSize)
        {
            var rect=StageNode(parent,name,position,size);
            var label=rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font=undoButton.GetComponentInChildren<TMP_Text>(true).font;
            label.text=text;label.fontSize=fontSize;label.fontSizeMax=fontSize;
            label.fontSizeMin=14;label.enableAutoSizing=true;
            label.fontStyle=FontStyles.Bold;label.alignment=TextAlignmentOptions.Center;
            label.color=Color.white;label.outlineColor=Color.black;label.outlineWidth=.12f;
            label.raycastTarget=false;return label;
        }
        Image StageImage(Transform parent,string name,Vector2 position,Vector2 size,Sprite sprite)
        {
            var image=StageNode(parent,name,position,size).gameObject.AddComponent<Image>();
            image.sprite=sprite;image.preserveAspect=true;image.raycastTarget=false;return image;
        }
        StageGoldPanel StageGold(RectTransform rect,bool selected=false)
        {
            var graphic=rect.gameObject.AddComponent<StageGoldPanel>();graphic.Selected=selected;
            return graphic;
        }
        Button StageButton(Transform parent,string name,Vector2 position,Vector2 size,string text,bool primary,UnityEngine.Events.UnityAction click,out TMP_Text label,bool hud=false)
        {
            var rect=StageNode(parent,name,position,size);
            Graphic graphic;
            if(hud)graphic=StageGold(rect);
            else
            {
                var image=rect.gameObject.AddComponent<Image>();
                image.sprite=StagePresentationArt.Get(primary?StagePresentationArt.Piece.YellowButton:StagePresentationArt.Piece.WhiteButton);
                graphic=image;
            }
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=graphic;
            var colors=button.colors;colors.highlightedColor=new Color(.9f,.94f,1);
            colors.pressedColor=new Color(.65f,.7f,.75f);button.colors=colors;
            button.onClick.AddListener(click);
            label=StageLabel(rect,"Label",Vector2.zero,size-new Vector2(22,10),text,hud?22:30);
            label.fontStyle=hud?FontStyles.Bold:FontStyles.Bold|FontStyles.Italic;
            label.color=hud?new Color(1,.94f,.75f):Color.black;label.outlineWidth=0;
            return button;
        }
        void StageDigits(Transform parent,string name,Vector2 position,float height,float maxWidth=float.PositiveInfinity)
        {
            string digits=StageMissions.Current.ToString("00");
            var sprites=new List<Sprite>();float width=0;
            foreach(char c in digits)
            {
                var sprite=StagePresentationArt.Digit(c-'0');sprites.Add(sprite);
                width+=height*sprite.rect.width/sprite.rect.height;
            }
            float gap=height*.015f;
            var root=StageNode(parent,name,position,new Vector2(width+gap*(digits.Length-1),height));
            root.localScale=Vector3.one*Mathf.Min(1,maxWidth/root.sizeDelta.x);
            float x=-root.sizeDelta.x*.5f;
            for(int i=0;i<sprites.Count;i++)
            {
                float w=height*sprites[i].rect.width/sprites[i].rect.height;
                StageImage(root,"Digit"+digits[i],new Vector2(x+w*.5f,0),new Vector2(w,height),sprites[i]);
                x+=w+gap;
            }
        }
        void FillStageStars(int halfStars)
        {
            for(int i=0;i<hudStars.Count;i++)hudStars[i].SetFill(Mathf.Clamp(halfStars-i*2,0,2)*.5f);
        }
        void BuildStageComicUI()
        {
            var parent=undoButtonRect.parent;
            stageBar=StageNode(parent,"Stage Mission",new Vector2(0,-30),new Vector2(1120,66));
            stageBar.anchorMin=stageBar.anchorMax=stageBar.pivot=new Vector2(.5f,1);
            StageGold(stageBar);
            StageLabel(stageBar,"StageTitle",new Vector2(-474,0),new Vector2(136,44),"스테이지",26);
            var badge=StageNode(stageBar,"Stage Number Badge",new Vector2(-372,0),new Vector2(62,44));
            StageGold(badge,true);StageDigits(badge,"Stage Number",Vector2.zero,32,52);
            for(int i=0;i<3;i++)hudStars.Add(StageNode(stageBar,"Star"+i,new Vector2(-307+i*34,0),new Vector2(29,29)).gameObject.AddComponent<StageRatingStar>());
            stageMessage=StageLabel(stageBar,"StageStatus",new Vector2(0,-47),new Vector2(650,24),"",17);
            stageMessage.color=new Color(.9f,.9f,.85f);
            stageAction=StageButton(stageBar,"StageAction",new Vector2(357,0),new Vector2(166,42),"▶ 시범보기",false,OnStageAction,out stageActionLabel,true);
            StageButton(stageBar,"StageList",new Vector2(497,0),new Vector2(100,42),"목록",false,LeaveStage,out _,true);
            stageShade=StageNode(parent,"Stage Modal Shade",Vector2.zero,Vector2.zero);
            stageShade.anchorMin=Vector2.zero;stageShade.anchorMax=Vector2.one;
            stageShade.offsetMin=stageShade.offsetMax=Vector2.zero;
            var dim=stageShade.gameObject.AddComponent<Image>();dim.color=new Color(0,0,0,.62f);
            introCard=StageNode(parent,"Stage Intro",Vector2.zero,new Vector2(570,370));
            StageImage(introCard,"Printed Intro Artwork",Vector2.zero,introCard.sizeDelta,StagePresentationArt.Get(StagePresentationArt.Piece.Intro));
            StageDigits(introCard,"Illustrated Stage Number",new Vector2(0,-3),146);
            StageButton(introCard,"StageStart",new Vector2(0,-119),new Vector2(355,68),"▶ 시작하기",true,StartStageGuide,out _);
            introCard.gameObject.SetActive(false);
            resultCard=StageNode(parent,"Stage Result",Vector2.zero,new Vector2(650,410));
            resultArtwork=StageImage(resultCard,"Printed Result Artwork",Vector2.zero,resultCard.sizeDelta,StagePresentationArt.Get(StagePresentationArt.Piece.Success));
            starStamp=resultCard.gameObject.AddComponent<StageStarStamp>();
            var starImages=new List<Image>();
            for(int i=0;i<3;i++)starImages.Add(StageImage(resultCard,"AwardStar"+i,new Vector2((i-1)*88,-7),new Vector2(78,72),StagePresentationArt.Get(StagePresentationArt.Piece.Star)));
            starStamp.Initialize(starImages.ToArray());
            resultDetail=StageLabel(resultCard,"Detail",new Vector2(0,-98),new Vector2(540,40),"",24);
            resultRetry=StageButton(resultCard,"StageRetry",new Vector2(-145,-145),new Vector2(260,60),"다시하기",true,RetryStage,out resultRetryLabel);
            StageButton(resultCard,"StageResultList",new Vector2(145,-145),new Vector2(260,60),"목록으로",false,LeaveStage,out _);
            Assets.Scripts.Often.ModalBackdrop.Attach(dim,null,DismissStageModal);
            Assets.Scripts.Often.ModalBackdrop.AttachSurface(introCard);
            Assets.Scripts.Often.ModalBackdrop.AttachSurface(resultCard);
            resultCard.gameObject.SetActive(false);stageShade.gameObject.SetActive(false);
        }
        void DismissStageModal()
        {
            stageModalDismissed=true;
            introCard.gameObject.SetActive(false);resultCard.gameObject.SetActive(false);stageShade.gameObject.SetActive(false);
            stageAction.gameObject.SetActive(true);stageActionLabel.text=stagePhase==StagePhase.Intro?"시작 화면":"결과 보기";
        }
        void LeaveStage()
        {
            ShotCtrl.canControl=false;ClearStageCushion();ResetStageShotState();PracticeSceneFlow.ReturnToMenu();
        }
        void RetryStage()
        {
            if(stagePhase==StagePhase.Won || stagePhase==StagePhase.Failed)AimStage();
            else if(stagePhase==StagePhase.Exhausted)OnStageAction();
        }
        void RefreshStageComicUI()
        {
            stageModalDismissed=false;
            bool intro=stagePhase==StagePhase.Intro;
            bool result=stagePhase==StagePhase.Won || stagePhase==StagePhase.Failed || stagePhase==StagePhase.Exhausted || stagePhase==StagePhase.Waiting || stagePhase==StagePhase.Error;
            introCard.gameObject.SetActive(intro);resultCard.gameObject.SetActive(result);
            stageShade.gameObject.SetActive(intro || result);stageBar.SetAsLastSibling();
            FillStageStars(stagePhase==StagePhase.Shooting || stagePhase==StagePhase.Won?stageAward:StageMissions.Chances-StageMissions.Spent(StageMissions.Current));
            if(result)
            {
                bool won=stagePhase==StagePhase.Won;
                resultArtwork.sprite=StagePresentationArt.Get(won?StagePresentationArt.Piece.Success:StagePresentationArt.Piece.Failure);
                resultDetail.text=won?$"스테이지 {StageMissions.Current:00} 완료":stagePhase==StagePhase.Failed?(stageShot!=null && stageShot.Scored?"득점했지만 미션 순서가 달라요":"다시 도전해 보세요"):stageMessage.text;
                if(won)starStamp.Begin(stageAward);else starStamp.Clear();
                resultRetryLabel.text=stagePhase==StagePhase.Exhausted?"15초 후 충전":"다시하기";
                resultRetry.interactable=stagePhase!=StagePhase.Waiting && stagePhase!=StagePhase.Error;
            }
            else starStamp.Clear();
            stageAction.gameObject.SetActive(stagePhase==StagePhase.Aim);stageActionLabel.text="▶ 시범보기";
            if(stagePhase==StagePhase.Aim)stageMessage.text="큐대를 조준해 미션에 도전하세요";
            else if(stagePhase==StagePhase.Demo)stageMessage.text="시범 재생 중";
            else if(stagePhase==StagePhase.Guide)stageMessage.text="쿠션 순서를 확인하세요";
            stageMessage.gameObject.SetActive(stagePhase==StagePhase.Aim || stagePhase==StagePhase.Guide || stagePhase==StagePhase.Demo);
            RefreshStageRoute();
        }
        void RefreshStageRoute()
        {
            if(mission==null || !stageBar)return;
            int count=mission.order.Replace("O","").Length;
            while(routeLabels.Count<count)
            {
                int i=routeLabels.Count;
                var rect=StageNode(stageBar,"CushionStep"+i,Vector2.zero,new Vector2(90,38));
                routePanels.Add(StageGold(rect));
                routeLabels.Add(StageLabel(rect,"Label",Vector2.zero,new Vector2(86,34),"",21));
            }
            int completed=stagePhase==StagePhase.Shooting?stageShot.Cushions:stagePhase==StagePhase.Demo?demoCushionCursor:stagePhase==StagePhase.Guide?guideNumber-1:stagePhase==StagePhase.Won?count:0;
            int index=0;
            foreach(char side in mission.order)
            {
                if(side=='O')continue;
                var label=routeLabels[index];float width=Mathf.Min(122,440f/count)-5;
                ((RectTransform)label.transform.parent).anchoredPosition=new Vector2(36+(index-(count-1)*.5f)*(width+5),0);
                ((RectTransform)label.transform.parent).sizeDelta=new Vector2(width,38);
                label.rectTransform.sizeDelta=new Vector2(width-8,34);
                label.text=$"{index+1} {StageDirections(side.ToString())}";
                bool active=index<=completed;routePanels[index].Selected=active;
                label.color=active?Color.black:new Color(.95f,.93f,.85f);label.outlineWidth=0;index++;
            }
        }
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StageRatingStar : MaskableGraphic
    {
        float fill;
        public void SetFill(float value){fill=value;SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();raycastTarget=false;
            float radius=rectTransform.rect.width*.48f;
            void Star(float scale,Color tint,bool clip)
            {
                for(int i=0;i<10;i++)
                {
                    Vector2 Point(int n){float a=(90+n*36)*Mathf.Deg2Rad;return new Vector2(Mathf.Cos(a),Mathf.Sin(a))*(n%2==0?radius:radius*.46f)*scale;}
                    var polygon=new List<Vector2>{Vector2.zero,Point(i),Point((i+1)%10)};
                    if(clip)
                    {
                        float boundary=Mathf.Lerp(-radius,radius,fill);
                        var output=new List<Vector2>();
                        for(int j=0;j<polygon.Count;j++)
                        {
                            var a=polygon[j];var b=polygon[(j+1)%polygon.Count];
                            bool ai=a.x<=boundary,bi=b.x<=boundary;
                            if(ai)output.Add(a);
                            if(ai!=bi)output.Add(Vector2.Lerp(a,b,(boundary-a.x)/(b.x-a.x)));
                        }
                        polygon=output;
                    }
                    if(polygon.Count<3)continue;
                    int start=mesh.currentVertCount;
                    foreach(var p in polygon)mesh.AddVert(p,tint,Vector2.zero);
                    for(int j=1;j<polygon.Count-1;j++)mesh.AddTriangle(start,start+j,start+j+1);
                }
            }
            Star(1,Color.black,false);Star(.87f,Color.white,false);
            Star(.70f,new Color(.23f,.24f,.26f),false);
            if(fill>0)Star(.70f,new Color(1,.88f,0),true);
        }
    }

}
