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
        public string Kind, Name, Description, Thumbnail, Visual, Personality;
        public int PriceGem, Revision, GemAmount, GemBonus, Power, FollowThrough, Tip, ShotSeconds;
        public bool Purchasable, Owned, Equipped;
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
        public long PurchaseId, ProductId;
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
