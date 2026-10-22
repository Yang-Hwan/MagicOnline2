using System.Text.Json;
using MagicOnline.MatchSettlement;
using Assets.Scripts.Prack.BackSys.ChartData;
using Assets.Scripts.Prack.BackSys.GameData;

namespace MagicOnline.GameApi;

public sealed record MatchRegistration(string RequestId, string OpponentUserId, int HallId, MatchRulesContract Rules);
public sealed record MatchReady(MatchRulesContract Rules);
public sealed class Matches(Database db, GameData data, SettlementService settlement)
{
    public async Task<string> Register(Guid caller, MatchRegistration input)
    {
        RequireProtocol(input.Rules);
        if (!Guid.TryParseExact(input.RequestId, "N", out _) || !Guid.TryParse(input.OpponentUserId, out var opponent) || opponent == caller)
            throw new ApiFault(400, "InvalidMatch");
        // An idempotent retry compares the frozen registration, not a later chart revision.
        await using (var existing = db.Source.CreateCommand("SELECT state::text FROM matches WHERE id=$1"))
        {
            existing.Parameters.AddWithValue(input.RequestId);
            if (await existing.ExecuteScalarAsync() is string json)
            {
                var original = JsonSerializer.Deserialize<RegisteredMatch>(json, Database.Json)!.Registration;
                if (original.UserA != caller.ToString() || original.UserB != opponent.ToString() || original.HallId != input.HallId)
                    throw new ApiFault(409, "MatchIdConflict");
                if (original.Rules == null || !original.Rules.SameAs(input.Rules)) throw new ApiFault(409, "MatchRulesMismatch");
                return input.RequestId;
            }
        }
        var chart = await data.Chart("SkillMatch");
        var hall = chart.EnumerateArray().FirstOrDefault(h => h.GetProperty("HallIdx").GetInt32() == input.HallId);
        if (hall.ValueKind == JsonValueKind.Undefined) throw new ApiFault(400, "InvalidHall");
        if (input.Rules.Runtime != MatchRulesContract.Pavilion && input.Rules.Runtime != MatchRulesContract.Practice)
            throw new ApiFault(409, "UnsupportedMatchRuntime");
        var settings = JsonSerializer.Deserialize<SkillMatchData>(hall.GetRawText(), Database.Json)!;
        var expected = MatchRulesContract.FromHall(settings, input.Rules.Runtime);
        if (!expected.SameAs(input.Rules)) throw new ApiFault(409, "MatchRulesMismatch");
        if (expected.MatchSeconds < 1 || expected.TargetScore < 1 || expected.Cushions < 0 || expected.Cushions > 3 ||
            (expected.Ball == 4 && expected.Cushions != 0) || (expected.Runtime == MatchRulesContract.Practice && expected.FinishMission != 0))
            throw new ApiFault(409, "UnsupportedMatchRules");
        var a = await data.Profile(caller); var b = await data.Profile(opponent);
        settlement.Register(new VerifiedRegistration(input.RequestId, caller.ToString(), opponent.ToString(), a.NickName, b.NickName,
            hall.GetProperty("MatchBall").GetInt32(), input.HallId, hall.GetProperty("PrizeCoin").GetInt64(), CoinPolicy.EntryPool, hall.GetRawText(), expected));
        return input.RequestId;
    }
    static void RequireProtocol(MatchRulesContract? rules)
    {
        if (rules == null || rules.Version != MatchRulesContract.CurrentVersion) throw new ApiFault(409, "MatchProtocolMismatch");
    }
    public bool Ready(string id, Guid caller, MatchReady input)
    {
        RequireProtocol(input.Rules);
        try { return settlement.Ready(id, caller.ToString(), input.Rules); }
        catch (MatchRulesMismatchException) { throw new ApiFault(409, "MatchRulesMismatch"); }
    }
    public async Task<object> Status(string id, Guid caller)
    {
        await using var cmd = db.Source.CreateCommand("SELECT state::text,(SELECT data::text FROM settlement_receipts WHERE match_id=m.id) FROM matches m WHERE id=$1 AND (user_a=$2 OR user_b=$2)");
        cmd.Parameters.AddWithValue(id); cmd.Parameters.AddWithValue(caller);
        await using var r = await cmd.ExecuteReaderAsync(); if (!await r.ReadAsync()) throw new ApiFault(404, "MatchNotFound");
        var state = JsonSerializer.Deserialize<RegisteredMatch>(r.GetString(0), Database.Json)!;
        var receipt = r.IsDBNull(1) ? null : JsonSerializer.Deserialize<SettlementReceipt>(r.GetString(1), Database.Json);
        return new { MatchId = id, Phase = state.Phase.ToString(), state.Disputed, state.ReadyA, state.ReadyB, Rules = state.Registration.Rules, Receipt = receipt };
    }
    public async Task<object[]> History(Guid caller)
    {
        await using var cmd = db.Source.CreateCommand("SELECT data::text FROM user_match_history WHERE user_id=$1 AND expires_at>now() ORDER BY expires_at DESC LIMIT 100");
        cmd.Parameters.AddWithValue(caller); await using var r = await cmd.ExecuteReaderAsync();
        var rows = new List<object>(); while (await r.ReadAsync()) rows.Add(JsonSerializer.Deserialize<JsonElement>(r.GetString(0))); return rows.ToArray();
    }
}

