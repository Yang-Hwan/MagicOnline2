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
        public Vector2 sourcePixelSize;
        public override Texture mainTexture => texture ? texture : base.mainTexture;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;const int rows=80;
            for(int i=0;i<=rows;i++)
            {
                float t=i/(float)rows;
                if(!texture && !rubber && r.height>0)
                {
                    float corner=Mathf.Min(r.width*.22f,r.height*.42f)/r.height;
                    t=i<16 ? corner*i/16f : i>rows-16 ? 1-corner*(rows-i)/16f : corner+(1-2*corner)*(i-16f)/(rows-32f);
                }
                float width;
                if(rubber) width=r.width*.5f*(1-Mathf.Sin(t*Mathf.PI)*Mathf.Lerp(.06f,.43f,tension));
                else if(domeSilhouette) {
                    // Clip the original backdrop around the domed cap; retain its original pixels.
                    float shape=t>.58f ? Mathf.Sqrt(Mathf.Max(0,1-Mathf.Pow((t-.58f)/.42f,2))) : 1;
                    width=r.width*.5f*shape;
                }
                else if(texture) width=r.width*.5f;
                else {float radius=Mathf.Min(r.width*.22f,r.height*.42f);float y=t*r.height;float dy=y<radius?radius-y:y>r.height-radius?y-(r.height-radius):0;width=r.width*.5f-radius+Mathf.Sqrt(Mathf.Max(0,radius*radius-dy*dy));}
                float v=t;
                // Keep the photographed end caps from stretching with the tall track.
                if(preserveEndCaps && r.height>0)
                {
                    float sourceHeight=sourcePixelSize.y>0?sourcePixelSize.y:uvRect.height*texture.height;
                    float sourceWidth=sourcePixelSize.x>0?sourcePixelSize.x:uvRect.width*texture.width;
                    float bottom=sourceBottomBorder/sourceHeight, top=sourceTopBorder/sourceHeight;
                    float scale=r.width/sourceWidth;
                    float bottomScreen=sourceBottomBorder*scale/r.height, topScreen=sourceTopBorder*scale/r.height;
                    v=t<bottomScreen ? t/bottomScreen*bottom : t>1-topScreen ? 1-(1-t)/topScreen*top : bottom+(t-bottomScreen)/(1-bottomScreen-topScreen)*(1-bottom-top);
                }
                v=uvRect.y+v*uvRect.height;
                vh.AddVert(new Vector2(r.center.x-width,r.yMin+t*r.height),color,new Vector2(domeSilhouette ? uvRect.center.x-uvRect.width*width/r.width : uvRect.xMin,v));
                vh.AddVert(new Vector2(r.center.x+width,r.yMin+t*r.height),color,new Vector2(domeSilhouette ? uvRect.center.x+uvRect.width*width/r.width : uvRect.xMax,v));
                if(i>0){int n=i*2;vh.AddTriangle(n-2,n,n-1);vh.AddTriangle(n-1,n,n+1);}
            }
        }
    }
}
