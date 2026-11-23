using Assets.Scripts.Often;
using Assets.Scripts.Sight.Vital.Pavilion;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Sight.Surface.Pavilion
{
    // Uses the approved artwork unchanged. No colliders, input handlers or score mutations.
    public sealed class ThreeCushionTechniqueMotion : MonoBehaviour
    {
        public const float Duration=2.1f;
        static ThreeCushionTechniqueMotion current;
        RectTransform rect,canvasRect;
        CanvasGroup opacity;
        RawImage brush,title,grandTitle;
        TechniqueSparkGraphic sparks;
        Material brushMaterial,titleMaterial,grandTitleMaterial;
        Behaviour owner;
        Transform cloth;
        float elapsed=Duration;
        float titleWidth;
        bool IsCompound => Technique==ThreeCushionTechnique.FrontGrand || Technique==ThreeCushionTechnique.OneBankReturn || Technique==ThreeCushionTechnique.ThreeBankReturn;
        public ThreeCushionTechnique Technique {get;private set;}
        public float Elapsed=>elapsed;
        public RawImage Title=>title;
        public RawImage Brush=>brush;

        public static ThreeCushionTechniqueMotion Install(RectTransform canvas,Behaviour panel,Transform clothSpace=null)
        {
            var go=new GameObject("Three Cushion Technique",typeof(RectTransform),typeof(CanvasGroup),typeof(ThreeCushionTechniqueMotion));
            go.layer=canvas.gameObject.layer;go.transform.SetParent(canvas,false);
            var v=go.GetComponent<ThreeCushionTechniqueMotion>();
            v.rect=go.GetComponent<RectTransform>();v.rect.anchorMin=v.rect.anchorMax=Vector2.one*.5f;
            v.canvasRect=canvas;v.owner=panel;
            var playArea=GameObject.Find("Table/ClothSpace");
            v.cloth=playArea?playArea.transform:clothSpace;
            v.opacity=go.GetComponent<CanvasGroup>();v.opacity.alpha=0;v.opacity.blocksRaycasts=false;v.opacity.interactable=false;
            v.brush=v.Art("Oxblood brush",Resources.Load<Texture2D>("Gameplay/Techniques/Brush"));
            v.title=v.Art("Gold brush title",Resources.Load<Texture2D>("Gameplay/Techniques/Back"));
            v.grandTitle=v.Art("Grand tour suffix",Resources.Load<Texture2D>("Gameplay/Techniques/Titles"));
            v.grandTitleMaterial=v.grandTitle.material;v.grandTitle.gameObject.SetActive(false);
            var fx=new GameObject("Impact embers",typeof(RectTransform),typeof(CanvasRenderer),typeof(TechniqueSparkGraphic));fx.layer=go.layer;fx.transform.SetParent(go.transform,false);
            v.sparks=fx.GetComponent<TechniqueSparkGraphic>();v.sparks.raycastTarget=false;
            v.brushMaterial=v.brush.material;v.titleMaterial=v.title.material;
            current=v;return v;
        }
        RawImage Art(string name,Texture texture)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(RawImage));go.layer=gameObject.layer;go.transform.SetParent(transform,false);
            var image=go.GetComponent<RawImage>();image.texture=texture;image.raycastTarget=false;
            var shader=Resources.Load<Shader>("Gameplay/Techniques/TechniqueArtwork");
            image.material=new Material(shader?shader:Shader.Find("UI/Default"));return image;
        }
        public static void Show(ThreeCushionTechnique kind)
        {
            if(MatchResultPresentation.EffectsBlocked || !current || !current.owner || !current.owner.isActiveAndEnabled || kind==ThreeCushionTechnique.None)return;
            current.Play(kind);
        }
        public bool Play(ThreeCushionTechnique kind)
        {
            if(MatchResultPresentation.EffectsBlocked)return false;
            if(kind<ThreeCushionTechnique.Side || kind>ThreeCushionTechnique.ThreeBankReturn)return false;
            Technique=kind;
            bool compound=IsCompound;
            grandTitle.gameObject.SetActive(compound);
            if(compound)
            {
                // Compose existing gold lettering for each compound technique.
                bool bankReturn=kind==ThreeCushionTechnique.OneBankReturn || kind==ThreeCushionTechnique.ThreeBankReturn;
                kind=kind==ThreeCushionTechnique.ThreeBankReturn?ThreeCushionTechnique.ThreeBank:
                    bankReturn?ThreeCushionTechnique.OneBank:ThreeCushionTechnique.Front;
                var suffixUV=bankReturn?new Rect(0,1-1540f/1774,1,220f/1774):new Rect(0,1-909f/1774,1,217f/1774);
                grandTitle.uvRect=suffixUV;
                grandTitleMaterial.SetVector("_UVBounds",new Vector4(suffixUV.xMin,suffixUV.yMin,suffixUV.xMax,suffixUV.yMax));
            }
            Rect uv=new Rect(0,0,1,1);
            if(kind==ThreeCushionTechnique.Back)title.texture=Resources.Load<Texture2D>("Gameplay/Techniques/Back");
            else if(kind==ThreeCushionTechnique.HorizontalHit)title.texture=Resources.Load<Texture2D>("Gameplay/Techniques/HorizontalHit");
            else if(kind==ThreeCushionTechnique.OneBank)title.texture=Resources.Load<Texture2D>("Gameplay/Techniques/OneBank");
            else
            {
                bool extra=kind>=ThreeCushionTechnique.Cut;
                title.texture=Resources.Load<Texture2D>(extra?"Gameplay/Techniques/ExtraTitles":"Gameplay/Techniques/Titles");
                int row=extra?(int)kind-(int)ThreeCushionTechnique.Cut:
                    kind==ThreeCushionTechnique.Side?0:(int)kind-2;
                // Actual authored row boundaries (top origin), including transparent guard bands.
                int[] edges=extra?new[]{24,294,550,806,1060,1300,1536}:new[]{16,248,480,692,909,1122,1320,1540,1774};
                float h=extra?1536:1774;
                uv=new Rect(0,1-edges[row+1]/h,1,(edges[row+1]-edges[row])/h);
            }
            if(!title.texture || !brush.texture)return false;
            title.uvRect=uv;titleMaterial.SetVector("_UVBounds",new Vector4(uv.xMin,uv.yMin,uv.xMax,uv.yMax));
            Layout();elapsed=0;AdvanceVisual(0);return true;
        }
        void Layout()
        {
            var bounds=canvasRect.rect;
            Vector2 center=new Vector2(0,bounds.height*.14f);float width=bounds.width*.77f;
            var camera=InputOutput.usedCamera?InputOutput.usedCamera:Camera.main;
            if(!cloth)
            {
                var bottom=GameObject.Find("Table/ClothSpace");
                if(!bottom)bottom=GameObject.Find("Table/Collider/Bottom");
                if(bottom)cloth=bottom.transform;
            }
            if(camera && cloth)
            {
                var collider=cloth.GetComponent<Collider>();var b=collider?collider.bounds:new Bounds(cloth.position,cloth.lossyScale);
                Vector2 lo=new Vector2(float.MaxValue,float.MaxValue),hi=new Vector2(float.MinValue,float.MinValue);
                bool visible=true;
                for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
                {
                    var screen=camera.WorldToScreenPoint(new Vector3(b.center.x+x*b.extents.x,b.max.y,b.center.z+z*b.extents.z));
                    if(screen.z<=0){visible=false;continue;}
                    var rootCanvas=canvasRect.GetComponent<Canvas>();
                    var uiCamera=rootCanvas && rootCanvas.renderMode!=RenderMode.ScreenSpaceOverlay?rootCanvas.worldCamera:null;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,screen,uiCamera,out var p);lo=Vector2.Min(lo,p);hi=Vector2.Max(hi,p);
                }
                if(visible && hi.x>lo.x){center=new Vector2((lo.x+hi.x)*.5f,Mathf.Lerp(lo.y,hi.y,.75f));width=hi.x-lo.x;}
            }
            rect.anchoredPosition=center;
            // Approved preview: lettering reduced to 80%, with the brush backing at the original size.
            bool compound=IsCompound;
            titleWidth=width*(compound?.75f:.58f);
            float brushWidth=width*.855f;
            brush.rectTransform.sizeDelta=new Vector2(brushWidth,brushWidth*brush.texture.height/brush.texture.width);
            float wordWidth=compound?titleWidth*.5f:titleWidth;
            title.rectTransform.sizeDelta=new Vector2(wordWidth,wordWidth*title.texture.height*title.uvRect.height/title.texture.width);
            if(compound)grandTitle.rectTransform.sizeDelta=new Vector2(wordWidth,wordWidth*grandTitle.texture.height*grandTitle.uvRect.height/grandTitle.texture.width);
            sparks.rectTransform.sizeDelta=new Vector2(brushWidth,brush.rectTransform.sizeDelta.y);
        }
        static float EaseOut(float t)=>1-Mathf.Pow(1-Mathf.Clamp01(t),3);
        public void AdvanceVisual(float seconds)
        {
            elapsed=Mathf.Clamp(seconds,0,Duration);opacity.alpha=elapsed>=Duration?0:1;
            float reveal=EaseOut(elapsed/.12f),exit=Mathf.Clamp01((elapsed-1.8f)/.3f);
            brushMaterial.SetFloat("_Reveal",reveal);brushMaterial.SetFloat("_Erase",exit);
            brush.color=new Color(1,1,1,Mathf.Lerp(1,.88f,Mathf.Clamp01(elapsed/.3f))*(1-exit));
            brush.rectTransform.anchoredPosition=new Vector2(Mathf.Lerp(-titleWidth*.10f,0,reveal)+exit*titleWidth*.07f,5);
            float enter=EaseOut((elapsed-.08f)/.13f);
            float scale=elapsed<.21f?Mathf.Lerp(1.65f,.96f,enter):Mathf.Lerp(.96f,1,Mathf.Clamp01((elapsed-.21f)/.09f));
            scale=Mathf.Lerp(scale,1.65f,exit*exit);
            title.rectTransform.localScale=Vector3.one*scale;
            title.color=new Color(1,1,1,enter*(1-exit));
            titleMaterial.SetFloat("_Blur",8*(1-enter)+8*exit);
            bool compound=IsCompound;
            // Atlas rows contain transparent side padding; bring the visible words closer.
            float compoundOffset=titleWidth*.19f*scale;
            title.rectTransform.anchoredPosition=compound?new Vector2(-compoundOffset,0):Vector2.zero;
            if(compound)
            {
                grandTitle.rectTransform.anchoredPosition=new Vector2(compoundOffset,0);
                grandTitle.rectTransform.localScale=title.rectTransform.localScale;
                grandTitle.color=title.color;
                grandTitleMaterial.SetFloat("_Blur",8*(1-enter)+8*exit);
            }
            sparks.SetTime(elapsed);
        }
        void Update()
        {
            if(!owner || !owner.isActiveAndEnabled){HideLocal();return;}
            if(elapsed>=Duration)return;
            Layout();AdvanceVisual(elapsed+Time.unscaledDeltaTime);
        }
        public static float ResultHold => current && current.owner && current.owner.isActiveAndEnabled && current.elapsed<Duration ? Mathf.Max(.001f,1f-current.elapsed) : 0;
        public static bool BeginResultFade(){if(!current||current.elapsed>=Duration)return false;current.AdvanceVisual(Mathf.Max(current.elapsed,1.8f));return true;}
        public static void Hide(){if(current)current.HideLocal();}
        void HideLocal(){elapsed=Duration;if(opacity)opacity.alpha=0;}
        void OnDisable()=>HideLocal();
        void OnDestroy(){if(current==this)current=null;if(brushMaterial)Destroy(brushMaterial);if(titleMaterial)Destroy(titleMaterial);if(grandTitleMaterial)Destroy(grandTitleMaterial);}
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TechniqueSparkGraphic:MaskableGraphic
    {
        float time=ThreeCushionTechniqueMotion.Duration;
        public float ReplayTime=>time;
        public void SetTime(float t){time=t;SetVerticesDirty();}
        static float Seed(int i)=>Mathf.Repeat(Mathf.Sin(i*78.13f+8.7f)*43758.54f,1);
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(time>=2.1f)return;
            float w=rectTransform.rect.width,h=rectTransform.rect.height,fade=1-Mathf.Clamp01((time-1.8f)/.3f);
            float hit=time>.13f && time<.3f?Mathf.Sin((time-.13f)/.17f*Mathf.PI):0;
            for(int i=0;i<4;i++)
            {
                float side=i%2==0?-1:1,x=side*w*(.1f+Mathf.Clamp01((time-.13f)/.17f)*.32f),y=(i-1.5f)*h*.08f;
                Streak(vh,new Vector2(x-side*w*.08f,y-h*.04f),new Vector2(x+side*w*.05f,y+h*.05f),h*.009f,new Color(1,.84f,.4f,hit*.9f));
            }
            for(int i=0;i<20;i++)
            {
                float age=time-.15f-i*.009f;if(age<0)continue;
                float alpha=Mathf.Clamp01(age/.08f)*(1-Mathf.Clamp01(age/2))*fade*.7f;
                Vector2 p=new Vector2((Seed(i)-.5f)*w*.85f+Mathf.Sin(age*2+i)*h*.025f,(Seed(i+31)-.5f)*h*.7f+age*h*(.07f+Seed(i+42)*.08f));
                var col=i%4==0?new Color(1,.4f,.13f,alpha):new Color(1,.85f,.42f,alpha);
                float r=h*(.005f+Seed(i+8)*.008f);
                Streak(vh,p-new Vector2(r,r),p+new Vector2(r,r),r,col);
            }
        }
        static void Streak(VertexHelper vh,Vector2 a,Vector2 b,float radius,Color color)
        {
            if(color.a<=0)return;
            Vector2 n=new Vector2(-(b-a).y,(b-a).x).normalized*radius;
            int start=vh.currentVertCount;var clear=new Color(color.r,color.g,color.b,0);
            vh.AddVert(a+n,clear,Vector2.zero);vh.AddVert(a-n,clear,Vector2.zero);
            vh.AddVert((a+b)*.5f+n,color,Vector2.zero);vh.AddVert((a+b)*.5f-n,color,Vector2.zero);
            vh.AddVert(b+n,clear,Vector2.zero);vh.AddVert(b-n,clear,Vector2.zero);
            vh.AddTriangle(start,start+2,start+1);vh.AddTriangle(start+1,start+2,start+3);
            vh.AddTriangle(start+2,start+4,start+3);vh.AddTriangle(start+3,start+4,start+5);
        }
    }
}
