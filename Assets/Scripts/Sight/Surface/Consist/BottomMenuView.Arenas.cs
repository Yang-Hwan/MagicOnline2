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
            public RawImage Blue,Gold,Entry;
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
            c.Entry=ArenaArt("Entry Brush",content,new Rect(23,496,274,54),new Rect(175,930,900,162));
            c.Action=Text("ArenaActionLabel",content,"",23,498,274,48,28,Color.white,autoSize:true);
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
            c.Action.text=c.Selected?(allowed?"입장하기":"입장 조건 확인"):"눌러서 중앙으로";
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
