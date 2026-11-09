using System.Text.Json;
using MagicOnline.GameApi.Admin;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MagicOnline.GameApi.Pages.Admin;

public class ChartTransferModel(AdminChartService charts) : PageModel
{
    public string? Error { get; private set; }
    bool CanImport => User.IsInRole("Owner") || User.IsInRole("Operator");

    public IActionResult OnGet()
    {
        ViewData["Title"] = "차트 가져오기";
        return CanImport ? Page() : Forbid(AdminSecurity.Scheme);
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        var options = new JsonSerializerOptions { WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All) };
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(await charts.ExportCharts(), options);
            return File(bytes, "application/json", $"MagicCushion-charts-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}Z.json");
        }
        catch (ApiFault error) { return StatusCode(error.Status,error.Message); }
    }

    public async Task<IActionResult> OnPostAsync(IFormFile? bundle)
    {
        if (!CanImport) return Forbid(AdminSecurity.Scheme);
        ViewData["Title"] = "차트 가져오기";
        try
        {
            if (bundle == null || bundle.Length == 0) throw new ApiFault(400,"차트 JSON 파일을 선택해 주세요.");
            using var input = bundle.OpenReadStream();
            await charts.ImportCharts(User,input,HttpContext.TraceIdentifier);
            TempData["ChartSuccess"] = "차트 4종을 적용했습니다.";
            return Redirect("/admin/charts");
        }
        catch (JsonException) { Error = "JSON 형식을 읽을 수 없습니다. 내보낸 원본 파일을 확인해 주세요."; }
        catch (ApiFault error) { Error = error.Message; Response.StatusCode = error.Status; return Page(); }
        catch (Npgsql.PostgresException e) when (e.SqlState is "22P05" or "22P02" or "22003")
        { Error = "DB에 저장할 수 없는 문자 또는 숫자가 포함되어 있습니다."; }
        Response.StatusCode = 400;
        return Page();
    }
}
