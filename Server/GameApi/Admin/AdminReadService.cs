using System.Globalization;
using System.Text.Json;

namespace MagicOnline.GameApi.Admin;

public sealed record AdminTable(string[] Columns, List<string[]> Rows, int Page = 1, bool HasNext = false);
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
        DateTime time => DateTime.SpecifyKind(time, DateTimeKind.Utc).AddHours(9).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        DateTimeOffset time => time.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        bool b => b ? "예" : "아니오",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "—"
    };
    public async Task<AdminDashboard> Dashboard()
    {
        await using var cmd = db.Source.CreateCommand("SELECT (SELECT count(*) FROM users),(SELECT count(*) FROM users WHERE created_at >= date_trunc('day',now() AT TIME ZONE 'Asia/Seoul') AT TIME ZONE 'Asia/Seoul'),(SELECT count(*) FROM matches WHERE phase=1),(SELECT count(*) FROM matches WHERE phase<>2 AND COALESCE((state->>'Disputed')::boolean,false)),(SELECT count(*) FROM settlement_receipts),now()");
        await using var r = await cmd.ExecuteReaderAsync(); await r.ReadAsync();
        return new(r.GetInt64(0), r.GetInt64(1), r.GetInt64(2), r.GetInt64(3), r.GetInt64(4), Format(r.GetValue(5)));
    }
    public Task<AdminTable> Members(string? q, string? state, int page)
    {
        var condition = state is "disabled" or "active" ? state : "";
        return Table(new[] { "회원 UUID", "로그인 ID", "닉네임", "상태", "Coin", "Jem", "가입일 (KST)" },
            "SELECT u.id,u.login_id,u.nickname,CASE WHEN u.disabled THEN '제한' ELSE '정상' END,w.coin::text,w.jem::text,u.created_at FROM users u JOIN wallets w ON w.user_id=u.id WHERE ($1='' OR position(lower($1) in lower(u.id::text||' '||u.login_id||' '||u.nickname))>0) AND ($2='' OR ($2='disabled' AND u.disabled) OR ($2='active' AND NOT u.disabled)) ORDER BY u.created_at DESC,u.id LIMIT 31 OFFSET $3", new object[] { Query(q), condition, (Page(page) - 1) * Size }, page, true);
    }
    public async Task<AdminMember> Member(Guid id)
    {
        var profile = await Table(new[] { "회원 UUID", "로그인 ID", "닉네임", "상태", "레벨", "Coin", "Jem", "가입일 (KST)" }, "SELECT u.id,u.login_id,u.nickname,CASE WHEN u.disabled THEN '제한' ELSE '정상' END,u.level,w.coin::text,w.jem::text,u.created_at FROM users u JOIN wallets w ON w.user_id=u.id WHERE u.id=$1", new object[] { id });
        if (profile.Rows.Count == 0) throw new ApiFault(404, "MemberNotFound");
        var stats = await Table(new[] { "4구 승", "4구 패", "4구 무", "3구 승", "3구 패", "3구 무" }, "SELECT ball_4_win,ball_4_lose,ball_4_draw,ball_3_win,ball_3_lose,ball_3_draw FROM user_stats WHERE user_id=$1", new object[] { id });
        return new(profile, stats, await Matches(id.ToString(), null, 1), await Ledger(id.ToString(), null, 1));
    }
    const string MatchSelect = "SELECT m.id,COALESCE(m.state->'Registration'->>'Ball','—'),a.login_id,b.login_id,CASE WHEN COALESCE((m.state->>'Disputed')::boolean,false) THEN '결과 불일치' WHEN m.phase=0 THEN '등록/준비' WHEN m.phase=1 THEN '진행 중' ELSE '정산 완료' END,COALESCE(r.data->>'Reason','—'),r.settled_at FROM matches m JOIN users a ON a.id=m.user_a JOIN users b ON b.id=m.user_b LEFT JOIN settlement_receipts r ON r.match_id=m.id ";
    public Task<AdminTable> Matches(string? q, string? state, int page)
    {
        string filter = state is "playing" or "disputed" or "applied" or "registered" ? state : "";
        return Table(new[] { "경기 ID", "종목", "참가자 A", "참가자 B", "상태", "종료 사유", "정산 시각 (KST)" }, MatchSelect +
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
        string currency = state is "coin" or "jem" ? state : "";
        return Table(new[] { "원장 ID", "회원 UUID", "로그인 ID", "요청 키", "Coin 변동", "Jem 변동", "기록 시각 (KST)" },
            "SELECT l.id::text,l.user_id,u.login_id,l.request_key,l.coin_delta::text,l.jem_delta::text,l.created_at FROM wallet_ledger l JOIN users u ON u.id=l.user_id WHERE ($1='' OR position(lower($1) in lower(l.user_id::text||' '||u.login_id||' '||u.nickname||' '||l.request_key))>0) AND ($2='' OR ($2='coin' AND l.coin_delta<>0) OR ($2='jem' AND l.jem_delta<>0)) ORDER BY l.id DESC LIMIT 31 OFFSET $3", new object[] { Query(q), currency, (Page(page) - 1) * Size }, page, true);
    }
    public async Task<AdminTable> Chart(string name)
    {
        if (name is not ("SkillMatch" or "Attendance" or "CommonCode")) name = "SkillMatch";
        await using var cmd = db.Source.CreateCommand("SELECT rows::text FROM charts WHERE name=$1"); cmd.Parameters.AddWithValue(name);
        var json = await cmd.ExecuteScalarAsync() as string ?? "[]";
        using var doc = JsonDocument.Parse(json);
        var list = doc.RootElement.EnumerateArray().ToArray();
        var columns = list.SelectMany(row => row.EnumerateObject().Select(p => p.Name)).Distinct().ToArray();
        return new(columns, list.Select(row => columns.Select(key => row.TryGetProperty(key, out var value) ? value.ToString() : "—").ToArray()).ToList());
    }
    public Task<AdminTable> Audit(int page) => Table(new[] { "기록 ID", "관리자", "작업", "대상", "변경 전", "변경 후", "사유", "요청 ID", "시각 (KST)" },
        "SELECT l.id::text,COALESCE(a.login_id,'시스템'),l.action,l.target,l.before_value,l.after_value,l.reason,l.request_id,l.created_at FROM admin_audit_logs l LEFT JOIN admin_users a ON a.id=l.admin_id ORDER BY l.id DESC LIMIT 31 OFFSET $1", new object[] { (Page(page) - 1) * Size }, page, true);
    public Task<AdminTable> Admins() => Table(new[] { "관리자 ID", "역할", "상태", "생성일 (KST)" }, "SELECT login_id,role,CASE WHEN disabled THEN '비활성' ELSE '활성' END,created_at FROM admin_users ORDER BY created_at LIMIT 100", Array.Empty<object>());
    public Task<AdminTable> WalletCheck(Guid id) => Table(new[] { "Coin 잔액", "Coin 원장 합계", "Jem 잔액", "Jem 원장 합계" },
        "SELECT w.coin::text,COALESCE(sum(l.coin_delta),0)::text,w.jem::text,COALESCE(sum(l.jem_delta),0)::text FROM wallets w LEFT JOIN wallet_ledger l ON l.user_id=w.user_id WHERE w.user_id=$1 GROUP BY w.user_id,w.coin,w.jem", new object[] { id });
}
