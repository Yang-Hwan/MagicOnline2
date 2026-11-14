using System.Text.Json;
using Assets.Scripts.Prack.BackSys.GameData;
using MagicOnline.GameApi;
using Microsoft.Extensions.Configuration;
using Npgsql;

var config=new ConfigurationBuilder().AddUserSecrets<Database>().Build();
var cs=new NpgsqlConnectionStringBuilder(config.GetConnectionString("GameDb"));
if(cs.Host is not ("localhost" or "127.0.0.1")) throw new Exception("Local test DB required");
await using var root=NpgsqlDataSource.Create(cs.ConnectionString);
string schema="player_info_"+Guid.NewGuid().ToString("N");
await using(var cmd=root.CreateCommand($"CREATE SCHEMA {schema}")) await cmd.ExecuteNonQueryAsync();
try
{
    cs.SearchPath=schema;
    await using var source=NpgsqlDataSource.Create(cs.ConnectionString);
    async Task Sql(string sql) { await using var cmd=source.CreateCommand(sql); await cmd.ExecuteNonQueryAsync(); }
    int checks=0;
    void Check(bool condition,string label) { if(!condition)throw new Exception(label); checks++; Console.WriteLine("PASS "+label); }
    await Sql("""
        CREATE TABLE users(id uuid PRIMARY KEY,nickname text,level integer,deleted_at timestamptz);
        CREATE TABLE user_stats(user_id uuid PRIMARY KEY,ball_4_win int,ball_4_lose int,ball_4_draw int,ball_3_win int,ball_3_lose int,ball_3_draw int,legacy_win int,legacy_lose int);
        CREATE TABLE matches(id text PRIMARY KEY,user_a uuid,user_b uuid);
        CREATE TABLE settlement_receipts(match_id text PRIMARY KEY,data jsonb,settled_at timestamptz);
        CREATE TABLE shop_products(id bigint PRIMARY KEY,kind text,name text,description text,thumbnail text);
        """);
    await Sql(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"Migrations","011_match_equipment.sql")));
    var a=Guid.NewGuid(); var b=Guid.NewGuid(); var outsider=Guid.NewGuid(); string match=Guid.NewGuid().ToString("N");
    await Sql($"""
        INSERT INTO users VALUES('{a}','Actual player',12,NULL),('{b}','Opponent',8,NULL);
        INSERT INTO user_stats VALUES('{a}',60,40,2,91,59,3,7,4),('{b}',0,0,0,0,0,0,0,0);
        INSERT INTO shop_products VALUES(7,'Cue','Actual cue','Catalog description','Gameplay/ShopCues/Inferno');
        """);
    var gear=new MatchEquipmentProfile {UserId=a.ToString(),CueId=7,CharacterId=8,Power=20,FollowThrough=-10,Tip=-5,ShotSeconds=-2};
    var snapshot=new MatchEquipmentSnapshot {MatchId=match,Players=new[]{gear,new MatchEquipmentProfile {UserId=b.ToString()}}};
    await using(var cmd=source.CreateCommand("INSERT INTO match_equipment_snapshots(match_id,user_a,user_b,data,friendly) VALUES($1,$2,$3,$4::jsonb,true)"))
    {
        cmd.Parameters.AddWithValue(match);cmd.Parameters.AddWithValue(a);cmd.Parameters.AddWithValue(b);cmd.Parameters.AddWithValue(JsonSerializer.Serialize(snapshot,Database.Json));
        await cmd.ExecuteNonQueryAsync();
    }
    var day=new DateTimeOffset(2026,11,14,0,0,0,TimeSpan.FromHours(9)).ToUniversalTime();
    var clock=new TestClock(day.AddHours(23));
    var service=new MatchPlayerInformation(new Database(source),new MatchEquipment(new Database(source)),clock);
    var blank=await service.Read(match,1,a);
    Check(blank.TotalPrize==0 && blank.TodayStreak==0 && blank.Total.WinRate==0,"empty history is zero, not synthetic values");
    Check(blank.CueName=="기본 큐대" && blank.Equipment.CueId==0,"unequipped cue fallback");
    async Task Receipt(string label,DateTimeOffset when,string outcome,long reward)
    {
        string id=label+Guid.NewGuid().ToString("N");
        await using var cmd=source.CreateCommand("WITH added AS (INSERT INTO matches VALUES($1,$2,$3) RETURNING id) INSERT INTO settlement_receipts SELECT id,$4::jsonb,$5 FROM added");
        cmd.Parameters.AddWithValue(id);cmd.Parameters.AddWithValue(a);cmd.Parameters.AddWithValue(b);
        cmd.Parameters.AddWithValue(JsonSerializer.Serialize(new{ResultA=outcome,ResultB=outcome=="Win"?"Lose":outcome=="Lose"?"Win":outcome,RewardA=reward,RewardB=outcome=="Lose"?1500:0}));
        cmd.Parameters.AddWithValue(when);await cmd.ExecuteNonQueryAsync();
    }
    await Receipt("old",day.AddYears(-1),"Win",1000);
    await Receipt("yesterday",day.AddTicks(-1),"Win",2000);
    Check((await service.Read(match,0,b)).TodayStreak==0,"Korean midnight resets streak");
    await Receipt("win",day,"Win",3000);
    Check((await service.Read(match,0,b)).TodayStreak==1,"today starts at Korean midnight");
    await Receipt("loss",day.AddMinutes(10),"Lose",300);
    Check((await service.Read(match,0,b)).TodayStreak==0,"loss clears streak; positive loser reward is not winning prize");
    await Receipt("win",day.AddMinutes(20),"Win",4000);
    await Receipt("win",day.AddMinutes(30),"Win",5000);
    await Receipt("cancel",day.AddMinutes(40),"Void",999999);
    var own=await service.Read(match,0,a); var other=await service.Read(match,0,b);
    Check(own.TodayStreak==2,"only the current daily consecutive wins count; void ignored");
    Check(own.TotalPrize==15000,"permanent receipts retain lifetime winning prizes beyond detail retention");
    Check(own.Nickname=="Actual player" && own.Level==12,"real profile fields");
    Check(own.Total.Wins==158 && own.Total.Losses==103 && own.Total.Draws==5,"total includes legacy and both disciplines");
    Check(own.FourBall.Wins==60 && own.ThreeBall.Wins==91,"discipline records remain separate");
    Check(Math.Abs(own.FourBall.WinRate-60)<.0001,"win rate excludes draws");
    Check(own.CueName=="Actual cue" && own.CueThumbnail=="Gameplay/ShopCues/Inferno" && own.Equipment.Power==20,"catalog name/image with frozen match attributes");
    Check(JsonSerializer.Serialize(own,Database.Json)==JsonSerializer.Serialize(other,Database.Json),"both participants receive the same selected player's information");
    await Receipt("draw",day.AddMinutes(50),"Draw",900000);
    own=await service.Read(match,0,a);
    Check(own.TodayStreak==0 && own.TotalPrize==15000,"draw breaks streak; refunds excluded from prize");
    async Task Rejected(Func<Task<MatchPlayerInfo>> operation,int status,string label)
    {
        try {await operation();throw new Exception("Unexpected authorization success");}
        catch(ApiFault e) {Check(e.Status==status,label);}
    }
    await Rejected(()=>service.Read(match,0,outsider),404,"outsider cannot query either participant");
    await Rejected(()=>service.Read(match,2,a),400,"invalid seat rejected");
    await Rejected(()=>service.Read(Guid.NewGuid().ToString("N"),0,a),404,"unknown match rejected");
    await Sql($"UPDATE users SET deleted_at=now() WHERE id='{b}'");
    await Rejected(()=>service.Read(match,1,a),404,"deleted player is unavailable");
    Console.WriteLine($"Player information: {checks} checks passed");
}
finally { await using var cmd=root.CreateCommand($"DROP SCHEMA {schema} CASCADE");await cmd.ExecuteNonQueryAsync(); }

sealed class TestClock(DateTimeOffset value):TimeProvider { public override DateTimeOffset GetUtcNow()=>value; }
