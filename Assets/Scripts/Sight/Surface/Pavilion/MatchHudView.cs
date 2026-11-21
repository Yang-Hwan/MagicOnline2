using System;
using System.Globalization;
using Assets.Scripts.Exert.BackSys;
using Assets.Scripts.Exert.Match;
using Assets.Scripts.Exert.Network;
using Assets.Scripts.Prack.BackSys.GameData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Sight.Surface.Pavilion
{
    // Presentation only: the existing match controller owns time, scores, coins and input.
    // Original avatar/timer paths remain intact for equipment, reactions and chat hit targets.
    public sealed class MatchHudView : MonoBehaviour
    {
        public const float DesignWidth=1560, DesignHeight=178;
        internal const float GaugeTop=58;
        public const float RingDuration=.72f;
        [Serializable] public sealed class Counter
        {
            public int Target=8, Actual, Desired, Displayed;
            public int Moving=-1, FromScore, ToScore;
            public float Started;
            public float Progress(float time)=>Mathf.Clamp01((time-Started)/RingDuration);
        }
        public readonly Counter[] Counters={new Counter(),new Counter()};
        readonly TMP_Text[] names=new TMP_Text[2],coins=new TMP_Text[2],series=new TMP_Text[2],levels=new TMP_Text[2];
        readonly Image[] sourceFaces=new Image[2],faces=new Image[2];
        readonly MatchHudPortraitGlow[] portraitGlows = new MatchHudPortraitGlow[2];
        readonly Image[] portraitShadows = new Image[2];
        readonly RectTransform[] sources=new RectTransform[2];
        RectTransform root;
        MatchHudGraphic graphic;
        MatchHudArtGraphic referenceArt;
        readonly Material[] nicknameMaterials=new Material[2];
        float nextDataRefresh, lastWidth=-1;
        int generation;
        string loadedMatch;
        bool localPractice;
        QuickShotBonusMotion quickMotion;
        float quickPulse = -10; int quickPulseSide;
        public bool QuickShotEnabled => !localPractice && NetworkManager.TryGetExistingNetwork(out var n) &&
            n is PunNetwork p && p.HasQuickShotBonus;
        // Preview/validation can use the same renderer without creating a paid match.
        internal bool QuickShotPreview;
        ReplayVisual replayVisual;
        public bool DrawQuickShot => replayVisual!=null?replayVisual.quick:QuickShotEnabled || QuickShotPreview;
        public void ShowQuickShotBonus(int side,long bonus)
        {
            if(bonus<=0)return;
            if(!quickMotion)quickMotion=QuickShotBonusMotion.Install(root,this);
            quickMotion.Show(side,bonus);quickPulse=VisualTime;quickPulseSide=side;
        }
        public bool ShowLevels => replayVisual!=null?replayVisual.levels:!localPractice;
        public int ActiveSide {get;private set;}
        public float RemainingRatio {get;private set;}=1;
        public float RemainingSeconds {get;private set;}=30;
        public bool TimerRunning {get;private set;}
        public float TurnBlend {get;private set;}=1;
        public float VisualTime {get;private set;}
        public bool IsSeries => replayVisual!=null?replayVisual.series:series[0] && (!string.IsNullOrEmpty(series[0].text)||!string.IsNullOrEmpty(series[1].text));
        [Serializable] public sealed class ReplayVisual
        {
            public int side;public float ratio,seconds,blend,time;public bool timer,quick,levels,series;public Counter[] counters;
        }
        public ReplayVisual CaptureReplayVisualState()=>new ReplayVisual{side=ActiveSide,ratio=RemainingRatio,seconds=RemainingSeconds,blend=TurnBlend,time=VisualTime,timer=TimerRunning,quick=DrawQuickShot,levels=ShowLevels,series=IsSeries,counters=Array.ConvertAll(Counters,n=>new Counter{Target=n.Target,Actual=n.Actual,Desired=n.Desired,Displayed=n.Displayed,Moving=n.Moving,FromScore=n.FromScore,ToScore=n.ToScore,Started=n.Started})};
        public string CaptureReplayVisual()=>JsonUtility.ToJson(CaptureReplayVisualState());
        public void ApplyReplayVisual(string json){replayVisual=JsonUtility.FromJson<ReplayVisual>(json);ActiveSide=replayVisual.side;RemainingRatio=replayVisual.ratio;RemainingSeconds=replayVisual.seconds;TurnBlend=replayVisual.blend;VisualTime=replayVisual.time;TimerRunning=replayVisual.timer;for(int i=0;i<2;i++){var a=replayVisual.counters[i];var b=Counters[i];b.Target=a.Target;b.Actual=a.Actual;b.Desired=a.Desired;b.Displayed=a.Displayed;b.Moving=a.Moving;b.FromScore=a.FromScore;b.ToScore=a.ToScore;b.Started=a.Started;}}
        public MatchHudGraphic Decoration=>graphic;
        public RectTransform DesignRoot=>root;
        static readonly Color Gold=new Color32(255,216,118,255);

        public static MatchHudView Install(Transform panel,bool localPractice=false)
        {
            var existing=panel.GetComponent<MatchHudView>();if(existing)return existing;
            var view=panel.gameObject.AddComponent<MatchHudView>();view.localPractice=localPractice;view.Build();return view;
        }
        static RectTransform Node(string name,Transform parent,float x,float y,float w,float h)
        {
            var r=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;r.SetParent(parent,false);Place(r,x,y,w,h);return r;
        }
        static void Place(RectTransform r,float x,float y,float w,float h)
        {
            r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);r.localScale=Vector3.one;r.localRotation=Quaternion.identity;
        }
        TMP_Text Label(string name,RectTransform r,string value,float size,Color color,TextAlignmentOptions align)
        {
            var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=Resources.Load<TMP_FontAsset>("Common/Fonts/Asset/NanumBarunGothic SDF");Style(t,size,color,align);t.text=value;return t;
        }
        static void Style(TMP_Text t,float size,Color color,TextAlignmentOptions align)
        {
            t.fontSize=size;t.fontSizeMax=size;t.fontSizeMin=Mathf.Min(16,size);t.enableAutoSizing=true;t.color=color;t.alignment=align;t.fontStyle=FontStyles.Bold;
            t.textWrappingMode=TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Ellipsis;t.richText=false;t.raycastTarget=false;t.margin=Vector4.zero;
        }
        TMP_Text Existing(Transform player,string path,float x,float y,float w,float h,float size,Color tint,TextAlignmentOptions align)
        {
            var t=player.Find(path).GetComponent<TMP_Text>();Place(t.rectTransform,x,y,w,h);t.enabled=true;Style(t,size,tint,align);return t;
        }
        readonly TMP_Text[] scoreTotals = new TMP_Text[2];
        void Build()
        {
            root=Node("MatchHud",transform,0,0,DesignWidth,DesignHeight);root.anchorMin=root.anchorMax=new Vector2(.5f,1);root.pivot=new Vector2(.5f,1);root.anchoredPosition=Vector2.zero;root.SetAsFirstSibling();
            var shell=Node("PaintedBlackGoldShell",root,0,0,DesignWidth,DesignHeight).gameObject.AddComponent<RawImage>();shell.texture=Resources.Load<Texture2D>("Gameplay/MatchHud/BlackGoldShell");shell.raycastTarget=false;
            graphic=Node("LiveLightingAndCounters",root,0,0,DesignWidth,DesignHeight).gameObject.AddComponent<MatchHudGraphic>();graphic.Initialize(this);
            referenceArt=Node("ReferenceVSAndScoreRings",root,0,0,DesignWidth,DesignHeight).gameObject.AddComponent<MatchHudArtGraphic>();referenceArt.Initialize(this);
            var box=(RectTransform)transform.Find("PlayerBoxs");box.anchorMin=box.anchorMax=new Vector2(.5f,1);box.pivot=new Vector2(.5f,1);box.anchoredPosition=Vector2.zero;box.sizeDelta=new Vector2(DesignWidth,DesignHeight);
            if(localPractice && box.TryGetComponent<Image>(out var legacyBackground))
                legacyBackground.enabled=false;
            for(int side=0;side<2;side++)
            {
                var p=(RectTransform)box.Find(side==0?"PlayerSelf":"PlayerOther");Place(p,0,0,DesignWidth,DesignHeight);
                foreach(var g in p.GetComponentsInChildren<Graphic>(true))g.enabled=false;
                bool self=side==0;float gx=self?205:880,aw=177,ax=self?9:DesignWidth-190;
                sources[side]=(RectTransform)p.Find("Avatar");Place(sources[side],ax,9,aw,150);
                sourceFaces[side]=sources[side].GetComponent<Image>();
                if(localPractice) sourceFaces[side].overrideSprite=
                    Resources.Load<Sprite>("Gameplay/PracticeAnonymousAvatar") ?? MatchAvatarReactions.Anonymous;
                sourceFaces[side].enabled=true;sourceFaces[side].color=Color.clear;sourceFaces[side].raycastTarget=!localPractice;
                var maskRect=Node("EquippedPortraitMask",p,ax,9,aw,150);maskRect.SetAsFirstSibling();var shape=maskRect.gameObject.AddComponent<MatchHudPortraitMask>();shape.raycastTarget=false;maskRect.gameObject.AddComponent<Mask>().showMaskGraphic=true;
                portraitGlows[side]=Node("TurnGlow",maskRect,0,0,aw,150).gameObject.AddComponent<MatchHudPortraitGlow>();
                portraitGlows[side].raycastTarget=false;
                var faceRect=Node("EquippedPortrait",maskRect,5,-3,aw+14,165);faces[side]=faceRect.gameObject.AddComponent<Image>();faces[side].raycastTarget=false;faces[side].preserveAspect=true;
                portraitShadows[side]=Node("WaitingShadow",maskRect,0,0,aw,150).gameObject.AddComponent<Image>();
                portraitShadows[side].color=new Color32(19,16,40,132);
                portraitShadows[side].raycastTarget=false;
                var playerAlignment=self?TextAlignmentOptions.MidlineLeft:TextAlignmentOptions.MidlineRight;
                Existing(p,"TxtCoin",gx+(self?32:16),16,426,36,28,Gold,playerAlignment);
                names[side]=Existing(p,"TxtName",gx+16,GaugeTop,438,42,31,Color.white,playerAlignment);names[side].transform.SetAsLastSibling();
                nicknameMaterials[side]=new Material(names[side].fontSharedMaterial);
                nicknameMaterials[side].EnableKeyword("OUTLINE_ON");nicknameMaterials[side].SetColor("_OutlineColor",new Color(0,0,0,1));
                nicknameMaterials[side].SetFloat("_OutlineWidth",.22f);nicknameMaterials[side].SetFloat("_OutlineSoftness",.02f);
                names[side].fontSharedMaterial=nicknameMaterials[side];names[side].extraPadding=true;names[side].UpdateMeshPadding();
                var timer=(RectTransform)p.Find("TimeInning");Place(timer,gx,GaugeTop,474,42);var hit=timer.GetComponent<Image>();if(!hit)hit=timer.gameObject.AddComponent<Image>();hit.enabled=true;hit.color=Color.clear;hit.raycastTarget=true;
                coins[side]=Existing(p,"TxtMatchCoin",self?542:881,110,137,35,25,Gold,self?TextAlignmentOptions.MidlineRight:TextAlignmentOptions.MidlineLeft);
                p.Find("HitCountBox/HitCnt").gameObject.SetActive(false);
                scoreTotals[side]=Label("ActualScore",Node("ActualScore",p,self?344:1055,146,160,26),"",20,Gold,TextAlignmentOptions.Center);
                if(!localPractice) series[side]=Existing(p,"TxtScore",self?669:835,15,56,43,26,Gold,TextAlignmentOptions.Center);
                if(ShowLevels)
                {
                    Node("LevelBacking",p,self?101:1370,141,65,29).gameObject.AddComponent<MatchHudBadgeGraphic>();
                    levels[side]=Label("Level",Node("Level",p,self?101:1370,141,65,29),"",20,Gold,TextAlignmentOptions.Center);
                }
                p.Find("HitCountBox/EggBox").gameObject.SetActive(false);
                // Completed beads collect at the inner end of each score rail.
                var finish=(RectTransform)p.Find("HitCountBox/Finish");
                if (!finish) continue;
                var finishSlot=Node("FinishSlot",p,self?344:1055,106,160,40);
                finish.SetParent(finishSlot,false);
                Place(finish,0,-800,160,40);
                foreach(var g in finish.GetComponentsInChildren<Graphic>(true))g.enabled=true;
            }
            var center=(RectTransform)transform.Find("TopCenter");foreach(var g in center.GetComponentsInChildren<Graphic>(true))g.enabled=false;
            center.anchorMin=center.anchorMax=new Vector2(.5f,1);center.pivot=new Vector2(.5f,1);center.sizeDelta=new Vector2(160,178);center.anchoredPosition=Vector2.zero;
            var time=center.Find("TxtTime").GetComponent<TMP_Text>();Place(time.rectTransform,16,91,128,47);time.enabled=true;Style(time,32,Color.white,TextAlignmentOptions.Center);
            var menu=(RectTransform)transform.Find("BtnMatchEnd");menu.anchorMin=menu.anchorMax=new Vector2(0,1);menu.pivot=Vector2.one*.5f;menu.anchoredPosition=new Vector2(86,-75);
            var cuePoint=transform.Find("PointSmall") as RectTransform;
            if(cuePoint){cuePoint.anchorMin=cuePoint.anchorMax=new Vector2(1,1);cuePoint.pivot=Vector2.one*.5f;cuePoint.anchoredPosition=new Vector2(-100,-75);}
            UpdatePortraitTurn();ResetScores(8);RefreshLayout();SyncPortraits();
        }
        public void SetPlayerName(int side,string name){if(side>=0&&side<2&&names[side])names[side].text=name??"";}
        public void SetLevel(int side,int level){if(levels[side])levels[side].text=level>0?"Lv."+level:"";}
        public void SetSeries(string self,string other){series[0].text=self??"";series[1].text=other??"";graphic.SetVerticesDirty();}
        public void ResetScores(int target)
        {
            if(quickMotion)quickMotion.Hide();quickPulse=-10;
            foreach(var counter in Counters){counter.Target=Mathf.Max(1,target);counter.Actual=counter.Desired=counter.Displayed=0;counter.Moving=-1;}for(int i=0;i<2;i++)if(scoreTotals[i])scoreTotals[i].text="0 / "+Counters[i].Target;
            if(graphic)graphic.SetVerticesDirty();
        }
        public void SetScore(int side,int score,bool animate=true)
        {
            var counter=Counters[side];counter.Actual=Mathf.Max(0,score);counter.Desired=Mathf.Clamp(score,0,counter.Target);
            if(scoreTotals[side])scoreTotals[side].text=counter.Actual+" / "+counter.Target;
            if(!animate){counter.Displayed=counter.Desired;counter.Moving=-1;}
            else if(counter.Moving<0)BeginRing(counter);
            if(graphic)graphic.SetVerticesDirty();
            if(referenceArt)referenceArt.SetVerticesDirty();
        }
        void BeginRing(Counter n)
        {
            if(n.Desired==n.Displayed)return;
            n.FromScore=n.Displayed;n.ToScore=n.Displayed+Math.Sign(n.Desired-n.Displayed);n.Moving=n.Target-Math.Max(n.FromScore,n.ToScore);n.Started=VisualTime;
        }
        void UpdatePortraitTurn()
        {
            for (int side=0; side<2; side++)
            {
                portraitGlows[side].enabled=side==ActiveSide;
                portraitShadows[side].enabled=side!=ActiveSide;
            }
            portraitGlows[ActiveSide].SetTime(VisualTime);
        }
        public void SetTurn(int side){ActiveSide=Mathf.Clamp(side,0,1);UpdatePortraitTurn();RemainingRatio=1;RemainingSeconds=30;TimerRunning=true;graphic.SetVerticesDirty();}
        public void SetTime(float progress,float duration,bool running=true)
        {
            RemainingRatio=1-Mathf.Clamp01(progress);RemainingSeconds=RemainingRatio*Mathf.Max(1,duration);TimerRunning=running;
        }
        public void StopTimer(){TimerRunning=false;}
        public void AdvanceVisual(float time,float delta)
        {
            VisualTime=time;
            for(int side=0;side<2;side++)if(coins[side])
            {
                float age=time-quickPulse;
                float pop=side==quickPulseSide && age>.7f && age<1.45f ? Mathf.Sin((age-.7f)/.75f*Mathf.PI)*.16f : 0;
                coins[side].rectTransform.localScale=Vector3.one*(1+pop);
            }
            TurnBlend=Mathf.Lerp(TurnBlend,ActiveSide==0?1:0,1-Mathf.Exp(-Mathf.Max(0,delta)*4));
            portraitGlows[ActiveSide].SetTime(time);
            foreach(var n in Counters)if(n.Moving>=0 && time-n.Started>=RingDuration+.48f){n.Displayed=n.ToScore;n.Moving=-1;BeginRing(n);}
            if(graphic)graphic.SetVerticesDirty();
            if(referenceArt)referenceArt.SetVerticesDirty();
        }
        void LateUpdate()
        {
            if(!root)return;AdvanceVisual(VisualTime+Time.unscaledDeltaTime,Time.unscaledDeltaTime);RefreshLayout();SyncPortraits();
            if(localPractice || Time.unscaledTime<nextDataRefresh)return;nextDataRefresh=Time.unscaledTime+.35f;
            if(NetworkManager.TryGetExistingNetwork(out var net)&&net is PunNetwork pun&&pun.HasLegacyMatch)
            {
                SetSeries(pun.SeriesSelfWinsText,pun.SeriesOtherWinsText);
                if(loadedMatch!=pun.LegacyMatchId){loadedMatch=pun.LegacyMatchId;FetchLevels(pun,loadedMatch,++generation);}
            }
        }
        async void FetchLevels(PunNetwork network,string id,int version)
        {
            // The same authorized profile endpoint as the avatar player-info window.
            for(int seat=0;seat<2;seat++)try
            {
                var info=await GameApiClient.Request<MatchPlayerInfo>("/api/matches/"+id+"/players/"+seat);
                if(!this||!isActiveAndEnabled||version!=generation||!network||!network.HasLegacyMatch||network.LegacyMatchId!=id)return;
                if(network.MatchEquipment==null||info.UserId!=network.MatchEquipment.Players[seat].UserId)continue;
                SetLevel(seat==network.LegacyLocalSeat?0:1,info.Level);
            }catch(Exception){/* Leave unknown level blank. Gameplay must not depend on this request. */}
        }
        void SyncPortraits(){for(int i=0;i<2;i++)if(faces[i]&&sourceFaces[i])faces[i].sprite=sourceFaces[i].overrideSprite;}
        void RefreshLayout()
        {
            float width=((RectTransform)transform).rect.width;if(Mathf.Abs(width-lastWidth)<.1f)return;lastWidth=width;
            // Retain the menu and cue-point controls in the side gutters at 16:9 and 4:3.
            float scale=Mathf.Min(1,Mathf.Max(.4f,(width-380)/DesignWidth));root.localScale=Vector3.one*scale;
            transform.Find("PlayerBoxs").localScale=Vector3.one*scale;transform.Find("TopCenter").localScale=Vector3.one*scale;
        }
        void OnEnable(){generation++;loadedMatch=null;nextDataRefresh=0;}
        void OnDisable(){generation++;loadedMatch=null;TimerRunning=false;foreach(var n in Counters){n.Moving=-1;n.Displayed=n.Desired;}}
        void OnDestroy()
        {
            foreach(var mat in nicknameMaterials)if(mat){if(Application.isPlaying)Destroy(mat);else DestroyImmediate(mat);}
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MatchHudPortraitGlow : MaskableGraphic
    {
        // The backdrop is static. Cache its sampled gradient and animate just the motes.
        readonly System.Collections.Generic.List<UIVertex> backdrop = new System.Collections.Generic.List<UIVertex>();
        readonly System.Collections.Generic.List<int> indices = new System.Collections.Generic.List<int>();
        Rect backdropRect;
        float time;
        public float ReplayTime=>time;
        public void SetTime(float value)
        {
            time=value;
            SetVerticesDirty();
        }

        // A softly fading diamond; unequal upper/lower radii give rising sparks a short tail.
        static void Glow(VertexHelper mesh, Vector2 center, float width, float above, float below, Color light)
        {
            int start=mesh.currentVertCount;
            Color edge=new Color(light.r,light.g,light.b,0);
            mesh.AddVert(center,light,Vector2.zero);
            mesh.AddVert(center+Vector2.left*width,edge,Vector2.zero);
            mesh.AddVert(center+Vector2.up*above,edge,Vector2.zero);
            mesh.AddVert(center+Vector2.right*width,edge,Vector2.zero);
            mesh.AddVert(center+Vector2.down*below,edge,Vector2.zero);
            for (int i=0;i<4;i++) mesh.AddTriangle(start,start+1+i,start+1+(i+1)%4);
        }

        void BuildBackdrop(Rect rect)
        {
            backdropRect=rect;
            backdrop.Clear();
            indices.Clear();
            const int columns=28, rows=20;
            for (int row=0;row<=rows;row++)
            {
                float height=row/(float)rows;
                for (int col=0;col<=columns;col++)
                {
                    float x=col/(float)columns;
                    // Light wells sit beside the shoulders and follow the slanted opening.
                    float lane=x-height*.22f;
                    float left=(lane-.14f)/.19f, right=(lane-.72f)/.17f;
                    float spread=Mathf.Exp(-left*left)+Mathf.Exp(-right*right);
                    float haze=spread*Mathf.Exp(-height*height*6f);
                    float basePool=Mathf.Exp(-height*height*75f);
                    var color=Color.Lerp(new Color(.012f,.068f,.066f),new Color(.055f,.62f,.36f),haze);
                    color=Color.Lerp(color,new Color(.68f,1f,.83f),basePool*spread);
                    var vertex=UIVertex.simpleVert;
                    vertex.position=new Vector2(rect.xMin+rect.width*x,rect.yMin+rect.height*height);
                    vertex.color=color;
                    backdrop.Add(vertex);
                    if(row==0 || col==0) continue;
                    int top=backdrop.Count-1, bottom=top-columns-1;
                    indices.Add(bottom-1);indices.Add(top-1);indices.Add(top);
                    indices.Add(bottom-1);indices.Add(top);indices.Add(bottom);
                }
            }
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect=rectTransform.rect;
            if(backdrop.Count==0 || backdropRect!=rect) BuildBackdrop(rect);
            mesh.AddUIVertexStream(backdrop,indices);
            for (int i=0;i<14;i++)
            {
                float rise=Mathf.Repeat(time*(.105f+.014f*(i%5))+i*.618034f,1);
                float lane=(i%2==0?.10f:.63f)+.13f*Mathf.Repeat(i*.414214f,1);
                float x=rect.xMin+rect.width*(lane+rise*.22f)+1.2f*Mathf.Sin(i+rise*5);
                var point=new Vector2(x,rect.yMin+rect.height*rise);
                Color spark=i%4==0 ? new Color(1,.82f,.37f) : new Color(.63f,1,.82f);
                spark.a=Mathf.Clamp01(Mathf.Min(rise,1-rise)*8)*(.82f+.18f*Mathf.Sin(i+time*3));
                Glow(mesh,point,3.8f,4,4,new Color(spark.r,spark.g,spark.b,spark.a*.42f));
                Glow(mesh,point,.85f,1,12+i%4*2,new Color(spark.r,spark.g,spark.b,spark.a*.62f));
                Glow(mesh,point,1.65f,1.8f,1.8f,spark);
            }
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MatchHudPortraitMask : MaskableGraphic
    {
        // Left and right edges of the painted opening, in local HUD units from the top.
        // Extra rows follow the rounded corners at the ends of the slanted sides.
        static readonly Vector3[] OpeningRows =
        {
            new Vector3(63, 169, 5), new Vector3(56, 173, 9),
            new Vector3(52, 174, 14), new Vector3(50, 174, 19),
            new Vector3(9, 133, 128), new Vector3(9, 130, 134),
            new Vector3(12, 125, 140), new Vector3(17, 119, 144),
            new Vector3(23, 115, 146)
        };

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = rectTransform.rect;
            var tint = new Color(.035f, .13f, .14f, 1);
            foreach (var row in OpeningRows)
            {
                int start = vh.currentVertCount;
                float y = rect.yMax - row.z;
                vh.AddVert(new Vector3(rect.xMin + row.x, y), tint, Vector2.zero);
                vh.AddVert(new Vector3(rect.xMin + row.y, y), tint, Vector2.zero);
                if (start == 0) continue;
                vh.AddTriangle(start - 2, start - 1, start);
                vh.AddTriangle(start, start - 1, start + 1);
            }
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MatchHudBadgeGraphic : MaskableGraphic
    {
        protected override void Awake(){base.Awake();raycastTarget=false;}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;
            void Pill(float inset,Color tint)
            {
                int center=vh.currentVertCount;vh.AddVert(r.center,tint,Vector2.zero);float radius=(r.height-2*inset)*.5f;
                for(int i=0;i<=32;i++)
                {
                    float a=i*Mathf.PI/16,x=Mathf.Cos(a),y=Mathf.Sin(a);float cx=x>=0?r.xMax-inset-radius:r.xMin+inset+radius;
                    var p=new Vector2(cx+x*radius,r.center.y+y*radius);var col=inset<1?Color.Lerp(new Color(.45f,.26f,.07f),new Color(1,.87f,.54f),(y+1)*.5f):tint;
                    vh.AddVert(p,col,Vector2.zero);if(i>0)vh.AddTriangle(center,center+i,center+i+1);
                }
            }
            Pill(0,new Color(.92f,.72f,.35f));Pill(1.5f,new Color(.035f,.05f,.055f));
        }
    }
}
