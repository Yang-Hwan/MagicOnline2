using System;
using System.Linq;
using Assets.Scripts.Exert.BackSys;
using TMPro;
using UnityEngine;

namespace Assets.Scripts.Sight.Surface.Consist
{
    public sealed partial class BottomMenuView
    {
        static readonly string[] InventoryKinds = { "Cue", "Character", "MessagePack" };
        static readonly string[] InventoryTabs = { "큐대", "캐릭터", "메시지" };
        ShopProductData[] inventoryItems = Array.Empty<ShopProductData>();
        RectTransform inventoryPanel, inventoryCards;
        TMP_Text inventoryStatus;
        int inventoryFilter, inventoryPage, inventoryRequest;
        bool inventorySaving;
        string inventoryOwner;

        void OpenInventory()
        {
            CloseModal();
            modal = Node("Inventory", design, 0, 0, Width, Height);
            Box("Shade", modal, 0, 0, Width, Height, new Color(0, 0, 0, .82f)).raycastTarget = true;
            inventoryPanel = Node("InventoryPanel", modal, 216, 105, 1240, 730);
            Box("Background", inventoryPanel, 0, 0, 1240, 730, new Color(.035f, .04f, .055f));
            Text("Title", inventoryPanel, "보유장비", 35, 22, 400, 55, 38, Yellow, TextAlignmentOptions.MidlineLeft);
            SolidButton("닫기", inventoryPanel, 1035, 25, 170, 48, CloseModal);
            for (int i = 0; i < InventoryTabs.Length; i++)
            {
                int filter = i;
                SolidButton(InventoryTabs[i], inventoryPanel, 40 + i * 390, 100, 370, 49, () =>
                { inventoryFilter = filter; inventoryPage = 0; OpenInventory(); }, inventoryFilter == i);
            }
            inventoryCards = Node("OwnedProducts", inventoryPanel, 0, 0, 1240, 650);
            inventoryStatus = Text("InventoryStatus", inventoryPanel, "보유장비를 불러오는 중…", 40, 670, 920, 42, 23, Color.white, TextAlignmentOptions.MidlineLeft);
            SolidButton("새로고침", inventoryPanel, 995, 668, 210, 44, () => OpenInventory());
            inventoryOwner = GameApiClient.UserId;
            inventoryItems = Array.Empty<ShopProductData>();
            ReadInventory(++inventoryRequest, inventoryPanel);
        }

        async void ReadInventory(int request, RectTransform panel)
        {
            if (!Application.isPlaying || string.IsNullOrEmpty(inventoryOwner))
            { if (inventoryStatus) inventoryStatus.text = "로그인 후 보유장비를 확인할 수 있습니다."; return; }
            try
            {
                var result = await GameApiClient.Request<ShopCatalogData>("/api/me/inventory");
                if (!InventoryCurrent(request, panel)) return;
                inventoryItems = result?.Products ?? Array.Empty<ShopProductData>();
                DrawInventory();
            }
            catch (Exception)
            { if (InventoryCurrent(request, panel)) inventoryStatus.text = "보유장비를 불러오지 못했습니다. 새로고침해 주세요."; }
        }

        bool InventoryCurrent(int request, RectTransform panel) => this && isActiveAndEnabled && panel && panel.gameObject.activeInHierarchy
            && request == inventoryRequest && inventoryOwner == GameApiClient.UserId;

