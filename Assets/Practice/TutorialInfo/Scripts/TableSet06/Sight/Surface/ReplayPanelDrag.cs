using UnityEngine;
using UnityEngine.EventSystems;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Surface
{
    // Only the header receives drag events; buttons and the title keep their own input.
    public sealed class ReplayPanelDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        RectTransform panel, bounds;
        Vector2 grabOffset, previousBoundsSize;
        readonly Vector3[] corners = new Vector3[4];
        int pointerId;
        bool dragging;

        public void Initialize(RectTransform target)
        {
            panel = target;
            bounds = (RectTransform)target.parent;
            FitInsideCanvas();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!panel || dragging || eventData.button != PointerEventData.InputButton.Left) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(bounds, eventData.position,
                eventData.pressEventCamera, out var local)) return;
            pointerId = eventData.pointerId;
            grabOffset = (Vector2)panel.localPosition - local;
            dragging = true;
            panel.SetAsLastSibling();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging || eventData.pointerId != pointerId) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(bounds, eventData.position,
                eventData.pressEventCamera, out var local)) return;
            var position = local + grabOffset;
            panel.localPosition = new Vector3(position.x, position.y, panel.localPosition.z);
            ClampInsideCanvas();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.pointerId == pointerId) dragging = false;
        }

        void OnDisable() => dragging = false;

        void LateUpdate()
        {
            if (bounds && bounds.rect.size != previousBoundsSize) FitInsideCanvas();
        }

        void FitInsideCanvas()
        {
            previousBoundsSize = bounds.rect.size;
            if (previousBoundsSize.x <= 0 || previousBoundsSize.y <= 0) return;
            float scale = Mathf.Min(1f, previousBoundsSize.x / panel.rect.width, previousBoundsSize.y / panel.rect.height);
            panel.localScale = Vector3.one * scale;
            dragging = false;
            ClampInsideCanvas();
        }

        void ClampInsideCanvas()
        {
            panel.GetWorldCorners(corners);
            Vector2 min = bounds.InverseTransformPoint(corners[0]);
            Vector2 max = bounds.InverseTransformPoint(corners[2]);
            var rect = bounds.rect;
            float x = min.x < rect.xMin ? rect.xMin - min.x : max.x > rect.xMax ? rect.xMax - max.x : 0;
            float y = min.y < rect.yMin ? rect.yMin - min.y : max.y > rect.yMax ? rect.yMax - max.y : 0;
            panel.localPosition += new Vector3(x, y, 0);
        }
    }
}
