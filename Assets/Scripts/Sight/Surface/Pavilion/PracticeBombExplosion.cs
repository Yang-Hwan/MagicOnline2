using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.Sight.Surface.Pavilion
{
    // Shared practice/multiplayer presentation; never alters table materials or ball physics.
    public sealed class PracticeBombExplosion : MonoBehaviour
    {
        public const float ImpactTime=.65f,BombInterval=.7f,Duration=4.5f;
        readonly Sprite[] artwork=new Sprite[9];
        sealed class Burst
        {
            public readonly SpriteRenderer[] debris=new SpriteRenderer[18],smoke=new SpriteRenderer[7];
            public SpriteRenderer bomb,shadow,flash,ring,fire;
            public Vector3 center;
        }
        readonly Burst[] bursts={new Burst(),new Burst()};
        readonly Vector2[] directions=new Vector2[18];
        Transform cloth;
        GameObject root;
        float depth,started;
        bool playing;
        public bool Finished=>!playing;
        public GameObject VisualRoot=>root;
        public void Configure(Transform value){cloth=value;}
        SpriteRenderer Layer(string name,int sprite,int order)
        {
            var go=new GameObject(name,typeof(SpriteRenderer));go.layer=cloth.gameObject.layer;
            go.transform.SetParent(root.transform,false);
            var r=go.GetComponent<SpriteRenderer>();r.sprite=artwork[sprite];r.sortingOrder=20100+order;
            r.maskInteraction=SpriteMaskInteraction.VisibleInsideMask;return r;
        }
        void Create()
        {
            if(root)return;
            var texture=Resources.Load<Texture2D>("Gameplay/Sentiments/Bomb/ExplosionAtlas");
            if(!texture)throw new System.InvalidOperationException("s1_4 explosion artwork is missing");
            // Equal cells, top-left first, normalized to one world unit per cell.
            for(int i=0;i<9;i++)artwork[i]=Sprite.Create(texture,new Rect(i%3*(texture.width/3), (2-i/3)*(texture.height/3),texture.width/3,texture.height/3),Vector2.one*.5f,texture.width/3f,0,SpriteMeshType.FullRect);
            // Standalone effects must retain the Sentiment identity used by replay discovery.
            root=new GameObject("Sentiment s1_4 explosion");SceneManager.MoveGameObjectToScene(root,gameObject.scene);
            for(int index=0;index<bursts.Length;index++){
                var b=bursts[index];string suffix=index==0?"":" (2)";
                b.bomb=Layer("Falling bomb"+suffix,0,30);b.shadow=Layer("Bomb shadow"+suffix,0,0);
                b.flash=Layer("Detonation flash"+suffix,1,25);b.ring=Layer("Shockwave"+suffix,2,2);b.fire=Layer("Fireball"+suffix,3,10);
                for(int i=0;i<b.debris.Length;i++)b.debris[i]=Layer("Casing shard "+i+suffix,8,20);
                for(int i=0;i<b.smoke.Length;i++)b.smoke[i]=Layer("Residual smoke "+i+suffix,6+i%2,12+i);
            }
            var maskGo=new GameObject("Cloth clipping",typeof(SpriteMask));maskGo.layer=cloth.gameObject.layer;maskGo.transform.SetParent(root.transform,false);
            var mask=maskGo.GetComponent<SpriteMask>();
            mask.sprite=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),Vector2.one*.5f,1);mask.isCustomRangeActive=true;mask.backSortingOrder=20099;mask.frontSortingOrder=20140;
            maskGo.transform.SetPositionAndRotation(cloth.position+Vector3.up*.003f,Quaternion.Euler(90,cloth.eulerAngles.y,0));
            maskGo.transform.localScale=new Vector3(cloth.TransformVector(Vector3.right).magnitude,cloth.TransformVector(Vector3.forward).magnitude,1);
            maskSprite=mask.sprite;
        }
        Sprite maskSprite;
        public void Play()
        {
            Stop();if(!cloth)return;Create();
            foreach(Transform child in transform)child.gameObject.SetActive(false);
            depth=cloth.TransformVector(Vector3.forward).magnitude;
            bursts[0].center=cloth.TransformPoint(new Vector3(-.19f,0,.08f))+Vector3.up*.008f;
            bursts[1].center=cloth.TransformPoint(new Vector3(.19f,0,-.10f))+Vector3.up*.008f;
            var random=new System.Random(104);
            for(int i=0;i<directions.Length;i++){
                float a=(float)random.NextDouble()*Mathf.PI*2;
                directions[i]=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
            }
            playing=true;started=Time.unscaledTime;root.SetActive(true);Sample(0);
        }
        void Place(SpriteRenderer r,Vector3 position,float size,float alpha,float angle=0)
        {
            r.enabled=alpha>.001f;if(!r.enabled)return;
            r.transform.SetPositionAndRotation(position,Quaternion.Euler(90,cloth.eulerAngles.y+angle,0));
            r.transform.localScale=Vector3.one*size;r.color=new Color(1,1,1,Mathf.Clamp01(alpha));
        }
        void Update(){if(playing)Sample(Time.unscaledTime-started);}
        public void Sample(float time)
        {
            if(!playing||!root)return;if(time>=Duration){Stop();return;}
            for(int i=0;i<bursts.Length;i++)SampleBurst(bursts[i],time-i*BombInterval,i);
        }
        void SampleBurst(Burst burst,float time,int index)
        {
            var bomb=burst.bomb;var shadow=burst.shadow;var flash=burst.flash;var ring=burst.ring;var fire=burst.fire;
            var debris=burst.debris;var smoke=burst.smoke;var center=burst.center;
            float age=time-ImpactTime,fall=Mathf.Clamp01(time/ImpactTime);
            // Screen-plane fall remains readable even from the overhead practice camera.
            var raised=center+cloth.forward*(depth*.75f*(1-fall*fall));
            Place(bomb,raised+Vector3.up*.02f,depth*Mathf.Lerp(.24f,.17f,fall),time>=0&&age<0?1:0,12*(1-fall));
            Place(shadow,center,depth*Mathf.Lerp(.10f,.17f,fall),time>=0&&age<0?.25f:0);
            shadow.color=new Color(0,0,0,time>=0&&age<0?.25f:0);
            Place(flash,center+Vector3.up*.02f,depth*(.3f+Mathf.Clamp01(age/.1f)*.85f),age>=0?1-Mathf.Clamp01(age/.20f):0,age*45);
            Place(ring,center,depth*Mathf.Lerp(.15f,1.65f,Mathf.Clamp01(age/.48f)),age>=0?1-Mathf.Clamp01(age/.48f):0);
            fire.sprite=artwork[age<.16f?3:age<.43f?4:5];
            Place(fire,center+Vector3.up*.012f,depth*Mathf.Lerp(.32f,1.0f,Mathf.Clamp01(age/.5f)),age>=0?1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.48f,.95f,age)):0);
            for(int i=0;i<debris.Length;i++){
                float small=i/(float)(debris.Length-1),life=Mathf.Lerp(1.0f,.55f,small),t=Mathf.Clamp01(age/life);
                var direction=directions[(i+index*5)%directions.Length];
                var d=cloth.right*direction.x+cloth.forward*direction.y;
                float distance=depth*Mathf.Lerp(.55f,1.15f,small)*(1-Mathf.Exp(-2.4f*Mathf.Max(0,age)));
                Place(debris[i],center+d*distance+Vector3.up*(.025f+Mathf.Sin(t*Mathf.PI)*depth*.12f),depth*Mathf.Lerp(.12f,.035f,small),age>=0&&age<life?1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,1,t)):0,i*137+age*(i%2==0?250:-330));
            }
            for(int i=0;i<smoke.Length;i++){
                float a=age-.26f-i*.045f,life=2.15f+i*.065f,t=Mathf.Clamp01(a/life);
                var d=cloth.right*Mathf.Sin(i*2.4f)+cloth.forward*Mathf.Cos(i*2.4f);
                // Thin out throughout the drift instead of holding an opaque cloud until the end.
                float alpha=a>=0?Mathf.SmoothStep(0,1,a/.30f)*Mathf.Pow(1-t,2.2f)*.42f:0;
                Place(smoke[i],center+d*depth*(.07f+t*.29f)+cloth.forward*depth*t*.18f+Vector3.up*.018f,depth*(.26f+t*.52f),alpha,i*51+t*18);
            }
        }
        public void Stop(){playing=false;if(root)root.SetActive(false);}
        void OnDisable(){Stop();}
        void OnDestroy(){Stop();Release(root);foreach(var sprite in artwork)Release(sprite);Release(maskSprite);}
        static void Release(Object value){if(!value)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
