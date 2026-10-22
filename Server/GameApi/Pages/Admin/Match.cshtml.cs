using System.Text.Json;
using MagicOnline.GameApi.Admin;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MagicOnline.GameApi.Pages.Admin;
public class MatchModel(AdminReadService read) : PageModel
{
    public string Id { get; set; } = "";
    public AdminMatch Data { get; set; } = null!;
    public async Task<IActionResult> OnGetAsync(string id)
    {
        Id=id;
        try{Data=await read.Match(id);}catch(ApiFault e)when(e.Status==404){return NotFound();}
        ViewData["Title"]="경기 상세"; return Page();
    }
    public string Pretty(string value){try{return JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(value),new JsonSerializerOptions{WriteIndented=true,Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping});}catch(JsonException){return value;}}
}
