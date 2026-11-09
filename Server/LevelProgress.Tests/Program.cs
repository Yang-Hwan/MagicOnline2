using MagicOnline.GameApi;
using MagicOnline.MatchSettlement;
using Microsoft.Extensions.Configuration;
using Npgsql;
var config=new ConfigurationBuilder().AddUserSecrets<Database>().Build();
var cs=new NpgsqlConnectionStringBuilder(config.GetConnectionString("GameDb"));
if(cs.Host is not ("localhost" or "127.0.0.1"))throw new Exception("Local DB only");
await using var root=NpgsqlDataSource.Create(cs.ConnectionString);
await using(var q=root.CreateCommand("SELECT rows::text FROM charts WHERE name='CommonCode'"))
 Console.WriteLine("Live LvPlayMax: "+LevelProgression.ReadLimit(await q.ExecuteScalarAsync() as string));
string schema="level_test_"+Guid.NewGuid().ToString("N");
await using(var q=root.CreateCommand($"CREATE SCHEMA {schema}"))await q.ExecuteNonQueryAsync();
try {
 cs.SearchPath=schema;
 await using var source=NpgsqlDataSource.Create(cs.ConnectionString);
 void Sql(string sql){using var q=source.CreateCommand(sql);q.ExecuteNonQuery();}
 Sql(File.ReadAllText("Server/GameApi/Migrations/001_initial.sql"));
 Sql(File.ReadAllText("Server/GameApi/Migrations/006_registration_reward.sql"));
 Sql(File.ReadAllText("Server/GameApi/Migrations/008_user_post_reward.sql"));
 string a=Guid.NewGuid().ToString(),b=Guid.NewGuid().ToString();
 foreach(var memberId in new[]{a,b})Sql($"INSERT INTO users(id,login_id,normalized_login,password_hash,nickname) VALUES('{memberId}','{memberId}','{memberId}','test','test'); INSERT INTO wallets(user_id,coin) VALUES('{memberId}',100000); INSERT INTO user_stats(user_id) VALUES('{memberId}');");
 Sql("""INSERT INTO charts VALUES('CommonCode','[{"GrpCd":"Lv","ComCd":"LvPlayMax","ComVal":50,"IsUse":"Y"}]')""");
 var db=new Database(source);var store=new PostgresSettlementStore(db);var service=new SettlementService(store,new ServerClock());
 int checks=0;
 void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS: "+label);}
 int Play(string id){using var q=source.CreateCommand($"SELECT level_progress FROM users WHERE id='{id}'");return (int)q.ExecuteScalar()!;}
 int Level(string id){using var q=source.CreateCommand($"SELECT level FROM users WHERE id='{id}'");return (int)q.ExecuteScalar()!;}
 string Match(bool start=true,int ball=3){string id=Guid.NewGuid().ToString("N");service.Register(new(id,a,b,"A","B",ball,1,10,CoinPolicy.EntryPool,"{}"));if(start){service.Ready(id,a);service.Ready(id,b);}return id;}
 void Finish(string id,bool draw=false){var r=draw?new ResultReport(2,2,-1):new ResultReport(3,1,0);service.ConfirmResult(id,a,r);service.ConfirmResult(id,b,r);}
 Sql("UPDATE users SET level=7,level_progress=48");
 var id=Match();Check(Play(a)==48,"readiness does not increment");Finish(id);Check(Play(a)==49&&Play(b)==49,"win and loss increment once");
 Parallel.For(0,12,_=>service.Surrender(id,a));Check(Play(a)==49&&Play(b)==49,"parallel settlement retries do not increment");
 id=Match(ball:4);Finish(id,true);Check(Level(a)==8&&Level(b)==8&&Play(a)==0&&Play(b)==0,"50th game draw levels up and resets");
 id=Match();service.Surrender(id,a);Check(Play(a)==1&&Play(b)==1,"surrender loser and winner both count");
 id=Match(false);service.Surrender(id,a);Check(Play(a)==1&&Play(b)==1,"cancel before start excluded");
 Sql("UPDATE users SET level_progress=60");id=Match();Finish(id);Check(Level(a)==9&&Play(a)==0,"lowered limit uses >= and resets to zero");
 Sql("""UPDATE charts SET rows='[{"GrpCd":"Lv","ComCd":"LvPlayMax","ComVal":0,"IsUse":"Y"}]'""");id=Match();Finish(id);Check(Play(a)==0,"invalid zero limit leaves progression unchanged without blocking settlement");
 Sql("""UPDATE charts SET rows='[{"GrpCd":"Lv","ComCd":"LvPlayMax","ComVal":50,"IsUse":"Y"}]'""");
 id=Match();
 Sql("CREATE FUNCTION reject_receipt() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'injected commit failure'; END $$; CREATE TRIGGER fail_receipt BEFORE INSERT ON settlement_receipts FOR EACH ROW EXECUTE FUNCTION reject_receipt();");
 service.ConfirmResult(id,a,new(3,1,0));bool failed=false;try{service.ConfirmResult(id,b,new(3,1,0));}catch(PostgresException){failed=true;}
 Check(failed&&Play(a)==0&&Play(b)==0,"failed receipt rolls back level progress with settlement");
 Sql("DROP TRIGGER fail_receipt ON settlement_receipts");service.ConfirmResult(id,b,new(3,1,0));Check(Play(a)==1&&Play(b)==1,"retry after rollback grants exactly one play");
 var member=await new MagicOnline.GameApi.Admin.AdminReadService(db).Member(Guid.Parse(a));
 Check(member.Profile.Columns.Contains("레벨플레이 수 (LvPlay)"),"admin basic information includes play count");
 Check(LevelProgression.ReadLimit("[]")==null,"missing limit rejected");
 Check(LevelProgression.ReadLimit("""[{"ComCd":"LvPlayMax","GrpCd":"Lv","IsUse":"N","ComVal":50}]""")==null,"disabled limit rejected");
 var adminRead=new MagicOnline.GameApi.Admin.AdminReadService(db);
 Check((await adminRead.BustHistory(Guid.Parse(a),1)).Rows.Count==0,"empty bust history");
 for(int n=1;n<=31;n++)Sql($"INSERT INTO wallet_ledger(user_id,request_key,coin_delta,created_at) VALUES('{a}','Bust:2026-11-05:{n}',100,'2026-11-05 00:00:00+00')");
 Sql($"INSERT INTO wallet_ledger(user_id,request_key,coin_delta) VALUES('{b}','Bust:2026-11-05:1',999)");
 var first=await adminRead.BustHistory(Guid.Parse(a),1);var second=await adminRead.BustHistory(Guid.Parse(a),2);
 Check(first.Rows.Count==30&&first.HasNext&&second.Rows.Count==1&&!second.HasNext,"bust pagination 30 plus 1");
 Check(first.Rows[0][2]=="31"&&second.Rows[0][2]=="1"&&first.Rows.All(r=>r[3]=="100"),"bust records exclude other users and non-support ledger with stable newest order");
 Check(first.Rows[0][4]=="2026-11-05 09:00:00","bust timestamps display KST");
 Console.WriteLine($"PASS: {checks} checks");
} finally {await using var q=root.CreateCommand($"DROP SCHEMA {schema} CASCADE");await q.ExecuteNonQueryAsync();}
