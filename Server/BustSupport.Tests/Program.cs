using MagicOnline.GameApi;
using Microsoft.Extensions.Configuration;
using Npgsql;
var config = new ConfigurationBuilder().AddUserSecrets<Database>().Build();
var cs = new NpgsqlConnectionStringBuilder(config.GetConnectionString("GameDb"));
if(cs.Host is not ("localhost" or "127.0.0.1")) throw new Exception("Local DB only");
await using var root = NpgsqlDataSource.Create(cs.ConnectionString);
string schema = "bust_test_" + Guid.NewGuid().ToString("N");
await using(var create=root.CreateCommand($"CREATE SCHEMA {schema}")) await create.ExecuteNonQueryAsync();
try {
 cs.SearchPath=schema;
 await using var source=NpgsqlDataSource.Create(cs.ConnectionString);
 async Task Sql(string sql) { await using var cmd=source.CreateCommand(sql); await cmd.ExecuteNonQueryAsync(); }
 await Sql("CREATE TABLE wallets(user_id uuid PRIMARY KEY,coin bigint); CREATE TABLE charts(name text PRIMARY KEY,rows jsonb); CREATE TABLE wallet_ledger(user_id uuid,request_key text,coin_delta bigint,jem_delta int,created_at timestamptz,UNIQUE(user_id,request_key));");
 var user=Guid.NewGuid(); var other=Guid.NewGuid();
 await Sql($"INSERT INTO wallets VALUES('{user}',0),('{other}',1000)");
 await Sql("""INSERT INTO charts VALUES ('CommonCode','[{"GrpCd":"Bust","ComCd":"BustHelpCoin","ComVal":100,"IsUse":"Y"},{"GrpCd":"Bust","ComCd":"Bust1DayHelpCnt","ComVal":2,"IsUse":"Y"}]'),('SkillMatch','[{"PrizeCoin":1000},{"PrizeCoin":5000}]')""");
 var clock=new Clock(); var service=new BustSupport(new Database(source),clock);
 int checks=0;
 void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS: "+label);}
 var concurrent=await Task.WhenAll(Enumerable.Range(0,10).Select(_=>service.Check(user)));
 Check(concurrent.Count(x=>x)==1,"concurrent requests pay only once");
 Check(!await service.Check(user),"repeated request cannot consume daily quota within 30 seconds");
 Check(!await service.Check(other),"equal minimum entry balance is not bankrupt");
 clock.Now=clock.Now.AddSeconds(30);
 var finalGrant=await service.CheckResult(user);
 Check(finalGrant.Granted && finalGrant.Remaining==0,"final daily grant reports zero remaining uses");
 clock.Now=clock.Now.AddSeconds(30);
 Check(!await service.Check(user),"daily maximum enforced");
 await using(var q=source.CreateCommand($"SELECT coin FROM wallets WHERE user_id='{user}'")) Check((long)(await q.ExecuteScalarAsync())! ==200,"wallet receives exact total");
 await using(var q=source.CreateCommand("SELECT count(*),sum(coin_delta) FROM wallet_ledger")) { await using var r=await q.ExecuteReaderAsync();await r.ReadAsync();Check(r.GetInt64(0)==2 && r.GetDecimal(1)==200,"ledger records exact successful grants"); }
 clock.Now=new DateTimeOffset(2026,10,28,15,0,0,TimeSpan.Zero);
 var nextDay=await service.CheckResult(user);
 Check(nextDay.Granted && nextDay.Remaining==1,"KST midnight grant reports one remaining use");
 await Sql("""UPDATE charts SET rows=jsonb_set(rows,'{0,ComVal}','250') WHERE name='CommonCode'""");
 clock.Now=clock.Now.AddSeconds(30);
 Check(await service.Check(user),"current common code reloaded each check");
 await using(var q=source.CreateCommand($"SELECT coin FROM wallets WHERE user_id='{user}'")) Check((long)(await q.ExecuteScalarAsync())! ==550,"updated support amount applied");
 await Sql("""UPDATE charts SET rows=jsonb_set(rows,'{0,IsUse}','"N"') WHERE name='CommonCode'""");
 clock.Now=clock.Now.AddDays(1);
 Check(!await service.Check(user),"disabled support is not paid");
 await Sql("""UPDATE charts SET rows='[]' WHERE name='CommonCode'""");
 Check(!await service.Check(user),"missing support settings are not paid");
 Console.WriteLine($"Passed {checks} checks; live wallets unchanged.");
} finally { await using var drop=root.CreateCommand($"DROP SCHEMA {schema} CASCADE");await drop.ExecuteNonQueryAsync(); }
sealed class Clock : TimeProvider { public DateTimeOffset Now = new(2026,10,28,14,58,0,TimeSpan.Zero); public override DateTimeOffset GetUtcNow()=>Now; }
