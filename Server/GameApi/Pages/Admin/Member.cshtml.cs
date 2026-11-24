using MagicOnline.GameApi.Admin;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MagicOnline.GameApi.Pages.Admin;
public class MemberModel(AdminReadService read, AdminMemberService members, Database db) : PageModel
{
    public sealed record MemberCue(long Id,string Name,bool Equipped,string Grade,int[] Initial,int[] Levels,int[] Maximum,int[] Effective,long Shots,long Steps,long Pending,int Reach)
    {
        public bool Maxed => Levels.SequenceEqual(Maximum);
    }
    public List<MemberCue> Cues { get; } = new();
    async Task LoadCues(Guid user)
    {
        await using var cmd=db.Source.CreateCommand("""
            SELECT p.id,p.name,EXISTS(SELECT FROM shop_user_equipment e WHERE e.user_id=i.user_id AND e.product_id=p.id AND e.slot='Cue'),
                   COALESCE(i.cue_ability,c.ability)::text,COALESCE(g.shots,0),COALESCE(g.steps,0),COALESCE(g.progress,0)
            FROM shop_user_inventory i JOIN shop_products p ON p.id=i.product_id
            JOIN shop_cues c ON c.product_id=p.id
            LEFT JOIN shop_cue_progress g ON g.user_id=i.user_id AND g.product_id=i.product_id
            WHERE i.user_id=$1 ORDER BY 3 DESC,p.sort_order,p.id
            """);
        cmd.Parameters.AddWithValue(user);
        await using var r=await cmd.ExecuteReaderAsync();
        while(await r.ReadAsync())
        {
            var ability=System.Text.Json.JsonSerializer.Deserialize<Assets.Scripts.Prack.BackSys.GameData.CueAbilitySettings>(r.GetString(3),Database.Json)!;
            long steps=r.GetInt64(5)+r.GetInt64(6)/ability.Reach;
            var levels=Assets.Scripts.Prack.BackSys.GameData.CueAbilitySettings.WithGuide(ability.LevelsFromSteps(steps));
            var effective=levels.Select(ability.Percent).ToArray();effective[4]=ability.GuidePercent(steps);
            Cues.Add(new(r.GetInt64(0),r.GetString(1),r.GetBoolean(2),ability.Grade,Assets.Scripts.Prack.BackSys.GameData.CueAbilitySettings.WithGuide(ability.Initial),levels,Assets.Scripts.Prack.BackSys.GameData.CueAbilitySettings.WithGuide(ability.Maximum),
                effective,r.GetInt64(4),steps,r.GetInt64(6)%ability.Reach,ability.Reach));
        }
    }
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

    public async Task<IActionResult> OnPostResetCueAsync(Guid id,long cueId,Guid operation,string? reason,bool confirmReset)
    {
        if(!CanEdit)return Forbid(AdminSecurity.Scheme);
        try {
            if(!confirmReset)throw new ApiFault(400,"초기화 내용 확인에 체크해 주세요.");
            await members.ResetCue(User,id,cueId,operation,reason);
            SuccessMessage="해당 큐대에 상점의 최신 능력치 설정을 적용하고 레벨·진도를 초기화했습니다. 다음 경기 또는 연습 재입장부터 반영됩니다.";
            return RedirectToPage(new{id,tab="cues"});
        } catch(ApiFault e) {
            if(e.Status==403)return Forbid(AdminSecurity.Scheme);
            if(e.Status==404)return NotFound();
            EditError=e.Message;Response.StatusCode=e.Status;return await OnGetAsync(id,"cues");
        }
    }
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
        Id=id; Tab=tab is "cues" or "mail" or "attendance" or "bust" or "games" or "wallet" or "payments" or "access" or "notes" ? tab : "profile";
        try { Data=await read.Member(id); Balance=await read.WalletCheck(id); if(Tab=="cues") await LoadCues(id); if(Tab=="mail") MemberMail=await read.MemberMail(id,page); if(Tab=="bust") BustHistory=await read.BustHistory(id,page); if(Tab=="attendance") AttendanceHistory=await read.AttendanceHistory(id,page); }
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
