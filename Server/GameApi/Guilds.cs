using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using Assets.Scripts.Prack.BackSys.GameData;
using Npgsql;

namespace MagicOnline.GameApi;
public sealed partial class Guilds(Database db)
{
    public const int Capacity=30;
    public static void Map(WebApplication app)
    {
        var api=app.MapGroup("/api/guilds").RequireAuthorization();
        Guid Me(ClaimsPrincipal u)=>Guid.Parse(u.FindFirstValue(ClaimTypes.NameIdentifier)!);
        api.MapGet("/me",(ClaimsPrincipal u,Guilds g)=>g.Mine(Me(u)));
        api.MapGet("",(string? q,string? mode,int? page,ClaimsPrincipal u,Guilds g)=>g.Search(Me(u),q??"",mode??"",page??0));
        api.MapPost("",(GuildCreate body,ClaimsPrincipal u,Guilds g)=>g.Create(Me(u),body));
        api.MapGet("/{id:guid}/members",(Guid id,ClaimsPrincipal u,Guilds g)=>g.Members(Me(u),id));
        api.MapPut("/{id:guid}",(Guid id,GuildEdit body,ClaimsPrincipal u,Guilds g)=>g.Edit(Me(u),id,body));
        api.MapPost("/{id:guid}/join",(Guid id,ClaimsPrincipal u,Guilds g)=>g.Join(Me(u),id));
        api.MapDelete("/{id:guid}/applications/me",(Guid id,ClaimsPrincipal u,Guilds g)=>g.CancelApplication(Me(u),id));
        api.MapPost("/{id:guid}/applications/{target:guid}",(Guid id,Guid target,GuildChoice body,ClaimsPrincipal u,Guilds g)=>g.AnswerApplication(Me(u),id,target,body.Accept));
        api.MapPost("/{id:guid}/invitations",(Guid id,GuildInviteInput body,ClaimsPrincipal u,Guilds g)=>g.Invite(Me(u),id,ParseUser(body.UserId)));
        api.MapPost("/invitations/{invite:guid}",(Guid invite,GuildChoice body,ClaimsPrincipal u,Guilds g)=>g.AnswerInvitation(Me(u),invite,body.Accept));
        api.MapPost("/{id:guid}/leave",(Guid id,ClaimsPrincipal u,Guilds g)=>g.Leave(Me(u),id));
        api.MapDelete("/{id:guid}/members/{target:guid}",(Guid id,Guid target,ClaimsPrincipal u,Guilds g)=>g.Kick(Me(u),id,target));
        api.MapPut("/{id:guid}/members/{target:guid}/role",(Guid id,Guid target,GuildRoleInput body,ClaimsPrincipal u,Guilds g)=>g.Role(Me(u),id,target,body.Role));
        api.MapGet("/{id:guid}/chat",(Guid id,long? after,ClaimsPrincipal u,Guilds g)=>g.Chat(Me(u),id,after??0));
        api.MapPost("/{id:guid}/chat",(Guid id,GuildMessageInput body,ClaimsPrincipal u,Guilds g)=>g.Send(Me(u),id,body));
    }
    static Guid ParseUser(string? id)=>Guid.TryParse(id,out var value)?value:throw new ApiFault(400,"회원을 확인해 주세요.");
    static string Clean(string? value,int min,int max,string label,bool multiline=false)
    {
        string s=(value??"").Normalize(NormalizationForm.FormC).Trim();
        if(multiline)s=s.Replace("\r\n","\n");
        if(s.Length<min || s.Length>max || s.Any(c=>char.IsControl(c) && !(multiline && c=='\n')))throw new ApiFault(400,$"{label}: {min}~{max}자로 입력해 주세요.");
        return s;
    }
    static void Mode(string? mode) { if(mode is not ("Open" or "Approval"))throw new ApiFault(400,"가입 방식을 선택해 주세요."); }
    static async Task Gate(NpgsqlConnection c,params Guid[] ids)
    {
        var users=ids.Distinct().Order().ToArray();
        await using var q=Database.Command(c,"SELECT id FROM users WHERE id=ANY($1) AND NOT disabled AND deleted_at IS NULL ORDER BY id FOR UPDATE",users);
        await using var r=await q.ExecuteReaderAsync();int count=0;while(await r.ReadAsync())count++;
        if(count!=users.Length)throw new ApiFault(404,"이용 가능한 회원을 찾을 수 없습니다.");
    }
    static async Task LockGuild(NpgsqlConnection c,Guid id)
    {
        await using var q=Database.Command(c,"SELECT id FROM guilds WHERE id=$1 AND deleted_at IS NULL FOR UPDATE",id);
        if(await q.ExecuteScalarAsync()==null)throw new ApiFault(404,"길드를 찾을 수 없습니다.");
    }
    static async Task<string> Member(NpgsqlConnection c,Guid user,Guid guild,bool manage=false)
    {
        await using var q=Database.Command(c,"SELECT m.role FROM guild_members m JOIN guilds g ON g.id=m.guild_id WHERE m.user_id=$1 AND m.guild_id=$2 AND g.deleted_at IS NULL",user,guild);
        var role=await q.ExecuteScalarAsync() as string;
        if(role==null || manage && role=="Member")throw new ApiFault(403,manage?"길드장 또는 부길드장만 사용할 수 있습니다.":"길드원만 사용할 수 있습니다.");
        return role;
    }
    static async Task Execute(NpgsqlConnection c,string sql,params object[] values) { await using var q=Database.Command(c,sql,values);await q.ExecuteNonQueryAsync(); }
    static async Task Audit(NpgsqlConnection c,Guid guild,Guid actor,string action,Guid? target=null)=>await Execute(c,"INSERT INTO guild_audit(guild_id,actor,action,target) VALUES($1,$2,$3,$4)",guild,actor,action,(object?)target??DBNull.Value);
    static async Task AddMember(NpgsqlConnection c,Guid guild,Guid user)
    {
        await using(var q=Database.Command(c,"SELECT guild_id FROM guild_members WHERE user_id=$1",user))
        {
            if(await q.ExecuteScalarAsync() is Guid existing) { if(existing==guild)return;throw new ApiFault(409,"이미 다른 길드에 가입되어 있습니다."); }
        }
        await using(var q=Database.Command(c,"SELECT count(*) FROM guild_members WHERE guild_id=$1",guild))
            if((long)(await q.ExecuteScalarAsync())!>=Capacity)throw new ApiFault(409,"길드 정원이 마감되었습니다.");
        await Execute(c,"INSERT INTO guild_members(user_id,guild_id,role) VALUES($1,$2,'Member')",user,guild);
        await Execute(c,"UPDATE guild_applications SET state=CASE WHEN guild_id=$2 THEN 'accepted' ELSE 'cancelled' END,updated_at=now() WHERE user_id=$1 AND state='pending'",user,guild);
        await Execute(c,"UPDATE guild_invitations SET state='closed' WHERE recipient=$1 AND state='pending'",user);
    }
    public async Task<GuildReply> Create(Guid user,GuildCreate input)
    {
        string name=Clean(input.Name,2,20,"길드명"),tag=(input.Tag??"").Trim().ToUpperInvariant(),description=Clean(input.Description,0,100,"소개");Mode(input.JoinMode);
        if(!Regex.IsMatch(tag,"^[A-Z0-9]{2,6}$") || !Guid.TryParse(input.RequestId,out var request) || request==Guid.Empty)throw new ApiFault(400,"태그는 영문/숫자 2~6자, 생성 요청 ID는 유효해야 합니다.");
        await using var c=await db.Source.OpenConnectionAsync();await using var tx=await c.BeginTransactionAsync();await Gate(c,user);
        await using(var q=Database.Command(c,"SELECT guild_id FROM guild_creation_requests WHERE user_id=$1 AND request_id=$2",user,request))
            if(await q.ExecuteScalarAsync() is Guid prior)return new(){GuildId=prior.ToString(),Joined=true,Message="생성 요청이 처리되었습니다."};
        await using(var q=Database.Command(c,"SELECT 1 FROM guild_members WHERE user_id=$1",user))
            if(await q.ExecuteScalarAsync()!=null)throw new ApiFault(409,"이미 길드에 가입되어 있습니다.");
        Guid id=Guid.NewGuid();
        await Execute(c,"INSERT INTO guilds(id,name,tag,description,join_mode) VALUES($1,$2,$3,$4,$5)",id,name,tag,description,input.JoinMode);
        await Execute(c,"INSERT INTO guild_members(user_id,guild_id,role) VALUES($1,$2,'Leader')",user,id);
        await Execute(c,"INSERT INTO guild_creation_requests VALUES($1,$2,$3)",user,request,id);
        await Execute(c,"UPDATE guild_applications SET state='cancelled',updated_at=now() WHERE user_id=$1 AND state='pending'",user);
        await Execute(c,"UPDATE guild_invitations SET state='closed' WHERE recipient=$1 AND state='pending'",user);
        await Audit(c,id,user,"Created");await tx.CommitAsync();return new(){GuildId=id.ToString(),Joined=true,Message="길드를 만들었습니다."};
    }
}
