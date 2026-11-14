using System.Reflection;
using Assets.Scripts.Prack.BackSys.GameData;
using MagicOnline.GameApi;
using Microsoft.Extensions.Configuration;
using Npgsql;

var config=new ConfigurationBuilder().AddUserSecrets<Database>().Build();
var cs=new NpgsqlConnectionStringBuilder(config.GetConnectionString("GameDb"));
if(cs.Host is not ("localhost" or "127.0.0.1"))throw new Exception("Local test DB required");
await using var root=NpgsqlDataSource.Create(cs.ConnectionString);
string schema="guild_test_"+Guid.NewGuid().ToString("N");
await using(var q=root.CreateCommand($"CREATE SCHEMA {schema}"))await q.ExecuteNonQueryAsync();
try
{
    cs.SearchPath=schema;await using var source=NpgsqlDataSource.Create(cs.ConnectionString);
    var db=new Database(source);await db.Migrate();var g=new Guilds(db);var friends=new Friends(db,new GameData(db));
    int checks=0;
    void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);checks++;}
    async Task Sql(string sql){await using var q=source.CreateCommand(sql);await q.ExecuteNonQueryAsync();}
    async Task Fail(Func<Task> action,int status,string label){try{await action();throw new Exception("Unexpected success: "+label);}catch(ApiFault e){Check(e.Status==status,label);}}
    async Task<Guid> User(){var id=Guid.NewGuid();await Sql($"INSERT INTO users(id,login_id,normalized_login,password_hash,nickname) VALUES('{id}','{id}','{id}','test','tester-{id.ToString()[..6]}'); INSERT INTO wallets(user_id,coin) VALUES('{id}',1000000); INSERT INTO user_stats(user_id) VALUES('{id}');");return id;}
    GuildCreate Input(string name,string tag,string mode="Approval")=>new(){Name=name,Tag=tag,Description="테스트 길드",JoinMode=mode,RequestId=Guid.NewGuid().ToString()};
    var a=await User();var b=await User();var c=await User();var d=await User();var e=await User();
    var create=Input("승부사","WIN");var guild=Guid.Parse((await g.Create(a,create)).GuildId);
    Check((await g.Create(a,create)).GuildId==guild.ToString(),"creation retry is idempotent");
    Check((await g.Mine(a)).Guild.MyRole=="Leader","creator is leader");
    await Fail(()=>g.Create(a,Input("중복가입","DUPE")),409,"one guild per user");
    await Fail(()=>g.Create(b,Input("잘못된태그","한글")),400,"tag validation");
    Check((await g.Search(b,"승부","Approval",0)).Rows.Single().Id==guild.ToString(),"search and join-mode filter");
    await g.Join(b,guild);await g.Join(b,guild);
    Check((await g.Mine(a)).Applications.Length==1 && (await g.Mine(b)).Guild==null,"approval applications are deduplicated without premature membership");
    await Fail(()=>g.Members(b,guild),403,"applicant cannot read roster");
    await g.AnswerApplication(a,guild,b,true);await g.AnswerApplication(a,guild,b,true);
    Check((await g.Members(b,guild)).Rows.Length==2,"approval creates member once");
    await Fail(()=>g.Invite(b,guild,c),403,"member cannot invite");
    await g.Invite(a,guild,c);await g.Invite(a,guild,c);
    var invitation=(await g.Mine(c)).Invitations.Single();
    Check((await friends.Poll(c,true)).GuildInvite?.Id==invitation.Id,"global inbox exposes guild invitation");
    await Fail(()=>g.AnswerInvitation(d,Guid.Parse(invitation.Id),true),404,"invitation recipient protected");
    await g.AnswerInvitation(c,Guid.Parse(invitation.Id),true);
    Check((await g.Mine(c)).Guild.Id==guild.ToString(),"invitation acceptance joins actual guild");
    await g.Invite(a,guild,d);var expired=(await g.Mine(d)).Invitations.Single();
    await Sql($"UPDATE guild_invitations SET expires_at=now()-interval '1 second' WHERE id='{expired.Id}'");
    await Fail(()=>g.AnswerInvitation(d,Guid.Parse(expired.Id),true),409,"expired invitation cannot join");
    await g.Invite(a,guild,d);await g.AnswerInvitation(d,Guid.Parse((await g.Mine(d)).Invitations.Single().Id),false);
    Check((await g.Mine(d)).Guild==null && (await g.Mine(d)).Invitations.Length==0,"declining does not join");
    await g.Role(a,guild,b,"Officer");await g.Invite(b,guild,d);
    await g.Role(a,guild,b,"Member");
    Check((await g.Mine(d)).Invitations.Length==0,"demoted inviter's pending invitation is hidden");
    await Fail(()=>g.AnswerInvitation(d,Guid.Parse(expired.Id),true),403,"demoted inviter cannot authorize acceptance");
    var edit=new GuildEdit{Description="새 소개",Notice="첫 공지\n둘째 줄",JoinMode="Open",Revision=1};
    await Fail(()=>g.Edit(b,guild,edit),403,"notice editing requires manager");
    await g.Edit(a,guild,edit);await Fail(()=>g.Edit(a,guild,edit),409,"notice revision prevents lost updates");
    Check((await g.Mine(b)).Guild.Notice==edit.Notice,"multiline notice persisted");
    await g.Join(d,guild);Check((await g.Mine(d)).Guild.Id==guild.ToString(),"open mode joins immediately");
    await friends.Poll(a,true);await friends.Poll(b,true);await friends.Poll(c,false);
    var roster=await g.Members(a,guild);
    Check(roster.Rows.Single(x=>x.Id==b.ToString()).Status=="online" && roster.Rows.Single(x=>x.Id==c.ToString()).Status=="busy","online and busy reflect presence");
    await Sql($"UPDATE friend_presence SET seen_at=now()-interval '41 seconds' WHERE user_id='{d}'");
    Check((await g.Members(a,guild)).Rows.Single(x=>x.Id==d.ToString()).Status=="offline","stale presence becomes offline");
    var message=new GuildMessageInput{RequestId=Guid.NewGuid().ToString(),Text="안녕하세요 <b>문자</b>"};
    await g.Send(a,guild,message);await g.Send(a,guild,message);
    var chat=(await g.Chat(b,guild,0)).Rows;
    Check(chat.Length==1 && chat[0].Text==message.Text,"chat retry has one real message");
    Check((await g.Chat(b,guild,chat[0].Id)).Rows.Length==0,"chat cursor avoids duplicates");
    await Fail(()=>g.Send(a,guild,new(){RequestId=message.RequestId,Text="변경"}),409,"message UUID cannot change content");
    await Fail(()=>g.Send(a,guild,new(){RequestId=Guid.NewGuid().ToString(),Text="연속"}),429,"server throttles chat");
    await Fail(()=>g.Chat(e,guild,0),403,"outsider cannot read chat");
    await Fail(()=>g.Send(e,guild,new(){RequestId=Guid.NewGuid().ToString(),Text="침입"}),403,"outsider cannot send chat");
    var hall=(await new GameData(db).Chart("SkillMatch")).EnumerateArray().First().GetProperty("HallIdx").GetInt32();
    var ticket=await friends.Challenge(a,new(b,hall));await friends.Decide(b,ticket.Id,true);
    Check((await friends.Poll(a,true)).Invite?.Id==ticket.Id && (await friends.Poll(b,true)).Invite?.Id==ticket.Id,"guild-only challenge acceptance delivers same Photon room ticket to both players");
    await g.Leave(b,guild);Check((await friends.Poll(a,true)).Invite==null,"leaving closes accepted guild challenge");
    await Fail(()=>g.Chat(b,guild,0),403,"leaving revokes guild data access");
    await friends.Poll(e,true);await Fail(()=>friends.Challenge(a,new(e,hall)),403,"unrelated user cannot challenge");
    await Fail(()=>friends.Challenge(a,new(c,hall)),409,"busy guild member cannot be challenged");
    await Fail(()=>g.Leave(a,guild),409,"leader must transfer before leaving others");
    await g.Role(a,guild,c,"Leader");await g.Leave(a,guild);
    Check((await g.Mine(c)).Guild.MyRole=="Leader","leader transfer is atomic");
    await g.Kick(c,guild,d);await Fail(()=>g.Members(d,guild),403,"kick revokes roster access");
    await g.Leave(c,guild);
    Check((await g.Search(e,"승부사","",0)).Rows.Length==0,"empty guild is dissolved");
    var open=Guid.Parse((await g.Create(a,Input("동시가입","RACE","Open"))).GuildId);
    var users=new List<Guid>();for(int n=0;n<32;n++)users.Add(await User());
    var outcomes=await Task.WhenAll(users.Select(async u=>{try{await g.Join(u,open);return true;}catch(ApiFault ex)when(ex.Status==409){return false;}}));
    Check(outcomes.Count(x=>x)==29 && (await g.Members(a,open)).Rows.Length==30,"concurrent joins enforce capacity of 30");
    var second=Guid.Parse((await g.Create(e,Input("중복경합","ONE","Open"))).GuildId);var racer=await User();
    var joined=Guid.Parse((await g.Members(a,open)).Rows.First(x=>x.Id!=a.ToString()).Id);
    await g.Kick(a,open,joined);
    var attempts=await Task.WhenAll(new[]{open,second}.Select(async id=>{try{await g.Join(racer,id);return true;}catch(ApiFault ex)when(ex.Status==409){return false;}}));
    Check(attempts.Count(x=>x)==1,"simultaneous joins to different guilds yield one membership");
    await using(var connection=await source.OpenConnectionAsync())
    {
        await using var tx=await connection.BeginTransactionAsync();
        await (Task)typeof(Guilds).GetMethod("RemoveDeletedMembers",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{connection,new[]{a}})!;
        await tx.CommitAsync();
    }
    var survivor=users.Where((x,i)=>x!=joined && outcomes[i]).First();
    Check((await g.Members(survivor,open)).Rows.Count(x=>x.Role=="Leader")==1,"admin deletion repairs leadership");
    Console.WriteLine($"Guilds: {checks} checks passed");
}
finally{await using var q=root.CreateCommand($"DROP SCHEMA {schema} CASCADE");await q.ExecuteNonQueryAsync();}
