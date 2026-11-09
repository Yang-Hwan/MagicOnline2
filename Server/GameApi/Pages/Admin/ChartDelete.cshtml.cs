using MagicOnline.GameApi.Admin;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MagicOnline.GameApi.Pages.Admin;
public class ChartDeleteModel(AdminChartService charts) : PageModel
{
    [BindProperty(SupportsGet = true)] public string Name { get; set; } = "SkillMatch";
    [BindProperty(SupportsGet = true)] public int? Row { get; set; }
    [BindProperty] public string? Revision { get; set; }
    [BindProperty] public string? Reason { get; set; }
    public Dictionary<string, string> Values { get; set; } = new();
    public ChartField[] Fields => AdminChartService.Fields(Name);
    public string? Error { get; set; }
    public bool CanDelete => User.IsInRole("Owner") || User.IsInRole("Operator");
    public async Task<IActionResult> OnGetAsync()
    {
        if (!CanDelete) return Forbid(AdminSecurity.Scheme);
        try
        {
            var doc = await charts.Load(Name);
            if (!ModelState.IsValid || !Row.HasValue || Row < 0 || Row >= doc.Rows.Count) return NotFound();
            Revision = doc.Revision;
            foreach (var field in Fields) Values[field.Key] = doc.Rows[Row.Value]?[field.Key]?.ToString() ?? "";
            if (Name=="PostReward")
            {
                var common=(await charts.Load("CommonCode")).Rows;
                foreach (var (key,group) in new[]{("TypeCode","PostRewardType"),("CurrencyCode","Wealth")})
                    if(long.TryParse(Values[key],out var number))
                        Values[key]=PostRewardChart.Codes(common,group).GetValueOrDefault(number,Values[key]);
            }
            ViewData["Title"] = "차트 항목 삭제";
            return Page();
        }
        catch (ApiFault e) when (e.Status == 404) { return NotFound(); }
    }
    public async Task<IActionResult> OnPostAsync()
    {
        if (!CanDelete) return Forbid(AdminSecurity.Scheme);
        try
        {
            if (!ModelState.IsValid || !Row.HasValue) throw new ApiFault(400, "삭제할 항목을 목록에서 다시 선택해 주세요.");
            await charts.Delete(User, Name, Row.Value, Revision, Reason, HttpContext.TraceIdentifier);
            TempData["ChartSuccess"] = "차트 항목을 삭제했습니다.";
            return Redirect("/admin/charts?name=" + Uri.EscapeDataString(Name));
        }
        catch (ApiFault e)
        {
            if (e.Status == 404) return NotFound();
            Error = e.Message; Response.StatusCode = e.Status;
            ViewData["Title"] = "차트 삭제 확인";
            return Page();
        }
    }
}
