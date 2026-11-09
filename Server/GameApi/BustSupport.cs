using System.Globalization;
using System.Text.Json;

namespace MagicOnline.GameApi;

public sealed record BustSupportResult(bool Granted, long Remaining);

public sealed class BustSupport(Database db, TimeProvider clock)
{
    public async Task<bool> Check(Guid user) => (await CheckResult(user)).Granted;

    public async Task<BustSupportResult> CheckResult(Guid user)
    {
        await using var c = await db.Source.OpenConnectionAsync();
        await using var tx = await c.BeginTransactionAsync();
        long balance;
        // Serialize support checks with each other and all other wallet writes.
        await using (var wallet = Database.Command(c, "SELECT coin FROM wallets WHERE user_id=$1 FOR UPDATE", user))
            balance = (long?)await wallet.ExecuteScalarAsync() ?? throw new ApiFault(404, "UserNotFound");
        var now = clock.GetUtcNow();
        var charts = new Dictionary<string, JsonElement>();
        await using (var query = Database.Command(c, "SELECT name,rows::text FROM charts WHERE name IN ('CommonCode','SkillMatch')"))
        await using (var reader = await query.ExecuteReaderAsync())
            while (await reader.ReadAsync()) charts[reader.GetString(0)] = JsonSerializer.Deserialize<JsonElement>(reader.GetString(1));
        if (!charts.TryGetValue("CommonCode", out var codes) || !charts.TryGetValue("SkillMatch", out var halls)) return new(false, 0);
        static string Text(JsonElement row, string key) => row.TryGetProperty(key, out var value) ? value.ToString().Trim() : "";
        long? Code(string key)
        {
            var matches = codes.EnumerateArray().Where(row => Text(row, "GrpCd") == "Bust" && Text(row, "ComCd") == key && Text(row, "IsUse") == "Y").ToArray();
            return matches.Length == 1 && long.TryParse(Text(matches[0], "ComVal"), out var value) ? value : null;
        }
        var amount = Code("BustHelpCoin");
        var limit = Code("Bust1DayHelpCnt");
        var fees = halls.EnumerateArray().Select(row => long.TryParse(Text(row, "PrizeCoin"), out var fee) ? fee : -1).ToArray();
        if (amount is null or <= 0 || limit is null or <= 0 || fees.Length == 0 || fees.Any(fee => fee < 0) || balance >= fees.Min()) return new(false, 0);
        if (amount > 9000000000000000L - balance) return new(false, 0);
        // KST calendar day, shared across devices and preserved across server restarts.
        string prefix = "Bust:" + now.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ":";
        long count;
        DateTime? last;
        await using (var query = Database.Command(c, "SELECT count(*),max(created_at) FROM wallet_ledger WHERE user_id=$1 AND request_key LIKE $2", user, prefix + "%"))
        await using (var reader = await query.ExecuteReaderAsync())
        {
            await reader.ReadAsync(); count = reader.GetInt64(0);
            last = reader.IsDBNull(1) ? null : reader.GetDateTime(1);
        }
        if (count >= limit || (last.HasValue && now.UtcDateTime - last.Value < TimeSpan.FromSeconds(30))) return new(false, 0);
        await using (var credit = Database.Command(c, "UPDATE wallets SET coin=coin+$2 WHERE user_id=$1", user, amount.Value)) await credit.ExecuteNonQueryAsync();
        await using (var ledger = Database.Command(c, "INSERT INTO wallet_ledger(user_id,request_key,coin_delta,jem_delta,created_at) VALUES($1,$2,$3,0,$4)", user, prefix + (count + 1).ToString(CultureInfo.InvariantCulture), amount.Value, now.UtcDateTime)) await ledger.ExecuteNonQueryAsync();
        int remaining=checked((int)Math.Max(0,limit.Value-count-1));
        MemberRewards.Load(c).Send(c,user,"파산",prefix+(count+1).ToString(CultureInfo.InvariantCulture),remaining);
        await tx.CommitAsync();
        return new(true, Math.Max(0, limit.Value - count - 1));
    }
}
