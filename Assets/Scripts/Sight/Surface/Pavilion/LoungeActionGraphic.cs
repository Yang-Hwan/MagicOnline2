using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Sight.Surface.Pavilion
{
    // Text-free painted PNG plates; text and interaction remain separate UI elements.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LoungeActionGraphic : MaskableGraphic
    {
        public Color Accent = Color.yellow;
        public bool Filled;
        public enum PlateKind { Action, Name, Status, Stat }
        public PlateKind Kind;
        static Texture2D nameArt, actionArt, darkArt, statArt;
        bool DarkAction => Kind == PlateKind.Action && Accent.maxColorComponent < .3f;
        public override Texture mainTexture {
            get {
                if (Kind == PlateKind.Stat) return statArt ? statArt : (statArt=Resources.Load<Texture2D>("Menu/rematch-frame-stat"));
                if (Kind == PlateKind.Name) return nameArt ? nameArt : (nameArt=Resources.Load<Texture2D>("Menu/rematch-frame-name"));
                if (Kind == PlateKind.Action && !DarkAction) return actionArt ? actionArt : (actionArt=Resources.Load<Texture2D>("Menu/rematch-frame-action"));
                return darkArt ? darkArt : (darkArt=Resources.Load<Texture2D>("Menu/rematch-frame-dark"));
            }
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            // UV windows omit transparent canvas margins without modifying source artwork.
            Rect uv = Kind == PlateKind.Stat ? new Rect(.05f,.20f,.905f,.64f) : Kind == PlateKind.Name ? new Rect(0, .24f, 1, .55f) :
                Kind == PlateKind.Action && !DarkAction ? new Rect(0,.16f,1,.70f) : new Rect(0,.21f,1,.58f);
            Color tint = (DarkAction ? Color.white : Accent) * color;
            var r=rectTransform.rect;
            vh.AddVert(new Vector3(r.xMin,r.yMin),tint,new Vector2(uv.xMin,uv.yMin));
            vh.AddVert(new Vector3(r.xMin,r.yMax),tint,new Vector2(uv.xMin,uv.yMax));
            vh.AddVert(new Vector3(r.xMax,r.yMax),tint,new Vector2(uv.xMax,uv.yMax));
            vh.AddVert(new Vector3(r.xMax,r.yMin),tint,new Vector2(uv.xMax,uv.yMin));
            vh.AddTriangle(0,1,2);vh.AddTriangle(2,3,0);
        }
    }
}
