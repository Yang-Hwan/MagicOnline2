using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Assets.Scripts.Sight.Surface.Pavilion
{
    // Temporary sprite presentation around the cue point; never changes gameplay state.
    public sealed class PracticeSkullCurse : MonoBehaviour
    {
        public const float FinalImpact=2.45f,Duration=4.4f;
        const int BoneCount=30;
        readonly Sprite[] art=new Sprite[6];
        readonly Image[] bones=new Image[BoneCount+1],hits=new Image[BoneCount+1],mist=new Image[3],motes=new Image[48];
        readonly Vector2[] offsets=new Vector2[BoneCount+1],directions=new Vector2[48];
        Transform cloth;
        GameObject root;
        Image skull;
        Canvas overlay;
        RectTransform overlayRect;
        Camera viewCamera;
        readonly List<KeyValuePair<Image,int>> layers=new List<KeyValuePair<Image,int>>();
        public Canvas OverlayCanvas=>overlay;
        public void SetViewCamera(Camera value){viewCamera=value;}
        Vector3 center;
        float depth,started;
        bool playing;
        public bool Finished=>!playing;
        public GameObject VisualRoot=>root;
        public Vector3 CuePoint=>center;
        public void Configure(Transform value){cloth=value;}
        Image Layer(string name,int index,int order,bool masked=true)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));go.layer=cloth.gameObject.layer;go.transform.SetParent(overlayRect,false);
            var r=go.GetComponent<Image>();r.sprite=art[index];r.raycastTarget=false;r.enabled=false;layers.Add(new KeyValuePair<Image,int>(r,order));return r;
        }
        void Create()
        {
            if(root)return;
            var texture=Resources.Load<Texture2D>("Gameplay/Sentiments/SkullCurse/SkullAtlas");
            if(!texture)throw new System.InvalidOperationException("s5_9 skull curse artwork is missing");
            for(int i=0;i<6;i++)art[i]=Sprite.Create(texture,new Rect(i%3*(texture.width/3),(1-i/3)*(texture.height/2),texture.width/3,texture.height/2),Vector2.one*.5f,texture.width/3f,0,SpriteMeshType.FullRect);
            root=new GameObject("Practice s5_9 skull curse");SceneManager.MoveGameObjectToScene(root,gameObject.scene);
            var screen=new GameObject("Skull curse above HUD",typeof(RectTransform),typeof(Canvas));screen.layer=cloth.gameObject.layer;screen.transform.SetParent(root.transform,false);
            overlay=screen.GetComponent<Canvas>();overlay.renderMode=RenderMode.ScreenSpaceOverlay;overlay.overrideSorting=true;overlay.sortingOrder=32767;overlayRect=(RectTransform)screen.transform;
            for(int i=0;i<mist.Length;i++)mist[i]=Layer("Curse mist "+i,4,1,false);
            for(int i=0;i<bones.Length;i++){bones[i]=Layer(i==BoneCount?"Final large bone":"Falling bone "+i,1+i%3,10);hits[i]=Layer("Bone impact "+i,5,12);}
            for(int i=0;i<motes.Length;i++)motes[i]=Layer("Dissolve mote "+i,5,15,false);
            skull=Layer("Curse skull",0,20,false);
            layers.Sort((a,b)=>a.Value.CompareTo(b.Value));foreach(var layer in layers)layer.Key.transform.SetAsLastSibling();
            var random=new System.Random(509);
            for(int i=0;i<offsets.Length;i++)offsets[i]=new Vector2(((float)random.NextDouble()-.5f)*.52f,((float)random.NextDouble()-.5f)*.32f);
            offsets[BoneCount]=new Vector2(.05f,-.04f);
            for(int i=0;i<directions.Length;i++){float angle=(float)random.NextDouble()*Mathf.PI*2;directions[i]=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));}
        }
        public void Play(Vector3 cuePoint)
        {
            Stop();if(!cloth)return;if(!viewCamera)viewCamera=Camera.main;depth=cloth.TransformVector(Vector3.forward).magnitude;Create();
            foreach(Transform child in transform)child.gameObject.SetActive(false);
            var local=cloth.InverseTransformPoint(cuePoint);local.x=Mathf.Clamp(local.x,-.46f,.46f);local.z=Mathf.Clamp(local.z,-.44f,.44f);local.y=0;
            center=cloth.TransformPoint(local)+Vector3.up*.014f;
            playing=true;started=Time.unscaledTime;root.SetActive(true);Sample(0);
        }
        Vector3 Offset(Vector2 p)=>cloth.right*p.x*depth+cloth.forward*p.y*depth;
        void Place(Image r,Vector3 point,float size,float alpha,float angle=0, bool purple=false)
        {
            r.enabled=alpha>.001f&&viewCamera;if(!r.enabled)return;
            var rotation=Quaternion.Euler(90,cloth.eulerAngles.y+angle,0);
            var p=viewCamera.WorldToViewportPoint(point);if(p.z<=0){r.enabled=false;return;}
            var right=viewCamera.WorldToViewportPoint(point+rotation*Vector3.right*size*.5f)-viewCamera.WorldToViewportPoint(point-rotation*Vector3.right*size*.5f);
            var up=viewCamera.WorldToViewportPoint(point+rotation*Vector3.up*size*.5f)-viewCamera.WorldToViewportPoint(point-rotation*Vector3.up*size*.5f);
            var extent=overlayRect.rect.size;
            Vector2 dx=Vector2.Scale(new Vector2(right.x,right.y),extent),dy=Vector2.Scale(new Vector2(up.x,up.y),extent);
            r.rectTransform.anchoredPosition=Vector2.Scale(new Vector2(p.x-.5f,p.y-.5f),extent);
            r.rectTransform.sizeDelta=new Vector2(dx.magnitude,dy.magnitude);
            r.rectTransform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(dx.y,dx.x)*Mathf.Rad2Deg);
            r.color=purple?new Color(.8f,.48f,1,Mathf.Clamp01(alpha)):new Color(1,1,1,Mathf.Clamp01(alpha));
        }
        void Update(){if(playing)Sample(Time.unscaledTime-started);}
        public void Sample(float time)
        {
            if(!playing||!root)return;if(time>=Duration){Stop();return;}
            float appear=Mathf.SmoothStep(0,1,time/.48f),fade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.8f,3.9f,time));
            var above=center+cloth.forward*depth*(.34f+Mathf.Sin(time*3)*.015f);
            Place(skull,above,depth*(.42f+appear*.10f),appear*fade,Mathf.Sin(time*2.5f)*5);
            for(int i=0;i<mist.Length;i++){
                float clear=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.9f,Duration,time));
                Place(mist[i],center+cloth.forward*depth*(i*.14f)+cloth.right*depth*Mathf.Sin(time+i)*.025f,depth*(.53f+time*.055f),appear*clear*.55f,time*(i%2==0?12:-14)+i*61);
            }
            for(int i=0;i<bones.Length;i++){
                bool final=i==BoneCount;
                float start=final?FinalImpact-.28f:.66f+(i/10)*.35f+(i%10)*.018f;
                float fall=final?.28f:.30f+(i%4)*.035f,age=time-start,landing=age-fall;
                var target=center+Offset(offsets[i]);
                float descent=Mathf.Clamp01(age/fall),bounce=landing>=0?Mathf.Abs(Mathf.Sin(Mathf.Min(landing,.65f)*Mathf.PI*3/.65f))*Mathf.Exp(-landing*5):0;
                var point=target+cloth.forward*depth*(age<fall?.48f*(1-descent*descent):bounce*(final?.08f:.045f));
                if(landing>0)point+=Offset(offsets[i])*(1-Mathf.Exp(-landing*3))*.45f;
                float dissolve=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.9f+i%5*.07f,3.9f+i%5*.07f,time));
                float size=depth*(final?.29f:.065f+(i%6)*.012f);
                Place(bones[i],point,size,age>=0?dissolve:0,i*137+(age<fall?Mathf.Max(0,age)*(i%2==0?370:-290):fall*(i%2==0?370:-290)+bounce*25));
                float hitAlpha=landing>=0?1-Mathf.Clamp01(landing/(final?.35f:.17f)):0;
                Place(hits[i],target,depth*(final?.38f:.10f),hitAlpha,25*i);
            }
            for(int i=0;i<motes.Length;i++){
                float age=time-(2.72f+i*.012f),life=.8f+(i%5)*.08f,t=Mathf.Clamp01(age/life);
                var d=directions[i];var point=center+Offset(d*(.08f+t*.31f))+cloth.forward*depth*(.1f+t*.22f);
                float a=age>=0&&age<life?Mathf.Sin(t*Mathf.PI)*(1-t):0;
                Place(motes[i],point,depth*(.017f+i%3*.006f)*(1-t*.5f),a*1.6f,i*31+time*24,true);
            }
        }
        public void Stop(){playing=false;if(root)root.SetActive(false);}
        void OnDisable(){Stop();}
        void OnDestroy(){Stop();Release(root);foreach(var sprite in art)Release(sprite);}
        static void Release(Object value){if(!value)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
