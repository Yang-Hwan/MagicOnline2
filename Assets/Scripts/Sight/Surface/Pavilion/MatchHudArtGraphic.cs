using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Sight.Surface.Pavilion
{
    // VS uses the concept sheet. The shader shades solid beads in local tile coordinates.
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
            var canvas=GetComponentInParent<Canvas>(true);if(canvas)canvas.rootCanvas.additionalShaderChannels|=AdditionalCanvasShaderChannels.TexCoord1;
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
        enum TileKind { Artwork, SilverBead, RedBead }
        void Tile(VertexHelper vh,Rect destination,Rect source,TileKind kind)
        {
            int n=vh.currentVertCount;var rect=rectTransform.rect;
            for(int i=0;i<4;i++)
            {
                float x=i==1||i==2?1:0,y=i>=2?1:0;
                var v=UIVertex.simpleVert;v.position=new Vector3(rect.xMin+destination.x+x*destination.width,rect.yMax-destination.y-y*destination.height);
                v.uv0=new Vector2((source.x+x*source.width)/SheetWidth,1-(source.y+y*source.height)/SheetHeight);
                v.uv1=new Vector4(x,y,(float)kind,0);v.color=Color.white;vh.AddVert(v);
            }
            vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n+2,n+3,n);
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(!view||!artwork)return;
            Tile(vh,new Rect(741.75f,13.225f,76.5f,53.55f),new Rect(1028,89,128,88),TileKind.Artwork);
            for(int side=0;side<2;side++)for(int i=0;i<Mathf.Min(15,view.Counters[side].Target);i++)
            {
                var kind=MatchHudGraphic.IsRedRing(i)?TileKind.RedBead:TileKind.SilverBead;
                Tile(vh,new Rect(Position(view,side,i)-RingWidth*.5f,126-RingHeight*.5f,RingWidth,RingHeight),Rect.zero,kind);
            }
        }
        protected override void OnDestroy()
        {
            if(ownedMaterial){if(Application.isPlaying)Destroy(ownedMaterial);else DestroyImmediate(ownedMaterial);}base.OnDestroy();
        }
    }
}
