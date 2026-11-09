using MagicOnline.GameApi.Admin;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MagicOnline.GameApi.Pages.Admin;
public class MemberModel(AdminReadService read, AdminMemberService members) : PageModel
{
    public Guid Id { get; set; }
    public string Tab { get; set; } = "profile";
    public AdminMember Data { get; set; } = null!;
    public AdminTable Balance { get; set; } = null!;
    public AdminTable MemberMail { get; set; } = null!;
    public AdminTable AttendanceHistory { get; set; } = null!;
    public AdminTable BustHistory { get; set; } = null!;
    [BindProperty] public string? EditNickname { get; set; }
    [BindProperty] public string? OriginalNickname { get; set; }
    [BindProperty] public string? EditReason { get; set; }
    [TempData] public string? SuccessMessage { get; set; }
    public string? EditError { get; set; }
    public bool CanEdit => User.IsInRole("Owner") || User.IsInRole("Operator");
    public Guid Operation { get; set; } = Guid.NewGuid();

    public async Task<IActionResult> OnPostWalletAsync(Guid id, string currency, string direction, long amount, Guid operation, string? reason)
    {
        if (!CanEdit) return Forbid(AdminSecurity.Scheme);
        try
        {
            await members.AdjustWallet(User, id, currency, direction, amount, operation, reason);
            SuccessMessage = $"{currency} {amount:N0}개 {(direction == "credit" ? "증액" : "차감")}을 처리했습니다.";
            return RedirectToPage(new { id, tab = "wallet" });
        }
        catch (ApiFault error)
        {
            if (error.Status == 404) return NotFound();
            if (error.Status == 403) return Forbid(AdminSecurity.Scheme);
            EditError = error.Message;
            Response.StatusCode = error.Status;
            return await OnGetAsync(id, "wallet");
        }
    }
    public async Task<IActionResult> OnGetAsync(Guid id, string? tab, int page = 1)
    {
        Id=id; Tab=tab is "mail" or "attendance" or "bust" or "games" or "wallet" or "payments" or "access" or "notes" ? tab : "profile";
        try { Data=await read.Member(id); Balance=await read.WalletCheck(id); if(Tab=="mail") MemberMail=await read.MemberMail(id,page); if(Tab=="bust") BustHistory=await read.BustHistory(id,page); if(Tab=="attendance") AttendanceHistory=await read.AttendanceHistory(id,page); }
        catch(ApiFault e) when(e.Status==404){return NotFound();}
        EditNickname=OriginalNickname=Data.Profile.Rows[0][2];
        ViewData["Title"]="회원 상세"; return Page();
    }
    public async Task<IActionResult> OnPostNicknameAsync(Guid id)
    {
        if (!CanEdit) return Forbid(AdminSecurity.Scheme);
        try
        {
            await members.Rename(User,id,EditNickname,OriginalNickname,EditReason,HttpContext.TraceIdentifier);
            SuccessMessage="닉네임을 저장했습니다.";
            return RedirectToPage(new { id, tab="profile" });
        }
        catch(ApiFault error)
        {
            if(error.Status==404)return NotFound();
            if(error.Status==403)return Forbid(AdminSecurity.Scheme);
            Id=id; Tab="profile"; EditError=error.Message;
            Data=await read.Member(id); Balance=await read.WalletCheck(id);
            OriginalNickname=Data.Profile.Rows[0][2];
            ModelState.Remove(nameof(OriginalNickname));
            ViewData["Title"]="회원 상세";
            Response.StatusCode=error.Status;
            return Page();
        }
    }
}
