using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Sight.Surface.Come
{
    // Positions are measured in the approved 1672 x 941 artwork. The entire view
    // scales together so hit targets and native text stay aligned at every aspect ratio.
    public sealed class StarryLoginView : MonoBehaviour
    {
        const float Width = 1672, Height = 941;
        RectTransform root;
        TMP_InputField id, password;
        Button login, register;

        public static bool Apply(PnlLogin owner, TMP_InputField id, TMP_InputField password,
            Button login, Button register, TextMeshProUGUI message)
        {
            var texture = Resources.Load<Texture2D>("Login/StarryNightBackplate");
            if (!texture) { Debug.LogError("Login background is missing."); return false; }
            var root = (RectTransform)owner.transform;
            var view = owner.gameObject.AddComponent<StarryLoginView>();
            view.root = root; view.id = id; view.password = password;
            view.login = login; view.register = register;
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, .5f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = new Vector2(Width, Height);
            var plate = new GameObject("StarryNightBackplate", typeof(RectTransform), typeof(RawImage));
            plate.transform.SetParent(root, false); plate.transform.SetAsFirstSibling();
            var art = plate.GetComponent<RawImage>(); art.texture = texture; art.raycastTarget = false;
            Place(plate.GetComponent<RectTransform>(), 0, 0, Width, Height);

            Input(id, 600, 451, 473, 62, "아이디");
            Input(password, 600, 527, 473, 59, "비밀번호");
            password.contentType = TMP_InputField.ContentType.Password;
            id.characterLimit = 32; password.characterLimit = 128;
            Action(login, 600, 605, 473, 73, "로그인  ›", new Color(.03f, .04f, .08f), 38);
            Action(register, 600, 694, 473, 58, "회원가입", new Color(.65f, .74f, 1), 29);
            // Temporary shortcuts retain PnlLogin's existing user01/user02 handlers
            // and its shared busy-state protection.
            TestAction(root.Find("BtnLoginTemp").GetComponent<Button>(), 600, "테스트로그인1\nuser01");
            TestAction(root.Find("BtnLoginTemp2").GetComponent<Button>(), 845, "테스트로그인2\nuser02");
            var heading = new GameObject("LoginHeading", typeof(RectTransform), typeof(TextMeshProUGUI));
            heading.transform.SetParent(root, false);
            var title = heading.GetComponent<TextMeshProUGUI>(); title.font = id.textComponent.font;
            title.text = "로그인"; title.fontSize = 39; title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.Center; title.color = Color.white; title.raycastTarget = false;
            Place(title.rectTransform, 750, 385, 172, 52);
            message.transform.SetParent(root, false);
            Place(message.rectTransform, 481, 858, 710, 65);
            message.text = ""; message.fontSize = 24; message.enableAutoSizing = true;
            message.fontSizeMin = 18; message.fontSizeMax = 24;
            message.alignment = TextAlignmentOptions.Center; message.color = new Color(1, .85f, .55f);
            message.raycastTarget = false;
            var navigation = new Navigation { mode = Navigation.Mode.Explicit };
            navigation.selectOnDown = password; id.navigation = navigation;
            navigation.selectOnUp = id; navigation.selectOnDown = login; password.navigation = navigation;
            navigation.selectOnUp = password; navigation.selectOnDown = register; login.navigation = navigation;
            navigation.selectOnUp = login; navigation.selectOnDown = id; register.navigation = navigation;
            view.Resize();
            return true;
        }

        static void TestAction(Button button, float x, string label)
        {
            button.gameObject.SetActive(true);
            Action(button, x, 793, 228, 54, label, new Color(.85f, .89f, 1), 21);
            var colors = button.colors;
            colors.normalColor = new Color(.035f, .06f, .17f, .96f);
            colors.highlightedColor = new Color(.15f, .22f, .42f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(.02f, .035f, .1f);
            colors.disabledColor = new Color(.04f, .04f, .07f, .6f);
            colors.fadeDuration = 0;
            button.colors = colors;
            button.GetComponent<Image>().canvasRenderer.SetColor(colors.normalColor);
        }
        static void Place(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h); rect.localScale = Vector3.one;
        }
        static void Stretch(RectTransform rect, float left = 0, float right = 0)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, 0); rect.offsetMax = new Vector2(-right, 0);
            rect.localScale = Vector3.one;
        }
        static void Input(TMP_InputField field, float x, float y, float w, float h, string placeholder)
        {
            Place((RectTransform)field.transform, x, y, w, h);
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.transition = Selectable.Transition.None;
            var image = field.GetComponent<Image>(); image.sprite = null; image.color = Color.clear;
            image.raycastTarget = true;
            Stretch(field.textViewport, 76, 18);
            Stretch(field.textComponent.rectTransform);
            field.textComponent.fontSize = 27; field.textComponent.enableAutoSizing = false;
            field.textComponent.color = Color.white; field.textComponent.alignment = TextAlignmentOptions.MidlineLeft;
            field.textComponent.fontStyle = FontStyles.Normal;
            field.caretColor = Color.white; field.customCaretColor = true;
            field.selectionColor = new Color(.35f, .5f, 1, .45f);
            if (field.placeholder is TMP_Text hint)
            {
                Stretch(hint.rectTransform); hint.text = placeholder;
                hint.fontSize = 27; hint.enableAutoSizing = false; hint.fontStyle = FontStyles.Normal;
                hint.color = new Color(.7f, .75f, .88f); hint.alignment = TextAlignmentOptions.MidlineLeft;
            }
        }
        static void Action(Button button, float x, float y, float w, float h, string label, Color color, float size)
        {
            Place((RectTransform)button.transform, x, y, w, h);
            var image = button.GetComponent<Image>(); image.sprite = null; image.color = Color.white;
            button.transition = Selectable.Transition.ColorTint;
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.clear; colors.highlightedColor = new Color(1, 1, 1, .12f);
            colors.selectedColor = new Color(1, 1, 1, .18f); colors.pressedColor = new Color(0, 0, 0, .3f);
            colors.disabledColor = new Color(0, 0, 0, .5f); button.colors = colors;
            var text = button.GetComponentInChildren<TMP_Text>(true);
            Stretch(text.rectTransform, 12, 12); text.text = label;
            text.fontSize = size; text.enableAutoSizing = false; text.fontStyle = FontStyles.Bold;
            text.color = color; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        }
        void Resize()
        {
            if (!root || !(root.parent is RectTransform parent)) return;
            float scale = Mathf.Min(parent.rect.width / Width, parent.rect.height / Height);
            root.localScale = Vector3.one * scale;
        }
        void LateUpdate() => Resize();
        void Update()
        {
            if (!InputKeyTab()) return;
            if (id.isFocused) password.Select();
            else if (password.isFocused) login.Select();
            else if (UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject == login.gameObject) register.Select();
            else id.Select();
        }
        static bool InputKeyTab() => UnityEngine.Input.GetKeyDown(KeyCode.Tab);
    }
}
