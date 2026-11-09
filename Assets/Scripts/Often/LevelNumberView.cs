using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public sealed class LevelNumberView : MonoBehaviour
{
    [SerializeField, Range(0, 9999)] private int level = 2;
    [SerializeField] private Sprite[] digits;
    [SerializeField] private Image[] places;

    public void SetLevel(int value)
    {
        level = Mathf.Clamp(value, 0, 9999);
        Refresh();
    }

    private void OnEnable() => Refresh();
    private void OnRectTransformDimensionsChange() => Refresh();

#if UNITY_EDITOR
    private void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall -= Refresh;
        UnityEditor.EditorApplication.delayCall += Refresh;
    }
#endif

    private void Refresh()
    {
        if (!this || !isActiveAndEnabled || digits == null || digits.Length != 10 ||
            places == null || places.Length != 4)
            return;

        string number = level.ToString(CultureInfo.InvariantCulture);
        const float gap = 0.035f;
        float width = gap * (number.Length - 1);

        for (int i = 0; i < number.Length; i++)
        {
            Sprite digit = digits[number[i] - '0'];
            if (!digit) return;
            width += digit.rect.width / digit.rect.height;
        }

        Rect bounds = ((RectTransform)transform).rect;
        float height = Mathf.Min(bounds.height, bounds.width / width);
        float x = -width * height * 0.5f;
        for (int i = 0; i < places.Length; i++)
        {
            Image place = places[i];
            if (!place) return;
            place.enabled = i < number.Length;
            if (!place.enabled) continue;

            Sprite digit = digits[number[i] - '0'];
            float digitWidth = height * digit.rect.width / digit.rect.height;
            place.sprite = digit;
            place.rectTransform.sizeDelta = new Vector2(digitWidth, height);
            place.rectTransform.anchoredPosition = new Vector2(x + digitWidth * 0.5f, 0);
            x += digitWidth + gap * height;
        }
    }
}
