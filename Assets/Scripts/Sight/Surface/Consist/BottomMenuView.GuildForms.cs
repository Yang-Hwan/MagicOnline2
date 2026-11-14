using System;
using System.Linq;
using Assets.Scripts.Exert.BackSys;
using Assets.Scripts.Prack.BackSys.GameData;
using TMPro;
using UnityEngine;
namespace Assets.Scripts.Sight.Surface.Consist
{
    public sealed partial class BottomMenuView
    {
        RectTransform GuildForm(string title,float height=610)
        {
            GuildCloseDialog();
            guildDialog=Node("GuildDialog",guildRoot,0,0,Width,Height);
            Box("Shade",guildDialog,0,127,Width,710,new Color(0,0,0,.84f)).raycastTarget=true;
            var panel=GPanel("DialogCard",guildDialog,276,(Height-height)*.5f,1120,height);
            Text("Title",panel,title,30,16,850,52,35,Color.white,TextAlignmentOptions.MidlineLeft);
            GButton("닫기",panel,945,18,145,45,GuildCloseDialog);
            Text("Error",guildDialog,"",300,(Height+height)*.5f-32,1070,24,20,new Color(1,.66f,.35f),TextAlignmentOptions.MidlineLeft,true);
            return panel;
        }
        void GuildCloseDialog(){if(guildDialog){guildDialog.gameObject.SetActive(false);Dispose(guildDialog.gameObject);guildDialog=null;}}
        void GuildConfirm(string title,string message,Action yes)
        {
            var panel=GuildForm(title,390);
            Text("Question",panel,message,45,102,1030,150,30,Color.white,TextAlignmentOptions.Center,true);
            GButton("확인",panel,600,280,220,55,()=>yes(),true,true);GButton("취소",panel,300,280,220,55,GuildCloseDialog);
        }
        void GuildCreateForm()
        {
            if(GuildId!=null)return;
            var panel=GuildForm("길드 만들기");string mode="Approval",request=Guid.NewGuid().ToString();
            Text("NameLabel",panel,"길드명 (2~20자)",38,91,270,45,26,Color.white,TextAlignmentOptions.MidlineLeft);
            var name=GInput("Name",panel,315,90,765,52,"길드명을 입력하세요",20);
            Text("TagLabel",panel,"태그 (영문/숫자 2~6자)",38,166,270,48,23,Color.white,TextAlignmentOptions.MidlineLeft,true);
            var tag=GInput("Tag",panel,315,165,765,52,"예: BLUE",6);
            Text("DescriptionLabel",panel,"길드 소개",38,242,260,48,26,Color.white,TextAlignmentOptions.MidlineLeft);
            var description=GInput("Description",panel,315,241,765,64,"함께할 길드원에게 소개해 주세요",100);
            Text("ModeLabel",panel,"가입 방식",38,334,260,48,26,Color.white,TextAlignmentOptions.MidlineLeft);
            var choice=Text("Mode",panel,"승인 가입: 운영자가 신청을 확인합니다.",315,397,760,44,23,Yellow,TextAlignmentOptions.MidlineLeft);
            GButton("자유 가입",panel,315,334,330,51,()=>{mode="Open";GuildSelectMode(panel,mode);choice.text="자유 가입: 빈자리가 있으면 바로 가입합니다.";});
            GButton("승인 가입",panel,665,334,330,51,()=>{mode="Approval";GuildSelectMode(panel,mode);choice.text="승인 가입: 운영자가 신청을 확인합니다.";},true,true);
            Text("Capacity",panel,"1인 1길드 · 정원 30명 · 생성 비용 없음",40,465,1040,39,24,Color.gray);
            GButton("길드 만들기",panel,720,518,350,54,()=>GuildAction(async()=>
            {
                await GameApiClient.Request<GuildReply>(GuildApi,"POST",new GuildCreate{Name=name.text.Trim(),Tag=tag.text.Trim().ToUpperInvariant(),Description=description.text.Trim(),JoinMode=mode,RequestId=request});
                guildTab=0;
            },"길드를 만들었습니다."),true,true);
        }
        void GuildManage()
        {
            var g=guildState?.Guild;if(g==null)return;
            var panel=GuildForm(GuildManager?"길드 관리":"길드 정보");
            Text("Guild",panel,"["+g.Tag+"] "+g.Name,35,83,1040,48,32,Yellow,TextAlignmentOptions.MidlineLeft,true);
            if(GuildManager)
            {
                GButton("공지·소개 수정",panel,40,154,490,62,GuildEditForm);
                GButton("길드 초대",panel,570,154,490,62,GuildInviteSearch);
                GButton("가입 신청 "+guildState.Applications.Length+"건",panel,40,247,490,62,GuildApplications);
                GButton("직책·길드원 관리",panel,570,247,490,62,GuildRoleManagement);
            }
            else Text("RoleInfo",panel,"현재 직책: "+GRole(g.MyRole)+"\n공지·초대·가입 승인은 길드 운영자가 관리합니다.",40,165,1020,170,29,Color.white,autoSize:true);
            string warning=g.MyRole=="Leader"?"길드원이 있으면 길드장을 위임한 뒤 탈퇴할 수 있습니다.\n마지막 길드원이 탈퇴하면 길드가 해산됩니다.":"탈퇴하면 길드 공지·채팅을 더 이상 조회할 수 없습니다.";
            Text("LeaveNote",panel,warning,40,365,1020,95,24,Color.gray,autoSize:true);
            GButton("길드 탈퇴",panel,660,508,400,58,()=>GuildConfirm("길드 탈퇴",g.MemberCount==1?"마지막 길드원입니다. 탈퇴하면 길드가 해산됩니다.\n탈퇴하시겠습니까?":"["+g.Tag+"] "+g.Name+" 길드에서 탈퇴하시겠습니까?",()=>GuildAction(()=>GameApiClient.Request<object>(GuildApi+"/"+g.Id+"/leave","POST"),"길드에서 탈퇴했습니다.")));
        }
        void GuildEditForm()
        {
            var g=guildState.Guild;var panel=GuildForm("공지·길드 소개 수정");string mode=g.JoinMode;
            Text("DescriptionLabel",panel,"길드 소개",35,100,240,45,27,Color.white,TextAlignmentOptions.MidlineLeft);
            var description=GInput("Description",panel,285,97,790,58,"길드 소개",100);description.text=g.Description;
            Text("NoticeLabel",panel,"공지",35,185,240,45,27,Color.white,TextAlignmentOptions.MidlineLeft);
            var notice=GInput("Notice",panel,285,180,790,140,"공지 (최대 300자)",300);notice.text=g.Notice;
            notice.lineType=TMP_InputField.LineType.MultiLineNewline;notice.textComponent.alignment=TextAlignmentOptions.TopLeft;
            Text("ModeLabel",panel,"가입 방식",35,351,240,45,27,Color.white,TextAlignmentOptions.MidlineLeft);
            var label=Text("Mode",panel,mode=="Open"?"현재 선택: 자유 가입":"현재 선택: 승인 가입",285,420,760,43,24,Yellow,TextAlignmentOptions.MidlineLeft);
            GButton("자유 가입",panel,285,350,370,54,()=>{mode="Open";GuildSelectMode(panel,mode);label.text="현재 선택: 자유 가입";},true,mode=="Open");
            GButton("승인 가입",panel,685,350,370,54,()=>{mode="Approval";GuildSelectMode(panel,mode);label.text="현재 선택: 승인 가입";},true,mode=="Approval");
            GButton("저장",panel,720,518,350,54,()=>GuildAction(()=>GameApiClient.Request<object>(GuildApi+"/"+g.Id,"PUT",new GuildEdit{Description=description.text,Notice=notice.text,JoinMode=mode,Revision=g.Revision}),"길드 정보를 저장했습니다."),true,true);
        }
        void GuildNoticeView()
        {
            var panel=GuildForm("길드 공지",500);
            var text=Text("Notice",panel,string.IsNullOrWhiteSpace(guildState.Guild.Notice)?"등록된 공지가 없습니다.":guildState.Guild.Notice,40,100,1040,325,27,Color.white,TextAlignmentOptions.TopLeft,true);
            text.textWrappingMode=TextWrappingModes.Normal;
        }
        void GuildSelectMode(Transform panel,string mode)
        {
            foreach(string label in new[]{"자유 가입","승인 가입"})
            {
                bool selected=label==(mode=="Open"?"자유 가입":"승인 가입");
                panel.Find(label+"Fill").GetComponent<UnityEngine.UI.Image>().color=selected?Yellow:GuildInk;
                panel.Find(label+"Border").GetComponent<UnityEngine.UI.Image>().color=selected?Yellow:Color.cyan;
                panel.Find(label+"Text").GetComponent<TMP_Text>().color=selected?Color.black:Color.white;
            }
        }
        void GuildInvitations()
        {
            var panel=GuildForm("받은 길드 초대");var host=Node("Invitations",panel,30,100,1060,425);var content=GScroll(host,425,out _);
            var invitations=guildState.Invitations;content.sizeDelta=new Vector2(1060,Math.Max(425,invitations.Length*100));
            for(int i=0;i<invitations.Length;i++)
            {
                var invitation=invitations[i];float y=i*100;
                Text("Guild",content,"["+invitation.Tag+"] "+invitation.GuildName,12,y,620,44,29,Color.white,TextAlignmentOptions.MidlineLeft,true);
                Text("Inviter",content,invitation.InviterName+"님의 초대",12,y+47,620,39,23,Color.gray,TextAlignmentOptions.MidlineLeft,true);
                GButton("가입",content,666,y+20,173,51,()=>GuildAction(async()=>{await GameApiClient.Request<GuildReply>(GuildApi+"/invitations/"+invitation.Id,"POST",new GuildChoice{Accept=true});guildTab=0;},"길드에 가입했습니다."),GuildId==null,true);
                GButton("거절",content,862,y+20,173,51,()=>GuildAction(()=>GameApiClient.Request<GuildReply>(GuildApi+"/invitations/"+invitation.Id,"POST",new GuildChoice{Accept=false}),"초대를 거절했습니다."));
            }
            if(invitations.Length==0)Text("Empty",content,"받은 초대가 없습니다.",0,100,1060,80,28,Color.gray);
        }
        void GuildApplications()
        {
            var guild=GuildId;var panel=GuildForm("가입 신청 관리");var host=Node("Applications",panel,30,100,1060,425);var content=GScroll(host,425,out _);
            var applicants=guildState.Applications;content.sizeDelta=new Vector2(1060,Math.Max(425,applicants.Length*83));
            for(int i=0;i<applicants.Length;i++)
            {
                var applicant=applicants[i];float y=i*83;
                Text("Applicant",content,applicant.Name+"    Lv."+applicant.Level,12,y,630,73,28,Color.white,TextAlignmentOptions.MidlineLeft,true);
                GButton("승인",content,666,y+10,173,51,()=>GuildAction(()=>GameApiClient.Request<object>(GuildApi+"/"+guild+"/applications/"+applicant.UserId,"POST",new GuildChoice{Accept=true}),"가입을 승인했습니다."),true,true);
                GButton("거절",content,862,y+10,173,51,()=>GuildAction(()=>GameApiClient.Request<object>(GuildApi+"/"+guild+"/applications/"+applicant.UserId,"POST",new GuildChoice{Accept=false}),"가입 신청을 거절했습니다."));
            }
            if(applicants.Length==0)Text("Empty",content,"대기 중인 가입 신청이 없습니다.",0,100,1060,80,28,Color.gray);
        }
        void GuildInviteSearch()
        {
            string guild=GuildId;var panel=GuildForm("길드원 초대");
            var input=GInput("Nickname",panel,35,97,790,52,"초대할 회원의 닉네임",24);
            var results=Node("InviteResults",panel,35,174,1040,356);int search=0;
            async void Search()
            {
                int version=++search;Clear(results);
                try
                {
                    var people=await GameApiClient.Request<FriendSocial.People>(FriendSocial.Api+"/search?q="+Uri.EscapeDataString(input.text.Trim()));
                    if(!results || !results.gameObject.activeInHierarchy || version!=search || guild!=GuildId)return;
                    var content=GScroll(results,356,out _);content.sizeDelta=new Vector2(1040,Math.Max(356,people.Rows.Length*73));
                    for(int i=0;i<people.Rows.Length;i++)
                    {
                        var person=people.Rows[i];float y=i*73;
                        Text("Member",content,person.Name+"   Lv."+person.Level,12,y,710,62,27,Color.white,TextAlignmentOptions.MidlineLeft,true);
                        GButton("초대 보내기",content,744,y+6,265,50,()=>GuildAction(()=>GameApiClient.Request<GuildReply>(GuildApi+"/"+guild+"/invitations","POST",new GuildInviteInput{UserId=person.Id}),"초대를 보냈습니다."),true,true);
                    }
                    if(people.Rows.Length==0)Text("Empty",content,"검색된 회원이 없습니다.",0,70,1040,70,27,Color.gray);
                }
                catch(Exception e){if(results && results.gameObject.activeInHierarchy && version==search)GuildError(e);}
            }
            input.onSubmit.AddListener(_=>Search());GButton("검색",panel,850,97,225,52,Search,true,true);
        }
        async void GuildRoleManagement()
        {
            int version=guildVersion;string guild=GuildId;
            try
            {
                var members=await GameApiClient.Request<GuildMembers>(GuildApi+"/"+guild+"/members");if(!GuildCurrent(version))return;
                var panel=GuildForm("직책·길드원 관리");var host=Node("ManageMembers",panel,30,100,1060,425);var content=GScroll(host,425,out _);
                var rows=members.Rows.Where(m=>m.Id!=guildOwner).ToArray();content.sizeDelta=new Vector2(1060,Math.Max(425,rows.Length*94));
                bool leader=guildState.Guild.MyRole=="Leader";
                for(int i=0;i<rows.Length;i++)
                {
                    var member=rows[i];float y=i*94;
                    Text("Name",content,member.Name+"  ("+GRole(member.Role)+")",10,y,450,76,25,Color.white,TextAlignmentOptions.MidlineLeft,true);
                    if(leader)
                    {
                        string role=member.Role=="Officer"?"Member":"Officer";
                        GButton(role=="Officer"?"부길드장":"일반회원",content,465,y+14,177,48,()=>GuildAction(()=>GameApiClient.Request<object>(GuildApi+"/"+guild+"/members/"+member.Id+"/role","PUT",new GuildRoleInput{Role=role}),"직책을 변경했습니다."));
                        GButton("길드장 위임",content,650,y+14,180,48,()=>GuildConfirm("길드장 위임",member.Name+"님에게 길드장을 위임하시겠습니까?\n내 직책은 부길드장으로 변경됩니다.",()=>GuildAction(()=>GameApiClient.Request<object>(GuildApi+"/"+guild+"/members/"+member.Id+"/role","PUT",new GuildRoleInput{Role="Leader"}),"길드장을 위임했습니다.")));
                    }
                    GButton("내보내기",content,848,y+14,185,48,()=>GuildConfirm("길드원 내보내기",member.Name+"님을 길드에서 내보내시겠습니까?",()=>GuildAction(()=>GameApiClient.Request<object>(GuildApi+"/"+guild+"/members/"+member.Id,"DELETE"),"길드원을 내보냈습니다.")),member.Role!="Leader" && (leader || member.Role=="Member"));
                }
                if(rows.Length==0)Text("Empty",content,"다른 길드원이 없습니다.",0,70,1060,70,27,Color.gray);
            }
            catch(Exception e){if(GuildCurrent(version))GuildError(e);}
        }
    }
}
