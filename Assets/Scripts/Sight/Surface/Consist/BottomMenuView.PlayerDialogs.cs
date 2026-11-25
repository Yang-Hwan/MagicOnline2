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
        long playerCueDialogId;
        void ClosePlayerDialog(){playerCueDialogId=0;if(playerDialog){playerDialog.gameObject.SetActive(false);Dispose(playerDialog.gameObject);playerDialog=null;}}
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
        static readonly int[] CueRowOrder={0,1,2,4,3};
        static readonly string[] CueRowNames={"파워","밀기","스핀","조준","시간"};
        static int[] CueLevels(ShopProductData cue)=>Assets.Scripts.Prack.BackSys.GameData.CueAbilitySettings.WithGuide(cue.CueAbility?.LevelsFromSteps(cue.CueSteps+cue.CuePending/Math.Max(1,cue.CueAbility.Reach)));
        static int CueDisplayLevel(ShopProductData cue,int index,int level)=>cue.CueAbility==null?level:index==4&&!cue.CueAbility.HasGuide()?100:cue.CueAbility.Percent(level);
        static float CueFraction(ShopProductData cue,int index,int level)
        {
            var a=cue.CueAbility;if(a==null)return 0;
            var min=Assets.Scripts.Prack.BackSys.GameData.CueAbilitySettings.WithGuide(a.Initial);
            var max=Assets.Scripts.Prack.BackSys.GameData.CueAbilitySettings.WithGuide(a.Maximum);
            return max[index]==min[index]?1:Mathf.Clamp01((float)(level-min[index])/(max[index]-min[index]));
        }
        static bool CueMaxed(ShopProductData cue,int index)=>cue.CueAbility!=null&&CueLevels(cue)[index]>=Assets.Scripts.Prack.BackSys.GameData.CueAbilitySettings.WithGuide(cue.CueAbility.Maximum)[index];
        void OpenPlayerCue(ShopProductData cue)
        {
            ClosePlayerDialog();playerCueDialogId=cue.Id;
            playerDialog=Node("PlayerDialog",playerPage,0,0,Width,Height);
            var shade=Box("Shade",playerDialog,0,0,Width,Height,new Color(0,0,0,.72f));
            var card=Node("CueDetailCard",playerDialog,(Width-1300)/2,(Height-840)/2,1300,840);
            CueChrome("Face",card,0,0,1300,840,CueLockerGraphic.Finish.Background);
            CueChrome("CueTitleBand",card,10,10,1280,104,CueLockerGraphic.Finish.Header);
            Text("CueTitle",card,cue.Name,180,22,940,57,43,CueIvory,TextAlignmentOptions.Center,true);
            var levels=CueLevels(cue);var ability=cue.CueAbility;
            int Display(int level)=>ability==null?level:ability.Percent(level);
            Text("CueGradeLevel",card,(ability?.Grade??"A")+"급  ·  Lv."+Display(levels.Min()),440,77,420,35,25,CueIvory);
            ModalBackdrop.Attach(shade,card,ClosePlayerDialog);
            CueChrome("SkinWell",card,30,125,1240,105,CueLockerGraphic.Finish.Inset);
            PlayerCueArt(card,cue,53,133,884,53);
            long reach=Math.Max(1,ability?.Reach??1),progress=Math.Max(0,cue.CuePending)%reach;
            bool growthComplete=ability!=null&&Enumerable.Range(0,5).All(i=>CueMaxed(cue,i));
            string progressText=ability==null?"진도 정보 없음":growthComplete?"진도 완료 · MAX":$"진도 {progress:N0} / {reach:N0}";
            Text("CueRoundProgress",card,progressText,53,191,315,32,22,CueIvory,TextAlignmentOptions.MidlineLeft);
            CueChrome("CueRoundTrack",card,380,199,550,20,CueLockerGraphic.Finish.Inset);
            float progressRatio=growthComplete?1:ability==null?0:(float)progress/reach;
            CueChrome("CueRoundFill",card,383,202,544*progressRatio,14,CueLockerGraphic.Finish.Gold);
            CueButton("EquipCue",card,cue.Equipped?"장착해제":"장착하기",988,151,250,52,()=>SetPlayerEquipment(cue,!cue.Equipped),false,!playerSaving);
            var bowls=cue.CueBowls;
            for(int row=0;row<5;row++){
                int index=CueRowOrder[row];float y=250+row*79;bool maxed=CueMaxed(cue,index);
                CueChrome("StatRow"+index,card,24,y,1252,73,maxed?CueLockerGraphic.Finish.Muted:CueLockerGraphic.Finish.Inset);
                Text("StatLevel"+index,card,CueRowNames[row]+"  Lv."+CueDisplayLevel(cue,index,levels[index]),40,y+16,240,42,29,maxed?CueQuiet:CueIvory,TextAlignmentOptions.MidlineLeft);
                float fraction=CueFraction(cue,index,levels[index]);
                CueChrome("Track"+index,card,292,y+21,546,31,CueLockerGraphic.Finish.Inset);
                CueChrome("Fill"+index,card,295,y+25,540*fraction,23,CueLockerGraphic.Finish.Fill);
                if(!maxed&&ability!=null&&bowls!=null&&bowls.Ready(index)){
                    var next=Assets.Scripts.Prack.BackSys.GameData.CueAbilitySettings.WithGuide(ability.LevelsFromSteps(cue.CueSteps+(cue.CuePending+1)/Math.Max(1,ability.Reach)));
                    float gain=CueFraction(cue,index,next[index])-fraction;
                    if(gain>0)Box("Preview"+index,card,295+540*fraction,y+25,540*gain,23,new Color(1,1,.8f,.6f));
                }
                bool done=maxed||(bowls!=null&&bowls.Drunk[index]);bool ready=!maxed&&bowls!=null&&bowls.Ready(index)&&!bowls.Auto;
                string value=maxed?"—":bowls==null?"—":ready?"+"+bowls.Filled[index]:$"{bowls.Filled[index]}/{bowls.Maximum[index]}";
                Text("BowlValue"+index,card,value,860,y+16,200,40,29,CueIvory);
                CueButton("DrinkBowl"+index,card,done?"완료":"성장",1080,y+14,176,47,()=>ChangeCueBowl(cue,index),true,ready&&!playerSaving);
            }
            if(bowls!=null&&bowls.Auto)Text("AutoPolicy",card,"자동 성장 적용 중",35,730,755,31,20,CueQuiet,TextAlignmentOptions.MidlineLeft);
            CueButton("GrowAll",card,playerSaving?"처리 중…":"모두성장",450,726,400,76,()=>ChangeCueBowl(cue,-1),true,!playerSaving&&CueCanGrow(cue));
        }
        [Serializable] sealed class BowlActionInput {public long CueId,Cycle;public string Operation;public int Index;}
        async void ChangeCueBowl(ShopProductData cue,int index)
        {
            if(playerSaving||!Application.isPlaying||string.IsNullOrEmpty(playerOwner)||cue.CueBowls==null||!playerDialog)return;
            var owner=playerOwner;int request=playerRequest;var dialog=playerDialog;playerSaving=true;
            var before=cue;long cueId=cue.Id;
            var indices=index<0?CueRowOrder.Where(i=>!CueMaxed(cue,i)&&cue.CueBowls.Ready(i)).ToArray():new[]{index};
            try {
                await GameApiClient.Request<Assets.Scripts.Prack.BackSys.GameData.CueBowlState>("/api/me/cue-bowls/drink","POST",new BowlActionInput{CueId=cueId,Cycle=cue.CueBowls.Cycle,Operation=Guid.NewGuid().ToString("D"),Index=index});
                var inventory=await GameApiClient.Request<ShopCatalogData>("/api/me/inventory");
                if(!PlayerCurrent(request)||playerOwner!=owner)return;
                playerItems=inventory.Products;cue=playerItems.FirstOrDefault(p=>p.Id==cueId);if(cue==null)return;
                DrawPlayerLocker();
                if(playerDialog==dialog){
                    OpenPlayerCue(cue);dialog=playerDialog;
                    await AnimateCueGrowthSequence(before,cue,indices,dialog);
                    await AnimateCueRoundProgress(before,cue,dialog);
                    if(dialog&&playerDialog==dialog&&CueLevelUpMotion.ShouldShow(before.CueSteps,cue.CueSteps,CueLevels(before),CueLevels(cue))){
                        var card=dialog.Find("CueDetailCard");
                        var header=card.Find("CueGradeLevel")?.GetComponent<TMP_Text>();int level=cue.CueAbility.Percent(CueLevels(cue).Min());if(header)header.text=cue.CueAbility.Grade+"급  ·  Lv."+level;
                        var celebration=CueLevelUpMotion.Play(dialog);
                        while(celebration&&dialog&&playerDialog==dialog&&!celebration.Finished)await System.Threading.Tasks.Task.Yield();
                        if(celebration)Dispose(celebration.gameObject);
                    }
                }else if(playerDialog&&playerCueDialogId==cueId){OpenPlayerCue(cue);dialog=playerDialog;}
                if(PlayerCurrent(request)){playerStatus.text="성장 처리를 완료했습니다.";playerSaving=false;if(playerDialog&&playerCueDialogId>0){var shown=playerItems.FirstOrDefault(p=>p.Id==playerCueDialogId);if(shown!=null)OpenPlayerCue(shown);}}
            }catch(Exception){if(PlayerCurrent(request)){playerStatus.text="성장 결과를 확인하지 못했습니다. 새로고침 후 확인해 주세요.";if(playerDialog==dialog)ClosePlayerDialog();}}
            finally{playerSaving=false;}
        }
        async System.Threading.Tasks.Task AnimateCueGrowthSequence(ShopProductData before,ShopProductData after,int[] indices,RectTransform dialog)
        {
            if(!dialog)return;var card=dialog.Find("CueDetailCard");if(!card)return;
            var levels=CueLevels(before);
            var header=card.Find("CueGradeLevel")?.GetComponent<TMP_Text>();if(header&&before.CueAbility!=null)header.text=before.CueAbility.Grade+"급  ·  Lv."+before.CueAbility.Percent(levels.Min());
            var progress=card.Find("CueRoundProgress")?.GetComponent<TMP_Text>();var progressFill=(RectTransform)card.Find("CueRoundFill");
            if(before.CueAbility!=null){long reach=Math.Max(1,before.CueAbility.Reach);if(progress)progress.text=$"진도 {before.CuePending:N0} / {reach:N0}";if(progressFill)progressFill.sizeDelta=new Vector2(544*(float)before.CuePending/reach,14);}
            // All data is already committed. Keep later rows visually waiting their turn.
            foreach(int stat in indices){
                var value=card.Find("BowlValue"+stat)?.GetComponent<TMP_Text>();if(value)value.text="+"+before.CueBowls.Filled[stat];
                var status=card.Find("DrinkBowl"+stat+"Label")?.GetComponent<TMP_Text>();if(status)status.text="대기";
                var fill=(RectTransform)card.Find("Fill"+stat);if(fill)fill.sizeDelta=new Vector2(540*CueFraction(before,stat,levels[stat]),23);
                var label=card.Find("StatLevel"+stat)?.GetComponent<TMP_Text>();int row=Array.IndexOf(CueRowOrder,stat);if(label)label.text=CueRowNames[row]+"  Lv."+CueDisplayLevel(before,stat,levels[stat]);
            }
            foreach(int stat in indices){
                if(!dialog||playerDialog!=dialog)break;
                var status=card.Find("DrinkBowl"+stat+"Label")?.GetComponent<TMP_Text>();if(status)status.text="성장 중";
                await AnimateCueGrowth(before,after,stat,dialog);
                if(!dialog||playerDialog!=dialog)break;
                if(status)status.text="완료";
            }
        }
        async System.Threading.Tasks.Task AnimateCueRoundProgress(ShopProductData before,ShopProductData after,RectTransform dialog)
        {
            if(!dialog||playerDialog!=dialog||before.CueAbility==null)return;
            if(before.CueSteps==after.CueSteps&&before.CuePending==after.CuePending)return;
            var card=dialog.Find("CueDetailCard");var fill=(RectTransform)card.Find("CueRoundFill");var label=card.Find("CueRoundProgress")?.GetComponent<TMP_Text>();
            long reach=Math.Max(1,before.CueAbility.Reach),target=after.CueSteps>before.CueSteps?reach:after.CuePending;float started=Time.unscaledTime;
            while(dialog&&playerDialog==dialog&&Time.unscaledTime-started<.42f){
                float t=Mathf.SmoothStep(0,1,(Time.unscaledTime-started)/.42f),value=Mathf.Lerp(before.CuePending,target,t);
                if(fill)fill.sizeDelta=new Vector2(544*value/reach,14);if(label)label.text=$"진도 {Mathf.RoundToInt(value):N0} / {reach:N0}";await System.Threading.Tasks.Task.Yield();
            }
            if(dialog&&playerDialog==dialog){if(fill)fill.sizeDelta=new Vector2(544*(float)target/reach,14);if(label)label.text=$"진도 {target:N0} / {reach:N0}";}
        }
        async System.Threading.Tasks.Task AnimateCueGrowth(ShopProductData before,ShopProductData after,int stat,RectTransform dialog)
        {
            if(!dialog)return;var card=dialog.Find("CueDetailCard");if(!card)return;
            int row=Array.IndexOf(CueRowOrder,stat);float y=250+row*79;
            int amount=before.CueBowls.Filled[stat];if(amount<1)return;
            var effects=Node("GrowthEffect"+stat,card,295,y+25,540,23);
            effects.gameObject.AddComponent<RectMask2D>();
            var group=effects.gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;
            Box("Glow",effects,0,0,540,23,new Color(1,.83f,.25f,.24f));
            var sweep=Box("Sweep",effects,540,0,36,23,new Color(1,1,.8f,.65f));
            var value=card.Find("BowlValue"+stat)?.GetComponent<TMP_Text>();
            var playback=effects.gameObject.AddComponent<CueGrowthPlayback>();playback.Configure(amount,(RectTransform)sweep.transform,group,value);
            var oldLevels=CueLevels(before);var newLevels=CueLevels(after);float start=Time.unscaledTime,duration=playback.Duration;
            while(dialog&&playerDialog==dialog&&Time.unscaledTime-start<duration){
                playback.Sample(Time.unscaledTime-start);float t=playback.Progress;
                var fill=(RectTransform)card.Find("Fill"+stat);if(fill)fill.sizeDelta=new Vector2(540*Mathf.Lerp(CueFraction(before,stat,oldLevels[stat]),CueFraction(after,stat,newLevels[stat]),t),23);
                var label=card.Find("StatLevel"+stat)?.GetComponent<TMP_Text>();int level=Mathf.RoundToInt(Mathf.Lerp(oldLevels[stat],newLevels[stat],t));if(label)label.text=CueRowNames[row]+"  Lv."+CueDisplayLevel(after,stat,level);
                await System.Threading.Tasks.Task.Yield();
            }
            if(value&&dialog&&playerDialog==dialog)value.text="0";
            if(effects)Dispose(effects.gameObject);
        }
    }
}
