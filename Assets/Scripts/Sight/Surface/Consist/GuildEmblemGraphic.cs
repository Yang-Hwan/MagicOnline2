using UnityEngine;
using UnityEngine.UI;
namespace Assets.Scripts.Sight.Surface.Consist
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GuildEmblemGraphic:MaskableGraphic
    {
        static readonly Color32[] Fields = {
            new Color32(13,89,172,255), new Color32(116,30,66,255), new Color32(26,48,130,255),
            new Color32(51,37,96,255), new Color32(13,94,88,255), new Color32(132,51,23,255)
        };
        public void SetDesign(int id)
        {
            design = Mathf.Clamp(id, 0, Fields.Length - 1);
            raycastTarget = false;
            SetVerticesDirty();
        }
        int design;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;
            Vector2 P(float x,float y)=>new Vector2(r.xMin+x*r.width,r.yMin+y*r.height);
            void Polygon(Vector2[] points,Color color)
            {int start=vh.currentVertCount;foreach(var p in points)vh.AddVert(p,color,Vector2.zero);for(int i=1;i<points.Length-1;i++)vh.AddTriangle(start,start+i,start+i+1);}
            Polygon(new[]{P(.5f,.98f),P(.94f,.8f),P(.89f,.28f),P(.5f,.02f),P(.11f,.28f),P(.06f,.8f)},new Color32(255,206,60,255));
            Polygon(new[]{P(.5f,.92f),P(.87f,.77f),P(.82f,.32f),P(.5f,.1f),P(.18f,.32f),P(.13f,.77f)},Fields[design]);
            void Line(Vector2 a,Vector2 b,float width,Color c){var n=Vector2.Perpendicular(b-a).normalized*width*.5f;Polygon(new[]{a-n,a+n,b+n,b-n},c);}
            Color gold = new Color32(255,218,104,255);
            if (design > 0)
            {
                switch (design)
                {
                    case 1: // Five-point star
                        for (int i = 0; i < 10; i++)
                        {
                            Vector2 Tip(int n)
                            {
                                float angle = Mathf.PI * (.5f + n / 5f);
                                float radius = n % 2 == 0 ? .31f : .135f;
                                return P(.5f + Mathf.Cos(angle)*radius, .53f + Mathf.Sin(angle)*radius);
                            }
                            Polygon(new[]{P(.5f,.53f),Tip(i),Tip(i+1)},gold);
                        }
                        break;
                    case 2:
                        Polygon(new[]{P(.61f,.84f),P(.25f,.49f),P(.52f,.49f)},Color.white);
                        Polygon(new[]{P(.52f,.57f),P(.76f,.57f),P(.39f,.2f)},Color.white);
                        break;
                    case 3:
                        Polygon(new[]{P(.24f,.65f),P(.48f,.43f),P(.31f,.38f)},gold);
                        Polygon(new[]{P(.31f,.38f),P(.5f,.73f),P(.69f,.38f)},gold);
                        Polygon(new[]{P(.76f,.65f),P(.69f,.38f),P(.52f,.43f)},gold);
                        Line(P(.32f,.32f),P(.68f,.32f),r.width*.045f,gold);
                        break;
                    case 4:
                        Polygon(new[]{P(.5f,.81f),P(.76f,.54f),P(.5f,.23f)},new Color32(56,224,211,255));
                        Polygon(new[]{P(.5f,.81f),P(.5f,.23f),P(.24f,.54f)},Color.white);
                        break;
                    case 5:
                        foreach (float sign in new[]{-1f,1f})
                            for(int i=0;i<3;i++)
                                Polygon(new[]{P(.5f,.42f-i*.045f),P(.5f+sign*(.32f-i*.055f),.78f-i*.105f),P(.5f+sign*.12f,.42f-i*.055f)},gold);
                        break;
                }
                return;
            }
            Line(P(.14f,.15f),P(.85f,.88f),r.width*.04f,new Color32(246,208,130,255));Line(P(.86f,.15f),P(.15f,.88f),r.width*.04f,new Color32(246,208,130,255));
            void Ball(float x,float y,Color c){var center=P(x,y);int start=vh.currentVertCount;vh.AddVert(center,c,Vector2.zero);for(int i=0;i<=24;i++){float angle=i*Mathf.PI*2/24;vh.AddVert(center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*r.width*.13f,c,Vector2.zero);if(i>0)vh.AddTriangle(start,start+i,start+i+1);}}
            Ball(.5f,.64f,Color.white);Ball(.35f,.4f,new Color32(242,35,50,255));Ball(.65f,.4f,new Color32(255,211,23,255));
        }
    }
}
