#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Assets.Scripts.Exert.BackSys;
using Assets.Scripts.Sight.Surface.Consist;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class LocalShopCatalogSmokeTest
{
    [MenuItem("Tools/Local Game API/Verify owned equipment UI")]
    public static void RunEquipmentUi() => RunPurchaseUi();
    [MenuItem("Tools/Local Game API/Verify shop purchase UI")]
    public static void RunPurchaseUi()
    {
        if (Application.isPlaying) { Debug.LogWarning("Stop Play mode before running the shop UI check."); return; }
        try
        {
            VerifyCards(new ShopCatalogData { Products = new[] {
                new ShopProductData { Id=1, Kind="Cue", Name="UI 큐대", PriceGem=100, Purchasable=true },
                new ShopProductData { Id=2, Kind="Character", Name="UI 캐릭터", PriceGem=150, Purchasable=true },
                new ShopProductData { Id=3, Kind="MessagePack", Name="UI 메시지", PriceGem=50, Purchasable=true },
                new ShopProductData { Id=4, Kind="GemPack", Name="UI 보석", GemAmount=100 }
            }});
            Directory.CreateDirectory("UserSettings");
            File.WriteAllText("UserSettings/ShopPurchaseUiTest.json", "{\"Ok\":true}");
        }
        catch (Exception e) { Debug.LogError("[ShopPurchaseUiTest] FAIL " + e); }
    }
    [MenuItem("Tools/Local Game API/Verify shop catalog")]
    public static async void Run()
    {
        bool batch = Application.isBatchMode;
        bool success = false;
        bool ownSession = string.IsNullOrEmpty(GameApiClient.UserId);
        try
        {
            if (!new Uri(GameApiClient.BaseUrl).IsLoopback) throw new InvalidOperationException("Local API required");
            if (ownSession)
                await GameApiClient.Login("shop_" + Guid.NewGuid().ToString("N").Substring(0, 12), "Test-" + Guid.NewGuid().ToString("N"), true);
            var catalog = await GameApiClient.Request<ShopCatalogData>("/api/shop/products");
            if (catalog?.Products == null) throw new InvalidOperationException("Missing Products array");
            foreach (var p in catalog.Products)
            {
                if (p.Id <= 0 || string.IsNullOrWhiteSpace(p.Name)) throw new InvalidOperationException("Invalid product");
                if (p.Kind == "Character" && p.Emotions.Length != 9) throw new InvalidOperationException("Missing emotions");
                if (p.Kind == "MessagePack" && p.Messages.Length == 0) throw new InvalidOperationException("Missing messages");
                if (p.Kind == "GemPack" && p.GemAmount <= 0) throw new InvalidOperationException("Missing gems");
            }
            if (!Application.isPlaying) VerifyCards(catalog);
            Directory.CreateDirectory("UserSettings");
            File.WriteAllText("UserSettings/ShopCatalogSmokeTest.json", JsonUtility.ToJson(catalog, true));
            Debug.Log("[ShopCatalogSmokeTest] PASS products=" + catalog.Products.Length + " kinds=" + string.Join(",", catalog.Products.Select(p => p.Kind).Distinct()));
            success = true;
        }
        catch (Exception e) { Debug.LogError("[ShopCatalogSmokeTest] FAIL " + e); }
        finally
        {
            if (ownSession) { try { await GameApiClient.Logout(); } catch { GameApiClient.Clear(); } }
            if (batch) EditorApplication.Exit(success ? 0 : 1);
        }
    }

    static void VerifyCards(ShopCatalogData catalog)
    {
        var preview = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("ShopCatalogVerification", typeof(RectTransform));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, preview);
        var view = root.AddComponent<BottomMenuView>();
        var type = typeof(BottomMenuView);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        void Set(string field, object value) => type.GetField(field, flags).SetValue(view, value);
        object Get(string field) => type.GetField(field, flags).GetValue(view);
        void Call(string method, params object[] args) => type.GetMethod(method, flags).Invoke(view, args);
        try
        {
            var font = Resources.Load<TMP_FontAsset>("Common/Fonts/Asset/NanumBarunGothic SDF");
            if (!font) throw new InvalidOperationException("Missing shop font");
            Set("font", font); Set("textMaterial", new Material(font.material));
            Set("body", root.GetComponent<RectTransform>()); Set("design", root.GetComponent<RectTransform>());
            Call("Shop"); // In edit mode this builds the UI without starting another network request.
            Set("shopProducts", catalog.Products);
            for (int filter = 0; filter < 5; filter++)
            {
                Set("shopFilter", filter); Set("shopPage", 0); Call("DrawShopCards");
                var cards = (RectTransform)Get("shopCards");
                string[] kinds = { "", "GemPack", "Cue", "Character", "MessagePack" };
                var expected = catalog.Products.Where(p => filter == 0 || p.Kind == kinds[filter]).Take(3).ToArray();
                foreach (var p in expected)
                    if (!cards.Find("ProductCard" + p.Id)) throw new InvalidOperationException("Missing card " + p.Id);
                if (filter == 0 && catalog.Products.Length > 3)
                {
                    Set("shopPage", 1); Call("DrawShopCards");
                    if (!cards.Find("ProductCard" + catalog.Products[3].Id)) throw new InvalidOperationException("Pagination failed");
                }
            }
            if (catalog.Products.Length > 0)
            {
                Call("ShowShopProduct", catalog.Products[0]);
                if (!root.transform.Find("Modal/Panel/ProductDescription")) throw new InvalidOperationException("Missing details");
            }
            var buyable = new ShopProductData { Id = 1, Kind = "Cue", Name = "UI 검증", PriceGem = 100, Revision = 1, Purchasable = true };
            Call("ShowShopProduct", buyable);
            if (!root.transform.Find("Modal/Panel/보석으로 구매")) throw new InvalidOperationException("Missing purchase button");
            buyable.Owned = true;
            Call("ShowShopProduct", buyable);
            if (root.transform.Find("Modal/Panel/보석으로 구매")) throw new InvalidOperationException("Owned item can be repurchased");
            buyable.Owned = false; buyable.Purchasable = false;
            Call("ShowShopProduct", buyable);
            if (root.transform.Find("Modal/Panel/보석으로 구매")) throw new InvalidOperationException("Disabled sale exposes purchase button");
            Set("shopProducts", Array.Empty<ShopProductData>()); Call("DrawShopCards");
            if (((TMP_Text)Get("shopStatus")).text != "표시할 상품이 없습니다.") throw new InvalidOperationException("Empty catalog failed");
            Call("OpenInventory");
            var inventory = catalog.Products.Where(p => p.Kind != "GemPack").Concat(Enumerable.Range(0, 4).Select(i => new ShopProductData { Id = 1000 + i, Kind = "Cue", Name = "장비 UI " + i, Owned = true })).ToArray();
            Set("inventoryItems", inventory);
            string[] inventoryKinds = { "Cue", "Character", "MessagePack" };
            for (int filter = 0; filter < 3; filter++)
            {
                Set("inventoryFilter", filter); Set("inventoryPage", 0); Call("DrawInventory");
                var expected = inventory.Where(p => p.Kind == inventoryKinds[filter]).ToArray();
                var cards = (RectTransform)Get("inventoryCards");
                foreach (var p in expected.Take(3))
                    if (!cards.Find("OwnedCard" + p.Id)) throw new InvalidOperationException("Missing inventory card " + p.Id);
                if (expected.Length > 3)
                {
                    Set("inventoryPage", 1); Call("DrawInventory");
                    if (!cards.Find("OwnedCard" + expected[3].Id)) throw new InvalidOperationException("Inventory pagination failed");
                }
            }
            var equipped = inventory.First(p => p.Kind == "Cue"); equipped.Equipped = true;
            Set("inventoryFilter", 0); Set("inventoryPage", 0); Call("DrawInventory");
            if (!((RectTransform)Get("inventoryCards")).Find("장착 해제")) throw new InvalidOperationException("Missing unequip button");
            equipped.Equipped = false; Call("DrawInventory");
            if (!((RectTransform)Get("inventoryCards")).Find("장착하기")) throw new InvalidOperationException("Missing equip button");
            Set("inventoryItems", Array.Empty<ShopProductData>()); Call("DrawInventory");
            if (!((TMP_Text)Get("inventoryStatus")).text.StartsWith("구매한 상품이 없습니다.")) throw new InvalidOperationException("Empty inventory failed");
            Debug.Log("[ShopCatalogSmokeTest] PASS UI: five filters, pagination, details, purchase button, owned/sale guards, empty catalog");
            Debug.Log("[ShopCatalogSmokeTest] PASS inventory UI: three filters, pagination, equip/unequip buttons, empty inventory");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(preview); }
    }
}
#endif
