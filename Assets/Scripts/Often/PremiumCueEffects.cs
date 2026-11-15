using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Often
{
    public enum PremiumCueStyle { None,Thunder,Nebula,Flame }
    public static class PremiumCueDesign
    {
        public static PremiumCueStyle Style(string key)
        {
            switch(key){case "Gameplay/PremiumCues/ThunderGod":return PremiumCueStyle.Thunder;case "Gameplay/PremiumCues/Nebula":return PremiumCueStyle.Nebula;case "Gameplay/PremiumCues/CrimsonFlame":return PremiumCueStyle.Flame;default:return PremiumCueStyle.None;}
        }
        public static Color Tint(PremiumCueStyle style)=>style==PremiumCueStyle.Thunder?new Color(.15f,.7f,1):style==PremiumCueStyle.Nebula?new Color(.7f,.25f,1):new Color(1,.28f,.025f);
    }

    // Canonical positions: X=0 is the butt, X=1 the tip; Y is measured in cue lengths.
    // No UnityEngine.Random calls: cosmetic animation must never alter the shot random stream.
    public sealed class CueEffectGeometry
    {
        public readonly List<Vector3> Vertices=new List<Vector3>(5000);
        public readonly List<Color> Colors=new List<Color>(5000);
        public readonly List<int> Indices=new List<int>(10000);
        static float Frac(float x)=>x-Mathf.Floor(x);
        static float Seed(int i)=>Frac(Mathf.Sin(i*127.1f+31.7f)*43758.5453f);
        void Triangle(Vector2 a,Vector2 b,Vector2 c,Color ca,Color cb,Color cc)
        {
            int n=Vertices.Count;Vertices.Add(a);Vertices.Add(b);Vertices.Add(c);Colors.Add(ca);Colors.Add(cb);Colors.Add(cc);Indices.Add(n);Indices.Add(n+1);Indices.Add(n+2);
        }
        void Strip(Vector2 a,Vector2 b,float width,Color color)
        {
            var d=(b-a).normalized;var n=new Vector2(-d.y,d.x)*width;
            // Layered low-alpha halo around a narrow bright core.
            Quad(a-n*5,b-n*5,b+n*5,a+n*5,new Color(color.r,color.g,color.b,color.a*.035f));
            Quad(a-n*2.7f,b-n*2.7f,b+n*2.7f,a+n*2.7f,new Color(color.r,color.g,color.b,color.a*.13f));
            var core=Color.Lerp(color,Color.white,.45f);core.a=color.a;Quad(a-n*.6f,b-n*.6f,b+n*.6f,a+n*.6f,core);
        }
        void Quad(Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color){Triangle(a,b,c,color,color,color);Triangle(a,c,d,color,color,color);}
        void Dot(Vector2 p,float radius,Color color)
        {
            var edge=new Color(color.r,color.g,color.b,0);
            for(int i=0;i<8;i++){float a=i*Mathf.PI*.25f,b=(i+1)*Mathf.PI*.25f;Triangle(p,p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,p+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,color,edge,edge);}
        }
        public void Build(PremiumCueStyle style,float time,float strength)
        {
            Vertices.Clear();Colors.Clear();Indices.Clear();if(style==PremiumCueStyle.None)return;
            strength=Mathf.Clamp01(strength);var tint=PremiumCueDesign.Tint(style);tint.a=.65f+strength*.35f;
            if(style==PremiumCueStyle.Thunder)
            {
                for(int arc=0;arc<3;arc++)
                {
                    var last=new Vector2(.04f,0);int tick=Mathf.FloorToInt(time*13);
                    for(int j=1;j<=38;j++){float u=j/38f;float wave=(Seed(j+arc*59+tick*137)-.5f)*(.04f+strength*.025f);var next=new Vector2(.04f+u*.91f,wave);Strip(last,next,.0009f+strength*.0006f,tint);last=next;}
                }
                for(int j=0;j<30;j++){float life=Frac(time*(.5f+Seed(j)*.6f)+Seed(j+33));var p=new Vector2(Seed(j+11)*.9f,Mathf.Sin(j*12.1f)*life*.065f);var c=Color.Lerp(tint,Color.white,.65f);c.a=(1-life)*.85f;Dot(p,.003f,c);}
            }
            else if(style==PremiumCueStyle.Nebula)
            {
                for(int ribbon=0;ribbon<2;ribbon++)
                {
                    var color=ribbon==0?tint:new Color(.15f,.8f,1,.8f);Vector2 last=Vector2.zero;
                    for(int j=0;j<=72;j++){float u=j/72f;float phase=u*18-time*2.7f+ribbon*Mathf.PI;var next=new Vector2(.04f+u*.89f,Mathf.Sin(phase)*.048f*(1-u*.35f));if(j>0)Strip(last,next,.0013f+strength*.0007f,color);last=next;}
                }
                for(int j=0;j<42;j++){float phase=time*(.6f+Seed(j)) + j*3.3f;var p=new Vector2(.04f+Seed(j+8)*.9f,Mathf.Sin(phase)*(.018f+Seed(j+71)*.055f));var c=Color.Lerp(tint,Color.white,Seed(j+9));c.a=.3f+.7f*Mathf.Pow(Mathf.Max(0,Mathf.Sin(phase*2)),4);Dot(p,.002f+Seed(j)*.002f,c);}
            }
            else
            {
                for(int flame=0;flame<22;flame++)
                {
                    float x=.025f+Seed(flame+6)*.79f,side=flame%2==0?1:-1;var last=new Vector2(x,0);
                    for(int j=1;j<=10;j++){float u=j/10f;float sway=Mathf.Sin(time*5+flame*2+u*4);var next=new Vector2(x+u*(.075f+Seed(flame)*.05f),side*(u*(1-u*.4f)*(.035f+Seed(flame+31)*.035f)+sway*u*.01f));var c=Color.Lerp(new Color(1,.8f,.13f,1),new Color(1,.05f,0,0),u);Strip(last,next,.0028f*(1-u)+.0003f,c);last=next;}
                }
                for(int j=0;j<40;j++){float life=Frac(time*(.28f+Seed(j)*.45f)+Seed(j+13));var c=new Color(1,.32f+Seed(j)*.5f,.04f,(1-life)*.9f);Dot(new Vector2(.03f+Seed(j+1)*.82f+life*.08f,(j%2==0?1:-1)*life*.09f),.002f+Seed(j)*.0017f,c);}
            }
            // The core pulses independently from the body texture.
            float pulse=1+Mathf.Sin(time*3)*.15f;Dot(new Vector2(.075f,0),(.022f+strength*.01f)*pulse,new Color(tint.r,tint.g,tint.b,.42f));
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PremiumCuePreview:MaskableGraphic
    {
        public PremiumCueStyle Style;
        public float Strength=.7f;
        public float PreviewTime=-1;
        readonly CueEffectGeometry geometry=new CueEffectGeometry();
        static Material glow;
        static int glowUsers;
        bool usesGlow;
        public static PremiumCuePreview Attach(RawImage image,string key)
        {
            var style=PremiumCueDesign.Style(key);if(style==PremiumCueStyle.None || !image)return null;
            var node=new GameObject("AnimatedCueEffects",typeof(RectTransform),typeof(CanvasRenderer));node.transform.SetParent(image.transform,false);
            var rect=(RectTransform)node.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var effect=node.AddComponent<PremiumCuePreview>();effect.Style=style;effect.raycastTarget=false;
            if(!glow){var shader=Resources.Load<Shader>("Gameplay/PremiumCues/PremiumCueUI");if(shader)glow=new Material(shader){hideFlags=HideFlags.HideAndDontSave};}
            if(glow){effect.material=glow;effect.usesGlow=true;glowUsers++;}return effect;
        }
        protected override void OnDestroy()
        {
            base.OnDestroy();if(!usesGlow)return;glowUsers--;
            if(glowUsers<=0 && glow){if(Application.isPlaying)Destroy(glow);else DestroyImmediate(glow);glow=null;glowUsers=0;}
        }
        void Update(){SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;geometry.Build(Style,PreviewTime>=0?PreviewTime:Time.unscaledTime,Strength);
            var vertex=UIVertex.simpleVert;
            for(int i=0;i<geometry.Vertices.Count;i++){var p=geometry.Vertices[i];vertex.position=new Vector3(r.xMin+p.x*r.width,r.center.y+p.y*r.width,0);vertex.color=geometry.Colors[i];vertex.uv0=new Vector2(.5f,.5f);vh.AddVert(vertex);}
            for(int i=0;i<geometry.Indices.Count;i+=3)vh.AddTriangle(geometry.Indices[i],geometry.Indices[i+1],geometry.Indices[i+2]);
        }
    }
}
