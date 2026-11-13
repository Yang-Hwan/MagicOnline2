using Assets.Scripts.Exert.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Sight.Surface.Pavilion
{
    public sealed class MatchEquipmentChat : MonoBehaviour
    {
        PunNetwork network;
        TMP_FontAsset font;
        RectTransform popup;
        TMP_Text selfMessage, otherMessage;
        TMP_Text sendStatus;
        float selfUntil, otherUntil;
        int page;
        Button avatarButton;
        string displayedMatch;
        public void Initialize(PunNetwork value)
        {
            network = value;
            displayedMatch = network.LegacyMatchId;
            font = Resources.Load<TMP_FontAsset>("Common/Fonts/Asset/NanumBarunGothic SDF");
            var avatar = transform.Find("PlayerBoxs/PlayerSelf/Avatar").GetComponent<Image>();
            avatar.raycastTarget = true;
            avatarButton = avatar.GetComponent<Button>() ?? avatar.gameObject.AddComponent<Button>();
            avatarButton.targetGraphic = avatar;
            avatarButton.onClick.AddListener(OpenFromAvatar);
            selfMessage = Bubble("SelfChat", .25f);
            otherMessage = Bubble("OtherChat", .75f);
            network.EquippedMessageReceived += Receive;
        }
        void OpenFromAvatar() { page = 0; Open(); }
        TMP_Text Bubble(string name, float x)
        {
            var root = Rect(name, transform, new Vector2(x, 1), new Vector2(0, -180), new Vector2(420, 96));
            var image = root.gameObject.AddComponent<Image>(); image.color = new Color(.025f, .035f, .05f, .92f); image.raycastTarget = false;
            var text = Label(Rect("Text", root, Vector2.one * .5f, Vector2.zero, new Vector2(395, 82)), "", 25);
            root.gameObject.SetActive(false); return text;
        }
        void Open()
        {
            Close();
            if (!network || !network.HasLegacyMatch || network.MatchEquipment == null) return;
            popup = Rect("EquippedChatMenu", transform, Vector2.one * .5f, Vector2.zero, new Vector2(540, 620));
            popup.gameObject.AddComponent<Image>().color = new Color(.035f, .04f, .055f, .98f);
            Label(Rect("Title", popup, new Vector2(.5f, 1), new Vector2(0, -48), new Vector2(500, 45)), "장착한 메시지", 30);
            var messages = network.MatchEquipment.Players[network.LegacyLocalSeat].Messages;
            int total = messages.Length;
            int pages = Mathf.Max(1, (total + 4) / 5); page = Mathf.Clamp(page, 0, pages - 1);
            for (int i = 0; i < 5 && page * 5 + i < total; i++)
            {
                int index = page * 5 + i;
                Button(Rect("Entry" + index, popup, new Vector2(.5f, 1), new Vector2(0, -112 - i * 65), new Vector2(485, 54)),
                    messages[index], () =>
                    {
                        bool sent = network.SendEquippedMessage(index);
                        if (sent) Close();
                        else if (sendStatus) sendStatus.text = "잠시 후 다시 보내 주세요.";
                    });
            }
            if (total == 0) Label(Rect("Empty", popup, Vector2.one * .5f, Vector2.zero, new Vector2(475, 160)), "다음 경기 전에 보유장비에서\n메시지 묶음을 장착해 주세요.", 27);
            sendStatus = Label(Rect("SendStatus", popup, new Vector2(.5f, 0), new Vector2(0, 155), new Vector2(480, 32)), "", 20);
            if (pages > 1)
            {
                Button(Rect("Prev", popup, new Vector2(.25f, 0), new Vector2(0, 105), new Vector2(140, 42)), "이전", () => { page = (page + pages - 1) % pages; Open(); });
                Button(Rect("Next", popup, new Vector2(.75f, 0), new Vector2(0, 105), new Vector2(140, 42)), "다음", () => { page = (page + 1) % pages; Open(); });
            }
            Button(Rect("Close", popup, new Vector2(.5f, 0), new Vector2(0, 43), new Vector2(250, 48)), "닫기", Close);
        }
        void Receive(int seat, string text)
        {
            bool self = seat == network.LegacyLocalSeat;
            (self ? selfMessage : otherMessage).text = text;
            (self ? selfMessage : otherMessage).transform.parent.gameObject.SetActive(true);
            if (self) selfUntil = Time.unscaledTime + 3; else otherUntil = Time.unscaledTime + 3;
        }
        void Update()
        {
            if (displayedMatch != network?.LegacyMatchId)
            {
                displayedMatch = network?.LegacyMatchId; Close();
                if (selfMessage) selfMessage.transform.parent.gameObject.SetActive(false);
                if (otherMessage) otherMessage.transform.parent.gameObject.SetActive(false);
            }
            if (selfMessage && Time.unscaledTime > selfUntil) selfMessage.transform.parent.gameObject.SetActive(false);
            if (otherMessage && Time.unscaledTime > otherUntil) otherMessage.transform.parent.gameObject.SetActive(false);
            if (popup && (!network || !network.HasLegacyMatch)) Close();
        }
        void Close() { if (popup) { popup.gameObject.SetActive(false); Destroy(popup.gameObject); popup = null; } }
        void OnDisable() => Close();
        TMP_Text Label(RectTransform root, string value, int size)
        {
            var text = root.gameObject.AddComponent<TextMeshProUGUI>(); text.font = font;
            text.text = value; text.richText = false; text.fontSize = size; text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white; text.raycastTarget = false; text.enableAutoSizing = true; text.fontSizeMin = 18; text.fontSizeMax = size;
            return text;
        }
        void Button(RectTransform root, string text, UnityEngine.Events.UnityAction action)
        {
            var image = root.gameObject.AddComponent<Image>(); image.color = new Color(.16f, .2f, .27f);
            var button = root.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
            Label(Rect("Label", root, Vector2.one * .5f, Vector2.zero, root.sizeDelta - new Vector2(12, 4)), text, 25);
        }
        static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.gameObject.layer = parent.gameObject.layer;
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = Vector2.one * .5f; rect.anchoredPosition = position; rect.sizeDelta = size;
            return rect;
        }
        void OnDestroy()
        {
            if (avatarButton) avatarButton.onClick.RemoveListener(OpenFromAvatar);
            if (network) network.EquippedMessageReceived -= Receive;
        }
    }
}
