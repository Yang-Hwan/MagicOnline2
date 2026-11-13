using System.Globalization;
using System.Text.Json;

namespace MagicOnline.GameApi.Admin;

public sealed record AdminTable(string[] Columns, List<string[]> Rows, int Page = 1, bool HasNext = false)
{
    public int[]? SourceRows { get; init; }
    public bool[]? GroupRows { get; init; }
}
public sealed record AdminMember(AdminTable Profile, AdminTable Stats, AdminTable Matches, AdminTable Ledger);
public sealed record AdminMatch(AdminTable Summary, AdminTable Ledger);
public sealed record AdminDashboard(long Members, long NewMembers, long Playing, long Disputed, long Receipts, string DatabaseTime);

public sealed class AdminReadService(Database db)
{
    const int Size = 30;
    static int Page(int page) => Math.Clamp(page, 1, 10000);
    static string Query(string? q) => (q ?? "").Trim()[..Math.Min((q ?? "").Trim().Length, 128)];
    async Task<AdminTable> Table(string[] columns, string sql, object[] parameters, int page = 1, bool paginate = false)
    {
        var rows = new List<string[]>();
        await using var cmd = db.Source.CreateCommand(sql);
        foreach (var p in parameters) cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = p });
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var row = new string[r.FieldCount];
            for (int i = 0; i < row.Length; i++) row[i] = r.IsDBNull(i) ? "—" : Format(r.GetValue(i));
            rows.Add(row);
        }
        bool next = paginate && rows.Count > Size;
        if (next) rows.RemoveAt(Size);
        return new(columns, rows, Page(page), next);
    }
    static string Format(object value) => value switch {
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTime time => DateTime.SpecifyKind(time, DateTimeKind.Utc).AddHours(9).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        DateTimeOffset time => time.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        bool b => b ? "예" : "아니오",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "—"
    };
    public async Task<AdminDashboard> Dashboard()
    {
        await using var cmd = db.Source.CreateCommand("SELECT (SELECT count(*) FROM users WHERE deleted_at IS NULL),(SELECT count(*) FROM users WHERE deleted_at IS NULL AND created_at >= date_trunc('day',now() AT TIME ZONE 'Asia/Seoul') AT TIME ZONE 'Asia/Seoul'),(SELECT count(*) FROM matches WHERE phase=1),(SELECT count(*) FROM matches WHERE phase<>2 AND COALESCE((state->>'Disputed')::boolean,false)),(SELECT count(*) FROM settlement_receipts),now()");
        await using var r = await cmd.ExecuteReaderAsync(); await r.ReadAsync();
        return new(r.GetInt64(0), r.GetInt64(1), r.GetInt64(2), r.GetInt64(3), r.GetInt64(4), Format(r.GetValue(5)));
    }
    public Task<AdminTable> Members(string? q, string? state, int page)
    {
        var condition = state is "disabled" or "active" ? state : "";
        return Table(new[] { "회원 UUID", "로그인 ID", "닉네임", "상태", "Coin", "Jem", "가입일 (KST)" },
            "SELECT u.id,u.login_id,u.nickname,CASE WHEN u.disabled THEN '제한' ELSE '정상' END,w.coin::text,w.jem::text,u.created_at FROM users u JOIN wallets w ON w.user_id=u.id WHERE u.deleted_at IS NULL AND ($1='' OR position(lower($1) in lower(u.id::text||' '||u.login_id||' '||u.nickname))>0) AND ($2='' OR ($2='disabled' AND u.disabled) OR ($2='active' AND NOT u.disabled)) ORDER BY u.created_at DESC,u.id LIMIT 31 OFFSET $3", new object[] { Query(q), condition, (Page(page) - 1) * Size }, page, true);
    }
    public async Task<AdminMember> Member(Guid id)
    {
        var profile = await Table(new[] { "회원 UUID", "로그인 ID", "닉네임", "상태", "레벨", "레벨플레이 수 (LvPlay)", "Coin", "Jem", "Cash", "Gold", "가입일 (KST)" }, "SELECT u.id,u.login_id,u.nickname,CASE WHEN u.disabled THEN '제한' ELSE '정상' END,u.level,u.level_progress,w.coin::text,w.jem::text,w.cash::text,w.gold::text,u.created_at FROM users u JOIN wallets w ON w.user_id=u.id WHERE u.id=$1 AND u.deleted_at IS NULL", new object[] { id });
        if (profile.Rows.Count == 0) throw new ApiFault(404, "MemberNotFound");
        var stats = await Table(new[] { "4구 승", "4구 패", "4구 무", "3구 승", "3구 패", "3구 무" }, "SELECT ball_4_win,ball_4_lose,ball_4_draw,ball_3_win,ball_3_lose,ball_3_draw FROM user_stats WHERE user_id=$1", new object[] { id });
        return new(profile, stats, await Matches(id.ToString(), null, 1), await Ledger(id.ToString(), null, 1));
    }
    const string MatchSelect = "SELECT m.id,COALESCE(m.state->'Registration'->>'HallId','—'),COALESCE(m.state->'Registration'->>'Ball','—'),a.login_id,b.login_id,CASE WHEN COALESCE((m.state->>'Disputed')::boolean,false) THEN '결과 불일치' WHEN m.phase=0 THEN '등록/준비' WHEN m.phase=1 THEN '진행 중' ELSE '정산 완료' END,COALESCE(r.data->>'Reason','—'),r.settled_at FROM matches m JOIN users a ON a.id=m.user_a JOIN users b ON b.id=m.user_b LEFT JOIN settlement_receipts r ON r.match_id=m.id ";
    public Task<AdminTable> Matches(string? q, string? state, int page)
    {
        string filter = state is "playing" or "disputed" or "applied" or "registered" ? state : "";
        return Table(new[] { "경기 ID", "경기장", "종목", "참가자 A", "참가자 B", "상태", "종료 사유", "정산 시각 (KST)" }, MatchSelect +
            "WHERE ($1='' OR position(lower($1) in lower(m.id||' '||a.login_id||' '||a.nickname||' '||b.login_id||' '||b.nickname||' '||m.user_a::text||' '||m.user_b::text))>0) AND ($2='' OR ($2='playing' AND m.phase=1) OR ($2='applied' AND m.phase=2) OR ($2='registered' AND m.phase=0) OR ($2='disputed' AND COALESCE((m.state->>'Disputed')::boolean,false))) ORDER BY COALESCE((m.state->>'RegisteredAt')::bigint,0) DESC,m.id LIMIT 31 OFFSET $3", new object[] { Query(q), filter, (Page(page) - 1) * Size }, page, true);
    }
    public async Task<AdminMatch> Match(string id)
    {
        if (id.Length > 64) throw new ApiFault(404, "MatchNotFound");
        var summary = await Table(new[] { "경기 ID", "A UUID", "B UUID", "규칙 계약", "서버 차트 스냅샷", "A 결과 보고", "B 결과 보고", "A 준비", "B 준비", "A 최근 신호 (UTC ms)", "B 최근 신호 (UTC ms)", "최종 영수증" },
            "SELECT m.id,m.user_a,m.user_b,m.state->'Registration'->'Rules',m.state->'Registration'->>'RulesSnapshot',m.state->'ReportA',m.state->'ReportB',m.state->>'ReadyA',m.state->>'ReadyB',m.state->>'LastSeenA',m.state->>'LastSeenB',r.data FROM matches m LEFT JOIN settlement_receipts r ON r.match_id=m.id WHERE m.id=$1", new object[] { id });
        if (summary.Rows.Count == 0) throw new ApiFault(404, "MatchNotFound");
        return new(summary, await Ledger(id, null, 1));
    }
    public Task<AdminTable> Ledger(string? q, string? state, int page)
    {
        string currency = state is "coin" or "jem" or "cash" or "gold" ? state : "";
        return Table(new[] { "원장 ID", "회원 UUID", "로그인 ID", "요청 키", "Coin 변동", "Jem 변동", "Cash 변동", "Gold 변동", "기록 시각 (KST)" },
            "SELECT l.id::text,l.user_id,u.login_id,l.request_key,l.coin_delta::text,l.jem_delta::text,l.cash_delta::text,l.gold_delta::text,l.created_at FROM wallet_ledger l JOIN users u ON u.id=l.user_id WHERE ($1='' OR position(lower($1) in lower(l.user_id::text||' '||u.login_id||' '||u.nickname||' '||l.request_key))>0) AND ($2='' OR ($2='coin' AND l.coin_delta<>0) OR ($2='jem' AND l.jem_delta<>0) OR ($2='cash' AND l.cash_delta<>0) OR ($2='gold' AND l.gold_delta<>0)) ORDER BY l.id DESC LIMIT 31 OFFSET $3", new object[] { Query(q), currency, (Page(page) - 1) * Size }, page, true);
    }
    public async Task<AdminTable> Chart(string name)
    {
        if (name is not ("SkillMatch" or "Attendance" or "CommonCode" or "PostReward")) name = "SkillMatch";
        await using var cmd = db.Source.CreateCommand("SELECT rows::text FROM charts WHERE name=$1"); cmd.Parameters.AddWithValue(name);
        var json = await cmd.ExecuteScalarAsync() as string ?? "[]";
        if (name=="PostReward")
        {
            await using var codes=db.Source.CreateCommand("SELECT rows::text FROM charts WHERE name='CommonCode'");
            var common=System.Text.Json.Nodes.JsonNode.Parse((string)(await codes.ExecuteScalarAsync())!)!.AsArray();
            var types=PostRewardChart.Codes(common,"PostRewardType");
            var currencies=PostRewardChart.Codes(common,"Wealth");
            var rewards=System.Text.Json.Nodes.JsonNode.Parse(json)!.AsArray();
            string Label(System.Text.Json.Nodes.JsonNode? value, Dictionary<long,string> labels) =>
                value==null ? "" : labels.GetValueOrDefault((long)value,value.ToString());
            var display=rewards.Select(r => new[] {
                r!["RewardId"]!.ToString(),Label(r["TypeCode"],types),r["RangeMin"]?.ToString()??"",
                r["RangeMax"]?.ToString()??"",r["Message"]!.ToString(),r["KeepDays"]!.ToString(),
                Label(r["CurrencyCode"],currencies),r["RewardVal"]?.ToString()??"" }).ToList();
            return new(PostRewardChart.Fields.Select(f=>f.Label).ToArray(),display);
        }
        return ChartTable(name, json);
    }
    public static AdminTable ChartTable(string name, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var rows = doc.RootElement.EnumerateArray().Select((value, index) => (Value: value, Index: index)).ToArray();
        string Value(JsonElement row, string key) => row.TryGetProperty(key, out var value) ? value.ToString() : "";
        string Group(JsonElement row) => (row.TryGetProperty("Grp", out var grp) ? grp.ToString() : Value(row, "GrpCd")).Trim();
        var columns = rows.SelectMany(row => row.Value.EnumerateObject().Select(p => p.Name)).Distinct().ToArray();
        if (name == "CommonCode")
        {
            columns = FieldsWithOrd(columns);
            int Order(JsonElement row) => int.TryParse(Value(row, "Ord"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var order) ? order : 0;
            var sorted = new List<(JsonElement Value, int Index)>();
            var emitted = new HashSet<int>();
            var children = rows.Where(row => Group(row.Value) != "#")
                .ToLookup(row => Group(row.Value), StringComparer.Ordinal);
            // Header Ord orders groups; child Ord only orders rows within that group.
            foreach (var header in rows.Where(row => Group(row.Value) == "#").OrderBy(row => Order(row.Value)))
            {
                sorted.Add(header);
                emitted.Add(header.Index);
                foreach (var child in children[Value(header.Value, "ComCd").Trim()].OrderBy(row => Order(row.Value)))
                    if (emitted.Add(child.Index)) sorted.Add(child);
            }
            // Keep legacy groups without a header visible at the end.
            sorted.AddRange(rows.Where(row => !emitted.Contains(row.Index))
                .OrderBy(row => Group(row.Value), StringComparer.Ordinal).ThenBy(row => Order(row.Value)));
            rows = sorted.ToArray(); // Stable sorting retains original order for equal Ord values.
        }
        return new(columns, rows.Select(row => columns.Select(key => row.Value.TryGetProperty(key, out var value) ? value.ToString() : key == "Ord" ? "0" : "—").ToArray()).ToList())
        {
            SourceRows = rows.Select(row => row.Index).ToArray(),
            GroupRows = rows.Select(row => name == "CommonCode" && Group(row.Value) == "#").ToArray()
        };
    }
    static string[] FieldsWithOrd(string[] columns) => AdminChartService.Fields("CommonCode").Select(f => f.Key).Concat(columns).Distinct().ToArray();
    public Task<AdminTable> Audit(int page) => Table(new[] { "기록 ID", "관리자", "작업", "대상", "변경 전", "변경 후", "사유", "요청 ID", "시각 (KST)" },
        "SELECT l.id::text,COALESCE(a.login_id,'시스템'),l.action,l.target,l.before_value,l.after_value,l.reason,l.request_id,l.created_at FROM admin_audit_logs l LEFT JOIN admin_users a ON a.id=l.admin_id ORDER BY l.id DESC LIMIT 31 OFFSET $1", new object[] { (Page(page) - 1) * Size }, page, true);
    public Task<AdminTable> Admins() => Table(new[] { "관리자 ID", "역할", "상태", "생성일 (KST)" }, "SELECT login_id,role,CASE WHEN disabled THEN '비활성' ELSE '활성' END,created_at FROM admin_users ORDER BY created_at LIMIT 100", Array.Empty<object>());
    public Task<AdminTable> MemberMail(Guid id,int page) => Table(
        new[]{"우편 번호","종류","내용","발송일시 (KST)","만료일시 (KST)","수령일시 (KST)","재화","보상값","상태"},
        "SELECT post_id,type_name,message,sent_at,expires_at,claimed_at,currency_name,reward_val,CASE WHEN claimed_at IS NOT NULL THEN '수령 완료' WHEN now()>=expires_at THEN '기간 만료' ELSE '미수령' END FROM user_post_reward WHERE user_id=$1 ORDER BY sent_at DESC,post_id DESC LIMIT 31 OFFSET $2",
        new object[]{id,(Page(page)-1)*Size},page,true);

    public Task<AdminTable> AttendanceHistory(Guid id, int page) => Table(
        new[] { "출석 기간 시작일", "일차", "보상재화", "보상값", "출석 예정일", "출석여부", "보상수령일자" },
        "SELECT term_start,day_num,CASE reward_type WHEN 1 THEN '코인' WHEN 2 THEN '보석' ELSE reward_type::text END,reward_val,give_date,CASE WHEN attendance THEN '출석' ELSE '미출석' END,COALESCE(to_char(take_dt AT TIME ZONE 'Asia/Seoul','YY.MM.DD HH24:MI'),'') FROM user_attendance WHERE user_id=$1 ORDER BY term_start DESC,day_num LIMIT 31 OFFSET $2",
        new object[] { id, (Page(page)-1)*Size },page,true);

    public Task<AdminTable> BustHistory(Guid id, int page) => Table(
        new[] { "기록 ID", "지원 일자 (KST)", "당일 지원 차수", "지급 코인", "지급 시각 (KST)" },
        "SELECT id::text,split_part(request_key,':',2),split_part(request_key,':',3),coin_delta::text,created_at FROM wallet_ledger WHERE user_id=$1 AND request_key LIKE 'Bust:%' ORDER BY created_at DESC,id DESC LIMIT 31 OFFSET $2",
        new object[] { id, (Page(page) - 1) * Size }, page, true);

    public Task<AdminTable> WalletCheck(Guid id) => Table(new[] { "Coin 잔액", "Coin 원장 합계", "Jem 잔액", "Jem 원장 합계" },
        "SELECT w.coin::text,COALESCE(sum(l.coin_delta),0)::text,w.jem::text,COALESCE(sum(l.jem_delta),0)::text FROM wallets w LEFT JOIN wallet_ledger l ON l.user_id=w.user_id WHERE w.user_id=$1 GROUP BY w.user_id,w.coin,w.jem", new object[] { id });
}
