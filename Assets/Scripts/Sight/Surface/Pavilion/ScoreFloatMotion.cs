using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Sight.Surface.Pavilion
{
    // A presentation of the confirmed result, not the number of points to award.
    public sealed class ScoreFloatMotion : MonoBehaviour
    {
        static ScoreFloatMotion current;
        static Sprite plus, minus;
        readonly List<Image> glyphs = new List<Image>();
        RectTransform rect, canvasRect;
        CanvasGroup opacity;
        Behaviour owner;
        Vector2 origin;
        float elapsed = Duration;
        const float Duration = .9f;

        public static void Install(RectTransform canvas, Behaviour panel)
        {
            var go = new GameObject("Score Float Motion", typeof(RectTransform), typeof(CanvasGroup), typeof(ScoreFloatMotion));
            go.layer = canvas.gameObject.layer;
            go.transform.SetParent(canvas, false);
            var view = go.GetComponent<ScoreFloatMotion>();
            view.rect = go.GetComponent<RectTransform>();
            view.rect.anchorMin = view.rect.anchorMax = new Vector2(.5f, .5f);
            view.rect.sizeDelta = new Vector2(240, 80);
            view.canvasRect = canvas;
            view.owner = panel;
            view.opacity = go.GetComponent<CanvasGroup>();
            view.opacity.alpha = 0;
            view.opacity.blocksRaycasts = false;
            view.opacity.interactable = false;
            current = view;
        }

        public static void Show(Vector3 worldPosition, Camera camera, int result)
        {
            if (!current || !camera || !current.owner || !current.owner.isActiveAndEnabled || result == 0) return;
            var screen = camera.WorldToScreenPoint(worldPosition);
            if (screen.z <= 0 || !current.SetArtwork(result)) return;
            // Capture once. Never retain a cue-ball Transform or track it during animation.
            RectTransformUtility.ScreenPointToLocalPointInRectangle(current.canvasRect, screen, null, out var point);
            current.origin = point + Vector2.up * 62;
            var bounds = current.canvasRect.rect;
            current.origin.x = Mathf.Clamp(current.origin.x, bounds.xMin + 120, bounds.xMax - 120);
            current.origin.y = Mathf.Clamp(current.origin.y, bounds.yMin + 40, bounds.yMax - 78);
            current.rect.anchoredPosition = current.origin;
            current.rect.localScale = Vector3.one * .85f;
            current.opacity.alpha = 1;
            current.elapsed = 0;
        }

        bool SetArtwork(int result)
        {
            if (!plus)
            {
                var atlas = Resources.Load<Texture2D>("Gameplay/ScoreSigns");
                if (!atlas) return false;
                // Original 1774 x 887 transparent brush artwork, no raster resampling.
                plus = MakeSign(atlas, new Rect(121, 69, 670, 678));
                minus = MakeSign(atlas, new Rect(1005, 264, 675, 467));
            }
            if (!CushionCountMotion.BrushGlyph(0)) return false;
            string digits = (result < 0 ? 1 : result).ToString(System.Globalization.CultureInfo.InvariantCulture);
            int count = digits.Length + 1;
            while (glyphs.Count < count)
            {
                var go = new GameObject("Brush Glyph", typeof(RectTransform), typeof(Image));
                go.layer = gameObject.layer;
                go.transform.SetParent(transform, false);
                var image = go.GetComponent<Image>();
                image.raycastTarget = false;
                glyphs.Add(image);
            }
            float width = 0;
            for (int i = 0; i < glyphs.Count; i++)
            {
                var image = glyphs[i];
                image.gameObject.SetActive(i < count);
                if (i >= count) continue;
                image.sprite = i == 0 ? (result < 0 ? minus : plus) : CushionCountMotion.BrushGlyph(digits[i - 1] - '0');
                image.color = result < 0 ? new Color(1, .16f, .19f) : new Color(1, .87f, .12f);
                float height = i == 0 ? (result < 0 ? 30 : 42) : 66;
                float w = height * image.sprite.rect.width / image.sprite.rect.height;
                image.rectTransform.sizeDelta = new Vector2(w, height);
                width += w;
            }
            float x = -width * .5f;
            for (int i = 0; i < count; i++)
            {
                var r = glyphs[i].rectTransform;
                r.anchoredPosition = new Vector2(x + r.sizeDelta.x * .5f, 0);
                x += r.sizeDelta.x;
            }
            return true;
        }

        static Sprite MakeSign(Texture2D atlas, Rect pixels)
        {
            var uv = new Rect(pixels.x * atlas.width / 1774f, pixels.y * atlas.height / 887f,
                pixels.width * atlas.width / 1774f, pixels.height * atlas.height / 887f);
            return Sprite.Create(atlas, uv, new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
        }

        public static void Hide()
        {
            if (!current) return;
            current.elapsed = Duration;
            current.opacity.alpha = 0;
        }

        void Update()
        {
            if (!owner || !owner.isActiveAndEnabled) { opacity.alpha = 0; elapsed = Duration; return; }
            if (elapsed >= Duration) return;
            elapsed = Mathf.Min(Duration, elapsed + Time.unscaledDeltaTime);
            float t = elapsed / Duration;
            rect.anchoredPosition = origin + Vector2.up * (32 * Mathf.SmoothStep(0, 1, t));
            rect.localScale = Vector3.one * Mathf.Lerp(.85f, 1, Mathf.Clamp01(elapsed / .12f));
            opacity.alpha = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - .3f) / .6f));
        }

        void OnDisable() { opacity.alpha = 0; elapsed = Duration; }
        void OnDestroy() { if (current == this) current = null; }
    }
}
