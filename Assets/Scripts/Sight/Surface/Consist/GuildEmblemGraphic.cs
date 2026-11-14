using UnityEngine;
using UnityEngine.UI;
namespace Assets.Scripts.Sight.Surface.Consist
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GuildEmblemGraphic:MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;
            Vector2 P(float x,float y)=>new Vector2(r.xMin+x*r.width,r.yMin+y*r.height);
            void Polygon(Vector2[] points,Color color)
            {int start=vh.currentVertCount;foreach(var p in points)vh.AddVert(p,color,Vector2.zero);for(int i=1;i<points.Length-1;i++)vh.AddTriangle(start,start+i,start+i+1);}
            Polygon(new[]{P(.5f,.98f),P(.94f,.8f),P(.89f,.28f),P(.5f,.02f),P(.11f,.28f),P(.06f,.8f)},new Color32(255,206,60,255));
            Polygon(new[]{P(.5f,.92f),P(.87f,.77f),P(.82f,.32f),P(.5f,.1f),P(.18f,.32f),P(.13f,.77f)},new Color32(13,89,172,255));
            void Line(Vector2 a,Vector2 b,float width,Color c){var n=Vector2.Perpendicular(b-a).normalized*width*.5f;Polygon(new[]{a-n,a+n,b+n,b-n},c);}
            Line(P(.14f,.15f),P(.85f,.88f),r.width*.04f,new Color32(246,208,130,255));Line(P(.86f,.15f),P(.15f,.88f),r.width*.04f,new Color32(246,208,130,255));
            void Ball(float x,float y,Color c){var center=P(x,y);int start=vh.currentVertCount;vh.AddVert(center,c,Vector2.zero);for(int i=0;i<=24;i++){float angle=i*Mathf.PI*2/24;vh.AddVert(center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*r.width*.13f,c,Vector2.zero);if(i>0)vh.AddTriangle(start,start+i,start+i+1);}}
            Ball(.5f,.64f,Color.white);Ball(.35f,.4f,new Color32(242,35,50,255));Ball(.65f,.4f,new Color32(255,211,23,255));
        }
    }
}
