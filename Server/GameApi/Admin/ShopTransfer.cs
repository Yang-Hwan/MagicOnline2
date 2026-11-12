using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;

namespace MagicOnline.GameApi.Admin;

internal static class ShopTransfer
{
    // Fixed allowlist in foreign-key order. Local revisions and all member records stay local.
    record Part(string Name,string Keys,string Fields)
    {
        public string Columns => Keys+","+Fields;
        public string Table => "shop_"+Name;
        public string Stage => "pg_temp.incoming_"+Name;
    }
    static readonly Part[] Parts = [
        new("content_assets","asset_key","content_hash,content_type,width,height"),
        new("products","id","kind,name,description,thumbnail,price_gem,sort_order,sale_starts_at,sale_ends_at,visible,purchasable"),
        new("gem_packs","product_id","amount,bonus"),
        new("cues","product_id","visual,power_pct,follow_pct,tip_pct,shot_seconds"),
        new("character_emotions","product_id,emotion","asset_key"),
        new("message_packs","product_id","personality"),
        new("messages","product_id,position","message")
    ];

    static async Task Execute(NpgsqlConnection c,string sql,params object[] args)
    {
        await using var cmd=Database.Command(c,sql,args);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task<JsonObject> Read(NpgsqlConnection c)
    {
        await Execute(c,"SET LOCAL TIME ZONE 'UTC'");
        var shop=new JsonObject();
        foreach(var part in Parts)
        {
            await using var cmd=Database.Command(c,$"""
                SELECT COALESCE(jsonb_agg(to_jsonb(r) ORDER BY {part.Keys}),'[]')::text
                FROM (SELECT {part.Columns} FROM {part.Table}) r
                """);
            shop[part.Name]=JsonNode.Parse((string)(await cmd.ExecuteScalarAsync())!);
        }
        return shop;
    }

    public static async Task Replace(NpgsqlConnection c,JsonElement shop,Guid admin,string requestId)
    {
        if(shop.ValueKind!=JsonValueKind.Object || shop.EnumerateObject().Count()!=Parts.Length)
            throw new ApiFault(400,"상점 상품 파일 구성이 올바르지 않습니다.");
        await Execute(c,"LOCK TABLE shop_content_assets,shop_products IN SHARE ROW EXCLUSIVE MODE");
        var before=await Read(c);
        if(JsonNode.DeepEquals(before,JsonNode.Parse(shop.GetRawText()))) return;
        try
        {
            foreach(var part in Parts)
            {
                if(!shop.TryGetProperty(part.Name,out var rows) || rows.ValueKind!=JsonValueKind.Array)
                    throw new ApiFault(400,$"상점 {part.Name} 목록이 없습니다.");
                var columns=part.Columns.Split(',').ToHashSet(StringComparer.Ordinal);
                foreach(var row in rows.EnumerateArray())
                    if(row.ValueKind!=JsonValueKind.Object || !columns.SetEquals(row.EnumerateObject().Select(p=>p.Name)))
                        throw new ApiFault(400,$"상점 {part.Name} 항목의 필드 구성이 올바르지 않습니다.");
                await Execute(c,$"CREATE TEMP TABLE incoming_{part.Name} ON COMMIT DROP AS SELECT {part.Columns} FROM {part.Table} WITH NO DATA");
                await Execute(c,$"INSERT INTO {part.Stage} SELECT {part.Columns} FROM jsonb_populate_recordset(NULL::{part.Table},$1::jsonb)",rows.GetRawText());
            }
            // A product ID is also referenced by inventory, purchases and equipped slots.
            await using var changedKind=Database.Command(c,"SELECT EXISTS(SELECT FROM shop_products p JOIN pg_temp.incoming_products i USING(id) WHERE i.kind<>p.kind)");
            if(await changedKind.ExecuteScalarAsync() is true)
                throw new ApiFault(409,"같은 상품 ID에 다른 상품 종류가 있습니다. 대상 상점 목록을 확인해 주세요.");
            foreach(var part in Parts.Reverse())
            {
                if(part.Name=="content_assets") continue;
                var match=string.Join(" AND ",part.Keys.Split(',').Select(k=>$"s.{k}=d.{k}"));
                try { await Execute(c,$"DELETE FROM {part.Table} d WHERE NOT EXISTS(SELECT FROM {part.Stage} s WHERE {match})"); }
                catch(PostgresException e) when(e.SqlState=="23503")
                {
                    throw new ApiFault(409,"회원 보유·구매 또는 다른 상품이 참조하는 항목을 제거할 수 없습니다.");
                }
            }
            foreach(var part in Parts)
            {
                var set=string.Join(",",part.Fields.Split(',').Select(k=>$"{k}=excluded.{k}"));
                bool product=part.Name=="products";
                if(product) set+=",revision=shop_products.revision+1,updated_at=now()";
                await Execute(c,$"""
                    INSERT INTO {part.Table} ({part.Columns}) {(product?"OVERRIDING SYSTEM VALUE":"")}
                    SELECT {part.Columns} FROM {part.Stage}
                    ON CONFLICT ({part.Keys}) DO UPDATE SET {set}
                    """);
            }
            await Execute(c,"DELETE FROM shop_content_assets a WHERE NOT EXISTS(SELECT FROM pg_temp.incoming_content_assets i WHERE i.asset_key=a.asset_key)");
            await using var invalid=Database.Command(c,"""
                SELECT EXISTS(SELECT FROM shop_products p
                    LEFT JOIN shop_cues c ON c.product_id=p.id
                    LEFT JOIN shop_gem_packs g ON g.product_id=p.id
                    LEFT JOIN shop_message_packs m ON m.product_id=p.id
                    WHERE p.id<1 OR (p.kind='Cue')<>(c.product_id IS NOT NULL)
                    OR (p.kind='GemPack')<>(g.product_id IS NOT NULL)
                    OR (p.kind='MessagePack')<>(m.product_id IS NOT NULL)
                    OR (p.kind='MessagePack' AND (trim(m.personality)='' OR NOT EXISTS(SELECT FROM shop_messages WHERE product_id=p.id)))
                    OR (SELECT count(*) FROM shop_character_emotions WHERE product_id=p.id)<>CASE WHEN p.kind='Character' THEN 9 ELSE 0 END)
                OR EXISTS(SELECT FROM shop_character_emotions WHERE emotion NOT IN ('기본','감사','미안','미소','환희','짜증','우울','좌절','분노'))
                """);
            if(await invalid.ExecuteScalarAsync() is true) throw new ApiFault(400,"상점 상품의 종류와 상세 정보가 일치하지 않습니다.");
            // Explicit IDs from another machine must not collide with the next locally created item.
            await Execute(c,"""
                DO $$ DECLARE highest bigint; BEGIN
                    SELECT max(id) INTO highest FROM shop_products;
                    IF highest >= (SELECT last_value FROM shop_products_id_seq) THEN
                        EXECUTE format('ALTER SEQUENCE shop_products_id_seq RESTART WITH %s', highest+1);
                    END IF;
                END $$
                """);
            var after=await Read(c);
            await Execute(c,"INSERT INTO admin_audit_logs(admin_id,action,target,request_id,before_value,after_value,reason) VALUES($1,'ChartImported','Shop',$2,$3::jsonb,$4::jsonb,'상점 전체 가져오기')",
                admin,requestId,before.ToJsonString(),after.ToJsonString());
        }
        catch(PostgresException e) when(e.SqlState.StartsWith("22") || e.SqlState.StartsWith("23") || e.SqlState=="21000")
        {
            throw new ApiFault(400,"상점 데이터의 값, 중복 키 또는 상품·리소스 참조가 올바르지 않습니다.");
        }
    }
}
