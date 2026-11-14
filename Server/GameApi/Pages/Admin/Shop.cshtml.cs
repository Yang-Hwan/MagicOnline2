using MagicOnline.GameApi.Admin;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MagicOnline.GameApi.Pages.Admin;

public class ShopModel(Database db) : PageModel
{
    public static readonly Dictionary<string,string> Kinds = new()
    {
        ["GemPack"]="보석 팩", ["Cue"]="큐대", ["Character"]="캐릭터", ["MessagePack"]="메시지 묶음"
    };
    public record Item(long Id,string Kind,string Name,int? Price,int Order,bool Visible,bool Purchasable);
    public string Kind { get; private set; } = "";
    public List<Item> Items { get; } = [];
    public bool CanEdit => User.IsInRole("Owner") || User.IsInRole("Operator");

    public async Task OnGetAsync(string? kind)
    {
        ViewData["Title"]="상점 상품";
        Kind = kind != null && Kinds.ContainsKey(kind) ? kind : "";
        await using var cmd=db.Source.CreateCommand("SELECT id,kind,name,price_gem,sort_order,visible,purchasable FROM shop_products WHERE ($1='' OR kind=$1) ORDER BY sort_order,id");
        cmd.Parameters.AddWithValue(Kind);
        await using var rows=await cmd.ExecuteReaderAsync();
        while(await rows.ReadAsync())
            Items.Add(new(rows.GetInt64(0),rows.GetString(1),rows.GetString(2),rows.IsDBNull(3)?null:rows.GetInt32(3),rows.GetInt32(4),rows.GetBoolean(5),rows.GetBoolean(6)));
    }
}
