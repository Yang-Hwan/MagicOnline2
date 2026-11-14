using Assets.Scripts.Prack.BackSys.GameData;
using Npgsql;
namespace MagicOnline.GameApi;
public sealed partial class Guilds
{
    public async Task<GuildReply> Join(Guid user,Guid guild)
    {
        await using var c=await db.Source.OpenConnectionAsync();await using var tx=await c.BeginTransactionAsync();await Gate(c,user);await LockGuild(c,guild);
        await using(var q=Database.Command(c,"SELECT guild_id FROM guild_members WHERE user_id=$1",user))
            if(await q.ExecuteScalarAsync() is Guid current) { if(current==guild)return new(){Joined=true,GuildId=guild.ToString(),Message="가입된 길드입니다."};throw new ApiFault(409,"한 번에 하나의 길드에 가입할 수 있습니다."); }
        string mode;await using(var q=Database.Command(c,"SELECT join_mode FROM guilds WHERE id=$1",guild))mode=(string)(await q.ExecuteScalarAsync())!;
        if(mode=="Open") {await AddMember(c,guild,user);await Audit(c,guild,user,"Joined");}
        else
        {
            await using(var q=Database.Command(c,"SELECT count(*) FROM guild_members WHERE guild_id=$1",guild))if((long)(await q.ExecuteScalarAsync())!>=Capacity)throw new ApiFault(409,"길드 정원이 마감되었습니다.");
            await using(var q=Database.Command(c,"SELECT 1 FROM guild_applications WHERE guild_id=$1 AND user_id=$2 AND state='pending'",guild,user))
                if(await q.ExecuteScalarAsync()!=null)return new(){Message="가입 신청을 기다리고 있습니다."};
            await using(var q=Database.Command(c,"SELECT count(*) FROM guild_applications WHERE user_id=$1 AND state='pending'",user))
                if((long)(await q.ExecuteScalarAsync())!>=10)throw new ApiFault(409,"진행 중인 가입 신청은 최대 10개입니다.");
            await Execute(c,"INSERT INTO guild_applications(guild_id,user_id,state) VALUES($1,$2,'pending') ON CONFLICT(guild_id,user_id) DO UPDATE SET state='pending',updated_at=now()",guild,user);
            await Audit(c,guild,user,"Applied");
        }
        await tx.CommitAsync();return new(){Joined=mode=="Open",GuildId=guild.ToString(),Message=mode=="Open"?"길드에 가입했습니다.":"가입 신청을 보냈습니다."};
    }
    public async Task CancelApplication(Guid user,Guid guild)
    {
        await using var c=await db.Source.OpenConnectionAsync();await using var tx=await c.BeginTransactionAsync();await Gate(c,user);await LockGuild(c,guild);
        await Execute(c,"UPDATE guild_applications SET state='cancelled',updated_at=now() WHERE guild_id=$1 AND user_id=$2 AND state='pending'",guild,user);await tx.CommitAsync();
    }
    public async Task AnswerApplication(Guid actor,Guid guild,Guid target,bool accept)
    {
        await using var c=await db.Source.OpenConnectionAsync();await using var tx=await c.BeginTransactionAsync();await Gate(c,actor,target);await LockGuild(c,guild);await Member(c,actor,guild,true);
        await using(var q=Database.Command(c,"SELECT state FROM guild_applications WHERE guild_id=$1 AND user_id=$2",guild,target))
        {
            string? state=await q.ExecuteScalarAsync() as string;
            if(state==(accept?"accepted":"rejected"))return;
            if(state!="pending")throw new ApiFault(409,"처리할 가입 신청이 없습니다.");
        }
        if(accept)await AddMember(c,guild,target);
        else await Execute(c,"UPDATE guild_applications SET state='rejected',updated_at=now() WHERE guild_id=$1 AND user_id=$2",guild,target);
        await Audit(c,guild,actor,accept?"ApplicationAccepted":"ApplicationRejected",target);await tx.CommitAsync();
    }
    public async Task<GuildReply> Invite(Guid actor,Guid guild,Guid target)
    {
        if(actor==target)throw new ApiFault(400,"자신을 초대할 수 없습니다.");
        await using var c=await db.Source.OpenConnectionAsync();await using var tx=await c.BeginTransactionAsync();await Gate(c,actor,target);await LockGuild(c,guild);await Member(c,actor,guild,true);
        await using(var q=Database.Command(c,"SELECT 1 FROM guild_members WHERE user_id=$1",target))if(await q.ExecuteScalarAsync()!=null)throw new ApiFault(409,"이미 길드에 가입한 회원입니다.");
        await using(var q=Database.Command(c,"SELECT count(*) FROM guild_members WHERE guild_id=$1",guild))if((long)(await q.ExecuteScalarAsync())!>=Capacity)throw new ApiFault(409,"길드 정원이 마감되었습니다.");
        await Execute(c,"""
            INSERT INTO guild_invitations(id,guild_id,inviter,recipient,state,expires_at) VALUES($1,$2,$3,$4,'pending',now()+interval '7 days')
            ON CONFLICT(guild_id,recipient) DO UPDATE SET inviter=excluded.inviter,state='pending',expires_at=excluded.expires_at
            WHERE guild_invitations.state<>'pending' OR guild_invitations.expires_at<=now()
            """,Guid.NewGuid(),guild,actor,target);
        await Audit(c,guild,actor,"Invited",target);await tx.CommitAsync();return new(){Message="길드 초대를 보냈습니다."};
    }
    public async Task<GuildReply> AnswerInvitation(Guid user,Guid invitation,bool accept)
    {
        await using var c=await db.Source.OpenConnectionAsync();await using var tx=await c.BeginTransactionAsync();await Gate(c,user);
        Guid guild,inviter;string state;bool live;
        await using(var q=Database.Command(c,"SELECT guild_id,inviter,state,expires_at>now() FROM guild_invitations WHERE id=$1 AND recipient=$2",invitation,user))
        await using(var r=await q.ExecuteReaderAsync())
        { if(!await r.ReadAsync())throw new ApiFault(404,"길드 초대를 찾을 수 없습니다.");guild=r.GetGuid(0);inviter=r.GetGuid(1);state=r.GetString(2);live=r.GetBoolean(3); }
        await LockGuild(c,guild);
        if(state==(accept?"accepted":"declined"))return new(){Joined=accept,GuildId=guild.ToString(),Message="이미 처리한 초대입니다."};
        if(state!="pending" || !live)throw new ApiFault(409,"종료된 길드 초대입니다.");
        if(accept)
        {
            await Member(c,inviter,guild,true);
            await using(var q=Database.Command(c,"SELECT 1 FROM users WHERE id=$1 AND NOT disabled AND deleted_at IS NULL",inviter))if(await q.ExecuteScalarAsync()==null)throw new ApiFault(409,"유효하지 않은 초대입니다.");
            await AddMember(c,guild,user);
        }
        await Execute(c,"UPDATE guild_invitations SET state=$3 WHERE id=$1 AND recipient=$2",invitation,user,accept?"accepted":"declined");
        await Audit(c,guild,user,accept?"InvitationAccepted":"InvitationDeclined");await tx.CommitAsync();
        return new(){Joined=accept,GuildId=guild.ToString(),Message=accept?"길드에 가입했습니다.":"초대를 거절했습니다."};
    }
    public async Task Edit(Guid actor,Guid guild,GuildEdit input)
    {
        string description=Clean(input.Description,0,100,"소개"),notice=Clean(input.Notice,0,300,"공지",true);Mode(input.JoinMode);
        await using var c=await db.Source.OpenConnectionAsync();await using var tx=await c.BeginTransactionAsync();await Gate(c,actor);await LockGuild(c,guild);await Member(c,actor,guild,true);
        await using(var q=Database.Command(c,"UPDATE guilds SET description=$3,notice=$4,join_mode=$5,revision=revision+1 WHERE id=$1 AND revision=$2",guild,input.Revision,description,notice,input.JoinMode))
            if(await q.ExecuteNonQueryAsync()!=1)throw new ApiFault(409,"다른 운영자가 수정했습니다. 새로고침 후 다시 시도해 주세요.");
        await Audit(c,guild,actor,"Edited");await tx.CommitAsync();
    }
    public async Task Role(Guid actor,Guid guild,Guid target,string role)
    {
        if(actor==target || role is not ("Leader" or "Officer" or "Member"))throw new ApiFault(400,"대상과 직책을 확인해 주세요.");
        await using var c=await db.Source.OpenConnectionAsync();await using var tx=await c.BeginTransactionAsync();await Gate(c,actor,target);await LockGuild(c,guild);
        if(await Member(c,actor,guild)!="Leader")throw new ApiFault(403,"길드장만 직책을 변경할 수 있습니다.");
        await Member(c,target,guild);
        if(role=="Leader")await Execute(c,"UPDATE guild_members SET role='Officer' WHERE user_id=$1",actor);
        await Execute(c,"UPDATE guild_members SET role=$3 WHERE user_id=$1 AND guild_id=$2",target,guild,role);
        await Audit(c,guild,actor,role=="Leader"?"LeadershipTransferred":"RoleChanged",target);await tx.CommitAsync();
    }
    public async Task Kick(Guid actor,Guid guild,Guid target)
    {
        if(actor==target)throw new ApiFault(400,"탈퇴 버튼을 사용해 주세요.");
        await using var c=await db.Source.OpenConnectionAsync();await using var tx=await c.BeginTransactionAsync();await Gate(c,actor,target);await LockGuild(c,guild);
        string role=await Member(c,actor,guild,true),other=await Member(c,target,guild);
        if(other=="Leader" || role=="Officer" && other!="Member")throw new ApiFault(403,"해당 직책의 회원은 내보낼 수 없습니다.");
        await Remove(c,guild,target);await Audit(c,guild,actor,"Kicked",target);await tx.CommitAsync();
    }
    public async Task Leave(Guid user,Guid guild)
    {
        await using var c=await db.Source.OpenConnectionAsync();await using var tx=await c.BeginTransactionAsync();await Gate(c,user);await LockGuild(c,guild);
        string role=await Member(c,user,guild);
        if(role=="Leader")
        {
            await using var q=Database.Command(c,"SELECT count(*) FROM guild_members WHERE guild_id=$1",guild);
            if((long)(await q.ExecuteScalarAsync())!>1)throw new ApiFault(409,"다른 길드원에게 길드장을 위임한 뒤 탈퇴해 주세요.");
        }
        await Remove(c,guild,user);await RepairLeadership(c,guild);await Audit(c,guild,user,"Left");await tx.CommitAsync();
    }
    static async Task Remove(NpgsqlConnection c,Guid guild,Guid user)
    {
        await Execute(c,"DELETE FROM guild_members WHERE guild_id=$1 AND user_id=$2",guild,user);
        await GuildLeague.MemberRemoved(c,user);
        await Execute(c,"UPDATE guild_invitations SET state='closed' WHERE guild_id=$1 AND inviter=$2 AND state='pending'",guild,user);
        await Execute(c,"UPDATE friend_challenges SET state='closed' WHERE guild_id=$1 AND (sender=$2 OR recipient=$2) AND state IN ('pending','accepted')",guild,user);
    }
    static async Task RepairLeadership(NpgsqlConnection c,Guid guild)
    {
        await using var q=Database.Command(c,"SELECT count(*),count(*) FILTER(WHERE role='Leader') FROM guild_members WHERE guild_id=$1",guild);
        long count,leaders;await using(var r=await q.ExecuteReaderAsync()){await r.ReadAsync();count=r.GetInt64(0);leaders=r.GetInt64(1);}
        if(count==0)
        {
            await Execute(c,"UPDATE guilds SET deleted_at=now() WHERE id=$1",guild);
            await Execute(c,"UPDATE guild_invitations SET state='closed' WHERE guild_id=$1 AND state='pending'",guild);
            await Execute(c,"UPDATE guild_applications SET state='cancelled',updated_at=now() WHERE guild_id=$1 AND state='pending'",guild);
        }
        else if(leaders==0)
            await Execute(c,"UPDATE guild_members SET role='Leader' WHERE user_id=(SELECT m.user_id FROM guild_members m JOIN users u ON u.id=m.user_id WHERE m.guild_id=$1 ORDER BY u.disabled,CASE m.role WHEN 'Officer' THEN 0 ELSE 1 END,m.joined_at,m.user_id LIMIT 1)",guild);
    }
    internal static async Task RemoveDeletedMembers(NpgsqlConnection c,Guid[] users)
    {
        await using(var exists=Database.Command(c,"SELECT to_regclass('guild_members') IS NOT NULL"))if(!(bool)(await exists.ExecuteScalarAsync())!)return;
        var guilds=new List<Guid>();
        await using(var q=Database.Command(c,"SELECT id FROM guilds WHERE id IN(SELECT guild_id FROM guild_members WHERE user_id=ANY($1)) ORDER BY id FOR UPDATE",users))
        await using(var r=await q.ExecuteReaderAsync())while(await r.ReadAsync())guilds.Add(r.GetGuid(0));
        foreach(var guild in guilds)
        {
            foreach(var user in users)await Remove(c,guild,user);
            await RepairLeadership(c,guild);
        }
        await Execute(c,"UPDATE guild_invitations SET state='closed' WHERE (inviter=ANY($1) OR recipient=ANY($1)) AND state='pending'",users);
        await Execute(c,"UPDATE guild_applications SET state='cancelled',updated_at=now() WHERE user_id=ANY($1) AND state='pending'",users);
    }
}
