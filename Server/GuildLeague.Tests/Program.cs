using Assets.Scripts.Prack.BackSys.GameData;
using Assets.Scripts.Prack.BackSys.ChartData;
using MagicOnline.GameApi;
using MagicOnline.MatchSettlement;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Text.Json;

var config=new ConfigurationBuilder().AddUserSecrets<Database>().Build();var cs=new NpgsqlConnectionStringBuilder(config.GetConnectionString("GameDb"));
if(cs.Host is not ("localhost" or "127.0.0.1"))throw new Exception("Local DB only");
await using var root=NpgsqlDataSource.Create(cs.ConnectionString);string schema="league_"+Guid.NewGuid().ToString("N");
await using(var q=root.CreateCommand($"CREATE SCHEMA {schema}"))await q.ExecuteNonQueryAsync();
try
{
    cs.SearchPath=schema;await using var source=NpgsqlDataSource.Create(cs.ConnectionString);var db=new Database(source);await db.Migrate();
    var clock=new Clock();var data=new GameData(db);var guilds=new Guilds(db);var league=new GuildLeague(db,data,clock);var settlement=new SettlementService(new PostgresSettlementStore(db),clock);var matches=new Matches(db,data,settlement);
    int checks=0;void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
    async Task Sql(string sql){await using var q=source.CreateCommand(sql);await q.ExecuteNonQueryAsync();}
    async Task<long> Scalar(string sql){await using var q=source.CreateCommand(sql);return Convert.ToInt64(await q.ExecuteScalarAsync());}
    async Task Reject(Func<Task> action,int status,string label){try{await action();throw new Exception("Expected rejection: "+label);}catch(ApiFault e){Check(e.Status==status,label);}}
    async Task<Guid> User(){var id=Guid.NewGuid();await Sql($"INSERT INTO users(id,login_id,normalized_login,password_hash,nickname) VALUES('{id}','{id}','{id}','test','선수'); INSERT INTO wallets(user_id,coin) VALUES('{id}',100000); INSERT INTO user_stats(user_id) VALUES('{id}');");return id;}
    async Task<Guid> Create(Guid u,string name,string tag)=>Guid.Parse((await guilds.Create(u,new(){Name=name,Tag=tag,Description="",JoinMode="Open",RequestId=Guid.NewGuid().ToString()})).GuildId);
    var a=await User();var b=await User();var same=await User();var outsider=await User();var ga=await Create(a,"에이스 길드","ACE");var gb=await Create(b,"쿠션 길드","CUE");await guilds.Join(same,ga);
    var hall=JsonSerializer.Deserialize<SkillMatchData>((await data.Chart("SkillMatch")).EnumerateArray().First(x=>x.GetProperty("MatchTotMin").GetInt32()<=30).GetRawText(),Database.Json)!;
    var initial=await league.Read(a);
    Check(initial.EndsAt-initial.StartsAt==28L*86400000,"season is exactly four weeks");Check(initial.Week is >=1 and <=4 && initial.Own.Points==1000,"initial rating and week are server values");
    await Reject(()=>league.Queue(outsider,hall.HallIdx),403,"guildless player cannot queue");
    Check((await league.Queue(a,hall.HallIdx)).State=="Searching","first player enters queue");
    Check((await league.Queue(same,hall.HallIdx)).State=="Searching","same guild members never match");await league.Cancel(same);
    var ticket=await league.Queue(b,hall.HallIdx);Check(ticket.State=="Matched" && ticket.OpponentUserId==a.ToString(),"different guilds match");
    Check((await league.Queue(a,hall.HallIdx)).MatchId==ticket.MatchId,"both players receive same match ticket");
    var rules=MatchRulesContract.FromHall(hall,MatchRulesContract.Pavilion);rules.CoinPolicy=1;rules.EntryCoin=rules.MaxCoin=0;
    await Reject(()=>matches.Register(outsider,new(ticket.MatchId,a.ToString(),hall.HallIdx,rules)),403,"outsider cannot register official ticket");
    await Reject(()=>matches.Register(a,new(ticket.MatchId,b.ToString(),hall.HallIdx,MatchRulesContract.FromHall(hall,MatchRulesContract.Pavilion))),409,"official ticket rejects paid or altered rules");
    await matches.Register(a,new(ticket.MatchId,b.ToString(),hall.HallIdx,rules));await matches.Register(a,new(ticket.MatchId,b.ToString(),hall.HallIdx,rules));
    matches.Ready(ticket.MatchId,a,new(rules));matches.Ready(ticket.MatchId,b,new(rules));
    Check(settlement.ConfirmResult(ticket.MatchId,a.ToString(),new(hall.TargetHit,0,0))==null && (await league.Read(a)).MyGames==0,"one client report cannot award guild points");
    var receipt=settlement.ConfirmResult(ticket.MatchId,b.ToString(),new(hall.TargetHit,0,0));settlement.ConfirmResult(ticket.MatchId,b.ToString(),new(hall.TargetHit,0,0));
    Check(receipt.ResultA=="Win" && receipt.EntryA==0 && receipt.RewardA==0,"official settlement is counted but entry and match prize are free");
    var won=await league.Read(a);var lost=await league.Read(b);
    Check(won.Own.Points==1024 && lost.Own.Points==988,"server settlement applies +24 / -12 once");
    Check(won.MyGames==1 && won.MyPoints==24 && won.MyRank==1,"personal contribution follows authenticated result");
    Check(won.Rankings.First().GuildId==ga.ToString() && won.Recent.Single().Result=="Win","ranking and recent results reflect settlement");
    Check(await Scalar("SELECT count(*) FROM guild_wars WHERE settled_at IS NOT NULL")==1,"receipt retries do not duplicate league settlement");
    await league.Queue(a,hall.HallIdx);Check((await league.Queue(b,hall.HallIdx)).State=="Searching","same opponent cannot score twice on same day");await league.Cancel(a);await league.Cancel(b);
    for(int i=0;i<4;i++)
    {
        var other=await User();await guilds.Join(other,gb);await Play(a,other);
    }
    Check((await league.Read(a)).TodayGames==5,"daily quota counts official games");await Reject(()=>league.Queue(a,hall.HallIdx),409,"sixth daily official game rejected");
    await Sql("UPDATE guild_wars SET created_at=created_at-interval '1 day'");
    for(int i=0;i<5;i++){var other=await User();await guilds.Join(other,gb);await Play(a,other);}
    Check((await league.Read(a)).MyGames==10,"ten counted games reach reward qualification");
    await Reject(()=>league.Claim(a,initial.Season),409,"current season reward cannot be claimed early");
    await Sql("UPDATE guild_wars SET created_at=created_at-interval '1 day'");
    var cancelUser=await User();await guilds.Join(cancelUser,gb);await league.Queue(a,hall.HallIdx);var cancel=await league.Queue(cancelUser,hall.HallIdx);await league.Cancel(a);
    await Reject(()=>matches.Register(a,new(cancel.MatchId,cancelUser.ToString(),hall.HallIdx,rules)),409,"cancelled ticket cannot register");
    var voidUser=await User();await guilds.Join(voidUser,gb);await league.Queue(a,hall.HallIdx);var voidTicket=await league.Queue(voidUser,hall.HallIdx);
    await matches.Register(a,new(voidTicket.MatchId,voidUser.ToString(),hall.HallIdx,rules));settlement.Surrender(voidTicket.MatchId,a.ToString());
    Check((await league.Read(a)).MyGames==10,"unstarted void match earns no points or contribution");
    var disputedUser=await User();await guilds.Join(disputedUser,gb);await league.Queue(a,hall.HallIdx);var disputed=await league.Queue(disputedUser,hall.HallIdx);
    await matches.Register(a,new(disputed.MatchId,disputedUser.ToString(),hall.HallIdx,rules));matches.Ready(disputed.MatchId,a,new(rules));matches.Ready(disputed.MatchId,disputedUser,new(rules));
    settlement.ConfirmResult(disputed.MatchId,a.ToString(),new(10,0,0));settlement.ConfirmResult(disputed.MatchId,disputedUser.ToString(),new(0,10,1));
    Check((await league.Read(a)).MyGames==10 && await Scalar($"SELECT count(*) FROM guild_wars WHERE match_id='{disputed.MatchId}' AND result_a='Void' AND settled_at IS NOT NULL")==1,"conflicting reports void league scoring without blocking season closure");
    long before=await Scalar($"SELECT coin FROM wallets WHERE user_id='{a}'");
    clock.Now=DateTimeOffset.FromUnixTimeMilliseconds(initial.EndsAt).AddSeconds(1);
    await league.FinalizeSeasons();var after=await league.Read(a);
    Check(after.Season==initial.Season+1 && after.Own.Points==1000 && after.MyGames==0,"new season resets ratings and contribution");
    Check(after.Rewards.Length==1 && !after.Rewards[0].Claimed,"ended qualified season exposes a claimable reward");
    var claimed=await Task.WhenAll(league.Claim(a,initial.Season),league.Claim(a,initial.Season));
    Check(claimed.All(x=>x.Claimed) && await Scalar($"SELECT coin FROM wallets WHERE user_id='{a}'")==before+15000,"concurrent reward claims credit wallet once");
    Check(await Scalar($"SELECT count(*) FROM wallet_ledger WHERE user_id='{a}' AND request_key='GuildSeason:{initial.Season}'")==1,"season reward has one permanent wallet ledger entry");
    Check((await league.Read(a)).Rewards[0].Emblem.StartsWith("GuildSeason:"),"claimed season emblem ownership remains visible");
    await Reject(()=>league.Claim(b,initial.Season),409,"underqualified participant cannot claim another player's reward");
    Check(await Scalar($"SELECT count(*) FROM guild_season_scores WHERE season_id={initial.Season} AND final_rank IS NOT NULL")==2,"final season ranks are frozen");
    await guilds.Role(a,ga,same,"Leader");await guilds.Leave(a,ga);
    Check((await league.Rewards(a)).Rewards.Single().Claimed,"finalized rewards remain accessible after leaving the guild");
    await league.Claim(a,initial.Season);
    Check(await Scalar($"SELECT coin FROM wallets WHERE user_id='{a}'")==before+15000,"leaving and retrying cannot collect the season twice");
    Console.WriteLine($"Guild league: {checks} checks passed");
    async Task Play(Guid winner,Guid loser)
    {
        await league.Queue(winner,hall.HallIdx);var t=await league.Queue(loser,hall.HallIdx);if(t.State!="Matched")throw new Exception("Expected match");
        await matches.Register(winner,new(t.MatchId,loser.ToString(),hall.HallIdx,rules));matches.Ready(t.MatchId,winner,new(rules));matches.Ready(t.MatchId,loser,new(rules));settlement.Surrender(t.MatchId,loser.ToString());
    }
}
finally{await using var q=root.CreateCommand($"DROP SCHEMA {schema} CASCADE");await q.ExecuteNonQueryAsync();}

sealed class Clock:TimeProvider,IServerClock{public DateTimeOffset Now=DateTimeOffset.UtcNow;public override DateTimeOffset GetUtcNow()=>Now;public long UtcMilliseconds=>Now.ToUnixTimeMilliseconds();}
