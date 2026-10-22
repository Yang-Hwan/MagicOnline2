using MagicOnline.GameApi.Admin;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MagicOnline.GameApi.Pages.Admin;
public class ChartEditModel(AdminChartService charts) : PageModel
{
    [BindProperty(SupportsGet=true)] public string Name { get; set; } = "SkillMatch";
    [BindProperty(SupportsGet=true)] public int? Row { get; set; }
    [BindProperty] public string? Revision { get; set; }
    [BindProperty] public Dictionary<string,string> Values { get; set; } = new();
    [BindProperty] public string? Reason { get; set; }
    public ChartField[] Fields => AdminChartService.Fields(Name);
    public string? Error { get; set; }
    public bool CanEdit => User.IsInRole("Owner") || User.IsInRole("Operator");
    public async Task<IActionResult> OnGetAsync()
    {
        if (!CanEdit) return Forbid(AdminSecurity.Scheme);
        try
        {
            var doc=await charts.Load(Name); Revision=doc.Revision;
            if(Row.HasValue && (Row<0 || Row>=doc.Rows.Count)) return NotFound();
            foreach(var field in Fields) Values[field.Key]=Row.HasValue ? doc.Rows[Row.Value]?[field.Key]?.ToString() ?? "" : "";
            if(!Row.HasValue && Name=="Attendance") Values["DayNum"]=(doc.Rows.Count+1).ToString();
            ViewData["Title"]=Row.HasValue ? "차트 항목 수정" : "차트 항목 추가";
            return Page();
        }
        catch(ApiFault e) when(e.Status==404) { return NotFound(); }
    }
    public async Task<IActionResult> OnPostAsync()
    {
        if(!CanEdit) return Forbid(AdminSecurity.Scheme);
        try
        {
            if(!ModelState.IsValid) throw new ApiFault(400,"입력 내용을 확인해 주세요.");
            await charts.Save(User,Name,Row,Revision,Values,Reason,HttpContext.TraceIdentifier);
            TempData["ChartSuccess"]=Row.HasValue ? "차트 항목을 수정했습니다." : "차트 항목을 추가했습니다.";
            return Redirect("/admin/charts?name="+Uri.EscapeDataString(Name));
        }
        catch(ApiFault e)
        {
            if(e.Status==404) return NotFound();
            Error=e.Message; Response.StatusCode=e.Status;
            ViewData["Title"]="차트 저장 확인";
            return Page();
        }
    }
}
