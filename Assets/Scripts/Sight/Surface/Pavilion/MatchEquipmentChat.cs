using System.Collections;
using System.Linq;
using Assets.Scripts.Exert.Network;
using Assets.Scripts.Prack.BackSys.GameData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Sight.Surface.Pavilion
{
    public sealed class MatchEquipmentChat : MonoBehaviour
    {
        static readonly Color Ink = new Color32(24, 27, 35, 255);
        static readonly Color Gold = new Color32(255, 201, 45, 255);
        PunNetwork network;
        TMP_FontAsset font;
        Sprite rounded;
        RectTransform popup, ownAvatar, opponentAvatar;
        TMP_Text selfMessage, otherMessage, status;
        float selfUntil, otherUntil;
        int page;
        Coroutine selection;
        Button avatarButton, opponentButton, ownTimeButton, otherTimeButton;
        MatchPlayerInfoWindow playerInfo;
        string displayedMatch;
        RectTransform Panel => (RectTransform)transform;

        public void Initialize(PunNetwork value)
        {
            network = value;
            displayedMatch = network.LegacyMatchId;
            font = Resources.Load<TMP_FontAsset>("Common/Fonts/Asset/NanumBarunGothic SDF");
            rounded = CreateRoundedSprite();
            ownAvatar = (RectTransform)transform.Find("PlayerBoxs/PlayerSelf/Avatar");
            opponentAvatar = (RectTransform)transform.Find("PlayerBoxs/PlayerOther/Avatar");
            playerInfo=gameObject.AddComponent<MatchPlayerInfoWindow>(); playerInfo.Initialize(network);
            avatarButton=Bind(ownAvatar,ShowSelfInformation);
            opponentButton=Bind(opponentAvatar,ShowOtherInformation);
            // Bind the fixed track, not the shrinking fill, so it stays clickable near timeout.
            ownTimeButton=Bind((RectTransform)transform.Find("PlayerBoxs/PlayerSelf/TimeInning"),Toggle);
            otherTimeButton=Bind((RectTransform)transform.Find("PlayerBoxs/PlayerOther/TimeInning"),Toggle);
            selfMessage = Bubble("SelfChat", true);
            otherMessage = Bubble("OtherChat", false);
            network.EquippedMessageReceived += Receive;
        }

        void Toggle()
        {
            playerInfo.Close();
            if (popup) Close();
            else { page = 0; Open(); }
        }
        void ShowSelfInformation() { Close(); playerInfo.Open(network.LegacyLocalSeat); }
        void ShowOtherInformation() { Close(); playerInfo.Open(1-network.LegacyLocalSeat); }
        static Button Bind(RectTransform rect,UnityEngine.Events.UnityAction action)
        {
            var image=rect.GetComponent<Image>();
            if(!image) { image=rect.gameObject.AddComponent<Image>(); image.color=Color.clear; }
            image.raycastTarget=true;
            var button=rect.GetComponent<Button>() ?? rect.gameObject.AddComponent<Button>();
            button.targetGraphic=image; button.transition=Selectable.Transition.None; button.onClick.AddListener(action);
            return button;
        }

        // Use avatar geometry in the HUD's local space, including Canvas scaling.
        void Place(RectTransform box, RectTransform avatar, bool left)
        {
            var bottom = Panel.InverseTransformPoint(avatar.TransformPoint(new Vector2(avatar.rect.center.x, avatar.rect.yMin)));
            float x = left ? bottom.x - 28 : bottom.x - box.rect.width + 28;
            box.anchorMin = box.anchorMax = Panel.pivot;
            box.pivot = new Vector2(0, 1);
            box.anchoredPosition = new Vector2(Mathf.Clamp(x, Panel.rect.xMin + 6, Panel.rect.xMax - box.rect.width - 6), bottom.y - 10);
        }

        TMP_Text Bubble(string name, bool self)
        {
            var root = Rect(name, transform, 0, 0, 200, 54);
            var tail = Rect("Tail", root, 0, 0, 18, 18);
            tail.anchorMin = tail.anchorMax = new Vector2(self ? 0 : 1, 1);
            tail.pivot = Vector2.one * .5f;
            tail.anchoredPosition = new Vector2(self ? 28 : -28, -2);
            tail.localRotation = Quaternion.Euler(0, 0, 45);
            Fill(tail, Color.white, false, false);
            var face = Rect("Face", root, 0, 0, 0, 0);
            Stretch(face, 0); Fill(face, Color.white, false);
            var label = Label(Rect("Text", root, 0, 0, 0, 0), "", 25, Ink);
            Stretch(label.rectTransform, 9);
            root.gameObject.SetActive(false);
            return label;
        }

        void Open()
        {
            Close();
            if (!network || !network.HasLegacyMatch || network.MatchEquipment == null) return;
            var profile = network.MatchEquipment.Players[network.LegacyLocalSeat];
            var packs = profile.MessagePacks;
            if ((packs == null || packs.Length == 0) && profile.Messages.Length > 0)
                packs = new[] { new MatchMessagePack { Name = "보유 메시지", Indices = Enumerable.Range(0, profile.Messages.Length).ToArray() } };
            int total = packs?.Length ?? 0;
            int columns = Mathf.Clamp(Mathf.FloorToInt((Panel.rect.width - 36) / 218), 1, 3);
            int start = page * columns;
            int count = Mathf.Min(columns, total - start);
            int rows = 1;
            for (int i = 0; i < count; i++) rows = Mathf.Max(rows, Mathf.Min(4, packs[start+i].Indices.Length));
            float width = Mathf.Min(244, (Panel.rect.width - 36) / columns - 8);
            float height = 52 + rows * 45;
            float outerWidth = count == 0 ? 330 : 24 + count * (width + 8);
            popup = Rect("EquippedChatMenu", transform, 0, 0, outerWidth, height + 28 + (total > columns ? 42 : 0));
            Fill(popup, Ink);
            Place(popup, ownAvatar, true);
            for (int i = 0; i < count; i++)
                Card(packs[start+i], profile.Messages, i, width, height);
            if (count == 0) Label(Rect("Empty", popup, 10, 10, outerWidth - 20, height), "보유한 메시지가 없습니다.", 23, Color.white);
            if (total > columns)
            {
                PageButton("이전", 16, -1, start > 0);
                PageButton("다음", outerWidth - 90, 1, start + count < total);
            }
        }

        void Card(MatchMessagePack pack, string[] messages, int column, float width, float height)
        {
            var card = Rect("Pack" + column, popup, 16 + column * (width + 8), 14, width, height);
            Fill(card, Color.white);
            var title = Label(Rect("Name", card, 14, 3, width - 28, 41), pack.Name, 24, Ink);
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.MidlineLeft;
            Fill(Rect("Divider", card, 14, 44, width - 28, 2), new Color32(203, 205, 211, 255), false, false);
            var viewport = Rect("Rows", card, 6, 48, width - 12, height - 52);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Content", viewport, 0, 0, width - 12, pack.Indices.Length * 45);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;
            for (int i = 0; i < pack.Indices.Length; i++)
            {
                int index = pack.Indices[i];
                var row = Rect("Entry" + index, content, 0, 45*i, width - 12, 43);
                var bg = Fill(row, Color.white);
                var label = Label(Rect("Label", row, 8, 0, width - 28, 43), messages[index], 22, Ink);
                label.alignment = TextAlignmentOptions.MidlineLeft;
                var button = row.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => { if (selection == null) selection = StartCoroutine(Send(index, bg)); });
            }
        }

        IEnumerator Send(int index, Image row)
        {
            row.color = Gold;
            yield return new WaitForSecondsRealtime(.15f);
            selection = null;
            if (network && network.SendEquippedMessage(index)) Close();
            else
            {
                row.color = Color.white;
                if (!status)
                {
                    var strip = Rect("Status", popup, 0, popup.rect.height, popup.rect.width, 34);
                    Fill(strip, Ink);
                    status = Label(Rect("Text", strip, 0, 0, strip.rect.width, 34), "잠시 후 다시 보내 주세요.", 18, Color.white);
                }
            }
        }

        void PageButton(string caption, float x, int direction, bool enabled)
        {
            var rect = Rect(caption, popup, x, popup.rect.height - 44, 74, 36);
            var image = Fill(rect, enabled ? Color.white : Color.gray);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image; button.interactable = enabled;
            button.onClick.AddListener(() => { page += direction; Open(); });
            Label(Rect("Text", rect, 0, 0, 74, 36), caption, 19, Ink);
        }

        void Receive(int seat, string message)
        {
            if (!isActiveAndEnabled) return;
            bool self = seat == network.LegacyLocalSeat;
            var label = self ? selfMessage : otherMessage;
            var box = (RectTransform)label.transform.parent;
            label.text = message;
            float width = Mathf.Clamp(label.GetPreferredValues(message).x + 40, 125, Mathf.Min(420, Panel.rect.width - 16));
            box.sizeDelta = new Vector2(width, Mathf.Max(54, label.GetPreferredValues(message, width - 18, Mathf.Infinity).y + 20));
            Place(box, self ? ownAvatar : opponentAvatar, self);
            box.gameObject.SetActive(true);
            if (self) selfUntil = Time.unscaledTime + 3; else otherUntil = Time.unscaledTime + 3;
        }

        void Update()
        {
            if (displayedMatch != network?.LegacyMatchId)
            {
                displayedMatch = network?.LegacyMatchId;
                Clear();
            }
            if (selfMessage && Time.unscaledTime > selfUntil) selfMessage.transform.parent.gameObject.SetActive(false);
            if (otherMessage && Time.unscaledTime > otherUntil) otherMessage.transform.parent.gameObject.SetActive(false);
            if (popup && (!network || !network.HasLegacyMatch)) Close();
        }

        void Close()
        {
            if (selection != null) { StopCoroutine(selection); selection = null; }
            if (popup) { popup.gameObject.SetActive(false); Destroy(popup.gameObject); popup = null; }
            status = null;
        }
        void Clear()
        {
            Close();
            if(playerInfo) playerInfo.Close();
            if (selfMessage) selfMessage.transform.parent.gameObject.SetActive(false);
            if (otherMessage) otherMessage.transform.parent.gameObject.SetActive(false);
        }
        void OnDisable() => Clear();
        static Sprite CreateRoundedSprite()
        {
            const int size = 32, radius = 12;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Abs(x + .5f - size / 2) - (size / 2 - radius));
                    float dy = Mathf.Max(0, Mathf.Abs(y + .5f - size / 2) - (size / 2 - radius));
                    byte alpha = (byte)(255 * Mathf.Clamp01(radius + .5f - Mathf.Sqrt(dx * dx + dy * dy)));
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            texture.SetPixels32(pixels); texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
        }
        Image Fill(RectTransform rect, Color color, bool raycast = true, bool curved = true)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = raycast;
            if (curved) { image.sprite = rounded; image.type = Image.Type.Sliced; }
            return image;
        }
        TMP_Text Label(RectTransform root, string value, int size, Color color)
        {
            var text = root.gameObject.AddComponent<TextMeshProUGUI>(); text.font = font;
            text.text = value; text.richText = false; text.alignment = TextAlignmentOptions.Center;
            text.color = color; text.raycastTarget = false; text.enableAutoSizing = true; text.fontSizeMin = 15; text.fontSizeMax = size;
            return text;
        }
        static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * inset; rect.offsetMax = -Vector2.one * inset;
        }
        static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.gameObject.layer = parent.gameObject.layer;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.up;
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
            return rect;
        }
        void OnDestroy()
        {
            if (avatarButton) avatarButton.onClick.RemoveListener(ShowSelfInformation);
            if (opponentButton) opponentButton.onClick.RemoveListener(ShowOtherInformation);
            if (ownTimeButton) ownTimeButton.onClick.RemoveListener(Toggle);
            if (otherTimeButton) otherTimeButton.onClick.RemoveListener(Toggle);
            if (network) network.EquippedMessageReceived -= Receive;
            if (rounded) { Destroy(rounded.texture); Destroy(rounded); }
        }
    }
}
