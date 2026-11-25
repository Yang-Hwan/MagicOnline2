using System;
using System.Linq;
using System.Threading.Tasks;
using Assets.Scripts.Exert.BackSys;
using Assets.Scripts.Exert.Network;
using Assets.Scripts.Often;
using Assets.Scripts.Prack.BackSys.GameData;
using Assets.Scripts.Prack.BackSys;
using Assets.Scripts.Sight.Surface.Pavilion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Sight.Surface.Consist
{
    public sealed partial class BottomMenuView
    {
        RectTransform playerPage,playerBoard,playerHeader,playerRecords,playerLocker,playerDialog;
        TMP_Text playerStatus;
        MatchPlayerInfo playerInformation;
        ShopProductData[] playerItems=Array.Empty<ShopProductData>();
        string playerOwner;
        int playerRequest,playerTab,playerCuePage,playerCharacterPage;
        long playerMessageId,playerCharacterId;
        bool playerSaving,playerLoading;
        static readonly Color PlayerGreen=new Color(.035f,.32f,.23f),PlayerCream=new Color(1,.975f,.89f),PlayerLine=new Color(.76f,.69f,.49f);
        Texture2D PlayerBoardArt=>Resources.Load<Texture2D>("Menu/PlayerInfo/PaperBoard");
        bool PlayerCurrent(int request)=>this && isActiveAndEnabled && playerPage && playerPage.gameObject.activeInHierarchy && request==playerRequest && playerOwner==GameApiClient.UserId;
        void OpenPlayerPage(int tab=0)
        {
            CloseModal();ClosePlayerPage();
            playerCueScroll=0;playerOwner=GameApiClient.UserId;playerTab=Mathf.Clamp(tab,0,1);playerCuePage=playerCharacterPage=0;
            playerItems=Array.Empty<ShopProductData>();playerInformation=null;playerMessageId=0;
            playerPage=Node("PlayerPage",design,0,0,Width,Height);
            Box("OpaquePage",playerPage,0,0,Width,Height,Color.black).raycastTarget=true;
            Art("HomeAtmosphere",playerPage,plates[0],0,0,Width,Height);
            Box("OldHeaderCover",playerPage,0,70,Width,62,new Color(.025f,.035f,.03f));
            Box("OldNavigationCover",playerPage,0,835,Width,106,new Color(.025f,.035f,.03f));
            Box("TopBar",playerPage,0,0,Width,75,new Color(.025f,.035f,.03f));
            Box("BottomBar",playerPage,0,887,Width,54,new Color(.025f,.035f,.03f));
            Text("BackArrow",playerPage,"‹",16,0,70,70,54,PlayerCream);
            Hit("PlayerBack",playerPage,0,0,100,75,ClosePlayerPage);
            PlayerHeading(playerPage,"선수정보",105,9,320,55);
            var wallet=BackendGame.Instance.UserMainData;
            PlayerCurrency(playerPage,"Coin",wallet.Coin,1085,20);
            PlayerCurrency(playerPage,"Jem",wallet.Jem,1370,20);
            playerBoard=Node("PlayerBoard",playerPage,66,78,1540,802);
            AttendancePaper("BoardOpaquePaper",playerBoard,35,57,1470,710,PlayerCream,Color.clear,35);
            AttendancePaper("BoardOpaqueHeader",playerBoard,38,63,1464,188,PlayerGreen,Color.clear,34);
            Art("PaperBoard",playerBoard,PlayerBoardArt,0,0,1540,802);
            playerHeader=Node("PlayerHeader",playerBoard,0,0,1540,253);
            playerRecords=Node("PlayerRecords",playerBoard,55,266,1430,126);
            playerLocker=Node("PlayerLocker",playerBoard,55,405,1430,327);
            playerStatus=Text("PlayerStatus",playerBoard,"불러오는 중…",60,745,1120,34,21,AttendanceInk,TextAlignmentOptions.MidlineLeft,true);
            PlayerButton("PlayerRefresh",playerBoard,"새로고침",1230,738,245,43,()=>ReloadPlayerPage(),false);
            DrawPlayerHeader();DrawPlayerRecords();DrawPlayerLocker();
            ReloadPlayerPage();
        }
        void ClosePlayerPage()
        {
            ClosePlayerDialog();playerRequest++;playerLoading=false;
            if(playerPage){playerPage.gameObject.SetActive(false);Dispose(playerPage.gameObject);playerPage=null;}
            playerOwner=null;playerInformation=null;playerItems=Array.Empty<ShopProductData>();
        }
        async void ReloadPlayerPage()
        {
            int request=++playerRequest;
            if(!playerPage)return;
            if(!Application.isPlaying || string.IsNullOrEmpty(playerOwner)){
                playerStatus.text="로그인 후 선수정보와 보유장비를 확인할 수 있습니다.";return;
            }
            playerLoading=true;playerStatus.text="선수정보와 사물함을 불러오는 중…";
            try {
                var information=GameApiClient.Request<MatchPlayerInfo>("/api/me/player-information");
                var inventory=GameApiClient.Request<ShopCatalogData>("/api/me/inventory");
                await Task.WhenAll(information,inventory);
                if(!PlayerCurrent(request))return;
                playerInformation=await information;playerItems=(await inventory)?.Products??Array.Empty<ShopProductData>();
                if(playerInformation.UserId!=playerOwner)throw new InvalidOperationException("PlayerChanged");
                playerStatus.text="장착 변경은 다음 경기·연습 입장부터 반영됩니다. 메시지는 여러 묶음을 선택할 수 있어요.";
                DrawPlayerHeader();DrawPlayerRecords();DrawPlayerLocker();
            } catch(Exception){if(PlayerCurrent(request))playerStatus.text="정보를 불러오지 못했습니다. 새로고침으로 다시 확인해 주세요.";}
            finally{if(PlayerCurrent(request))playerLoading=false;}
        }
        void PlayerCurrency(Transform parent,string kind,long value,float x,float y)
        {
            Art(kind+"Icon",parent,Resources.Load<Texture2D>("Gameplay/level-up-"+kind),x,y,40,35);
            Text(kind+"Balance",parent,value.ToString("N0"),x+47,y-1,200,40,27,Color.white,TextAlignmentOptions.MidlineLeft,true);
        }
        void PlayerHeading(Transform parent,string title,float x,float y,float w,float h)
        {
            var ribbon=Node("BrushTitle",parent,x,y,w,h).gameObject.AddComponent<PlayerBrushGraphic>();ribbon.color=Yellow;ribbon.raycastTarget=false;
            Text("Heading",parent,title,x,y,w,h,35,Color.black);
        }
        Button PlayerButton(string id,Transform parent,string label,float x,float y,float w,float h,UnityEngine.Events.UnityAction action,bool primary=true)
        {
            AttendancePaper(id+"Face",parent,x,y,w,h,primary?PlayerGreen:PlayerCream,primary?PlayerGreen:PlayerLine,h*.5f,1.5f);
            Text(id+"Label",parent,label,x+8,y,w-16,h,24,primary?Color.white:AttendanceInk,TextAlignmentOptions.Center,true);
            return Hit(id,parent,x,y,w,h,action);
        }
        void PlayerPencil(Transform parent,float x,float y,float size,Color color)
        {
            var graphic=Node("EditPencil",parent,x,y,size,size).gameObject.AddComponent<PlayerPencilGraphic>();graphic.color=color;graphic.raycastTarget=false;
        }
        EquipmentPortrait PlayerPortrait(string id,Transform parent,ShopProductData character,float x,float y,float size,string emotion="기본")
        {
            AttendancePaper(id+"Frame",parent,x-4,y-4,size+8,size+8,PlayerCream,PlayerLine,15,2);
            var image=Box(id,parent,x,y,size,size,Color.white);
            image.preserveAspect=true;
            var portrait=image.gameObject.AddComponent<EquipmentPortrait>();
            portrait.SetProfile(new MatchEquipmentProfile{Emotions=character?.Emotions?.Select(e=>new MatchEquipmentEmotion{Code=e.Code,Asset=e.Asset}).ToArray()??Array.Empty<MatchEquipmentEmotion>()});
            portrait.Show(emotion,MatchAvatarReactions.Anonymous);return portrait;
        }
        void DrawPlayerHeader()
        {
            if(!playerHeader)return;Clear(playerHeader);
            var cached=BackendGame.Instance.UserMainData;
            var coinLabel=playerPage.Find("CoinBalance")?.GetComponent<TMP_Text>();if(coinLabel)coinLabel.text=cached.Coin.ToString("N0");
            var gemLabel=playerPage.Find("JemBalance")?.GetComponent<TMP_Text>();if(gemLabel)gemLabel.text=cached.Jem.ToString("N0");
            var character=playerItems.FirstOrDefault(p=>p.Kind=="Character" && p.Equipped);
            PlayerPortrait("PlayerAvatar",playerHeader,character,60,66,154);
            Hit("ChangeCharacter",playerHeader,50,55,174,177,OpenPlayerCharacters);
            PlayerPencil(playerHeader,182,190,34,PlayerCream);
            string name=playerInformation?.Nickname??(UserInfo.DisplayName??"플레이어");
            Text("PlayerNickname",playerHeader,name,253,76,585,59,46,Color.white,TextAlignmentOptions.MidlineLeft,true);
            PlayerPencil(playerHeader,851,84,40,PlayerCream);
            Hit("ChangeNickname",playerHeader,245,70,660,72,OpenPlayerNickname);
            int level=playerInformation?.Level??cached.LvVel,progress=playerInformation?.LevelProgress??cached.LvProc;
            int target=BackendChart.commonCodeData?.FirstOrDefault(c=>c.ComCd=="LvPlayMax" && c.IsUse=="Y")?.ComVal??0;
            Text("PlayerLevel",playerHeader,"Lv."+level,253,140,160,45,32,Yellow,TextAlignmentOptions.MidlineLeft);
            AttendancePaper("LevelTrack",playerHeader,253,190,400,17,new Color(.02f,.15f,.1f),Color.clear,8);
            float fill=target>0?Mathf.Clamp01((float)progress/target):0;
            if(fill>0)AttendancePaper("LevelFill",playerHeader,253,190,400*fill,17,Yellow,Color.clear,8);
            Text("LevelProgress",playerHeader,target>0?$"{progress:N0} / {target:N0}":$"{progress:N0} / —",671,177,230,38,24,PlayerCream,TextAlignmentOptions.MidlineLeft,true);
            Text("LevelRemaining",playerHeader,target>0?$"다음 레벨까지 {Math.Max(0,target-progress):N0}":"레벨 진도 기준 확인 중",253,212,650,27,19,PlayerCream,TextAlignmentOptions.MidlineLeft);
            Box("HeaderDivider",playerHeader,958,81,1.5f,145,new Color(.9f,.84f,.62f,.6f));
            Art("PrizeIcon",playerHeader,Resources.Load<Texture2D>("Gameplay/level-up-Coin"),1003,105,57,52);
            Text("PrizeHeading",playerHeader,"총획득상금",1078,95,350,42,28,PlayerCream,TextAlignmentOptions.MidlineLeft);
            Text("TotalPrize",playerHeader,playerInformation?.TotalPrize.ToString("N0")??"—",1003,151,455,65,49,Yellow,TextAlignmentOptions.MidlineLeft,true);
        }
        void DrawPlayerRecords()
        {
            if(!playerRecords)return;Clear(playerRecords);
            string[] titles={"전체 전적","3구 전적","4구 전적"};
            var records=new[]{playerInformation?.Total,playerInformation?.ThreeBall,playerInformation?.FourBall};
            for(int i=0;i<3;i++){
                float x=i*477;
                if(i>0)Box("RecordDivider",playerRecords,x-15,3,1,113,PlayerLine);
                Text("RecordTitle"+i,playerRecords,titles[i],x,0,445,31,26,AttendanceInk);
                Text("RecordRate"+i,playerRecords,records[i]==null?"—":records[i].WinRate.ToString("F1")+"%",x,24,445,64,46,PlayerGreen,autoSize:true);
                Text("RecordSummary"+i,playerRecords,records[i]==null?"기록 확인 중":$"{records[i].Wins:N0}승 {records[i].Losses:N0}패 {records[i].Draws:N0}무",x,88,445,34,23,AttendanceInk,TextAlignmentOptions.Center,true);
            }
            Box("RecordRule",playerRecords,0,124,1430,1,PlayerLine);
        }
    }
}
