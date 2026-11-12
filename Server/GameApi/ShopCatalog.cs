using System.Security.Claims;

namespace MagicOnline.GameApi;

public static class ShopCatalog
{
    internal const string Query = """
                SELECT jsonb_build_object('Products',COALESCE(jsonb_agg(item ORDER BY sort_order,id),'[]'::jsonb))::text
                FROM (
                    SELECT p.id,p.sort_order,jsonb_build_object(
                        'Id',p.id,'Kind',p.kind,'Name',p.name,'Description',p.description,
                        'Thumbnail',COALESCE(p.thumbnail,''),'PriceGem',COALESCE(p.price_gem,0),
                        'Purchasable',p.purchasable,'Revision',p.revision,
                        'Owned',EXISTS(SELECT FROM shop_user_inventory WHERE user_id=$1 AND product_id=p.id),
                        'Equipped',EXISTS(SELECT FROM shop_user_equipment WHERE user_id=$1 AND product_id=p.id AND slot=p.kind)
                            OR EXISTS(SELECT FROM shop_user_selected_message_packs WHERE user_id=$1 AND product_id=p.id),
                        'GemAmount',COALESCE(g.amount,0),'GemBonus',COALESCE(g.bonus,0),
                        'Visual',COALESCE(c.visual,''),'Power',COALESCE(c.power_pct,0),
                        'FollowThrough',COALESCE(c.follow_pct,0),'Tip',COALESCE(c.tip_pct,0),
                        'ShotSeconds',COALESCE(c.shot_seconds,0),'Personality',COALESCE(m.personality,''),
                        'Emotions',COALESCE((SELECT jsonb_agg(jsonb_build_object('Code',emotion,'Asset',asset_key) ORDER BY emotion)
                            FROM shop_character_emotions WHERE product_id=p.id),'[]'::jsonb),
                        'Messages',COALESCE((SELECT jsonb_agg(message ORDER BY position)
                            FROM shop_messages WHERE product_id=p.id),'[]'::jsonb)) AS item
                    FROM shop_products p LEFT JOIN shop_gem_packs g ON g.product_id=p.id
                    LEFT JOIN shop_cues c ON c.product_id=p.id LEFT JOIN shop_message_packs m ON m.product_id=p.id
                    WHERE ($2 AND EXISTS(SELECT FROM shop_user_inventory WHERE user_id=$1 AND product_id=p.id))
                       OR (NOT $2 AND p.visible AND (p.sale_starts_at IS NULL OR p.sale_starts_at<=now())
                        AND (p.sale_ends_at IS NULL OR p.sale_ends_at>now()))
                ) catalog
                """;

    public static void Map(WebApplication app)
    {
        app.MapGet("/api/shop/products", (Database db, ClaimsPrincipal user) => Read(db, User(user), false)).RequireAuthorization();
        app.MapGet("/api/me/inventory", (Database db, ClaimsPrincipal user) => Read(db, User(user), true)).RequireAuthorization();
    }

    static Guid User(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

    static async Task<IResult> Read(Database db, Guid user, bool inventory)
    {
            // One statement provides a consistent catalog snapshot, including ordered details.
            await using var cmd = db.Source.CreateCommand(Query);
            cmd.Parameters.AddWithValue(user);
            cmd.Parameters.AddWithValue(inventory);
            return Results.Text((string)(await cmd.ExecuteScalarAsync())!, "application/json; charset=utf-8");
    }
}
