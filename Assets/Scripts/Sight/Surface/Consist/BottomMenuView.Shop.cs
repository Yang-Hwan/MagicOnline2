using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Exert.BackSys;
using Assets.Scripts.Prack.BackSys;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Assets.Scripts.Sight.Surface.Consist
{
    public sealed partial class BottomMenuView
    {
        static readonly string[] ShopKinds = { "", "GemPack", "Cue", "Character", "MessagePack" };
        static readonly string[] ShopTabs = { "전체", "보석", "큐대", "캐릭터", "메시지" };
        readonly List<Texture2D> shopTextures = new List<Texture2D>();
        ShopProductData[] shopProducts = Array.Empty<ShopProductData>();
        RectTransform shopCards;
        TMP_Text shopStatus;
        int shopPage, shopRequest;
        bool shopBuying;

        string ShopPendingKey => "ShopPending:" + GameApiClient.BaseUrl + ":" + GameApiClient.UserId;
        ShopPurchaseInput PendingShopPurchase()
        {
            if (string.IsNullOrEmpty(GameApiClient.UserId)) return null;
            try { return JsonUtility.FromJson<ShopPurchaseInput>(PlayerPrefs.GetString(ShopPendingKey, "")); }
            catch (ArgumentException) { return null; }
        }

        void Shop()
        {
            shopFilter = Mathf.Clamp(shopFilter, 0, 4);
            ReleaseShopTextures();
            // Cover the baked-in sample products; all product content below comes from the API.
            Box("CatalogBackground", body, 265, 200, 1140, 628, new Color(.035f, .04f, .055f));
            for (int i = 0; i < ShopTabs.Length; i++)
            {
                int filter = i;
                SolidButton(ShopTabs[i], body, 287 + i * 220, 215, 211, 49, () =>
                { shopFilter = filter; shopPage = 0; SelectPage(4); }, shopFilter == i);
            }
            shopCards = Node("CatalogCards", body, 0, 0, Width, Height);
            shopStatus = Text("ShopStatus", body, "상품을 불러오는 중…", 300, 775, 610, 40, 24, Color.white);
            SolidButton("새로고침", body, 1170, 771, 200, 44, () => { shopPage = 0; SelectPage(4); });
            LoadShop(++shopRequest, shopCards);
        }

        async void LoadShop(int request, RectTransform target)
        {
            shopProducts = Array.Empty<ShopProductData>();
            if (!Application.isPlaying || string.IsNullOrEmpty(GameApiClient.UserId))
            { if (shopStatus) shopStatus.text = "로그인 후 상점을 이용할 수 있습니다."; return; }
            try
            {
                var result = await GameApiClient.Request<ShopCatalogData>("/api/shop/products");
                if (!this || !isActiveAndEnabled || CurrentPage != 4 || request != shopRequest || !target) return;
                shopProducts = result?.Products ?? Array.Empty<ShopProductData>();
                DrawShopCards();
            }
            catch (Exception)
            {
                if (this && CurrentPage == 4 && request == shopRequest && target && shopStatus)
                    shopStatus.text = "상품을 불러오지 못했습니다. 새로고침해 주세요.";
            }
        }

        void DrawShopCards()
        {
            Clear(shopCards);
            ReleaseShopTextures();
            var items = shopProducts.Where(p => shopFilter == 0 || p.Kind == ShopKinds[shopFilter]).ToArray();
            int pages = Mathf.Max(1, (items.Length + 2) / 3);
            shopPage = Mathf.Clamp(shopPage, 0, pages - 1);
            var pending = PendingShopPurchase();
            shopStatus.text = pending != null ? "확인이 끝나지 않은 구매가 있습니다." : items.Length == 0 ? "표시할 상품이 없습니다." : $"총 {items.Length}개 · 상품을 눌러 자세히 보기";
            if (pending != null)
                SolidButton(shopBuying ? "구매 확인 중…" : "구매 결과 확인", shopCards, 925, 771, 230, 44, () => PurchaseShop(pending));
            for (int i = 0; i < 3 && shopPage * 3 + i < items.Length; i++)
            {
                var product = items[shopPage * 3 + i];
                float x = 297 + i * 362;
                Box("ProductCard" + product.Id, shopCards, x, 290, 342, 410, new Color(.12f, .14f, .19f));
                string kind = ShopTabs[Mathf.Max(0, Array.IndexOf(ShopKinds, product.Kind))];
                Text("ProductKind", shopCards, kind, x + 15, 300, 312, 35, 23, Yellow);
                var image = Art("ProductImage", shopCards, null, x + 76, 346, 190, 150);
                if (product.Kind == "Cue") Place(image.rectTransform, x + 12, 346, 318, 150);
                image.color = Color.clear;
                var placeholder = Text("ImagePlaceholder", shopCards, "이미지 준비 중", x + 15, 385, 312, 55, 27, Color.gray);
                string asset = product.Thumbnail;
                if (string.IsNullOrWhiteSpace(asset) && product.Kind == "Cue") asset = product.Visual;
                if (string.IsNullOrWhiteSpace(asset) && product.Kind == "Character")
                    asset = product.Emotions?.FirstOrDefault(e => e.Code == "기본")?.Asset;
                StartCoroutine(LoadShopImage(asset, image, placeholder));
                Text("ProductName", shopCards, product.Name, x + 12, 510, 318, 65, 30, Color.white, autoSize: true);
                string price = product.Kind == "GemPack" ? $"보석 {product.GemAmount:N0} + {product.GemBonus:N0}\n현금 가격 준비 중" : $"보석 {product.PriceGem:N0}개";
                Text("ProductPrice", shopCards, price, x + 12, 578, 318, 65, 26, Yellow, autoSize: true);
                Text("SaleStatus", shopCards, product.Owned ? "보유 중" : !product.Purchasable ? "판매 중지" : product.Kind == "GemPack" ? "현금 결제 준비 중" : "보석으로 구매", x + 12, 649, 318, 35, 23, Color.white);
                Hit("ProductDetails" + product.Id, shopCards, x, 290, 342, 410, () => ShowShopProduct(product));
            }
            if (pages > 1)
            {
                SolidButton("이전", shopCards, 570, 715, 150, 44, () => { shopPage = (shopPage + pages - 1) % pages; DrawShopCards(); });
                Text("CatalogPage", shopCards, $"{shopPage + 1} / {pages}", 735, 715, 200, 44, 26, Yellow);
                SolidButton("다음", shopCards, 950, 715, 150, 44, () => { shopPage = (shopPage + 1) % pages; DrawShopCards(); });
            }
        }

        void ShowShopProduct(ShopProductData p)
        {
            string detail = p.Kind == "Cue" ? $"파워 {p.Power:+0;-0;0}% · 팔로스루 {p.FollowThrough:+0;-0;0}%\n당점 {p.Tip:+0;-0;0}% · 준비시간 {p.ShotSeconds:+0;-0;0}초" :
                p.Kind == "GemPack" ? $"기본 보석 {p.GemAmount:N0}개 + 추가 {p.GemBonus:N0}개" :
                p.Kind == "Character" ? "감정: " + string.Join(", ", (p.Emotions ?? Array.Empty<ShopEmotionData>()).Select(e => e.Code)) :
                "성격: " + p.Personality + "\n" + string.Join("\n", p.Messages ?? Array.Empty<string>());
            var panel = Dialog(p.Name, "");
            var text = Text("ProductDescription", panel, p.Description + "\n" + detail + "\n" + (p.Owned ? "보유 중" : p.Kind == "GemPack" ? "현금 결제 준비 중" : $"가격: 보석 {p.PriceGem:N0}개"),
                45, 103, 750, 155, 25, Color.white, TextAlignmentOptions.TopLeft, true);
            text.textWrappingMode = TextWrappingModes.Normal;
            if (p.Kind != "GemPack" && !p.Owned && p.Purchasable)
                SolidButton(shopBuying ? "구매 처리 중…" : "보석으로 구매", panel, 235, 270, 370, 45, () => ConfirmShopPurchase(p));
            else if (!p.Owned && !p.Purchasable)
                Text("NotForSale", panel, "판매 중지", 235, 270, 370, 45, 25, Color.gray);
        }

        void ConfirmShopPurchase(ShopProductData p)
        {
            if (shopBuying) return;
            var pending = PendingShopPurchase();
            if (pending != null)
            {
                var panel = Dialog("이전 구매 확인", "이전 구매 결과를 먼저 확인해 주세요.\n새로운 구매를 진행하기 전에 중복 결제를 확인합니다.");
                SolidButton("구매 결과 확인", panel, 235, 240, 370, 60, () => PurchaseShop(pending));
                return;
            }
            if (string.IsNullOrEmpty(GameApiClient.UserId)) { Dialog("구매", "로그인 후 이용해 주세요."); return; }
            var confirm = Dialog("상품 구매", $"{p.Name}\n보석 {p.PriceGem:N0}개로 구매하시겠습니까?\n보유 보석: {BackendGame.Instance.UserMainData.Jem:N0}개");
            // Persist this ID only after explicit confirmation, before sending a potentially committed request.
            SolidButton("구매 확정", confirm, 235, 240, 370, 60, () =>
            {
                if (shopBuying || PendingShopPurchase() != null) return;
                var input = new ShopPurchaseInput { RequestId = Guid.NewGuid().ToString(), ProductId = p.Id, ExpectedPriceGem = p.PriceGem, ExpectedRevision = p.Revision };
                PlayerPrefs.SetString(ShopPendingKey, JsonUtility.ToJson(input)); PlayerPrefs.Save();
                PurchaseShop(input);
            });
        }

        async void PurchaseShop(ShopPurchaseInput input)
        {
            if (shopBuying || !Application.isPlaying || string.IsNullOrEmpty(GameApiClient.UserId)) return;
            shopBuying = true;
            string owner = GameApiClient.UserId, key = ShopPendingKey;
            Dialog("상품 구매", "구매 결과를 확인하고 있습니다…");
            string message;
            try
            {
                var receipt = await GameApiClient.Request<ShopPurchaseResult>("/api/shop/purchases", "POST", input);
                if (receipt == null || receipt.ProductId != input.ProductId) throw new InvalidOperationException("InvalidPurchaseReceipt");
                PlayerPrefs.DeleteKey(key); PlayerPrefs.Save();
                var product = shopProducts.FirstOrDefault(p => p.Id == receipt.ProductId);
                if (product != null) product.Owned = true;
                BackendGame.Instance.UserMainData.Jem = receipt.BalanceGem;
                try { await BackendGame.Instance.LoadAsync(); } catch (Exception) { /* Receipt balance remains authoritative for this purchase. */ }
                message = $"{(receipt.AlreadyProcessed ? "구매 내역을 확인했습니다." : "구매했습니다.")}\n사용 보석: {receipt.PaidGem:N0}개\n상품이 보유 목록에 추가되었습니다.";
            }
            catch (GameApiException e)
            {
                bool terminal = e.Status >= 400 && e.Status < 500 && e.Status != 401 && e.Status != 408 && e.Status != 429;
                if (terminal) { PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); }
                message = e.Message == "ShopInsufficientGems" ? "보석이 부족합니다." :
                    e.Message == "ShopAlreadyOwned" ? "이미 보유한 상품입니다." :
                    e.Message == "ShopNotForSale" ? "현재 판매하지 않는 상품입니다." :
                    e.Message == "ShopProductChanged" ? "상품 정보 또는 가격이 변경됐습니다. 새로고침한 후 다시 확인해 주세요." :
                    e.Message == "ShopProductNotFound" ? "상품을 찾을 수 없습니다." :
                    terminal ? "구매할 수 없습니다. 상품 정보를 새로고침해 주세요." :
                    "구매 결과를 확인하지 못했습니다. 상점의 ‘구매 결과 확인’을 눌러 주세요. 같은 요청으로 확인하므로 중복 차감되지 않습니다.";
                if (e.Message == "ShopAlreadyOwned") { var p = shopProducts.FirstOrDefault(x => x.Id == input.ProductId); if (p != null) p.Owned = true; }
            }
            catch (Exception) { message = "구매 결과를 확인하지 못했습니다. 상점에서 ‘구매 결과 확인’을 눌러 주세요."; }
            finally { shopBuying = false; }
            if (this && isActiveAndEnabled && owner == GameApiClient.UserId)
            {
                RefreshUser();
                if (CurrentPage == 4 && shopCards) { DrawShopCards(); Dialog("구매 결과", message); }
            }
        }

        IEnumerator LoadShopImage(string key, RawImage target, TMP_Text placeholder)
        {
            if (string.IsNullOrWhiteSpace(key)) yield break;
            if (!Uri.TryCreate(key, UriKind.Absolute, out var uri))
            {
                var local = Resources.Load<Texture2D>(key);
                if (local && target) SetShopImage(local,target,placeholder);
                yield break;
            }
            if (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback)) yield break;
            using (var request = UnityWebRequestTexture.GetTexture(uri.AbsoluteUri))
            {
                request.timeout = 12;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success || !target) yield break;
                var texture = DownloadHandlerTexture.GetContent(request);
                shopTextures.Add(texture); SetShopImage(texture,target,placeholder);
            }
        }

        static void SetShopImage(Texture texture, RawImage target, TMP_Text placeholder)
        {
            var rect = target.rectTransform;
            var box = rect.sizeDelta;
            float scale = Mathf.Min(box.x / texture.width, box.y / texture.height);
            var size = new Vector2(texture.width, texture.height) * scale;
            rect.anchoredPosition += Vector2.Scale((box - size) * .5f, new Vector2(1, -1));
            rect.sizeDelta = size;
            target.texture = texture;
            target.color = Color.white;
            if (placeholder) placeholder.gameObject.SetActive(false);
        }

        void ReleaseShopTextures()
        {
            foreach (var texture in shopTextures) if (texture) Destroy(texture);
            shopTextures.Clear();
        }
    }
}
