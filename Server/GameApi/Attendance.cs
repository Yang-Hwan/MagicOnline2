using System.Text.Json;

namespace MagicOnline.GameApi;

public sealed class Attendance(Database db, GameData data)
{
    public async Task<object> CheckIn(Guid user)
    {
        var chart = await data.Chart("Attendance"); var rules = chart.EnumerateArray().ToArray();
        if (rules.Length == 0) throw new ApiFault(409, "AttendanceNotConfigured");
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(9));
        await using var c = await db.Source.OpenConnectionAsync(); await using var tx = await c.BeginTransactionAsync();
        // The same wallet lock is used by match settlement.
        await using (var wallet = Database.Command(c, "SELECT user_id FROM wallets WHERE user_id=$1 FOR UPDATE", user)) await wallet.ExecuteScalarAsync();
        DateOnly start = today, end = today.AddDays(rules.Length - 1);
        await using (var term = Database.Command(c, "SELECT starts_on,ends_on FROM attendance_terms WHERE user_id=$1 AND starts_on<=$2 AND ends_on>=$2 ORDER BY starts_on DESC LIMIT 1", user, today))
        await using (var r = await term.ExecuteReaderAsync())
            if (await r.ReadAsync()) { start = r.GetFieldValue<DateOnly>(0); end = r.GetFieldValue<DateOnly>(1); }
        await using (var term = Database.Command(c, "INSERT INTO attendance_terms(user_id,starts_on,ends_on) VALUES($1,$2,$3) ON CONFLICT DO NOTHING", user, start, end)) await term.ExecuteNonQueryAsync();
        int day = today.DayNumber - start.DayNumber + 1;
        var rule = rules.Single(r => r.GetProperty("DayNum").GetInt32() == day);
        int type = rule.GetProperty("RewardType").GetInt32(), value = rule.GetProperty("RewardVal").GetInt32();
        if (type is not (1 or 2) || value < 0) throw new ApiFault(409, "InvalidAttendanceReward");
        int added;
        await using (var insert = Database.Command(c, "INSERT INTO user_attendance(user_id,give_date,day_num,reward_type,reward_val) VALUES($1,$2,$3,$4,$5) ON CONFLICT DO NOTHING", user, today, day, type, value)) added = await insert.ExecuteNonQueryAsync();
        if (added > 0)
        {
            long coin = type == 1 ? value : 0; int jem = type == 2 ? value : 0;
            await using (var credit = Database.Command(c, "UPDATE wallets SET coin=coin+$2,jem=jem+$3 WHERE user_id=$1", user, coin, jem)) await credit.ExecuteNonQueryAsync();
            await using var ledger = Database.Command(c, "INSERT INTO wallet_ledger(user_id,request_key,coin_delta,jem_delta) VALUES($1,$2,$3,$4)", user, "Attendance:" + today.ToString("yyyy-MM-dd"), coin, jem); await ledger.ExecuteNonQueryAsync();
        }
        var received = new HashSet<int>();
        await using (var query = Database.Command(c, "SELECT day_num FROM user_attendance WHERE user_id=$1 AND give_date BETWEEN $2 AND $3", user, start, end))
        await using (var r = await query.ExecuteReaderAsync()) while (await r.ReadAsync()) received.Add(r.GetInt32(0));
        await tx.CommitAsync();
        long Stamp(DateOnly date) => new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(9)).ToUnixTimeSeconds();
        return new { StTs = Stamp(start), EnTs = Stamp(end), DtInfo = $"{start:MM.dd} - {end:MM.dd}", Rows = rules.Select(r => new {
            DayNum = r.GetProperty("DayNum").GetInt32(), RewardType = r.GetProperty("RewardType").GetInt32(), RewardVal = r.GetProperty("RewardVal").GetInt32(),
            GiveVal = received.Contains(r.GetProperty("DayNum").GetInt32()) ? r.GetProperty("DayNum").GetInt32() : r.GetProperty("DayNum").GetInt32() > day ? -1 : 0
        }).ToArray() };
    }
}
