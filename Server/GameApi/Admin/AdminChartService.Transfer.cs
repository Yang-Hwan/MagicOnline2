using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MagicOnline.GameApi.Admin;

public sealed partial class AdminChartService
{
    // Match the PostReward editor's lock order (reward first, then common codes).
    static readonly string[] TransferNames = ["PostReward", "CommonCode", "Attendance", "SkillMatch"];

    public async Task<JsonObject> ExportCharts()
    {
        await using var cmd = db.Source.CreateCommand("SELECT name,rows::text FROM charts WHERE name=ANY($1)");
        cmd.Parameters.AddWithValue(TransferNames);
        var charts = new JsonObject();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            charts[reader.GetString(0)] = JsonNode.Parse(reader.GetString(1));
        if (charts.Count != TransferNames.Length) throw new ApiFault(409,"DB에 차트 4종이 모두 있어야 내보낼 수 있습니다.");
        return new JsonObject { ["version"] = 1, ["charts"] = charts };
    }

    static void CheckProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) CheckProperties(item);
        if (element.ValueKind != JsonValueKind.Object) return;
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!keys.Add(property.Name)) throw new ApiFault(400,"JSON에 중복된 필드명이 있습니다.");
            CheckProperties(property.Value);
        }
    }

    public async Task ImportCharts(ClaimsPrincipal actor, Stream file, string requestId)
    {
        if (!(actor.IsInRole("Owner") || actor.IsInRole("Operator")) ||
            !Guid.TryParse(actor.FindFirstValue(ClaimTypes.NameIdentifier),out var admin))
            throw new ApiFault(403,"차트를 가져올 권한이 없습니다.");

        using var document = await JsonDocument.ParseAsync(file);
        var root = document.RootElement;
        CheckProperties(root);
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("version",out var version) ||
            version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out int v) || v != 1 ||
            !root.TryGetProperty("charts",out var charts) || charts.ValueKind != JsonValueKind.Object ||
            charts.EnumerateObject().Count() != TransferNames.Length)
            throw new ApiFault(400,"전체 내보내기로 받은 버전 1 차트 파일을 선택해 주세요.");

        var incoming = new Dictionary<string,JsonArray>();
        foreach (string name in TransferNames)
        {
            if (!charts.TryGetProperty(name,out var data) || data.ValueKind != JsonValueKind.Array)
                throw new ApiFault(400,$"{name} 차트 목록이 없습니다.");
            var rows = JsonNode.Parse(data.GetRawText())!.AsArray();
            var fields = Fields(name);
            int index = 0;
            foreach (var item in rows)
            {
                index++;
                if (item is not JsonObject row) throw new ApiFault(400,$"{name} {index}행의 형식이 올바르지 않습니다.");
                var values = fields.ToDictionary(f => f.Key, f => row[f.Key]?.ToString() ?? "");
                if (name == "CommonCode" && !row.ContainsKey("Ord")) values["Ord"] = "0";
                try
                {
                    var parsed = ParseRow(name,fields,values);
                    foreach (var field in fields)
                    {
                        if (row[field.Key] is JsonObject or JsonArray ||
                            (!field.Numeric && row[field.Key] is JsonValue text && !text.TryGetValue<string>(out _)))
                            throw new ApiFault(400,$"{field.Label} 값의 형식이 올바르지 않습니다.");
                        // Keep optional legacy omissions and all additional server-only columns.
                        if (row.ContainsKey(field.Key) && field.Numeric)
                            row[field.Key] = parsed[field.Key]?.DeepClone();
                    }
                }
                catch (ApiFault error) { throw new ApiFault(400,$"{name} {index}행 · {error.Message}"); }
            }
            if (name == "Attendance" && rows.Count == 0) throw new ApiFault(400,"출석 보상은 1일차 이상 필요합니다.");
            ValidateKeys(name,rows);
            incoming.Add(name,rows);
        }
        PostRewardChart.Validate(incoming["PostReward"],incoming["CommonCode"]);

        await using var connection = await db.Source.OpenConnectionAsync();
        await using var tx = await connection.BeginTransactionAsync();
        foreach (string name in TransferNames)
        {
            await using var find = Database.Command(connection,"SELECT rows::text FROM charts WHERE name=$1 FOR UPDATE",name);
            var before = await find.ExecuteScalarAsync() as string ?? throw new ApiFault(409,$"대상 DB에 {name} 차트가 없습니다.");
            var after = incoming[name].ToJsonString();
            await using var update = Database.Command(connection,"UPDATE charts SET rows=$2::jsonb WHERE name=$1 AND rows IS DISTINCT FROM $2::jsonb",name,after);
            if (await update.ExecuteNonQueryAsync() == 0) continue;
            await using var audit = Database.Command(connection,"INSERT INTO admin_audit_logs(admin_id,action,target,request_id,before_value,after_value,reason) VALUES($1,'ChartImported',$2,$3,$4::jsonb,$5::jsonb,'차트 전체 가져오기')",admin,name,requestId,before,after);
            await audit.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
    }
}