        void DrawInventory()
        {
            Clear(inventoryCards);
            ReleaseShopTextures();
            inventoryFilter = Mathf.Clamp(inventoryFilter, 0, 2);
            var items = inventoryItems.Where(p => p.Kind == InventoryKinds[inventoryFilter]).ToArray();
            int pages = Mathf.Max(1, (items.Length + 2) / 3);
            inventoryPage = Mathf.Clamp(inventoryPage, 0, pages - 1);
            inventoryStatus.text = items.Length == 0 ? "구매한 상품이 없습니다. 상점에서 구매해 주세요." : inventorySaving ? "장착 상태를 저장하는 중…" :
                inventoryFilter == 2 ? "메시지는 여러 묶음을 함께 선택할 수 있습니다." : "한 번에 하나씩 장착합니다. 해제하면 기본 선택 상태로 돌아갑니다.";
            for (int i = 0; i < 3 && inventoryPage * 3 + i < items.Length; i++)
            {
                var p = items[inventoryPage * 3 + i];
                float x = 40 + i * 390;
                Box("OwnedCard" + p.Id, inventoryCards, x, 175, 370, 410, p.Equipped ? new Color(.16f, .22f, .12f) : new Color(.12f, .14f, .19f));
                Text("EquippedStatus", inventoryCards, p.Equipped ? "장착 중" : "보유 중", x + 10, 184, 350, 35, 24, p.Equipped ? Yellow : Color.white);
                var image = Art("OwnedImage", inventoryCards, null, x + 110, 225, 150, 95);
                image.color = Color.clear;
                var placeholder = Text("ImagePlaceholder", inventoryCards, "이미지 준비 중", x + 10, 240, 350, 55, 25, Color.gray);
                string asset = p.Thumbnail;
                if (string.IsNullOrWhiteSpace(asset)) asset = p.Kind == "Cue" ? p.Visual : p.Emotions?.FirstOrDefault(e => e.Code == "기본")?.Asset;
                StartCoroutine(LoadShopImage(asset, image, placeholder));
                Text("OwnedName", inventoryCards, p.Name, x + 12, 327, 346, 55, 29, Color.white, autoSize: true);
                string detail = p.Kind == "Cue" ? $"파워 {p.Power:+0;-0;0}% · 팔로스루 {p.FollowThrough:+0;-0;0}%\n당점 {p.Tip:+0;-0;0}% · 준비시간 {p.ShotSeconds:+0;-0;0}초" :
                    p.Kind == "Character" ? "감정 " + (p.Emotions?.Length ?? 0) + "종\n" + string.Join(", ", (p.Emotions ?? Array.Empty<ShopEmotionData>()).Select(e => e.Code)) :
                    p.Personality + "\n" + string.Join("\n", p.Messages ?? Array.Empty<string>());
                Text("OwnedDescription", inventoryCards, detail, x + 15, 389, 340, 138, 23, Color.white, TextAlignmentOptions.TopLeft, true);
                string label = p.Equipped ? "장착 해제" : "장착하기";
                SolidButton(label, inventoryCards, x + 35, 535, 300, 44, () => SetInventoryEquipment(p), p.Equipped);
            }
            if (pages > 1)
            {
                SolidButton("이전", inventoryCards, 350, 610, 150, 44, () => { inventoryPage = (inventoryPage + pages - 1) % pages; DrawInventory(); });
                Text("InventoryPage", inventoryCards, $"{inventoryPage + 1} / {pages}", 515, 610, 200, 44, 26, Yellow);
                SolidButton("다음", inventoryCards, 730, 610, 150, 44, () => { inventoryPage = (inventoryPage + 1) % pages; DrawInventory(); });
            }
        }

        async void SetInventoryEquipment(ShopProductData product)
        {
            if (inventorySaving || !Application.isPlaying || string.IsNullOrEmpty(GameApiClient.UserId)) return;
            inventorySaving = true;
            int request = inventoryRequest;
            var panel = inventoryPanel;
            string owner = inventoryOwner;
            bool desired = !product.Equipped;
            DrawInventory();
            try
            {
                await GameApiClient.Request<object>("/api/me/equipment", "POST", new ShopEquipmentInput { Slot = product.Kind, ProductId = product.Id, Equipped = desired });
                // Render only a fresh server snapshot, including changes made by another device.
                var result = await GameApiClient.Request<ShopCatalogData>("/api/me/inventory");
                if (!InventoryCurrent(request, panel)) return;
                inventoryItems = result?.Products ?? Array.Empty<ShopProductData>();
                inventorySaving = false;
                DrawInventory();
                inventoryStatus.text = desired ? "장착했습니다." : "장착을 해제했습니다.";
            }
            catch (GameApiException e)
            {
                if (InventoryCurrent(request, panel)) inventoryStatus.text = e.Message == "EquipmentNotOwned" ? "보유하지 않은 상품입니다. 새로고침해 주세요." :
                    "장착 결과를 확인하지 못했습니다. 새로고침으로 현재 상태를 확인해 주세요.";
            }
            catch (Exception)
            { if (InventoryCurrent(request, panel)) inventoryStatus.text = "장착 결과를 확인하지 못했습니다. 새로고침해 주세요."; }
            finally
            {
                inventorySaving = false;
                if (this && owner == GameApiClient.UserId && request != inventoryRequest && inventoryPanel && inventoryPanel.gameObject.activeInHierarchy)
                    ReadInventory(++inventoryRequest, inventoryPanel);
            }
        }
    }
}
