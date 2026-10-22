using System.Security.Claims;
using System.Text.Json;

namespace MagicOnline.GameApi.Admin;

public sealed class AdminMemberService(Database db)
{
    public async Task Rename(ClaimsPrincipal actor, Guid member, string? nickname, string? original, string? reason, string requestId)
    {
        if ((!actor.IsInRole("Owner") && !actor.IsInRole("Operator")) ||
            !Guid.TryParse(actor.FindFirstValue(ClaimTypes.NameIdentifier), out var admin))
            throw new ApiFault(403, "닉네임을 수정할 권한이 없습니다.");
        if (string.IsNullOrWhiteSpace(nickname) || nickname.Trim().Length > 24 || nickname.Any(char.IsControl))
            throw new ApiFault(400, "닉네임은 공백만 사용할 수 없으며 1~24자로 입력해 주세요. 제어 문자는 사용할 수 없습니다.");
        if (reason?.Length > 200 || (reason?.Any(char.IsControl) ?? false))
            throw new ApiFault(400, "수정 사유는 제어 문자 없이 200자 이내로 입력해 주세요.");
        nickname=nickname.Trim();
        reason=string.IsNullOrWhiteSpace(reason) ? "관리자 닉네임 수정" : reason.Trim();
        await using var c = await db.Source.OpenConnectionAsync();
        await using var tx = await c.BeginTransactionAsync();
        string before;
        await using (var cmd = Database.Command(c, "SELECT nickname FROM users WHERE id=$1 FOR UPDATE", member))
            before=await cmd.ExecuteScalarAsync() as string ?? throw new ApiFault(404, "회원을 찾을 수 없습니다.");
        // A repeated submission of the same desired value has no additional effect.
        if (before==nickname) return;
        if (before!=original) throw new ApiFault(409, "다른 작업에서 닉네임이 변경됐습니다. 현재 닉네임을 확인하고 다시 저장해 주세요.");
        await using (var update = Database.Command(c, "UPDATE users SET nickname=$2 WHERE id=$1", member,nickname))
            await update.ExecuteNonQueryAsync();
        await using (var audit = Database.Command(c, "INSERT INTO admin_audit_logs(admin_id,action,target,request_id,before_value,after_value,reason) VALUES($1,'MemberNicknameChanged',$2,$3,$4::jsonb,$5::jsonb,$6)",
            admin,member.ToString(),requestId,JsonSerializer.Serialize(new{NickName=before}),JsonSerializer.Serialize(new{NickName=nickname}),reason))
            await audit.ExecuteNonQueryAsync();
        await tx.CommitAsync();
    }
}
