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
        public const float RingDuration=.72f;
        public sealed class Counter
        {
            public int Target=8, Desired, Displayed;
            public int Moving=-1, FromScore, ToScore;
            public float Started;
            public float Progress(float time)=>Mathf.Clamp01((time-Started)/RingDuration);
        }
        public readonly Counter[] Counters={new Counter(),new Counter()};
        readonly TMP_Text[] names=new TMP_Text[2],coins=new TMP_Text[2],series=new TMP_Text[2],levels=new TMP_Text[2];
        readonly Image[] sourceFaces=new Image[2],faces=new Image[2];
        readonly RectTransform[] sources=new RectTransform[2];
        RectTransform root;
        MatchHudGraphic graphic;
        MatchHudArtGraphic referenceArt;
        readonly Material[] nicknameMaterials=new Material[2];
        float nextDataRefresh, lastWidth=-1;
        int generation;
        string loadedMatch;
        public int ActiveSide {get;private set;}
        public float RemainingRatio {get;private set;}=1;
        public float RemainingSeconds {get;private set;}=30;
        public bool TimerRunning {get;private set;}
        public float TurnBlend {get;private set;}=1;
        public float VisualTime {get;private set;}
        public bool IsSeries => series[0] && (!string.IsNullOrEmpty(series[0].text)||!string.IsNullOrEmpty(series[1].text));
        public MatchHudGraphic Decoration=>graphic;
        public RectTransform DesignRoot=>root;
        static readonly Color Gold=new Color32(255,216,118,255);

        public static MatchHudView Install(Transform panel)
        {
            var existing=panel.GetComponent<MatchHudView>();if(existing)return existing;
            var view=panel.gameObject.AddComponent<MatchHudView>();view.Build();return view;
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
        void Build()
        {
            root=Node("MatchHud",transform,0,0,DesignWidth,DesignHeight);root.anchorMin=root.anchorMax=new Vector2(.5f,1);root.pivot=new Vector2(.5f,1);root.anchoredPosition=Vector2.zero;root.SetAsFirstSibling();
            var shell=Node("PaintedBlackGoldShell",root,0,0,DesignWidth,DesignHeight).gameObject.AddComponent<RawImage>();shell.texture=Resources.Load<Texture2D>("Gameplay/MatchHud/BlackGoldShell");shell.raycastTarget=false;
            graphic=Node("LiveLightingAndCounters",root,0,0,DesignWidth,DesignHeight).gameObject.AddComponent<MatchHudGraphic>();graphic.Initialize(this);
            referenceArt=Node("ReferenceVSAndScoreRings",root,0,0,DesignWidth,DesignHeight).gameObject.AddComponent<MatchHudArtGraphic>();referenceArt.Initialize(this);
            var box=(RectTransform)transform.Find("PlayerBoxs");box.anchorMin=box.anchorMax=new Vector2(.5f,1);box.pivot=new Vector2(.5f,1);box.anchoredPosition=Vector2.zero;box.sizeDelta=new Vector2(DesignWidth,DesignHeight);
            for(int side=0;side<2;side++)
            {
                var p=(RectTransform)box.Find(side==0?"PlayerSelf":"PlayerOther");Place(p,0,0,DesignWidth,DesignHeight);
                foreach(var g in p.GetComponentsInChildren<Graphic>(true))g.enabled=false;
                bool self=side==0;float gx=self?205:880,aw=177,ax=self?9:DesignWidth-190;
                sources[side]=(RectTransform)p.Find("Avatar");Place(sources[side],ax,9,aw,150);
                sourceFaces[side]=sources[side].GetComponent<Image>();sourceFaces[side].enabled=true;sourceFaces[side].color=Color.clear;sourceFaces[side].raycastTarget=true;
                var maskRect=Node("EquippedPortraitMask",p,ax,9,aw,150);maskRect.SetAsFirstSibling();var shape=maskRect.gameObject.AddComponent<MatchHudPortraitMask>();shape.raycastTarget=false;maskRect.gameObject.AddComponent<Mask>().showMaskGraphic=true;
                var faceRect=Node("EquippedPortrait",maskRect,5,-3,aw+14,165);faces[side]=faceRect.gameObject.AddComponent<Image>();faces[side].raycastTarget=false;faces[side].preserveAspect=true;
                names[side]=Existing(p,"TxtName",gx+16,42,438,43,31,Color.white,self?TextAlignmentOptions.MidlineLeft:TextAlignmentOptions.MidlineRight);names[side].transform.SetAsLastSibling();
                nicknameMaterials[side]=new Material(names[side].fontSharedMaterial);
                nicknameMaterials[side].EnableKeyword("OUTLINE_ON");nicknameMaterials[side].SetColor("_OutlineColor",new Color(0,0,0,1));
                nicknameMaterials[side].SetFloat("_OutlineWidth",.22f);nicknameMaterials[side].SetFloat("_OutlineSoftness",.02f);
                names[side].fontSharedMaterial=nicknameMaterials[side];names[side].extraPadding=true;names[side].UpdateMeshPadding();
                var timer=(RectTransform)p.Find("TimeInning");Place(timer,gx,43,474,42);var hit=timer.GetComponent<Image>();if(!hit)hit=timer.gameObject.AddComponent<Image>();hit.enabled=true;hit.color=Color.clear;hit.raycastTarget=true;
                coins[side]=Existing(p,"TxtMatchCoin",self?542:881,110,137,35,25,Gold,self?TextAlignmentOptions.MidlineRight:TextAlignmentOptions.MidlineLeft);
                p.Find("HitCountBox/HitCnt").gameObject.SetActive(false);
                series[side]=Existing(p,"TxtScore",self?669:835,15,56,43,26,Gold,TextAlignmentOptions.Center);
                Node("LevelBacking",p,self?101:1370,141,65,29).gameObject.AddComponent<MatchHudBadgeGraphic>();
                levels[side]=Label("Level",Node("Level",p,self?101:1370,141,65,29),"",20,Gold,TextAlignmentOptions.Center);
                p.Find("HitCountBox/EggBox").gameObject.SetActive(false);
                // Completed beads collect at the inner end of each score rail.
                var finish=(RectTransform)p.Find("HitCountBox/Finish");
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
            ResetScores(8);RefreshLayout();SyncPortraits();
        }
        public void SetPlayerName(int side,string name){if(side>=0&&side<2&&names[side])names[side].text=name??"";}
        public void SetLevel(int side,int level){if(levels[side])levels[side].text=level>0?"Lv."+level:"";}
        public void SetSeries(string self,string other){series[0].text=self??"";series[1].text=other??"";graphic.SetVerticesDirty();}
        public void ResetScores(int target)
        {
            foreach(var counter in Counters){counter.Target=Mathf.Max(1,target);counter.Desired=counter.Displayed=0;counter.Moving=-1;}if(graphic)graphic.SetVerticesDirty();
        }
        public void SetScore(int side,int score,bool animate=true)
        {
            var counter=Counters[side];counter.Desired=Mathf.Clamp(score,0,counter.Target);
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
        public void SetTurn(int side){ActiveSide=Mathf.Clamp(side,0,1);RemainingRatio=1;RemainingSeconds=30;TimerRunning=true;graphic.SetVerticesDirty();}
        public void SetTime(float progress,float duration,bool running=true)
        {
            RemainingRatio=1-Mathf.Clamp01(progress);RemainingSeconds=RemainingRatio*Mathf.Max(1,duration);TimerRunning=running;
        }
        public void StopTimer(){TimerRunning=false;}
        public void AdvanceVisual(float time,float delta)
        {
            VisualTime=time;TurnBlend=Mathf.Lerp(TurnBlend,ActiveSide==0?1:0,1-Mathf.Exp(-Mathf.Max(0,delta)*4));
            foreach(var n in Counters)if(n.Moving>=0 && time-n.Started>=RingDuration+.48f){n.Displayed=n.ToScore;n.Moving=-1;BeginRing(n);}
            if(graphic)graphic.SetVerticesDirty();
            if(referenceArt)referenceArt.SetVerticesDirty();
        }
        void LateUpdate()
        {
            if(!root)return;AdvanceVisual(VisualTime+Time.unscaledDeltaTime,Time.unscaledDeltaTime);RefreshLayout();SyncPortraits();
            if(Time.unscaledTime<nextDataRefresh)return;nextDataRefresh=Time.unscaledTime+.35f;
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
