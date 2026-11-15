using Microsoft.Extensions.Configuration;
using MagicOnline.GameApi;
using Npgsql;
var config=new ConfigurationBuilder().AddUserSecrets<Database>().Build();var cs=new NpgsqlConnectionStringBuilder(config.GetConnectionString("GameDb"));
if(cs.Host is not ("localhost" or "127.0.0.1"))throw new Exception("Local DB only");
await using var root=NpgsqlDataSource.Create(cs.ConnectionString);string schema="premium_"+Guid.NewGuid().ToString("N");await using(var q=root.CreateCommand($"CREATE SCHEMA {schema}"))await q.ExecuteNonQueryAsync();
try
{
 cs.SearchPath=schema;await using var source=NpgsqlDataSource.Create(cs.ConnectionString);var db=new Database(source);await db.Migrate();int checks=0;
 async Task Sql(string sql){await using var q=source.CreateCommand(sql);await q.ExecuteNonQueryAsync();}
 void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
 var a=Guid.NewGuid();var b=Guid.NewGuid();foreach(var user in new[]{a,b})await Sql($"INSERT INTO users(id,login_id,normalized_login,password_hash,nickname) VALUES('{user}','{user}','{user}','test','큐대검증');INSERT INTO wallets(user_id,jem) VALUES('{user}',10000);INSERT INTO user_stats(user_id) VALUES('{user}')");
 var products=new List<(long Id,string Visual,int Price,int Revision)>();
 await using(var q=source.CreateCommand("SELECT p.id,c.visual,p.price_gem,p.revision,c.power_pct,c.follow_pct,c.tip_pct,c.shot_seconds,p.visible,p.purchasable FROM shop_products p JOIN shop_cues c ON c.product_id=p.id WHERE c.visual LIKE 'Gameplay/PremiumCues/%' ORDER BY c.visual"))
 await using(var r=await q.ExecuteReaderAsync())while(await r.ReadAsync()){products.Add((r.GetInt64(0),r.GetString(1),r.GetInt32(2),r.GetInt32(3)));Check(r.GetInt32(4)==0 && r.GetInt32(5)==0 && r.GetInt32(6)==0 && r.GetInt32(7)==0 && r.GetBoolean(8) && r.GetBoolean(9),"cosmetic premium cue is balanced and purchasable: "+r.GetString(1));}
 Check(products.Count==3 && products.Select(x=>x.Visual).Distinct().Count()==3,"three distinct premium cues seeded");
 await db.Migrate();await using(var q=source.CreateCommand("SELECT count(*) FROM shop_cues WHERE visual LIKE 'Gameplay/PremiumCues/%'"))Check((long)(await q.ExecuteScalarAsync())! ==3,"migration retry does not duplicate products");
 var shop=new ShopPurchases(db);var equip=new ShopEquipment(db);var snapshots=new MatchEquipment(db);
 foreach(var p in products)
 {
  var request=new ShopPurchaseRequest(Guid.NewGuid(),p.Id,p.Price,p.Revision);var receipts=await Task.WhenAll(shop.Buy(a,request),shop.Buy(a,request));
  Check(receipts.Count(x=>!x.AlreadyProcessed)==1,"purchase retry charges once: "+p.Visual);
  await equip.Set(a,new("Cue",p.Id,true));string id=Guid.NewGuid().ToString("N");var frozen=await snapshots.Freeze(id,a,b,true);
  Check(frozen.Players[0].CueVisual==p.Visual && frozen.Players[0].CueId==p.Id,"equipped cue effect key enters server match snapshot");
  var peer=await snapshots.Read(id,b);Check(peer.Players[0].CueVisual==p.Visual && peer.Players[1].CueVisual=="","opponent reads same cue appearance and default fallback");
  await equip.Set(a,new("Cue",p.Id,false));Check((await snapshots.Read(id,a)).Players[0].CueVisual==p.Visual,"mid-match equipment changes do not change frozen effects");
 }
 await using(var q=source.CreateCommand($"SELECT jem FROM wallets WHERE user_id='{a}'"))Check((int)(await q.ExecuteScalarAsync())! ==10000-products.Sum(x=>x.Price),"exact total gem debit for three products");
 Console.WriteLine($"Premium cues: {checks} checks passed");
}
finally{await using var q=root.CreateCommand($"DROP SCHEMA {schema} CASCADE");await q.ExecuteNonQueryAsync();}
