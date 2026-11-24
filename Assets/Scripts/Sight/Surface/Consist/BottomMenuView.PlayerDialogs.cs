using System;
using System.Linq;
using Assets.Scripts.Exert.BackSys;
using Assets.Scripts.Prack.BackSys;
using Assets.Scripts.Often;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Sight.Surface.Consist
{
    public sealed partial class BottomMenuView
    {
        void ClosePlayerDialog(){if(playerDialog){playerDialog.gameObject.SetActive(false);Dispose(playerDialog.gameObject);playerDialog=null;}}
        RectTransform PlayerDialog(string title,float width,float height)
        {
            ClosePlayerDialog();
            playerDialog=Node("PlayerDialog",playerPage,0,0,Width,Height);
            var shade=Box("Shade",playerDialog,0,0,Width,Height,new Color(0,0,0,.68f));
            var card=Node("PlayerDialogCard",playerDialog,(Width-width)/2,(Height-height)/2,width,height);
            AttendancePaper("DialogOpaquePaper",card,16,31,width-32,height-57,PlayerCream,Color.clear,28);
            AttendancePaper("DialogOpaqueHeader",card,width*.026f,height*.075f,width*.948f,height*.235f,PlayerGreen,Color.clear,28);
            Art("PaperBoard",card,PlayerBoardArt,0,0,width,height);
            ModalBackdrop.Attach(shade,card,ClosePlayerDialog);
            Text("DialogTitle",card,title,40,58,width-80,height*.17f,41,Color.white);
            Text("DismissHint",playerDialog,"바깥 영역을 누르면 닫힙니다",(Width-width)/2,(Height+height)/2+4,width,31,21,PlayerCream);
            return card;
        }
        void OpenPlayerNickname()
        {
            if(!playerPage || playerSaving)return;
            var card=PlayerDialog("닉네임 변경",930,550);
            string current=playerInformation?.Nickname??UserInfo.DisplayName??"";
            Text("CurrentNicknameLabel",card,"현재 닉네임",70,196,230,48,27,AttendanceInk,TextAlignmentOptions.MidlineLeft);
            Text("CurrentNickname",card,current,316,196,540,48,29,AttendanceInk,TextAlignmentOptions.MidlineLeft,true);
            Text("NewNicknameLabel",card,"새 닉네임",70,270,230,55,27,AttendanceInk,TextAlignmentOptions.MidlineLeft);
            var field=Node("NicknameInput",card,309,270,545,58);
            AttendancePaper("InputFace",field,0,0,545,58,PlayerCream,PlayerGreen,12,2);
            var target=field.gameObject.AddComponent<Image>();target.color=Color.clear;
            var input=field.gameObject.AddComponent<TMP_InputField>();input.targetGraphic=target;
            var viewport=Node("TextViewport",field,14,4,515,50);viewport.gameObject.AddComponent<RectMask2D>();
            var value=(TextMeshProUGUI)Text("Value",viewport,current,0,0,515,50,30,AttendanceInk,TextAlignmentOptions.MidlineLeft);
            input.textViewport=viewport;input.textComponent=value;input.fontAsset=font;
            input.characterLimit=24;input.lineType=TMP_InputField.LineType.SingleLine;input.richText=false;input.text=current;
            input.caretColor=PlayerGreen;input.customCaretColor=true;input.selectionColor=new Color(.3f,.6f,.4f,.3f);
            Text("NicknameHint",card,"1~24자까지 입력할 수 있어요.",309,336,545,34,22,AttendanceInk,TextAlignmentOptions.MidlineLeft);
            var status=Text("NicknameStatus",card,"",70,378,790,38,20,AttendanceInk,TextAlignmentOptions.Center,true);
            var button=PlayerButton("SaveNickname",card,"변경하기",264,433,405,57,()=>SavePlayerNickname(input,status));
        }
        async void SavePlayerNickname(TMP_InputField input,TMP_Text status)
        {
            if(playerSaving || !input)return;
            string name=input.text.Trim();
            if(string.IsNullOrWhiteSpace(name) || name.Length>24 || name.Any(char.IsControl)){status.text="닉네임을 1~24자로 입력해 주세요.";return;}
            if(!Application.isPlaying || string.IsNullOrEmpty(playerOwner)){status.text="로그인 후 변경할 수 있습니다.";return;}
            string owner=playerOwner;int request=playerRequest;var dialog=playerDialog;
            playerSaving=true;input.interactable=false;status.text="변경 중…";
            try {
                await GameApiClient.Request<UserMainData>("/api/me/profile","PATCH",new ProfileEdit{NickName=name,AvatarIdx=Mathf.Clamp(BackendGame.Instance.UserMainData.AvatarIdx,1,100)});
                if(owner!=GameApiClient.UserId)return;
                await BackendGame.Instance.LoadAsync();
                var updated=await GameApiClient.Request<Assets.Scripts.Prack.BackSys.GameData.MatchPlayerInfo>("/api/me/player-information");
                if(!PlayerCurrent(request))return;
                playerInformation=updated;RefreshUser();DrawPlayerHeader();
                if(playerDialog==dialog)ClosePlayerDialog();
                playerStatus.text="닉네임을 변경했습니다.";
            } catch(Exception){if(PlayerCurrent(request) && status)status.text="저장 결과를 확인하지 못했습니다. 새로고침 후 다시 확인해 주세요.";}
            finally{playerSaving=false;if(input)input.interactable=true;if(this && playerPage && playerOwner==owner && request!=playerRequest)ReloadPlayerPage();}
        }
        void OpenPlayerCharacters()
        {
            if(!playerPage || playerSaving)return;
            playerCharacterPage=0;
            playerCharacterId=playerItems.FirstOrDefault(p=>p.Kind=="Character" && p.Equipped)?.Id??0;
            DrawPlayerCharacters();
        }
        void DrawPlayerCharacters()
        {
            var owned=PlayerOwned("Character");
            var basic=new ShopProductData{Id=0,Kind="Character",Name="기본 캐릭터",Owned=true,Equipped=!owned.Any(p=>p.Equipped),Emotions=Array.Empty<ShopEmotionData>()};
            var items=new[]{basic}.Concat(owned).ToArray();
            var selected=items.FirstOrDefault(p=>p.Id==playerCharacterId)??items[0];
            int pages=(items.Length+5)/6;playerCharacterPage=Mathf.Clamp(playerCharacterPage,0,pages-1);
            var card=PlayerDialog("캐릭터 변경",1230,710);
            Text("OwnedCharacters",card,$"보유 캐릭터 {owned.Length}개 · 기본 캐릭터 포함",52,233,580,35,23,AttendanceInk,TextAlignmentOptions.MidlineLeft,true);
            for(int i=0;i<6 && playerCharacterPage*6+i<items.Length;i++){
                var item=items[playerCharacterPage*6+i];float x=65+i%3*170,y=280+i/3*176;
                AttendancePaper("CharacterSelection",card,x-9,y-9,146,172,item.Id==selected.Id?new Color(.82f,.93f,.72f):PlayerCream,item.Id==selected.Id?PlayerGreen:PlayerLine,14,2);
                PlayerPortrait("Character"+item.Id,card,item,x,y,128);
                Text("CharacterName",card,item.Equipped?"장착 중":item.Name,x-5,y+132,140,30,20,AttendanceInk,TextAlignmentOptions.Center,true);
                Hit("SelectCharacter"+item.Id,card,x-10,y-10,150,172,()=>{playerCharacterId=item.Id;DrawPlayerCharacters();});
            }
            if(pages>1){PlayerButton("PreviousCharacters",card,"‹",60,634,100,36,()=>{playerCharacterPage--;DrawPlayerCharacters();},false).interactable=playerCharacterPage>0;Text("CharacterPage",card,$"{playerCharacterPage+1} / {pages}",190,634,150,36,21,AttendanceInk);PlayerButton("NextCharacters",card,"›",400,634,100,36,()=>{playerCharacterPage++;DrawPlayerCharacters();},false).interactable=playerCharacterPage+1<pages;}
            PlayerPortrait("CharacterPreview",card,selected,790,230,205);
            Text("SelectedCharacterName",card,selected.Name,664,450,475,44,32,AttendanceInk,TextAlignmentOptions.Center,true);
            string[] expressions={"기본","환희","좌절"};
            for(int i=0;i<3;i++){
                string emotion=expressions[i];float x=700+i*130;
                PlayerPortrait("Emotion"+i,card,selected,x,508,88,emotion);
                Text("EmotionLabel",card,emotion,x,600,88,29,21,AttendanceInk);
            }
            PlayerButton("EquipCharacter",card,selected.Equipped?"장착 중":"장착하기",707,641,370,43,()=>SetPlayerEquipment(selected,true)).interactable=!selected.Equipped && !playerSaving;
        }
        void OpenPlayerCue(ShopProductData cue)
        {
            var card=PlayerDialog(cue.Name,1130,650);
            PlayerCueArt(card,cue,75,225,980,90);
            Text("CueSummary",card,PlayerCueSummary(cue),60,325,1010,50,26,AttendanceInk,TextAlignmentOptions.Center,true);
            string levels=cue.CueAbility==null?cue.CueDetail:"현재 레벨  "+string.Join(" / ",Assets.Scripts.Prack.BackSys.GameData.CueAbilitySettings.WithGuide(cue.CueAbility.LevelsFromSteps(cue.CueSteps+cue.CuePending/cue.CueAbility.Reach)))+"\n최고 레벨  "+string.Join(" / ",Assets.Scripts.Prack.BackSys.GameData.CueAbilitySettings.WithGuide(cue.CueAbility.Maximum))+"\n능력치 순서: 파워 · 팔로스루 · 당점 · 시간 · 가이드";
            Text("CueLevels",card,levels,60,380,1010,109,24,AttendanceInk,TextAlignmentOptions.Center,true);
            Text("CueGrowth",card,PlayerCueProgress(cue),60,492,1010,38,25,AttendanceInk);
            PlayerButton("EquipCue",card,cue.Equipped?"장착 해제 · 기본 큐대로":"장착하기",315,557,500,53,()=>SetPlayerEquipment(cue,!cue.Equipped)).interactable=!playerSaving;
        }
    }
}
