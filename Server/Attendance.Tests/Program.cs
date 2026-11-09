using System.Text.Json;
using MagicOnline.GameApi;
using MagicOnline.GameApi.Admin;
using Microsoft.Extensions.Configuration;
using Npgsql;
var cfg=new ConfigurationBuilder().AddUserSecrets<Database>().Build();
var cs=new NpgsqlConnectionStringBuilder(cfg.GetConnectionString("GameDb"));
if(cs.Host is not ("127.0.0.1" or "localhost"))throw new Exception("Local DB only");
await using var root=NpgsqlDataSource.Create(cs.ConnectionString);
await using(var q=root.CreateCommand("SELECT jsonb_array_length(rows) FROM charts WHERE name='Attendance'"))Console.WriteLine("Live attendance chart days: "+await q.ExecuteScalarAsync());
string schema="attendance_test_"+Guid.NewGuid().ToString("N");
await using(var q=root.CreateCommand($"CREATE SCHEMA {schema}"))await q.ExecuteNonQueryAsync();
try {
 cs.SearchPath=schema;
 await using var source=NpgsqlDataSource.Create(cs.ConnectionString);
 void Sql(string sql){using var q=source.CreateCommand(sql);q.ExecuteNonQuery();}
 long Count(string sql){using var q=source.CreateCommand(sql);return Convert.ToInt64(q.ExecuteScalar());}
 Sql(File.ReadAllText("Server/GameApi/Migrations/001_initial.sql"));
 Guid user=Guid.NewGuid(),legacy=Guid.NewGuid(),invalid=Guid.NewGuid();
 foreach(var u in new[]{user,legacy,invalid})Sql($"INSERT INTO users(id,login_id,normalized_login,password_hash,nickname) VALUES('{u}','{u}','{u}','test','test'); INSERT INTO wallets(user_id,coin) VALUES('{u}',100); INSERT INTO user_stats(user_id) VALUES('{u}');");
 Sql($"INSERT INTO attendance_terms VALUES('{legacy}','2026-11-05','2026-12-04'); INSERT INTO user_attendance VALUES('{legacy}','2026-11-05',1,1,10); INSERT INTO wallet_ledger(user_id,request_key,coin_delta,created_at) VALUES('{legacy}','Attendance:2026-11-05',10,'2026-11-05 04:04:00+00');");
 Sql(File.ReadAllText("Server/GameApi/Migrations/005_attendance_schedule.sql"));
 string chart=JsonSerializer.Serialize(Enumerable.Range(1,30).Select(d=>new{DayNum=d,RewardType=d%2==0?2:1,RewardVal=d*10}));
 using(var q=source.CreateCommand("INSERT INTO charts VALUES('Attendance',$1::jsonb)")){q.Parameters.AddWithValue(chart);q.ExecuteNonQuery();}
 int tests=0;void Check(bool ok,string name){if(!ok)throw new Exception(name);tests++;Console.WriteLine("PASS: "+name);}
 var db=new Database(source);var clock=new Clock();var service=new Attendance(db,clock);
 clock.Now=DateTimeOffset.Parse("2026-11-05T14:59:00Z");
 var responses=await Task.WhenAll(Enumerable.Range(0,12).Select(_=>service.CheckIn(user)));
 var first=(Attendance.Reply)responses[0];
 Check(first.Rows.Length==30&&first.Rows.Count(r=>r.Attenddance==1)==1&&first.Rows[0].DayNum==1,"concurrent initial login creates one 30-day schedule and one attendance");
 Check(first.Rows[0].GiveDt==new DateOnly(2026,11,5)&&first.Rows[29].GiveDt==new DateOnly(2026,12,4),"scheduled calendar dates include exactly 30 days");
 Check(first.Rows.All(r=>r.TakeDt==null)&&Count($"SELECT coin FROM wallets WHERE user_id='{user}'")==100&&Count($"SELECT count(*) FROM wallet_ledger WHERE user_id='{user}'")==0,"no reward payout or receipt before mail claim");
 clock.Now=DateTimeOffset.Parse("2026-11-05T15:00:00Z");
 var viewed=await service.Status(user);
 Check(viewed.TodayDay==2 && viewed.Rows.Count==30 && viewed.Rows.Count(r=>r.Attenddance==1)==1 && viewed.Rows[1].Attenddance==0,"opening the calendar after KST midnight reads the schedule without checking in");
 Check((await service.Status(invalid)).Rows.Count==0,"another account's schedule is not visible");
 var next=(Attendance.Reply)await service.CheckIn(user);
 Check(next.Rows[1].Attenddance==1&&next.Rows.Count(r=>r.Attenddance==1)==2,"KST midnight checks in next day");
 Sql("UPDATE charts SET rows=jsonb_set(rows,'{0,RewardVal}','999') WHERE name='Attendance'");
 clock.Now=DateTimeOffset.Parse("2026-11-08T03:00:00Z");next=(Attendance.Reply)await service.CheckIn(user);
 Check(next.Rows[2].Attenddance==0&&next.Rows[3].Attenddance==1&&next.Rows[0].RewardVal==10,"missed days stay absent and current term retains reward snapshot");
 Sql($"UPDATE user_attendance SET take_dt='2026-11-08 04:04:00+00' WHERE user_id='{user}' AND day_num=4");
 next=(Attendance.Reply)await service.CheckIn(user);Check(next.Rows[3].TakeDt!=null,"repeat login preserves claimed timestamp");
 clock.Now=DateTimeOffset.Parse("2026-12-04T14:59:00Z");next=(Attendance.Reply)await service.CheckIn(user);Check(next.Rows[29].Attenddance==1&&Count($"SELECT count(*) FROM attendance_terms WHERE user_id='{user}'")==1,"day 30 stays within current term");
 clock.Now=DateTimeOffset.Parse("2026-12-07T00:00:00Z");next=(Attendance.Reply)await service.CheckIn(user);
 Check(next.Rows[0].GiveDt==new DateOnly(2026,12,7)&&next.Rows[0].RewardVal==999&&Count($"SELECT count(*) FROM user_attendance WHERE user_id='{user}'")==60,"expired term retained; next login starts fresh 30 days with latest chart");
 clock.Now=DateTimeOffset.Parse("2026-11-05T05:00:00Z");var old=(Attendance.Reply)await service.CheckIn(legacy);
 Check(old.Rows.Length==30&&old.Rows[0].Attenddance==1&&old.Rows[0].TakeDt!=null&&old.Rows[0].RewardVal==10,"legacy paid row preserved while schedule is filled");
 var admin=new AdminReadService(db);var table=await admin.AttendanceHistory(legacy,1);
 Check(table.Rows[0][6]=="26.11.05 13:04","admin claim timestamp formatting in KST");
 var page1=await admin.AttendanceHistory(user,1);var page2=await admin.AttendanceHistory(user,2);
 Check(page1.Rows.Count==30&&page1.HasNext&&page2.Rows.Count==30&&!page2.HasNext,"admin retains and paginates both attendance terms");
 Sql("UPDATE charts SET rows='[]' WHERE name='Attendance'");bool failed=false;
 try{await service.CheckIn(invalid);}catch(ApiFault e){failed=e.Code()=="AttendanceRequires30Days";}
 Check(failed&&Count($"SELECT count(*) FROM attendance_terms WHERE user_id='{invalid}'")==0,"invalid chart creates no partial term");
 clock.Now=DateTimeOffset.Parse("2026-12-07T00:00:00Z");next=(Attendance.Reply)await service.CheckIn(user);Check(next.Rows.Length==30,"existing snapshot works even if chart is later incomplete");
 Console.WriteLine($"PASS: {tests} checks");
} finally {await using var q=root.CreateCommand($"DROP SCHEMA {schema} CASCADE");await q.ExecuteNonQueryAsync();}
sealed class Clock:TimeProvider {public DateTimeOffset Now; public override DateTimeOffset GetUtcNow()=>Now;}
static class ErrorExt {public static string Code(this ApiFault e)=>e.Message;}
