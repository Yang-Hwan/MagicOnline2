using System.Security.Claims;

namespace MagicOnline.GameApi;

public sealed record ShopPurchaseRequest(Guid RequestId, long ProductId, int ExpectedPriceGem, int ExpectedRevision);
public sealed record ShopPurchaseReceipt(long PurchaseId, long ProductId, int PaidGem, int BalanceGem, bool AlreadyProcessed, long GrantedCoin=0, long BalanceCoin=0);

public sealed class ShopPurchases(Database db)
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/shop/purchases", (ClaimsPrincipal user, ShopPurchaseRequest input, ShopPurchases shop) =>
            shop.Buy(Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!), input)).RequireAuthorization();
    }

    public async Task<ShopPurchaseReceipt> Buy(Guid user, ShopPurchaseRequest input)
    {
        if (input.RequestId == Guid.Empty || input.ProductId <= 0 || input.ExpectedPriceGem < 0 || input.ExpectedRevision < 1)
            throw new ApiFault(400, "ShopInvalidRequest");
        await using var c = await db.Source.OpenConnectionAsync();
        await using var tx = await c.BeginTransactionAsync();
        int balance;long coinBalance;
        // Same first lock as every other wallet writer: retries and different products serialize per user.
        await using (var wallet = Database.Command(c, "SELECT jem,coin FROM wallets WHERE user_id=$1 FOR UPDATE", user))
        await using(var r=await wallet.ExecuteReaderAsync()){
            if(!await r.ReadAsync())throw new ApiFault(404,"UserNotFound");
            balance=r.GetInt32(0);coinBalance=r.GetInt64(1);
        }

        await using (var previous = Database.Command(c, "SELECT id,product_id,paid_gem,credited_coin FROM shop_purchases WHERE user_id=$1 AND request_id=$2", user, input.RequestId))
        await using (var r = await previous.ExecuteReaderAsync())
        {
            if (await r.ReadAsync())
            {
                if (r.GetInt64(1) != input.ProductId || r.GetInt32(2) != input.ExpectedPriceGem)
                    throw new ApiFault(409, "ShopRequestConflict");
                return new(r.GetInt64(0), r.GetInt64(1), r.GetInt32(2), balance, true,r.GetInt64(3),coinBalance);
            }
        }

        int price;string kind;long grantedCoin=0;
        // Keep sale status and price stable until the purchase commits, including concurrent admin edits.
        await using (var product = Database.Command(c, """
            SELECT kind,price_gem,revision,visible,purchasable,
                (sale_starts_at IS NULL OR sale_starts_at<=clock_timestamp())
                AND (sale_ends_at IS NULL OR sale_ends_at>clock_timestamp())
            FROM shop_products WHERE id=$1 FOR SHARE
            """, input.ProductId))
        await using (var r = await product.ExecuteReaderAsync())
        {
            if (!await r.ReadAsync()) throw new ApiFault(404, "ShopProductNotFound");
            kind=r.GetString(0);
            if (kind is not ("CoinPack" or "Cue" or "Character" or "MessagePack")) throw new ApiFault(400, "ShopGemPackRequiresPayment");
            if (!r.GetBoolean(3) || !r.GetBoolean(4) || !r.GetBoolean(5)) throw new ApiFault(409, "ShopNotForSale");
            price = r.GetInt32(1);
            if (price != input.ExpectedPriceGem || r.GetInt32(2) != input.ExpectedRevision) throw new ApiFault(409, "ShopProductChanged");
        }
        if(kind=="CoinPack"){
            await using var pack=Database.Command(c,"SELECT amount+bonus FROM shop_coin_packs WHERE product_id=$1",input.ProductId);
            grantedCoin=(long?)await pack.ExecuteScalarAsync()??throw new ApiFault(409,"ShopProductChanged");
            if(grantedCoin<=0 || grantedCoin>9_000_000_000_000_000L || coinBalance>9_000_000_000_000_000L-grantedCoin)throw new ApiFault(400,"ShopCoinLimit");
        }
        if(kind!="CoinPack")
        await using (var owned = Database.Command(c, "SELECT EXISTS(SELECT FROM shop_user_inventory WHERE user_id=$1 AND product_id=$2)", user, input.ProductId))
            if (await owned.ExecuteScalarAsync() is true) throw new ApiFault(409, "ShopAlreadyOwned");
        if (balance < price) throw new ApiFault(400, "ShopInsufficientGems");

        await using (var debit = Database.Command(c, "UPDATE wallets SET jem=jem-$2,coin=coin+$3 WHERE user_id=$1", user, price,grantedCoin)) await debit.ExecuteNonQueryAsync();
        if(kind!="CoinPack")
        await using (var grant = Database.Command(c, "INSERT INTO shop_user_inventory(user_id,product_id) VALUES($1,$2)", user, input.ProductId)) await grant.ExecuteNonQueryAsync();
        long purchase;
        await using (var receipt = Database.Command(c, "INSERT INTO shop_purchases(user_id,product_id,paid_gem,request_id,credited_coin) VALUES($1,$2,$3,$4,$5) RETURNING id", user, input.ProductId, price, input.RequestId,grantedCoin))
            purchase = (long)(await receipt.ExecuteScalarAsync())!;
        await using (var ledger = Database.Command(c, "INSERT INTO wallet_ledger(user_id,request_key,coin_delta,jem_delta) VALUES($1,$2,$3,$4)", user, "Shop:" + input.RequestId.ToString("N"), grantedCoin,-price)) await ledger.ExecuteNonQueryAsync();
        await tx.CommitAsync();
        return new(purchase, input.ProductId, price, balance - price, false,grantedCoin,coinBalance+grantedCoin);
    }
}
