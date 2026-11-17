using UnityEngine;
using UnityEngine.UI;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{
    public static class StagePresentationArt
    {
        public enum Piece { Intro, Success, Failure, YellowButton, WhiteButton, Star }
        static readonly Sprite[] pieces=new Sprite[6],digits=new Sprite[10];
        static readonly Sprite[] cushionNumbers=new Sprite[9];
        public static Sprite CushionNumber(int number)
        {
            if(number<1 || number>9)throw new System.ArgumentOutOfRangeException(nameof(number));
            int index=number-1;
            if(!cushionNumbers[index])
            {
                var texture=Resources.Load<Texture2D>("StageMissions/UI/CushionNumbers");
                if(!texture)throw new System.InvalidOperationException("Missing cushion number artwork");
                // Bounds follow the ink frame in the approved 1254px atlas; no font substitution.
                float x=new[]{84f,477f,868f}[index%3];
                float y=new[]{79f,472f,864f}[index/3];
                var rect=new Rect(x*texture.width/1254f,(1254-y-284)*texture.height/1254f,
                    302*texture.width/1254f,284*texture.height/1254f);
                cushionNumbers[index]=Sprite.Create(texture,rect,new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
                cushionNumbers[index].name="Cushion guide "+number;
            }
            return cushionNumbers[index];
        }
        // Top-left pixel bounds in the generated atlases, including the original ink outline.
        static readonly Rect[] pieceBounds={new Rect(29,33,702,442),new Rect(774,33,715,443),
            new Rect(31,534,704,452),new Rect(841,554,590,124),new Rect(833,688,604,126),new Rect(1035,826,169,155)};
        static readonly Rect[] digitBounds={new Rect(50,151,232,265),new Rect(376,151,176,267),
            new Rect(644,151,236,266),new Rect(950,152,232,266),new Rect(1256,153,230,264),
            new Rect(47,632,231,276),new Rect(345,630,234,278),new Rect(662,631,218,277),
            new Rect(941,632,241,276),new Rect(1256,631,233,277)};
        static Sprite Create(string resource,Rect bounds,string name)
        {
            var texture=Resources.Load<Texture2D>(resource);
            if(!texture)throw new System.InvalidOperationException("Missing stage artwork: "+resource);
            var rect=new Rect(bounds.x*texture.width/1536f,(1024-bounds.y-bounds.height)*texture.height/1024f,
                bounds.width*texture.width/1536f,bounds.height*texture.height/1024f);
            var sprite=Sprite.Create(texture,rect,new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
            sprite.name=name;return sprite;
        }
        public static Sprite Get(Piece piece)
        {
            int i=(int)piece;
            if(!pieces[i])pieces[i]=Create("StageMissions/UI/PopupArt",pieceBounds[i],piece.ToString());
            return pieces[i];
        }
        public static Sprite Digit(int value)
        {
            if(value<0 || value>9)throw new System.ArgumentOutOfRangeException(nameof(value));
            if(!digits[value])digits[value]=Create("StageMissions/UI/Digits",digitBounds[value],"Stage digit "+value);
            return digits[value];
        }
    }

    [RequireComponent(typeof(Image))]
    public sealed class StageCushionNumber : MonoBehaviour
    {
        public int Number{get;private set;}
        public void SetNumber(int number)
        {
            var image=GetComponent<Image>();
            image.sprite=StagePresentationArt.CushionNumber(number);
            image.preserveAspect=true;image.raycastTarget=false;Number=number;
        }
    }

    public sealed class StageStarStamp : MonoBehaviour
    {
        public const float LandingSeconds=.3f, PauseSeconds=.5f;
        public const float InitialScale=26f;
        Image[] stars;
        StageStarImpact[] impacts;
        int award;
        float elapsed;
        bool playing;
        public float Elapsed=>elapsed;
        public int Award=>award;
        public void Initialize(Image[] images)
        {
            stars=images;
            impacts=new StageStarImpact[stars.Length];
            for(int i=0;i<stars.Length;i++)
            {
                var star=stars[i];
                star.type=Image.Type.Filled;star.fillMethod=Image.FillMethod.Horizontal;
                star.fillOrigin=0;star.raycastTarget=false;
                var burst=new GameObject("Star Impact Fragments",typeof(RectTransform),typeof(StageStarImpact));
                burst.transform.SetParent(star.transform,false);
                ((RectTransform)burst.transform).sizeDelta=new Vector2(400,400);
                impacts[i]=burst.GetComponent<StageStarImpact>();
            }
            Clear();
        }
        public static bool Sample(int index,int halfStars,float time,out float alpha,out float scale,out float offsetY)
        {
            alpha=0;scale=1;offsetY=0;
            if(index<0 || index>2 || halfStars<=index*2)return false;
            float age=time-index*(LandingSeconds+PauseSeconds);
            if(age<0)return false;
            float t=Mathf.Clamp01(age/LandingSeconds);
            float ease=1-Mathf.Pow(1-t,3);
            alpha=Mathf.Lerp(.12f,1,ease);
            scale=t<.8f?Mathf.Lerp(InitialScale,.9f,1-Mathf.Pow(1-t/.8f,3)):Mathf.Lerp(.9f,1,(t-.8f)/.2f);
            offsetY=42*(1-ease);
            return true;
        }
        public void Begin(int halfStars)
        {
            award=Mathf.Clamp(halfStars,0,6);elapsed=0;playing=true;Apply();
        }
        public void Clear()
        {
            playing=false;award=0;elapsed=0;
            if(stars!=null)foreach(var star in stars)star.gameObject.SetActive(false);
            if(impacts!=null)foreach(var impact in impacts)impact.SetAge(-1);
        }
        void Apply()
        {
            int count=(award+1)/2;
            for(int i=0;i<stars.Length;i++)
            {
                bool visible=Sample(i,award,elapsed,out float alpha,out float scale,out float y);
                var star=stars[i];star.gameObject.SetActive(visible);
                impacts[i].SetAge(visible?elapsed-i*(LandingSeconds+PauseSeconds)-LandingSeconds:-1);
                if(!visible)continue;
                star.fillAmount=Mathf.Clamp01((award-i*2)*.5f);
                star.color=new Color(1,1,1,alpha);
                star.rectTransform.anchoredPosition=new Vector2((i-(count-1)*.5f)*88,-35+y);
                star.rectTransform.localScale=Vector3.one*scale;
                // A half-star's visible centre is left of its original full sprite centre.
                impacts[i].rectTransform.anchoredPosition=new Vector2(star.fillAmount<1?-star.rectTransform.rect.width*.25f:0,0);
            }
        }
        void Update()
        {
            if(!playing)return;
            elapsed+=Time.unscaledDeltaTime;Apply();
            if(elapsed>=((award+1)/2-1)*(LandingSeconds+PauseSeconds)+LandingSeconds+StageStarImpact.Duration)playing=false;
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StageStarImpact : MaskableGraphic
    {
        public const float Duration=.45f;
        public const int FragmentCount=28;
        float age=-1;
        public void SetAge(float seconds)
        {
            age=seconds;raycastTarget=false;
            bool visible=seconds>=0 && seconds<Duration;
            if(gameObject.activeSelf!=visible)gameObject.SetActive(visible);
            if(visible)SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if(age<0 || age>=Duration)return;
            float t=age/Duration;
            float travel=1-Mathf.Pow(1-t,2);
            Color tint=Color.Lerp(new Color(1,1,.85f),new Color(1,.55f,.06f),t);
            tint.a=Mathf.Pow(1-t,1.6f);
            for(int i=0;i<FragmentCount;i++)
            {
                float angle=(i*360f/FragmentCount+(i%3-1)*3)*Mathf.Deg2Rad;
                var direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                float distance=Mathf.Lerp(12,95+(i%5)*21,travel);
                var centre=direction*distance+Vector2.down*(26*t*t);
                float rotation=angle+(i%2==0?1:-1)*t*2.5f;
                var axis=new Vector2(Mathf.Cos(rotation),Mathf.Sin(rotation));
                var side=new Vector2(-axis.y,axis.x);
                float length=(i%4==0?32:12+i%3*5)*(1-t*.75f);
                float width=(i%4==0?2:3.5f)*(1-t*.7f);
                int start=mesh.currentVertCount;
                mesh.AddVert(centre+axis*length*.5f,tint,Vector2.zero);
                mesh.AddVert(centre+side*width,tint,Vector2.zero);
                mesh.AddVert(centre-axis*length*.5f,tint,Vector2.zero);
                mesh.AddVert(centre-side*width,tint,Vector2.zero);
                mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start,start+2,start+3);
            }
            // A short-lived impact ring anchors the fragments to the exact landing instant.
            float flash=Mathf.Clamp01(1-age/.16f);
            if(flash<=0)return;
            float radius=Mathf.Lerp(12,70,1-flash),thickness=4*flash;
            var ring=new Color(1,.91f,.42f,flash*.8f);
            for(int i=0;i<40;i++)
            {
                float a=i*Mathf.PI/20,b=(i+1)*Mathf.PI/20;
                var p=new Vector2(Mathf.Cos(a),Mathf.Sin(a));var q=new Vector2(Mathf.Cos(b),Mathf.Sin(b));
                int start=mesh.currentVertCount;
                mesh.AddVert(p*(radius-thickness),ring,Vector2.zero);mesh.AddVert(p*(radius+thickness),ring,Vector2.zero);
                mesh.AddVert(q*(radius+thickness),ring,Vector2.zero);mesh.AddVert(q*(radius-thickness),ring,Vector2.zero);
                mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start,start+2,start+3);
            }
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StageGoldPanel : MaskableGraphic
    {
        bool selected;
        public bool Selected{get=>selected;set{if(selected==value)return;selected=value;SetVerticesDirty();}}
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var r=rectTransform.rect;
            void Rounded(float inset,Color bottom,Color top)
            {
                var bounds=new Rect(r.xMin+inset,r.yMin+inset,r.width-2*inset,r.height-2*inset);
                float radius=Mathf.Min(10,bounds.height*.35f);
                int start=mesh.currentVertCount;mesh.AddVert(bounds.center,Color.Lerp(bottom,top,.5f)*color,Vector2.zero);
                for(int corner=0;corner<4;corner++)
                {
                    var center=new Vector2(corner==0 || corner==3?bounds.xMax-radius:bounds.xMin+radius,corner<2?bounds.yMax-radius:bounds.yMin+radius);
                    for(int j=0;j<=5;j++)
                    {
                        float angle=(corner*90+j*18)*Mathf.Deg2Rad;
                        var point=center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
                        var tint=Color.Lerp(bottom,top,(point.y-bounds.yMin)/bounds.height)*color;
                        mesh.AddVert(point,tint,Vector2.zero);
                    }
                }
                for(int i=0;i<24;i++)mesh.AddTriangle(start,start+1+i,start+1+(i+1)%24);
            }
            Rounded(0,new Color(.24f,.16f,.06f),new Color(.93f,.76f,.38f));
            Rounded(1.2f,new Color(.02f,.022f,.025f,.99f),new Color(.11f,.12f,.13f,.99f));
            if(selected)Rounded(2.1f,new Color(.7f,.43f,.07f),new Color(1,.91f,.52f));
        }
    }
}
