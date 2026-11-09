using MagicOnline.GameApi;
using MagicOnline.GameApi.Admin;
using MagicOnline.MatchSettlement;
using Microsoft.Extensions.Configuration;
using Npgsql;

var cfg=new ConfigurationBuilder().AddUserSecrets<Database>().Build();
var cs=new NpgsqlConnectionStringBuilder(cfg.GetConnectionString("GameDb"));
if(cs.Host is not ("localhost" or "127.0.0.1")) throw new Exception("Local DB only");
await using var root=NpgsqlDataSource.Create(cs.ConnectionString);
string schema="mail_test_"+Guid.NewGuid().ToString("N");
await using(var create=root.CreateCommand($"CREATE SCHEMA {schema}"))await create.ExecuteNonQueryAsync();
try
{
 cs.SearchPath=schema;
 await using var source=NpgsqlDataSource.Create(cs.ConnectionString);
 void Sql(string sql){using var cmd=source.CreateCommand(sql);cmd.ExecuteNonQuery();}
 long Number(string sql){using var cmd=source.CreateCommand(sql);return Convert.ToInt64(cmd.ExecuteScalar());}
 foreach(var file in new[]{"001_initial.sql","005_attendance_schedule.sql","006_registration_reward.sql","007_post_reward_chart.sql","008_user_post_reward.sql"})
     Sql(File.ReadAllText(Path.Combine("Server","GameApi","Migrations",file)));
 Sql("INSERT INTO charts SELECT * FROM public.charts");
 Sql("""UPDATE charts SET rows=(SELECT jsonb_agg(CASE WHEN e->>'ComCd'='LvPlayMax' THEN jsonb_set(e,'{ComVal}','2') ELSE e END) FROM jsonb_array_elements(rows) e) WHERE name='CommonCode'""");
 var db=new Database(source);var accounts=new Accounts(db);
 string a=(await accounts.Register(new("mail_test_a","test_password"))).UserId;
 string b=(await accounts.Register(new("mail_test_b","test_password"))).UserId;
 void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS "+message);}
 long Mail(string kind)=>Number($"SELECT count(*) FROM user_post_reward WHERE user_id='{a}' AND type_name='{kind}'");
 var game=new GameData(db);
 Check(Mail("가입")==1 && (await game.Profile(Guid.Parse(a))).Coin==100000,"signup grants immediate coin and separate unclaimed mail");
 Check(Number($"SELECT reward_val FROM user_post_reward WHERE user_id='{a}' AND claimed_at IS NULL AND expires_at-sent_at=interval '48 hours'")==10000,"signup mail snapshots amount and expiry");
 await accounts.Login(new("mail_test_a","test_password"));Check(Mail("가입")==1,"login does not reissue");
 Sql($"UPDATE wallets SET coin=0 WHERE user_id='{a}'");
 var bust=new BustSupport(db,TimeProvider.System);
 var results=await Task.WhenAll(Enumerable.Range(0,5).Select(_=>bust.CheckResult(Guid.Parse(a))));
 var granted=results.Single(x=>x.Granted);
 Check(Mail("파산")==1 && Number($"SELECT count(*) FROM user_post_reward WHERE user_id='{a}' AND message='파산으로 긴급지급 남음({granted.Remaining})'")==1,"concurrent support sends just one mail with remaining count");
 Sql($"UPDATE wallets SET coin=100000 WHERE user_id='{a}'");
 var service=new SettlementService(new PostgresSettlementStore(db),new ServerClock());
 string Start(){string id=Guid.NewGuid().ToString("N");service.Register(new(id,a,b,"A","B",3,1,10,CoinPolicy.EntryPool,"{}"));service.Ready(id,a);service.Ready(id,b);return id;}
 void Finish(string id, ResultReport report){service.ConfirmResult(id,a,report);service.ConfirmResult(id,b,report);}
 var first=Start();Finish(first,new(2,1,0));
 var plain=service.ConfirmResult(first,a,new(2,1,0));
 Check(plain.LevelUpA==null && plain.LevelUpB==null,"non-leveling receipt has no awards");
 var second=Start();Finish(second,new(2,1,0));
 var levels=service.ConfirmResult(second,b,new(2,1,0));
 Check(levels.LevelUpA==new LevelUpAward(2,"Jem",13) && levels.LevelUpB==levels.LevelUpA,"winner and loser retain the actual immediate award on receipt retry");
 var profile=await game.Profile(Guid.Parse(a));
 Check(profile.LvVel==2 && profile.Jem==13 && Mail("레벨")==1,"second level grants 13 Jem and a separate level mail");
 Check(Number($"SELECT reward_val FROM user_post_reward WHERE user_id='{a}' AND message='2레벨업 축하보상' AND currency_name='Coin'")==5000,"level mail uses chart band");
 string third=Start();service.Surrender(third,b);
 Parallel.For(0,8,_=>service.Surrender(third,b));
 Check(Mail("연승")==1 && Number($"SELECT reward_val FROM user_post_reward WHERE user_id='{a}' AND message='3연승 보상'")==15,"third consecutive win (surrender), retries issue only one streak mail");
 Sql($"UPDATE users SET level_progress=0 WHERE id='{b}'");
 var drawn=Start();Finish(drawn,new(2,2,-1));
 var drawLevels=service.ConfirmResult(drawn,b,new(2,2,-1));
 Check(drawLevels.LevelUpA==new LevelUpAward(3,"Jem",18) && drawLevels.LevelUpB==null,"draw levels only the eligible player");
 Check(Number($"SELECT win_streak FROM user_stats WHERE user_id='{a}'")==0,"draw resets streak");
 var list=await new AdminReadService(db).MemberMail(Guid.Parse(a),1);
 Check(list.Rows.All(r=>r[8]=="미수령") && list.Rows.Count>=4,"admin member tab shows unclaimed mail");
 Sql(File.ReadAllText("Server/GameApi/Migrations/009_friends.sql"));
 await FriendChecks.Run(db,game,Guid.Parse(a),Guid.Parse(b));
 string rollback=Start();service.ConfirmResult(rollback,a,new(2,1,0));
 Sql("CREATE FUNCTION reject_receipt() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'injected'; END $$; CREATE TRIGGER fail_receipt BEFORE INSERT ON settlement_receipts FOR EACH ROW EXECUTE FUNCTION reject_receipt()");
 long count=Number("SELECT count(*) FROM user_post_reward");
 try{service.ConfirmResult(rollback,b,new(2,1,0));throw new Exception("Expected rollback");}catch(PostgresException){}
 Check(Number("SELECT count(*) FROM user_post_reward")==count && Number($"SELECT win_streak FROM user_stats WHERE user_id='{a}'")==0,"failed settlement rolls back rewards and win streak");
 var mailbox=new Mailbox(db);
 var uid=Guid.Parse(a);
 await new Attendance(db,TimeProvider.System).CheckIn(uid);
 await new Attendance(db,TimeProvider.System).CheckIn(uid);
 Check(Mail("출석")==1,"same-day attendance issues only one mail");
 // Make enough claimable rows to require scrolling; one other row is expired.
 Sql($"INSERT INTO user_post_reward(user_id,event_key,reward_id,type_code,type_name,message,sent_at,expires_at,currency_code,currency_name,reward_val) SELECT '{a}','test:'||n,11,6,'점검','점검 보상',now(),now()+interval '1 day',1,'Coin',100 FROM generate_series(1,10) n");
 Sql($"UPDATE user_post_reward SET sent_at=now()-interval '2 days',expires_at=now()-interval '1 day' WHERE user_id='{a}' AND event_key='test:1'");
 var inbox=await mailbox.List(uid);
 Check(inbox.Rows.Count>8 && inbox.Rows.Count==Number($"SELECT count(*) FROM user_post_reward WHERE user_id='{a}' AND claimed_at IS NULL AND expires_at>now()"),"badge includes only eligible rows, beyond eight");
 var one=inbox.Rows.First(r=>r.Currency=="Coin");
 var before=await game.Profile(uid);
 await mailbox.Claim(Guid.Parse(b),one.PostId);
 await Task.WhenAll(Enumerable.Range(0,3).Select(_=>mailbox.Claim(uid,one.PostId)));
 var afterOne=await game.Profile(uid);
 Check(afterOne.Coin-before.Coin==one.Amount && (await mailbox.List(uid)).Rows.Count==inbox.Rows.Count-1,"single claim paid once and owner scoped");
 var remaining=await mailbox.List(uid);
 await Task.WhenAll(mailbox.Claim(uid,null),mailbox.Claim(uid,remaining.Rows[0].PostId),mailbox.Claim(uid,null));
 var paid=await game.Profile(uid);
 Check(paid.Coin-before.Coin==inbox.Rows.Where(r=>r.Currency=="Coin").Sum(r=>r.Amount) &&
       paid.Jem-before.Jem==inbox.Rows.Where(r=>r.Currency=="Jem").Sum(r=>r.Amount) &&
       (await mailbox.List(uid)).Rows.Count==0,"claim all with concurrent single claim pays exact total");
 Check(Number($"SELECT count(*) FROM user_attendance WHERE user_id='{a}' AND take_dt IS NOT NULL")==1,"attendance receipt recorded on actual claim");
 Check(Number($"SELECT count(*) FROM user_post_reward WHERE user_id='{a}' AND event_key='test:1' AND claimed_at IS NULL")==1,"expired mail not collected");

}
finally
{
 await using var drop=root.CreateCommand($"DROP SCHEMA {schema} CASCADE");await drop.ExecuteNonQueryAsync();
}
