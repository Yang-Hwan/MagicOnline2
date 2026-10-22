using MagicOnline.GameApi.Admin;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MagicOnline.GameApi.Pages.Admin;

public class IndexModel(AdminReadService read) : PageModel
{
    public string Section { get; set; } = "dashboard";
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? State { get; set; }
    [BindProperty(SupportsGet = true)] public string? Name { get; set; }
    [BindProperty(SupportsGet = true, Name = "p")] public int Number { get; set; } = 1;
    public AdminDashboard? Dashboard { get; set; }
    public AdminTable? Table { get; set; }
    public AdminTable? Extra { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public bool Planned { get; set; }
    public async Task<IActionResult> OnGetAsync(string? section)
    {
        Section = section ?? "dashboard";
        Number = Math.Clamp(Number, 1, 10000);
        switch (Section)
        {
            case "dashboard": Title="대시보드"; Description="실제 게임 DB의 회원·대전·정산 현황"; Dashboard=await read.Dashboard(); Table=await read.Matches(null,"disputed",1); break;
            case "members": Title="회원 관리"; Description="회원 검색과 프로필·전적·원장 연결 조회"; Table=await read.Members(Q,State,Number); break;
            case "matches": Title="대전 관리"; Description="경기 상태·종료 사유·결과 보고·정산 기록"; Table=await read.Matches(Q,State,Number); break;
            case "wallet": Title="재화 원장"; Description="회원·요청 키·경기 ID별 Coin·Jem 변동"; Table=await read.Ledger(Q,State,Number); if(Guid.TryParse(Q,out var user))Extra=await read.WalletCheck(user); break;
            case "charts": Title="차트 관리"; Description="경기장 규칙·출석 보상·공통 코드 조회 및 추가·수정"; Name=Name is "Attendance" or "CommonCode" ? Name : "SkillMatch"; Table=await read.Chart(Name); break;
            case "system": Title="시스템 관리"; Description="관리자 계정·역할과 인증·회원 변경 기록"; if(User.IsInRole("Owner")){Table=await read.Audit(Number); Extra=await read.Admins();} break;
            case "stats": Title="기본 통계"; Description="현재 누적 집계 · 테스트 계정 포함"; Dashboard=await read.Dashboard(); break;
            case "payments": Title="결제 관리"; Description="Android·iOS 결제 연동은 3차 개발 범위입니다."; Planned=true; break;
            case "operations": Title="운영 관리"; Description="공지·점검·지원 버전은 2차 개발 범위입니다."; Planned=true; break;
            case "support": Title="고객 지원"; Description="운영 메모·문의·신고 연결은 후속 개발 범위입니다."; Planned=true; break;
            default: return NotFound();
        }
        ViewData["Title"]=Title;
        return Page();
    }
    public string PageUrl(int page) => "/admin/"+Section+"?"+Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString("", new Dictionary<string,string?> { ["q"]=Q,["state"]=State,["name"]=Name,["p"]=page.ToString() }).TrimStart('?');
}
