using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Assets.Scripts.Often;

namespace Assets.Scripts.Sight.Surface.Consist
{
    public sealed class PracticeMenuView : MonoBehaviour
    {
        GameObject panel;
        Sprite buttonBackground;
        TMP_FontAsset koreanFont;

        void Awake()
        {
            var sprites = Resources.LoadAll<Sprite>("Common/Sprites/single_btns");
            buttonBackground = sprites.FirstOrDefault(sprite => sprite.name == "single_btns_0");
            var three = sprites.FirstOrDefault(sprite => sprite.name == "single_btns_1");
            var four = sprites.FirstOrDefault(sprite => sprite.name == "single_btns_2");
            var replay = sprites.FirstOrDefault(sprite => sprite.name == "single_btns_3");
            koreanFont = Resources.Load<TMP_FontAsset>("Common/Fonts/Asset/NanumBarunGothic SDF");
            if (!buttonBackground || !three || !four || !replay || !koreanFont)
            {
                Debug.LogError("Practice menu sprites or Korean font could not be loaded from Resources.");
                return;
            }

            var backdrop = GetComponent<Image>();
            var layout = gameObject.AddComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = backdrop.preferredHeight;
            layout.flexibleHeight = 0;

            for (int i = 0; i < transform.childCount; i++) transform.GetChild(i).gameObject.SetActive(false);
            panel = new GameObject("Practice Menu", typeof(RectTransform));
            var root = (RectTransform)panel.transform;
            root.SetParent(transform, false);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;

            string[] labels = { "3구 연습", "4구 연습", "리플레이 - 개별", "리플레이 - 시합" };
            string[] names = { "Three Ball Practice", "Four Ball Practice", "Individual Replay", "Match Replay" };
            var icons = new[] { three, four, replay, replay };
            var modes = new[] { PracticeSceneFlow.Mode.ThreeBall, PracticeSceneFlow.Mode.FourBall,
                PracticeSceneFlow.Mode.Replay, PracticeSceneFlow.Mode.MatchReplay };

            for (int i = 0; i < labels.Length; i++)
            {
                var obj = new GameObject(names[i], typeof(RectTransform), typeof(Image), typeof(Button));
                var rect = (RectTransform)obj.transform;
                rect.SetParent(root, false);
                rect.anchorMin = new Vector2(.27f, .73f - i * .18f);
                rect.anchorMax = new Vector2(.77f, .91f - i * .18f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;

                var picture = obj.GetComponent<Image>();
                picture.sprite = buttonBackground;
                picture.type = Image.Type.Sliced;
                var button = obj.GetComponent<Button>();
                button.targetGraphic = picture;
                var colors = button.colors;
                colors.highlightedColor = new Color(1f, .95f, .8f);
                colors.pressedColor = new Color(.75f, .75f, .75f);
                button.colors = colors;
                var mode = modes[i];
                button.onClick.AddListener(() => PracticeSceneFlow.Enter(mode));

                var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                var iconRect = (RectTransform)iconObject.transform;
                iconRect.SetParent(rect, false);
                iconRect.anchorMin = new Vector2(.08f, .18f);
                iconRect.anchorMax = new Vector2(.25f, .82f);
                iconRect.offsetMin = iconRect.offsetMax = Vector2.zero;
                iconObject.GetComponent<Image>().sprite = icons[i];
                iconObject.GetComponent<Image>().preserveAspect = true;
                iconObject.GetComponent<Image>().raycastTarget = false;

                var textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                var textRect = (RectTransform)textObject.transform;
                textRect.SetParent(rect, false);
                textRect.anchorMin = new Vector2(.27f, .1f);
                textRect.anchorMax = new Vector2(.94f, .9f);
                textRect.offsetMin = textRect.offsetMax = Vector2.zero;
                var text = textObject.GetComponent<TMP_Text>();
                text.text = labels[i];
                text.font = koreanFont;
                text.fontSize = 40;
                text.color = new Color(.24f, .13f, .055f);
                text.alignment = TextAlignmentOptions.MidlineLeft;
                text.raycastTarget = false;
            }
            panel.SetActive(false);
        }

        public void SetVisible(bool visible) { if (panel) panel.SetActive(visible); }
    }
}
