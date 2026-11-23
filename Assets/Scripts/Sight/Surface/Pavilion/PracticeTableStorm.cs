using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Assets.Scripts.Sight.Surface.Pavilion
{
    // An overlay for local practice only; no material, lighting or physics state is changed.
    public sealed class PracticeTableStorm : MonoBehaviour
    {
        public const float Duration=4.8f;
        static readonly float[] StrikeTimes={1.35f,1.95f,2.65f};
        readonly Sprite[] art=new Sprite[4];
        readonly Image[] clouds=new Image[5],rain=new Image[96];
        readonly SpriteRenderer[] splashes=new SpriteRenderer[24],bolts=new SpriteRenderer[3],impacts=new SpriteRenderer[3];
        readonly Vector2[] drops=new Vector2[96];
        Transform cloth;
        GameObject root;
        Sprite white;
        Image veil;
        Canvas weatherCanvas;
        RectTransform weatherRect;
        public Canvas WeatherCanvas=>weatherCanvas;
        float width,depth,started;
        bool playing;
        public bool Finished=>!playing;
        public GameObject VisualRoot=>root;
        public void Configure(Transform value){cloth=value;}
        SpriteRenderer Layer(string name,Sprite sprite,int order)
        {
            var go=new GameObject(name,typeof(SpriteRenderer));go.layer=cloth.gameObject.layer;go.transform.SetParent(root.transform,false);
            var r=go.GetComponent<SpriteRenderer>();r.sprite=sprite;r.sortingOrder=20200+order;r.maskInteraction=SpriteMaskInteraction.VisibleInsideMask;r.enabled=false;return r;
        }
        void Create()
        {
            if(root)return;
            var texture=Resources.Load<Texture2D>("Gameplay/Sentiments/Storm/StormAtlas");
            if(!texture)throw new System.InvalidOperationException("s5_8 storm artwork is missing");
            for(int i=0;i<4;i++)art[i]=Sprite.Create(texture,new Rect(i%2*(texture.width/2),(1-i/2)*(texture.height/2),texture.width/2,texture.height/2),Vector2.one*.5f,texture.width/2f,0,SpriteMeshType.FullRect);
            white=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),Vector2.one*.5f,1);
            root=new GameObject("Practice s5_8 thunderstorm");SceneManager.MoveGameObjectToScene(root,gameObject.scene);
            var screen=new GameObject("Fullscreen weather above HUD",typeof(RectTransform),typeof(Canvas));screen.layer=cloth.gameObject.layer;screen.transform.SetParent(root.transform,false);
            weatherCanvas=screen.GetComponent<Canvas>();weatherCanvas.renderMode=RenderMode.ScreenSpaceOverlay;weatherCanvas.overrideSorting=true;weatherCanvas.sortingOrder=32767;
            weatherRect=(RectTransform)screen.transform;
            veil=ScreenLayer("Storm light",white);
            for(int i=0;i<clouds.Length;i++)clouds[i]=ScreenLayer("Storm cloud "+i,art[i%2]);
            for(int i=0;i<rain.Length;i++)rain[i]=ScreenLayer("Rain streak "+i,white);
            for(int i=0;i<splashes.Length;i++)splashes[i]=Layer("Rain splash "+i,art[3],14);
            for(int i=0;i<bolts.Length;i++){bolts[i]=Layer("Lightning "+i,art[2],25);impacts[i]=Layer("Lightning impact "+i,art[3],26);}
            var go=new GameObject("Cloth clipping",typeof(SpriteMask));go.layer=cloth.gameObject.layer;go.transform.SetParent(root.transform,false);
            var mask=go.GetComponent<SpriteMask>();mask.sprite=white;mask.isCustomRangeActive=true;mask.backSortingOrder=20199;mask.frontSortingOrder=20240;
            go.transform.SetPositionAndRotation(cloth.position+Vector3.up*.003f,Quaternion.Euler(90,cloth.eulerAngles.y,0));
            go.transform.localScale=new Vector3(width,depth,1);
            var random=new System.Random(508);
            for(int i=0;i<drops.Length;i++)drops[i]=new Vector2((float)random.NextDouble(),(float)random.NextDouble());
        }
        Image ScreenLayer(string name,Sprite sprite)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));go.layer=cloth.gameObject.layer;go.transform.SetParent(weatherRect,false);
            var image=go.GetComponent<Image>();image.sprite=sprite;image.raycastTarget=false;image.enabled=false;return image;
        }
        void PlaceScreen(Image image,Vector2 position,Vector2 size,Color color,float angle=0)
        {
            image.enabled=color.a>.001f;if(!image.enabled)return;
            var extent=weatherRect.rect.size;
            // Use current canvas dimensions so window resizing and aspect ratios need no rebuild.
            image.rectTransform.anchoredPosition=Vector2.Scale(position,extent);
            image.rectTransform.sizeDelta=size*extent.y;
            image.rectTransform.localRotation=Quaternion.Euler(0,0,angle);image.color=color;
        }
        public void Play()
        {
            Stop();if(!cloth)return;
            width=cloth.TransformVector(Vector3.right).magnitude;depth=cloth.TransformVector(Vector3.forward).magnitude;Create();
            foreach(Transform child in transform)child.gameObject.SetActive(false);
            started=Time.unscaledTime;playing=true;root.SetActive(true);Sample(0);
        }
        Vector3 Point(float x,float z)=>cloth.position+cloth.right*x*width+cloth.forward*z*depth+Vector3.up*.012f;
        void Place(SpriteRenderer r,Vector3 p,Vector2 size,Color color,float angle=0)
        {
            r.enabled=color.a>.001f;if(!r.enabled)return;
            r.transform.SetPositionAndRotation(p,Quaternion.Euler(90,cloth.eulerAngles.y+angle,0));r.transform.localScale=new Vector3(size.x,size.y,1);r.color=color;
        }
        void Update(){if(playing)Sample(Time.unscaledTime-started);}
        public void Sample(float time)
        {
            if(!playing||!root)return;if(time>=Duration){Stop();return;}
            float arrival=Mathf.SmoothStep(0,1,time/.8f),clear=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(3.3f,Duration,time));
            float wet=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,1.05f,time))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(3.05f,4.25f,time)));
            float light=0;
            for(int i=0;i<bolts.Length;i++){
                float age=time-StrikeTimes[i];float a=age>=0&&age<.27f?(age<.07f?1:age<.11f?.18f:1-Mathf.InverseLerp(.11f,.27f,age)):0;light=Mathf.Max(light,a);
                float x=i==0?-.26f:i==1?.25f:-.04f,z=i==1?-.25f:-.07f;
                float length=depth*.62f;
                Place(bolts[i],Point(x,z)+cloth.forward*length*.36f,new Vector2(length*.65f,length),new Color(.8f,.94f,1,a),i==1?-12:8);
                Place(impacts[i],Point(x,z),Vector2.one*depth*(.13f+Mathf.Clamp01(age/.27f)*.28f),new Color(.7f,.9f,1,a));
            }
            float aspect=weatherRect.rect.height>0?weatherRect.rect.width/weatherRect.rect.height:1;
            PlaceScreen(veil,Vector2.zero,new Vector2(aspect,1),new Color(light>.01f?.5f:.025f,light>.01f?.7f:.04f,light>.01f?1:.09f,light>.01f?light*.19f:arrival*clear*.19f));
            for(int i=0;i<clouds.Length;i++){
                float x=(i-2)*.24f,z=.39f;
                float travel=(1-arrival)*(i%2==0?-1:1)*.9f;
                float drift=Mathf.Sin(time*.7f+i)*.025f;
                float size=Mathf.Max(.64f,aspect*.33f);
                PlaceScreen(clouds[i],new Vector2(x+travel+drift,z+(1-clear)*.22f),new Vector2(size*1.25f,size),new Color(1+light*.3f,1+light*.3f,1+light*.4f,arrival*clear*.82f),(i%3-1)*7);
            }
            for(int i=0;i<rain.Length;i++){
                float speed=1.3f+(i%7)*.13f,phase=Mathf.Repeat(drops[i].y+time*speed,1);
                float x=drops[i].x-.5f-(phase-.5f)*.10f,z=.52f-phase*1.04f;
                float edge=Mathf.SmoothStep(0,1,phase/.08f)*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.87f,1,phase)));
                PlaceScreen(rain[i],new Vector2(x,z),new Vector2(.0022f,.055f+i%5*.012f),new Color(.57f,.8f,1,wet*edge*(.50f+i%3*.16f)),-18);
            }
            for(int i=0;i<splashes.Length;i++){
                float phase=Mathf.Repeat(time*1.65f+drops[i].y,1),a=1-Mathf.SmoothStep(0,1,phase);
                Place(splashes[i],Point(drops[i+24].x-.5f,drops[i+48].y-.5f),Vector2.one*depth*(.025f+phase*.065f),new Color(.55f,.8f,1,wet*a*.65f),i*41);
            }
        }
        public void Stop(){playing=false;if(root)root.SetActive(false);}
        void OnDisable(){Stop();}
        void OnDestroy(){Stop();Release(root);foreach(var sprite in art)Release(sprite);Release(white);}
        static void Release(Object value){if(!value)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
