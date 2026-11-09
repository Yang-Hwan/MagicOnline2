using System.Security.Claims;
using Npgsql;
namespace MagicOnline.GameApi;

public sealed class Friends(Database db, GameData data)
{
    public sealed record ChallengeInput(Guid Other,int Hall);
    public sealed record Pulse(bool Available);
    public sealed record Choice(bool Accept);
    public sealed record Player(Guid Id,string Name,int Level,int Avatar,string Status);
    public sealed record SearchPlayer(Guid Id,string Name,int Level,int Avatar,string Relation);
    public sealed record SearchList(List<SearchPlayer> Rows);
    public sealed record Invite(Guid Id,Guid Other,string Name,int Hall,string State,bool Incoming);
    public sealed record FriendList(IEnumerable<Player> Rows);
    public sealed record Inbox(Guid? From,string? Name,Invite? Invite);
    public sealed record Ticket(Guid Id);
    static readonly string StatusSql = "CASE WHEN p.seen_at IS NULL OR p.seen_at < now()-interval '40 seconds' THEN 'offline' WHEN NOT p.available OR EXISTS(SELECT 1 FROM match_players m WHERE m.user_id=u.id AND m.active) THEN 'busy' ELSE 'online' END";
    const string Pair = "least(sender,recipient)=least($1::uuid,$2::uuid) AND greatest(sender,recipient)=greatest($1::uuid,$2::uuid)";

    public static void Map(WebApplication app)
    {
        var api=app.MapGroup("/api/me/friends").RequireAuthorization();
        Guid Me(ClaimsPrincipal u)=>Guid.Parse(u.FindFirstValue(ClaimTypes.NameIdentifier)!);
        api.MapGet("",(ClaimsPrincipal u,Friends f)=>f.List(Me(u)));
        api.MapGet("/search",(string q,ClaimsPrincipal u,Friends f)=>f.Search(Me(u),q));
        api.MapDelete("/{other:guid}",(Guid other,ClaimsPrincipal u,Friends f)=>f.Remove(Me(u),other));
        api.MapGet("/{other:guid}",(Guid other,ClaimsPrincipal u,Friends f)=>f.Relation(Me(u),other));
        api.MapPost("/pulse",(ClaimsPrincipal u,Pulse body,Friends f)=>f.Poll(Me(u),body.Available));
        api.MapPost("/{other:guid}/request",(Guid other,ClaimsPrincipal u,Friends f)=>f.Request(Me(u),other));
        api.MapPost("/{other:guid}/answer",(Guid other,Choice body,ClaimsPrincipal u,Friends f)=>f.Answer(Me(u),other,body.Accept));
        api.MapPost("/challenge",(ChallengeInput body,ClaimsPrincipal u,Friends f)=>f.Challenge(Me(u),body));
        api.MapPost("/challenge/{id:guid}",(Guid id,Choice body,ClaimsPrincipal u,Friends f)=>f.Decide(Me(u),id,body.Accept));
    }

    public async Task<FriendList> List(Guid user)
    {
        await using var c=await db.Source.OpenConnectionAsync();
        using var q=Database.Command(c,$"SELECT u.id,u.nickname,u.level,u.avatar_idx,{StatusSql} FROM friend_links f JOIN users u ON u.id=CASE WHEN f.sender=$1 THEN f.recipient ELSE f.sender END LEFT JOIN friend_presence p ON p.user_id=u.id WHERE (f.sender=$1 OR f.recipient=$1) AND f.accepted ORDER BY u.nickname,u.id",user);
        using var r=await q.ExecuteReaderAsync();var rows=new List<Player>();
        while(await r.ReadAsync()) rows.Add(new(r.GetGuid(0),r.GetString(1),r.GetInt32(2),r.GetInt32(3),r.GetString(4)));
        return new(rows.OrderBy(x=>x.Status=="online"?0:x.Status=="busy"?1:2));
    }

