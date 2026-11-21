using UnityEngine;
using UnityEngine.UI;
namespace Assets.Scripts.Sight.Surface.Pavilion
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ElasticControlGraphic : MaskableGraphic
    {
        public bool rubber;
        public float tension;
        public Texture texture;
        public Rect uvRect = new Rect(0, 0, 1, 1);
        public bool preserveEndCaps;
        public float sourceBottomBorder=50,sourceTopBorder=40;
        public bool domeSilhouette;
        public bool roundedSilhouette;
        public bool coverSilhouette;
        public Vector2 silhouetteEndRadii;
        [Range(0,.5f)] public float cornerRadius=.22f;
        public Vector2 sourcePixelSize;
        public override Texture mainTexture => texture ? texture : base.mainTexture;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;const int rows=80;
            for(int i=0;i<=rows;i++)
            {
                float t=i/(float)rows;
                float bottom=0,top=0,bottomScreen=0,topScreen=0;
                if(texture && preserveEndCaps && r.height>0)
                {
                    float sourceHeight=sourcePixelSize.y>0?sourcePixelSize.y:uvRect.height*texture.height;
                    float sourceWidth=sourcePixelSize.x>0?sourcePixelSize.x:uvRect.width*texture.width;
                    bottom=sourceBottomBorder/sourceHeight;top=sourceTopBorder/sourceHeight;
                    float scale=r.width/sourceWidth;
                    // Compress end regions together for a nearly closed shaft;
                    // otherwise only the continuous middle texture changes length.
                    float endScale=Mathf.Min(1,r.height/Mathf.Max(.001f,(sourceBottomBorder+sourceTopBorder)*scale));
                    bottomScreen=sourceBottomBorder*scale*endScale/r.height;
                    topScreen=sourceTopBorder*scale*endScale/r.height;
                    t=i<16 ? bottomScreen*i/16f : i>rows-16 ? 1-topScreen*(rows-i)/16f :
                        bottomScreen+(1-bottomScreen-topScreen)*(i-16f)/(rows-32f);
                }
                if((!texture || roundedSilhouette) && !rubber && r.height>0)
                {
                    float corner=(roundedSilhouette ? Mathf.Min(r.width,r.height)*cornerRadius : Mathf.Min(r.width*.22f,r.height*.42f))/r.height;
                    t=i<16 ? corner*i/16f : i>rows-16 ? 1-corner*(rows-i)/16f : corner+(1-2*corner)*(i-16f)/(rows-32f);
                }
                float v=t;
                // Preserve the UV-space end shapes even when the metal middle grows.
                if(texture && preserveEndCaps && r.height>0)
                {
                    v=t<=bottomScreen ? t/Mathf.Max(.000001f,bottomScreen)*bottom :
                        t>=1-topScreen ? 1-(1-t)/Mathf.Max(.000001f,topScreen)*top :
                        bottom+(t-bottomScreen)/Mathf.Max(.000001f,1-bottomScreen-topScreen)*(1-bottom-top);
                }
                float width;
                if(rubber) width=r.width*.5f*(1-Mathf.Sin(t*Mathf.PI)*Mathf.Lerp(.06f,.43f,tension));
                else if(domeSilhouette) {
                    // Clip the original backdrop around the domed cap; retain its original pixels.
                    float shape=t>.58f ? Mathf.Sqrt(Mathf.Max(0,1-Mathf.Pow((t-.58f)/.42f,2))) : 1;
                    width=r.width*.5f*shape;
                }
                else if(coverSilhouette){
                    float lower=silhouetteEndRadii.x/Mathf.Max(1,sourcePixelSize.y);
                    float upper=silhouetteEndRadii.y/Mathf.Max(1,sourcePixelSize.y);
                    float dy=v<lower ? 1-v/Mathf.Max(.000001f,lower) : v>1-upper ? (v-1+upper)/Mathf.Max(.000001f,upper) : 0;
                    width=r.width*.5f*Mathf.Sqrt(Mathf.Max(0,1-dy*dy));
                }
                else if(texture && !roundedSilhouette) width=r.width*.5f;
                else {float radius=roundedSilhouette ? Mathf.Min(r.width,r.height)*cornerRadius : Mathf.Min(r.width*.22f,r.height*.42f);float y=t*r.height;float dy=y<radius?radius-y:y>r.height-radius?y-(r.height-radius):0;width=r.width*.5f-radius+Mathf.Sqrt(Mathf.Max(0,radius*radius-dy*dy));}
                v=uvRect.y+v*uvRect.height;
                // Map clipped vertices to their original image positions; do not
                // stretch the full texture row into the narrower curved ends.
                bool clipped=domeSilhouette || roundedSilhouette || coverSilhouette;
                vh.AddVert(new Vector2(r.center.x-width,r.yMin+t*r.height),color,new Vector2(clipped ? uvRect.center.x-uvRect.width*width/r.width : uvRect.xMin,v));
                vh.AddVert(new Vector2(r.center.x+width,r.yMin+t*r.height),color,new Vector2(clipped ? uvRect.center.x+uvRect.width*width/r.width : uvRect.xMax,v));
                if(i>0){int n=i*2;vh.AddTriangle(n-2,n,n-1);vh.AddTriangle(n-1,n,n+1);}
            }
        }
    }
}
