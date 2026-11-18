using System.Collections.Generic;
using Assets.Scripts.Exert.BackSys;
using Assets.Scripts.Prack.BackSys.ChartData;
using Assets.Scripts.Prack.BackSys;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Sight.Surface.Consist
{
    public sealed partial class BottomMenuView
    {
        sealed class ArenaCard
        {
            public RectTransform Root,Swing;
            public RawImage Blue,Gold;
            public Graphic Entry;
            public TMP_Text Title,Status,Prize,Rules,Goal,Time,Finish,Action;
            public int Logical=int.MinValue,Index=-1;
            public bool Selected;
        }
        readonly List<ArenaCard> arenaCards=new List<ArenaCard>();
        ArenaSnapCarousel arenaCarousel;
        Texture2D arenaAtlas;
        TMP_Text arenaPage;
        int lastArenaPage=-1;
        RawImage ArenaArt(string name,Transform parent,Rect area,Rect source)
        {
            if(!arenaAtlas)arenaAtlas=Resources.Load<Texture2D>("Menu/Arenas/Cards");
            var image=Art(name,parent,arenaAtlas,area.x,area.y,area.width,area.height);
            image.uvRect=new Rect(source.x/1254,1-(source.y+source.height)/1254,source.width/1254,source.height/1254);
            return image;
        }
        void BuildArenaCarousel()
        {
            arenaCards.Clear();lastArenaPage=-1;
            var scene=Resources.Load<Texture2D>("Menu/Arenas/Backdrop");
            if(scene)backplate.texture=scene;
            // Clip at the screen bounds only. The foreground wall's real alpha silhouette
            // determines which parts of the moving signs show through the broken opening.
            var viewport=Node("Arena Carousel",body,0,143,Width,692);
            viewport.gameObject.AddComponent<RectMask2D>();
            var touch=viewport.gameObject.AddComponent<Image>();touch.color=Color.clear;
            arenaCarousel=viewport.gameObject.AddComponent<ArenaSnapCarousel>();
            for(int i=0;i<5;i++)arenaCards.Add(CreateArenaCard(viewport,i));
            var foreground=Resources.Load<Texture2D>("Menu/Arenas/Foreground");
            if(foreground)
            {
                var wall=Art("Foreground Broken Wall",body,foreground,0,130,Width,705);
                wall.uvRect=new Rect(0,1-835f/941,1,705f/941);
                wall.raycastTarget=true;wall.gameObject.AddComponent<ArenaWallHitBlocker>();
            }
            arenaPage=Text("ArenaPagination",body,"",690,729,292,30,22,Yellow);
            Text("ArenaHint",body,"좌우로 드래그 · 양옆 카드를 누르면 중앙으로 이동",420,771,850,35,23,Color.white,autoSize:true);
            if(BackendChart.skillMatchData.Count==0)
                Text("NoArena",body,"경기장 정보를 불러오지 못했습니다",480,420,710,80,30,Color.white);
            arenaCarousel.Changed=PositionArenaCards;
            arenaCarousel.Initialize(BackendChart.skillMatchData.Count,selectedHall);
        }
        ArenaCard CreateArenaCard(Transform parent,int slot)
        {
            var c=new ArenaCard();
            c.Root=Node("Hanging Arena "+slot,parent,0,0,320,560);
            c.Root.pivot=new Vector2(.5f,1);
            // Rail slider stays level; only the hook and the suspended sign swing below it.
            ArenaArt("Steel Slider",c.Root,new Rect(140,0,39,69),new Rect(320,12,63,110));
            c.Swing=Node("Suspended Sign",c.Root,160,62,320,498);c.Swing.pivot=new Vector2(.5f,1);
            c.Blue=ArenaArt("Blue Sign",c.Swing,new Rect(0,0,320,498),new Rect(96,110,514,790));
            c.Gold=ArenaArt("Selected Sign",c.Swing,new Rect(0,0,320,498),new Rect(619,112,562,806));
            var content=Node("Live Arena Information",c.Swing,0,-62,320,560);
            c.Title=Text("ArenaTitle",content,"",24,165,272,51,31,Color.white,autoSize:true);
            c.Title.textWrappingMode=TextWrappingModes.NoWrap;
            c.Status=Text("ArenaPresence",content,"",30,233,260,31,21,Color.white,autoSize:true);
            TMP_Text Row(string label,float y,out TMP_Text value)
            {
                var caption=Text("Label"+label,content,label,34,y,106,34,22,Color.white,TextAlignmentOptions.Left);
                value=Text("Value"+label,content,"",139,y,145,34,25,label=="입장"?Yellow:Color.white,TextAlignmentOptions.Right,true);
                value.textWrappingMode=TextWrappingModes.NoWrap;
                Box("Divider",content,34,y+35,250,1,new Color(.27f,.51f,.62f,.5f));
                return caption;
            }
            Row("입장",274,out c.Prize);Row("규칙",316,out c.Rules);
            Row("목표",358,out c.Goal);Row("제한시간",400,out c.Time);
            c.Finish=Text("ArenaFinish",content,"",35,445,250,32,23,new Color(.7f,.94f,1),autoSize:true);
            c.Entry=Node("Entry Capsule",content,50.4f,501.4f,219.2f,43.2f).gameObject.AddComponent<ArenaEntryCapsuleGraphic>();
            c.Entry.color=Yellow;c.Entry.raycastTarget=false;
            c.Action=Text("ArenaActionLabel",content,"",50.4f,502.8f,219.2f,38.4f,22.4f,Color.white,autoSize:true);
            // Button covers the sign, so a tap anywhere on a side card brings it to centre.
            var hit=Hit("ArenaAction"+slot,content,0,80,320,480,()=>
            {
                if(!arenaCarousel || c.Index<0)return;
                if(!arenaCarousel.Settled || c.Logical!=arenaCarousel.Target)arenaCarousel.SnapTo(c.Logical);
                else if(c.Index<BackendChart.skillMatchData.Count)EnterArena(BackendChart.skillMatchData[c.Index]);
            });
            hit.transition=UnityEngine.UI.Selectable.Transition.None;
            hit.targetGraphic.color=Color.clear;
            return c;
        }
        void BindArenaCard(ArenaCard c)
        {
            if(c.Index<0 || c.Index>=BackendChart.skillMatchData.Count)return;
            var hall=BackendChart.skillMatchData[c.Index];
            c.Title.text=hall.HallName;
            c.Status.text=roomCounts.TryGetValue(hall.HallIdx,out int count)?$"접속자 {count:N0}명":"접속자 확인 중";
            c.Prize.text=$"{hall.PrizeCoin:N0} 코인";
            c.Rules.text=$"{hall.MatchBall}구";c.Goal.text=$"{hall.TargetHit}개";c.Time.text=$"{hall.MatchTotMin}분";
            string[] finishes={"","쿠션마무리","뱅크마무리","투뱅크마무리"};
            c.Finish.text=finishes[Mathf.Clamp(hall.FinishMission,0,3)];
            c.Finish.gameObject.SetActive(!string.IsNullOrEmpty(c.Finish.text));
            bool allowed=CanEnter(hall,BackendGame.Instance.UserMainData.Coin);
            c.Entry.gameObject.SetActive(c.Selected);
            c.Action.text=c.Selected?(allowed?"입장하기":"입장 조건 확인"):"";
            c.Action.color=c.Selected?Color.black:new Color(.75f,.94f,1);
        }
        void PositionArenaCards()
        {
            if(!arenaCarousel || CurrentPage!=2)return;
            int count=BackendChart.skillMatchData.Count;
            if(count!=arenaCarousel.Count)
            {
                arenaCarousel.Initialize(count,Mathf.Clamp(selectedHall,0,Mathf.Max(0,count-1)));return;
            }
            int anchor=Mathf.RoundToInt(arenaCarousel.Position);
            for(int i=0;i<arenaCards.Count;i++)
            {
                var c=arenaCards[i];int logical=anchor+i-2;
                bool visible=count>0 && (arenaCarousel.Loop || logical>=0 && logical<count);
                c.Root.gameObject.SetActive(visible);if(!visible)continue;
                int index=arenaCarousel.Index(logical);
                bool chosen=arenaCarousel.Settled && logical==arenaCarousel.Target;
                bool rebind=c.Index!=index || c.Selected!=chosen;
                c.Logical=logical;c.Index=index;c.Selected=chosen;
                float distance=logical-arenaCarousel.Position;
                c.Root.anchoredPosition=new Vector2(Width*.5f+distance*ArenaSnapCarousel.Pitch,0);
                c.Swing.localRotation=Quaternion.Euler(0,0,arenaCarousel.Sway*(1-Mathf.Min(.3f,Mathf.Abs(distance)*.12f)));
                float glow=Mathf.SmoothStep(0,1,1-Mathf.Clamp01(Mathf.Abs(distance)));
                c.Gold.color=new Color(1,1,1,glow);
                if(rebind)BindArenaCard(c);
            }
            int page=arenaCarousel.Index(arenaCarousel.Target);
            if(page!=lastArenaPage)
            {
                lastArenaPage=page;arenaPage.text=count==0?"0 / 0":$"{page+1} / {count}";
            }
            if(arenaCarousel.Settled && page>=0)selectedHall=page;
        }
        void RefreshArenaCards()
        {
            if(CurrentPage!=2 || !arenaCarousel)return;
            PositionArenaCards();
            foreach(var card in arenaCards)if(card.Root.gameObject.activeSelf)BindArenaCard(card);
        }
    }

    // Radius follows half the button height, so both ends remain true semicircles.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ArenaEntryCapsuleGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect=rectTransform.rect;
            float radius=Mathf.Min(rect.width,rect.height)*.5f;
            if(radius<=0)return;
            const int steps=24;
            Color Paint(float y)
            {
                var tint=Color.Lerp(new Color(1,.65f,.035f),new Color(1,.95f,.24f),Mathf.InverseLerp(rect.yMin,rect.yMax,y));
                tint.a=color.a;return tint;
            }
            vh.AddVert(rect.center,Paint(rect.center.y),Vector2.zero);
            for(int side=0;side<2;side++)
            {
                float cx=side==0?rect.xMax-radius:rect.xMin+radius;
                for(int i=0;i<=steps;i++)
                {
                    float angle=(-90f+180f*side+180f*i/steps)*Mathf.Deg2Rad;
                    var point=new Vector2(cx+Mathf.Cos(angle)*radius,rect.center.y+Mathf.Sin(angle)*radius);
                    vh.AddVert(point,Paint(point.y),Vector2.zero);
                }
            }
            int perimeter=2*(steps+1);
            for(int i=0;i<perimeter;i++)vh.AddTriangle(0,i+1,(i+1)%perimeter+1);
            // Deterministic spray flecks and angled paint scratches, contained by the capsule.
            // Keep the centre quiet for the black entry label; no per-frame random flicker.
            float Noise(int seed)=>Mathf.Repeat(Mathf.Sin(seed*91.71f+13.3f)*43758.54f,1);
            bool Inside(Vector2 p)
            {
                float cx=Mathf.Clamp(p.x,rect.xMin+radius,rect.xMax-radius);
                return (p-new Vector2(cx,rect.center.y)).sqrMagnitude<(radius-1)*(radius-1);
            }
            void Mark(Vector2 a,Vector2 b,float halfWidth,Color tint)
            {
                var offset=new Vector2(-(b-a).y,(b-a).x).normalized*halfWidth;
                if(!Inside(a-offset)||!Inside(a+offset)||!Inside(b-offset)||!Inside(b+offset))return;
                int start=vh.currentVertCount;tint.a*=color.a;
                vh.AddVert(a-offset,tint,Vector2.zero);vh.AddVert(b-offset,tint,Vector2.zero);
                vh.AddVert(b+offset,tint,Vector2.zero);vh.AddVert(a+offset,tint,Vector2.zero);
                vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start+2,start+3,start);
            }
            for(int i=0;i<13;i++)
            {
                float u=(i+.5f)/13;
                var a=new Vector2(rect.xMin+rect.width*u,rect.yMin+rect.height*(i%2==0?.12f:.72f));
                var b=a+new Vector2(rect.height*.24f,rect.height*.17f);
                Mark(a,b,.6f+Noise(i)*1.1f,i%3==0?new Color(1,1,.62f,.34f):new Color(.57f,.29f,.015f,.16f));
            }
            for(int i=0;i<90;i++)
            {
                float u=Noise(i+100),v=Noise(i+300);
                var p=new Vector2(rect.xMin+u*rect.width,rect.yMin+v*rect.height);
                float quiet=u>.2f&&u<.8f&&v>.25f&&v<.75f?.35f:1;
                float size=.25f+Noise(i+500)*.65f;
                var tint=i%3==0?new Color(1,1,.65f,.28f*quiet):new Color(.43f,.25f,.015f,.19f*quiet);
                Mark(p,p+Vector2.right*size*2,size,tint);
            }
        }
    }

    // The foreground wall receives hits only outside its opening. Hidden cards cannot be tapped.
    public sealed class ArenaWallHitBlocker : MonoBehaviour,ICanvasRaycastFilter
    {
        public const float AlphaHitThreshold=.075f;
        Material opaqueWall;
        void Awake()
        {
            var shader=Resources.Load<Shader>("Menu/Arenas/OpaqueWall");
            if(!shader)return;
            opaqueWall=new Material(shader);
            GetComponent<RawImage>().material=opaqueWall;
        }
        void OnDestroy(){if(opaqueWall)Destroy(opaqueWall);}
        public bool IsRaycastLocationValid(Vector2 screenPoint,Camera camera)
        {
            var rect=(RectTransform)transform;
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,screenPoint,camera,out var p))return false;
            var image=GetComponent<RawImage>();
            var texture=image.texture as Texture2D;
            if(!texture || !texture.isReadable)return true;
            float u=(p.x-rect.rect.xMin)/rect.rect.width;
            float v=(p.y-rect.rect.yMin)/rect.rect.height;
            var uv=image.uvRect;
            // Match hit occlusion to the same irregular pixels that cover the cards.
            return texture.GetPixelBilinear(uv.x+u*uv.width,uv.y+v*uv.height).a>AlphaHitThreshold;
        }
    }
}
