using Assets.Scripts.Often;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Assets.Scripts.Exert.BackSys;
using Assets.Scripts.Prack.BackSys;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Sight.Surface.Consist
{
    public sealed partial class BottomMenuView
    {
        [Serializable] sealed class MailItem { public long PostId, Amount, ExpiresAt; public string Message, Currency; }
        [Serializable] sealed class MailClaim { public long Claimed; }
        readonly List<MailItem> mailSessionRows = new List<MailItem>();
        readonly HashSet<long> mailReceived = new HashSet<long>();
        RectTransform mailSession;
        TMP_Text receiveAllLabel;
        [Serializable] sealed class MailList { public long ServerTime; public MailItem[] Rows; }
        const int MailRowHeight = 64;
        const int MailVisibleRows = 8;
        const int MailListWidth = 1250;
        const int MailListHeight = MailRowHeight * MailVisibleRows;
        readonly Dictionary<string,Texture2D> mailArt = new Dictionary<string,Texture2D>();
        readonly List<(MailItem item,TMP_Text text)> mailTimers = new List<(MailItem,TMP_Text)>();
        MailItem[] mailItems = Array.Empty<MailItem>();
        RectTransform mailBadge, mailContent;
        TMP_Text mailCountText, mailStatus;
        ScrollRect mailScroll;
        Button receiveAll;
        float nextMailTick, nextMailRefresh, mailClockStart;
        long serverTime;
        int mailQuery;
        bool mailBusy, mailLoaded, mailLoading;
        string mailOwner;
        long MailNow => serverTime + (long)(Time.realtimeSinceStartup - mailClockStart);

        Texture2D MailTexture(string key)
        {
            if (!mailArt.TryGetValue(key,out var texture))
                mailArt[key]=texture=Resources.Load<Texture2D>("Menu/Mail/"+key);
            return texture;
        }

        RawImage MailArt(string key,Transform parent,float x,float y,float w,float h)
        {
            var texture = MailTexture(key);
            if (texture)
            {
                float scale = Mathf.Min(w / texture.width, h / texture.height);
                float fittedWidth = texture.width * scale, fittedHeight = texture.height * scale;
                x += (w - fittedWidth) / 2;
                y += (h - fittedHeight) / 2;
                w = fittedWidth;
                h = fittedHeight;
            }
            return Art(key,parent,texture,x,y,w,h);
        }

        void BuildMailBadge()
        {
            mailBadge=Node("ClaimableMailBadge",body,898,150,75,75);
            MailArt("Badge",mailBadge,0,0,75,75);
            mailCountText=Text("Count",mailBadge, "",8,3,59,65,33,Color.white,TextAlignmentOptions.Center,true);
            UpdateMailBadge();
        }

        void UpdateMailBadge()
        {
            if (!mailBadge) return;
            int count=mailItems.Length;
            mailBadge.gameObject.SetActive(mailLoaded && mailOwner==GameApiClient.UserId && count>0);
            mailCountText.text=count.ToString("N0",CultureInfo.InvariantCulture);
        }

        void OpenMailbox()
        {
            CloseModal();
            modal=Node("Mailbox",design,0,0,Width,Height);
            Box("Shade",modal,0,0,Width,Height,new Color(0,0,0,.75f)).raycastTarget=true;
            mailSession = modal;
            mailSessionRows.Clear(); mailReceived.Clear();
            const float panelWidth = 1340, topHeight = 130;
            var panel=Node("MailPanel",modal,(Width-panelWidth)/2,60,panelWidth,820);
            Assets.Scripts.Often.ModalBackdrop.Attach(modal.Find("Shade").GetComponent<UnityEngine.UI.Image>(),panel,CloseModal);
            AttendancePaper("Shadow",panel,0,8,1340,816,new Color(0,0,0,.2f),Color.clear,32);
            AttendancePaper("Paper",panel,0,0,1340,816,new Color(1,.97f,.89f),new Color(.77f,.66f,.4f),32,2);
            AttendancePaper("Header",panel,5,5,1330,130,new Color(.035f,.32f,.26f),new Color(.14f,.43f,.33f),28);
            MailArt("Envelope",panel,24,-3,145,145);
            Text("Title",panel,"우편함",175,29,225,72,53,Color.white,TextAlignmentOptions.MidlineLeft);
            Text("Subtitle",panel,"도착한 선물을 확인하고 보상을 받아 보세요.",425,48,670,45,23,new Color(.88f,.94f,.84f));
            var viewport=Node("MailViewport",panel,45,topHeight+10,MailListWidth,MailListHeight);
            var surface=viewport.gameObject.AddComponent<Image>();
            surface.color=Color.clear; surface.raycastTarget=true;
            viewport.gameObject.AddComponent<RectMask2D>();
            mailScroll=viewport.gameObject.AddComponent<ScrollRect>();
            mailScroll.horizontal=false; mailScroll.vertical=true;
            mailScroll.viewport=viewport;
            mailScroll.movementType=ScrollRect.MovementType.Clamped;
            mailScroll.scrollSensitivity=45;
            mailContent=Node("MailRows",viewport,0,0,MailListWidth,MailListHeight);
            mailScroll.content=mailContent;
            float footerY = topHeight + MailListHeight + 45;
            Box("FooterLine",panel,45,footerY-15,1250,1,new Color(.79f,.72f,.55f));
            mailStatus=Text("MailStatus",panel,"",60,footerY+6,410,45,20,AttendanceInk,TextAlignmentOptions.MidlineLeft);
            MailGloss("ReceiveAllFace",panel,520,footerY,300,60,false);
            receiveAllLabel=Text("ReceiveAllLabel",panel,"모두 받기",520,footerY,300,60,29,Color.white);
            receiveAll=MailHit("모두받기",panel,520,footerY,300,60,()=>ClaimMail(null));
            DrawMailRows(false);
            LoadMailbox();
        }

        void TickMailbox()
        {
            if(!Application.isPlaying || !initialized || CurrentPage!=0 || Time.realtimeSinceStartup<nextMailTick) return;
            nextMailTick=Time.realtimeSinceStartup+1;
            if(mailOwner!=GameApiClient.UserId)
            {
                mailItems=Array.Empty<MailItem>();mailSessionRows.Clear();mailReceived.Clear();mailLoaded=false;UpdateMailBadge();DrawMailRows(true);
            }
            if(mailLoaded && Array.Exists(mailItems,r=>r.ExpiresAt<=MailNow))
            {
                mailItems=Array.FindAll(mailItems,r=>r.ExpiresAt>MailNow);
                UpdateMailBadge(); DrawMailRows(true);
            }
            else if(mailContent) foreach(var row in mailTimers) row.text.text=MailTime(row.item.ExpiresAt);
            if(!mailLoading && !mailBusy && Time.realtimeSinceStartup>=nextMailRefresh) LoadMailbox();
        }

        string MailTime(long expiry)
        {
            long seconds=Math.Max(0,expiry-MailNow);
            if(seconds>=86400) return $"{seconds/86400}일 {seconds%86400/3600}시간";
            if(seconds>=3600) return $"{seconds/3600}시간 {seconds%3600/60}분";
            return $"{(seconds+59)/60}분";
        }

        async void LoadMailbox() { await ReadMail(); }

        async Task ReadMail()
        {
            string owner=GameApiClient.UserId;
            if(!Application.isPlaying || string.IsNullOrEmpty(owner)) return;
            int query=++mailQuery;
            mailLoading=true;nextMailRefresh=Time.realtimeSinceStartup+30;
            try
            {
                var reply=await GameApiClient.Request<MailList>("/api/me/mail");
                if(!this || query!=mailQuery || owner!=GameApiClient.UserId) return;
                mailOwner=owner;mailItems=reply.Rows??Array.Empty<MailItem>();
                serverTime=reply.ServerTime;mailClockStart=Time.realtimeSinceStartup;mailLoaded=true;
                UpdateMailBadge();DrawMailRows(true);
            }
            catch(Exception)
            {
                if(this && query==mailQuery && mailStatus) mailStatus.text="우편 목록을 다시 확인 중입니다.";
                if(this && query==mailQuery) nextMailRefresh=Time.realtimeSinceStartup+5;
            }
            finally { if(this && query==mailQuery) mailLoading=false; }
        }

        void DrawMailRows(bool preserveScroll)
        {
            if(!mailContent || !mailContent.gameObject.activeInHierarchy || modal != mailSession) return;
            mailSessionRows.RemoveAll(r => !mailReceived.Contains(r.PostId) && !mailItems.Any(m => m.PostId == r.PostId));
            foreach(var item in mailItems)
                if(!mailSessionRows.Any(r => r.PostId == item.PostId)) mailSessionRows.Add(item);
            var display = mailSessionRows.ToArray();
            float offset=preserveScroll?mailContent.anchoredPosition.y:0;
            Clear(mailContent);mailTimers.Clear();
            int height=Math.Max(MailListHeight,display.Length*MailRowHeight);
            mailContent.sizeDelta=new Vector2(MailListWidth,height);
            mailScroll.StopMovement();
            mailScroll.vertical=display.Length>MailVisibleRows;
            mailContent.anchoredPosition=new Vector2(0,Mathf.Clamp(offset,0,height-MailListHeight));
            receiveAll.interactable=!mailBusy && mailItems.Length>0;
            receiveAllLabel.text=mailBusy?"받는 중…":"모두 받기";
            mailStatus.text=mailBusy?"수령 중…":!mailLoaded?"불러오는 중…":"";
            if(mailLoaded && display.Length==0)
                Text("Empty",mailContent,"받을 수 있는 우편이 없습니다.",0,180,MailListWidth,64,23,AttendanceInk);
            for(int i=0;i<display.Length;i++)
            {
                var item=display[i];
                bool received=mailReceived.Contains(item.PostId);
                var row=Node("Mail"+item.PostId,mailContent,0,i*MailRowHeight,MailListWidth,60);
                AttendancePaper("Tile",row,0,0,MailListWidth,60,received?new Color(.85f,.89f,.83f):new Color(1,.99f,.94f),new Color(.83f,.78f,.65f),12);
                var ink=received?new Color(.47f,.55f,.48f):AttendanceInk;
                var avatar=MailArt("Operator",row,12,7,46,46);
                if(received) avatar.color=new Color(1,1,1,.45f);
                Text("Sender",row,"운영자",67,0,84,60,20,ink);
                foreach(float x in new[]{162f,735f,905f,1082f})
                    Box("Divider",row,x,16,1,28,new Color(.76f,.71f,.56f,.65f));
                Text("Subject",row,item.Message,181,0,536,60,23,ink,TextAlignmentOptions.MidlineLeft,true);
                var time=Text("Remaining",row,received?"수령 완료":MailTime(item.ExpiresAt),750,0,142,60,18,ink,TextAlignmentOptions.Center,true);
                if(!received) mailTimers.Add((item,time));
                var icon=MailCurrency(item.Currency,row,921,17,29,26);
                if(received) icon.gameObject.AddComponent<CanvasGroup>().alpha=.4f;
                Text("Amount",row,"× "+item.Amount.ToString("N0",CultureInfo.InvariantCulture),957,0,117,60,18,ink,TextAlignmentOptions.MidlineLeft,true);
                MailGloss("ReceiveFace",row,1097,9,137,42,received);
                Text("ReceiveLabel",row,received?"수령 완료":"받기",1097,9,137,42,21,Color.white);
                MailHit("받기",row,1097,9,137,42,()=>ClaimMail(item.PostId)).interactable=!mailBusy && !received;
            }
        }

        async void ClaimMail(long? id)
        {
            if(mailBusy || !mailLoaded || mailOwner!=GameApiClient.UserId || !mailSession || modal!=mailSession) return;
            var opened=mailSession;
            string owner=GameApiClient.UserId;
            var queue=mailSessionRows.Where(r=>!mailReceived.Contains(r.PostId) && (!id.HasValue || r.PostId==id.Value)).ToArray();
            mailBusy=true; ++mailQuery;mailLoading=false;
            DrawMailRows(true);
            string error=null;
            bool walletChanged=false;
            try
            {
                foreach(var item in queue)
                {
                    if(!this || !opened || modal!=opened || owner!=GameApiClient.UserId) break;
                    var result=await GameApiClient.Request<MailClaim>($"/api/me/mail/{item.PostId}/claim","POST");
                    if(!this || owner!=GameApiClient.UserId) return;
                    walletChanged |= result != null && result.Claimed>0;
                    mailItems=mailItems.Where(r=>r.PostId!=item.PostId).ToArray();
                    UpdateMailBadge();
                    if(!opened || modal!=opened) break;
                    if(result==null || result.Claimed==0)
                    {
                        error="이미 수령했거나 기한이 지난 보상입니다.";
                        DrawMailRows(true);
                        continue;
                    }
                    mailReceived.Add(item.PostId);
                    GameAudio.Play(SoundCue.Reward, .65f);
                    DrawMailRows(true);
                    // Keep the currently processed row visible during a long receive-all queue.
                    int index=mailSessionRows.FindIndex(r=>r.PostId==item.PostId);
                    float y=index*MailRowHeight, offset=mailContent.anchoredPosition.y;
                    if(y<offset || y+MailRowHeight>offset+MailListHeight)
                        mailContent.anchoredPosition=new Vector2(0,Mathf.Clamp(y,0,mailContent.sizeDelta.y-MailListHeight));
                    await ShowMailReward(opened,item);
                }
            }
            catch(Exception) { error="수령 결과를 확인해 주세요. 목록을 다시 불러옵니다."; }
            finally
            {
                if(this)
                {
                    // Wallet refresh must not turn a successful receipt into a failed receipt.
                    if(walletChanged && owner==GameApiClient.UserId)
                        try { await BackendGame.Instance.LoadAsync(); }
                        catch(Exception) { error="보상은 수령했습니다. 보유 재화 갱신을 기다려 주세요."; }
                    if(this)
                    {
                        mailBusy=false;
                        await ReadMail();
                        if(this && opened && modal==opened)
                        {
                            DrawMailRows(true);
                            if(error!=null) mailStatus.text=error;
                        }
                    }
                }
            }
        }

        Button MailHit(string name, Transform parent, float x, float y, float w, float h, UnityEngine.Events.UnityAction action)
        {
            var button=Hit(name,parent,x,y,w,h,action);
            var colors=button.colors; colors.disabledColor=Color.clear; button.colors=colors;
            return button;
        }

        static void MailGloss(string name, Transform parent, float x, float y, float w, float h, bool muted)
        {
            AttendancePaper(name+"Shadow",parent,x,y+3,w,h,new Color(0,.12f,.08f,.15f),Color.clear,h/2,0);
            AttendancePaper(name+"Rim",parent,x,y,w,h,muted?new Color(.67f,.78f,.68f):new Color(.91f,.75f,.36f),new Color(1,.94f,.72f),h/2,1);
            var graphic=Node(name,parent,x+3,y+3,w-6,h-6).gameObject.AddComponent<MailGlossGraphic>();
            graphic.Muted=muted; graphic.raycastTarget=false;
        }

        RectTransform MailCurrency(string currency, Transform parent, float x, float y, float w, float h)
        {
            var root=Node("CurrencyIcon",parent,x,y,w,h);
            var texture=currency=="Coin"?Resources.Load<Texture2D>("Gameplay/level-up-Coin"):currency=="Jem"?Resources.Load<Texture2D>("Gameplay/level-up-Jem"):null;
            if(texture)
            {
                float scale=Mathf.Min(w/texture.width,h/texture.height);
                Art("Currency",root,texture,(w-texture.width*scale)/2,(h-texture.height*scale)/2,texture.width*scale,texture.height*scale);
            }
            else
            {
                bool gold=currency=="Gold";
                AttendancePaper("CurrencyBadge",root,0,0,w,h,gold?new Color(1,.79f,.24f):new Color(.29f,.67f,.47f),gold?new Color(.78f,.54f,.13f):new Color(.16f,.45f,.29f),gold?Mathf.Min(w,h)/2:8,2);
                Text("Symbol",root,gold?"G":currency=="Cash"?"C":currency,0,0,w,h,h*.52f,Color.white);
            }
            return root;
        }

        async Task ShowMailReward(RectTransform opened, MailItem item)
        {
            var overlay=Node("RewardOverlay",opened,0,0,Width,Height);
            var group=overlay.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts=true;group.interactable=false;
            Box("RewardDim",overlay,0,0,Width,Height,new Color(0,0,0,.58f)).raycastTarget=true;
            var light=Node("RewardRadiance",overlay,(Width-760)/2,(Height-670)/2,760,670).gameObject.AddComponent<MailRewardLightGraphic>();
            light.raycastTarget=false;
            var card=Node("RewardCard",overlay,(Width-400)/2,(Height-330)/2,400,330);
            // Nested translucent silhouettes create a soft halo without obscuring the inbox.
            for(int i=5;i>=1;i--)
                AttendancePaper("Glow"+i,card,-i*6,-i*6,400+i*12,330+i*12,new Color(1,.83f,.32f,.035f),Color.clear,28+i*5,0);
            AttendancePaper("Card",card,0,0,400,330,new Color(1,.98f,.90f),new Color(.78f,.61f,.28f),28,2);
            Text("Title",card,"보상 획득",35,22,330,40,28,AttendanceInk);
            MailCurrency(item.Currency,card,152,77,96,84);
            Text("Amount",card,"+"+item.Amount.ToString("N0",CultureInfo.InvariantCulture),24,167,352,58,44,AttendanceInk,TextAlignmentOptions.Center,true);
            string currency=item.Currency=="Coin"?"코인":item.Currency=="Jem"?"젬":item.Currency=="Gold"?"골드":item.Currency=="Cash"?"캐시":item.Currency;
            Text("Currency",card,currency,35,228,330,27,19,new Color(.43f,.52f,.41f));
            Box("Line",card,55,267,290,1,new Color(.83f,.76f,.58f));
            Text("Message",card,item.Message,25,279,350,32,23,AttendanceInk,TextAlignmentOptions.Center,true);
            float started=Time.realtimeSinceStartup;
            try
            {
                while(this && opened && modal==opened && overlay)
                {
                    float t=Time.realtimeSinceStartup-started;
                    if(t>=1) break;
                    light.Phase=t; light.SetVerticesDirty();
                    float enter=Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.16f));
                    group.alpha=enter*(1-Mathf.SmoothStep(0,1,Mathf.Clamp01((t-.78f)/.22f)));
                    float scale=Mathf.Lerp(.94f,1,enter);
                    card.localScale=Vector3.one*scale;
                    card.anchoredPosition=new Vector2((Width-400*scale)/2,-(Height-330*scale)/2);
                    await Task.Delay(16);
                }
            }
            finally { if(overlay) { overlay.gameObject.SetActive(false); Dispose(overlay.gameObject); } }
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MailGlossGraphic : MaskableGraphic
    {
        public bool Muted;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r=rectTransform.rect; float radius=r.height/2;
            const int slices=32;
            for(int i=0;i<=slices;i++)
            {
                float t=i/(float)slices, y=r.yMin+t*r.height;
                float inset=radius-Mathf.Sqrt(Mathf.Max(0,radius*radius-(y-r.center.y)*(y-r.center.y)));
                Color c=Color.Lerp(new Color(.005f,.19f,.12f),new Color(.04f,.62f,.39f),t);
                // The narrow upper reflection fades into the darker curved lower surface.
                float shine=Mathf.Exp(-Mathf.Pow((t-.85f)/.105f,2))*.65f;
                c=Color.Lerp(c,new Color(.83f,1,.88f),shine);
                if(Muted) c=Color.Lerp(new Color(.39f,.53f,.44f),new Color(.81f,.89f,.79f),t*.55f+shine*.35f);
                vh.AddVert(new Vector2(r.xMin+inset,y),c,Vector2.zero);
                vh.AddVert(new Vector2(r.xMax-inset,y),Color.Lerp(c,Muted?new Color(.46f,.60f,.50f):new Color(.01f,.28f,.17f),.24f),Vector2.zero);
                if(i>0){int n=i*2;vh.AddTriangle(n-2,n,n-1);vh.AddTriangle(n-1,n,n+1);}
            }
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MailRewardLightGraphic : MaskableGraphic
    {
        public float Phase;
        static Color Gold(float alpha) => new Color(1,.75f,.22f,alpha);
        static Vector2 Direction(float angle) => new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
        static void Triangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color ca, Color cb, Color cc)
        {
            int n=vh.currentVertCount;vh.AddVert(a,ca,Vector2.zero);vh.AddVert(b,cb,Vector2.zero);vh.AddVert(c,cc,Vector2.zero);vh.AddTriangle(n,n+1,n+2);
        }
        static void Bloom(VertexHelper vh, Vector2 center, float rx, float ry, Color tint)
        {
            for(int i=0;i<64;i++)
            {
                Vector2 a=Direction(i*Mathf.PI/32),b=Direction((i+1)*Mathf.PI/32);
                Triangle(vh,center,center+Vector2.Scale(a,new Vector2(rx,ry)),center+Vector2.Scale(b,new Vector2(rx,ry)),tint,Color.clear,Color.clear);
            }
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();Vector2 center=rectTransform.rect.center;
            float pulse=.85f+.15f*Mathf.Sin(Phase*19);
            Bloom(vh,center,370,320,Gold(.65f*pulse));
            Bloom(vh,center,295,260,new Color(1,.93f,.60f,.72f));
            // Radial rays spread with the receipt, each with a different flicker phase.
            for(int i=0;i<44;i++)
            {
                float angle=i*2.39996f+Phase*.09f;
                float length=245+(i%7)*14+Phase*45;
                float a=(.16f+.15f*Mathf.Sin(i*3.1f+Phase*15))*pulse;
                Triangle(vh,center+Direction(angle)*80,center+Direction(angle-.018f)*length,center+Direction(angle+.018f)*length,Gold(a),Gold(0),Gold(0));
            }
            // Flowing golden ribbons outside the card, with soft luminous edges.
            for(int ribbon=0;ribbon<3;ribbon++)
            {
                Vector2 previous=Vector2.zero;
                for(int j=0;j<=72;j++)
                {
                    float angle=j/72f*Mathf.PI*2;
                    float wave=Mathf.Sin(angle*4-Phase*9+ribbon*2)*9;
                    Vector2 p=center+new Vector2(Mathf.Cos(angle)*(255+wave+ribbon*12),Mathf.Sin(angle)*(211+wave+ribbon*7));
                    if(j>0)
                    {
                        float alpha=(.12f+.28f*Mathf.Pow(Mathf.Max(0,Mathf.Sin(angle*3-Phase*13+ribbon)),4))*pulse;
                        Vector2 normal=(p-center).normalized*(1.5f+ribbon);
                        Triangle(vh,previous-normal,p-normal,p+normal,Gold(0),Gold(alpha),Gold(0));
                        Triangle(vh,previous-normal,p+normal,previous+normal,Gold(alpha),Gold(0),Gold(0));
                    }
                    previous=p;
                }
            }
            for(int i=0;i<30;i++)
            {
                float angle=i*2.39996f;
                Vector2 p=center+Vector2.Scale(Direction(angle),new Vector2(240+(i%4)*20+Phase*32,200+(i%5)*17+Phase*27));
                float flicker=.25f+.75f*Mathf.Pow(Mathf.Sin(Phase*16+i*2),2);
                float size=(4+i%4)*flicker;
                Bloom(vh,p,size*3,size*3,Gold(.55f*flicker));
                for(int k=0;k<4;k++)
                {
                    Vector2 tip=p+Direction(k*Mathf.PI/2)*size*1.8f;
                    Vector2 left=p+Direction(k*Mathf.PI/2-.78f)*size*.28f;
                    Vector2 right=p+Direction(k*Mathf.PI/2+.78f)*size*.28f;
                    Triangle(vh,left,tip,right,new Color(1,1,.84f,flicker),Gold(.1f),new Color(1,1,.84f,flicker));
                }
            }
        }
    }

}
