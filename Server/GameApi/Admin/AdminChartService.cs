using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MagicOnline.GameApi.Admin;

public sealed record ChartField(string Key, string Label, bool Numeric = true);
public sealed record ChartDocument(JsonArray Rows, string Revision);
public sealed class AdminChartService(Database db)
{
    public static ChartField[] Fields(string name) => name switch {
        "SkillMatch" => [new("HallIdx","경기장 코드"), new("MatchBall","종목 (3 / 4)"), new("TargetHit","목표 점수"), new("PrizeCoin","입장료 (Coin)"), new("MaxCoin","최대 Coin"), new("HallName","경기장 이름",false), new("MatchTotMin","경기 시간 (분)"), new("MatchCushion","쿠션 수 (0~3)"), new("FinishMission","마무리 미션 (0~3)")],
        "Attendance" => [new("DayNum","출석 일차"), new("RewardType","보상 종류 (1: Coin / 2: Jem)"), new("RewardVal","보상 수량")],
        "CommonCode" => [new("GrpCd","그룹 코드",false), new("ComCd","공통 코드",false), new("ComVal","값 1"), new("ComVal2","값 2"), new("Info","설명",false), new("IsUse","사용 여부 (Y / N)",false)],
        _ => throw new ApiFault(404,"차트를 찾을 수 없습니다.")
    };
    static string Hash(string json) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    public async Task<ChartDocument> Load(string name)
    {
        _ = Fields(name);
        await using var cmd = db.Source.CreateCommand("SELECT rows::text FROM charts WHERE name=$1");
        cmd.Parameters.AddWithValue(name);
        var json = await cmd.ExecuteScalarAsync() as string ?? throw new ApiFault(404,"차트를 찾을 수 없습니다.");
        return new(JsonNode.Parse(json)!.AsArray(),Hash(json));
    }
    public async Task Save(ClaimsPrincipal actor, string name, int? row, string? revision, Dictionary<string,string> values, string? reason, string requestId)
    {
        if (!(actor.IsInRole("Owner") || actor.IsInRole("Operator")) || !Guid.TryParse(actor.FindFirstValue(ClaimTypes.NameIdentifier),out var admin))
            throw new ApiFault(403,"차트를 수정할 권한이 없습니다.");
        var fields = Fields(name);
        if (reason?.Length > 200 || (reason?.Any(char.IsControl) ?? false)) throw new ApiFault(400,"변경 사유는 제어 문자 없이 200자 이내로 입력해 주세요.");
        var edited = new JsonObject();
        foreach (var f in fields)
        {
            var value = (values.GetValueOrDefault(f.Key,"") ?? "").Trim();
            if (f.Numeric)
            {
                if (!long.TryParse(value,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out var n) ||
                    (f.Key is not ("PrizeCoin" or "MaxCoin") && (n < int.MinValue || n > int.MaxValue)))
                    throw new ApiFault(400,$"{f.Label}: 올바른 정수를 입력해 주세요.");
                edited[f.Key] = n;
            }
            else
            {
                if ((value.Length == 0 && f.Key != "Info") || value.Length > (f.Key == "Info" ? 500 : 100) || value.Any(char.IsControl))
                    throw new ApiFault(400,$"{f.Label}: 길이와 입력 내용을 확인해 주세요.");
                edited[f.Key] = value;
            }
        }
        long N(string key) => edited[key]!.GetValue<long>();
        void Require(bool condition,string message) { if (!condition) throw new ApiFault(400,message); }
        if (name == "SkillMatch")
        {
            Require(N("HallIdx")>0 && N("MatchBall") is 3 or 4 && N("TargetHit")>0,"경기장 코드는 양수, 종목은 3 또는 4, 목표 점수는 양수여야 합니다.");
            Require(N("PrizeCoin")>=0 && N("PrizeCoin")<=long.MaxValue/2 && N("MaxCoin")>=N("PrizeCoin"),"입장료는 0 이상이어야 하며 최대 Coin 이하여야 합니다.");
            Require(N("MatchTotMin")>0 && N("MatchTotMin")<=int.MaxValue/60 && N("MatchCushion") is >=0 and <=3 && (N("MatchBall")!=4 || N("MatchCushion")==0) && N("FinishMission") is >=0 and <=3,"경기 시간·쿠션 수·마무리 미션을 확인해 주세요. 4구의 쿠션 수는 0입니다.");
        }
        if (name == "Attendance") Require(N("DayNum")>0 && N("RewardType") is 1 or 2 && N("RewardVal")>=0,"일차는 양수, 보상 종류는 1 또는 2, 보상 수량은 0 이상이어야 합니다.");
        if (name == "CommonCode") Require(edited["IsUse"]!.GetValue<string>() is "Y" or "N","사용 여부는 Y 또는 N으로 입력해 주세요.");
        await using var c = await db.Source.OpenConnectionAsync();
        await using var tx = await c.BeginTransactionAsync();
        string before;
        await using (var cmd = Database.Command(c,"SELECT rows::text FROM charts WHERE name=$1 FOR UPDATE",name))
            before = await cmd.ExecuteScalarAsync() as string ?? throw new ApiFault(404,"차트를 찾을 수 없습니다.");
        if (Hash(before)!=revision) throw new ApiFault(409,"다른 작업에서 차트가 변경됐습니다. 목록으로 돌아가 최신 내용을 확인한 후 다시 수정해 주세요.");
        var rows = JsonNode.Parse(before)!.AsArray();
        if (row.HasValue && (row<0 || row>=rows.Count)) throw new ApiFault(404,"항목을 찾을 수 없습니다.");
        // Preserve any server-only columns when editing an existing row.
        if (row.HasValue) { var target=rows[row.Value]!.AsObject(); foreach(var p in edited) target[p.Key]=p.Value?.DeepClone(); }
        else rows.Add(edited);
        string Key(JsonNode r) => name == "CommonCode" ? JsonSerializer.Serialize(new[]{r["GrpCd"]!.ToString(),r["ComCd"]!.ToString()}) : r[name=="SkillMatch" ? "HallIdx" : "DayNum"]!.ToString();
        if (rows.Select(r=>Key(r!)).Distinct(StringComparer.Ordinal).Count()!=rows.Count) throw new ApiFault(400,"동일한 코드가 이미 있습니다. 다른 코드를 입력해 주세요.");
        if (name=="Attendance")
        {
            var days=rows.Select(r=>int.Parse(r!["DayNum"]!.ToString(),CultureInfo.InvariantCulture)).Order().ToArray();
            if (!days.SequenceEqual(Enumerable.Range(1,rows.Count))) throw new ApiFault(400,"출석 일차는 1부터 빈 번호 없이 이어져야 합니다. 마지막 일차 다음 번호로 추가해 주세요.");
        }
        string after=rows.ToJsonString();
        await using(var update=Database.Command(c,"UPDATE charts SET rows=$2::jsonb WHERE name=$1",name,after)) await update.ExecuteNonQueryAsync();
        await using(var audit=Database.Command(c,"INSERT INTO admin_audit_logs(admin_id,action,target,request_id,before_value,after_value,reason) VALUES($1,$2,$3,$4,$5::jsonb,$6::jsonb,$7)",admin,row.HasValue ? "ChartRowUpdated" : "ChartRowAdded",name,requestId,before,after,string.IsNullOrWhiteSpace(reason) ? "관리자 차트 변경" : reason.Trim())) await audit.ExecuteNonQueryAsync();
        await tx.CommitAsync();
    }
}
