using MagicOnline.GameApi.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace MagicOnline.GameApi.Pages.Admin;

[AllowAnonymous, EnableRateLimiting("admin-login")]
public class SetupModel(AdminAccounts accounts, IWebHostEnvironment env) : PageModel
{
    [BindProperty] public string LoginId { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    [BindProperty] public string ConfirmPassword { get; set; } = "";
    public string? Error { get; set; }
    async Task<bool> Allowed() => AdminSecurity.LocalSetup(HttpContext, env) && !await accounts.HasOwner();
    public async Task<IActionResult> OnGetAsync() => AdminSecurity.DevelopmentLogin(HttpContext, env) ? LocalRedirect("/admin/login") : await Allowed() ? Page() : NotFound();
    public async Task<IActionResult> OnPostAsync()
    {
        if (AdminSecurity.DevelopmentLogin(HttpContext, env)) return LocalRedirect("/admin/login");
        if (!await Allowed()) return NotFound();
        try {
            if (Password != ConfirmPassword) throw new ApiFault(400,"비밀번호 확인이 일치하지 않습니다.");
            await accounts.CreateFirst(LoginId, Password, HttpContext.TraceIdentifier);
            return LocalRedirect("/admin/login");
        }
        catch (ApiFault error) { Error = error.Message; Password = ConfirmPassword = ""; ModelState.Remove(nameof(Password)); ModelState.Remove(nameof(ConfirmPassword)); return Page(); }
    }
}
