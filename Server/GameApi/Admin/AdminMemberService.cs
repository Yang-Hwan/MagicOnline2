using System.Security.Claims;
using System.Text.Json;

namespace MagicOnline.GameApi.Admin;

public sealed partial class AdminMemberService(Database db)
{
    public async Task AdjustWallet(ClaimsPrincipal actor, Guid member, string currency, string direction, long amount, Guid operation, string? reason)
    {
        if ((!actor.IsInRole("Owner") && !actor.IsInRole("Operator")) ||
            !Guid.TryParse(actor.FindFirstValue(ClaimTypes.NameIdentifier), out var admin))
            throw new ApiFault(403, "재화를 조정할 권한이 없습니다.");
        if (currency is not ("Coin" or "Jem") || direction is not ("credit" or "debit") || operation == Guid.Empty)
            throw new ApiFault(400, "재화와 조정 방식을 확인해 주세요.");
        long limit = currency == "Coin" ? 9_000_000_000_000_000L : int.MaxValue;
        if (amount <= 0 || amount > limit)
            throw new ApiFault(400, "수량을 재화의 허용 범위 안에서 양의 정수로 입력해 주세요.");
        if (reason?.Length > 200 || (reason?.Any(char.IsControl) ?? false))
            throw new ApiFault(400, "조정 사유는 제어 문자 없이 200자 이내로 입력해 주세요.");

        long delta = direction == "credit" ? amount : -amount;
        long coin = currency == "Coin" ? delta : 0;
        int jem = currency == "Jem" ? (int)delta : 0;
        string key = $"AdminAdjust:{admin:N}:{operation:N}";
        await using var c = await db.Source.OpenConnectionAsync();
        await using var tx = await c.BeginTransactionAsync();
        await using var wallet = Database.Command(c, "SELECT w.coin,w.jem FROM wallets w JOIN users u ON u.id=w.user_id WHERE w.user_id=$1 AND u.deleted_at IS NULL FOR UPDATE OF w FOR SHARE OF u", member);
        long before;
        await using (var row = await wallet.ExecuteReaderAsync())
        {
            if (!await row.ReadAsync()) throw new ApiFault(404, "회원 지갑을 찾을 수 없습니다.");
            before = currency == "Coin" ? row.GetInt64(0) : row.GetInt32(1);
        }
        await using var entry = Database.Command(c, "INSERT INTO wallet_ledger(user_id,request_key,coin_delta,jem_delta) VALUES($1,$2,$3,$4) ON CONFLICT(user_id,request_key) DO NOTHING", member,key,coin,jem);
        if (await entry.ExecuteNonQueryAsync() == 0)
        {
            await using var previous = Database.Command(c, "SELECT coin_delta=$3 AND jem_delta=$4 FROM wallet_ledger WHERE user_id=$1 AND request_key=$2",member,key,coin,jem);
            if (await previous.ExecuteScalarAsync() is not true)
                throw new ApiFault(409, "이미 처리된 요청입니다. 새로고침 후 다시 입력해 주세요.");
            return;
        }
        long after = before + delta;
        if (after < 0) throw new ApiFault(400, "차감할 수량이 현재 잔액보다 많습니다.");
        if (after > limit) throw new ApiFault(400, "조정 후 잔액이 보유 한도를 초과합니다.");
        await using var update = Database.Command(c, "UPDATE wallets SET coin=coin+$2,jem=jem+$3 WHERE user_id=$1",member,coin,jem);
        await update.ExecuteNonQueryAsync();
        await using var audit = Database.Command(c, "INSERT INTO admin_audit_logs(admin_id,action,target,request_id,before_value,after_value,reason) VALUES($1,'MemberWalletAdjusted',$2,$3,$4::jsonb,$5::jsonb,$6)",
            admin,member.ToString(),key,JsonSerializer.Serialize(new {currency,balance=before}),JsonSerializer.Serialize(new {currency,balance=after,delta}),string.IsNullOrWhiteSpace(reason) ? "관리자 재화 조정" : reason.Trim());
        await audit.ExecuteNonQueryAsync();
        await tx.CommitAsync();
    }

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
        await using (var cmd = Database.Command(c, "SELECT nickname FROM users WHERE id=$1 AND deleted_at IS NULL FOR UPDATE", member))
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
