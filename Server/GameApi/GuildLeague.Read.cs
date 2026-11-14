using Assets.Scripts.Prack.BackSys.GameData;
using Npgsql;
namespace MagicOnline.GameApi;
public sealed partial class GuildLeague
{
    public async Task<GuildLeagueState> Rewards(Guid user)
    {
        await using var c=await db.Source.OpenConnectionAsync();await using var tx=await c.BeginTransactionAsync();await Prepare(c);
        var rows=new List<GuildLeagueReward>();
        await using(var q=Database.Command(c,"SELECT season_id,tier,coin,gem,emblem,claimed_at IS NOT NULL FROM guild_season_rewards WHERE user_id=$1 ORDER BY season_id DESC LIMIT 24",user))
        await using(var r=await q.ExecuteReaderAsync())while(await r.ReadAsync())rows.Add(new(){Season=r.GetInt32(0),Tier=r.GetString(1),Coin=r.GetInt64(2),Gem=r.GetInt32(3),Emblem=r.GetString(4),Claimed=r.GetBoolean(5)});
        await tx.CommitAsync();return new(){Rewards=rows.ToArray()};
    }
    public async Task FinalizeSeasons()
    {
        await using var c=await db.Source.OpenConnectionAsync();await using var tx=await c.BeginTransactionAsync();await Prepare(c);await tx.CommitAsync();
    }
    public async Task<GuildLeagueState> Read(Guid user)
    {
        await using var c=await db.Source.OpenConnectionAsync();await using var tx=await c.BeginTransactionAsync();var s=await Prepare(c);var guild=await Guild(c,user);
        var state=new GuildLeagueState{Season=s.Id,StartsAt=s.Start.ToUnixTimeMilliseconds(),EndsAt=s.End.ToUnixTimeMilliseconds(),Week=Math.Clamp((int)(clock.GetUtcNow()-s.Start).TotalDays/7+1,1,4),DaysLeft=Math.Max(0,(int)Math.Ceiling((s.End-clock.GetUtcNow()).TotalDays)),TodayGames=await Today(c,user,clock.GetUtcNow())};
        await Exec(c,"INSERT INTO guild_season_scores(season_id,guild_id) VALUES($1,$2) ON CONFLICT DO NOTHING",s.Id,guild);
        var ranks=new List<GuildLeagueRank>();
        await using(var q=Database.Command(c,"SELECT s.guild_id,g.name,g.tag,s.points,s.wins,s.losses,s.draws,row_number() OVER(ORDER BY s.points DESC,s.wins DESC,s.guild_id)::int FROM guild_season_scores s JOIN guilds g ON g.id=s.guild_id WHERE s.season_id=$1 ORDER BY s.points DESC,s.wins DESC,s.guild_id",s.Id))
        await using(var r=await q.ExecuteReaderAsync())while(await r.ReadAsync())
        {
            var rank=new GuildLeagueRank{GuildId=r.GetGuid(0).ToString(),Name=r.GetString(1),Tag=r.GetString(2),Points=r.GetInt32(3),Wins=r.GetInt32(4),Losses=r.GetInt32(5),Draws=r.GetInt32(6),Rank=r.GetInt32(7)};rank.Tier=Tier(rank.Points);if(rank.GuildId==guild.ToString())state.Own=rank;ranks.Add(rank);
        }
        state.Rankings=ranks.ToArray();
        var contributions=new List<GuildLeagueContribution>();
        await using(var q=Database.Command(c,"SELECT c.user_id,CASE WHEN u.deleted_at IS NULL THEN u.nickname ELSE '탈퇴한 회원' END,c.games,c.points,row_number() OVER(ORDER BY c.points DESC,c.games DESC,c.user_id)::int FROM guild_contributions c JOIN users u ON u.id=c.user_id WHERE c.season_id=$1 AND c.guild_id=$2 ORDER BY c.points DESC,c.games DESC,c.user_id",s.Id,guild))
        await using(var r=await q.ExecuteReaderAsync())while(await r.ReadAsync())
        {
            var row=new GuildLeagueContribution{UserId=r.GetGuid(0).ToString(),Name=r.GetString(1),Games=r.GetInt32(2),Points=r.GetInt32(3),Rank=r.GetInt32(4)};contributions.Add(row);
            if(row.UserId==user.ToString()){state.MyGames=row.Games;state.MyPoints=row.Points;state.MyRank=row.Rank;}
        }
        state.Contributions=contributions.ToArray();
        var results=new List<GuildLeagueResult>();
        await using(var q=Database.Command(c,"SELECT w.match_id,g.name,CASE WHEN w.guild_a=$2 THEN w.result_a WHEN w.result_a='Win' THEN 'Lose' WHEN w.result_a='Lose' THEN 'Win' ELSE w.result_a END,CASE WHEN w.guild_a=$2 THEN w.delta_a ELSE w.delta_b END FROM guild_wars w JOIN guilds g ON g.id=CASE WHEN w.guild_a=$2 THEN w.guild_b ELSE w.guild_a END WHERE w.season_id=$1 AND (w.guild_a=$2 OR w.guild_b=$2) AND w.settled_at IS NOT NULL ORDER BY w.settled_at DESC LIMIT 10",s.Id,guild))
        await using(var r=await q.ExecuteReaderAsync())while(await r.ReadAsync())results.Add(new(){MatchId=r.GetString(0),Opponent=r.GetString(1),Result=r.GetString(2),Delta=r.GetInt32(3)});
        state.Recent=results.ToArray();
        var rewards=new List<GuildLeagueReward>();
        await using(var q=Database.Command(c,"SELECT season_id,tier,coin,gem,emblem,claimed_at IS NOT NULL FROM guild_season_rewards WHERE user_id=$1 ORDER BY season_id DESC LIMIT 24",user))
        await using(var r=await q.ExecuteReaderAsync())while(await r.ReadAsync())rewards.Add(new(){Season=r.GetInt32(0),Tier=r.GetString(1),Coin=r.GetInt64(2),Gem=r.GetInt32(3),Emblem=r.GetString(4),Claimed=r.GetBoolean(5)});
        state.Rewards=rewards.ToArray();await tx.CommitAsync();return state;
    }
    public async Task<GuildLeagueReward> Claim(Guid user,int season)
    {
        await using var c=await db.Source.OpenConnectionAsync();await using var tx=await c.BeginTransactionAsync();await Prepare(c);
        // Match settlement and admin wallet operations lock wallets before users too.
        await using(var q=Database.Command(c,"SELECT user_id FROM wallets WHERE user_id=$1 FOR UPDATE",user))if(await q.ExecuteScalarAsync()==null)throw new ApiFault(404,"회원을 찾을 수 없습니다.");
        await using(var q=Database.Command(c,"SELECT id FROM users WHERE id=$1 AND NOT disabled AND deleted_at IS NULL FOR UPDATE",user))if(await q.ExecuteScalarAsync()==null)throw new ApiFault(403,"이용할 수 없는 회원입니다.");
        GuildLeagueReward reward;
        await using(var q=Database.Command(c,"SELECT tier,coin,gem,emblem,claimed_at IS NOT NULL FROM guild_season_rewards WHERE season_id=$1 AND user_id=$2 FOR UPDATE",season,user))
        await using(var r=await q.ExecuteReaderAsync())
        {
            if(!await r.ReadAsync())throw new ApiFault(409,"시즌이 종료되지 않았거나 수령 조건을 충족하지 않았습니다.");
            reward=new(){Season=season,Tier=r.GetString(0),Coin=r.GetInt64(1),Gem=r.GetInt32(2),Emblem=r.GetString(3),Claimed=r.GetBoolean(4)};
        }
        if(!reward.Claimed)
        {
            await Exec(c,"UPDATE wallets SET coin=coin+$2,jem=jem+$3 WHERE user_id=$1",user,reward.Coin,reward.Gem);
            await Exec(c,"INSERT INTO wallet_ledger(user_id,request_key,coin_delta,jem_delta) VALUES($1,$2,$3,$4)",user,"GuildSeason:"+season,reward.Coin,reward.Gem);
            await Exec(c,"UPDATE guild_season_rewards SET claimed_at=$3 WHERE season_id=$1 AND user_id=$2",season,user,clock.GetUtcNow());
        }
        reward.Claimed=true;await tx.CommitAsync();return reward;
    }
}

public sealed class GuildLeagueMaintenance(GuildLeague service,ILogger<GuildLeagueMaintenance> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken token)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromSeconds(15));
        while(await timer.WaitForNextTickAsync(token))try{await service.FinalizeSeasons();}catch(Exception e)when(e is not OperationCanceledException){logger.LogWarning("Guild season finalization deferred ({Type})",e.GetType().Name);}
    }
}
