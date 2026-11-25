using UnityEngine;
using UnityEngine.UI;
namespace Assets.Scripts.Often
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CueLockerGraphic : MaskableGraphic
    {
        public enum Finish { Panel, Inset, Gold, Muted, Fill, Blue, Header, Background }
        public Finish finish;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;float radius=Mathf.Min(9,r.height*.22f);
            if(r.width<1||r.height<1)return;
            bool gold=finish==Finish.Gold,fill=finish==Finish.Fill,blue=finish==Finish.Blue||finish==Finish.Header;
            bool backdrop=finish==Finish.Background,panel=finish==Finish.Panel;
            Layer(vh,new Rect(r.x+1,r.y-3,r.width,r.height),radius,new Color(0,0,0,.35f),new Color(0,0,0,.35f));
            Layer(vh,r,radius,new Color(.02f,.09f,.19f),blue?new Color(.29f,.78f,.96f):gold?new Color(.9f,.78f,.25f):new Color(.15f,.35f,.54f));
            r=Inset(r,1.4f);
            Color bottom=gold?new Color(.64f,.44f,.055f):fill?new Color(.16f,.57f,.025f):blue?new Color(.09f,.32f,.57f):backdrop?new Color(.04f,.13f,.40f):new Color(.015f,.035f,.10f,.96f);
            Color top=gold?new Color(1,.94f,.26f):fill?new Color(.47f,.99f,.16f):blue?new Color(.28f,.67f,.93f):backdrop?new Color(.06f,.48f,.72f):finish==Finish.Muted?new Color(.20f,.27f,.34f):new Color(.045f,.14f,.27f,.96f);
            Layer(vh,r,radius-1,bottom,top);
            if(gold||fill||blue){
                Layer(vh,new Rect(r.x+2,r.y+r.height*.54f,r.width-4,r.height*.42f),Mathf.Max(1,radius-3),new Color(1,1,1,.02f),new Color(1,1,1,.26f));
                Layer(vh,new Rect(r.x+radius,r.yMax-2,Mathf.Max(0,r.width-radius*2),1),.5f,new Color(1,1,1,.25f),new Color(1,1,1,.4f));
            }
            if(backdrop||panel){
                // Low-contrast halftone and billiard-ball silhouettes keep the cue readable.
                float spacing=Mathf.Max(23,Mathf.Sqrt(r.width*r.height/1400));
                for(float x=r.x+16;x<r.xMax-12;x+=spacing)for(float y=r.y+15;y<r.yMax-12;y+=spacing){
                    float alpha=.018f+.025f*(.5f+.5f*Mathf.Sin(x*.013f+y*.02f));
                    var tint=new Color(.20f,.80f,1,alpha);Dot(vh,new Vector2(x,y),3.5f,tint);
                }
                float size=Mathf.Min(r.width*.18f,r.height*.75f);
                Ring(vh,new Vector2(r.x+r.width*.77f,r.y+r.height*.48f),size,.13f*size,new Color(.25f,.84f,1,backdrop?.13f:.055f));
                Ring(vh,new Vector2(r.x+r.width*.23f,r.y+r.height*.45f),size*.67f,.10f*size,new Color(.25f,.84f,1,backdrop?.11f:.04f));
            }
        }
        void Dot(VertexHelper vh,Vector2 centre,float radius,Color tint)
        {
            int n=vh.currentVertCount;vh.AddVert(centre,tint*color,Vector2.zero);
            for(int i=0;i<8;i++){float angle=i*Mathf.PI/4;vh.AddVert(centre+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius,tint*color,Vector2.zero);}
            for(int i=0;i<8;i++)vh.AddTriangle(n,n+1+i,n+1+(i+1)%8);
        }
        void Ring(VertexHelper vh,Vector2 centre,float radius,float thickness,Color tint)
        {
            for(int i=0;i<64;i++){
                float a=i*Mathf.PI/32,b=(i+1)*Mathf.PI/32;int n=vh.currentVertCount;
                var u=new Vector2(Mathf.Cos(a),Mathf.Sin(a));var v=new Vector2(Mathf.Cos(b),Mathf.Sin(b));
                vh.AddVert(centre+u*radius,tint*color,Vector2.zero);vh.AddVert(centre+v*radius,tint*color,Vector2.zero);
                vh.AddVert(centre+v*(radius-thickness),tint*color,Vector2.zero);vh.AddVert(centre+u*(radius-thickness),tint*color,Vector2.zero);
                vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
            }
        }
        static Rect Inset(Rect r,float p)=>new Rect(r.x+p,r.y+p,Mathf.Max(0,r.width-p*2),Mathf.Max(0,r.height-p*2));
        void Layer(VertexHelper vh,Rect r,float radius,Color bottom,Color top)
        {
            if(r.width<=0||r.height<=0)return;radius=Mathf.Clamp(radius,0,Mathf.Min(r.width,r.height)*.5f);
            int start=vh.currentVertCount;Add(r.center,Color.Lerp(bottom,top,.5f));
            for(int corner=0;corner<4;corner++)for(int step=0;step<=8;step++){
                float a=(corner*90+step*90f/8)*Mathf.Deg2Rad;
                var p=new Vector2((corner==0||corner==3?r.xMax-radius:r.xMin+radius)+Mathf.Cos(a)*radius,(corner<2?r.yMax-radius:r.yMin+radius)+Mathf.Sin(a)*radius);
                Add(p,Color.Lerp(bottom,top,Mathf.InverseLerp(r.yMin,r.yMax,p.y)));
            }
            for(int i=0;i<36;i++)vh.AddTriangle(start,start+1+i,start+1+(i+1)%36);
            void Add(Vector2 p,Color c){c*=color;vh.AddVert(p,c,Vector2.zero);}
        }
    }
}
