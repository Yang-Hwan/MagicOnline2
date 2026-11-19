using System.Security.Claims;
using System.Text.Json;
using Assets.Scripts.Prack.BackSys.GameData;
using MagicOnline.MatchSettlement;
using Npgsql;

namespace MagicOnline.GameApi;
public static class CueProgress
{
    public sealed record PracticePoint(Guid RunId,int Shot,long CueId);
    public static (int Min,int Max) TimeRange(NpgsqlConnection c)
    {
        using var cmd=Database.Command(c,"SELECT rows::text FROM charts WHERE name='CommonCode'");
        using var doc=JsonDocument.Parse((string)cmd.ExecuteScalar()!);
        var rows=doc.RootElement.EnumerateArray().Where(r=>r.GetProperty("GrpCd").ToString()=="Cue" && r.GetProperty("ComCd").ToString()=="Sec").ToArray();
        if(rows.Length!=1 || rows[0].GetProperty("IsUse").ToString()!="Y" || !int.TryParse(rows[0].GetProperty("ComVal").ToString(),out var min) || !int.TryParse(rows[0].GetProperty("ComVal2").ToString(),out var max) || min<1 || min>max || max>300)
            throw new ApiFault(409,"공통코드 Cue/Sec의 사용 여부와 최저·최고 시간(1~300초)을 확인해 주세요.");
        return (min,max);
    }
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/me/cue-progress/practice", async (PracticePoint input,ClaimsPrincipal caller,Database db,MatchEquipment equipment)=>{
            if(input.RunId==Guid.Empty || input.Shot<1 || input.Shot>1000000 || input.CueId<1)throw new ApiFault(400,"InvalidCueProgress");
            var user=Guid.Parse(caller.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await using(var c=await db.Source.OpenConnectionAsync())
            await using(var tx=await c.BeginTransactionAsync())
            {
                await using(var gate=Database.Command(c,"SELECT user_id FROM wallets WHERE user_id=$1 FOR UPDATE",user))await gate.ExecuteScalarAsync();
                await using(var check=Database.Command(c,"SELECT EXISTS(SELECT FROM shop_user_inventory WHERE user_id=$1 AND product_id=$2)",user,input.CueId))
                    if(await check.ExecuteScalarAsync() is not true)throw new ApiFault(403,"EquipmentNotOwned");
                Award(c,user,input.CueId,"Practice:"+input.RunId.ToString("N")+":"+input.Shot,1);
                await tx.CommitAsync();
            }
            return await equipment.CurrentProfile(user);
        }).RequireAuthorization();
    }
    static void Award(NpgsqlConnection c,Guid user,long cue,string key,int shots)
    {
        if(cue<=0 || shots<=0)return;
        using var add=Database.Command(c,"INSERT INTO shop_cue_progress_events(user_id,event_key,product_id,points) VALUES($1,$2,$3,$4) ON CONFLICT DO NOTHING RETURNING 1",user,key,cue,shots);
        if(add.ExecuteScalar()==null)return;
        using var progress=Database.Command(c,"""
            INSERT INTO shop_cue_progress(user_id,product_id,shots,steps,progress)
            SELECT $1,$2,$3,$3/(COALESCE(i.cue_ability,c.ability)->>'Reach')::int,$3%(COALESCE(i.cue_ability,c.ability)->>'Reach')::int FROM shop_cues c JOIN shop_user_inventory i ON i.product_id=c.product_id AND i.user_id=$1 WHERE c.product_id=$2
            ON CONFLICT(user_id,product_id) DO UPDATE SET shots=shop_cue_progress.shots+excluded.shots,
                steps=shop_cue_progress.steps+(shop_cue_progress.progress+excluded.shots)/(SELECT (COALESCE(i.cue_ability,c.ability)->>'Reach')::int FROM shop_cues c JOIN shop_user_inventory i ON i.product_id=c.product_id AND i.user_id=$1 WHERE c.product_id=$2),
                progress=(shop_cue_progress.progress+excluded.shots)%(SELECT (COALESCE(i.cue_ability,c.ability)->>'Reach')::int FROM shop_cues c JOIN shop_user_inventory i ON i.product_id=c.product_id AND i.user_id=$1 WHERE c.product_id=$2)
            """,user,cue,(long)shots);
        progress.ExecuteNonQuery();
    }
    // Invoked only in the first committed settlement, in the existing wallet transaction.
    internal static void Settle(NpgsqlConnection c,RegisteredMatch match,SettlementReceipt receipt)
    {
        if(receipt.ResultA is not ("Win" or "Lose" or "Draw") || match.Registration.Rules==null || match.Registration.Rules.Version<5)return;
        using var query=Database.Command(c,"SELECT data::text FROM match_equipment_snapshots WHERE match_id=$1",match.Registration.MatchId);
        if(query.ExecuteScalar() is not string json)return;
        var snapshot=JsonSerializer.Deserialize<MatchEquipmentSnapshot>(json,Database.Json)!;
        var counts=new int[2];
        foreach(var row in (match.AgreedCoinEvents??"").Split(';',StringSplitOptions.RemoveEmptyEntries))
        {var values=row.Split(',');counts[int.Parse(values[1])]++;}
        for(int seat=0;seat<2;seat++)Award(c,Guid.Parse(snapshot.Players[seat].UserId),snapshot.Players[seat].CueId,"Match:"+match.Registration.MatchId,counts[seat]);
    }
}
