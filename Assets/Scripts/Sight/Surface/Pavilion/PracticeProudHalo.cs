using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Assets.Scripts.Sight.Surface.Pavilion
{
    // Shared by local practice and multiplayer; replays store the resulting Image states.
    public sealed class PracticeProudHalo : MonoBehaviour
    {
        public const float Duration=3.8f;
        readonly Sprite[] art=new Sprite[4];
        readonly Image[] rings=new Image[2],stars=new Image[5],motes=new Image[32];
        Material rayMaterial,ringMaterial;
        Image rays,crown;
        Transform cloth;
        Camera camera;
        GameObject root;
        Canvas overlay;
        RectTransform screen;
        Vector3 center;
        float depth,started;
        bool playing;
        public bool Finished=>!playing;
        public GameObject VisualRoot=>root;
        public Canvas OverlayCanvas=>overlay;
        public Vector3 CuePoint=>center;
        public void Configure(Transform value){cloth=value;}
        public void SetViewCamera(Camera value){camera=value;}
        Image Layer(string name,int index)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));go.layer=cloth.gameObject.layer;go.transform.SetParent(screen,false);
            var image=go.GetComponent<Image>();image.sprite=art[index];image.raycastTarget=false;image.enabled=false;return image;
        }
        void Create()
        {
            if(root)return;
            var texture=Resources.Load<Texture2D>("Gameplay/Sentiments/ProudHalo/PrideAtlas");
            if(!texture)throw new System.InvalidOperationException("s8_5 proud halo artwork is missing");
            for(int i=0;i<4;i++)art[i]=Sprite.Create(texture,new Rect(i%2*(texture.width/2),(1-i/2)*(texture.height/2),texture.width/2,texture.height/2),Vector2.one*.5f,texture.width/2f,0,SpriteMeshType.FullRect);
            root=new GameObject("Sentiment s8_5 proud halo");SceneManager.MoveGameObjectToScene(root,gameObject.scene);
            var go=new GameObject("Proud halo above HUD",typeof(RectTransform),typeof(Canvas));go.layer=cloth.gameObject.layer;go.transform.SetParent(root.transform,false);
            overlay=go.GetComponent<Canvas>();overlay.renderMode=RenderMode.ScreenSpaceOverlay;overlay.overrideSorting=true;overlay.sortingOrder=32767;screen=(RectTransform)go.transform;
            rays=Layer("Golden halo rays",0);
            for(int i=0;i<rings.Length;i++)rings[i]=Layer("Expanding gold ring "+i,1);
            for(int i=0;i<motes.Length;i++)motes[i]=Layer("Gold mote "+i,3);
            crown=Layer("Proud crown",2);
            for(int i=0;i<stars.Length;i++)stars[i]=Layer("Pride star "+i,3);
            var shader=Resources.Load<Shader>("Gameplay/Sentiments/ProudHalo/HaloMask");
            if(!shader)throw new System.InvalidOperationException("s8_5 halo transparency shader is missing");
            rayMaterial=new Material(shader);rayMaterial.SetVector("_UVRect",new Vector4(0,.5f,.5f,.5f));
            ringMaterial=new Material(shader);ringMaterial.SetVector("_UVRect",new Vector4(.5f,.5f,.5f,.5f));ringMaterial.SetFloat("_InnerRadius",.27f);ringMaterial.SetFloat("_InnerFeather",.045f);ringMaterial.SetFloat("_OuterFeather",.09f);
            rays.material=rayMaterial;foreach(var ring in rings)ring.material=ringMaterial;
        }
        public void Play(Vector3 cuePoint)
        {
            Stop();if(!cloth)return;if(!camera)camera=Camera.main;depth=cloth.TransformVector(Vector3.forward).magnitude;Create();
            foreach(Transform child in transform)child.gameObject.SetActive(false);
            var p=cloth.InverseTransformPoint(cuePoint);p.x=Mathf.Clamp(p.x,-.46f,.46f);p.z=Mathf.Clamp(p.z,-.44f,.44f);p.y=0;
            center=cloth.TransformPoint(p)+Vector3.up*.014f;
            playing=true;started=Time.unscaledTime;root.SetActive(true);Sample(0);
        }
        Vector3 Point(float x,float y)=>center+cloth.right*x*depth+cloth.forward*y*depth;
        void Place(Image image,Vector3 point,float size,float alpha,float angle=0)
        {
            image.enabled=alpha>.001f&&camera;if(!image.enabled)return;
            var q=Quaternion.Euler(90,cloth.eulerAngles.y+angle,0);var p=camera.WorldToViewportPoint(point);if(p.z<=0){image.enabled=false;return;}
            var a=camera.WorldToViewportPoint(point+q*Vector3.right*size*.5f)-camera.WorldToViewportPoint(point-q*Vector3.right*size*.5f);
            var b=camera.WorldToViewportPoint(point+q*Vector3.up*size*.5f)-camera.WorldToViewportPoint(point-q*Vector3.up*size*.5f);
            var extent=screen.rect.size;Vector2 dx=Vector2.Scale(new Vector2(a.x,a.y),extent),dy=Vector2.Scale(new Vector2(b.x,b.y),extent);
            var rect=image.rectTransform;rect.anchoredPosition=Vector2.Scale(new Vector2(p.x-.5f,p.y-.5f),extent);rect.sizeDelta=new Vector2(dx.magnitude,dy.magnitude);rect.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(dx.y,dx.x)*Mathf.Rad2Deg);image.color=new Color(1,1,1,Mathf.Clamp01(alpha));
        }
        void Update(){if(playing)Sample(Time.unscaledTime-started);}
        public void Sample(float time)
        {
            if(!playing||!root)return;if(time>=Duration){Stop();return;}
            float clear=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.35f,3.35f,time));
            float unfold=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.42f,.95f,time));
            float pulse=Mathf.Exp(-Mathf.Pow((time-1.2f)/.19f,2))+Mathf.Exp(-Mathf.Pow((time-1.95f)/.22f,2));
            Place(rays,center,depth*Mathf.Lerp(.54f,.96f,unfold),unfold*clear*(.70f+pulse*.30f),time*9);
            for(int i=0;i<rings.Length;i++){
                float age=time-(.58f+i*.76f),t=Mathf.Clamp01(age/.90f);
                Place(rings[i],center,depth*Mathf.Lerp(.30f,1.08f,t),age>=0?Mathf.Sin(t*Mathf.PI)*clear*.80f:0);
            }
            float arrive=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.95f,1.3f,time));
            Place(crown,Point(0,.10f+arrive*.025f+Mathf.Sin(time*3)*.008f),depth*.20f,arrive*clear,-8+Mathf.Sin(time*2.2f)*5);
            for(int i=0;i<stars.Length;i++){
                float age=time-(1.10f+i*.16f),t=Mathf.Clamp01(age/.85f),angle=i*137+17;
                float radius=.31f+i%2*.05f;var p=Point(Mathf.Cos(angle*Mathf.Deg2Rad)*radius,Mathf.Sin(angle*Mathf.Deg2Rad)*radius);
                float alpha=age>=0?Mathf.Sin(t*Mathf.PI)*clear:0;
                Place(stars[i],p,depth*(.09f+i%2*.025f)*(1+Mathf.Sin(t*Mathf.PI)*.25f),alpha,i*19+time*10);
            }
            for(int i=0;i<motes.Length;i++){
                float angle=i*137.508f*Mathf.Deg2Rad,t=Mathf.Clamp01(time/.65f),radius=Mathf.Lerp(.32f+i%4*.03f,.09f,t);
                float alpha=Mathf.Sin(t*Mathf.PI)*.75f;
                var p=Point(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius);
                if(time>=.65f){
                    float age=time-(2.20f+i*.017f),life=.85f+i%4*.07f,u=Mathf.Clamp01(age/life);
                    p=Point(Mathf.Cos(angle)*(.12f+i%5*.037f),Mathf.Sin(angle)*.17f+u*.24f);
                    alpha=age>=0?Mathf.Sin(u*Mathf.PI)*(1-u):0;
                }
                Place(motes[i],p,depth*(.018f+i%3*.006f),alpha,time*15+i*23);
            }
        }
        public void Stop(){playing=false;if(root)root.SetActive(false);}
        void OnDisable(){Stop();}
        void OnDestroy(){Stop();Release(root);foreach(var sprite in art)Release(sprite);Release(rayMaterial);Release(ringMaterial);}
        static void Release(Object value){if(!value)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
