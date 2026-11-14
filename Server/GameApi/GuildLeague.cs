using System.Security.Claims;
using System.Text.Json;
using Assets.Scripts.Prack.BackSys.GameData;
using Assets.Scripts.Prack.BackSys.ChartData;
using MagicOnline.MatchSettlement;
using Npgsql;
namespace MagicOnline.GameApi;

public sealed partial class GuildLeague(Database db,GameData data,TimeProvider clock)
{
    public const int DailyLimit=5;
    public static void Map(WebApplication app)
    {
        var g=app.MapGroup("/api/guild-league").RequireAuthorization();
        Guid Me(ClaimsPrincipal p)=>Guid.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!);
        g.MapGet("",(ClaimsPrincipal p,GuildLeague s)=>s.Read(Me(p)));
        g.MapGet("/rewards",(ClaimsPrincipal p,GuildLeague s)=>s.Rewards(Me(p)));
        g.MapPost("/queue",(GuildWarQueueInput b,ClaimsPrincipal p,GuildLeague s)=>s.Queue(Me(p),b.HallId));
        g.MapDelete("/queue",(ClaimsPrincipal p,GuildLeague s)=>s.Cancel(Me(p)));
        g.MapPost("/rewards/{season:int}",(int season,ClaimsPrincipal p,GuildLeague s)=>s.Claim(Me(p),season));
    }
    internal static readonly DateTimeOffset Anchor=new(2026,11,2,0,0,0,TimeSpan.FromHours(9));
    static (int Id,DateTimeOffset Start,DateTimeOffset End) Season(DateTimeOffset now)
    {
        int n=Math.Max(0,(int)Math.Floor((now-Anchor).TotalDays/28));var start=Anchor.AddDays(n*28).ToUniversalTime();return(n+1,start,start.AddDays(28));
    }
    public static string Tier(int points)=>points>=3000?"다이아 I":points>=2600?"다이아 II":points>=2200?"골드 I":points>=1800?"골드 II":points>=1400?"실버 I":points>=1000?"실버 II":"브론즈";
    static (long Coin,int Gem) Award(int points)=>points>=2600?(60000,200):points>=1800?(30000,100):points>=1000?(15000,50):(5000,20);
    static async Task Exec(NpgsqlConnection c,string sql,params object[] args){await using var q=Database.Command(c,sql,args);await q.ExecuteNonQueryAsync();}
    static async Task<Guid> Guild(NpgsqlConnection c,Guid user,bool gate=false)
    {
        await using var q=Database.Command(c,"SELECT m.guild_id FROM guild_members m JOIN users u ON u.id=m.user_id JOIN guilds g ON g.id=m.guild_id WHERE m.user_id=$1 AND NOT u.disabled AND u.deleted_at IS NULL AND g.deleted_at IS NULL"+(gate?" FOR UPDATE OF u":""),user);
        return await q.ExecuteScalarAsync() is Guid id?id:throw new ApiFault(403,"길드 가입 후 공식 길드전에 참여할 수 있습니다.");
    }
    async Task<(int Id,DateTimeOffset Start,DateTimeOffset End)> Prepare(NpgsqlConnection c)
    {
        var s=Season(clock.GetUtcNow());
        await Exec(c,"SELECT pg_advisory_xact_lock(687451018)");
        await Exec(c,"INSERT INTO guild_seasons(id,starts_at,ends_at) VALUES($1,$2,$3) ON CONFLICT DO NOTHING",s.Id,s.Start,s.End);
        // Finish only once all bound games have a server settlement. Unbound tickets expire after two minutes.
        var ended=new List<int>();await using(var q=Database.Command(c,"SELECT id FROM guild_seasons s WHERE ends_at<=$1 AND finalized_at IS NULL AND NOT EXISTS(SELECT FROM guild_wars w WHERE w.season_id=s.id AND w.bound AND w.settled_at IS NULL) ORDER BY id",clock.GetUtcNow()))
        await using(var r=await q.ExecuteReaderAsync())while(await r.ReadAsync())ended.Add(r.GetInt32(0));
        foreach(int id in ended)
        {
            await Exec(c,"UPDATE guild_season_scores s SET final_rank=r.rank FROM (SELECT guild_id,row_number() OVER(ORDER BY points DESC,wins DESC,guild_id)::int rank FROM guild_season_scores WHERE season_id=$1) r WHERE s.season_id=$1 AND s.guild_id=r.guild_id",id);
            var awards=new List<(Guid User,Guid Guild,int Points)>();
            await using(var q=Database.Command(c,"SELECT c.user_id,c.guild_id,s.points FROM guild_contributions c JOIN guild_season_scores s USING(season_id,guild_id) JOIN users u ON u.id=c.user_id JOIN guild_seasons season ON season.id=c.season_id WHERE c.season_id=$1 AND c.games>=10 AND EXISTS(SELECT FROM guild_membership_spans m WHERE m.user_id=c.user_id AND m.guild_id=c.guild_id AND m.joined_at<=season.ends_at AND (m.left_at IS NULL OR m.left_at>=season.ends_at)) AND NOT u.disabled AND u.deleted_at IS NULL",id))
            await using(var r=await q.ExecuteReaderAsync())while(await r.ReadAsync())awards.Add((r.GetGuid(0),r.GetGuid(1),r.GetInt32(2)));
            foreach(var a in awards){var reward=Award(a.Points);await Exec(c,"INSERT INTO guild_season_rewards(season_id,user_id,guild_id,tier,coin,gem,emblem) VALUES($1,$2,$3,$4,$5,$6,$7) ON CONFLICT DO NOTHING",id,a.User,a.Guild,Tier(a.Points),reward.Coin,reward.Gem,$"GuildSeason:{id}:{Tier(a.Points)}");}
            await Exec(c,"UPDATE guild_seasons SET finalized_at=$2 WHERE id=$1",id,clock.GetUtcNow());
        }
        return s;
    }
    static DateTimeOffset Day(DateTimeOffset now)=>new DateTimeOffset(now.ToOffset(TimeSpan.FromHours(9)).Date,TimeSpan.FromHours(9)).ToUniversalTime();
    static async Task<int> Today(NpgsqlConnection c,Guid user,DateTimeOffset now)
    {
        await using var q=Database.Command(c,"SELECT count(*) FROM guild_wars WHERE (user_a=$1 OR user_b=$1) AND created_at>=$2 AND (result_a IN ('Win','Lose','Draw') OR settled_at IS NULL AND (bound OR created_at>now()-interval '2 minutes'))",user,Day(now));return checked((int)(long)(await q.ExecuteScalarAsync())!);
    }
    public async Task<GuildWarTicket> Queue(Guid user,int hallId)
    {
        var halls=await data.Chart("SkillMatch");var h=halls.EnumerateArray().FirstOrDefault(x=>x.GetProperty("HallIdx").GetInt32()==hallId);
        if(h.ValueKind==JsonValueKind.Undefined)throw new ApiFault(400,"경기장을 선택해 주세요.");
        var settings=JsonSerializer.Deserialize<SkillMatchData>(h.GetRawText(),Database.Json)!;
        if(settings.MatchTotMin is <1 or >30)throw new ApiFault(400,"공식전은 30분 이하 경기장만 지원합니다.");
        await using var c=await db.Source.OpenConnectionAsync();await using var tx=await c.BeginTransactionAsync();var s=await Prepare(c);var guild=await Guild(c,user,true);
        await Exec(c,"DELETE FROM guild_war_queue WHERE seen_at<now()-interval '20 seconds' OR season_id<>$1",s.Id);
        await using(var q=Database.Command(c,"SELECT match_id,user_a,user_b,hall_id FROM guild_wars WHERE (user_a=$1 OR user_b=$1) AND settled_at IS NULL AND created_at>now()-interval '2 minutes' ORDER BY created_at DESC LIMIT 1",user))
        await using(var r=await q.ExecuteReaderAsync())if(await r.ReadAsync())return new(){State="Matched",MatchId=r.GetString(0),OpponentUserId=(r.GetGuid(1)==user?r.GetGuid(2):r.GetGuid(1)).ToString(),HallId=r.GetInt32(3)};
        if(clock.GetUtcNow()>=s.End.AddMinutes(-35))throw new ApiFault(409,"시즌 마감 35분 전에는 새 공식전을 시작할 수 없습니다.");
        if(await Today(c,user,clock.GetUtcNow())>=DailyLimit)throw new ApiFault(409,"오늘 점수 반영 5경기를 모두 참여했습니다.");
        await using(var q=Database.Command(c,"SELECT 1 FROM match_players WHERE user_id=$1 AND active",user))if(await q.ExecuteScalarAsync()!=null)throw new ApiFault(409,"진행 중인 경기가 있습니다.");
        await using(var q=Database.Command(c,"SELECT 1 FROM friend_challenges WHERE (sender=$1 OR recipient=$1) AND state<>'closed' AND expires_at>now()",user))if(await q.ExecuteScalarAsync()!=null)throw new ApiFault(409,"친선경기 요청을 먼저 종료해 주세요.");
        await Exec(c,"INSERT INTO guild_season_scores(season_id,guild_id) VALUES($1,$2) ON CONFLICT DO NOTHING",s.Id,guild);
        Guid other=Guid.Empty,otherGuild=Guid.Empty;
        await using(var q=Database.Command(c,"""
            SELECT q.user_id,q.guild_id FROM guild_war_queue q JOIN guild_members m ON m.user_id=q.user_id AND m.guild_id=q.guild_id
            JOIN users u ON u.id=q.user_id JOIN guilds g ON g.id=q.guild_id
            JOIN guild_season_scores theirs ON theirs.guild_id=q.guild_id AND theirs.season_id=q.season_id
            JOIN guild_season_scores own ON own.guild_id=$2 AND own.season_id=q.season_id
            WHERE q.season_id=$1 AND q.guild_id<>$2 AND q.hall_id=$3 AND NOT u.disabled AND u.deleted_at IS NULL AND g.deleted_at IS NULL
            AND abs(own.points-theirs.points)<=400 AND NOT EXISTS(SELECT FROM match_players WHERE user_id=q.user_id AND active)
            AND abs(u.level-(SELECT level FROM users WHERE id=$4))<=10
            AND NOT EXISTS(SELECT FROM guild_wars w WHERE w.created_at>=date_trunc('day',now() AT TIME ZONE 'Asia/Seoul') AT TIME ZONE 'Asia/Seoul' AND ((w.user_a=$4 AND w.user_b=q.user_id) OR (w.user_b=$4 AND w.user_a=q.user_id)) AND (w.result_a IN ('Win','Lose','Draw') OR w.settled_at IS NULL AND (w.bound OR w.created_at>now()-interval '2 minutes')))
            ORDER BY q.created_at LIMIT 1
            """,s.Id,guild,hallId,user))
        await using(var r=await q.ExecuteReaderAsync())if(await r.ReadAsync()){other=r.GetGuid(0);otherGuild=r.GetGuid(1);}
        if(other!=Guid.Empty && await Today(c,other,clock.GetUtcNow())<DailyLimit)
        {
            string id=Guid.NewGuid().ToString("N");
            await Exec(c,"INSERT INTO guild_wars(match_id,season_id,user_a,user_b,guild_a,guild_b,hall_id) VALUES($1,$2,$3,$4,$5,$6,$7)",id,s.Id,user,other,guild,otherGuild,hallId);
            await Exec(c,"DELETE FROM guild_war_queue WHERE user_id IN ($1,$2)",user,other);await tx.CommitAsync();return new(){State="Matched",MatchId=id,OpponentUserId=other.ToString(),HallId=hallId};
        }
        await Exec(c,"INSERT INTO guild_war_queue(user_id,guild_id,season_id,hall_id) VALUES($1,$2,$3,$4) ON CONFLICT(user_id) DO UPDATE SET guild_id=excluded.guild_id,season_id=excluded.season_id,hall_id=excluded.hall_id,seen_at=now()",user,guild,s.Id,hallId);
        await tx.CommitAsync();return new(){State="Searching",HallId=hallId};
    }
    public async Task Cancel(Guid user)
    {
        await using var c=await db.Source.OpenConnectionAsync();await using var tx=await c.BeginTransactionAsync();await Exec(c,"SELECT pg_advisory_xact_lock(687451018)");
        await Exec(c,"DELETE FROM guild_war_queue WHERE user_id=$1",user);
        await Exec(c,"UPDATE guild_wars SET settled_at=now(),result_a='Void' WHERE (user_a=$1 OR user_b=$1) AND NOT bound AND settled_at IS NULL",user);await tx.CommitAsync();
    }
    internal static async Task<bool> IsOfficial(NpgsqlConnection c,string id,Guid a,Guid b)
    {
        await using var q=Database.Command(c,"SELECT user_a,user_b FROM guild_wars WHERE match_id=$1",id);await using var r=await q.ExecuteReaderAsync();if(!await r.ReadAsync())return false;
        if(!(r.GetGuid(0)==a && r.GetGuid(1)==b || r.GetGuid(0)==b && r.GetGuid(1)==a))throw new ApiFault(403,"길드전 참가자가 다릅니다.");return true;
    }
    internal static void Bind(NpgsqlConnection c,VerifiedRegistration r)
    {
        using(var exists=Database.Command(c,"SELECT to_regclass('guild_wars') IS NOT NULL"))if(!(bool)exists.ExecuteScalar()!)return;
        using var q=Database.Command(c,"UPDATE guild_wars w SET bound=true WHERE match_id=$1 AND NOT bound AND settled_at IS NULL AND created_at>now()-interval '2 minutes' AND hall_id=$2 AND EXISTS(SELECT FROM guild_members WHERE user_id=w.user_a AND guild_id=w.guild_a) AND EXISTS(SELECT FROM guild_members WHERE user_id=w.user_b AND guild_id=w.guild_b) AND EXISTS(SELECT FROM guild_seasons WHERE id=w.season_id AND ends_at>now())",r.MatchId,r.HallId);
        int changed=q.ExecuteNonQuery();using var check=Database.Command(c,"SELECT 1 FROM guild_wars WHERE match_id=$1",r.MatchId);
        if(changed==0 && check.ExecuteScalar()!=null)throw new ApiFault(409,"길드전 매칭이 종료되었습니다.");
    }
    internal static void Apply(NpgsqlConnection c,SettlementReceipt receipt)
    {
        using(var exists=Database.Command(c,"SELECT to_regclass('guild_wars') IS NOT NULL"))if(!(bool)exists.ExecuteScalar()!)return;
        int season;Guid a,b,ga,gb;
        using(var q=Database.Command(c,"SELECT season_id,user_a,user_b,guild_a,guild_b FROM guild_wars WHERE match_id=$1 AND bound AND settled_at IS NULL FOR UPDATE",receipt.MatchId))
        using(var r=q.ExecuteReader()){if(!r.Read())return;season=r.GetInt32(0);a=r.GetGuid(1);b=r.GetGuid(2);ga=r.GetGuid(3);gb=r.GetGuid(4);}
        string result=a.ToString()==receipt.UserA?receipt.ResultA:receipt.ResultB;bool counted=result is "Win" or "Lose" or "Draw";
        if(receipt.Match!=null)receipt.Match.MatchMode="GuildWar";
        int da=result=="Win"?24:result=="Lose"?-12:result=="Draw"?4:0,db=result=="Lose"?24:result=="Win"?-12:result=="Draw"?4:0;
        foreach(var row in new[]{(User:a,Guild:ga,Delta:da,Result:result),(User:b,Guild:gb,Delta:db,Result:result=="Win"?"Lose":result=="Lose"?"Win":result)}.OrderBy(x=>x.Guild))
        {
            if(!counted)continue;
            using(var q=Database.Command(c,"INSERT INTO guild_season_scores(season_id,guild_id) VALUES($1,$2) ON CONFLICT DO NOTHING",season,row.Guild))q.ExecuteNonQuery();
            using(var q=Database.Command(c,"UPDATE guild_season_scores SET points=greatest(0,points+$3),wins=wins+$4,losses=losses+$5,draws=draws+$6 WHERE season_id=$1 AND guild_id=$2",season,row.Guild,row.Delta,row.Result=="Win"?1:0,row.Result=="Lose"?1:0,row.Result=="Draw"?1:0))q.ExecuteNonQuery();
            using(var q=Database.Command(c,"INSERT INTO guild_contributions(season_id,guild_id,user_id,games,points) VALUES($1,$2,$3,1,$4) ON CONFLICT(season_id,guild_id,user_id) DO UPDATE SET games=guild_contributions.games+1,points=guild_contributions.points+excluded.points",season,row.Guild,row.User,row.Delta))q.ExecuteNonQuery();
        }
        using var save=Database.Command(c,"UPDATE guild_wars SET settled_at=now(),result_a=$2,delta_a=$3,delta_b=$4 WHERE match_id=$1",receipt.MatchId,result,da,db);save.ExecuteNonQuery();
    }
    internal static void VoidDispute(NpgsqlConnection c,string match)
    {
        using(var exists=Database.Command(c,"SELECT to_regclass('guild_wars') IS NOT NULL"))if(!(bool)exists.ExecuteScalar()!)return;
        using var q=Database.Command(c,"UPDATE guild_wars SET settled_at=now(),result_a='Void' WHERE match_id=$1 AND bound AND settled_at IS NULL",match);q.ExecuteNonQuery();
    }
    internal static async Task MemberRemoved(NpgsqlConnection c,Guid user)
    {
        await using(var exists=Database.Command(c,"SELECT to_regclass('guild_wars') IS NOT NULL"))if(!(bool)(await exists.ExecuteScalarAsync())!)return;
        await Exec(c,"DELETE FROM guild_war_queue WHERE user_id=$1",user);
        await Exec(c,"UPDATE guild_wars SET settled_at=now(),result_a='Void' WHERE (user_a=$1 OR user_b=$1) AND NOT bound AND settled_at IS NULL",user);
    }
}
