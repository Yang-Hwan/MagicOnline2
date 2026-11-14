using Assets.Scripts.Prack.BackSys.GameData;
using Npgsql;
namespace MagicOnline.GameApi;
public sealed partial class Guilds
{
    const string Summary="""
        SELECT g.id,g.name,g.tag,g.description,g.notice,g.join_mode,g.revision,
            (SELECT count(*) FROM guild_members x WHERE x.guild_id=g.id),
            (SELECT count(*) FROM guild_members x JOIN friend_presence p ON p.user_id=x.user_id JOIN users u ON u.id=x.user_id WHERE x.guild_id=g.id AND NOT u.disabled AND p.seen_at>=now()-interval '40 seconds'),
            COALESCE((SELECT u.nickname FROM guild_members x JOIN users u ON u.id=x.user_id WHERE x.guild_id=g.id AND x.role='Leader'),''),
            COALESCE((SELECT role FROM guild_members WHERE guild_id=g.id AND user_id=$1),''),
            EXISTS(SELECT FROM guild_applications WHERE guild_id=g.id AND user_id=$1 AND state='pending')
        FROM guilds g
        """;
    static GuildInfo Info(NpgsqlDataReader r)=>new(){Id=r.GetGuid(0).ToString(),Name=r.GetString(1),Tag=r.GetString(2),Description=r.GetString(3),Notice=r.GetString(4),JoinMode=r.GetString(5),Revision=r.GetInt32(6),MemberCount=checked((int)r.GetInt64(7)),OnlineCount=checked((int)r.GetInt64(8)),LeaderName=r.GetString(9),MyRole=r.GetString(10),ApplicationPending=r.GetBoolean(11)};
    public async Task<GuildState> Mine(Guid user)
    {
        await using var c=await db.Source.OpenConnectionAsync();var state=new GuildState();
        await using(var q=Database.Command(c,Summary+" WHERE g.deleted_at IS NULL AND EXISTS(SELECT FROM guild_members WHERE user_id=$1 AND guild_id=g.id)",user))
        await using(var r=await q.ExecuteReaderAsync())if(await r.ReadAsync())state.Guild=Info(r);
        state.Invitations=await Invitations(c,user);
        if(state.Guild?.MyRole is "Leader" or "Officer")
        {
            await using var q=Database.Command(c,"SELECT a.user_id,u.nickname,u.level FROM guild_applications a JOIN users u ON u.id=a.user_id WHERE a.guild_id=$1 AND a.state='pending' AND NOT u.disabled AND NOT EXISTS(SELECT FROM guild_members m WHERE m.user_id=a.user_id) ORDER BY a.updated_at LIMIT 50",Guid.Parse(state.Guild.Id));
            await using var r=await q.ExecuteReaderAsync();var rows=new List<GuildApplication>();
            while(await r.ReadAsync())rows.Add(new(){UserId=r.GetGuid(0).ToString(),Name=r.GetString(1),Level=r.GetInt32(2)});
            state.Applications=rows.ToArray();
        }
        return state;
    }
    internal static async Task<GuildInvitation[]> Invitations(NpgsqlConnection c,Guid user)
    {
        await using var q=Database.Command(c,"""
            SELECT i.id,i.guild_id,g.name,g.tag,u.nickname FROM guild_invitations i JOIN guilds g ON g.id=i.guild_id JOIN users u ON u.id=i.inviter
            JOIN guild_members m ON m.user_id=i.inviter AND m.guild_id=i.guild_id AND m.role IN ('Leader','Officer')
            WHERE i.recipient=$1 AND i.state='pending' AND i.expires_at>now() AND g.deleted_at IS NULL AND NOT u.disabled
                AND NOT EXISTS(SELECT FROM guild_members WHERE user_id=$1) ORDER BY i.expires_at DESC LIMIT 20
            """,user);
        await using var r=await q.ExecuteReaderAsync();var rows=new List<GuildInvitation>();
        while(await r.ReadAsync())rows.Add(new(){Id=r.GetGuid(0).ToString(),GuildId=r.GetGuid(1).ToString(),GuildName=r.GetString(2),Tag=r.GetString(3),InviterName=r.GetString(4)});
        return rows.ToArray();
    }
    public async Task<GuildSearch> Search(Guid user,string query,string mode,int page)
    {
        query=Clean(query,0,24,"검색어");if(mode!="")Mode(mode);if(page is <0 or >10000)throw new ApiFault(400,"페이지를 확인해 주세요.");
        await using var q=db.Source.CreateCommand(Summary+" WHERE g.deleted_at IS NULL AND ($2='' OR strpos(lower(g.name||' '||g.tag),lower($2))>0) AND ($3='' OR g.join_mode=$3) ORDER BY g.name,g.id LIMIT 21 OFFSET $4");
        q.Parameters.AddWithValue(user);q.Parameters.AddWithValue(query);q.Parameters.AddWithValue(mode);q.Parameters.AddWithValue(page*20);
        await using var r=await q.ExecuteReaderAsync();var rows=new List<GuildInfo>();while(await r.ReadAsync())rows.Add(Info(r));
        return new(){HasMore=rows.Count>20,Rows=rows.Take(20).ToArray()};
    }
    public async Task<GuildMembers> Members(Guid user,Guid guild)
    {
        await using var c=await db.Source.OpenConnectionAsync();await Member(c,user,guild);
        await using var q=Database.Command(c,$"""
            SELECT u.id,u.nickname,u.level,u.avatar_idx,m.role,CASE WHEN u.disabled THEN 'offline' ELSE {Friends.StatusSql} END,
                COALESCE(NULLIF((SELECT asset_key FROM shop_character_emotions WHERE product_id=e.product_id AND emotion='기본'),''),cp.thumbnail,'')
            FROM guild_members m JOIN users u ON u.id=m.user_id
            LEFT JOIN friend_presence p ON p.user_id=u.id
            LEFT JOIN shop_user_equipment e ON e.user_id=u.id AND e.slot='Character' LEFT JOIN shop_products cp ON cp.id=e.product_id
            WHERE m.guild_id=$1 AND u.deleted_at IS NULL
            ORDER BY CASE m.role WHEN 'Leader' THEN 0 WHEN 'Officer' THEN 1 ELSE 2 END,u.nickname,u.id
            """,guild);
        await using var r=await q.ExecuteReaderAsync();var rows=new List<GuildMember>();
        while(await r.ReadAsync())rows.Add(new(){Id=r.GetGuid(0).ToString(),Name=r.GetString(1),Level=r.GetInt32(2),Avatar=r.GetInt32(3),Role=r.GetString(4),Status=r.GetString(5),Portrait=r.GetString(6)});
        return new(){Rows=rows.ToArray()};
    }
    public async Task<GuildChat> Chat(Guid user,Guid guild,long after)
    {
        if(after<0)throw new ApiFault(400,"메시지 위치를 확인해 주세요.");
        await using var c=await db.Source.OpenConnectionAsync();await Member(c,user,guild);
        string order=after==0?"DESC":"ASC";
        await using var q=Database.Command(c,$"""
            SELECT x.id,x.author,CASE WHEN u.deleted_at IS NOT NULL OR m.user_id IS NULL THEN '탈퇴한 길드원' ELSE u.nickname END,x.message,
                (extract(epoch FROM x.created_at)*1000)::bigint,COALESCE(m.role,'Member')
            FROM (SELECT * FROM guild_messages WHERE guild_id=$1 AND id>$2 ORDER BY id {order} LIMIT 50) x
            JOIN users u ON u.id=x.author LEFT JOIN guild_members m ON m.user_id=x.author AND m.guild_id=$1 ORDER BY x.id
            """,guild,after);
        await using var r=await q.ExecuteReaderAsync();var rows=new List<GuildMessage>();
        while(await r.ReadAsync())rows.Add(new(){Id=r.GetInt64(0),UserId=r.GetGuid(1).ToString(),Name=r.GetString(2),Text=r.GetString(3),SentAt=r.GetInt64(4),Role=r.GetString(5)});
        return new(){Rows=rows.ToArray()};
    }
    public async Task<GuildReply> Send(Guid user,Guid guild,GuildMessageInput input)
    {
        string text=Clean(input.Text,1,250,"메시지");
        if(!Guid.TryParse(input.RequestId,out var request)||request==Guid.Empty)throw new ApiFault(400,"메시지 요청 ID를 확인해 주세요.");
        await using var c=await db.Source.OpenConnectionAsync();await using var tx=await c.BeginTransactionAsync();await Gate(c,user);await LockGuild(c,guild);await Member(c,user,guild);
        await using(var q=Database.Command(c,"SELECT message FROM guild_messages WHERE guild_id=$1 AND author=$2 AND request_id=$3",guild,user,request))
            if(await q.ExecuteScalarAsync() is string old)
            {
                if(old!=text)throw new ApiFault(409,"같은 요청 ID로 다른 메시지를 보낼 수 없습니다.");
                return new(){Message="메시지를 보냈습니다."};
            }
        await using(var q=Database.Command(c,"UPDATE guild_members SET last_chat_at=now() WHERE user_id=$1 AND (last_chat_at IS NULL OR last_chat_at<=now()-interval '2 seconds')",user))
            if(await q.ExecuteNonQueryAsync()!=1)throw new ApiFault(429,"잠시 후 다시 보내 주세요.");
        await Execute(c,"INSERT INTO guild_messages(guild_id,author,request_id,nickname_snapshot,message) SELECT $1,id,$2,nickname,$3 FROM users WHERE id=$4",guild,request,text,user);
        await tx.CommitAsync();return new(){Message="메시지를 보냈습니다."};
    }
}
