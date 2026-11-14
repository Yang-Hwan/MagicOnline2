using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Exert.BackSys;
using Assets.Scripts.Exert.Network;
using Assets.Scripts.Prack.BackSys.ChartData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Sight.Surface.Consist
{
    public sealed partial class BottomMenuView
    {
        RectTransform friendsPanel;
        int friendListVersion, searchVersion;
        bool managingFriends;
        FriendSocial.Person[] displayedFriends;

        void OpenFriends()
        {
            managingFriends=false;
            var panel=OpenFriendsFrame();
            SolidButton("친구찾기",panel,574,38,187,58,OpenFriendSearch);
            var manage=Node("Management",panel,775,38,187,58);
            void DrawManageButton()
            {
                Clear(manage);
                SolidButton(managingFriends?"관리 완료":"친구관리",manage,0,0,187,58,()=>
                {
                    managingFriends=!managingFriends;
                    DrawManageButton();
                    if(displayedFriends!=null)DrawFriends((RectTransform)panel.Find("List"),displayedFriends);
                },managingFriends);
            }
            DrawManageButton();
            var surface=Node("List",panel,27,137,1066,584);
            Text("Status",surface,"친구 목록을 불러오는 중…",0,180,1066,60,28,Color.white);
            RefreshFriends();
            StartCoroutine(FriendListTimer(surface));
        }

        RectTransform OpenFriendsFrame()
        {
            CloseModal();
            displayedFriends=null;
            modal=Node("Friends",design,0,0,Width,Height);
            Box("Shade",modal,0,0,Width,Height,new Color(0,0,0,.70f)).raycastTarget=true;
            friendsPanel=Node("FriendsPanel",modal,272,78,1126,764);
            Art("Frame",friendsPanel,Resources.Load<Texture2D>("Menu/Friends/Panel"),0,0,1126,764);
            Hit("닫기",friendsPanel,1024,6,90,82,CloseModal);
            return friendsPanel;
        }
        IEnumerator FriendListTimer(RectTransform panel)
        {
            while(panel && panel.gameObject.activeInHierarchy)
            {
                yield return new WaitForSecondsRealtime(30);
                if(panel && panel.gameObject.activeInHierarchy)RefreshFriends();
            }
        }
        async void RefreshFriends()
        {
            if(!friendsPanel || !friendsPanel.gameObject.activeInHierarchy)return;
            var surface=friendsPanel.Find("List") as RectTransform;
            if(!surface)return;
            int version=++friendListVersion;
            try
            {
                var data=await GameApiClient.Request<FriendSocial.People>(FriendSocial.Api);
                if(!surface || !surface.gameObject.activeInHierarchy || version!=friendListVersion)return;
                var rows=data.Rows??Array.Empty<FriendSocial.Person>();
                if(displayedFriends!=null && displayedFriends.Length==rows.Length &&
                    !rows.Where((p,i)=>p.Id!=displayedFriends[i].Id || p.Name!=displayedFriends[i].Name ||
                        p.Status!=displayedFriends[i].Status || p.Avatar!=displayedFriends[i].Avatar || p.Level!=displayedFriends[i].Level).Any())return;
                displayedFriends=rows;
                DrawFriends(surface,rows);
            }
            catch(Exception e)
            {
                if(surface && surface.gameObject.activeInHierarchy && version==friendListVersion)
                {
                    displayedFriends=null;
                    Clear(surface);Text("Error",surface,e.Message,40,170,980,90,24,Color.white);
                    SolidButton("다시 시도",surface,430,288,206,58,RefreshFriends);
                }
            }
        }

        void DrawFriends(RectTransform surface,FriendSocial.Person[] rows)
        {
            var old=surface.GetComponentInChildren<ScrollRect>();
            float pos=old?old.verticalNormalizedPosition:1;
            Clear(surface);
            if(rows.Length==0){Text("Empty",surface,"아직 등록된 친구가 없습니다.",0,183,1066,60,26,Color.white);return;}
            var content=FriendScroll(surface,rows.Length,114,out var scroll);
            var visible=new Dictionary<int,RectTransform>();
            int drawnFrom=-1;
            void DrawVisible()
            {
                int start=Mathf.Clamp(Mathf.FloorToInt(content.anchoredPosition.y/114),0,rows.Length-1);
                if(start==drawnFrom)return;
                int end=Math.Min(rows.Length,start+7);
                for(int i=drawnFrom;i<drawnFrom+7;i++)
                {
                    if(i>=start && i<end || !visible.TryGetValue(i,out var oldRow))continue;
                    oldRow.gameObject.SetActive(false);
                    Dispose(oldRow.gameObject);
                    visible.Remove(i);
                }
                drawnFrom=start;
                for(int i=start;i<end;i++)
                    if(!visible.ContainsKey(i))visible[i]=DrawFriendRow(content,rows[i],i);
            }
            scroll.onValueChanged.AddListener(_=>DrawVisible());
            Canvas.ForceUpdateCanvases();scroll.verticalNormalizedPosition=pos;
            DrawVisible();
        }

        RectTransform DrawFriendRow(RectTransform content,FriendSocial.Person player,int i)
        {
            bool online=player.Status=="online";
            var row=Node("Friend"+player.Id,content,0,i*114,1048,106);
            Box("Edge",row,0,0,1048,106,new Color(.65f,.70f,.76f));
            Box("Card",row,2,2,1044,102,new Color(.035f,.052f,.074f));
            var avatar=Resources.Load<Texture2D>("Menu/Friends/Avatar"+player.Avatar)??Resources.Load<Texture2D>("Menu/Friends/Avatar1");
            Art("Avatar",row,avatar,18,10,86,86);
            Text("Level",row,"Lv."+player.Level,124,8,120,90,27,Color.white,TextAlignmentOptions.MidlineLeft,true);
            Text("Name",row,player.Name,248,8,340,90,32,Color.white,TextAlignmentOptions.MidlineLeft,true);
            string state=online?"온라인":player.Status=="busy"?"게임 중":"오프라인";
            Text("State",row,state,595,10,206,86,28,online?Color.green:player.Status=="busy"?Yellow:Color.gray);
            string action=managingFriends?"삭제":"도전";
            var color=managingFriends?new Color(.81f,.22f,.24f):online?Color.cyan:new Color(.28f,.29f,.31f);
            Box("ChallengeFill",row,833,19,187,69,color);
            Text("ActionLabel",row,action,833,19,187,69,31,managingFriends?Color.white:online?Color.black:Color.gray);
            if(managingFriends)
            {
                Button remove=null;
                remove=Hit(action,row,833,19,187,69,()=>RemoveFriend(player,remove));
            }
            else Hit(action,row,833,19,187,69,()=>ChooseFriendHall(player)).interactable=online;
            return row;
        }

        async void RemoveFriend(FriendSocial.Person player,Button button)
        {
            button.interactable=false;
            try
            {
                await GameApiClient.Request<object>(FriendSocial.Api+"/"+player.Id,"DELETE");
                if(this)RefreshFriends();
            }
            catch(Exception e)
            {
                if(button)button.interactable=true;
                if(FriendSocial.Instance)FriendSocial.Instance.Toast(e.Message);
            }
        }

        void OpenFriendSearch()
        {
            var panel=OpenFriendsFrame();
            SolidButton("친구 목록",panel,775,38,187,58,OpenFriends);
            Text("SearchTitle",panel,"닉네임 검색",32,137,151,58,25,Color.white);
            var field=Box("Nickname",panel,195,137,655,58,new Color(.12f,.15f,.19f));
            field.raycastTarget=true;
            var input=field.gameObject.AddComponent<TMP_InputField>();
            var viewport=Node("Viewport",field.transform,12,0,631,58);
            viewport.gameObject.AddComponent<RectMask2D>();
            var value=(TextMeshProUGUI)Text("Value",viewport,"",0,0,631,58,26,Color.white,TextAlignmentOptions.MidlineLeft);
            input.targetGraphic=field;input.textViewport=viewport;input.textComponent=value;input.fontAsset=font;
            input.characterLimit=24;input.lineType=TMP_InputField.LineType.SingleLine;
            input.richText=false;
            var results=Node("SearchResults",panel,27,216,1066,500);
            void Submit()=>SearchFriends(results,input.text.Trim());
            SolidButton("검색",panel,868,137,179,58,Submit,true);
            input.onSubmit.AddListener(_=>Submit());
            input.ActivateInputField();
        }

        async void SearchFriends(RectTransform results,string query)
        {
            int version=++searchVersion;
            Clear(results);
            var status=Text("Status",results,query.Length==0?"닉네임을 입력해 주세요.":"검색 중…",
                30,150,1000,60,25,Color.white);
            if(query.Length==0)return;
            try
            {
                var reply=await GameApiClient.Request<FriendSocial.People>(FriendSocial.Api+"/search?q="+Uri.EscapeDataString(query));
                if(!results || !results.gameObject.activeInHierarchy || version!=searchVersion)return;
                var rows=reply.Rows??Array.Empty<FriendSocial.Person>();
                if(rows.Length==0){status.text="검색된 회원이 없습니다.";return;}
                Clear(results);
                var content=FriendScroll(results,rows.Length,84,out _);
                for(int i=0;i<rows.Length;i++)
                {
                    var player=rows[i];
                    var row=Node("Member"+player.Id,content,0,i*84,1048,78);
                    Box("Card",row,0,0,1048,78,new Color(.07f,.10f,.14f));
                    var avatar=Resources.Load<Texture2D>("Menu/Friends/Avatar"+player.Avatar)??Resources.Load<Texture2D>("Menu/Friends/Avatar1");
                    Art("Avatar",row,avatar,14,7,64,64);
                    Text("Level",row,"Lv."+player.Level,100,0,125,78,23,Color.white,TextAlignmentOptions.MidlineLeft,true);
                    Text("Name",row,player.Name,230,0,530,78,27,Color.white,TextAlignmentOptions.MidlineLeft,true);
                    bool canRequest=string.IsNullOrEmpty(player.Relation);
                    Box("RequestFill",row,823,12,198,54,canRequest?Color.cyan:Color.gray);
                    var label=Text("RequestLabel",row,canRequest?"친구 요청":player.Relation,823,12,198,54,23,Color.black);
                    Button button=null;
                    button=Hit("친구 요청",row,823,12,198,54,()=>FriendSocial.Instance.Add(player.Id,button,label));
                    button.interactable=canRequest;
                }
            }
            catch(Exception e){if(status && results && version==searchVersion)status.text=e.Message;}
        }

        RectTransform FriendScroll(RectTransform host,int count,int stride,out ScrollRect scroll)
        {
            var viewport=Node("Viewport",host,0,0,host.rect.width,host.rect.height);
            viewport.gameObject.AddComponent<Image>().color=Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll=viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport=viewport;scroll.horizontal=false;scroll.vertical=count*stride>host.rect.height;
            scroll.scrollSensitivity=48;scroll.movementType=ScrollRect.MovementType.Clamped;
            var content=Node("Rows",viewport,0,0,host.rect.width,Math.Max(count*stride,host.rect.height));
            scroll.content=content;return content;
        }

        void ChooseFriendHall(FriendSocial.Person other)
        {
            var panel=Dialog("친선 경기장 선택","");
            Text("Opponent",panel,$"상대  {other.Name}    ·    입장 무료 · 코인 및 공식 기록 미반영",45,86,750,39,22,Color.white);
            var halls=BackendChart.skillMatchData;
            if(halls==null || halls.Count==0){Text("Empty",panel,"경기장이 없습니다.",80,160,680,55,24,Color.white);return;}
            var host=Node("ArenaList",panel,50,134,740,180);
            var content=FriendScroll(host,halls.Count,62,out _);
            for(int i=0;i<halls.Count;i++)
            {
                var hall=halls[i];float y=i*62;
                Box("Arena",content,0,y,730,57,new Color(.07f,.12f,.16f));
                Text("ArenaName",content,$"{hall.HallName} · {hall.MatchBall}구",13,y,303,57,23,Color.white,TextAlignmentOptions.MidlineLeft,true);
                Text("Rules",content,$"{hall.TargetHit}점 · {hall.MatchTotMin}분",316,y,198,57,21,Color.white);
                Box("SendFill",content,533,y+6,182,44,Yellow);
                Text("SendLabel",content,"도전 요청",533,y+6,182,44,23,Color.black);
                Hit("도전 요청",content,533,y+6,182,44,()=>SendFriendChallenge(other.Id,hall,panel));
            }
        }
        bool sendingChallenge;
        async void SendFriendChallenge(string other,SkillMatchData hall,RectTransform panel)
        {
            if(sendingChallenge)return;
            sendingChallenge=true;
            try
            {
                await FriendSocial.Instance.SendChallenge(other,hall.HallIdx);
                if(panel && panel.gameObject.activeInHierarchy)CloseModal();
            }
            catch(Exception e){if(panel && panel.gameObject.activeInHierarchy)FriendSocial.Instance.Toast(e.Message);}
            finally{sendingChallenge=false;}
        }
        internal void EnterFriendly(FriendSocial.Invite invite)
        {
            if(!legacyRooms || !NetworkManager.TryGetExistingNetwork(out var net) || !(net is PunNetwork pun))
            {FriendSocial.Instance.Toast("경기 서버 연결을 확인해 주세요.");return;}
            CloseModal();
            legacyRooms.GotoFriendRoom(pun,invite.Hall,invite.Id,invite.Other);
        }
    }
}