    public async Task<SearchList> Search(Guid user,string query)
    {
        query=query.Trim();
        if(query.Length is 0 or >24)throw new ApiFault(400,"닉네임을 1~24자로 입력해 주세요.");
        await using var c=await db.Source.OpenConnectionAsync();
        using var q=Database.Command(c,"""
            SELECT u.id,u.nickname,u.level,u.avatar_idx,
                   CASE WHEN f.accepted THEN '친구' WHEN f.sender=$1 THEN '요청 중'
                        WHEN f.recipient=$1 THEN '받은 요청' ELSE '' END
            FROM users u LEFT JOIN friend_links f
              ON least(f.sender,f.recipient)=least($1::uuid,u.id)
             AND greatest(f.sender,f.recipient)=greatest($1::uuid,u.id)
            WHERE u.id<>$1 AND NOT u.disabled AND strpos(lower(u.nickname),lower($2))>0
            ORDER BY (lower(u.nickname)=lower($2)) DESC,u.nickname,u.id LIMIT 30
            """,user,query);
        using var reader=await q.ExecuteReaderAsync();
        var rows=new List<SearchPlayer>();
        while(await reader.ReadAsync())rows.Add(new(reader.GetGuid(0),reader.GetString(1),reader.GetInt32(2),reader.GetInt32(3),reader.GetString(4)));
        return new(rows);
    }

