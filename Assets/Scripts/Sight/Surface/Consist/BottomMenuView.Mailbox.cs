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
            const float panelWidth = 1340;
            const float bodyHeight = MailListHeight + 60;
            var top = MailTexture("Top");
            var bottom = MailTexture("Bottom");
            var strip = MailTexture("Middle");
            float topHeight = panelWidth * top.height / top.width;
            float bottomHeight = panelWidth * bottom.height / bottom.width;
            float panelHeight = topHeight + bodyHeight + bottomHeight;
            var panel=Node("MailPanel",modal,(Width-panelWidth)/2,(Height-panelHeight)/2,panelWidth,panelHeight);
            Art("Top",panel,top,0,0,panelWidth,topHeight);
            var middle = Art("Middle",panel,strip,0,topHeight,panelWidth,bodyHeight);
            middle.uvRect = new Rect(0,0,1,bodyHeight / (panelWidth * strip.height / strip.width));
            Art("Bottom",panel,bottom,0,topHeight+bodyHeight,panelWidth,bottomHeight);
            Hit("CloseMail",panel,1184,topHeight*.14f,114,topHeight*.59f,CloseModal);
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
            float footerY = topHeight + MailListHeight + 23;
            mailStatus=Text("MailStatus",panel,"",60,footerY,410,45,19,Color.white,TextAlignmentOptions.MidlineLeft);
            MailArt("ReceiveAll",panel,583,footerY,174,54);
            receiveAll=Hit("모두받기",panel,583,footerY,174,54,()=>ClaimMail(null));
            DrawMailRows(false);
            LoadMailbox();
        }

        void TickMailbox()
        {
            if(!Application.isPlaying || !initialized || CurrentPage!=0 || Time.realtimeSinceStartup<nextMailTick) return;
            nextMailTick=Time.realtimeSinceStartup+1;
            if(mailOwner!=GameApiClient.UserId)
            {
                mailItems=Array.Empty<MailItem>();mailLoaded=false;UpdateMailBadge();
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
            if(!mailContent || !mailContent.gameObject.activeInHierarchy) return;
            float offset=preserveScroll?mailContent.anchoredPosition.y:0;
            Clear(mailContent);mailTimers.Clear();
            int height=Math.Max(MailListHeight,mailItems.Length*MailRowHeight);
            mailContent.sizeDelta=new Vector2(MailListWidth,height);
            mailScroll.StopMovement();
            mailScroll.vertical=mailItems.Length>MailVisibleRows;
            mailContent.anchoredPosition=new Vector2(0,Mathf.Clamp(offset,0,height-MailListHeight));
            receiveAll.interactable=!mailBusy && mailItems.Length>0;
            mailStatus.text=mailBusy?"수령 중…":!mailLoaded?"불러오는 중…":"";
            if(mailLoaded && mailItems.Length==0)
                Text("Empty",mailContent,"받을 수 있는 우편이 없습니다.",0,180,MailListWidth,64,23,Color.white);
            for(int i=0;i<mailItems.Length;i++)
            {
                var item=mailItems[i];
                var row=Node("Mail"+item.PostId,mailContent,0,i*MailRowHeight,MailListWidth,60);
                Box("Edge",row,0,0,MailListWidth,60,new Color(.52f,.62f,.65f));
                Box("Fill",row,2,2,MailListWidth-4,56,new Color(.047f,.075f,.106f));
                MailArt("Operator",row,15,7,46,46);
                Text("Sender",row,"운영자",70,0,91,60,20,Color.white);
                Text("Subject",row,item.Message,173,0,503,60,22,Color.white,TextAlignmentOptions.MidlineLeft,true);
                MailArt("Clock",row,689,17,26,26);
                var time=Text("Remaining",row,MailTime(item.ExpiresAt),720,0,145,60,19,Color.white,TextAlignmentOptions.MidlineLeft);
                mailTimers.Add((item,time));
                MailArt(item.Currency,row,884,11,42,38);
                Text("Amount",row,item.Amount.ToString("N0"),935,0,166,60,21,Color.white,TextAlignmentOptions.MidlineLeft,true);
                MailArt("Receive",row,1122,8,109,44);
                Hit("받기",row,1122,8,109,44,()=>ClaimMail(item.PostId)).interactable=!mailBusy;
            }
        }

        async void ClaimMail(long? id)
        {
            if(mailBusy || !mailLoaded || mailOwner!=GameApiClient.UserId) return;
            mailBusy=true; ++mailQuery;mailLoading=false;
            DrawMailRows(true);
            string error=null;
            try
            {
                await GameApiClient.Request<object>(id.HasValue?$"/api/me/mail/{id.Value}/claim":"/api/me/mail/claim-all","POST");
                await BackendGame.Instance.LoadAsync();
            }
            catch(Exception) { error="수령 결과를 확인해 주세요."; }
            finally
            {
                if(this)
                {
                    mailBusy=false;
                    await ReadMail();
                    if(this && mailStatus && error!=null)mailStatus.text=error;
                }
            }
        }
    }
}
