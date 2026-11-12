using MagicOnline.GameApi;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Assets.Scripts.Prack.BackSys.GameData;

var config = new ConfigurationBuilder().AddUserSecrets<Database>().Build();
var cs = new NpgsqlConnectionStringBuilder(config.GetConnectionString("GameDb"));
if (cs.Host is not ("localhost" or "127.0.0.1")) throw new Exception("Local DB only");
await using var root = NpgsqlDataSource.Create(cs.ConnectionString);
string schema = "shop_test_" + Guid.NewGuid().ToString("N");
await using (var create = root.CreateCommand($"CREATE SCHEMA {schema}")) await create.ExecuteNonQueryAsync();
try
{
    cs.SearchPath = schema;
    await using var source = NpgsqlDataSource.Create(cs.ConnectionString);
    async Task Sql(string sql) { await using var cmd = source.CreateCommand(sql); await cmd.ExecuteNonQueryAsync(); }
    async Task<long> Scalar(string sql) { await using var cmd = source.CreateCommand(sql); return Convert.ToInt64(await cmd.ExecuteScalarAsync()); }
    int checks = 0;
    void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks++; Console.WriteLine("PASS " + label); }
    await Sql("""
        CREATE TABLE users(id uuid PRIMARY KEY);
        CREATE TABLE wallets(user_id uuid PRIMARY KEY REFERENCES users,jem integer NOT NULL CHECK(jem>=0));
        CREATE TABLE wallet_ledger(user_id uuid REFERENCES users,request_key text,coin_delta bigint,jem_delta integer,UNIQUE(user_id,request_key));
        """);
    await Sql(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Migrations", "010_shop.sql")));
    await Sql(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Migrations", "011_match_equipment.sql")));
    var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid(); var poor = Guid.NewGuid(); var failure = Guid.NewGuid();
    await Sql($"INSERT INTO users VALUES('{a}'),('{b}'),('{c}'),('{poor}'),('{failure}'); INSERT INTO wallets VALUES('{a}',1000),('{b}',200),('{c}',600),('{poor}',0),('{failure}',500)");
    await Sql("""
        INSERT INTO shop_content_assets VALUES('test',NULL,NULL,NULL,NULL);
        INSERT INTO shop_products(id,kind,name,price_gem,visible,purchasable) OVERRIDING SYSTEM VALUE VALUES
        (1,'Cue','cue',100,true,true),(2,'Character','character',200,true,true),(3,'MessagePack','messages',150,true,true),
        (4,'GemPack','cash',NULL,true,true),(5,'Cue','hidden',10,false,true),(6,'Cue','disabled',10,true,false),
        (7,'Cue','future',10,true,true),(8,'Cue','expired',10,true,true),(9,'Cue','free',0,true,true),
        (10,'Cue','costly A',400,true,true),(11,'Cue','costly B',400,true,true);
        UPDATE shop_products SET sale_starts_at=now()+interval '1 day' WHERE id=7;
        UPDATE shop_products SET sale_ends_at=now()-interval '1 day' WHERE id=8;
        INSERT INTO shop_cues SELECT id,'test',0,0,0,0 FROM shop_products WHERE kind='Cue';
        INSERT INTO shop_gem_packs VALUES(4,100,0);
        INSERT INTO shop_character_emotions SELECT 2,unnest(ARRAY['기본','감사','미안','미소','환희','짜증','우울','좌절','분노']),'test';
        INSERT INTO shop_message_packs VALUES(3,'기본'); INSERT INTO shop_messages VALUES(3,1,'안녕하세요.');
        """);
    var service = new ShopPurchases(new Database(source));
    ShopPurchaseRequest Request(long product, int price) => new(Guid.NewGuid(), product, price, 1);
    async Task<string> Snapshot(Guid user)
    {
        await using var cmd = source.CreateCommand($"SELECT json_build_array((SELECT jem FROM wallets WHERE user_id='{user}'),(SELECT count(*) FROM shop_purchases WHERE user_id='{user}'),(SELECT count(*) FROM shop_user_inventory WHERE user_id='{user}'),(SELECT count(*) FROM wallet_ledger WHERE user_id='{user}'))::text");
        return (string)(await cmd.ExecuteScalarAsync())!;
    }
    async Task Reject(Guid user, ShopPurchaseRequest input, string code)
    {
        string before = await Snapshot(user);
        try { await service.Buy(user, input); throw new Exception("Expected " + code); }
        catch (ApiFault e) { Check(e.Message == code, "reject " + code); }
        Check(before == await Snapshot(user), "rejection leaves wallet, inventory, receipts and ledger unchanged");
    }
    var request = Request(1, 100);
    var receipts = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => service.Buy(a, request)));
    Check(receipts.Select(r => r.PurchaseId).Distinct().Count() == 1 && receipts.Count(r => !r.AlreadyProcessed) == 1, "concurrent same request returns one purchase");
    Check(await Scalar($"SELECT jem FROM wallets WHERE user_id='{a}'") == 900, "single exact debit");
    await service.Buy(a, Request(2, 200)); await service.Buy(a, Request(3, 150));
    Check(await Scalar($"SELECT count(*) FROM shop_user_inventory WHERE user_id='{a}'") == 3, "cue, character and message pack granted");
    Check(await Scalar($"SELECT sum(jem_delta) FROM wallet_ledger WHERE user_id='{a}'") == -450, "ledger exact debit total");
    await Reject(a, Request(1, 100), "ShopAlreadyOwned");
    await Reject(a, request with { ProductId = 2 }, "ShopRequestConflict");
    await Reject(a, request with { ExpectedPriceGem = 99 }, "ShopRequestConflict");
    await Sql("UPDATE shop_products SET visible=false,purchasable=false,price_gem=101,revision=2 WHERE id=1");
    Check((await service.Buy(a, request)).AlreadyProcessed, "committed retry succeeds after price change and sale removal");
    await Sql("UPDATE shop_products SET visible=true,purchasable=true,price_gem=100,revision=1 WHERE id=1");
    await Reject(poor, Request(1, 100), "ShopInsufficientGems");
    await Reject(a, Request(4, 0), "ShopGemPackRequiresPayment");
    foreach (int id in new[] { 5, 6, 7, 8 }) await Reject(a, Request(id, 10), "ShopNotForSale");
    await Reject(a, Request(10, 399), "ShopProductChanged");
    await Reject(a, Request(10, 400) with { ExpectedRevision = 2 }, "ShopProductChanged");
    await Reject(a, Request(999, 0), "ShopProductNotFound");
    await Reject(a, request with { RequestId = Guid.Empty }, "ShopInvalidRequest");
    await Reject(Guid.NewGuid(), Request(1, 100), "UserNotFound");
    var free = await service.Buy(poor, Request(9, 0));
    Check(free.PaidGem == 0 && free.BalanceGem == 0, "free item grants with exact zero ledger");
    async Task<string> Attempt(Guid user, ShopPurchaseRequest input)
    { try { await service.Buy(user, input); return "OK"; } catch (ApiFault e) { return e.Message; } }
    var duplicate = await Task.WhenAll(Attempt(b, Request(1, 100)), Attempt(b, Request(1, 100)));
    Check(duplicate.Count(s => s == "OK") == 1 && duplicate.Contains("ShopAlreadyOwned"), "different requests cannot buy same item twice");
    var spending = await Task.WhenAll(Attempt(c, Request(10, 400)), Attempt(c, Request(11, 400)));
    Check(spending.Count(s => s == "OK") == 1 && spending.Contains("ShopInsufficientGems") && await Scalar($"SELECT jem FROM wallets WHERE user_id='{c}'") == 200, "different products cannot overspend concurrently");
    string beforeFailure = await Snapshot(failure);
    await Sql($"CREATE FUNCTION reject_test_ledger() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.user_id='{failure}' THEN RAISE EXCEPTION 'test ledger failure'; END IF; RETURN NEW; END $$; CREATE TRIGGER reject_test_ledger BEFORE INSERT ON wallet_ledger FOR EACH ROW EXECUTE FUNCTION reject_test_ledger()");
    var failedRequest = Request(1, 100);
    try { await service.Buy(failure, failedRequest); throw new Exception("Expected injected failure"); }
    catch (PostgresException e) when (e.SqlState == "P0001") { }
    Check(beforeFailure == await Snapshot(failure), "failure at final ledger write rolls back debit, grant and receipt");
    await Sql("DROP TRIGGER reject_test_ledger ON wallet_ledger");
    var recovered = await service.Buy(failure, failedRequest);
    Check(recovered.BalanceGem == 400 && (await service.Buy(failure, failedRequest)).AlreadyProcessed, "same request recovers after rollback and then replays without debit");
    Check(await Scalar($"SELECT count(*) FROM shop_user_inventory WHERE user_id='{b}'") == 1 && await Scalar($"SELECT count(*) FROM shop_user_inventory WHERE user_id='{poor}'") == 1, "ownership remains scoped to each user");

    var equipment = new ShopEquipment(new Database(source));
    var equipCue = new ShopEquipmentRequest("Cue", 1, true);
    string walletBeforeEquipment = await Snapshot(a);
    await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => equipment.Set(a, equipCue)));
    Check(await Scalar($"SELECT count(*) FROM shop_user_equipment WHERE user_id='{a}' AND slot='Cue' AND product_id=1") == 1, "concurrent equip retries retain one cue slot");
    await equipment.Set(a, new("Character", 2, true));
    await equipment.Set(a, new("MessagePack", 3, true));
    Check(await Scalar($"SELECT count(*) FROM shop_user_equipment WHERE user_id='{a}'") == 2 && await Scalar($"SELECT count(*) FROM shop_user_selected_message_packs WHERE user_id='{a}'") == 1, "cue, character and message selection persist independently");
    async Task RejectEquipment(Guid user, ShopEquipmentRequest input, string code)
    {
        try { await equipment.Set(user, input); throw new Exception("Expected " + code); }
        catch (ApiFault error) { Check(error.Message == code, "equipment rejects " + code); }
    }
    await RejectEquipment(b, new("Character", 2, true), "EquipmentNotOwned");
    await RejectEquipment(a, new("Cue", 2, true), "EquipmentKindMismatch");
    await RejectEquipment(a, new("GemPack", 4, true), "EquipmentInvalidRequest");
    await RejectEquipment(a, new("Cue", 0, true), "EquipmentInvalidRequest");
    await RejectEquipment(Guid.NewGuid(), equipCue, "UserNotFound");
    Check(await Scalar($"SELECT product_id FROM shop_user_equipment WHERE user_id='{a}' AND slot='Cue'") == 1, "invalid requests preserve current equipment");
    Check(await Scalar($"SELECT count(*) FROM shop_user_equipment WHERE user_id='{b}'") == 0, "cannot modify another user's equipment");
    await Sql("UPDATE shop_products SET visible=false,purchasable=false,sale_ends_at=now()-interval '1 day' WHERE id=1");
    await equipment.Set(a, equipCue);
    Check(await Scalar($"SELECT product_id FROM shop_user_equipment WHERE user_id='{a}' AND slot='Cue'") == 1, "owned hidden, expired, sale-stopped product remains equippable");
    Check(walletBeforeEquipment == await Snapshot(a), "equipment never charges or changes ownership, receipts or ledger");
    await service.Buy(a, Request(9, 0));
    await equipment.Set(a, new("Cue", 9, true));
    await equipment.Set(a, equipCue with { Equipped = false });
    Check(await Scalar($"SELECT product_id FROM shop_user_equipment WHERE user_id='{a}' AND slot='Cue'") == 9, "switching cues replaces slot; delayed unequip of old cue keeps new cue");
    await equipment.Set(a, new("Cue", 9, false));
    await equipment.Set(a, new("Cue", 9, false));
    Check(await Scalar($"SELECT count(*) FROM shop_user_equipment WHERE user_id='{a}' AND slot='Cue'") == 0, "cue unequip retries are idempotent");
    await Sql("""
        INSERT INTO shop_products(id,kind,name,price_gem,visible,purchasable) OVERRIDING SYSTEM VALUE VALUES(12,'MessagePack','second messages',0,true,true);
        INSERT INTO shop_message_packs VALUES(12,'소심'); INSERT INTO shop_messages VALUES(12,1,'감사합니다.');
        """);
    await service.Buy(a, Request(12, 0));
    await equipment.Set(a, new("MessagePack", 12, true));
    await equipment.Set(a, new("MessagePack", 12, true));
    Check(await Scalar($"SELECT count(*) FROM shop_user_selected_message_packs WHERE user_id='{a}'") == 2, "multiple message packs can be selected without duplicates");
    await equipment.Set(a, new("MessagePack", 3, false));
    Check(await Scalar($"SELECT product_id FROM shop_user_selected_message_packs WHERE user_id='{a}'") == 12, "deselecting a message pack keeps other selections");
    await equipment.Set(a, new("Character", 2, false));
    Check(await Scalar($"SELECT count(*) FROM shop_user_equipment WHERE user_id='{a}' AND slot='Character'") == 0, "character unequip returns to default selection");
    await equipment.Set(a, new("Cue", 1, true));
    await equipment.Set(a, new("Character", 2, true));
    await equipment.Set(a, new("MessagePack", 3, true));
    await equipment.Set(b, new("Cue", 1, true));
    await Sql("UPDATE shop_cues SET power_pct=20,follow_pct=-40,tip_pct=10,shot_seconds=5 WHERE product_id=1");
    var frozen = new MatchEquipment(new Database(source));
    string match = Guid.NewGuid().ToString("N");
    var snapshots = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => frozen.Freeze(match, a, b)));
    Check(snapshots.All(s => s.MatchId == match && s.Players.Length == 2 && s.Players[0].UserId == a.ToString() && s.Players[1].UserId == b.ToString()), "concurrent match freeze retains participant order");
    var snapshot = await frozen.Read(match, b);
    Check(snapshot.Players[0].CueId == 1 && snapshot.Players[0].Power == 20 && snapshot.Players[1].Power == 20, "both players share frozen server cue stats including hidden owned items");
    Check(snapshot.Players[0].CharacterId == 2 && snapshot.Players[0].Emotions.Length == 9, "selected character includes nine frozen images");
    Check(snapshot.Players[0].Messages.SequenceEqual(new[] { "안녕하세요.", "감사합니다." }) && snapshot.Players[1].Messages.Length == 0, "only selected message packs included in stable order");
    await equipment.Set(a, new("Cue", 9, true));
    await equipment.Set(a, new("MessagePack", 3, false));
    await Sql("UPDATE shop_cues SET power_pct=-40 WHERE product_id=1; UPDATE shop_messages SET message='changed' WHERE product_id=3");
    var unchanged = await frozen.Freeze(match, a, b);
    Check(unchanged.Players[0].CueId == 1 && unchanged.Players[0].Power == 20 && unchanged.Players[0].Messages[0] == "안녕하세요.", "equipment and admin changes never mutate active match snapshot");
    var nextMatch = await frozen.Freeze(Guid.NewGuid().ToString("N"), a, b);
    Check(nextMatch.Players[0].CueId == 9 && nextMatch.Players[1].Power == -40 && nextMatch.Players[0].Messages.SequenceEqual(new[] { "감사합니다." }), "next match uses latest equipment and catalog");
    try { await frozen.Read(match, poor); throw new Exception("Expected nonparticipant denial"); }
    catch (ApiFault error) { Check(error.Status == 404, "only match participants read equipment snapshot"); }
    try { await frozen.Freeze(match, b, a); throw new Exception("Expected participant conflict"); }
    catch (ApiFault error) { Check(error.Status == 409, "same match ID cannot reverse participants"); }
    try { await frozen.Freeze(match, a, b, true); throw new Exception("Expected mode conflict"); }
    catch (ApiFault error) { Check(error.Status == 409, "friendly snapshot cannot replace ranked match snapshot"); }
    var defaults = await frozen.Freeze(Guid.NewGuid().ToString("N"), poor, failure);
    Check(defaults.Players.All(p => p.CueId == 0 && p.CharacterId == 0 && p.Messages.Length == 0), "unequipped users use neutral default profile");
    var basicProfile = new MatchEquipmentProfile();
    Check(basicProfile.PowerScale == 1 && basicProfile.TipScale == 1 && Math.Abs(basicProfile.EffectiveFollow(.2f) - .2f) < .00001f && basicProfile.TurnSeconds(30) == 30, "default profile preserves existing physics and clock");
    var enhanced = snapshot.Players[0];
    Check(Math.Abs(enhanced.PowerScale - 1.2f) < .00001f && Math.Abs(enhanced.TipScale - 1.1f) < .00001f && Math.Abs(enhanced.EffectiveFollow(.5f) - .3f) < .00001f && enhanced.TurnSeconds(30) == 35, "equipment math applies power, tip, follow and turn seconds");
    var bounded = new MatchEquipmentProfile { Power = -999, Tip = 999, FollowThrough = 999, ShotSeconds = -999 };
    Check(Math.Abs(bounded.PowerScale - .6f) < .00001f && Math.Abs(bounded.TipScale - 1.1f) < .00001f && bounded.EffectiveFollow(1) == 1 && bounded.TurnSeconds(30) == 25, "equipment math enforces configured bounds");
    Console.WriteLine($"Passed {checks} checks; live wallets and products unchanged.");
}
finally
{
    await using var drop = root.CreateCommand($"DROP SCHEMA {schema} CASCADE"); await drop.ExecuteNonQueryAsync();
}
