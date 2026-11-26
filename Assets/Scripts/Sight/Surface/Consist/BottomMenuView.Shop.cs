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
using Assets.Scripts.Often;
using Assets.Scripts.Prack.BackSys.GameData;

namespace Assets.Scripts.Sight.Surface.Consist
{
    public sealed partial class BottomMenuView
    {
        static readonly string[] ShopKinds = { "GemPack", "CoinPack", "Cue", "Character", "MessagePack" };
        static readonly string[] ShopTabs = { "보석", "코인", "큐대", "캐릭터", "메시지" };
        readonly List<Texture2D> shopTextures = new List<Texture2D>();
        ShopProductData[] shopProducts = Array.Empty<ShopProductData>();
        RectTransform shopCards;
        TMP_Text shopStatus;
        int shopPage, shopRequest;
        bool shopBuying;
        float shopCuePosition;
        int shopArtFrame = -1;
        bool shopLocalImageLoading;

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
            // Reuse the clean patterned strip, without the painted products or side illustrations.
            for (float y = 127; y < 837; y += 140)
                for (float x = 0; x < Width; x += 1000)
                {
                    float w = Mathf.Min(1000, Width - x), h = Mathf.Min(140, 837 - y);
                    var backdrop = Art("CatalogBackground", body, plates[4], x, y, w, h);
                    backdrop.uvRect = UV(new Rect(700, 130, w / 2, h / 2));
                }
            Box("CatalogTint", body, 0, 127, Width, 710, new Color(0, 0, 0, .22f));
            for (int i = 0; i < ShopTabs.Length; i++)
            {
                int filter = i;
                SolidButton(ShopTabs[i], body, 48 + i * 318, 154, 306, 54, () =>
                { shopFilter = filter; shopPage = 0; shopCuePosition = 0; SelectPage(4); }, shopFilter == i);
            }
            shopCards = Node("CatalogCards", body, 0, 0, Width, Height);
            shopStatus = Text("ShopStatus", body, "상품을 불러오는 중…", 48, 782, 1060, 36, 24, Color.white);
            SolidButton("새로고침", body, 1418, 778, 206, 44, () => { shopPage = 0; SelectPage(4); });
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
            var items = shopProducts.Where(p => p.Kind == ShopKinds[shopFilter]).ToArray();
            if (shopFilter == 2) { DrawCueShopCarousel(items); return; }
            int pages = Mathf.Max(1, (items.Length + 2) / 3);
            shopPage = Mathf.Clamp(shopPage, 0, pages - 1);
            var pending = PendingShopPurchase();
            shopStatus.text = pending != null ? "확인이 끝나지 않은 구매가 있습니다." : items.Length == 0 ? "표시할 상품이 없습니다." : $"총 {items.Length}개 · 상품을 눌러 자세히 보기";
            if (pending != null)
                SolidButton(shopBuying ? "구매 확인 중…" : "구매 결과 확인", shopCards, 1168, 778, 230, 44, () => PurchaseShop(pending));
            for (int i = 0; i < 3 && shopPage * 3 + i < items.Length; i++)
            {
                var product = items[shopPage * 3 + i];
                float x = 48 + i * 534;
                var card = Node("ProductCard" + product.Id, shopCards, x, 260, 508, 440);
                Box("CardBackground", card, 0, 0, 508, 440, new Color(0, 0, 0, .65f));
                string kind = ShopTabs[Mathf.Max(0, Array.IndexOf(ShopKinds, product.Kind))];
                Text("ProductKind", card, kind, 20, 14, 468, 35, 23, Yellow);
                var image = Art("ProductImage", card, null, 110, 65, 288, 170);

                image.color = Color.clear;
                var placeholder = Text("ImagePlaceholder", card, "이미지 준비 중", 20, 120, 468, 55, 27, Color.gray);
                string asset = product.Thumbnail;
                if (string.IsNullOrWhiteSpace(asset) && product.Kind == "Cue") asset = product.Visual;
                if (string.IsNullOrWhiteSpace(asset) && product.Kind == "Character")
                    asset = product.Emotions?.FirstOrDefault(e => e.Code == "기본")?.Asset;
                if(string.IsNullOrWhiteSpace(asset) && product.Kind=="CoinPack")asset="Gameplay/level-up-Coin";
                if(string.IsNullOrWhiteSpace(asset) && product.Kind=="GemPack")asset="Gameplay/level-up-Jem";
                if(Application.isPlaying)StartCoroutine(LoadShopImage(asset,image,placeholder));
                else {var local=DailyCueArt.Load(asset??"");if(local)SetShopImage(local,image,placeholder);}
                if(product.Kind=="Cue")PremiumCuePreview.Attach(image,product.Visual);
                Text("ProductName", card, product.Name, 20, 245, 468, 65, 30, Color.white, autoSize: true);
                string price = product.Kind == "CoinPack" ? $"코인 {product.CoinAmount+product.CoinBonus:N0}개\n보석 {product.PriceGem:N0}개" : product.Kind == "GemPack" ? $"보석 {product.GemAmount:N0} + {product.GemBonus:N0}\n현금 가격 준비 중" : $"보석 {product.PriceGem:N0}개";
                Text("ProductPrice", card, price, 20, 312, 468, 65, 26, Yellow, autoSize: true);
                Text("SaleStatus", card, product.Owned ? "보유 중" : !product.Purchasable ? "판매 중지" : product.Kind == "GemPack" ? "현금 결제 준비 중" : "보석으로 구매", 20, 390, 468, 35, 23, Color.white);
                Hit("ProductDetails" + product.Id, card, 0, 0, 508, 440, () => ShowShopProduct(product));
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
            if(p.Kind=="Cue"){ShowPremiumCueProduct(p);return;}
            string detail = p.Kind == "Cue" ? p.CueDetail :
                p.Kind == "CoinPack" ? $"기본 코인 {p.CoinAmount:N0}개 + 추가 코인 {p.CoinBonus:N0}개\n총 {p.CoinAmount+p.CoinBonus:N0} 코인 지급 · 반복 구매 가능" :
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

        void DrawCueShopCarousel(ShopProductData[] items)
        {
            const float cardWidth = 1000, stride = 1032, viewportWidth = Width - 64;
            float padding = (viewportWidth - cardWidth) / 2;
            var viewport = Node("ShopCueViewport", shopCards, 32, 246, viewportWidth, 474);
            var surface = viewport.gameObject.AddComponent<Image>();
            surface.color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Node("ShopCueContent", viewport, 0, 0,
                viewportWidth + Mathf.Max(0, items.Length - 1) * stride, 474);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = true; scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 65; scroll.decelerationRate = .08f;
            // Keep only the visible card, its neighbours, and one extra card on each side.
            // The content width still represents the entire catalog so dragging remains continuous.
            var cards = new Dictionary<int, RectTransform>();
            int windowCenter = -1;
            void ShowWindow(int center)
            {
                if (center == windowCenter) return;
                windowCenter = center;
                foreach (int index in cards.Keys.Where(index => Mathf.Abs(index - center) > 2).ToArray())
                {
                    cards[index].gameObject.SetActive(false);
                    Dispose(cards[index].gameObject);
                    cards.Remove(index);
                }
                // Start the centered artwork first, then preload adjacent cards.
                foreach (int offset in new[] { 0, -1, 1, -2, 2 })
                {
                    int index = center + offset;
                    if (index < 0 || index >= items.Length || cards.ContainsKey(index)) continue;
                    var card = Node("ShopCue" + items[index].Id, content, padding + index * stride, 0, cardWidth, 470);
                    cards.Add(index, card);
                    DrawShopCueCard(card, items[index]);
                }
            }
            var pending = PendingShopPurchase();
            shopStatus.text = pending != null ? "확인이 끝나지 않은 구매가 있습니다." :
                items.Length == 0 ? "표시할 큐대가 없습니다." : $"총 {items.Length}개 · 좌우로 밀어 큐대를 확인하세요";
            if (pending != null)
                SolidButton(shopBuying ? "구매 확인 중…" : "구매 결과 확인", shopCards, 1168, 778, 230, 44, () => PurchaseShop(pending));
            if (items.Length == 0) return;
            var counter = Text("CueCatalogPosition", shopCards, "", 726, 730, 220, 42, 26, Yellow);
            void UpdatePosition(Vector2 position)
            {
                shopCuePosition = Mathf.Clamp01(position.x);
                int index = Mathf.RoundToInt(shopCuePosition * (items.Length - 1));
                counter.text = $"{index + 1} / {items.Length}";
                ShowWindow(index);
            }
            void Move(int direction)
            {
                int index = Mathf.RoundToInt(scroll.horizontalNormalizedPosition * (items.Length - 1));
                scroll.StopMovement();
                scroll.horizontalNormalizedPosition = Mathf.Clamp(index + direction, 0, items.Length - 1) / (float)Mathf.Max(1, items.Length - 1);
                UpdatePosition(new Vector2(scroll.horizontalNormalizedPosition, 0));
            }
            if (items.Length > 1)
            {
                CueButton("PreviousCue", shopCards, "‹", 640, 730, 70, 42, () => Move(-1), false);
                CueButton("NextCue", shopCards, "›", 962, 730, 70, 42, () => Move(1), false);
            }
            scroll.horizontalNormalizedPosition = Mathf.Clamp01(shopCuePosition);
            UpdatePosition(new Vector2(scroll.horizontalNormalizedPosition, 0));
            scroll.onValueChanged.AddListener(UpdatePosition);
        }

        void DrawShopCueCard(RectTransform card, ShopProductData cue)
        {
            Box("CueCardBackground", card, 0, 0, 1000, 470, new Color(0, 0, 0, .68f));
            Box("CueCardAccent", card, 0, 0, 1000, 3, Yellow);
            Box("CueTitleBand", card, 0, 3, 1000, 56, new Color(.08f, .18f, .27f, .85f));
            Text("CueGrade", card, cue.CueAbility == null ? "큐대" : cue.CueAbility.Grade + "급", 24, 8, 110, 45, 28, Yellow);
            Text("CueName", card, cue.Name, 145, 8, 645, 45, 34, Color.white, autoSize: true);
            Text("CueOwned", card, cue.Equipped ? "장착 중" : cue.Owned ? "보유 중" : "", 804, 8, 172, 45, 24, Yellow);
            var image = Art("ProductImage", card, null, 32, 88, 936, 115);
            image.color = Color.clear;
            var placeholder = Text("ImagePlaceholder", card, "이미지 준비 중", 32, 112, 936, 50, 25, Color.gray);
            string asset = string.IsNullOrWhiteSpace(cue.Thumbnail) ? cue.Visual : cue.Thumbnail;
            if (Application.isPlaying) StartCoroutine(LoadShopImage(asset, image, placeholder));
            else { var local = DailyCueArt.Load(asset ?? ""); if (local) SetShopImage(local, image, placeholder); }
            PremiumCuePreview.Attach(image, cue.Visual);
            Hit("CueArtworkDetails", card, 0, 60, 1000, 156, () => ShowShopProduct(cue));
            DrawShopCueStats(card, cue, 24, 236);
            CueButton("CueDetails", card, "상세 보기", 742, 252, 230, 54, () => ShowShopProduct(cue), false);
            CueButton("BuyCue", card, cue.Owned ? "보유 중" : !cue.Purchasable ? "판매 중지" : $"보석 {cue.PriceGem:N0}개\n구매", 742, 326, 230, 104,
                () => ConfirmShopPurchase(cue), true, !cue.Owned && cue.Purchasable && !shopBuying);
        }

        void DrawShopCueStats(RectTransform parent, ShopProductData cue, float x, float y)
        {
            var ability = cue.CueAbility;
            var levels = ability == null ? null : CueAbilitySettings.WithGuide(
                ability.LevelsFromSteps(cue.CueSteps + cue.CuePending / Math.Max(1, ability.Reach)));
            var maximum = ability == null ? null : CueAbilitySettings.WithGuide(ability.Maximum);
            string[] names = { "파워", "팔로스루", "당점", "준비시간", "가이드" };
            int[] legacy = { cue.Power, cue.FollowThrough, cue.Tip, cue.ShotSeconds, 0 };
            for (int i = 0; i < names.Length; i++)
            {
                float top = y + i * 35;
                Text("CueStatName" + i, parent, names[i], x, top, 126, 30, 23, Color.white, TextAlignmentOptions.MidlineLeft);
                if (ability == null)
                {
                    Text("CueStatValue" + i, parent, i == 4 ? "—" : legacy[i].ToString("+0;-0;0") + (i == 3 ? "초" : "%"),
                        x + 140, top, 510, 30, 24, Color.white, TextAlignmentOptions.MidlineLeft);
                    continue;
                }
                int current = ability.Percent(levels[i]), max = ability.Percent(maximum[i]);
                Box("CueStatTrack" + i, parent, x + 140, top + 7, 324, 16, new Color(.08f, .14f, .17f, .9f));
                Box("CueStatGrowth" + i, parent, x + 140, top + 7, 324 * max / 100f, 16, new Color(.64f, .18f, .72f));
                Box("CueStatFill" + i, parent, x + 140, top + 7, 324 * current / 100f, 16, new Color(.35f, .85f, .18f));
                int minSeconds = cue.CueTimeMin > 0 ? cue.CueTimeMin : 20;
                int maxSeconds = Math.Max(minSeconds, cue.CueTimeMax);
                string value = i == 3 ? ((int)Math.Round(minSeconds + (maxSeconds - minSeconds) * current / 100.0, MidpointRounding.AwayFromZero)) + "초" : current + "%";
                Text("CueStatValue" + i, parent, value, x + 472, top, 90, 30, 24, Color.white);
                Text("CueStatMaximum" + i, parent, "최대 " + (i == 3 ? ((int)Math.Round(minSeconds + (maxSeconds - minSeconds) * max / 100.0, MidpointRounding.AwayFromZero)) + "초" : max + "%"),
                    x + 570, top, 122, 30, 20, new Color(.87f, .58f, 1));
            }
            Text("CueStatLegend", parent, ability == null ? "기본 큐대 보정 수치" : "초록: 현재 능력 · 보라: 성장 가능 범위", x, y + 180, 690, 30, 19, CueQuiet, TextAlignmentOptions.MidlineLeft);
        }

        void ShowPremiumCueProduct(ShopProductData p)
        {
            var panel = Dialog(p.Name, ""); Clear(panel); Place(panel, 216, 150, 1240, 650);
            modal.Find("Shade").GetComponent<Image>().color = new Color(0, 0, 0, .4f);
            Box("Background", panel, 0, 0, 1240, 650, new Color(0, 0, 0, .9f));
            Box("Accent", panel, 0, 0, 1240, 3, Yellow);
            Text("Title", panel, p.Name, 35, 20, 1060, 65, 38, Yellow);
            CueButton("CloseCueDetails", panel, "닫기", 1100, 25, 108, 50, CloseModal, false);
            var image = Art("PremiumPreview", panel, null, 45, 110, 1150, 155); image.color = Color.clear;
            var placeholder = Text("Loading", panel, "큐대를 불러오는 중…", 45, 158, 1150, 55, 27, Color.gray);
            string asset = string.IsNullOrWhiteSpace(p.Visual) ? p.Thumbnail : p.Visual;
            if (Application.isPlaying) StartCoroutine(LoadShopImage(asset, image, placeholder));
            else { var local = DailyCueArt.Load(asset ?? ""); if (local) SetShopImage(local, image, placeholder); }
            PremiumCuePreview.Attach(image, p.Visual);
            Text("Grade", panel, p.CueAbility == null ? "큐대" : p.CueAbility.Grade + "급", 40, 280, 200, 40, 30, Yellow, TextAlignmentOptions.MidlineLeft);
            DrawShopCueStats(panel, p, 40, 332);
            var description = Text("Description", panel, p.Description ?? "", 800, 288, 390, 172, 25, Color.white, TextAlignmentOptions.TopLeft, true);
            description.textWrappingMode = TextWrappingModes.Normal;
            CueButton("BuyCueDetail", panel, p.Owned ? "보유 중" : !p.Purchasable ? "판매 중지" : $"보석 {p.PriceGem:N0}개 · 구매", 800, 484, 390, 68,
                () => ConfirmShopPurchase(p), true, !p.Owned && p.Purchasable && !shopBuying);
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
                if(owner!=GameApiClient.UserId)return;
                if (receipt == null || receipt.ProductId != input.ProductId) throw new InvalidOperationException("InvalidPurchaseReceipt");
                PlayerPrefs.DeleteKey(key); PlayerPrefs.Save();
                var product = shopProducts.FirstOrDefault(p => p.Id == receipt.ProductId);
                if (product != null && product.Kind!="CoinPack") product.Owned = true;
                if(receipt.GrantedCoin>0)BackendGame.Instance.UserMainData.Coin=receipt.BalanceCoin;
                BackendGame.Instance.UserMainData.Jem = receipt.BalanceGem;
                try { await BackendGame.Instance.LoadAsync(); } catch (Exception) { /* Receipt balance remains authoritative for this purchase. */ }
                message = receipt.GrantedCoin>0 ? $"{(receipt.AlreadyProcessed ? "구매 내역을 확인했습니다." : "코인을 구매했습니다.")}\n사용 보석: {receipt.PaidGem:N0}개\n지급 코인: {receipt.GrantedCoin:N0}개\n보유 코인: {receipt.BalanceCoin:N0}개" : $"{(receipt.AlreadyProcessed ? "구매 내역을 확인했습니다." : "구매했습니다.")}\n사용 보석: {receipt.PaidGem:N0}개\n상품이 보유 목록에 추가되었습니다.";
            }
            catch (GameApiException e)
            {
                bool terminal = e.Status >= 400 && e.Status < 500 && e.Status != 401 && e.Status != 408 && e.Status != 429;
                if (terminal) { PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); }
                message = e.Message == "ShopInsufficientGems" ? "보석이 부족합니다." :
                    e.Message == "ShopCoinLimit" ? "구매 후 코인이 보유 한도를 초과합니다." :
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
            if (key.StartsWith("/shop-images/", StringComparison.Ordinal)) key = GameApiClient.BaseUrl + key;
            if (!Uri.TryCreate(key, UriKind.Absolute, out var uri))
            {
                // Cached, tightly framed cue art can be displayed immediately.
                if (DailyCueArt.TryGetCached(key, out var cached))
                {
                    if (target) SetShopImage(cached, target, placeholder);
                    yield break;
                }
                // Serialize cold loads: a quick swipe must not decode the whole catalog.
                while (shopLocalImageLoading)
                {
                    yield return null;
                    if (!target || !target.gameObject.activeInHierarchy) yield break;
                }
                if (!target || !target.gameObject.activeInHierarchy) yield break;
                if (DailyCueArt.TryGetCached(key, out cached))
                {
                    SetShopImage(cached, target, placeholder);
                    yield break;
                }
                shopLocalImageLoading = true;
                try
                {
                    var loading = Resources.LoadAsync<Texture2D>(key);
                    yield return loading;
                    if (!target || !target.gameObject.activeInHierarchy)
                    {
                        if (DailyCueArt.Contains(key) && loading.asset) Resources.UnloadAsset(loading.asset);
                        yield break;
                    }
                    Texture2D local;
                    if (DailyCueArt.Contains(key))
                    {
                        // At most one cold crop per frame; the UI can paint between images.
                        while (shopArtFrame == Time.frameCount)
                        {
                            yield return null;
                            if (!target || !target.gameObject.activeInHierarchy)
                            {
                                if (loading.asset) Resources.UnloadAsset(loading.asset);
                                yield break;
                            }
                        }
                        shopArtFrame = Time.frameCount;
                        local = DailyCueArt.Load(key);
                    }
                    else local = loading.asset as Texture2D;
                    if (local && target) SetShopImage(local, target, placeholder);
                }
                finally { shopLocalImageLoading = false; }
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
