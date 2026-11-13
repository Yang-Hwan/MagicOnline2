using System.Security.Claims;
using System.Text.Json;
using Npgsql;

namespace MagicOnline.GameApi.Admin;

public sealed partial class AdminMemberService
{
    public sealed record DeleteCandidate(Guid Id, string Login, string Nickname, bool Deleted, bool HasMatch);
    static Guid DeleteActor(ClaimsPrincipal actor)
    {
        if (!actor.IsInRole("Owner") || !Guid.TryParse(actor.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
            throw new ApiFault(403, "회원 삭제는 최고 관리자만 할 수 있습니다.");
        return id;
    }
    static void ValidateIds(Guid[] ids)
    {
        if (ids.Length is < 1 or > 30 || ids.Any(id => id == Guid.Empty) || ids.Distinct().Count() != ids.Length)
            throw new ApiFault(400, "삭제할 회원을 1~30명 선택해 주세요.");
    }
    public async Task<List<DeleteCandidate>> DeletePreview(ClaimsPrincipal actor, Guid[] ids)
    {
        DeleteActor(actor); ValidateIds(ids);
        await using var cmd = db.Source.CreateCommand("""
            SELECT u.id,u.login_id,u.nickname,u.deleted_at IS NOT NULL,
                EXISTS(SELECT FROM matches m WHERE (m.user_a=u.id OR m.user_b=u.id) AND m.phase<>2)
            FROM users u WHERE u.id=ANY($1) ORDER BY u.login_id,u.id
            """);
        cmd.Parameters.AddWithValue(ids);
        await using var reader = await cmd.ExecuteReaderAsync();
        var rows = new List<DeleteCandidate>();
        while (await reader.ReadAsync()) rows.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetBoolean(3), reader.GetBoolean(4)));
        if (rows.Count != ids.Length) throw new ApiFault(404, "선택한 회원을 찾을 수 없습니다. 목록에서 다시 선택해 주세요.");
        return rows;
    }
    public async Task<int> DeleteMembers(ClaimsPrincipal actor, Guid[] ids, string[] expectedLogins, string? confirmation, string? reason, string requestId)
    {
        Guid admin = DeleteActor(actor); ValidateIds(ids);
        if (expectedLogins.Length != ids.Length || confirmation?.Trim() != "삭제")
            throw new ApiFault(400, "삭제 대상과 확인 문구 ‘삭제’를 확인해 주세요.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 200 || reason.Any(char.IsControl))
            throw new ApiFault(400, "삭제 사유를 제어 문자 없이 1~200자로 입력해 주세요.");
        var expected = ids.Zip(expectedLogins).ToDictionary(pair => pair.First, pair => pair.Second);
        await using var c = await db.Source.OpenConnectionAsync();
        await using var tx = await c.BeginTransactionAsync();
        // Same order as wallet writers; user locks serialize deletion with new match registration/login.
        await using (var wallets = Database.Command(c, "SELECT user_id FROM wallets WHERE user_id=ANY($1) ORDER BY user_id FOR UPDATE", ids))
        await using (var rows = await wallets.ExecuteReaderAsync()) while (await rows.ReadAsync()) { }
        var members = new List<(Guid Id, string Login, string Nickname, bool Deleted)>();
        await using (var select = Database.Command(c, "SELECT id,login_id,nickname,deleted_at IS NOT NULL FROM users WHERE id=ANY($1) ORDER BY id FOR UPDATE", ids))
        await using (var rows = await select.ExecuteReaderAsync())
            while (await rows.ReadAsync()) members.Add((rows.GetGuid(0), rows.GetString(1), rows.GetString(2), rows.GetBoolean(3)));
        if (members.Count != ids.Length) throw new ApiFault(404, "삭제 대상을 찾을 수 없습니다.");
        if (members.Any(member => member.Login != expected[member.Id])) throw new ApiFault(409, "삭제 대상 정보가 변경됐습니다. 목록에서 다시 선택해 주세요.");
        var current = members.Where(member => !member.Deleted).ToArray();
        if (current.Length == 0) return 0; // Repeat submission: no extra audit or side effects.
        var currentIds = current.Select(member => member.Id).ToArray();
        await using (var active = Database.Command(c, "SELECT EXISTS(SELECT FROM matches WHERE phase<>2 AND (user_a=ANY($1) OR user_b=ANY($1)))", currentIds))
            if (await active.ExecuteScalarAsync() is true) throw new ApiFault(409, "준비 중·진행 중이거나 미정산인 경기가 있는 회원은 삭제할 수 없습니다. 해당 경기를 종료한 뒤 다시 시도해 주세요.");
        // Refresh rotates a locked session; avoid waiting on authentication work while holding user locks.
        try
        {
            await using var sessions = Database.Command(c, "SELECT id FROM auth_sessions WHERE user_id=ANY($1) ORDER BY id FOR UPDATE NOWAIT", currentIds);
            await using var rows = await sessions.ExecuteReaderAsync(); while (await rows.ReadAsync()) { }
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.LockNotAvailable)
        { throw new ApiFault(409, "회원의 로그인 처리가 진행 중입니다. 잠시 후 다시 삭제해 주세요."); }
        await using (var update = Database.Command(c, "UPDATE users SET deleted_at=now(),disabled=true,normalized_login='DELETED:'||id::text WHERE id=ANY($1)", currentIds)) await update.ExecuteNonQueryAsync();
        await using (var revoke = Database.Command(c, "UPDATE auth_sessions SET revoked=true WHERE user_id=ANY($1)", currentIds)) await revoke.ExecuteNonQueryAsync();
        await using (var challenges = Database.Command(c, "UPDATE friend_challenges SET state='closed' WHERE (sender=ANY($1) OR recipient=ANY($1)) AND state IN ('pending','accepted')", currentIds)) await challenges.ExecuteNonQueryAsync();
        await using (var presence = Database.Command(c, "DELETE FROM friend_presence WHERE user_id=ANY($1)", currentIds)) await presence.ExecuteNonQueryAsync();
        foreach (var member in current)
        {
            await using var audit = Database.Command(c, "INSERT INTO admin_audit_logs(admin_id,action,target,request_id,before_value,after_value,reason) VALUES($1,'MemberDeleted',$2,$3,$4::jsonb,$5::jsonb,$6)",
                admin, member.Id.ToString(), requestId, JsonSerializer.Serialize(new { member.Login, member.Nickname }), JsonSerializer.Serialize(new { Deleted = true, Disabled = true }), reason.Trim());
            await audit.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
        return current.Length;
    }
}
