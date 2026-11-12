using System.Security.Claims;
using System.Text.Json;
using Assets.Scripts.Prack.BackSys.GameData;
using Npgsql;

namespace MagicOnline.GameApi;

public sealed class MatchEquipment(Database db)
{
    public sealed record Opponent(string OpponentUserId);
    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/matches/{id}/equipment").RequireAuthorization();
        group.MapGet("", (string id, ClaimsPrincipal user, MatchEquipment service) => service.Read(id, Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!)));
        // Friendly matches have no settlement registration but use the same immutable equipment contract.
        group.MapPost("", (string id, Opponent input, ClaimsPrincipal user, MatchEquipment service) =>
            Guid.TryParse(input.OpponentUserId, out var opponent) ? service.Freeze(id, Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!), opponent, true) : throw new ApiFault(400, "InvalidOpponent"));
    }
    public async Task<MatchEquipmentSnapshot> Read(string id, Guid caller)
    {
        await using var cmd = db.Source.CreateCommand("SELECT data::text FROM match_equipment_snapshots WHERE match_id=$1 AND (user_a=$2 OR user_b=$2)");
        cmd.Parameters.AddWithValue(id); cmd.Parameters.AddWithValue(caller);
        return JsonSerializer.Deserialize<MatchEquipmentSnapshot>((string?)await cmd.ExecuteScalarAsync() ?? throw new ApiFault(404, "MatchEquipmentNotFound"), Database.Json)!;
    }
    public async Task<MatchEquipmentSnapshot> Freeze(string id, Guid a, Guid b, bool friendly = false)
    {
        if (!Guid.TryParseExact(id, "N", out _) || a == b) throw new ApiFault(400, "InvalidMatch");
        await using var c = await db.Source.OpenConnectionAsync();
        await using var tx = await c.BeginTransactionAsync();
        async Task<MatchEquipmentSnapshot?> Existing()
        {
            await using var cmd = Database.Command(c, "SELECT data::text,user_a,user_b,friendly FROM match_equipment_snapshots WHERE match_id=$1", id);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return null;
            if (r.GetGuid(1) != a || r.GetGuid(2) != b || r.GetBoolean(3) != friendly) throw new ApiFault(409, "MatchEquipmentConflict");
            return JsonSerializer.Deserialize<MatchEquipmentSnapshot>(r.GetString(0), Database.Json)!;
        }
        var existing = await Existing();
        if (existing != null) return existing;
        // Equipment changes lock wallets first too. Stable UUID order prevents cross-match deadlocks.
        await using (var gate = Database.Command(c, "SELECT user_id FROM wallets WHERE user_id IN ($1,$2) ORDER BY user_id FOR SHARE", a, b))
        await using (var r = await gate.ExecuteReaderAsync())
        { int count = 0; while (await r.ReadAsync()) count++; if (count != 2) throw new ApiFault(404, "UserNotFound"); }
        await using (var products = Database.Command(c, "SELECT p.id FROM shop_products p WHERE EXISTS(SELECT FROM shop_user_inventory i WHERE i.product_id=p.id AND i.user_id IN ($1,$2)) ORDER BY p.id FOR SHARE", a, b))
        await using (var r = await products.ExecuteReaderAsync()) while (await r.ReadAsync()) { }
        var snapshot = new MatchEquipmentSnapshot { MatchId = id, Players = new[] { await Profile(c, a), await Profile(c, b) } };
        await using (var save = Database.Command(c, "INSERT INTO match_equipment_snapshots(match_id,user_a,user_b,data,friendly) VALUES($1,$2,$3,$4::jsonb,$5) ON CONFLICT DO NOTHING", id, a, b, JsonSerializer.Serialize(snapshot, Database.Json), friendly)) await save.ExecuteNonQueryAsync();
        var result = (await Existing())!;
        await tx.CommitAsync();
        return result;
    }
    static async Task<MatchEquipmentProfile> Profile(NpgsqlConnection c, Guid user)
    {
        await using var cmd = Database.Command(c, ShopCatalog.Query, user, true);
        using var doc = JsonDocument.Parse((string)(await cmd.ExecuteScalarAsync())!);
        var profile = new MatchEquipmentProfile { UserId = user.ToString() };
        var messages = new List<string>();
        foreach (var p in doc.RootElement.GetProperty("Products").EnumerateArray().Where(p => p.GetProperty("Equipped").GetBoolean()))
        {
            switch (p.GetProperty("Kind").GetString())
            {
                case "Cue":
                    profile.CueId = p.GetProperty("Id").GetInt64(); profile.Power = p.GetProperty("Power").GetInt32();
                    profile.FollowThrough = p.GetProperty("FollowThrough").GetInt32(); profile.Tip = p.GetProperty("Tip").GetInt32(); profile.ShotSeconds = p.GetProperty("ShotSeconds").GetInt32(); break;
                case "Character":
                    profile.CharacterId = p.GetProperty("Id").GetInt64();
                    profile.Emotions = JsonSerializer.Deserialize<MatchEquipmentEmotion[]>(p.GetProperty("Emotions").GetRawText(), Database.Json)!; break;
                case "MessagePack":
                    messages.AddRange(p.GetProperty("Messages").EnumerateArray().Select(m => m.GetString()!)); break;
            }
        }
        profile.Messages = messages.Distinct(StringComparer.Ordinal).ToArray();
        return profile;
    }
}
