using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Often
{
    // A transient visual only. Growth is committed before this is played.
    public sealed class CueLevelUpMotion : MonoBehaviour
    {
        public const float Duration=3.2f;
        CanvasGroup group;
        CueLevelUpGraphic light;
        Material lightMaterial;
        GameObject[] hidden;
        bool[] previouslyActive;
        float started;
        public bool Finished {get;private set;}
        public static bool ShouldShow(long before,long after,int[] oldLevels,int[] newLevels)
        {
            if(after<=before||oldLevels==null||newLevels==null||oldLevels.Length!=newLevels.Length)return false;
            for(int i=0;i<oldLevels.Length;i++)if(newLevels[i]>oldLevels[i])return true;return false;
        }
        public static CueLevelUpMotion Play(RectTransform parent)
        {
            var go=new GameObject("Cue level-up celebration",typeof(RectTransform),typeof(CanvasGroup),typeof(CueLevelUpMotion));
            go.layer=parent.gameObject.layer;go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var motion=go.GetComponent<CueLevelUpMotion>();motion.Build();motion.started=Time.unscaledTime;motion.Sample(0);return motion;
        }
        RectTransform Node(string name,float width,float height,float y)
        {
            var go=new GameObject(name,typeof(RectTransform));go.layer=gameObject.layer;go.transform.SetParent(transform,false);
            var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.one*.5f;rect.sizeDelta=new Vector2(width,height);rect.anchoredPosition=new Vector2(0,y);return rect;
        }
        readonly System.Collections.Generic.List<Sprite> sprites=new System.Collections.Generic.List<Sprite>();
        CanvasGroup artwork;
        Image dim;
        Image Picture(string name,string resource,float width,float height,float y)
        {
            var texture=Resources.Load<Texture2D>("Gameplay/CueLevelUp/"+resource);
            var image=Node(name,width,height,y).gameObject.AddComponent<Image>();image.raycastTarget=false;
            var sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),Vector2.one*.5f,100);sprites.Add(sprite);image.sprite=sprite;return image;
        }
        void Build()
        {
            group=GetComponent<CanvasGroup>();group.interactable=false;group.blocksRaycasts=false;
            var card=transform.parent.Find("CueDetailCard");
            if(card){string[] names={"GrowAll","GrowAllFace","GrowAllLabel","CueSkin"};hidden=new GameObject[names.Length];previouslyActive=new bool[names.Length];for(int i=0;i<names.Length;i++){var node=card.Find(names[i])??card.Find("GrowthPanel/"+names[i]);if(!node)continue;hidden[i]=node.gameObject;previouslyActive[i]=hidden[i].activeSelf;hidden[i].SetActive(false);}}
            light=Node("Progress electricity",570,800,0).gameObject.AddComponent<CueLevelUpGraphic>();light.raycastTarget=false;
            var track=card?card.Find("GrowthPanel/CueRoundTrack") as RectTransform:null;
            if(track){light.rectTransform.position=track.TransformPoint(track.rect.center);}
            var shader=Resources.Load<Shader>("Gameplay/CueLevelUp/Light");if(shader){lightMaterial=new Material(shader);light.material=lightMaterial;}
            var art=Node("Level-up artwork",1300,840,0);artwork=art.gameObject.AddComponent<CanvasGroup>();artwork.blocksRaycasts=false;artwork.interactable=false;
            dim=Node("Celebration background dim",1,1,0).gameObject.AddComponent<Image>();dim.raycastTarget=false;dim.color=new Color(0,.01f,.025f,.57f);
            dim.rectTransform.anchorMin=Vector2.zero;dim.rectTransform.anchorMax=Vector2.one;dim.rectTransform.offsetMin=dim.rectTransform.offsetMax=Vector2.zero;
            dim.transform.SetSiblingIndex(0);
            var overlay=Picture("CUE LEVEL UP laurels and radiance","Celebration",1450,1450f*819/1920,0);
            overlay.rectTransform.SetParent(art,false);
        }

        void Update(){if(Application.isPlaying&&!Finished)Sample(Time.unscaledTime-started);}
        public void Sample(float elapsed)
        {
            elapsed=Mathf.Max(0,elapsed);Finished=elapsed>=Duration;group.alpha=Finished?0:1;
            float t=elapsed-.5f;artwork.alpha=Mathf.Clamp01(t/.18f)*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.55f,Duration,elapsed)));
            dim.color=new Color(0,.01f,.025f,.57f*artwork.alpha);
            float pop=Mathf.Clamp01(t/.4f);artwork.transform.localScale=Vector3.one*(1+Mathf.Sin(pop*Mathf.PI)*.045f);
            light.color=Color.white;light.phase=elapsed;light.SetVerticesDirty();
        }
        void OnDisable(){Finished=true;}
        void OnDestroy(){if(transform.parent && transform.parent.gameObject.activeInHierarchy && hidden!=null)for(int i=0;i<hidden.Length;i++)if(hidden[i])hidden[i].SetActive(previouslyActive[i]);foreach(var sprite in sprites)Release(sprite);Release(lightMaterial);}
        static void Release(UnityEngine.Object value){if(!value)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CueLevelUpGraphic : MaskableGraphic
    {
        public float phase;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            void Segment(Vector2 a,Vector2 b,float width,Color tint){var normal=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;int n=vh.currentVertCount;vh.AddVert(a-normal,tint*color,Vector2.zero);vh.AddVert(a+normal,tint*color,Vector2.zero);vh.AddVert(b+normal,tint*color,Vector2.zero);vh.AddVert(b-normal,tint*color,Vector2.zero);vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);}
            float pulse=FlashIntensity(phase);if(pulse<=0)return;
            // Two discrete discharges from the center of the completed progress bar.
            // The branching shape is stable within each flash, rather than wriggling across the bar.
            int flash=phase<.25f?0:1;
            var gold=new Color(1,.66f,.12f,pulse);
            for(int arc=0;arc<5;arc++){
                float endX=(arc-2)*48+(flash==0?8:-8);
                float length=215+(arc%3)*43;
                Vector2 previous=Vector2.zero;
                for(int i=1;i<=9;i++){
                    float f=i/9f;
                    float x=endX*f+Mathf.Sin(i*2.7f+arc*4.3f+flash)*13*f;
                    var next=new Vector2(x,-length*f);
                    float taper=1-f*.85f;
                    Segment(previous,next,8*taper,new Color(1,.55f,.02f,pulse*.10f*taper));
                    Segment(previous,next,2*taper,new Color(gold.r,gold.g,gold.b,pulse*.65f*taper));
                    Segment(previous,next,.7f*taper,new Color(1,.94f,.6f,pulse*taper));
                    if(i==4&&(arc==0||arc==3)){
                        var branch=next+new Vector2(arc==0?-28:32,-55);
                        Segment(next,branch,1,new Color(1,.72f,.2f,pulse*.35f));
                    }
                    previous=next;
                }
            }
            // Small soft flare with a fine vertical and horizontal glint at the origin.
            for(int i=0;i<32;i++){
                float a=i*Mathf.PI/16,b=(i+1)*Mathf.PI/16;int n=vh.currentVertCount;
                vh.AddVert(Vector2.zero,new Color(1,.85f,.3f,pulse*.65f)*color,Vector2.zero);
                vh.AddVert(new Vector2(Mathf.Cos(a)*29,Mathf.Sin(a)*23),new Color(1,.6f,.08f,0),Vector2.zero);
                vh.AddVert(new Vector2(Mathf.Cos(b)*29,Mathf.Sin(b)*23),new Color(1,.6f,.08f,0),Vector2.zero);vh.AddTriangle(n,n+1,n+2);
            }
            Segment(new Vector2(-24,0),new Vector2(24,0),1.2f,new Color(1,.96f,.65f,pulse));
            Segment(new Vector2(0,-19),new Vector2(0,19),1.5f,new Color(1,1,.85f,pulse));
            Segment(new Vector2(0,-6),new Vector2(0,6),4,new Color(1,1,.9f,pulse));
        }
        public static float FlashIntensity(float elapsed)
        {
            float t=elapsed<.25f?elapsed:elapsed-.25f;
            float duration=elapsed<.25f?.18f:.20f;
            if(elapsed<0||elapsed>=.45f||t>=duration)return 0;
            return Mathf.Clamp01(t/.018f)*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.045f,duration,t)));
        }
    }
}
