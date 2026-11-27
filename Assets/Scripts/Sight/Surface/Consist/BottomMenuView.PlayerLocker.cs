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
        static readonly Color CueIvory=new Color(.96f,.99f,1),CueQuiet=new Color(.66f,.78f,.88f);
        RectTransform CueChrome(string name,Transform parent,float x,float y,float w,float h,CueLockerGraphic.Finish finish)
        {
            var node=Node(name,parent,x,y,w,h);var graphic=node.gameObject.AddComponent<CueLockerGraphic>();graphic.finish=finish;graphic.raycastTarget=false;return node;
        }
        UnityEngine.UI.Button CueButton(string id,Transform parent,string label,float x,float y,float w,float h,UnityEngine.Events.UnityAction action,bool gold,bool enabled=true)
        {
            CueChrome(id+"Face",parent,x,y,w,h,enabled?(gold?CueLockerGraphic.Finish.Gold:CueLockerGraphic.Finish.Blue):CueLockerGraphic.Finish.Muted);
            Text(id+"Label",parent,label,x+8,y,w-16,h,h>60?33:26,enabled?(gold?new Color(.13f,.08f,.015f):CueIvory):CueQuiet);
            var button=Hit(id,parent,x,y,w,h,action);button.interactable=enabled;return button;
        }
        float playerCueScroll;
        static bool CueCanGrow(ShopProductData cue)=>cue.CueBowls!=null&&!cue.CueBowls.Auto&&Enumerable.Range(0,5).Any(i=>!CueMaxed(cue,i)&&cue.CueBowls.Ready(i));
        void DrawPlayerCues()
        {
            var items=PlayerOwned("Cue");
            if(items.Length==0){Text("EmptyCues",playerLocker,"보유한 큐대가 없습니다. 기본 큐대를 사용 중입니다.",50,105,1330,90,30,AttendanceInk);return;}
            const float cardWidth=920,spacing=30;
            CueChrome("CueListBackdrop",playerLocker,-6,53,1442,254,CueLockerGraphic.Finish.Background);
            var viewport=Node("CueViewport",playerLocker,0,58,1430,250);
            var surface=viewport.gameObject.AddComponent<UnityEngine.UI.Image>();surface.color=Color.clear;
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var content=Node("CueCards",viewport,0,0,Mathf.Max(1430,items.Length*(cardWidth+spacing)-spacing),250);
            var scroll=viewport.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();scroll.viewport=viewport;scroll.content=content;
            scroll.horizontal=true;scroll.vertical=false;scroll.scrollSensitivity=45;scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;
            for(int i=0;i<items.Length;i++){
                var cue=items[i];var card=Node("PlayerCue"+cue.Id,content,i*(cardWidth+spacing),0,cardWidth,240);
                AttendancePaper("CardShadow",card,3,6,cardWidth-6,232,new Color(0,0,0,.12f),Color.clear,22);
                CueChrome("CardFace",card,0,0,cardWidth,230,CueLockerGraphic.Finish.Panel);
                CueChrome("CardTitleBand",card,2,2,916,67,CueLockerGraphic.Finish.Header);
                CueChrome("GradeBadge",card,25,15,88,43,CueLockerGraphic.Finish.Blue);
                Text("CueGrade",card,(cue.CueAbility?.Grade??"A")+"급",25,15,88,43,26,Color.white);
                Text("CueName",card,cue.Name,137,11,510,49,34,CueIvory,TextAlignmentOptions.Center,true);
                Text("EquipState",card,cue.Equipped?"장착중":"장착하기",674,15,215,42,25,CueIvory);
                CueChrome("SkinWell",card,26,84,868,86,CueLockerGraphic.Finish.Inset);
                PlayerCueArt(card,cue,49,96,820,61);
                Text("DetailHint",card,"큐대 상세 보기  ›",29,183,500,29,20,CueQuiet,TextAlignmentOptions.MidlineLeft);
                if(CueCanGrow(cue)){
                    CueChrome("GrowthNotice",card,710,181,180,35,CueLockerGraphic.Finish.Gold);
                    Text("GrowthNoticeLabel",card,"성장!",710,181,180,35,25,new Color(.13f,.08f,.015f));
                }
                // One card target: the growth badge is an indicator, never a drink action.
                Hit("CueDetails",card,0,0,cardWidth,230,()=>{if(!playerSaving)OpenPlayerCue(cue);});
            }
            scroll.horizontalNormalizedPosition=playerCueScroll;
            scroll.onValueChanged.AddListener(v=>playerCueScroll=v.x);
            Text("CueScrollHint",playerLocker,"좌우로 밀어 큐대를 확인하세요",0,309,1430,22,17,AttendanceInk);
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
                GameAudio.Play(SoundCue.Equip, .7f);
                playerStatus.text=equipped?"장착했습니다. 다음 경기·연습 입장부터 반영됩니다.":"장착을 해제했습니다.";
            } catch(Exception){if(PlayerCurrent(request))playerStatus.text="장착 결과를 확인하지 못했습니다. 새로고침 후 다시 확인해 주세요.";}
            finally{playerSaving=false;if(this && playerPage && playerOwner==owner && request!=playerRequest)ReloadPlayerPage();}
        }
    }
}
