using System.Security.Claims;

namespace MagicOnline.GameApi;

public sealed record ShopEquipmentRequest(string Slot, long ProductId, bool Equipped);
public sealed record ShopEquipmentResult(string Slot, long ProductId, bool Equipped);

public sealed class ShopEquipment(Database db)
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/me/equipment", (ClaimsPrincipal user, ShopEquipmentRequest input, ShopEquipment equipment) =>
            equipment.Set(Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!), input)).RequireAuthorization();
    }

    public async Task<ShopEquipmentResult> Set(Guid user, ShopEquipmentRequest input)
    {
        if (input.Slot is not ("Cue" or "Character" or "MessagePack") || input.ProductId <= 0)
            throw new ApiFault(400, "EquipmentInvalidRequest");
        await using var c = await db.Source.OpenConnectionAsync();
        await using var tx = await c.BeginTransactionAsync();
        // Same per-user lock as purchases. Explicit desired state makes retrying safe.
        await using (var gate = Database.Command(c, "SELECT user_id FROM wallets WHERE user_id=$1 FOR UPDATE", user))
            if (await gate.ExecuteScalarAsync() == null) throw new ApiFault(404, "UserNotFound");
        await using (var owned = Database.Command(c, """
            SELECT p.kind FROM shop_user_inventory i JOIN shop_products p ON p.id=i.product_id
            WHERE i.user_id=$1 AND i.product_id=$2 FOR SHARE OF i,p
            """, user, input.ProductId))
        {
            var kind = (string?)await owned.ExecuteScalarAsync();
            if (kind == null) throw new ApiFault(403, "EquipmentNotOwned");
            if (kind != input.Slot) throw new ApiFault(400, "EquipmentKindMismatch");
        }
        string sql = input.Slot == "MessagePack"
            ? input.Equipped
                ? "INSERT INTO shop_user_selected_message_packs(user_id,product_id) VALUES($1,$2) ON CONFLICT DO NOTHING"
                : "DELETE FROM shop_user_selected_message_packs WHERE user_id=$1 AND product_id=$2"
            : input.Equipped
                ? "INSERT INTO shop_user_equipment(user_id,product_id,slot) VALUES($1,$2,$3) ON CONFLICT(user_id,slot) DO UPDATE SET product_id=excluded.product_id"
                : "DELETE FROM shop_user_equipment WHERE user_id=$1 AND product_id=$2 AND slot=$3";
        await using (var save = input.Slot == "MessagePack"
            ? Database.Command(c, sql, user, input.ProductId)
            : Database.Command(c, sql, user, input.ProductId, input.Slot))
            await save.ExecuteNonQueryAsync();
        await tx.CommitAsync();
        return new(input.Slot, input.ProductId, input.Equipped);
    }
}
