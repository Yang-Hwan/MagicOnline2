using System;
using System.Linq;
using Assets.Scripts.Exert.BackSys;
using Assets.Scripts.Exert.Network;
using Assets.Scripts.Prack.BackSys.GameData;
using TMPro;
using UnityEngine;
namespace Assets.Scripts.Sight.Surface.Consist
{
    public sealed partial class BottomMenuView
    {
        GuildLeagueState guildLeague;
        int leagueTab,leaguePage,leagueBall=4;
        string leagueTier="";
        bool leagueSearching,leaguePolling;
        int leagueSearchVersion;
        readonly System.Threading.SemaphoreSlim leagueQueueGate=new System.Threading.SemaphoreSlim(1,1);
        float leagueNextQueue;
        public bool IsGuildWarSearching=>leagueSearching;
        void DrawGuildLeague()
        {
            if(!guildBody || guildTab!=3)return;Clear(guildBody);
            string[] tabs={"시즌 현황","길드 순위","기여도·보상"};
            for(int i=0;i<3;i++){int index=i;GButton(tabs[i],guildBody,i*512,0,499,48,()=>{leagueTab=index;leaguePage=0;DrawGuildLeague();},true,leagueTab==i);}
            if(guildLeague==null){Text("LoadingLeague",guildBody,"시즌 정보를 불러오는 중…",0,180,1536,60,29,Color.white);return;}
            var s=guildLeague;var own=s.Own;
            Text("Season",guildBody,$"시즌 {s.Season:00} · 4주 리그",8,55,640,43,30,Color.white,TextAlignmentOptions.MidlineLeft);
            Text("Deadline",guildBody,$"{DateTimeOffset.FromUnixTimeMilliseconds(s.StartsAt).ToOffset(TimeSpan.FromHours(9)):MM.dd} ~ {DateTimeOffset.FromUnixTimeMilliseconds(s.EndsAt).ToOffset(TimeSpan.FromHours(9)).AddDays(-1):MM.dd}   ·   시즌 종료 D-{s.DaysLeft}",660,55,870,43,26,Yellow,TextAlignmentOptions.MidlineRight);
            if(leagueTab==0)
            {
                var card=GPanel("LeagueGuild",guildBody,0,108,434,442);
                Node("Emblem",card,25,16,130,130).gameObject.AddComponent<GuildEmblemGraphic>();
                Text("Guild",card,"["+guildState.Guild.Tag+"]\n"+guildState.Guild.Name,170,22,240,110,30,Color.white,autoSize:true);
                Text("Tier",card,own.Tier,22,160,390,62,42,Yellow);
                Text("Points",card,$"{own.Points:N0}점   ·   전체 {own.Rank}위",22,240,390,50,30,Color.white);
                Text("Record",card,$"{own.Wins}승 / {own.Losses}패 / {own.Draws}무",22,304,390,43,25,Color.white);
                Text("Policy",card,"승리 +24 · 패배 -12 · 무승부 +4",15,378,402,40,23,Color.cyan);
                var match=GPanel("LeagueMatch",guildBody,454,108,1082,281);
                for(int i=0;i<4;i++)Text("Week"+i,match,$"{i+1}주차"+(s.Week==i+1?" 진행 중":s.Week>i+1?" 완료":""),18+i*264,10,252,43,24,s.Week==i+1?Yellow:Color.gray);
                Text("Official",match,"1대1 공식 길드전",24,65,700,55,37,Color.white,TextAlignmentOptions.MidlineLeft);
                Text("Quota",match,$"오늘 점수 반영 {s.TodayGames} / {s.DailyLimit}경기",24,128,650,42,27,Color.white,TextAlignmentOptions.MidlineLeft);
                GButton("4구",match,708,70,155,46,()=>{leagueBall=4;DrawGuildLeague();},!leagueSearching,leagueBall==4);
                GButton("3구",match,881,70,155,46,()=>{leagueBall=3;DrawGuildLeague();},!leagueSearching,leagueBall==3);
                GButton(leagueSearching?"매칭 취소":"공식 길드전 시작",match,647,184,390,61,()=>{if(leagueSearching)StopGuildWarSearch();else StartGuildWarSearch();DrawGuildLeague();},leagueSearching || s.TodayGames<s.DailyLimit,true);
                Text("MatchPolicy",match,leagueSearching?"비슷한 실력의 다른 길드원을 찾는 중…":"입장 무료 · 등급과 레벨이 비슷한 상대 매칭\n같은 상대는 하루 한 번만 점수 반영",24,190,593,67,22,Color.cyan,TextAlignmentOptions.MidlineLeft,true);
                var recent=GPanel("RecentGuildWars",guildBody,454,405,1082,145);
                Text("RecentTitle",recent,"최근 길드전",15,8,260,35,25,Yellow,TextAlignmentOptions.MidlineLeft);
                for(int i=0;i<2 && i<s.Recent.Length;i++){var r=s.Recent[i];Text("Result"+i,recent,$"{ResultLabel(r.Result)}   {r.Delta:+0;-0;0}점   vs {r.Opponent}",20,49+i*43,1035,40,25,Color.white,TextAlignmentOptions.MidlineLeft,true);}
                if(s.Recent.Length==0)Text("Empty",recent,"아직 완료된 공식 길드전이 없습니다.",20,60,1035,55,25,Color.gray);
            }
            else if(leagueTab==1)DrawLeagueRanks();else DrawLeagueRewards();
        }
        static string ResultLabel(string r)=>r=="Win"?"승리":r=="Lose"?"패배":r=="Draw"?"무승부":"취소";
        async void GuildRewardInbox()
        {
            int version=guildVersion;
            try
            {
                var state=await GameApiClient.Request<GuildLeagueState>("/api/guild-league/rewards");if(!GuildCurrent(version))return;
                var panel=GuildForm("시즌 보상함");var host=Node("Rewards",panel,30,100,1060,425);var rows=GScroll(host,425,out _);rows.sizeDelta=new Vector2(1060,Math.Max(425,state.Rewards.Length*98));
                for(int i=0;i<state.Rewards.Length;i++)
                {
                    var r=state.Rewards[i];Text("Reward",rows,$"시즌 {r.Season} · {r.Tier}\n{r.Coin:N0} 코인 · {r.Gem} 보석 · 시즌 문장",10,i*98,720,87,26,Color.white,TextAlignmentOptions.MidlineLeft,true);
                    GButton(r.Claimed?"수령 완료":"보상 수령",rows,750,i*98+17,275,55,()=>GuildAction(async()=>{await GameApiClient.Request<GuildLeagueReward>("/api/guild-league/rewards/"+r.Season,"POST");RefreshWalletFromServer();},"시즌 보상을 수령했습니다."),!r.Claimed,true);
                }
                if(state.Rewards.Length==0)Text("Empty",rows,"수령 가능한 시즌 보상이 없습니다.\n공식전 10경기 이상 참여하고 시즌 종료 시 소속을 유지해 주세요.",0,100,1060,150,28,Color.gray,autoSize:true);
            }
            catch(Exception e){if(GuildCurrent(version))GuildError(e);}
        }
        void DrawLeagueRanks()
        {
            var own=guildLeague.Own;
            Text("OwnRank",guildBody,$"내 길드 {own.Rank}위 · [{own.Tag}] {own.Name} · {own.Tier} · {own.Points:N0}점",0,105,1536,45,29,Yellow,autoSize:true);
            string[] tiers={"전체","브론즈","실버","골드","다이아"};
            for(int i=0;i<5;i++){string filter=i==0?"":tiers[i];GButton(tiers[i],guildBody,i*233,158,220,43,()=>{leagueTier=filter;leaguePage=0;DrawGuildLeague();},true,leagueTier==filter);}
            var rows=guildLeague.Rankings.Where(x=>leagueTier=="" || x.Tier.StartsWith(leagueTier)).ToArray();int pages=Math.Max(1,(rows.Length+5)/6);leaguePage=Mathf.Clamp(leaguePage,0,pages-1);
            Text("RankColumn",guildBody,"순위",5,212,115,35,23,Color.gray);
            Text("GuildColumn",guildBody,"길드",125,212,650,35,23,Color.gray,TextAlignmentOptions.MidlineLeft);
            Text("TierColumn",guildBody,"등급",785,212,230,35,23,Color.gray);
            Text("ScoreColumn",guildBody,"시즌 점수",1020,212,220,35,23,Color.gray);
            Text("RecordColumn",guildBody,"전적",1250,212,280,35,23,Color.gray);
            for(int i=0;i<6 && leaguePage*6+i<rows.Length;i++)
            {
                var r=rows[leaguePage*6+i];float y=251+i*39;Color color=r.GuildId==GuildId?Yellow:Color.white;
                Text("Rank",guildBody,r.Rank.ToString(),5,y,115,35,25,color);
                Text("GuildName",guildBody,$"[{r.Tag}] {r.Name}",125,y,650,35,25,color,TextAlignmentOptions.MidlineLeft,true);
                Text("Tier",guildBody,r.Tier,785,y,230,35,25,color);
                Text("Score",guildBody,r.Points.ToString("N0"),1020,y,220,35,25,color);
                Text("Record",guildBody,$"{r.Wins}승 {r.Losses}패 {r.Draws}무",1250,y,280,35,23,color);
            }
            GButton("이전",guildBody,540,505,140,43,()=>{leaguePage--;DrawGuildLeague();},leaguePage>0);
            Text("Page",guildBody,$"{leaguePage+1} / {pages}",695,505,145,43,24,Color.white);
            GButton("다음",guildBody,855,505,140,43,()=>{leaguePage++;DrawGuildLeague();},leaguePage+1<pages);
            GButton("내 길드 위치",guildBody,1190,505,340,43,()=>{leagueTier="";leaguePage=Array.FindIndex(guildLeague.Rankings,x=>x.GuildId==GuildId)/6;DrawGuildLeague();});
        }
        void DrawLeagueRewards()
        {
            var s=guildLeague;var left=GPanel("Contributions",guildBody,0,108,747,442);var right=GPanel("Rewards",guildBody,766,108,770,442);
            Text("Title",left,"우리 길드 기여도",20,10,700,48,32,Color.white,TextAlignmentOptions.MidlineLeft);
            Text("Mine",left,$"내 기여도 {s.MyPoints:+0;-0;0}점 · {s.MyGames}경기 · "+(s.MyRank==0?"미참여":$"길드 내 {s.MyRank}위"),20,65,700,48,25,Color.cyan,autoSize:true);
            var host=Node("ContributionList",left,18,125,708,295);var content=GScroll(host,295,out _);content.sizeDelta=new Vector2(708,Math.Max(295,s.Contributions.Length*51));
            for(int i=0;i<s.Contributions.Length;i++){var r=s.Contributions[i];Text("Contributor",content,$"{r.Rank}   {r.Name}    {r.Games}경기    {r.Points:+0;-0;0}점",5,i*51,695,45,24,r.UserId==guildOwner?Yellow:Color.white,TextAlignmentOptions.MidlineLeft,true);}
            if(s.Contributions.Length==0)Text("Empty",content,"공식 길드전에 참여해 기여도를 쌓아 보세요.",0,60,700,90,25,Color.gray);
            Text("Title",right,"시즌 보상",22,10,720,48,32,Color.white,TextAlignmentOptions.MidlineLeft);
            int points=s.Own.Points;long coin=points>=2600?60000:points>=1800?30000:points>=1000?15000:5000;int gem=points>=2600?200:points>=1800?100:points>=1000?50:20;
            Text("Expected",right,$"현재 예상 · {s.Own.Tier}\n{coin:N0} 코인 + {gem} 보석 + 시즌 문장",20,65,730,96,29,Yellow,autoSize:true);
            Text("Condition",right,$"수령 조건: 공식전 10경기 이상 ({s.MyGames}/10)\n시즌 종료 시 길드 소속 유지 · 최종 등급으로 확정",20,168,730,68,23,Color.white,autoSize:true);
            Text("RewardGuide",right,"브론즈 5,000/20 · 실버 15,000/50\n골드 30,000/100 · 다이아 60,000/200 (코인/보석)",20,242,730,66,22,Color.cyan,autoSize:true);
            var rewardHost=Node("RewardList",right,20,320,730,103);var rewardRows=GScroll(rewardHost,103,out _);rewardRows.sizeDelta=new Vector2(730,Math.Max(103,s.Rewards.Length*89));
            for(int i=0;i<s.Rewards.Length;i++)
            {
                var r=s.Rewards[i];Text("Reward",rewardRows,$"시즌 {r.Season} · {r.Tier}\n{r.Coin:N0} 코인 · {r.Gem} 보석 · 시즌 문장",0,i*89,470,80,23,Color.white,TextAlignmentOptions.MidlineLeft,true);
                GButton(r.Claimed?"수령 완료":"보상 수령",rewardRows,480,i*89+14,235,52,()=>GuildAction(async()=>{await GameApiClient.Request<GuildLeagueReward>("/api/guild-league/rewards/"+r.Season,"POST");RefreshUser();RefreshWalletFromServer();},"시즌 보상을 수령했습니다."),!r.Claimed,true);
            }
            if(s.Rewards.Length==0)Text("Pending",rewardRows,"시즌 종료 후 조건 충족 시 수령 가능",0,10,720,70,25,Color.gray);
        }
        void StartGuildWarSearch()
        {
            if(leagueSearching)return;
            if(!Photon.Pun.PhotonNetwork.IsConnectedAndReady || Photon.Pun.PhotonNetwork.InRoom){GuildError(new Exception("경기 서버 연결을 확인해 주세요."));return;}
            leagueSearchVersion++;leagueSearching=true;leagueNextQueue=0;
        }
        void StopGuildWarSearch()
        {
            bool was=leagueSearching;leagueSearching=false;leagueSearchVersion++;
            if(was && Application.isPlaying && guildOwner==GameApiClient.UserId)CancelLeagueQueue();
        }
        async void CancelLeagueQueue()
        {
            string owner=guildOwner;await leagueQueueGate.WaitAsync();
            try{if(owner==GameApiClient.UserId)await GameApiClient.Request<object>("/api/guild-league/queue","DELETE");}
            catch(Exception e){Debug.LogWarning("[GuildLeague] "+e.Message);}
            finally{leagueQueueGate.Release();}
        }
        void TickGuildWarSearch()
        {
            if(Application.isPlaying && leagueSearching && !leaguePolling && Time.unscaledTime>=leagueNextQueue)PollGuildWarQueue();
        }
        async void PollGuildWarQueue()
        {
            int version=guildVersion,search=leagueSearchVersion;leaguePolling=true;leagueNextQueue=Time.unscaledTime+3;
            try
            {
                var hall=BackendChart.skillMatchData?.FirstOrDefault(x=>x.MatchBall==leagueBall && x.MatchTotMin>0 && x.MatchTotMin<=30);
                if(hall==null)throw new Exception("선택한 종목의 공식 경기장이 없습니다.");
                GuildWarTicket ticket;await leagueQueueGate.WaitAsync();
                try
                {
                    if(!GuildCurrent(version)||!leagueSearching||search!=leagueSearchVersion)return;
                    ticket=await GameApiClient.Request<GuildWarTicket>("/api/guild-league/queue","POST",new GuildWarQueueInput{HallId=hall.HallIdx});
                }
                finally{leagueQueueGate.Release();}
                if(!GuildCurrent(version)||!leagueSearching||search!=leagueSearchVersion)return;
                if(ticket.State=="Matched")
                {
                    if(!legacyRooms || !NetworkManager.TryGetExistingNetwork(out var net) || !(net is PunNetwork pun))throw new Exception("경기 서버 연결을 확인해 주세요.");
                    leagueSearching=false;CloseModal();legacyRooms.GotoGuildWarRoom(pun,ticket.HallId,ticket.MatchId,ticket.OpponentUserId);
                }
            }
            catch(Exception e){if(GuildCurrent(version)){StopGuildWarSearch();GuildError(e);if(guildTab==3)DrawGuildLeague();}}
            finally{leaguePolling=false;}
        }
    }
}
