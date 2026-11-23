using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Assets.Scripts.Sight.Surface.Pavilion
{
    // All elements are non-interactive overlay images, captured by state-event replays.
    public sealed class PracticeTearFountain : MonoBehaviour
    {
        public const float FinalImpact=3.1f,Duration=4.6f;
        readonly Sprite[] art=new Sprite[6];
        readonly Sprite[] fountainFrames=new Sprite[4];
        readonly Image[] jets=new Image[4],drops=new Image[32],large=new Image[2],splashes=new Image[2];
        Transform cloth;
        GameObject root;
        Image face;
        Canvas overlay;
        RectTransform screen;
        Camera camera;
        Vector3 center;
        float depth,started;
        bool playing;
        public Canvas OverlayCanvas=>overlay;
        public GameObject VisualRoot=>root;
        public Vector3 CuePoint=>center;
        public bool Finished=>!playing;
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
            var texture=Resources.Load<Texture2D>("Gameplay/Sentiments/TearFountain/TearAtlas");
            if(!texture)throw new System.InvalidOperationException("s5_11 tear fountain artwork is missing");
            for(int i=0;i<6;i++)art[i]=Sprite.Create(texture,new Rect(i%3*(texture.width/3),(1-i/3)*(texture.height/2),texture.width/3,texture.height/2),Vector2.one*.5f,texture.width/3f,0,SpriteMeshType.FullRect);
            var fountain=Resources.Load<Texture2D>("Gameplay/Sentiments/TearFountain/FountainLoop");
            if(!fountain)throw new System.InvalidOperationException("s5_11 connected fountain artwork is missing");
            for(int i=0;i<4;i++)fountainFrames[i]=Sprite.Create(fountain,new Rect(i%2*(fountain.width/2),(1-i/2)*(fountain.height/2),fountain.width/2,fountain.height/2),Vector2.one*.5f,fountain.width/2f,0,SpriteMeshType.FullRect);
            root=new GameObject("Sentiment s5_11 tear fountain");SceneManager.MoveGameObjectToScene(root,gameObject.scene);
            var go=new GameObject("Tear fountain above HUD",typeof(RectTransform),typeof(Canvas));go.layer=cloth.gameObject.layer;go.transform.SetParent(root.transform,false);
            overlay=go.GetComponent<Canvas>();overlay.renderMode=RenderMode.ScreenSpaceOverlay;overlay.overrideSorting=true;overlay.sortingOrder=32767;screen=(RectTransform)go.transform;
            for(int i=0;i<2;i++)splashes[i]=Layer("Tear splash "+i,4);
            for(int i=0;i<jets.Length;i++)jets[i]=Layer("Tear jet "+i,5);
            for(int i=0;i<drops.Length;i++)drops[i]=Layer("Flying tear "+i,3);
            for(int i=0;i<2;i++)large[i]=Layer("Final large tear "+i,3);
            face=Layer("Crying face",0);
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
        void Place(Image image,Vector3 point,Vector2 size,float alpha,float angle=0)
        {
            image.enabled=alpha>.001f&&camera;if(!image.enabled)return;
            var q=Quaternion.Euler(90,cloth.eulerAngles.y+angle,0);var p=camera.WorldToViewportPoint(point);if(p.z<=0){image.enabled=false;return;}
            var a=camera.WorldToViewportPoint(point+q*Vector3.right*size.x*.5f)-camera.WorldToViewportPoint(point-q*Vector3.right*size.x*.5f);
            var b=camera.WorldToViewportPoint(point+q*Vector3.up*size.y*.5f)-camera.WorldToViewportPoint(point-q*Vector3.up*size.y*.5f);
            var extent=screen.rect.size;Vector2 dx=Vector2.Scale(new Vector2(a.x,a.y),extent),dy=Vector2.Scale(new Vector2(b.x,b.y),extent);
            var rect=image.rectTransform;rect.anchoredPosition=Vector2.Scale(new Vector2(p.x-.5f,p.y-.5f),extent);rect.sizeDelta=new Vector2(dx.magnitude,dy.magnitude);rect.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(dx.y,dx.x)*Mathf.Rad2Deg);image.color=new Color(1,1,1,Mathf.Clamp01(alpha));
        }
        Vector3 Arc(int side,float t,float strength)
        {
            float s=side==0?-1:1;
            return Point(s*Mathf.Lerp(.065f,.30f,t),Mathf.Lerp(.245f,-.10f,t)+Mathf.Sin(t*Mathf.PI)*(.16f+strength*.045f));
        }
        void Update(){if(playing)Sample(Time.unscaledTime-started);}
        public void Sample(float time)
        {
            if(!playing||!root)return;if(time>=Duration){Stop();return;}
            float appear=Mathf.SmoothStep(0,1,time/.5f),fade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(3.25f,4.25f,time));
            float pulse=time>=.65f?Mathf.Sin((time-.65f)*Mathf.PI*3.5f):0;
            face.sprite=art[time<.65f?0:time<2.65f?1:2];
            Place(face,Point(0,.22f),new Vector2(depth*(.35f+pulse*.002f),depth*(.32f-pulse*.002f)),appear*fade,time<2.65f?pulse*.6f:Mathf.Sin(time*9)*.6f);
            float jet= time>=.65f?Mathf.SmoothStep(0,1,(time-.65f)/.20f)*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.15f,2.65f,time))):0;
            for(int side=0;side<2;side++){
                float frame=Mathf.Max(0,time-.65f)*10,blend=frame-Mathf.Floor(frame);int current=(int)frame%4;
                for(int layer=0;layer<2;layer++){
                    var image=jets[side*2+layer];image.sprite=fountainFrames[(current+layer)%4];
                    Place(image,Point(side==0?-.22f:.22f,.07f),new Vector2(depth*.40f,depth*.50f),jet*(layer==0?1-blend:blend));
                    image.rectTransform.localScale=new Vector3(side==0?1:-1,1,1);
                }
                float s=side==0?-1:1,age=time-2.72f,t=Mathf.Clamp01(age/(FinalImpact-2.72f));
                Place(large[side],Point(s*Mathf.Lerp(.08f,.30f,t),Mathf.Lerp(.245f,-.04f,t*t)),Vector2.one*depth*.10f,age>=0&&time<FinalImpact?1:0,s*4);
                float hit=time-FinalImpact;
                float splash=hit>=0?1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.1f,1.20f,hit)):0;
                Place(splashes[side],Point(s*.30f,-.06f),new Vector2(depth*.27f,depth*.20f),splash);
            }
            for(int i=0;i<drops.Length;i++){
                int side=i%2;float phase=Mathf.Repeat(time*1.5f+i*.137f,1);var point=Arc(side,phase,pulse)+cloth.right*depth*Mathf.Sin(i*7+time*4)*.018f;
                float alpha=0; // The looping fountain sprite already includes flowing drops and its landing splash.
                if(time>=FinalImpact){float age=time-FinalImpact-i*.008f,t=Mathf.Clamp01(age/.85f);point=Point((side==0?-1:1)*(.3f+t*(.08f+i%4*.025f)),-.12f+Mathf.Sin(t*Mathf.PI)*.09f-t*.08f);alpha=age>=0?Mathf.Sin(t*Mathf.PI)*(1-t):0;}
                Place(drops[i],point,Vector2.one*depth*(.023f+i%4*.007f),alpha,(side==0?-1:1)*(20+phase*55));
            }
        }
        public void Stop(){playing=false;if(root)root.SetActive(false);}
        void OnDisable(){Stop();}
        void OnDestroy(){Stop();Release(root);foreach(var sprite in art)Release(sprite);foreach(var sprite in fountainFrames)Release(sprite);}
        static void Release(Object value){if(!value)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
