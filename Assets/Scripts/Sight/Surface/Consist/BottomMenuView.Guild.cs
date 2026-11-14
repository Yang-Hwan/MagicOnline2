using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Assets.Scripts.Exert.BackSys;
using Assets.Scripts.Prack.BackSys.GameData;
using Assets.Scripts.Sight.Surface.Pavilion;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
namespace Assets.Scripts.Sight.Surface.Consist
{
    public sealed partial class BottomMenuView
    {
        const string GuildApi="/api/guilds";
        static readonly Color GuildInk=new Color32(17,24,34,255),GuildRow=new Color32(31,39,52,255);
        RectTransform guildRoot,guildBody,guildTabs,guildDialog,guildRows,guildChatRows;
        TMP_Text guildStatus,guildCount,guildOnline,guildNotice,guildLeader;
        TMP_InputField guildSearchInput,guildChatInput;
        ScrollRect guildChatScroll;
        GuildState guildState;
        GuildMembers guildMembers=new GuildMembers();
        GuildSearch guildSearch=new GuildSearch();
        readonly List<GuildMessage> guildMessages=new List<GuildMessage>();
        int guildTab,guildPage,guildMemberPage,guildVersion,guildSearchVersion;
        string guildOwner,guildQuery="",guildMode="",guildMemberQuery="",guildSearchHash,guildMembersHash;
        string guildChatDraft="",guildPendingText,guildPendingMessage;
        bool guildFetching,guildBusy,guildOnlineOnly;
        float guildNext;
        string GuildId=>guildState?.Guild?.Id;
        bool GuildManager=>guildState?.Guild?.MyRole=="Leader" || guildState?.Guild?.MyRole=="Officer";
        bool GuildCurrent(int version)=>this && isActiveAndEnabled && guildRoot && guildRoot.gameObject.activeInHierarchy && version==guildVersion && guildOwner==GameApiClient.UserId;
        static GuildState NormalizeGuildState(GuildState state)
        {
            state=state??new GuildState();
            // Unity JSON may turn an explicit null reference into an empty DTO.
            if(!Guid.TryParse(state.Guild?.Id,out var id) || id==Guid.Empty)state.Guild=null;
            state.Invitations=state.Invitations??Array.Empty<GuildInvitation>();
            state.Applications=state.Applications??Array.Empty<GuildApplication>();
            return state;
        }
        void OpenGuild()
        {
            CloseModal();guildOwner=GameApiClient.UserId;guildBusy=guildFetching=false;guildState=null;guildTab=0;guildNext=0;
            guildRoot=modal=Node("Guild",design,0,0,Width,Height);
            GPanel("GuildFrame",guildRoot,40,134,1592,686);
            Text("GuildTitle",guildRoot,"길드",66,145,250,59,44,Color.white,TextAlignmentOptions.MidlineLeft);
            Text("GuildBreadcrumb",guildRoot,"홈 > 길드",266,155,260,42,24,Color.white,TextAlignmentOptions.MidlineLeft);
            GButton("홈으로",guildRoot,1420,151,180,48,CloseModal);
            guildTabs=Node("GuildTabs",guildRoot,570,151,820,52);
            guildBody=Node("GuildContent",guildRoot,68,222,1536,550);
            guildStatus=Text("GuildStatus",guildRoot,"길드 정보를 불러오는 중…",66,782,1535,30,21,Color.white,TextAlignmentOptions.MidlineLeft,true);
            DrawGuild();if(Application.isPlaying)FetchGuild();
        }
        void CloseGuildState()
        {
            StopGuildWarSearch();guildLeague=null;
            ReleaseGuildPortraits();
            guildVersion++;guildSearchVersion++;guildRoot=guildBody=guildDialog=guildRows=guildChatRows=null;
            guildChatInput=guildSearchInput=null;guildMessages.Clear();guildMembersHash=guildSearchHash=null;
            guildChatDraft=guildPendingText=guildPendingMessage=null;guildFetching=guildBusy=false;
        }
        void TickGuild()
        {
            if(!guildRoot)return;
            TickGuildWarSearch();
            if(guildOwner!=GameApiClient.UserId){CloseModal();return;}
            if(Application.isPlaying && !guildFetching && !guildBusy && !guildDialog && Time.unscaledTime>=guildNext)FetchGuild();
        }
        void RefreshGuild(){guildNext=0;if(guildRoot && !guildFetching && !guildBusy)FetchGuild();}
        async void FetchGuild()
        {
            int version=guildVersion;if(!GuildCurrent(version) || guildFetching)return;
            guildFetching=true;guildNext=Time.unscaledTime+5;
            try
            {
                var state=NormalizeGuildState(await GameApiClient.Request<GuildState>(GuildApi+"/me"));if(!GuildCurrent(version))return;
                bool redraw=guildState==null || GuildId!=state.Guild?.Id || guildState.Guild?.MyRole!=state.Guild?.MyRole;
                if(GuildId!=state.Guild?.Id){guildLeague=null;guildRoot.GetComponent<MatchPlayerInfoWindow>()?.Close();guildMessages.Clear();guildMembers=new GuildMembers();guildMembersHash=null;guildChatDraft="";guildMemberPage=0;}
                guildState=state;if(state.Guild==null)guildTab=2;
                if(redraw)DrawGuild();else RefreshGuildSummary();
                if(guildStatus)guildStatus.text="";
                if(guildTab==0 && GuildId!=null)
                {
                    long after=guildMessages.Count==0?0:guildMessages[guildMessages.Count-1].Id;
                    var chat=await GameApiClient.Request<GuildChat>(GuildApi+"/"+GuildId+"/chat?after="+after);
                    if(!GuildCurrent(version) || guildTab!=0)return;
                    foreach(var message in chat.Rows)if(!guildMessages.Any(m=>m.Id==message.Id))guildMessages.Add(message);
                    if(guildMessages.Count>200)guildMessages.RemoveRange(0,guildMessages.Count-200);
                    if(chat.Rows.Length>0)DrawGuildChat();
                }
                else if(guildTab==1 && GuildId!=null)
                {
                    var members=await GameApiClient.Request<GuildMembers>(GuildApi+"/"+GuildId+"/members");
                    if(!GuildCurrent(version)||guildTab!=1)return;
                    string hash=JsonUtility.ToJson(members);if(hash!=guildMembersHash){guildMembers=members;guildMembersHash=hash;DrawGuildMembers();}
                }
                else if(guildTab==2 && (!guildSearchInput || !guildSearchInput.isFocused))await ReadGuildSearch(version);
                else if(guildTab==3 && GuildId!=null)
                {
                    var league=await GameApiClient.Request<GuildLeagueState>("/api/guild-league");
                    if(!GuildCurrent(version)||guildTab!=3)return;guildLeague=league;DrawGuildLeague();
                }
            }
            catch(Exception e){if(GuildCurrent(version))GuildError(e);}
            finally{if(version==guildVersion)guildFetching=false;}
        }
        async Task ReadGuildSearch(int version)
        {
            int search=++guildSearchVersion;
            var reply=await GameApiClient.Request<GuildSearch>(GuildApi+"?q="+Uri.EscapeDataString(guildQuery)+"&mode="+guildMode+"&page="+guildPage);
            if(!GuildCurrent(version)||search!=guildSearchVersion||guildTab!=2)return;
            string hash=JsonUtility.ToJson(reply);guildSearch=reply;
            if(hash!=guildSearchHash){guildSearchHash=hash;DrawGuildSearchRows();}
        }
        void DrawGuild()
        {
            if(!guildBody)return;Clear(guildTabs);Clear(guildBody);guildRows=guildChatRows=null;guildChatInput=guildSearchInput=null;
            string[] tabs={"길드 홈","길드원","길드 찾기","길드전"};
            for(int i=0;i<4;i++){int tab=i;GButton(tabs[i],guildTabs,i*205,0,196,50,()=>{guildTab=tab;guildMembersHash=guildSearchHash=null;DrawGuild();guildNext=0;},guildState!=null && (i==2 || GuildId!=null),guildTab==i);}
            if(guildState==null){Text("Loading",guildBody,"길드 정보를 불러오는 중…",0,150,1536,90,30,Color.white);return;}
            if(guildTab==0)GuildHome();else if(guildTab==1)GuildRoster();else if(guildTab==2)GuildFind();else DrawGuildLeague();
        }
        void GuildHome()
        {
            var info=guildState.Guild;if(info==null)return;
            var summary=GPanel("GuildIdentity",guildBody,0,0,450,550);
            var emblem=Node("GuildEmblem",summary,25,20,175,175).gameObject.AddComponent<GuildEmblemGraphic>();emblem.raycastTarget=false;
            Text("Tag",summary,"["+info.Tag+"]",206,25,220,40,28,Color.cyan,TextAlignmentOptions.MidlineLeft);
            Text("GuildName",summary,info.Name,206,70,220,68,38,Color.white,TextAlignmentOptions.MidlineLeft,true);
            Text("Description",summary,info.Description,26,205,398,75,25,Color.white,TextAlignmentOptions.TopLeft,true);
            guildLeader=Text("Leader",summary,"",26,296,400,44,26,Color.white,TextAlignmentOptions.MidlineLeft,true);
            guildCount=Text("Count",summary,"",26,350,400,44,27,Yellow,TextAlignmentOptions.MidlineLeft);
            guildOnline=Text("Online",summary,"",26,399,400,44,27,Color.green,TextAlignmentOptions.MidlineLeft);
            GButton("길드원 보기",summary,25,477,193,49,()=>{guildTab=1;DrawGuild();guildNext=0;});
            GButton(GuildManager?"길드 관리":"길드 정보",summary,232,477,193,49,GuildManage);
            var announcement=GPanel("GuildAnnouncement",guildBody,470,0,1066,93);
            Text("NoticeCaption",announcement,"공지",19,12,115,64,33,Yellow);
            guildNotice=Text("Notice",announcement,"",151,12,889,64,27,Color.white,TextAlignmentOptions.MidlineLeft,true);
            Hit("ReadNotice",announcement,0,0,1066,93,GuildNoticeView);
            var chat=GPanel("GuildChat",guildBody,470,111,1066,439);
            Text("ChatTitle",chat,"길드 채팅",22,10,500,43,31,Color.white,TextAlignmentOptions.MidlineLeft);
            var viewport=Node("ChatViewport",chat,20,63,1026,289);
            guildChatRows=GScroll(viewport,289,out guildChatScroll);
            guildChatScroll.verticalNormalizedPosition=0;
            guildChatInput=GInput("GuildMessage",chat,22,367,846,52,"길드원에게 메시지를 보내세요",250);
            guildChatInput.text=guildChatDraft??"";guildChatInput.onValueChanged.AddListener(v=>guildChatDraft=v);
            guildChatInput.onSubmit.AddListener(_=>SendGuildMessage());GButton("보내기",chat,885,367,158,52,SendGuildMessage,true,true);
            RefreshGuildSummary();DrawGuildChat();
        }
        void RefreshGuildSummary()
        {
            var info=guildState?.Guild;if(info==null)return;
            if(guildLeader)guildLeader.text="길드장  "+info.LeaderName;
            if(guildCount)guildCount.text="길드원  "+info.MemberCount+" / 30명";
            if(guildOnline)guildOnline.text="접속 중  "+info.OnlineCount+"명";
            if(guildNotice)guildNotice.text=string.IsNullOrWhiteSpace(info.Notice)?"등록된 공지가 없습니다.":info.Notice;
        }
        void DrawGuildChat()
        {
            if(!guildChatRows)return;
            bool bottom=!guildChatScroll || guildChatScroll.verticalNormalizedPosition<=.03f;float pos=guildChatScroll?guildChatScroll.verticalNormalizedPosition:0;
            Clear(guildChatRows);float y=0;
            foreach(var message in guildMessages)
            {
                var name=Text("Author",guildChatRows,message.Name+"  "+GRole(message.Role),12,y,740,30,21,message.Role=="Leader"?Yellow:Color.cyan,TextAlignmentOptions.MidlineLeft,true);
                name.fontStyle=FontStyles.Normal;
                Text("SentAt",guildChatRows,DateTimeOffset.FromUnixTimeMilliseconds(message.SentAt).ToOffset(TimeSpan.FromHours(9)).ToString("HH:mm"),862,y,136,30,18,Color.gray,TextAlignmentOptions.MidlineRight);
                var line=Text("Message",guildChatRows,message.Text,12,y+31,980,56,25,Color.white,TextAlignmentOptions.TopLeft);
                line.fontStyle=FontStyles.Normal;line.textWrappingMode=TextWrappingModes.Normal;
                float h=Mathf.Max(40,line.GetPreferredValues(message.Text,980,1000).y+8);line.rectTransform.sizeDelta=new Vector2(980,h);y+=h+44;
            }
            if(guildMessages.Count==0)Text("EmptyChat",guildChatRows,"길드원에게 첫 인사를 건네 보세요.",10,50,995,64,25,Color.gray);
            guildChatRows.sizeDelta=new Vector2(1026,Mathf.Max(289,y));Canvas.ForceUpdateCanvases();if(guildChatScroll)guildChatScroll.verticalNormalizedPosition=bottom?0:pos;
        }
        void SendGuildMessage()
        {
            string text=(guildChatInput?guildChatInput.text:guildChatDraft??"").Trim();if(text.Length==0 || guildBusy)return;
            if(guildPendingText!=text){guildPendingText=text;guildPendingMessage=Guid.NewGuid().ToString();}
            string id=GuildId;
            GuildAction(async()=>{await GameApiClient.Request<GuildReply>(GuildApi+"/"+id+"/chat","POST",new GuildMessageInput{Text=text,RequestId=guildPendingMessage});
                guildPendingText=guildPendingMessage=null;if(guildChatDraft==text){guildChatDraft="";if(guildChatInput)guildChatInput.text="";}},"",false);
        }
        void GuildRoster()
        {
            Text("RosterSummary",guildBody,"["+guildState.Guild.Tag+"] "+guildState.Guild.Name+"   ·   "+guildState.Guild.MemberCount+" / 30명",5,0,780,52,29,Color.white,TextAlignmentOptions.MidlineLeft,true);
            GButton(guildOnlineOnly?"접속 중":"전체",guildBody,812,0,150,48,()=>{guildOnlineOnly=!guildOnlineOnly;guildMemberPage=0;DrawGuildMembers();},true,guildOnlineOnly);
            var search=GInput("MemberSearch",guildBody,982,0,335,48,"닉네임 검색",24);search.text=guildMemberQuery;
            search.onValueChanged.AddListener(v=>{guildMemberQuery=v;guildMemberPage=0;DrawGuildMembers();});
            GButton("길드 초대",guildBody,1335,0,195,48,GuildInviteSearch,GuildManager,true);
            guildRows=Node("GuildMemberRows",guildBody,0,70,1536,475);DrawGuildMembers();
        }
        void DrawGuildMembers()
        {
            if(!guildRows || guildTab!=1)return;Clear(guildRows);ReleaseGuildPortraits();
            var members=guildMembers.Rows.Where(m=>(!guildOnlineOnly || m.Status!="offline") && (m.Name??"").IndexOf(guildMemberQuery??"",StringComparison.OrdinalIgnoreCase)>=0).ToArray();
            int pages=Math.Max(1,(members.Length+4)/5);guildMemberPage=Mathf.Clamp(guildMemberPage,0,pages-1);
            Text("MemberColumn",guildRows,"길드원",90,0,550,34,23,Color.gray,TextAlignmentOptions.MidlineLeft);
            Text("RoleColumn",guildRows,"직책",682,0,236,34,23,Color.gray);
            Text("StatusColumn",guildRows,"접속 상태",955,0,240,34,23,Color.gray);
            Text("ChallengeColumn",guildRows,"친선경기",1262,0,246,34,23,Color.gray);
            for(int i=0;i<5 && guildMemberPage*5+i<members.Length;i++)
            {
                var member=members[guildMemberPage*5+i];float y=42+i*72;
                var row=Node("Member"+member.Id,guildRows,0,y,1536,66);Box("Background",row,0,0,1536,66,GuildRow);
                var avatar=Box("Avatar",row,14,5,56,56,Color.white);avatar.preserveAspect=true;
                var portrait=avatar.gameObject.AddComponent<EquipmentPortrait>();portrait.SetProfile(new MatchEquipmentProfile{Emotions=new[]{new MatchEquipmentEmotion{Code="기본",Asset=member.Portrait}}});
                var fallback=Resources.Load<Texture2D>("Menu/Friends/Avatar"+member.Avatar)??Resources.Load<Texture2D>("Menu/Friends/Avatar1");
                if(fallback){var sprite=Sprite.Create(fallback,new Rect(0,0,fallback.width,fallback.height),Vector2.one*.5f);GuildOwnedSprites.Add(sprite);portrait.Show("기본",sprite);}else portrait.Show("기본",null);
                Text("Name",row,member.Name,90,3,430,60,29,Color.white,TextAlignmentOptions.MidlineLeft,true);
                Text("Level",row,"Lv."+member.Level,527,3,120,60,22,Color.gray);
                Text("Role",row,GRole(member.Role),682,3,236,60,26,member.Role=="Leader"?Yellow:Color.white);
                Text("Status",row,member.Status=="online"?"접속 중":member.Status=="busy"?"경기 중":"오프라인",955,3,240,60,27,member.Status=="online"?Color.green:member.Status=="busy"?Yellow:Color.gray);
                Hit("PlayerInformation",row,0,0,1230,66,()=>GuildPlayer(member));
                bool self=member.Id==guildOwner;
                GButton(self?"나":member.Status=="busy"?"경기 중":"경기 신청",row,1262,8,246,49,()=>ChooseFriendHall(new FriendSocial.Person{Id=member.Id,Name=member.Name,Level=member.Level,Avatar=member.Avatar,Status=member.Status}),!self && member.Status=="online");
            }
            if(members.Length==0)Text("Empty",guildRows,"표시할 길드원이 없습니다.",0,140,1536,70,28,Color.gray);
            GButton("이전",guildRows,552,418,120,43,()=>{guildMemberPage--;DrawGuildMembers();},guildMemberPage>0);
            Text("Page",guildRows,(guildMemberPage+1)+" / "+pages,689,419,150,42,24,Color.white);
            GButton("다음",guildRows,855,418,120,43,()=>{guildMemberPage++;DrawGuildMembers();},guildMemberPage+1<pages);
        }
        readonly List<Sprite> GuildOwnedSprites=new List<Sprite>();
        void ReleaseGuildPortraits(){foreach(var sprite in GuildOwnedSprites)if(sprite){if(Application.isPlaying)Destroy(sprite);else DestroyImmediate(sprite);}GuildOwnedSprites.Clear();}
        void GuildPlayer(GuildMember member)
        {
            var info=guildRoot.GetComponent<MatchPlayerInfoWindow>()??guildRoot.gameObject.AddComponent<MatchPlayerInfoWindow>();info.Initialize(null);info.OpenGuild(GuildId,member.Id);
        }
        void GuildFind()
        {
            var search=GInput("GuildSearch",guildBody,0,2,580,52,"길드명 또는 태그 검색",24);guildSearchInput=search;search.text=guildQuery;
            void Submit(){guildQuery=search.text.Trim();guildPage=0;guildSearchHash=null;GuildSearchNow();}
            search.onSubmit.AddListener(_=>Submit());GButton("검색",guildBody,595,2,135,52,Submit,true,true);
            string[] names={"전체","자유 가입","승인 가입"},modes={"","Open","Approval"};
            for(int i=0;i<3;i++){string mode=modes[i];GButton(names[i],guildBody,757+i*154,2,145,52,()=>{guildMode=mode;guildPage=0;guildSearchHash=null;DrawGuild();GuildSearchNow();},true,guildMode==mode);}
            GButton("길드 만들기",guildBody,1240,2,286,52,GuildCreateForm,GuildId==null,true);
            string notice=GuildId==null?"아직 가입한 길드가 없습니다. 함께 당구를 즐길 길드를 찾아보세요.":"이미 가입한 길드가 있습니다. 다른 길드는 조회만 할 수 있습니다.";
            Text("JoinHint",guildBody,notice,0,65,925,37,22,Color.gray,TextAlignmentOptions.MidlineLeft,true);
            GButton("시즌 보상함",guildBody,950,65,264,42,GuildRewardInbox);
            GButton("받은 초대 "+guildState.Invitations.Length+"개",guildBody,1240,65,286,42,GuildInvitations,guildState.Invitations.Length>0);
            guildRows=Node("GuildSearchRows",guildBody,0,122,1536,425);DrawGuildSearchRows();
        }
        async void GuildSearchNow(){try{await ReadGuildSearch(guildVersion);}catch(Exception e){if(guildRoot)GuildError(e);}}
        void DrawGuildSearchRows()
        {
            if(!guildRows || guildTab!=2)return;Clear(guildRows);
            // The server page is 20; a scroll list keeps all returned guilds available.
            var content=GScroll(guildRows,425,out _);content.sizeDelta=new Vector2(1536,Mathf.Max(425,guildSearch.Rows.Length*127+53));
            for(int i=0;i<guildSearch.Rows.Length;i++)
            {
                var g=guildSearch.Rows[i];var row=GPanel("Guild"+g.Id,content,0,i*127,1514,117);
                Node("Emblem",row,16,10,96,96).gameObject.AddComponent<GuildEmblemGraphic>().raycastTarget=false;
                Text("Name",row,"["+g.Tag+"] "+g.Name,135,9,610,46,31,Color.white,TextAlignmentOptions.MidlineLeft,true);
                Text("Description",row,g.Description,135,60,645,40,23,Color.gray,TextAlignmentOptions.MidlineLeft,true);
                Text("Members",row,g.MemberCount+" / 30명 · 접속 중 "+g.OnlineCount+"명",788,10,400,43,25,Color.white);
                Text("Mode",row,g.JoinMode=="Open"?"자유 가입":"승인 가입",788,62,400,38,24,Color.cyan);
                string label=GuildId==g.Id?"소속 길드":GuildId!=null?"가입 중":g.ApplicationPending?"신청 취소":g.MemberCount>=30?"정원 마감":g.JoinMode=="Open"?"바로 가입":"가입 신청";
                GButton(label,row,1220,34,270,55,()=>GuildJoin(g),GuildId==null && (g.ApplicationPending || g.MemberCount<30),!g.ApplicationPending);
            }
            if(guildSearch.Rows.Length==0)Text("Empty",content,"조건에 맞는 길드가 없습니다.",0,100,1500,70,28,Color.gray);
            float y=Mathf.Max(355,guildSearch.Rows.Length*127+2);
            GButton("이전",content,530,y,150,43,()=>{guildPage=Math.Max(0,guildPage-1);GuildSearchNow();},guildPage>0);
            Text("Page",content,(guildPage+1)+"페이지",700,y,150,43,23,Color.white);
            GButton("다음",content,870,y,150,43,()=>{guildPage++;GuildSearchNow();},guildSearch.HasMore);
        }
        void GuildJoin(GuildInfo guild)
        {
            if(guild.ApplicationPending)GuildAction(()=>GameApiClient.Request<object>(GuildApi+"/"+guild.Id+"/applications/me","DELETE"),"가입 신청을 취소했습니다.");
            else GuildConfirm(guild.JoinMode=="Open"?"길드 가입":"가입 신청","["+guild.Tag+"] "+guild.Name+" 길드에 "+(guild.JoinMode=="Open"?"가입하시겠습니까?":"가입을 신청하시겠습니까?"),()=>GuildAction(async()=>{var reply=await GameApiClient.Request<GuildReply>(GuildApi+"/"+guild.Id+"/join","POST");if(reply.Joined)guildTab=0;},guild.JoinMode=="Open"?"가입했습니다.":"가입 신청을 보냈습니다."));
        }
        async void GuildAction(Func<Task> action,string success,bool redraw=true)
        {
            if(guildBusy)return;int version=guildVersion;guildBusy=true;
            try
            {
                await action();if(!GuildCurrent(version))return;
                if(redraw){GuildCloseDialog();guildSearchHash=guildMembersHash=null;DrawGuild();}
                if(guildStatus)guildStatus.text=success;guildNext=0;
            }
            catch(Exception e){if(GuildCurrent(version))GuildError(e);}
            finally{if(version==guildVersion)guildBusy=false;}
        }
        void GuildError(Exception e)
        {
            string message=e.Message=="DataConflict"?"이미 사용 중인 길드명/태그이거나 변경된 요청입니다.":e.Message;
            if(guildStatus)guildStatus.text=message;
            if(guildDialog){var error=guildDialog.Find("Error");if(error)error.GetComponent<TMP_Text>().text=message;}
        }
        static string GRole(string role)=>role=="Leader"?"길드장":role=="Officer"?"부길드장":"길드원";
        RectTransform GPanel(string name,Transform parent,float x,float y,float w,float h)
        {var rect=Node(name,parent,x,y,w,h);rect.gameObject.AddComponent<PlayerInfoFrame>().color=GuildInk;return rect;}
        Button GButton(string label,Transform parent,float x,float y,float w,float h,UnityAction action,bool enabled=true,bool primary=false)
        {
            var edge=enabled?(primary?Yellow:Color.cyan):Color.gray;
            Box(label+"Border",parent,x,y,w,h,edge);Box(label+"Fill",parent,x+2,y+2,w-4,h-4,primary && enabled?Yellow:GuildInk);
            var button=Hit(label,parent,x,y,w,h,action);button.interactable=enabled;
            Text(label+"Text",parent,label,x+7,y,w-14,h,26,!enabled?Color.gray:primary?Color.black:Color.white,autoSize:true);return button;
        }
        TMP_InputField GInput(string name,Transform parent,float x,float y,float w,float h,string placeholder,int limit)
        {
            var image=Box(name,parent,x,y,w,h,GuildRow);image.raycastTarget=true;
            var input=image.gameObject.AddComponent<TMP_InputField>();var viewport=Node("Viewport",image.transform,12,0,w-24,h);viewport.gameObject.AddComponent<RectMask2D>();
            var value=(TextMeshProUGUI)Text("Value",viewport,"",0,0,w-24,h,25,Color.white,TextAlignmentOptions.MidlineLeft);value.fontStyle=FontStyles.Normal;
            var hint=Text("Placeholder",viewport,placeholder,0,0,w-24,h,23,Color.gray,TextAlignmentOptions.MidlineLeft,true);hint.fontStyle=FontStyles.Normal;
            input.targetGraphic=image;input.textViewport=viewport;input.textComponent=value;input.placeholder=hint;input.fontAsset=font;input.characterLimit=limit;input.lineType=TMP_InputField.LineType.SingleLine;
            // Disable IME composition markup in the input as well as its text label.
            input.richText=false;return input;
        }
        RectTransform GScroll(RectTransform host,float height,out ScrollRect scroll)
        {
            var viewport=Node("Viewport",host,0,0,host.rect.width,height);var bg=viewport.gameObject.AddComponent<Image>();bg.color=Color.clear;viewport.gameObject.AddComponent<RectMask2D>();
            scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.horizontal=false;scroll.scrollSensitivity=45;scroll.movementType=ScrollRect.MovementType.Clamped;
            var content=Node("Rows",viewport,0,0,host.rect.width,height);scroll.content=content;return content;
        }
    }
}
