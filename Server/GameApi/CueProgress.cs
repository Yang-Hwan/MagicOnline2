using System.Security.Claims;
using System.Text.Json;
using Assets.Scripts.Prack.BackSys.GameData;
using MagicOnline.MatchSettlement;
using Npgsql;

namespace MagicOnline.GameApi;
public static class CueProgress
{
    public sealed record PracticePoint(Guid RunId,int Shot,long CueId,int Points=1);
    public sealed record BowlAction(long CueId,Guid Operation,long Cycle,int Index=-1);
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
        app.MapPost("/api/me/cue-bowls/drink", (BowlAction input,ClaimsPrincipal caller,Database db)=>ChangeBowl(input,caller,db)).RequireAuthorization();
        app.MapPost("/api/me/cue-progress/practice", async (PracticePoint input,ClaimsPrincipal caller,Database db,MatchEquipment equipment)=>{
            if(input.RunId==Guid.Empty || input.Shot<1 || input.Shot>1000000 || input.CueId<1 || input.Points<1 || input.Points>2)throw new ApiFault(400,"InvalidCueProgress");
            var user=Guid.Parse(caller.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await using(var c=await db.Source.OpenConnectionAsync())
            await using(var tx=await c.BeginTransactionAsync())
            {
                await using(var gate=Database.Command(c,"SELECT user_id FROM wallets WHERE user_id=$1 FOR UPDATE",user))await gate.ExecuteScalarAsync();
                await using(var check=Database.Command(c,"SELECT EXISTS(SELECT FROM shop_user_inventory WHERE user_id=$1 AND product_id=$2)",user,input.CueId))
                    if(await check.ExecuteScalarAsync() is not true)throw new ApiFault(403,"EquipmentNotOwned");
                Award(c,user,input.CueId,"Practice:"+input.RunId.ToString("N")+":"+input.Shot,input.Points);
                await tx.CommitAsync();
            }
            return await equipment.CurrentProfile(user);
        }).RequireAuthorization();
    }
    sealed record Growth(CueBowlState Bowls,int[] NextMaximum,int Reach,long Steps,long Progress,long Shots);
    static Growth Load(NpgsqlConnection c,Guid user,long cue)
    {
        using var cmd=Database.Command(c,"""
            SELECT COALESCE(i.cue_ability,c.ability)::text,c.ability::text,g.bowls::text,
                   COALESCE(g.steps,0),COALESCE(g.progress,0),COALESCE(g.shots,0)
            FROM shop_user_inventory i JOIN shop_cues c ON c.product_id=i.product_id
            LEFT JOIN shop_cue_progress g ON g.user_id=i.user_id AND g.product_id=i.product_id
            WHERE i.user_id=$1 AND i.product_id=$2 FOR SHARE OF c
            """,user,cue);
        using var r=cmd.ExecuteReader();if(!r.Read())throw new ApiFault(403,"EquipmentNotOwned");
        var ability=JsonSerializer.Deserialize<CueAbilitySettings>(r.GetString(0),Database.Json)!;
        var latest=JsonSerializer.Deserialize<CueAbilitySettings>(r.GetString(1),Database.Json)!;
        var state=r.IsDBNull(2)?new CueBowlState{Maximum=(int[])latest.BowlMaximum.Clone()}:JsonSerializer.Deserialize<CueBowlState>(r.GetString(2),Database.Json)!;
        state.Auto=latest.BowlAuto;
        return new(state,latest.BowlMaximum,ability.Reach,r.GetInt64(3),r.GetInt64(4),r.GetInt64(5));
    }
    static void Save(NpgsqlConnection c,Guid user,long cue,Growth growth,long accepted,long rounds)
    {
        long progress=growth.Progress+rounds;
        using var cmd=Database.Command(c,"""
            INSERT INTO shop_cue_progress(user_id,product_id,shots,steps,progress,bowls) VALUES($1,$2,$3,$4,$5,$6::jsonb)
            ON CONFLICT(user_id,product_id) DO UPDATE SET shots=excluded.shots,steps=excluded.steps,progress=excluded.progress,bowls=excluded.bowls
            """,user,cue,growth.Shots+accepted,growth.Steps+progress/growth.Reach,progress%growth.Reach,JsonSerializer.Serialize(growth.Bowls,Database.Json));
        cmd.ExecuteNonQuery();
    }
    static long ApproveAutomatic(Growth growth)
    {
        var state=growth.Bowls;if(!state.Auto)return 0;
        for(int i=0;i<5;i++)if(state.Ready(i))state.Drunk[i]=true;
        if(!state.Complete)return 0;state.Reset(growth.NextMaximum);return 1;
    }
    // Product policy changes apply on the next member read/score, including already-full bowls.
    public static async Task SyncPolicies(Database db,Guid user)
    {
        await using var c=await db.Source.OpenConnectionAsync();await using var tx=await c.BeginTransactionAsync();
        using(var gate=Database.Command(c,"SELECT user_id FROM wallets WHERE user_id=$1 FOR UPDATE",user))gate.ExecuteScalar();
        var ids=new List<long>();
        using(var query=Database.Command(c,"""
            SELECT g.product_id FROM shop_cue_progress g JOIN shop_cues c ON c.product_id=g.product_id
            WHERE g.user_id=$1 AND COALESCE((g.bowls->>'Auto')::boolean,false)<>COALESCE((c.ability->>'BowlAuto')::boolean,false)
            """,user))using(var r=query.ExecuteReader())while(r.Read())ids.Add(r.GetInt64(0));
        foreach(long cue in ids){var growth=Load(c,user,cue);Save(c,user,cue,growth,0,ApproveAutomatic(growth));}
        await tx.CommitAsync();
    }
    static async Task<CueBowlState> ChangeBowl(BowlAction input,ClaimsPrincipal caller,Database db)
    {
        if(input.CueId<1||input.Operation==Guid.Empty||(input.Index<0||input.Index>4))throw new ApiFault(400,"InvalidBowlAction");
        var user=Guid.Parse(caller.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await using var c=await db.Source.OpenConnectionAsync();await using var tx=await c.BeginTransactionAsync();
        using(var gate=Database.Command(c,"SELECT user_id FROM wallets WHERE user_id=$1 FOR UPDATE",user))gate.ExecuteScalar();
        var growth=Load(c,user,input.CueId);var state=growth.Bowls;
        using(var operation=Database.Command(c,"INSERT INTO shop_cue_bowl_operations(user_id,operation,product_id) VALUES($1,$2,$3) ON CONFLICT DO NOTHING RETURNING 1",user,input.Operation,input.CueId))
        {
            if(operation.ExecuteScalar()==null){await tx.CommitAsync();return state;}
        }
        if(state.Auto)throw new ApiFault(409,"이 큐대는 상품 설정에 따라 자동으로 마십니다.");
        if(state.Cycle!=input.Cycle||!state.Ready(input.Index))throw new ApiFault(409,"가득 찬 바가지만 마실 수 있습니다. 새로고침해 주세요.");
        state.Drunk[input.Index]=true;
        long rounds=0;if(state.Complete){rounds=1;state.Reset(growth.NextMaximum);}
        Save(c,user,input.CueId,growth,0,rounds);await tx.CommitAsync();return state;
    }
    static void Award(NpgsqlConnection c,Guid user,long cue,string key,int shots)
    {
        if(cue<=0 || shots<=0)return;
        // Mark even discarded scoring events as processed: retries cannot recover lost points.
        using var add=Database.Command(c,"INSERT INTO shop_cue_progress_events(user_id,event_key,product_id,points) VALUES($1,$2,$3,$4) ON CONFLICT DO NOTHING RETURNING 1",user,key,cue,shots);
        if(add.ExecuteScalar()==null)return;
        var growth=Load(c,user,cue);long accepted=0,rounds=ApproveAutomatic(growth);
        for(int n=0;n<shots;n++){
            if(!growth.Bowls.Add(System.Security.Cryptography.RandomNumberGenerator.GetInt32(60)))break;
            accepted++;
            if(growth.Bowls.Complete){rounds++;growth.Bowls.Reset(growth.NextMaximum);}
        }
        Save(c,user,cue,growth,accepted,rounds);
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
        {var values=row.Split(',');counts[int.Parse(values[1])]+=int.Parse(values[3]);}
        for(int seat=0;seat<2;seat++)Award(c,Guid.Parse(snapshot.Players[seat].UserId),snapshot.Players[seat].CueId,"Match:"+match.Registration.MatchId,counts[seat]);
    }
}
