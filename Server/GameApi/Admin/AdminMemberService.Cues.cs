using System.Security.Claims;
using System.Text.Json;

namespace MagicOnline.GameApi.Admin;

public sealed partial class AdminMemberService
{
    public async Task ResetCue(ClaimsPrincipal actor,Guid member,long cue,Guid operation,string? reason)
    {
        if((!actor.IsInRole("Owner") && !actor.IsInRole("Operator")) || !Guid.TryParse(actor.FindFirstValue(ClaimTypes.NameIdentifier),out var admin))
            throw new ApiFault(403,"큐대를 초기화할 권한이 없습니다.");
        if(cue<1 || operation==Guid.Empty)throw new ApiFault(400,"초기화할 큐대를 확인해 주세요.");
        if(reason?.Length>200 || (reason?.Any(char.IsControl)??false))throw new ApiFault(400,"사유는 제어 문자 없이 200자 이내로 입력해 주세요.");
        string key=$"AdminCueReset:{member:N}:{cue}:{operation:N}";
        await using var c=await db.Source.OpenConnectionAsync();
        await using var tx=await c.BeginTransactionAsync();
        // Same lock order as growth and equipment changes; a retry cannot erase later growth.
        await using(var gate=Database.Command(c,"SELECT w.user_id FROM wallets w JOIN users u ON u.id=w.user_id WHERE w.user_id=$1 AND u.deleted_at IS NULL FOR UPDATE OF w FOR SHARE OF u",member))
            if(await gate.ExecuteScalarAsync()==null)throw new ApiFault(404,"회원을 찾을 수 없습니다.");
        await using(var retry=Database.Command(c,"SELECT EXISTS(SELECT FROM admin_audit_logs WHERE action='MemberCueReset' AND target=$1 AND request_id=$2)",member.ToString(),key))
            if(await retry.ExecuteScalarAsync() is true)return;
        string before,latest;
        await using(var query=Database.Command(c,"""
            SELECT jsonb_build_object('CueId',i.product_id,'Ability',COALESCE(i.cue_ability,c.ability),'Shots',COALESCE(g.shots,0),'Steps',COALESCE(g.steps,0),'Progress',COALESCE(g.progress,0),'Bowls',g.bowls)::text,c.ability::text
            FROM shop_user_inventory i JOIN shop_products p ON p.id=i.product_id JOIN shop_cues c ON c.product_id=p.id
            LEFT JOIN shop_cue_progress g ON g.user_id=i.user_id AND g.product_id=i.product_id
            WHERE i.user_id=$1 AND i.product_id=$2 FOR SHARE OF p,c FOR UPDATE OF i
            """,member,cue))
        await using(var r=await query.ExecuteReaderAsync()){
            if(!await r.ReadAsync())throw new ApiFault(404,"회원이 보유한 큐대를 찾을 수 없습니다.");
            before=r.GetString(0);latest=r.GetString(1);
        }
        await using(var update=Database.Command(c,"UPDATE shop_user_inventory SET cue_ability=$3::jsonb WHERE user_id=$1 AND product_id=$2",member,cue,latest))await update.ExecuteNonQueryAsync();
        using var beforeDocument=JsonDocument.Parse(before);
        var savedBowls=beforeDocument.RootElement.GetProperty("Bowls");
        var bowls=savedBowls.ValueKind==JsonValueKind.Null?new Assets.Scripts.Prack.BackSys.GameData.CueBowlState():JsonSerializer.Deserialize<Assets.Scripts.Prack.BackSys.GameData.CueBowlState>(savedBowls.GetRawText(),Database.Json)!;
        bowls.Auto=JsonSerializer.Deserialize<Assets.Scripts.Prack.BackSys.GameData.CueAbilitySettings>(latest,Database.Json)!.BowlAuto;
        bowls.Reset(JsonSerializer.Deserialize<Assets.Scripts.Prack.BackSys.GameData.CueAbilitySettings>(latest,Database.Json)!.BowlMaximum);
        await using(var reset=Database.Command(c,"INSERT INTO shop_cue_progress(user_id,product_id,shots,steps,progress,bowls) VALUES($1,$2,0,0,0,$3::jsonb) ON CONFLICT(user_id,product_id) DO UPDATE SET shots=0,steps=0,progress=0,bowls=excluded.bowls",member,cue,JsonSerializer.Serialize(bowls,Database.Json)))await reset.ExecuteNonQueryAsync();
        // Keep historical scoring keys so already processed shots cannot be credited again.
        string after=JsonSerializer.Serialize(new{CueId=cue,Ability=JsonSerializer.Deserialize<JsonElement>(latest),Shots=0,Steps=0,Progress=0,Bowls=bowls});
        await using(var audit=Database.Command(c,"INSERT INTO admin_audit_logs(admin_id,action,target,request_id,before_value,after_value,reason) VALUES($1,'MemberCueReset',$2,$3,$4::jsonb,$5::jsonb,$6)",admin,member.ToString(),key,before,after,string.IsNullOrWhiteSpace(reason)?"큐대 최신 설정 적용 및 성장 초기화":reason.Trim()))await audit.ExecuteNonQueryAsync();
        await tx.CommitAsync();
    }
}
