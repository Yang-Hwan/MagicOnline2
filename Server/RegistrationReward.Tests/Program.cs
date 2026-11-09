using System.Text.Json;
using MagicOnline.GameApi;
using Microsoft.Extensions.Configuration;
using Npgsql;

var settings = new ConfigurationBuilder().AddUserSecrets<Database>().Build();
var cs = new NpgsqlConnectionStringBuilder(settings.GetConnectionString("GameDb"));
if (cs.Host is not ("localhost" or "127.0.0.1")) throw new Exception("Local DB only");
await using var root = NpgsqlDataSource.Create(cs.ConnectionString);
string schema = "registration_test_" + Guid.NewGuid().ToString("N");
await using (var create = root.CreateCommand($"CREATE SCHEMA {schema}")) await create.ExecuteNonQueryAsync();
try
{
    cs.SearchPath = schema;
    await using var source = NpgsqlDataSource.Create(cs.ConnectionString);
    async Task Run(string sql)
    {
        await using var cmd = source.CreateCommand(sql);
        await cmd.ExecuteNonQueryAsync();
    }
    await Run(File.ReadAllText("Server/GameApi/Migrations/001_initial.sql"));
    await Run(File.ReadAllText("Server/GameApi/Migrations/006_registration_reward.sql"));
    await Run(File.ReadAllText("Server/GameApi/Migrations/008_user_post_reward.sql"));
    var db = new Database(source);
    var accounts = new Accounts(db);
    var data = new GameData(db);
    async Task Configure(string currency, string enabled = "Y")
    {
        // Use 71 deliberately: the lookup must follow the chart, not a hard-coded type number.
        var json = JsonSerializer.Serialize(new[] {
            new { GrpCd="Bust", ComCd="BustMemReg", ComVal=100000, ComVal2=71, IsUse=enabled },
            new { GrpCd="Wealth", ComCd=currency, ComVal=71, ComVal2=0, IsUse="Y" }
        });
        await using var cmd = source.CreateCommand("INSERT INTO charts VALUES('CommonCode',$1::jsonb) ON CONFLICT(name) DO UPDATE SET rows=excluded.rows");
        cmd.Parameters.AddWithValue(json);
        await cmd.ExecuteNonQueryAsync();
    }
    async Task<long> Ledger(Guid user, string currency)
    {
        await using var cmd = source.CreateCommand($"SELECT {currency.ToLowerInvariant()}_delta FROM wallet_ledger WHERE user_id=$1 AND request_key='InitialGrant'");
        cmd.Parameters.AddWithValue(user);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }
    foreach (string currency in new[] { "Coin","Jem","Cash","Gold" })
    {
        await Configure(currency);
        var input = new Credentials("test_" + currency,"test_reward_password");
        var user = Guid.Parse((await accounts.Register(input)).UserId);
        await accounts.Login(input);
        try { await accounts.Register(input); throw new Exception("Duplicate signup accepted"); }
        catch (ApiFault e) when (e.Status == 409) { }
        var p = await data.Profile(user);
        var v = new Dictionary<string,long> { ["Coin"]=p.Coin,["Jem"]=p.Jem,["Cash"]=p.Cash,["Gold"]=p.Gold };
        if (v[currency]!=100000 || v.Values.Sum()!=100000 || await Ledger(user,currency)!=100000)
            throw new Exception("Incorrect wallet or ledger");
        Console.WriteLine($"PASS {currency}: mapped grant once, repeated signup and login");
    }
    await Configure("Unknown");
    try { await accounts.Register(new("invalid_reward","test_reward_password")); throw new Exception("Invalid type accepted"); }
    catch (ApiFault e) when (e.Status == 409) { }
    await using (var q=source.CreateCommand("SELECT count(*) FROM users WHERE login_id='invalid_reward'"))
        if ((long)(await q.ExecuteScalarAsync())! != 0) throw new Exception("Partial account");
    await Configure("Coin","N");
    var noGrant=await data.Profile(Guid.Parse((await accounts.Register(new("no_reward","test_reward_password"))).UserId));
    if(noGrant.Coin!=0 || noGrant.Jem!=0 || noGrant.Gold!=0 || noGrant.Cash!=0) throw new Exception("Disabled reward granted");
    Console.WriteLine("PASS invalid mapping rolls back signup; disabled reward grants zero");
}
finally
{
    await using var drop = root.CreateCommand($"DROP SCHEMA {schema} CASCADE");
    await drop.ExecuteNonQueryAsync();
}
