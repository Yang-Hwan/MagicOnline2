using System.Data;
using System.Text.Json;
using Assets.Scripts.Prack.BackSys;
using MagicOnline.MatchSettlement;
using Npgsql;

namespace MagicOnline.GameApi;

public sealed class PostgresSettlementStore(Database db) : ISettlementStore
{
    static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Database.Json);
    public void Create(RegisteredMatch match)
    {
        using var c = db.Source.OpenConnection(); using var tx = c.BeginTransaction();
        var r = match.Registration;
        using (var existing = Database.Command(c, "SELECT state::text FROM matches WHERE id=$1", r.MatchId))
        {
            if (existing.ExecuteScalar() is string json)
            {
                var old = JsonSerializer.Deserialize<RegisteredMatch>(json, Database.Json)!.Registration;
                if (old.UserA != r.UserA || old.UserB != r.UserB || old.HallId != r.HallId) throw new ApiFault(409, "MatchIdConflict");
                if ((old.Rules != null || r.Rules != null) && (old.Rules == null || !old.Rules.SameAs(r.Rules)))
                    throw new ApiFault(409, "MatchRulesMismatch");
                return;
            }
        }
        using (var add = Database.Command(c, "INSERT INTO matches(id,user_a,user_b,state) VALUES($1,$2,$3,$4::jsonb)", r.MatchId, Guid.Parse(r.UserA), Guid.Parse(r.UserB), Serialize(match))) add.ExecuteNonQuery();
        // Reserve only the caller's slot. An unaccepted invitation cannot block its recipient.
        foreach (var id in new[] { r.UserA, r.UserB }.Order(StringComparer.Ordinal))
        {
            using var player = Database.Command(c, "INSERT INTO match_players(match_id,user_id,active) VALUES($1,$2,$3)", r.MatchId, Guid.Parse(id), id == r.UserA);
            player.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public T Execute<T>(string matchId, Func<SettlementContext, T> operation)
    {
        // Match row lock + consistently ordered wallet locks cover different matches and reward writers.
        // A failed operation, constraint violation or process death rolls the entire transaction back.
        for (int attempt = 0; ; attempt++)
        {
            try { return ExecuteOnce(matchId, operation); }
            catch (PostgresException e) when (attempt < 2 && (e.SqlState == PostgresErrorCodes.DeadlockDetected || e.SqlState == PostgresErrorCodes.SerializationFailure)) { }
        }
    }
    T ExecuteOnce<T>(string matchId, Func<SettlementContext, T> operation)
    {
        using var c = db.Source.OpenConnection(); using var tx = c.BeginTransaction(IsolationLevel.ReadCommitted);
        RegisteredMatch match;
        using (var cmd = Database.Command(c, "SELECT state::text FROM matches WHERE id=$1 FOR UPDATE", matchId))
            match = JsonSerializer.Deserialize<RegisteredMatch>(cmd.ExecuteScalar() as string ?? throw new KeyNotFoundException(), Database.Json)!;
        var context = new SettlementContext { Match = match };
        using (var cmd = Database.Command(c, "SELECT data::text FROM settlement_receipts WHERE match_id=$1", matchId))
            if (cmd.ExecuteScalar() is string receipt) context.Receipt = JsonSerializer.Deserialize<SettlementReceipt>(receipt, Database.Json);
        var original = new Dictionary<string, UserMainData>();
        foreach (var user in new[] { match.Registration.UserA, match.Registration.UserB }.Order(StringComparer.Ordinal))
        {
            using (var cmd = Database.Command(c, "SELECT user_id FROM wallets WHERE user_id=$1 FOR UPDATE", Guid.Parse(user)))
                if (cmd.ExecuteScalar() == null) throw new KeyNotFoundException();
            using var query = Database.Command(c, GameData.UserSql + "WHERE u.id=$1", Guid.Parse(user));
            using var reader = query.ExecuteReader(); reader.Read();
            var data = GameData.ReadUser(reader);
            original[user] = JsonSerializer.Deserialize<UserMainData>(Serialize(data), Database.Json)!;
            if (user == match.Registration.UserA) context.UserA = data; else context.UserB = data;
        }
        var beforePhase = match.Phase; bool hadReceipt = context.Receipt != null;
        T result = operation(context);
        if (context.Receipt == null && match.ReadyB)
        {
            // This uniqueness check is in the same transaction as the entry debit.
            using var accept = Database.Command(c, "UPDATE match_players SET active=true WHERE match_id=$1 AND user_id=$2", matchId, Guid.Parse(match.Registration.UserB));
            accept.ExecuteNonQuery();
        }
        foreach (var data in new[] { context.UserA, context.UserB })
        {
            var before = original[data.InDate];
            using (var update = Database.Command(c, "UPDATE wallets SET coin=$2,jem=$3 WHERE user_id=$1", Guid.Parse(data.InDate), data.Coin, data.Jem)) update.ExecuteNonQuery();
            using (var update = Database.Command(c, "UPDATE user_stats SET ball_4_win=$2,ball_4_lose=$3,ball_4_draw=$4,ball_3_win=$5,ball_3_lose=$6,ball_3_draw=$7 WHERE user_id=$1", Guid.Parse(data.InDate), data.Ball_4_Win, data.Ball_4_Lose, data.Ball_4_Draw, data.Ball_3_Win, data.Ball_3_Lose, data.Ball_3_Draw)) update.ExecuteNonQuery();
            if (before.Coin != data.Coin || (!hadReceipt && context.Receipt != null) || beforePhase != match.Phase)
            {
                string key = matchId + (context.Receipt != null ? ":Settlement" : ":Entry");
                using var ledger = Database.Command(c, "INSERT INTO wallet_ledger(user_id,request_key,coin_delta) VALUES($1,$2,$3)", Guid.Parse(data.InDate), key, checked(data.Coin - before.Coin));
                ledger.ExecuteNonQuery();
            }
        }
        if (!hadReceipt && context.Receipt != null)
        {
            var receipt = context.Receipt;
            var detail = receipt.Match; var a = receipt.PlayerA; var b = receipt.PlayerB;
            // Permanent receipts contain no six-month detail payloads.
            receipt.Match = null; receipt.PlayerA = null; receipt.PlayerB = null;
            using (var save = Database.Command(c, "INSERT INTO settlement_receipts(match_id,data) VALUES($1,$2::jsonb)", matchId, Serialize(receipt))) save.ExecuteNonQuery();
            receipt.Match = detail; receipt.PlayerA = a; receipt.PlayerB = b;
            using (var save = Database.Command(c, "INSERT INTO match_history(match_id,data,expires_at) VALUES($1,$2::jsonb,$3)", matchId, Serialize(detail), DateTimeOffset.FromUnixTimeMilliseconds(detail.HistoryExpiresAt))) save.ExecuteNonQuery();
            foreach (var history in new[] { a, b })
            {
                using var save = Database.Command(c, "INSERT INTO user_match_history(match_id,user_id,data,expires_at) VALUES($1,$2,$3::jsonb,$4)", matchId, Guid.Parse(history.UserId), Serialize(history), DateTimeOffset.FromUnixTimeMilliseconds(history.HistoryExpiresAt));
                save.ExecuteNonQuery();
            }
            using var deactivate = Database.Command(c, "UPDATE match_players SET active=false WHERE match_id=$1", matchId); deactivate.ExecuteNonQuery();
        }
        using (var save = Database.Command(c, "UPDATE matches SET state=$2::jsonb,phase=$3 WHERE id=$1", matchId, Serialize(match), (int)match.Phase)) save.ExecuteNonQuery();
        tx.Commit(); return result;
    }
}

public sealed class ServerClock : IServerClock { public long UtcMilliseconds => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); }
