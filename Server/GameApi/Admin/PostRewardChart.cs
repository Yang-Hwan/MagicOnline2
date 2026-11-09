using System.Text.Json.Nodes;

namespace MagicOnline.GameApi.Admin;

public static class PostRewardChart
{
    public static readonly ChartField[] Fields = [
        new("RewardId","고유번호"), new("TypeCode","종류"), new("RangeMin","범위 시작",Optional:true),
        new("RangeMax","범위 끝",Optional:true), new("Message","내용",false), new("KeepDays","보유기간 (일)"),
        new("CurrencyCode","재화",Optional:true), new("RewardVal","보상값",Optional:true)
    ];

    public static Dictionary<long,string> Codes(JsonArray common, string group)
    {
        var result = new Dictionary<long,string>();
        foreach (var row in common)
        {
            if (row?["GrpCd"]?.ToString().Trim() != group || row["IsUse"]?.ToString() != "Y") continue;
            if (!long.TryParse(row["ComVal"]?.ToString(),out long key) ||
                !result.TryAdd(key,row["ComCd"]!.ToString()))
                throw new ApiFault(409, group + " 그룹의 ComVal은 중복 없는 숫자여야 합니다.");
        }
        return result;
    }

    public static JsonArray Examples(JsonArray common)
    {
        var types = Codes(common,"PostRewardType");
        var wealth = Codes(common,"Wealth");
        var rows = new JsonArray();
        void Add(int id,string type,int? lo,int? hi,string msg,int days,string? currency,int? value)
        {
            var t = types.SingleOrDefault(x=>x.Value==type);
            var w = wealth.SingleOrDefault(x=>x.Value==currency);
            if (t.Value==null || (currency!=null && w.Value==null)) return;
            rows.Add(new JsonObject {
                ["RewardId"]=id, ["TypeCode"]=t.Key, ["RangeMin"]=lo, ["RangeMax"]=hi,
                ["Message"]=msg, ["KeepDays"]=days, ["CurrencyCode"]=currency==null?null:(long?)w.Key, ["RewardVal"]=value
            });
        }
        Add(1,"가입",null,null,"가입축하보상",2,"Coin",10000);
        Add(2,"출석",null,null,"?일차 출석보상",3,null,null);
        Add(3,"파산",null,null,"파산으로 긴급지급 남음(?)",1,"Coin",1000);
        Add(4,"연승",3,4,"?연승 보상",3,"Jem",15);
        Add(5,"연승",5,5,"?연승 보상",3,"Jem",25);
        Add(6,"연승",6,50,"?연승 보상",3,"Jem",35);
        Add(7,"레벨",0,50,"?레벨업 축하보상",3,"Coin",5000);
        Add(8,"레벨",51,100,"?레벨업 축하보상",3,"Coin",6000);
        Add(9,"레벨",101,150,"?레벨업 축하보상",3,"Coin",7000);
        Add(10,"레벨",151,10000,"?레벨업 축하보상",3,"Coin",8000);
        Add(11,"점검",null,null,"시스템 점검",2,"Coin",10000);
        return rows;
    }

    public static void Validate(JsonArray rows, JsonArray common)
    {
        var types = Codes(common,"PostRewardType");
        var wealth = Codes(common,"Wealth");
        var ranges = new List<(long Type,long Min,long Max)>();
        foreach(var item in rows)
        {
            var row=item!.AsObject();
            long type=(long)row["TypeCode"]!;
            if (!types.TryGetValue(type,out var label)) throw new ApiFault(400,"공통코드의 사용 가능한 우편 종류를 선택해 주세요.");
            if ((long)row["RewardId"]!<=0 || (long)row["KeepDays"]!<=0) throw new ApiFault(400,"고유번호와 보유기간은 양수여야 합니다.");
            long? min=(long?)row["RangeMin"], max=(long?)row["RangeMax"];
            if (label is "연승" or "레벨")
            {
                if (!min.HasValue || !max.HasValue || min<0 || min>max) throw new ApiFault(400,"시작부터 끝까지의 범위를 입력해 주세요.");
                if(ranges.Any(x=>x.Type==type && min<=x.Max && max>=x.Min)) throw new ApiFault(400,"같은 종류의 범위가 겹칩니다.");
                ranges.Add((type,min.Value,max.Value));
            }
            else if(min.HasValue || max.HasValue) throw new ApiFault(400,"이 종류는 범위를 비워 주세요.");
            long? currency=(long?)row["CurrencyCode"], value=(long?)row["RewardVal"];
            if(label=="출석")
            {
                if(currency.HasValue || value.HasValue) throw new ApiFault(400,"출석의 재화와 보상값은 출석 보상 차트에서 가져오므로 비워 주세요.");
            }
            else if(!currency.HasValue || !wealth.ContainsKey(currency.Value) || !value.HasValue || value<0)
                throw new ApiFault(400,"유효한 재화와 0 이상의 보상값을 입력해 주세요.");
        }
    }
}
