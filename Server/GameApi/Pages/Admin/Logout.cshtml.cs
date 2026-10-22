using MagicOnline.GameApi.Admin;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MagicOnline.GameApi.Pages.Admin;
public class LogoutModel(AdminAccounts accounts) : PageModel
{
    public IActionResult OnGet() => Redirect("/admin");
    public async Task<IActionResult> OnPostAsync() { await accounts.Logout(User, HttpContext.TraceIdentifier); await HttpContext.SignOutAsync(AdminSecurity.Scheme); return LocalRedirect("/admin/login"); }
}
