using System;
using System.Linq;
using System.Threading.Tasks;
using Assets.Scripts.Exert.BackSys;
using Assets.Scripts.Exert.Network;
using Assets.Scripts.Often;
using TMPro;
using UnityEngine;

namespace Assets.Scripts.Sight.Surface.Consist
{
    public sealed partial class BottomMenuView
    {
        void DrawPlayerLocker()
        {
            if(!playerLocker)return;Clear(playerLocker);
            PlayerHeading(playerLocker,"사물함",0,0,190,43);
            PlayerButton("LockerCue",playerLocker,"큐대",225,0,180,43,()=>{playerTab=0;playerCuePage=0;DrawPlayerLocker();},playerTab==0);
            PlayerButton("LockerMessages",playerLocker,"메시지",420,0,180,43,()=>{playerTab=1;playerCuePage=0;DrawPlayerLocker();},playerTab==1);
            if(playerTab==0)DrawPlayerCues();else DrawPlayerMessages();
        }
        ShopProductData[] PlayerOwned(string kind)=>playerItems.Where(p=>p.Owned && p.Kind==kind).OrderByDescending(p=>p.Equipped).ThenBy(p=>p.Name).ThenBy(p=>p.Id).ToArray();
        void DrawPlayerCues()
        {
            var items=PlayerOwned("Cue");
            if(items.Length==0){Text("EmptyCues",playerLocker,"보유한 큐대가 없습니다. 기본 큐대를 사용 중입니다.",50,105,1330,90,30,AttendanceInk);return;}
            int pages=(items.Length+2)/3;playerCuePage=Mathf.Clamp(playerCuePage,0,pages-1);
            for(int i=0;i<3 && playerCuePage*3+i<items.Length;i++){
                var cue=items[playerCuePage*3+i];
                var row=Node("PlayerCue"+cue.Id,playerLocker,0,57+i*78,1430,75);
                if(cue.Equipped)AttendancePaper("EquippedRow",row,-8,0,1446,72,new Color(.88f,.94f,.77f,.85f),new Color(.57f,.69f,.38f),13,1);
                Text("CueName",row,cue.Name,0,5,230,34,29,AttendanceInk,TextAlignmentOptions.MidlineLeft,true);
                Text("CueGrade",row,(cue.CueAbility?.Grade??"A")+"급",0,41,80,27,21,PlayerGreen,TextAlignmentOptions.MidlineLeft);
                PlayerCueArt(row,cue,255,1,760,37);
                Text("CueAbilities",row,PlayerCueSummary(cue),95,39,950,31,20,AttendanceInk,TextAlignmentOptions.MidlineLeft,true);
                string progress=PlayerCueProgress(cue);
                Text("CueProgress",row,progress,1020,2,200,31,20,AttendanceInk,TextAlignmentOptions.Center,true);
                float fraction=progress.Contains("MAX")?1:cue.CueAbility==null?0:(float)(cue.CuePending%cue.CueAbility.Reach)/cue.CueAbility.Reach;
                AttendancePaper("GrowthTrack",row,1040,40,160,12,new Color(.82f,.83f,.73f),Color.clear,6);
                if(fraction>0)AttendancePaper("GrowthFill",row,1040,40,160*fraction,12,PlayerGreen,Color.clear,6);
                PlayerButton("CueDetails",row,cue.Equipped?"장착 중 · 상세":"상세 · 장착",1235,14,188,43,()=>OpenPlayerCue(cue),!cue.Equipped);
                Box("RowRule",row,0,74,1430,1,PlayerLine);
            }
            if(pages>1)PlayerPager(playerLocker,pages,playerCuePage,301,()=>{playerCuePage--;DrawPlayerLocker();},()=>{playerCuePage++;DrawPlayerLocker();});
        }
        string PlayerCueSummary(ShopProductData cue)
        {
            if(cue.CueAbility==null)return cue.CueDetail.Replace("\n"," · ");
            var a=cue.CueAbility;var l=a.LevelsFromSteps(cue.CueSteps+cue.CuePending/a.Reach);
            int seconds=(int)Math.Round(cue.CueTimeMin+(cue.CueTimeMax-cue.CueTimeMin)*a.Percent(l[3])/100.0,MidpointRounding.AwayFromZero);
            return $"파워 {a.Percent(l[0])}% · 팔로스루 {a.Percent(l[1])}% · 당점 {a.Percent(l[2])}% · 시간 {seconds}초 · 가이드 {a.GuidePercent(cue.CueSteps+cue.CuePending/a.Reach)}%";
        }
        string PlayerCueProgress(ShopProductData cue)
        {
            if(cue.CueAbility==null)return "성장 정보 없음";
            var a=cue.CueAbility;var levels=a.LevelsFromSteps(cue.CueSteps+cue.CuePending/a.Reach);
            return levels.SequenceEqual(a.Maximum)?"최고 레벨 MAX":$"진도 {cue.CuePending%a.Reach:N0} / {a.Reach:N0}";
        }
        void PlayerCueArt(Transform parent,ShopProductData cue,float x,float y,float w,float h)
        {
            var image=Art("CueSkin",parent,null,x,y,w,h);image.color=Color.clear;
            var loader=image.gameObject.AddComponent<PlayerPageArt>();
            loader.Load(string.IsNullOrWhiteSpace(cue.Visual)?cue.Thumbnail:cue.Visual);
            PremiumCuePreview.Attach(image,cue.Visual);
        }
        void PlayerPager(Transform parent,int pages,int current,float y,UnityEngine.Events.UnityAction previous,UnityEngine.Events.UnityAction next)
        {
            PlayerButton("PreviousPage",parent,"‹ 이전",1030,y,120,33,previous,false).interactable=current>0;
            Text("PageCount",parent,$"{current+1} / {pages}",1155,y,125,33,21,AttendanceInk);
            PlayerButton("NextPage",parent,"다음 ›",1290,y,120,33,next,false).interactable=current+1<pages;
        }
        void DrawPlayerMessages()
        {
            var items=PlayerOwned("MessagePack");
            if(items.Length==0){Text("EmptyMessages",playerLocker,"보유한 메시지 묶음이 없습니다. 상점에서 확인해 주세요.",50,105,1330,90,29,AttendanceInk);return;}
            var selected=items.FirstOrDefault(p=>p.Id==playerMessageId)??items[0];playerMessageId=selected.Id;
            int pages=(items.Length+2)/3;playerCuePage=Mathf.Clamp(playerCuePage,0,pages-1);
            for(int i=0;i<3 && playerCuePage*3+i<items.Length;i++){
                var item=items[playerCuePage*3+i];float y=62+i*67;
                AttendancePaper("MessageRow",playerLocker,0,y,570,58,item.Id==selected.Id?new Color(.85f,.94f,.72f):PlayerCream,PlayerLine,12);
                AttendancePaper("SpeechIcon",playerLocker,17,y+17,31,25,PlayerGreen,Color.clear,7);
                Text("SpeechDots",playerLocker,"…",18,y+7,30,30,22,Color.white);
                Text("MessageName",playerLocker,item.Name,62,y,342,58,28,AttendanceInk,TextAlignmentOptions.MidlineLeft,true);
                Text("MessageState",playerLocker,item.Equipped?"장착 중":"미장착",410,y,145,58,21,PlayerGreen);
                Hit("PreviewMessage"+item.Id,playerLocker,0,y,570,58,()=>{playerMessageId=item.Id;DrawPlayerLocker();});
            }
            Text("MessagePreviewTitle",playerLocker,selected.Name+" · 메시지 미리보기",640,55,765,37,25,AttendanceInk,TextAlignmentOptions.MidlineLeft,true);
            var viewport=Node("MessagePreviewViewport",playerLocker,640,95,765,160);
            var mask=viewport.gameObject.AddComponent<UnityEngine.UI.Image>();mask.color=Color.clear;
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var content=Node("Messages",viewport,0,0,765,Mathf.Max(160,selected.Messages.Length*34));
            var scroll=viewport.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.scrollSensitivity=28;scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;
            for(int i=0;i<selected.Messages.Length;i++){
                AttendancePaper("MessageLine",content,0,i*34,760,30,new Color(1,.99f,.94f),PlayerLine,9);
                Text("Phrase"+i,content,selected.Messages[i],18,i*34,724,30,22,AttendanceInk,TextAlignmentOptions.MidlineLeft,true);
            }
            if(selected.Messages.Length==0)Text("NoPhrases",content,"등록된 메시지가 없습니다.",12,0,730,50,22,AttendanceInk);
            PlayerButton("EquipMessage",playerLocker,playerSaving?"저장 중…":selected.Equipped?"장착 해제":"장착하기",814,274,370,44,()=>SetPlayerEquipment(selected,!selected.Equipped)).interactable=!playerSaving;
            if(pages>1){
                PlayerButton("PreviousMessages",playerLocker,"‹",0,276,90,39,()=>{playerCuePage--;DrawPlayerLocker();},false).interactable=playerCuePage>0;
                Text("MessagePages",playerLocker,$"{playerCuePage+1} / {pages}",100,276,120,39,21,AttendanceInk);
                PlayerButton("NextMessages",playerLocker,"›",230,276,90,39,()=>{playerCuePage++;DrawPlayerLocker();},false).interactable=playerCuePage+1<pages;
            }
        }
        async void SetPlayerEquipment(ShopProductData product,bool equipped)
        {
            if(playerSaving || !Application.isPlaying || !playerPage || string.IsNullOrEmpty(playerOwner))return;
            string owner=playerOwner;int request=playerRequest;
            if(product.Id==0){product=playerItems.FirstOrDefault(p=>p.Kind=="Character" && p.Equipped);equipped=false;if(product==null)return;}
            playerSaving=true;playerStatus.text="장착 상태를 저장하고 있습니다…";
            try {
                await GameApiClient.Request<object>("/api/me/equipment","POST",new ShopEquipmentInput{Slot=product.Kind,ProductId=product.Id,Equipped=equipped});
                if(owner!=GameApiClient.UserId)return;
                // Server confirmation owns all visual state; do not optimistically equip an item.
                var inventory=await GameApiClient.Request<ShopCatalogData>("/api/me/inventory");
                await MatchEquipmentRuntime.RefreshLocalAsync();
                if(!PlayerCurrent(request))return;
                playerItems=inventory?.Products??Array.Empty<ShopProductData>();
                playerSaving=false;ClosePlayerDialog();DrawPlayerHeader();DrawPlayerLocker();
                headerRequest++;headerOwner=owner;ShowHeaderCharacter(playerItems.FirstOrDefault(p=>p.Kind=="Character" && p.Equipped));
                playerStatus.text=equipped?"장착했습니다. 다음 경기·연습 입장부터 반영됩니다.":"장착을 해제했습니다.";
            } catch(Exception){if(PlayerCurrent(request))playerStatus.text="장착 결과를 확인하지 못했습니다. 새로고침 후 다시 확인해 주세요.";}
            finally{playerSaving=false;if(this && playerPage && playerOwner==owner && request!=playerRequest)ReloadPlayerPage();}
        }
    }
}
