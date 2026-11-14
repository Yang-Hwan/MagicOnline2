using UnityEngine;
using UnityEngine.UI;
namespace Assets.Scripts.Sight.Surface.Pavilion
{
    // Invisible portrait mask aligned with the transparent opening of the painted frame.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RematchAvatarShape : MaskableGraphic
    {
        public bool MirrorHorizontally { get; private set; }
        public static Image CreatePortrait(RectTransform frame, Color accent, bool mirrorHorizontally = false)
        {
            RectTransform Layer(string label, Transform parent, Vector2 min, Vector2 max)
            {
                var rect = (RectTransform)new GameObject(label, typeof(RectTransform)).transform;
                rect.SetParent(parent, false);
                rect.anchorMin = min; rect.anchorMax = max;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                return rect;
            }

            var backing = Layer("PortraitBackdrop", frame, Vector2.zero, Vector2.one);
            var shape = backing.gameObject.AddComponent<RematchAvatarShape>();
            shape.MirrorHorizontally = mirrorHorizontally;
            shape.color = Color.Lerp(new Color(.065f,.085f,.13f), accent, .42f);
            shape.raycastTarget = false;
            backing.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var face = Layer("NeutralAvatar", backing, new Vector2(.09f,.08f), new Vector2(.91f,.89f)).gameObject.AddComponent<Image>();
            face.raycastTarget = false; face.preserveAspect = true;
            var rim = Layer("PaintedFrame", frame, Vector2.zero, Vector2.one).gameObject.AddComponent<RawImage>();
            rim.texture = Resources.Load<Texture2D>("Menu/rematch-frame-portrait");
            if (mirrorHorizontally) rim.uvRect = new Rect(1, 0, -1, 1);
            rim.color = accent; rim.raycastTarget = false;
            return face;
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;
            var points=new[]{new Vector2(.17f,.895f),new Vector2(.858f,.812f),new Vector2(.926f,.09f),new Vector2(.083f,.12f)};
            foreach(var p in points)vh.AddVert(new Vector3(r.xMin+(MirrorHorizontally?1-p.x:p.x)*r.width,r.yMin+p.y*r.height),color,p);
            if (MirrorHorizontally) { vh.AddTriangle(0,3,2); vh.AddTriangle(2,1,0); }
            else { vh.AddTriangle(0,1,2); vh.AddTriangle(2,3,0); }
        }
    }
}
