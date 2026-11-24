using System;

namespace Assets.Scripts.Exert.BackSys
{
    [Serializable] public sealed class ShopCatalogData
    {
        public ShopProductData[] Products = Array.Empty<ShopProductData>();
    }
    [Serializable] public sealed class ShopProductData
    {
        public long Id;
        public long CoinAmount,CoinBonus;
        public string Kind, Name, Description, Thumbnail, Visual, Personality;
        public int PriceGem, Revision, GemAmount, GemBonus, Power, FollowThrough, Tip, ShotSeconds;
        public bool Purchasable, Owned, Equipped;
        public Assets.Scripts.Prack.BackSys.GameData.CueAbilitySettings CueAbility;
        public long CueProgress,CueSteps,CuePending;
        public int CueTimeMin=20,CueTimeMax=40;
        public string CueDetail
        {
            get {
                if(CueAbility==null)return $"파워 {Power:+0;-0;0}% · 팔로스루 {FollowThrough:+0;-0;0}%\n당점 {Tip:+0;-0;0}% · 준비시간 {ShotSeconds:+0;-0;0}초";
                var l=Assets.Scripts.Prack.BackSys.GameData.CueAbilitySettings.WithGuide(CueAbility.LevelsFromSteps(CueSteps+CuePending/CueAbility.Reach));
                var range=BackendChart.commonCodeData.Find(c=>c.GrpCd=="Cue" && c.ComCd=="Sec" && c.IsUse=="Y");
                int min=CueTimeMin>0?CueTimeMin:range?.ComVal??20,max=CueTimeMax>=min?CueTimeMax:range?.ComVal2??40;
                int seconds=(int)Math.Round(min+(max-min)*CueAbility.Percent(l[3])/100.0,MidpointRounding.AwayFromZero);
                return $"{CueAbility.Grade}급 · Lv {l[0]}/{l[1]}/{l[2]}/{l[3]}/{l[4]}\n파워 {CueAbility.Percent(l[0])}% · 스루 {CueAbility.Percent(l[1])}%\n당점 {CueAbility.Percent(l[2])}% · 준비시간 {seconds}초\n가이드 {CueAbility.GuidePercent(CueSteps+CuePending/CueAbility.Reach)}%\n진도 {CuePending%CueAbility.Reach}/{CueAbility.Reach} · 최대 {string.Join("/",Assets.Scripts.Prack.BackSys.GameData.CueAbilitySettings.WithGuide(CueAbility.Maximum))}";
            }
        }
        public ShopEmotionData[] Emotions = Array.Empty<ShopEmotionData>();
        public string[] Messages = Array.Empty<string>();
    }
    [Serializable] public sealed class ShopEmotionData { public string Code, Asset; }
    [Serializable] public sealed class ShopPurchaseInput
    {
        public string RequestId;
        public long ProductId;
        public int ExpectedPriceGem, ExpectedRevision;
    }
    [Serializable] public sealed class ShopPurchaseResult
    {
        public long PurchaseId, ProductId, GrantedCoin, BalanceCoin;
        public int PaidGem, BalanceGem;
        public bool AlreadyProcessed;
    }
    [Serializable] public sealed class ShopEquipmentInput
    {
        public string Slot;
        public long ProductId;
        public bool Equipped;
    }
}
