using System.Text.Json;

namespace MagicOnline.GameApi;

public sealed class Attendance(Database db, TimeProvider clock)
{
    public sealed record Row(int DayNum, int RewardType, int RewardVal, int GiveVal, int Attenddance, DateOnly GiveDt, DateTime? TakeDt);
    public sealed record Reply(long StTs, long EnTs, string DtInfo, Row[] Rows);
    public sealed record Calendar(int TodayDay, List<Row> Rows);
    public async Task<object> CheckIn(Guid user)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(9)).DateTime);
        await using var c = await db.Source.OpenConnectionAsync();
        await using var tx = await c.BeginTransactionAsync();
        // Serialize first-login schedule creation and all same-day check-ins per member.
        await using (var wallet = Database.Command(c, "SELECT user_id FROM wallets WHERE user_id=$1 FOR UPDATE", user))
            if (await wallet.ExecuteScalarAsync() == null) throw new ApiFault(404, "UserNotFound");
        DateOnly start = today, end = today.AddDays(29);
        bool existing = false;
        await using (var term = Database.Command(c, "SELECT starts_on,ends_on FROM attendance_terms WHERE user_id=$1 AND starts_on<=$2 AND ends_on>=$2 ORDER BY starts_on DESC LIMIT 1", user, today))
        await using (var r = await term.ExecuteReaderAsync())
            if (await r.ReadAsync()) { start = r.GetFieldValue<DateOnly>(0); end = r.GetFieldValue<DateOnly>(1); existing = true; }
        int days = end.DayNumber - start.DayNumber + 1;
        long scheduled;
        await using (var count = Database.Command(c, "SELECT count(*) FROM user_attendance WHERE user_id=$1 AND term_start=$2", user, start))
            scheduled = (long)(await count.ExecuteScalarAsync())!;
        if (!existing || scheduled < days)
        {
            // Snapshot the chart once per term; later chart edits affect the next term only.
            await using var config = Database.Command(c, "SELECT rows::text FROM charts WHERE name='Attendance'");
            using var chart = JsonDocument.Parse(await config.ExecuteScalarAsync() as string ?? "[]");
            var rules = chart.RootElement.EnumerateArray().OrderBy(r => r.GetProperty("DayNum").GetInt32()).ToArray();
            if (rules.Length != 30 || !rules.Select(r => r.GetProperty("DayNum").GetInt32()).SequenceEqual(Enumerable.Range(1,30)))
                throw new ApiFault(409, "AttendanceRequires30Days");
            foreach (var rule in rules)
                if (rule.GetProperty("RewardType").GetInt32() is not (1 or 2) || rule.GetProperty("RewardVal").GetInt32() < 0)
                    throw new ApiFault(409, "InvalidAttendanceReward");
            await using (var term = Database.Command(c, "INSERT INTO attendance_terms(user_id,starts_on,ends_on) VALUES($1,$2,$3) ON CONFLICT DO NOTHING", user, start, end))
                await term.ExecuteNonQueryAsync();
            // Existing legacy terms retain their original end date and already-paid records.
            foreach (var rule in rules.Take(days))
            {
                int day = rule.GetProperty("DayNum").GetInt32();
                await using var insert = Database.Command(c, "INSERT INTO user_attendance(user_id,term_start,give_date,day_num,reward_type,reward_val) VALUES($1,$2,$3,$4,$5,$6) ON CONFLICT DO NOTHING",
                    user,start,start.AddDays(day-1),day,rule.GetProperty("RewardType").GetInt32(),rule.GetProperty("RewardVal").GetInt32());
                await insert.ExecuteNonQueryAsync();
            }
        }
        int marked;
        // In particular, repeated logins must never clear an existing receipt timestamp.
        await using (var mark = Database.Command(c, "UPDATE user_attendance SET attendance=true WHERE user_id=$1 AND term_start=$2 AND give_date=$3 AND NOT attendance", user,start,today))
            marked=await mark.ExecuteNonQueryAsync();
        var rows = await ReadRows(c, user, start, today);
        if (marked==1)
        {
            var day=rows.Single(r=>r.GiveDt==today);
            MemberRewards.Load(c).Send(c,user,"출석","Attendance:"+today.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture),
                day.DayNum,day.RewardType,day.RewardVal);
        }
        await tx.CommitAsync();
        long Stamp(DateOnly date) => new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue),TimeSpan.FromHours(9)).ToUnixTimeSeconds();
        return new Reply(Stamp(start),Stamp(end),$"{start:MM.dd} - {end:MM.dd}",rows.ToArray());
    }

    public async Task<Calendar> Status(Guid user)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(9)).DateTime);
        await using var c = await db.Source.OpenConnectionAsync();
        await using var term = Database.Command(c, "SELECT starts_on FROM attendance_terms WHERE user_id=$1 AND $2 BETWEEN starts_on AND ends_on ORDER BY starts_on DESC LIMIT 1", user, today);
        DateOnly start;
        await using (var reader = await term.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync()) return new(0, new());
            start = reader.GetFieldValue<DateOnly>(0);
        }
        return new(today.DayNumber - start.DayNumber + 1, await ReadRows(c, user, start, today));
    }

    static async Task<List<Row>> ReadRows(Npgsql.NpgsqlConnection c, Guid user, DateOnly start, DateOnly today)
    {
        var rows = new List<Row>();
        await using (var query = Database.Command(c, "SELECT day_num,reward_type,reward_val,attendance,give_date,take_dt FROM user_attendance WHERE user_id=$1 AND term_start=$2 ORDER BY day_num", user,start))
        await using (var r = await query.ExecuteReaderAsync())
            while(await r.ReadAsync())
            {
                int day=r.GetInt32(0); bool attended=r.GetBoolean(3); var date=r.GetFieldValue<DateOnly>(4);
                rows.Add(new(day,r.GetInt32(1),r.GetInt32(2),attended?day:date>today?-1:0,attended?1:0,date,r.IsDBNull(5)?null:r.GetDateTime(5)));
            }
        return rows;
    }

}
