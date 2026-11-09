using System.Text.Json;
using Npgsql;

namespace MagicOnline.GameApi;

internal sealed record RegistrationReward(string Currency, long Amount)
{
    static string Field(JsonElement row, string key) =>
        row.TryGetProperty(key, out var value) ? value.ToString().Trim() : "";

    static ApiFault Invalid() => new(409, "회원가입 보상 공통코드 설정을 확인해 주세요.");

    static RegistrationReward Read(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var rows = doc.RootElement.EnumerateArray().Where(r => Field(r, "IsUse") == "Y").ToArray();
        var grants = rows.Where(r => Field(r, "GrpCd") == "Bust" && Field(r, "ComCd") == "BustMemReg").ToArray();
        if (grants.Length == 0) return new("", 0);
        if (grants.Length != 1 || !long.TryParse(Field(grants[0], "ComVal"), out var amount) ||
            amount < 0 || !long.TryParse(Field(grants[0], "ComVal2"), out var type)) throw Invalid();
        var types = rows.Where(r => Field(r, "GrpCd") == "Wealth" &&
            long.TryParse(Field(r, "ComVal"), out var code) && code == type).ToArray();
        if (types.Length != 1) throw Invalid();
        string currency = Field(types[0], "ComCd");
        if (currency is not ("Coin" or "Jem" or "Cash" or "Gold") ||
            (currency == "Coin" && amount > 9_000_000_000_000_000L) ||
            (currency == "Jem" && amount > int.MaxValue)) throw Invalid();
        return new(currency, amount);
    }

    public static async Task CreateWallet(NpgsqlConnection connection, Guid user)
    {
        await using var config = Database.Command(connection, "SELECT rows::text FROM charts WHERE name='CommonCode'");
        var reward = Read(await config.ExecuteScalarAsync() as string ?? "[]");
        long coin = reward.Currency == "Coin" ? reward.Amount : 0;
        int jem = reward.Currency == "Jem" ? checked((int)reward.Amount) : 0;
        long cash = reward.Currency == "Cash" ? reward.Amount : 0;
        long gold = reward.Currency == "Gold" ? reward.Amount : 0;
        await using var wallet = Database.Command(connection, "INSERT INTO wallets(user_id,coin,jem,cash,gold) VALUES($1,$2,$3,$4,$5)", user,coin,jem,cash,gold);
        await wallet.ExecuteNonQueryAsync();
        if (reward.Amount == 0) return;
        await using var ledger = Database.Command(connection, "INSERT INTO wallet_ledger(user_id,request_key,coin_delta,jem_delta,cash_delta,gold_delta) VALUES($1,'InitialGrant',$2,$3,$4,$5)", user,coin,jem,cash,gold);
        await ledger.ExecuteNonQueryAsync();
    }
}
