using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using MagicOnline.GameApi.Admin;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;

namespace MagicOnline.GameApi.Pages.Admin;

public class ShopEditModel(Database db) : PageModel
{
    public static readonly string[] Emotions=["기본","감사","미안","미소","환희","짜증","우울","좌절","분노"];
    [BindProperty] public Product Input { get; set; } = new();
    public class Product
    {
        public string Kind { get; set; } = "";
        public int Revision { get; set; }
        [Required(ErrorMessage="상품명을 입력해 주세요."),StringLength(120)] public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string? Thumbnail { get; set; }
        public int? Price { get; set; }
        public int Order { get; set; }
        public bool Visible { get; set; }
        public bool Purchasable { get; set; }
        public DateTime? Starts { get; set; }
        public DateTime? Ends { get; set; }
        public int Amount { get; set; }
        public int Bonus { get; set; }
        public string? Visual { get; set; }
        public int Power { get; set; }
        public int Follow { get; set; }
        public int Tip { get; set; }
        public int Seconds { get; set; }
        public Dictionary<string,string> Faces { get; set; } = new();
        public string? Personality { get; set; }
        public string? Messages { get; set; }
        public string[] Lines => (Messages??"").Split('\n',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);
    }
    bool CanEdit => User.IsInRole("Owner") || User.IsInRole("Operator");

