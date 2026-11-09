using System.Globalization;
using System.Text.Json.Nodes;
using Npgsql;
using MagicOnline.GameApi.Admin;
using MagicOnline.MatchSettlement;

namespace MagicOnline.GameApi;

// All calls use the transaction that recorded the originating event.
internal sealed class MemberRewards(JsonArray rules, JsonArray common)
{
    public static MemberRewards Load(NpgsqlConnection c)
    {
        using var cmd=Database.Command(c,"SELECT name,rows::text FROM charts WHERE name IN ('PostReward','CommonCode')");
        using var reader=cmd.ExecuteReader();
        JsonArray rules=new(), common=new();
        while(reader.Read())
        {
            var rows=JsonNode.Parse(reader.GetString(1))!.AsArray();
            if(reader.GetString(0)=="PostReward") rules=rows; else common=rows;
        }
        return new(rules,common);
    }

    public void Send(NpgsqlConnection c,Guid user,string kind,string eventKey,int? value=null, int? attendanceCurrency=null, long? attendanceAmount=null)
    {
        if (rules.Count==0) return;
        var types=PostRewardChart.Codes(common,"PostRewardType");
        var wealth=PostRewardChart.Codes(common,"Wealth");
        foreach(var row in rules)
        {
            var type=(int)row!["TypeCode"]!;
            if(types.GetValueOrDefault(type)!=kind) continue;
            int? min=(int?)row["RangeMin"], max=(int?)row["RangeMax"];
            if(min.HasValue && (value==null || value<min || value>max)) continue;
            string message=row["Message"]!.ToString();
            if(value.HasValue) message=message.Replace("?",value.Value.ToString(CultureInfo.InvariantCulture));
            int currency=attendanceCurrency ?? (int)row["CurrencyCode"]!;
            long amount=attendanceAmount ?? (long)row["RewardVal"]!;
            using var insert=Database.Command(c,"""
                INSERT INTO user_post_reward(user_id,event_key,reward_id,type_code,type_name,message,sent_at,expires_at,currency_code,currency_name,reward_val)
                VALUES ($1,$2,$3,$4,$5,$6,now(),now()+$7 * interval '24 hours',$8,$9,$10)
                ON CONFLICT (user_id,event_key,reward_id) DO NOTHING
                """,user,eventKey,(int)row["RewardId"]!,type,kind,message,(int)row["KeepDays"]!,currency,wealth[currency],amount);
            insert.ExecuteNonQuery();
        }
    }

    public LevelUpAward? SettlePlayer(NpgsqlConnection c,Guid user,string matchId,string outcome,int oldLevel,int level)
    {
        using var streak=Database.Command(c,"UPDATE user_stats SET win_streak=CASE WHEN $2='Win' THEN win_streak+1 ELSE 0 END WHERE user_id=$1 RETURNING win_streak",user,outcome);
        int wins=(int)streak.ExecuteScalar()!;
        if(wins>0) Send(c,user,"연승","WinStreak:"+matchId,wins);
        if(level<=oldLevel) return null;
        var award = CreditLevelReward(c,user,matchId,level);
        Send(c,user,"레벨","Level:"+matchId,level);
        return award;
    }

    LevelUpAward CreditLevelReward(NpgsqlConnection c,Guid user,string matchId,int level)
    {
        JsonNode? Code(string key) => common.SingleOrDefault(r => r?["GrpCd"]?.ToString()=="Lv" &&
            r["ComCd"]?.ToString()==key && r["IsUse"]?.ToString()=="Y");
        var rise=Code("LvUpRise");var plus=Code("LvUpRisePlus");
        if(rise==null || plus==null) return new(level);
        long amount=checked(level*(long)rise["ComVal"]!+(long)plus["ComVal"]!);
        if(amount<=0) return new(level);
        var wealth=PostRewardChart.Codes(common,"Wealth");
        string kind=wealth[(int)rise["ComVal2"]!];
        if(kind is not ("Coin" or "Jem" or "Cash" or "Gold")) throw new ApiFault(409,"레벨 보상 재화 코드를 확인해 주세요.");
        long coin=kind=="Coin"?amount:0, cash=kind=="Cash"?amount:0, gold=kind=="Gold"?amount:0;
        int jem=kind=="Jem"?checked((int)amount):0;
        using var credit=Database.Command(c,"UPDATE wallets SET coin=coin+$2,jem=jem+$3,cash=cash+$4,gold=gold+$5 WHERE user_id=$1",user,coin,jem,cash,gold);
        credit.ExecuteNonQuery();
        using var ledger=Database.Command(c,"INSERT INTO wallet_ledger(user_id,request_key,coin_delta,jem_delta,cash_delta,gold_delta) VALUES($1,$2,$3,$4,$5,$6)",user,"LevelUp:"+matchId,coin,jem,cash,gold);
        ledger.ExecuteNonQuery();
        return new(level, kind, amount);
    }
}
