using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Sight.Surface.Pavilion
{
    // Viewports into the original approved concept sheet: no redrawn VS or subpixel
    // specular strips. Texture filtering and fragment coverage survive mobile scaling.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MatchHudArtGraphic : MaskableGraphic
    {
        public const float RingWidth=13.6f,RingHeight=37,Spacing=14.4f;
        const float SheetWidth=2172,SheetHeight=724;
        MatchHudView view;
        Texture2D artwork;
        Material ownedMaterial;
        public override Texture mainTexture=>artwork?artwork:Texture2D.whiteTexture;
        public void Initialize(MatchHudView owner)
        {
            view=owner;raycastTarget=false;artwork=Resources.Load<Texture2D>("Gameplay/MatchHud/ReferenceArtwork");
            ownedMaterial=new Material(Resources.Load<Shader>("Gameplay/MatchHud/ReferenceArt"));material=ownedMaterial;
            var canvas=GetComponentInParent<Canvas>();canvas.rootCanvas.additionalShaderChannels|=AdditionalCanvasShaderChannels.TexCoord1;
            SetAllDirty();
        }
        public static float Position(MatchHudView view,int side,int index)
        {
            var n=view.Counters[side];int count=Mathf.Min(15,n.Target);float start=side==0?214:1055;
            float x=MatchHudGraphic.RingX(side,index,Mathf.Min(count,n.Displayed),count,start,290,Spacing);
            if(n.Moving!=index)return x;
            float p=n.Progress(view.VisualTime),ease=p<.5f?4*p*p*p:1-Mathf.Pow(-2*p+2,3)/2;
            float from=MatchHudGraphic.RingX(side,index,Mathf.Min(count,n.FromScore),count,start,290,Spacing);
            float to=MatchHudGraphic.RingX(side,index,Mathf.Min(count,n.ToScore),count,start,290,Spacing);
            return Mathf.Lerp(from,to,ease);
        }
        void Tile(VertexHelper vh,Rect destination,Rect source,bool ellipse)
        {
            int n=vh.currentVertCount;var rect=rectTransform.rect;
            for(int i=0;i<4;i++)
            {
                float x=i==1||i==2?1:0,y=i>=2?1:0;
                var v=UIVertex.simpleVert;v.position=new Vector3(rect.xMin+destination.x+x*destination.width,rect.yMax-destination.y-y*destination.height);
                v.uv0=new Vector2((source.x+x*source.width)/SheetWidth,1-(source.y+y*source.height)/SheetHeight);
                v.uv1=new Vector4(x,y,ellipse?1:0,0);v.color=Color.white;vh.AddVert(v);
            }
            vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n+2,n+3,n);
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(!view||!artwork)return;
            Tile(vh,new Rect(737.5f,10.25f,85,59.5f),new Rect(1028,89,128,88),false);
            for(int side=0;side<2;side++)for(int i=0;i<Mathf.Min(15,view.Counters[side].Target);i++)
            {
                var source=MatchHudGraphic.IsRedRing(i)?new Rect(399,226,24,57):new Rect(316,226,22,57);
                Tile(vh,new Rect(Position(view,side,i)-RingWidth*.5f,126-RingHeight*.5f,RingWidth,RingHeight),source,true);
            }
        }
        protected override void OnDestroy()
        {
            if(ownedMaterial){if(Application.isPlaying)Destroy(ownedMaterial);else DestroyImmediate(ownedMaterial);}base.OnDestroy();
        }
    }
}