    public async Task Remove(Guid user,Guid other)
    {
        await using var c=await db.Source.OpenConnectionAsync();
        await using var tx=await c.BeginTransactionAsync();
        using var remove=Database.Command(c,"DELETE FROM friend_links WHERE accepted AND "+Pair,user,other);
        if(await remove.ExecuteNonQueryAsync()>0)
        {
            using var cancel=Database.Command(c,"UPDATE friend_challenges SET state='closed' WHERE state='pending' AND "+Pair,user,other);
            await cancel.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
    }

    public async Task<object> Relation(Guid user,Guid other)
    {
        await using var c=await db.Source.OpenConnectionAsync();
        using var q=Database.Command(c,"SELECT CASE WHEN accepted THEN '친구' WHEN sender=$1 THEN '요청 중' ELSE '받은 요청' END FROM friend_links WHERE "+Pair,user,other);
        return new { Message=await q.ExecuteScalarAsync() as string ?? "" };
    }
    public async Task<object> Request(Guid user,Guid other)
    {
        if(other==user)throw new ApiFault(400,"자신에게 요청할 수 없습니다.");
        await using var c=await db.Source.OpenConnectionAsync();
        using var q=Database.Command(c,"""
            INSERT INTO friend_links(sender,recipient)
            SELECT $1::uuid,id FROM users WHERE id=$2 AND NOT disabled
            ON CONFLICT DO NOTHING
            """,user,other);
        await q.ExecuteNonQueryAsync();
        using var read=Database.Command(c,"SELECT CASE WHEN accepted THEN '친구' WHEN sender=$1 THEN '요청 중' ELSE '받은 요청' END FROM friend_links WHERE "+Pair,user,other);
        return new {Message=await read.ExecuteScalarAsync() ?? throw new ApiFault(404,"친구 요청을 보낼 수 없는 회원입니다.")};
    }

    public async Task Answer(Guid user,Guid other,bool accept)
    {
        await using var c=await db.Source.OpenConnectionAsync();
        using var q=Database.Command(c,(accept?"UPDATE friend_links SET accepted=true":"DELETE FROM friend_links")+" WHERE recipient=$1 AND sender=$2 AND NOT accepted",user,other);
        await q.ExecuteNonQueryAsync();
    }

    public async Task<Inbox> Poll(Guid user,bool available)
    {
        await using var c=await db.Source.OpenConnectionAsync();
        using(var q=Database.Command(c,"""
            INSERT INTO friend_presence VALUES($1,$2,now()) ON CONFLICT(user_id) DO UPDATE
            SET available=excluded.available,seen_at=excluded.seen_at
            WHERE friend_presence.available<>excluded.available OR friend_presence.seen_at<now()-interval '15 seconds'
            """,user,available))
            await q.ExecuteNonQueryAsync();
        Guid? from=null;string? name=null;
        using(var q=Database.Command(c,"SELECT u.id,u.nickname FROM friend_links f JOIN users u ON u.id=f.sender WHERE f.recipient=$1 AND NOT f.accepted ORDER BY u.id LIMIT 1",user))
        using(var r=await q.ExecuteReaderAsync())if(await r.ReadAsync()){from=r.GetGuid(0);name=r.GetString(1);}
        using var query=Database.Command(c,"SELECT ch.id,u.id,u.nickname,ch.hall_id,ch.state,ch.recipient=$1 FROM friend_challenges ch JOIN users u ON u.id=CASE WHEN ch.sender=$1 THEN ch.recipient ELSE ch.sender END WHERE (ch.sender=$1 OR ch.recipient=$1) AND ch.expires_at>now() AND ch.state <> 'closed' ORDER BY ch.expires_at DESC LIMIT 1",user);
        using var row=await query.ExecuteReaderAsync();
        Invite? invite=await row.ReadAsync()?new(row.GetGuid(0),row.GetGuid(1),row.GetString(2),row.GetInt32(3),row.GetString(4),row.GetBoolean(5)):null;
        return new(from,name,invite);
    }

    static async Task CheckAvailable(NpgsqlConnection c,Guid user,Guid other)
    {
        using var q=Database.Command(c,$"SELECT u.id,{StatusSql} FROM users u LEFT JOIN friend_presence p ON p.user_id=u.id WHERE u.id IN ($1,$2) ORDER BY u.id FOR UPDATE OF u",user,other);
        using var r=await q.ExecuteReaderAsync();int count=0;
        while(await r.ReadAsync())
        {
            count++;var status=r.GetString(1);
            if(status=="offline")throw new ApiFault(409,"온라인 상태가 아니어서 도전을 요청할 수 없습니다.");
            if(status=="busy")throw new ApiFault(409,"게임 중이어서 도전을 요청할 수 없습니다.");
        }
        if(count!=2)throw new ApiFault(404,"회원을 찾을 수 없습니다.");
    }

    public async Task<Ticket> Challenge(Guid user,ChallengeInput input)
    {
        var halls=await data.Chart("SkillMatch");
        if(!halls.EnumerateArray().Any(h=>h.GetProperty("HallIdx").GetInt32()==input.Hall))throw new ApiFault(400,"경기장을 확인해 주세요.");
        await using var c=await db.Source.OpenConnectionAsync();await using var tx=await c.BeginTransactionAsync();
        await CheckAvailable(c,user,input.Other);
        using(var expired=Database.Command(c,"DELETE FROM friend_challenges WHERE (sender=$1 OR recipient=$1) AND expires_at<=now()",user))
            await expired.ExecuteNonQueryAsync();
        using(var q=Database.Command(c,"SELECT 1 FROM friend_links WHERE accepted AND "+Pair+" FOR SHARE",user,input.Other))
            if(await q.ExecuteScalarAsync()==null)throw new ApiFault(403,"친구에게만 도전할 수 있습니다.");
        using(var q=Database.Command(c,"SELECT 1 FROM friend_challenges WHERE expires_at>now() AND state <> 'closed' AND (sender IN ($1,$2) OR recipient IN ($1,$2))",user,input.Other))
            if(await q.ExecuteScalarAsync()!=null)throw new ApiFault(409,"이미 진행 중인 도전 요청이 있습니다.");
        var id=Guid.NewGuid();
        using(var q=Database.Command(c,"INSERT INTO friend_challenges VALUES($1,$2,$3,$4,'pending',now()+interval '45 seconds')",id,user,input.Other,input.Hall))
            await q.ExecuteNonQueryAsync();
        await tx.CommitAsync();return new(id);
    }

    public async Task Decide(Guid user,Guid id,bool accept)
    {
        await using var c=await db.Source.OpenConnectionAsync();await using var tx=await c.BeginTransactionAsync();
        if(accept)
        {
            using var q=Database.Command(c,"SELECT sender FROM friend_challenges WHERE id=$1 AND recipient=$2",id,user);
            if(await q.ExecuteScalarAsync() is not Guid sender)throw new ApiFault(404,"도전 요청이 없습니다.");
            await CheckAvailable(c,user,sender);
        }
        string where=accept?"recipient=$2":"(recipient=$2 OR sender=$2)";
        using var update=Database.Command(c,$"UPDATE friend_challenges SET state=$3,expires_at=now()+interval '45 seconds' WHERE id=$1 AND {where} AND state='pending' AND expires_at>now()",id,user,accept?"accepted":"closed");
        if(await update.ExecuteNonQueryAsync()!=1)throw new ApiFault(409,"종료된 도전 요청입니다.");
        await tx.CommitAsync();
    }
}