public sealed class MatchMaintenance(Database db, SettlementService settlement, ILogger<MatchMaintenance> logger) : BackgroundService
{
    string cursor = "";
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var pending = new List<RegisteredMatch>();
                await using (var cmd = db.Source.CreateCommand("SELECT state::text FROM matches WHERE phase<>2 AND NOT (state->>'Disputed')::boolean AND id>$1 ORDER BY id LIMIT 500"))
                {
                    cmd.Parameters.AddWithValue(cursor);
                await using (var r = await cmd.ExecuteReaderAsync(stoppingToken))
                    while (await r.ReadAsync(stoppingToken)) pending.Add(JsonSerializer.Deserialize<RegisteredMatch>(r.GetString(0), Database.Json)!);
                }
                cursor = pending.Count == 500 ? pending[^1].Registration.MatchId : "";
                foreach (var m in pending)
                {
                    try
                    {
                        if (m.Phase == ServerMatchPhase.Playing) settlement.EvaluateDisconnect(m.Registration.MatchId, m.Registration.UserA);
                        else if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - m.RegisteredAt > 120000)
                            settlement.Surrender(m.Registration.MatchId, m.Registration.UserA);
                    }
                    catch (Exception e) { logger.LogWarning("Match maintenance deferred ({Type})", e.GetType().Name); }
                }
                // Bounded batches; permanent receipts and wallet ledgers are never deleted here.
                await using var cleanup = db.Source.CreateCommand("DELETE FROM user_match_history WHERE (match_id,user_id) IN (SELECT h.match_id,h.user_id FROM user_match_history h JOIN matches m ON m.id=h.match_id WHERE h.expires_at<now() AND m.phase=2 AND NOT (m.state->>'Disputed')::boolean LIMIT 500); DELETE FROM match_history WHERE match_id IN (SELECT h.match_id FROM match_history h JOIN matches m ON m.id=h.match_id WHERE h.expires_at<now() AND m.phase=2 AND NOT (m.state->>'Disputed')::boolean LIMIT 500);");
                await cleanup.ExecuteNonQueryAsync(stoppingToken);
                await using var scrub = db.Source.CreateCommand("UPDATE matches m SET state=jsonb_set((m.state-'ReportA'-'ReportB'),'{Registration}',(m.state->'Registration')-'NameA'-'NameB'-'RulesSnapshot'-'Rules') WHERE m.id IN (SELECT m2.id FROM matches m2 JOIN settlement_receipts r ON r.match_id=m2.id WHERE m2.phase=2 AND r.settled_at<now()-interval '6 months' AND m2.state->'Registration' ? 'RulesSnapshot' LIMIT 500)");
                await scrub.ExecuteNonQueryAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception e) { logger.LogError("Maintenance failed ({Type})", e.GetType().Name); }
        }
    }
}
