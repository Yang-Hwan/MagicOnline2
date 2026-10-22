using MagicOnline.GameApi.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace MagicOnline.GameApi.Pages.Admin;

[AllowAnonymous, EnableRateLimiting("admin-login")]
public class LoginModel(AdminAccounts accounts, IWebHostEnvironment env) : PageModel
{
    [BindProperty] public string LoginId { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    public string? Error { get; set; }
    public bool CanSetup { get; set; }
    public bool DevelopmentLogin { get; set; }
    public async Task OnGetAsync()
    {
        DevelopmentLogin = AdminSecurity.DevelopmentLogin(HttpContext, env);
        CanSetup = !DevelopmentLogin && AdminSecurity.LocalSetup(HttpContext, env) && !await accounts.HasOwner();
    }
    public async Task<IActionResult> OnPostAsync()
    {
        await OnGetAsync();
        var principal = DevelopmentLogin ? await accounts.LoginForDevelopment(HttpContext, env, LoginId) : await accounts.Login(LoginId, Password, HttpContext.TraceIdentifier);
        Password = ""; ModelState.Remove(nameof(Password));
        if (principal == null) { Error = DevelopmentLogin ? "관리자 ID를 확인해 주세요. 최초 접속 이후에는 등록된 관리자 ID를 입력해 주세요. (영문·숫자·밑줄 3~32자)" : "로그인 정보를 확인해 주세요. 반복 실패 시 잠시 후 다시 시도해 주세요."; return Page(); }
        await HttpContext.SignInAsync(AdminSecurity.Scheme, principal, new AuthenticationProperties { IsPersistent = false, AllowRefresh = false, ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30) });
        return LocalRedirect("/admin");
    }
}
