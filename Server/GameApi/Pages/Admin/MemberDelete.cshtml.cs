using MagicOnline.GameApi.Admin;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MagicOnline.GameApi.Pages.Admin;

public class MemberDeleteModel(AdminMemberService members) : PageModel
{
    [BindProperty] public Guid[] Ids { get; set; } = [];
    [BindProperty] public string[] ExpectedLogins { get; set; } = [];
    [BindProperty] public string? Confirmation { get; set; }
    [BindProperty] public string? Reason { get; set; }
    public List<AdminMemberService.DeleteCandidate> Candidates { get; set; } = [];
    public string? Error { get; set; }
    public bool CanDelete => User.IsInRole("Owner");
    public bool Blocked => Candidates.Any(candidate => !candidate.Deleted && candidate.HasMatch);
    public async Task<IActionResult> OnGetAsync(Guid? id)
    {
        if (id.HasValue) Ids = [id.Value];
        return await Preview();
    }
    public Task<IActionResult> OnPostPreviewAsync() => Preview();
    async Task<IActionResult> Preview()
    {
        if (!CanDelete) return Forbid(AdminSecurity.Scheme);
        ViewData["Title"] = "회원 삭제 확인";
        try
        {
            if (!ModelState.IsValid) throw new ApiFault(400, "회원 목록에서 삭제 대상을 다시 선택해 주세요.");
            Candidates = await members.DeletePreview(User, Ids);
            // Display and hidden IDs follow the same order; never trust an unbound checkbox count.
            Ids = Candidates.Select(c => c.Id).ToArray(); ExpectedLogins = Candidates.Select(c => c.Login).ToArray();
            ModelState.Remove(nameof(Ids)); ModelState.Remove(nameof(ExpectedLogins));
            return Page();
        }
        catch (ApiFault e) { Error = e.Message; Response.StatusCode = e.Status; return Page(); }
    }
    public Task<IActionResult> OnPostAsync() => OnPostDeleteAsync();
    public async Task<IActionResult> OnPostDeleteAsync()
    {
        if (!CanDelete) return Forbid(AdminSecurity.Scheme);
        try
        {
            if (!ModelState.IsValid) throw new ApiFault(400, "삭제 대상과 입력 내용을 확인해 주세요.");
            int count = await members.DeleteMembers(User, Ids, ExpectedLogins, Confirmation, Reason, HttpContext.TraceIdentifier);
            TempData["MemberSuccess"] = count == 0 ? "이미 삭제된 회원입니다." : $"회원 {count}명을 삭제했습니다.";
            return Redirect("/admin/members");
        }
        catch (ApiFault e)
        {
            Error = e.Message; Response.StatusCode = e.Status; ViewData["Title"] = "회원 삭제 확인";
            try { Candidates = await members.DeletePreview(User, Ids); } catch (ApiFault) { }
            return Page();
        }
    }
}
