using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Sight.Surface.Pavilion
{
    // Bounded, deterministic UI geometry: no particles/colliders or shared random state.
    // Vertex-alpha halos blend in the UI itself, independent of camera post-processing.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MatchHudGraphic : MaskableGraphic
    {
        MatchHudView view;
        VertexHelper mesh;
        static readonly Color Gold=new Color32(239,196,104,255),Cyan=new Color32(55,255,229,255),Violet=new Color32(209,104,255,255);
        static readonly Color[] RedMetal={new Color(.23f,.01f,.035f),new Color(1,.55f,.57f),new Color(.76f,.025f,.07f),new Color(.33f,0,.025f),new Color(1,.26f,.32f),new Color(.3f,.02f,.04f)};
        static readonly Color[] SilverMetal={new Color(.11f,.16f,.19f),new Color(.99f,.99f,.93f),new Color(.65f,.73f,.78f),new Color(.1f,.16f,.2f),new Color(.92f,.98f,1),new Color(.16f,.21f,.24f)};
        static readonly Vector2[] SelfRim={new Vector2(58,12),new Vector2(188,12),new Vector2(139,158),new Vector2(12,158)};
        static readonly Vector2[] OtherRim={new Vector2(1418,12),new Vector2(1547,12),new Vector2(1498,158),new Vector2(1369,158)};
        readonly Vector2[] rim=new Vector2[65];
        public void Initialize(MatchHudView owner){view=owner;raycastTarget=false;SetVerticesDirty();}
        static Color Alpha(Color c,float a){c.a=a;return c;}
        static float Frac(float x)=>x-Mathf.Floor(x);
        static float Seed(int i)=>Frac(Mathf.Sin(i*91.71f+13.3f)*43758.54f);
        Vector3 Local(Vector2 p)=>new Vector3(rectTransform.rect.xMin+p.x,rectTransform.rect.yMax-p.y);
        void Quad(Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color ca,Color cb,Color cc,Color cd)
        {
            int n=mesh.currentVertCount;mesh.AddVert(Local(a),ca,Vector2.zero);mesh.AddVert(Local(b),cb,Vector2.zero);mesh.AddVert(Local(c),cc,Vector2.zero);mesh.AddVert(Local(d),cd,Vector2.zero);mesh.AddTriangle(n,n+1,n+2);mesh.AddTriangle(n+2,n+3,n);
        }
        void Triangle(Vector2 a,Vector2 b,Vector2 c,Color ca,Color cb,Color cc)
        {
            int n=mesh.currentVertCount;mesh.AddVert(Local(a),ca,Vector2.zero);mesh.AddVert(Local(b),cb,Vector2.zero);mesh.AddVert(Local(c),cc,Vector2.zero);mesh.AddTriangle(n,n+1,n+2);
        }
        void Solid(float x,float y,float w,float h,Color color)=>Quad(new Vector2(x,y),new Vector2(x+w,y),new Vector2(x+w,y+h),new Vector2(x,y+h),color,color,color,color);
        void Segment(Vector2 a,Vector2 b,float width,Color col)
        {
            var direction=b-a;if(direction.sqrMagnitude<.001f)return;var n=new Vector2(-direction.y,direction.x).normalized*width*.5f;Quad(a-n,b-n,b+n,a+n,col,col,col,col);
        }
        void SoftSegment(Vector2 a,Vector2 b,float radius,Color color)
        {
            var d=b-a;if(d.sqrMagnitude<.001f)return;var n=new Vector2(-d.y,d.x).normalized*radius;var clear=Alpha(color,0);Quad(a-n,b-n,b,a,clear,clear,color,color);Quad(a,b,b+n,a+n,color,color,clear,clear);
        }
        Vector2 RimNormal(Vector2[] points,int i,int count,float radius)
        {
            int last=count-2,index=i==count-1?0:i,previous=index==0?last:index-1,next=index==last?0:index+1;
            var a=(points[index]-points[previous]).normalized;var b=(points[next]-points[index]).normalized;
            var na=new Vector2(-a.y,a.x);var nb=new Vector2(-b.y,b.x);var average=(na+nb).normalized;
            return average*(radius/Mathf.Max(.7f,Mathf.Abs(Vector2.Dot(average,na))));
        }
        void SoftPath(Vector2[] points,int count,float radius,Color col)
        {
            var clear=Alpha(col,0);
            for(int i=1;i<count;i++)
            {
                var a=points[i-1];var b=points[i];var an=RimNormal(points,i-1,count,radius);var bn=RimNormal(points,i,count,radius);
                Quad(a-an,b-bn,b,a,clear,clear,col,col);Quad(a,b,b+bn,a+an,col,col,clear,clear);
            }
        }
        void Aura(Vector2 p,float rx,float ry,Color color)
        {
            var edge=Alpha(color,0);const int N=20;
            for(int i=0;i<N;i++){float a=i*2*Mathf.PI/N,b=(i+1)*2*Mathf.PI/N;Triangle(p,p+new Vector2(Mathf.Cos(a)*rx,Mathf.Sin(a)*ry),p+new Vector2(Mathf.Cos(b)*rx,Mathf.Sin(b)*ry),color,edge,edge);}
        }
        void EllipseLine(Vector2 p,float rx,float ry,Color color,float width)
        {
            for(int i=0;i<20;i++){float a=i*Mathf.PI*.1f,b=(i+1)*Mathf.PI*.1f;Segment(p+new Vector2(Mathf.Cos(a)*rx,Mathf.Sin(a)*ry),p+new Vector2(Mathf.Cos(b)*rx,Mathf.Sin(b)*ry),width,color);}
        }
        static Vector2 Bezier(Vector2 a,Vector2 b,Vector2 c,Vector2 d,float t){float u=1-t;return u*u*u*a+3*u*u*t*b+3*u*t*t*c+t*t*t*d;}
        void TurnRim(int side,float intensity)
        {
            // This follows the painted shell's long top/bottom rails and curved neck.
            int n=0;rim[n++]=new Vector2(67,9);rim[n++]=new Vector2(653,9);
            for(int i=1;i<=12;i++)rim[n++]=Bezier(new Vector2(653,9),new Vector2(696,9),new Vector2(697,65),new Vector2(741,65),i/12f);
            for(int i=1;i<=12;i++)rim[n++]=Bezier(new Vector2(741,65),new Vector2(697,69),new Vector2(697,159),new Vector2(648,159),i/12f);
            rim[n++]=new Vector2(26,159);
            for(int i=1;i<=8;i++)rim[n++]=Bezier(new Vector2(26,159),new Vector2(4,159),new Vector2(7,147),new Vector2(10,137),i/8f);
            rim[n++]=new Vector2(48,22);rim[n++]=new Vector2(67,9);
            if(side==1)
            {
                // The right avatar leans in the same direction as its painted frame.
                // Mirroring the left outline puts its lower corner outside that frame.
                n=0;rim[n++]=new Vector2(907,9);rim[n++]=new Vector2(1530,9);
                for(int i=1;i<=8;i++)rim[n++]=Bezier(new Vector2(1530,9),new Vector2(1552,9),new Vector2(1552,16),new Vector2(1548,29),i/8f);
                rim[n++]=new Vector2(1510,146);
                for(int i=1;i<=8;i++)rim[n++]=Bezier(new Vector2(1510,146),new Vector2(1506,159),new Vector2(1499,159),new Vector2(1483,159),i/8f);
                rim[n++]=new Vector2(912,159);
                for(int i=1;i<=12;i++)rim[n++]=Bezier(new Vector2(912,159),new Vector2(863,159),new Vector2(863,69),new Vector2(819,65),i/12f);
                for(int i=1;i<=12;i++)rim[n++]=Bezier(new Vector2(819,65),new Vector2(863,65),new Vector2(864,9),new Vector2(907,9),i/12f);
            }
            Color tint=side==0?Cyan:Violet;float breath=.82f+.12f*Mathf.Sin(view.VisualTime*1.45f+side*.8f),power=intensity*breath;
            float phase=Frac(view.VisualTime*.14f+side*.29f);
            SoftPath(rim,n,15,Alpha(tint,power*.12f));SoftPath(rim,n,5,Alpha(tint,power*.29f));
            for(int i=1;i<n;i++)
            {
                float nx=((rim[i].x+rim[i-1].x)*.5f)/1560;
                float sweep=Mathf.Exp(-Mathf.Pow((nx-phase*1.45f+.2f)/.08f,2))*Mathf.Pow(Mathf.Sin(phase*Mathf.PI),2);
                Segment(rim[i-1],rim[i],1.25f,Alpha(Color.Lerp(tint,Color.white,.72f),power*(.35f+sweep*.58f)));
            }
            var corner=new Vector2(side==0?82:1468,157);Aura(corner,65,12,Alpha(tint,power*.28f));Aura(corner,32,5,Alpha(Color.Lerp(tint,Color.white,.5f),power*.45f));
            Aura(corner,75,1.5f,Alpha(tint,power*.3f));
            // The portrait perimeter shares the same bloom, rather than orbiting a dot.
            Vector2[] av=side==0?SelfRim:OtherRim;
            for(int i=0;i<4;i++)Segment(av[i],av[(i+1)%4],1.1f,Alpha(Color.Lerp(tint,Color.white,.65f),power*.6f));
        }
        static float CapsuleEdge(float yy,float height,float radius)
        {
            float v=Mathf.Abs(yy-height*.5f)-height*.5f+radius;return v>0?radius-Mathf.Sqrt(Mathf.Max(0,radius*radius-v*v)):0;
        }
        void Capsule(float x,float y,float width,float height,Color top,Color middle,Color bottom)
        {
            for(int i=0;i<20;i++)
            {
                float a=height*i/20,b=height*(i+1)/20,ea=CapsuleEdge(a,height,height*.5f),eb=CapsuleEdge(b,height,height*.5f);
                Color ca=a<height*.5f?Color.Lerp(top,middle,a/height*2):Color.Lerp(middle,bottom,a/height*2-1),cb=b<height*.5f?Color.Lerp(top,middle,b/height*2):Color.Lerp(middle,bottom,b/height*2-1);
                Quad(new Vector2(x+ea,y+a),new Vector2(x+width-ea,y+a),new Vector2(x+width-eb,y+b),new Vector2(x+eb,y+b),ca,ca,cb,cb);
            }
        }
        void Gauge(int side)
        {
            float x=side==0?205:880,y=43,w=474,h=42;
            Capsule(x-1.4f,y-1.4f,w+2.8f,h+2.8f,new Color(.65f,.71f,.63f),new Color(.16f,.23f,.18f),new Color(.38f,.5f,.36f));
            Capsule(x,y,w,h,new Color(.03f,.07f,.045f),new Color(.07f,.15f,.08f),new Color(.01f,.035f,.02f));
            bool active=side==view.ActiveSide;float ratio=active?view.RemainingRatio:0,heat=active&&view.TimerRunning?Mathf.SmoothStep(0,1,Mathf.Clamp01((6-view.RemainingSeconds)/4)):0;
            Color normal=new Color(.35f,.95f,.045f),liquid=Color.Lerp(normal,new Color(1,.09f,.025f),heat),bright=Color.Lerp(new Color(.76f,1,.39f),new Color(1,.65f,.28f),heat),dark=Color.Lerp(new Color(.035f,.22f,.008f),new Color(.34f,.025f,.005f),heat);
            float fill=w*ratio,fx=side==0?x:x+w-fill;
            if(fill>2)
            {
                // Preserve the pill silhouette even when the remaining segment is tiny.
                float radius=h*.5f;
                for(int i=0;i<20;i++)
                {
                    float a=h*i/20,b=h*(i+1)/20,ea=CapsuleEdge(a,h,radius),eb=CapsuleEdge(b,h,radius);
                    float leftA=Mathf.Max(fx,x+ea),rightA=Mathf.Min(fx+fill,x+w-ea),leftB=Mathf.Max(fx,x+eb),rightB=Mathf.Min(fx+fill,x+w-eb);
                    if(leftA>=rightA||leftB>=rightB)continue;
                    Color ca=LiquidColor(a/h,bright,liquid,dark),cb=LiquidColor(b/h,bright,liquid,dark);
                    Quad(new Vector2(leftA,y+a),new Vector2(rightA,y+a),new Vector2(rightB,y+b),new Vector2(leftB,y+b),ca,ca,cb,cb);
                }
                // Bubble positions are continuous in track space as the fill recedes.
                int count=view.TimerRunning?34:0;
                for(int i=0;i<count;i++)
                {
                    float life=Frac(view.VisualTime*(.16f+Seed(i)*.12f)*(1+heat*2.7f)+Seed(i+19));
                    float xx=x+w*Seed(i+42)+Mathf.Sin(view.VisualTime*1.1f+i)*1.1f,yy=y+h+4-life*(h+9),r=.7f+Seed(i+77)*2.4f;
                    float edge=CapsuleEdge(Mathf.Clamp(yy-y,0,h),h,radius);
                    if(xx-r<Mathf.Max(fx,x+edge)+2||xx+r>Mathf.Min(fx+fill,x+w-edge)-2||yy-r<y+2||yy+r>y+h-2)continue;
                    float alpha=Mathf.Sin(life*Mathf.PI)*(.13f+heat*.22f);
                    EllipseLine(new Vector2(xx,yy),r,r,Alpha(new Color(1,1,.78f),alpha),.7f);
                    if(i%3==0)Aura(new Vector2(xx-r*.3f,yy-r*.4f),r*.6f,r*.6f,Alpha(Color.white,alpha*.7f));
                }
                // Slow caustic ribbons; only the urgent state adds faster liquid motion.
                for(int row=0;row<2;row++)for(int j=0;j<34;j++)
                {
                    float ax=fx+fill*j/34,bx=fx+fill*(j+1)/34;
                    float ay=y+13+row*14+Mathf.Sin(j*.22f-view.VisualTime*(.7f+heat*2))*1.3f,by=y+13+row*14+Mathf.Sin((j+1)*.22f-view.VisualTime*(.7f+heat*2))*1.3f;
                    if(ax<x+18||bx>x+w-18)continue;SoftSegment(new Vector2(ax,ay),new Vector2(bx,by),2,Alpha(new Color(1,1,.75f),.045f+heat*.04f));
                }
                float end=side==0?fx+fill:fx;SoftSegment(new Vector2(end,y+5),new Vector2(end,y+h-5),5,Alpha(bright,.22f));
            }
            if(heat>0)
            {
                float pulse=(.18f+.1f*Mathf.Sin(view.VisualTime*7))*heat;
                SoftSegment(new Vector2(x+20,y+h),new Vector2(x+w-20,y+h),10,Alpha(liquid,pulse));
            }
        }
        static Color LiquidColor(float u,Color bright,Color liquid,Color dark)
        {
            if(u<.18f)return Color.Lerp(Color.Lerp(bright,Color.white,.3f),liquid,u/.18f);
            if(u<.47f)return Color.Lerp(bright,liquid,(u-.18f)/.29f);
            return Color.Lerp(liquid,dark,(u-.47f)/.53f);
        }
        public static bool IsRedRing(int index)=>index>=5&&index<=9;
        public static float RingX(int side,int index,int score,int count,float start,float length,float spacing)
        {
            float u=index<count-score?index*spacing:length-(count-1-index)*spacing;return side==0?start+u:start+length-u;
        }
        void Ring(Vector2 p,float width,bool red,float light)
        {
            // Chrome/red body is sampled from the approved art by MatchHudArtGraphic.
            if(light>0)Aura(p,11,22,Alpha(new Color(1,.78f,.32f),light*.18f));
        }
        void ScoreRail(int side)
        {
            var n=view.Counters[side];int count=Mathf.Min(15,n.Target);float x=side==0?214:1055,y=126,length=290,spacing=MatchHudArtGraphic.Spacing;
            Capsule(x-8,y-20,length+16,40,new Color(.015f,.022f,.025f,.9f),new Color(.015f,.02f,.025f,.9f),new Color(.015f,.02f,.025f,.9f));
            Segment(new Vector2(x-3,y+2),new Vector2(x+length+3,y+2),4.5f,new Color(.11f,.12f,.12f));Segment(new Vector2(x-3,y),new Vector2(x+length+3,y),3,new Color(.52f,.55f,.53f));Segment(new Vector2(x-3,y-.9f),new Vector2(x+length+3,y-.9f),.8f,new Color(.9f,.91f,.84f));
            int score=Mathf.Min(count,n.Displayed);float age=view.VisualTime-n.Started,progress=n.Progress(view.VisualTime),ease=progress<.5f?4*progress*progress*progress:1-Mathf.Pow(-2*progress+2,3)/2;
            for(int i=0;i<count;i++)
            {
                float xx=RingX(side,i,score,count,x,length,spacing),light=0;
                if(n.Moving==i)
                {
                    float from=RingX(side,i,Mathf.Min(count,n.FromScore),count,x,length,spacing),to=RingX(side,i,Mathf.Min(count,n.ToScore),count,x,length,spacing);xx=Mathf.Lerp(from,to,ease);light=Mathf.Sin(progress*Mathf.PI)*.9f;
                    if(progress<1)for(int trail=1;trail<=10;trail++)
                    {
                        float p=Mathf.Max(0,progress-trail*.012f),e=p<.5f?4*p*p*p:1-Mathf.Pow(-2*p+2,3)/2;
                        Aura(new Vector2(Mathf.Lerp(from,to,e),y),14,9,Alpha(new Color(1,.74f,.29f),(.075f-trail*.006f)*Mathf.Sin(progress*Mathf.PI)));
                    }
                    if(progress>=1){float fade=Mathf.Clamp01((age-MatchHudView.RingDuration)/.48f);Aura(new Vector2(to,y),32,23,Alpha(new Color(1,.77f,.38f),(1-fade)*.22f));EllipseLine(new Vector2(to,y),4+fade*22,15+fade*10,Alpha(new Color(1,.87f,.58f),(1-fade)*.38f),.8f);}
                }
                Ring(new Vector2(xx,y),8.8f,IsRedRing(i),light);
            }
        }
        void Badge(float x,float y,float w,float h)
        {
            Capsule(x-1,y-1,w+2,h+2,new Color(.93f,.79f,.43f),new Color(.56f,.36f,.12f),new Color(.92f,.69f,.28f));Capsule(x,y,w,h,new Color(.055f,.065f,.065f),new Color(.025f,.04f,.045f),new Color(.035f,.045f,.05f));
        }
        void Coin(float x,float y)
        {
            var outline=new Color(.72f,.43f,.09f);var top=new Color(1,.91f,.55f);var baseColor=new Color(.88f,.58f,.12f);
            for(int layer=2;layer>=0;layer--)
            {
                float yy=y+layer*3;
                Solid(x-9,yy,18,3,baseColor);
                for(int i=0;i<20;i++){float a=i*Mathf.PI*.1f,b=(i+1)*Mathf.PI*.1f;Triangle(new Vector2(x,yy),new Vector2(x+Mathf.Cos(a)*10,yy+Mathf.Sin(a)*4),new Vector2(x+Mathf.Cos(b)*10,yy+Mathf.Sin(b)*4),top,top,baseColor);}
                EllipseLine(new Vector2(x,yy),10,4,outline,.8f);
            }
            EllipseLine(new Vector2(x,y-1),7,2.8f,new Color(1,.97f,.75f),.7f);
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(!view)return;mesh=vh;
            TurnRim(0,view.TurnBlend);TurnRim(1,1-view.TurnBlend);
            Gauge(0);Gauge(1);ScoreRail(0);ScoreRail(1);
            Badge(101,141,65,29);Badge(1370,141,65,29);
            Coin(530,123);Coin(1031,123);
            if(view.IsSeries){Badge(674,15,46,43);Badge(840,15,46,43);}
        }
    }
}
