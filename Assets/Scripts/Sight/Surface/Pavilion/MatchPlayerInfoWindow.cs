using System;
using System.Globalization;
using Assets.Scripts.Exert.BackSys;
using Assets.Scripts.Exert.Network;
using Assets.Scripts.Prack.BackSys.GameData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Sight.Surface.Pavilion
{
    public sealed class MatchPlayerInfoWindow : MonoBehaviour
    {
        const float Width=900, Height=850;
        static readonly Color Gold=new Color32(255,205,66,255), Ink=new Color32(19,25,35,255), Muted=new Color32(180,185,196,255);
        static MatchPlayerInfoWindow active;
        public static bool IsOpen => active && active.overlay && active.overlay.gameObject.activeInHierarchy;
        PunNetwork network;
        RectTransform overlay, card;
        TMP_FontAsset font;
        Material fontMaterial;
        string matchId, owner;
        int request;
        bool guildView;
        public void Initialize(PunNetwork value)
        {
            network=value; font=Resources.Load<TMP_FontAsset>("Common/Fonts/Asset/NanumBarunGothic SDF");
            if(font && !fontMaterial)
            {
                fontMaterial=new Material(font.material);
                fontMaterial.DisableKeyword("UNDERLAY_ON");fontMaterial.DisableKeyword("UNDERLAY_INNER");
                fontMaterial.SetFloat("_OutlineWidth",0);fontMaterial.SetFloat("_FaceDilate",0);
            }
        }

        public async void Open(int seat)
        {
            Close();
            guildView=false;
            if (!network || !network.HasLegacyMatch || network.MatchEquipment==null || seat<0 || seat>1) return;
            matchId=network.LegacyMatchId; owner=GameApiClient.UserId;
            int version=++request;
            BuildShell();
            var loading=Text("Loading",card,"선수정보를 불러오는 중…",36,360,828,70,28,Color.white);
            try
            {
                var info=await GameApiClient.Request<MatchPlayerInfo>("/api/matches/"+matchId+"/players/"+seat);
                if (!Current(version)) return;
                if (info.UserId!=network.MatchEquipment.Players[seat].UserId) throw new InvalidOperationException("PlayerChanged");
                loading.gameObject.SetActive(false); Destroy(loading.gameObject);
                Draw(info,seat);
            }
            catch (Exception)
            {
                if (!Current(version)) return;
                loading.text="선수정보를 불러오지 못했습니다.";
                Button("Retry",card,"다시 시도",325,455,250,56,()=>Open(seat));
            }
        }
        public async void OpenGuild(string guild,string user)
        {
            Close();guildView=true;owner=GameApiClient.UserId;int version=++request;BuildShell();
            var loading=Text("Loading",card,"선수정보를 불러오는 중…",36,360,828,70,28,Color.white);
            try
            {
                var info=await GameApiClient.Request<MatchPlayerInfo>("/api/guilds/"+guild+"/members/"+user+"/profile");
                if(!Current(version))return;
                if(info.UserId!=user)throw new InvalidOperationException();
                loading.gameObject.SetActive(false);Destroy(loading.gameObject);Draw(info,0);
            }
            catch(Exception) {if(Current(version)){loading.text="선수정보를 불러오지 못했습니다.";Button("Retry",card,"다시 시도",325,455,250,56,()=>OpenGuild(guild,user));}}
        }
        bool Current(int version) => this && isActiveAndEnabled && overlay && version==request && owner==GameApiClient.UserId &&
            (guildView || network && network.HasLegacyMatch && network.LegacyMatchId==matchId);
        void BuildShell()
        {
            overlay=Node("PlayerInformation",transform,0,0,0,0);
            overlay.anchorMin=Vector2.zero; overlay.anchorMax=Vector2.one; overlay.offsetMin=overlay.offsetMax=Vector2.zero;
            var shade=Fill(overlay,new Color(0,0,0,.7f));
            var outside=overlay.gameObject.AddComponent<Button>(); outside.targetGraphic=shade; outside.transition=Selectable.Transition.None;
            outside.onClick.AddListener(Close);
            card=Node("PlayerInfoCard",overlay,0,0,Width,Height);
            card.anchorMin=card.anchorMax=card.pivot=Vector2.one*.5f; card.anchoredPosition=Vector2.zero;
            var panel=card.gameObject.AddComponent<PlayerInfoFrame>(); panel.color=Ink;
            card.gameObject.AddComponent<Button>().transition=Selectable.Transition.None; // Consume clicks inside the card.
            Text("Title",card,"선수정보",80,14,740,64,44,Color.white).fontStyle=FontStyles.Bold;
            Button("Close",card,"×",810,13,66,65,Close);
            Fill(Node("HeaderRule",card,28,86,844,2),Gold,false);
            overlay.SetAsLastSibling(); active=this; Resize();
        }
        void Draw(MatchPlayerInfo info,int seat)
        {
            var faceFrame=Node("AvatarFrame",card,36,108,232,210); Fill(faceFrame,Gold,false);
            var faceBacking=Node("AvatarBacking",faceFrame,3,3,226,204); Fill(faceBacking,Ink,false);
            var face=Fill(Node("Avatar",faceBacking,0,0,226,204),Color.white,false);
            face.preserveAspect=true;
            var portrait=face.gameObject.AddComponent<EquipmentPortrait>(); portrait.SetProfile(info.Equipment);
            portrait.Show("기본",MatchAvatarReactions.NeutralForSeat(seat));
            var nickname=Text("Nickname",card,info.Nickname,300,108,415,62,40,Color.white,TextAlignmentOptions.MidlineLeft);
            nickname.fontStyle=FontStyles.Bold;
            float nameWidth=Mathf.Clamp(nickname.GetPreferredValues(info.Nickname).x+6,60,415);
            nickname.rectTransform.sizeDelta=new Vector2(nameWidth,62);
            Text("Level",card,"Lv."+info.Level,300+nameWidth+14,127,128,33,24,Muted,TextAlignmentOptions.MidlineLeft);
            if(info.TodayStreak>=2)
            {
                var streak=Node("TodayStreak",card,300,178,230,49); Fill(streak,Gold,false);
                Text("Text",streak,"금일 "+info.TodayStreak+"연승",4,0,222,49,29,Ink).fontStyle=FontStyles.Bold;
            }
            var prize=Node("TotalPrize",card,300,246,560,72); Fill(prize,new Color32(35,32,23,255),false);
            prize.gameObject.AddComponent<Outline>().effectColor=Gold;
            Text("Caption",prize,"총 획득 상금",14,12,172,48,25,Color.white,TextAlignmentOptions.MidlineLeft);
            Text("Amount",prize,info.TotalPrize.ToString("N0",CultureInfo.InvariantCulture)+" 코인",185,10,360,52,36,Gold,TextAlignmentOptions.MidlineRight).fontStyle=FontStyles.Bold;
            Record("TotalRecord","총 전적",info.Total,350);
            Record("FourBallRecord","4구 전적",info.FourBall,411);
            Record("ThreeBallRecord","3구 전적",info.ThreeBall,472);
            Text("RecordNote",card,"승률은 무승부 제외 · 상금은 누적 승리 상금",36,532,824,24,17,Muted,TextAlignmentOptions.MidlineRight);
            Fill(Node("CueRule",card,30,570,840,2),Gold,false);
            Text("CueCaption",card,"장착 큐대",36,582,205,45,30,Color.white,TextAlignmentOptions.MidlineLeft);
            Text("CueName",card,info.CueName,260,582,600,45,30,Gold,TextAlignmentOptions.MidlineLeft);
            Text("CueDescription",card,info.CueDescription,36,627,824,26,18,Muted,TextAlignmentOptions.MidlineLeft);
            var cue=Fill(Node("CueImage",card,48,650,804,80),Color.white,false);
            cue.preserveAspect=true;
            var cuePortrait=cue.gameObject.AddComponent<EquipmentPortrait>();
            string asset=string.IsNullOrWhiteSpace(info.CueThumbnail)?info.Equipment.CueVisual:info.CueThumbnail;
            cuePortrait.SetProfile(new MatchEquipmentProfile {Emotions=new[]{new MatchEquipmentEmotion {Code="기본",Asset=asset}}});
            cuePortrait.Show("기본",null);
            // A missing catalog image must not leave a white rectangle over the card.
            cue.color=Color.white;
            Text("CueImageFallback",card,"큐대 이미지 미등록",48,671,804,30,18,Muted).gameObject.AddComponent<CueImageFallback>().Image=cue;
            Stat("Power","파워",info.Equipment.Power,"%",36,740);
            Stat("Follow","팔로스루",info.Equipment.FollowThrough,"%",468,740);
            Stat("Tip","당점 범위",info.Equipment.Tip,"%",36,791);
            Stat("ShotSeconds","제한시간",info.Equipment.ShotSeconds,"초",468,791);
        }
        void Record(string name,string caption,PlayerRecord record,float y)
        {
            var row=Node(name,card,36,y,828,52); Fill(row,new Color32(34,41,53,255),false);
            Text("Caption",row,caption,20,0,210,52,29,Gold,TextAlignmentOptions.MidlineLeft);
            Text("WinRate",row,record.WinRate.ToString("F1",CultureInfo.InvariantCulture)+"%",240,0,190,52,35,Color.white).fontStyle=FontStyles.Bold;
            string count=record.Wins.ToString("N0")+"승 / "+record.Losses.ToString("N0")+"패"+(record.Draws>0?" / "+record.Draws.ToString("N0")+"무":"");
            Text("Counts",row,count,437,0,367,52,26,Muted,TextAlignmentOptions.MidlineRight);
        }
        void Stat(string name,string caption,int value,string unit,float x,float y)
        {
            var box=Node(name,card,x,y,396,42); Fill(box,new Color32(34,41,53,255),false);
            Text("Caption",box,caption,14,0,210,42,26,Gold,TextAlignmentOptions.MidlineLeft);
            Text("Value",box,value.ToString("+0;-0;0",CultureInfo.InvariantCulture)+unit,217,0,162,42,29,Color.white,TextAlignmentOptions.MidlineRight);
        }
        public void Close()
        {
            request++;
            if (overlay) { overlay.gameObject.SetActive(false); if(Application.isPlaying) Destroy(overlay.gameObject); else DestroyImmediate(overlay.gameObject); overlay=null; card=null; }
            if(active==this) active=null;
        }
        void Resize() { if(card) card.localScale=Vector3.one*Mathf.Max(.01f,Mathf.Min((overlay.rect.width-30)/Width,(overlay.rect.height-30)/Height)); }
        void Update() { if (overlay && !Current(request)) Close(); }
        void LateUpdate() { Resize(); }
        void OnDisable() { Close(); }
        void OnDestroy() { if(fontMaterial) {if(Application.isPlaying)Destroy(fontMaterial);else DestroyImmediate(fontMaterial);} }
        TMP_Text Text(string name,Transform parent,string value,float x,float y,float w,float h,int size,Color color,TextAlignmentOptions align=TextAlignmentOptions.Center)
        {
            var text=Node(name,parent,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>();
            text.font=font; if(fontMaterial)text.fontSharedMaterial=fontMaterial;
            text.text=value??""; text.richText=false; text.fontSize=size; text.fontSizeMax=size; text.fontSizeMin=15;
            text.enableAutoSizing=true; text.textWrappingMode=TextWrappingModes.NoWrap; text.overflowMode=TextOverflowModes.Ellipsis;
            text.color=color; text.alignment=align; text.raycastTarget=false; return text;
        }
        void Button(string name,Transform parent,string caption,float x,float y,float w,float h,UnityEngine.Events.UnityAction action)
        {
            var rect=Node(name,parent,x,y,w,h); var bg=Fill(rect,new Color32(35,42,54,255));
            var button=rect.gameObject.AddComponent<Button>(); button.targetGraphic=bg; button.onClick.AddListener(action);
            Text("Caption",rect,caption,0,0,w,h,34,Color.white);
        }
        static Image Fill(RectTransform rect,Color color,bool raycast=true) { var image=rect.gameObject.AddComponent<Image>(); image.color=color; image.raycastTarget=raycast; return image; }
        static RectTransform Node(string name,Transform parent,float x,float y,float w,float h)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent,false); rect.gameObject.layer=parent.gameObject.layer;
            rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.up; rect.anchoredPosition=new Vector2(x,-y); rect.sizeDelta=new Vector2(w,h); return rect;
        }
    }
    public sealed class CueImageFallback : MonoBehaviour
    {
        public Image Image;
        void LateUpdate() { if(Image) { bool loaded=Image.overrideSprite; Image.enabled=loaded; GetComponent<TMP_Text>().enabled=!loaded; } }
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PlayerInfoFrame : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r=rectTransform.rect;
            Vector2[] Corners(float inset,float cut) => new[]{new Vector2(inset+cut,r.height-inset),new Vector2(r.width-inset-cut,r.height-inset),new Vector2(r.width-inset,r.height-inset-cut),new Vector2(r.width-inset,inset+cut),new Vector2(r.width-inset-cut,inset),new Vector2(inset+cut,inset),new Vector2(inset,inset+cut),new Vector2(inset,r.height-inset-cut)};
            var outer=Corners(0,24); var inner=Corners(3,22);
            foreach(var p in outer) vh.AddVert(new Vector3(r.xMin+p.x,r.yMin+p.y),new Color32(255,205,66,255),Vector2.zero);
            foreach(var p in inner) vh.AddVert(new Vector3(r.xMin+p.x,r.yMin+p.y),color,Vector2.zero);
            for(int i=0;i<8;i++) { int next=(i+1)%8; vh.AddTriangle(i,next,8+next); vh.AddTriangle(8+next,8+i,i); }
            for(int i=1;i<7;i++) vh.AddTriangle(8,8+i,8+i+1);
        }
    }
}
