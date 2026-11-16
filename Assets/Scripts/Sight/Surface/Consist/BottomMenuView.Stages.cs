using Assets.Scripts.Often;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Sight.Surface.Consist
{
    public sealed partial class BottomMenuView
    {
        Texture2D stageArt;
        Texture2D StageArt => stageArt ? stageArt : (stageArt=Resources.Load<Texture2D>("StageMissions/StageArt"));
        RawImage StageSlice(string name,Transform parent,Rect area,Rect source)
        {
            var image=Art(name,parent,StageArt,area.x,area.y,area.width,area.height);
            image.uvRect=UV(source);
            return image;
        }
        void OpenStages()
        {
            CloseModal();
            modal=Node("StageList",design,0,0,Width,Height);
            Box("Background",modal,0,0,Width,Height,Color.black).raycastTarget=true;
            StageSlice("Graffiti",modal,new Rect(0,0,Width,Height),new Rect(600,28,558,162));
            StageSlice("Title",modal,new Rect(0,0,Width,174),new Rect(0,0,Width,174));
            int done=0;
            while(done<StageMissions.Count && StageMissions.Best(done+1)>0) done++;
            Box("CountBacking",modal,1240,29,378,83,Yellow);
            Text("Count",modal,$"완료 {done} / 100",1240,29,378,83,52,Color.black);
            Hit("Back",modal,18,12,145,119,CloseModal);
            var viewport=Node("Viewport",modal,0,185,Width,610);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color=Color.clear;
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();
            const int pitch=311;
            var content=Node("Cards",viewport,0,0,pitch*StageMissions.Count+35,610);
            scroll.viewport=viewport; scroll.content=content; scroll.vertical=false;
            scroll.movementType=ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity=48;
            var cards=new RectTransform[StageMissions.Count];
            for(int i=0;i<cards.Length;i++)
                cards[i]=Node("Stage"+(i+1),content,20+i*pitch,15,300,559);
            // Keep a card on either side ready during a swipe. Only visible previews need UI meshes.
            int previousFirst=-1;
            void ShowWindow(Vector2 ignored)
            {
                int first=Mathf.Clamp(Mathf.FloorToInt(-content.anchoredPosition.x/pitch)-1,0,cards.Length-1);
                if(first==previousFirst)return;
                const int window=9;
                if(previousFirst>=0)
                    for(int i=previousFirst;i<Mathf.Min(previousFirst+window,cards.Length);i++)
                        if(i<first || i>=first+window) Clear(cards[i]);
                for(int i=first;i<Mathf.Min(first+window,cards.Length);i++)
                    if(previousFirst<0 || i<previousFirst || i>=previousFirst+window) StageCard(cards[i],i+1);
                previousFirst=first;
            }
            scroll.onValueChanged.AddListener(ShowWindow);
            Text("First",modal,"1",65,815,105,64,43,Color.white);
            Text("Last",modal,"100",1480,815,135,64,43,Color.white);
            var track=Box("ScrollTrack",modal,180,838,1300,22,new Color(.25f,.25f,.25f));
            track.raycastTarget=true;
            var handle=Box("ScrollHandle",track.transform,0,0,65,22,Yellow);
            Stretch(handle.rectTransform);
            var bar=track.gameObject.AddComponent<Scrollbar>();
            bar.handleRect=handle.rectTransform; bar.targetGraphic=handle;
            scroll.horizontalScrollbar=bar;
            Canvas.ForceUpdateCanvases();
            int focus=PracticeSceneFlow.ReturnToPracticeMenu ? StageMissions.Current : Mathf.Min(done+1,100);
            scroll.horizontalNormalizedPosition=Mathf.Clamp01(((focus-1)*pitch-Width*.35f)/(content.rect.width-Width));
            ShowWindow(Vector2.zero);
        }
        void StageCard(RectTransform card,int id)
        {
            bool open=StageMissions.Open(id);
            int best=StageMissions.Best(id);
            bool challenge=open && best==0;
            Rect src=!open ? new Rect(966,211,285,550) : challenge ? new Rect(654,200,306,574) : new Rect(21,210,337,554);
            StageSlice("Card",card,new Rect(0,0,300,559),src);
            Box("NumberBacking",card,36,22,229,99,open?new Color(.025f,.12f,.16f):new Color(.12f,.12f,.13f));
            Text("Number",card,id.ToString("00"),36,18,229,100,76,Color.white);
            if(!open) return;
            var mission=StageMissions.Get(id);
            var felt=Box("Cloth",card,41,160,217,251,new Color(.015f,.48f,.59f));
            Vector2 OnCloth(Vector2 p) => new Vector2(9+p.y*199,9+(1-p.x)*233);
            for(int j=1;j<mission.route.Length;j++)
            {
                var a=OnCloth(mission.route[j-1]); var b=OnCloth(mission.route[j]);
                int dots=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)/11));
                for(int d=0;d<dots;d++) {
                    var point=Vector2.Lerp(a,b,d/(float)dots);
                    Box("Dot",felt.transform,point.x,point.y,3.5f,3.5f,Color.white);
                }
            }
            for(int j=0;j<mission.layout.Length;j++)
            {
                var p=OnCloth(mission.layout[j]);
                Rect ball=j==0?new Rect(128,539,38,38):j==1?new Rect(226,443,37,37):new Rect(134,390,37,37);
                var image=Node("Ball",felt.transform,p.x-11,p.y-11,22,22).gameObject.AddComponent<StageBallGraphic>();
                image.texture=StageArt; image.uvRect=UV(ball); image.raycastTarget=false;
            }
            if(best>0) {
                Box("Rating",card,24,449,252,73,Color.white);
                for(int j=0;j<3;j++) {
                    int fill=Mathf.Clamp(best-2*j,0,2);
                    var star=StageSlice("Star",card,new Rect(40+j*75,452,65,62),fill==1?new Rect(527,656,73,68):new Rect(70,657,71,67));
                    if(fill==0) star.color=new Color(.36f,.36f,.36f);
                }
            }
            Hit("Challenge"+id,card,0,0,300,559,()=>StageMissions.Enter(id));
        }
    }

    public sealed class StageBallGraphic : RawImage
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect=rectTransform.rect;
            mesh.AddVert(rect.center,color,uvRect.center);
            const int segments=20;
            for(int i=0;i<segments;i++)
            {
                float angle=i*(2*Mathf.PI/segments);
                var radial=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*.5f;
                mesh.AddVert(rect.center+Vector2.Scale(rect.size,radial),color,uvRect.center+Vector2.Scale(uvRect.size,radial));
                mesh.AddTriangle(0,1+(i+1)%segments,1+i);
            }
        }
    }
}