    public async Task<IActionResult> OnGetAsync(long? id,string? kind)
    {
        if(!CanEdit) return Forbid(AdminSecurity.Scheme);
        if(id==null){
            if(kind==null || !ShopModel.Kinds.ContainsKey(kind)) return BadRequest();
            Input.Kind=kind;
            return Page();
        }
        await using var c=await db.Source.OpenConnectionAsync();
        await using var cmd=Database.Command(c,"""
            SELECT p.kind,p.name,p.description,p.thumbnail,p.price_gem,p.sort_order,p.visible,p.purchasable,
                p.sale_starts_at AT TIME ZONE 'Asia/Seoul',p.sale_ends_at AT TIME ZONE 'Asia/Seoul',p.revision,
                g.amount,g.bonus,q.visual,q.power_pct,q.follow_pct,q.tip_pct,q.shot_seconds,m.personality,
                (SELECT string_agg(message,chr(10) ORDER BY position) FROM shop_messages WHERE product_id=p.id),
                (SELECT json_object_agg(emotion,asset_key)::text FROM shop_character_emotions WHERE product_id=p.id)
            FROM shop_products p LEFT JOIN shop_gem_packs g ON g.product_id=p.id
            LEFT JOIN shop_cues q ON q.product_id=p.id LEFT JOIN shop_message_packs m ON m.product_id=p.id WHERE p.id=$1
            """,id.Value);
        await using var r=await cmd.ExecuteReaderAsync();
        if(!await r.ReadAsync()) return NotFound();
        string? Str(int i)=>r.IsDBNull(i)?null:r.GetString(i);
        int Num(int i)=>r.IsDBNull(i)?0:r.GetInt32(i);
        Input=new Product {Kind=r.GetString(0),Name=r.GetString(1),Description=Str(2),Thumbnail=Str(3),
            Price=r.IsDBNull(4)?null:Num(4),Order=Num(5),Visible=r.GetBoolean(6),Purchasable=r.GetBoolean(7),
            Starts=r.IsDBNull(8)?null:r.GetDateTime(8),Ends=r.IsDBNull(9)?null:r.GetDateTime(9),Revision=Num(10),
            Amount=Num(11),Bonus=Num(12),Visual=Str(13),Power=Num(14),Follow=Num(15),Tip=Num(16),Seconds=Num(17),
            Personality=Str(18),Messages=Str(19),Faces=System.Text.Json.JsonSerializer.Deserialize<Dictionary<string,string>>(Str(20)??"{}")!};
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(long? id)
    {
        if(!CanEdit) return Forbid(AdminSecurity.Scheme);
        var p=Input;
        if(!ShopModel.Kinds.ContainsKey(p.Kind)) return BadRequest();
        string? error=p.Kind switch {
            "GemPack" when p.Amount<=0 || p.Bonus<0 => "기본 보석은 1 이상, 추가 보석은 0 이상으로 입력해 주세요.",
            "Cue" when string.IsNullOrWhiteSpace(p.Visual) || p.Power is <-40 or >20 || p.Follow is <-40 or >20 || p.Tip is <-10 or >10 || p.Seconds is <-5 or >5 => "큐대 리소스와 성능 범위를 확인해 주세요.",
            "Character" when Emotions.Any(e=>!p.Faces.TryGetValue(e,out var key)||string.IsNullOrWhiteSpace(key)) => "감정 9종의 이미지 경로를 모두 입력해 주세요.",
            "MessagePack" when string.IsNullOrWhiteSpace(p.Personality) || p.Lines.Length==0 => "성격과 메시지를 한 줄 이상 입력해 주세요.",
            _ => null
        };
        if(p.Kind!="GemPack" && p.Price is null or <0) error="보석 가격은 0 이상으로 입력해 주세요.";
        if(p.Starts>=p.Ends) error="판매 종료 시각은 시작 시각보다 늦어야 합니다.";
        if(error!=null) ModelState.AddModelError("",error);
        if(!ModelState.IsValid) return Page();
        await using var c=await db.Source.OpenConnectionAsync();
        await using var tx=await c.BeginTransactionAsync();
        async Task Run(string sql,params object[] args){
            await using var command=Database.Command(c,sql,args);
            await command.ExecuteNonQueryAsync();
        }
        async Task Asset(string? key){
            if(!string.IsNullOrWhiteSpace(key)) await Run("INSERT INTO shop_content_assets(asset_key) VALUES($1) ON CONFLICT DO NOTHING",key.Trim());
        }
        await Asset(p.Thumbnail);
        object Db(object? value)=>value??DBNull.Value;
        object Time(DateTime? value)=>Db(value.HasValue?new DateTimeOffset(DateTime.SpecifyKind(value.Value,DateTimeKind.Unspecified),TimeSpan.FromHours(9)).UtcDateTime:null);
        object[] common=[p.Kind,p.Name.Trim(),p.Description??"",Db(p.Thumbnail?.Trim()),Db(p.Kind=="GemPack"?null:p.Price),
            p.Order,p.Visible,p.Purchasable,Time(p.Starts),Time(p.Ends)];
        const string insert="""
            INSERT INTO shop_products(kind,name,description,thumbnail,price_gem,sort_order,visible,purchasable,sale_starts_at,sale_ends_at)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10) RETURNING id
            """;
        const string update="""
            UPDATE shop_products SET name=$2,description=$3,thumbnail=$4,price_gem=$5,sort_order=$6,visible=$7,
                purchasable=$8,sale_starts_at=$9,sale_ends_at=$10,revision=revision+1,updated_at=now()
            WHERE kind=$1 AND id=$11 AND revision=$12 RETURNING id
            """;
        await using var save=Database.Command(c,id.HasValue?update:insert,id.HasValue?[..common,id.Value,p.Revision]:common);
        if(await save.ExecuteScalarAsync() is not long product){
            Response.StatusCode=409;
            ModelState.AddModelError("","상품이 변경되었습니다. 목록에서 다시 열어 주세요.");
            return Page();
        }
        switch(p.Kind){
            case "GemPack":
                await Run("INSERT INTO shop_gem_packs VALUES($1,$2,$3) ON CONFLICT(product_id) DO UPDATE SET amount=excluded.amount,bonus=excluded.bonus",product,p.Amount,p.Bonus);
                break;
            case "Cue":
                await Asset(p.Visual);
                await Run("""
                    INSERT INTO shop_cues VALUES($1,$2,$3,$4,$5,$6) ON CONFLICT(product_id) DO UPDATE SET visual=excluded.visual,
                    power_pct=excluded.power_pct,follow_pct=excluded.follow_pct,tip_pct=excluded.tip_pct,shot_seconds=excluded.shot_seconds
                    """,product,p.Visual!.Trim(),p.Power,p.Follow,p.Tip,p.Seconds);
                break;
            case "Character":
                foreach(var emotion in Emotions){
                    var key=p.Faces[emotion].Trim();
                    await Asset(key);
                    await Run("INSERT INTO shop_character_emotions VALUES($1,$2,$3) ON CONFLICT(product_id,emotion) DO UPDATE SET asset_key=excluded.asset_key",product,emotion,key);
                }
                break;
            case "MessagePack":
                await Run("INSERT INTO shop_message_packs VALUES($1,$2) ON CONFLICT(product_id) DO UPDATE SET personality=excluded.personality",product,p.Personality!.Trim());
                await Run("DELETE FROM shop_messages WHERE product_id=$1",product);
                var lines=p.Lines;
                for(int i=0;i<lines.Length;i++) await Run("INSERT INTO shop_messages VALUES($1,$2,$3)",product,i+1,lines[i]);
                break;
        }
        await Run("INSERT INTO admin_audit_logs(admin_id,action,target,request_id,after_value) VALUES($1,$2,$3,$4,$5::jsonb)",
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),id.HasValue?"ShopProductUpdated":"ShopProductAdded",
            $"ShopProduct/{product}",HttpContext.TraceIdentifier,System.Text.Json.JsonSerializer.Serialize(p));
        await tx.CommitAsync();
        TempData["ShopSaved"]=$"{p.Name} 상품을 저장했습니다.";
        return RedirectToPage("Shop");
    }
}
