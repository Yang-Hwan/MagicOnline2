using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Ball = Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.BallC;

namespace Assets.Scripts.Sight.Surface.Pavilion
{
    // Temporary presentation only. The cloth, balls, camera and physics are never modified.
    public sealed class PracticeTableDemolition : MonoBehaviour
    {
        public const float Duration=3.25f;
        public const float WindupSeconds=.16f,StrikeSeconds=.09f;
        const float StrikeInterval=.65f,StartDelay=.1f;
        const int ChipsPerStrike=12;
        public static float ImpactTime(int strike)=>StartDelay+strike*StrikeInterval+WindupSeconds+StrikeSeconds;
        const string Art="Gameplay/Sentiments/Demolition/";
        static readonly float[] CrackAngles={-16,24,8};
        static readonly int[] HammerViews={0,2,2};
        readonly Sprite[] hammerSprites=new Sprite[3],crackSprites=new Sprite[3];
        readonly Sprite[] chipSprites=new Sprite[6];
        readonly SpriteRenderer[] cracks=new SpriteRenderer[3],chips=new SpriteRenderer[3*ChipsPerStrike],rays=new SpriteRenderer[18];
        readonly Vector3[] points=new Vector3[3];
        readonly List<Ball> balls=new List<Ball>();
        struct Fragment {public Vector3 direction,offset;public float size,aspect,speed,spin,rotation,life,delay,lift,shade;public int sprite;}
        readonly Fragment[] fragments=new Fragment[3*ChipsPerStrike];
        int burstNumber;
        Transform cloth;
        GameObject visuals;
        SpriteRenderer hammer,shadow;
        Sprite white;
        SpriteMask clip;
        float started,width,depth,surface;
        bool playing;
        SentimentSpriteOverlay overlay;
        public SentimentSpriteOverlay Overlay=>overlay;
        public bool Finished=>!playing;
        public GameObject VisualRoot=>visuals;
        public IReadOnlyList<Vector3> StrikePoints=>points;
        public int VisibleCracks {get{int n=0;foreach(var crack in cracks)if(crack&&crack.enabled&&crack.color.a>0)n++;return n;}}

        public void Configure(Transform clothSpace){cloth=clothSpace;}
        SpriteRenderer Layer(string name,int order,bool masked=true)
        {
            var node=new GameObject(name,typeof(SpriteRenderer));node.layer=cloth.gameObject.layer;
            node.transform.SetParent(visuals.transform,false);
            var r=node.GetComponent<SpriteRenderer>();r.sortingOrder=20000+order;
            r.maskInteraction=masked?SpriteMaskInteraction.VisibleInsideMask:SpriteMaskInteraction.None;
            r.enabled=false;return r;
        }
        void CreateVisuals()
        {
            if(visuals)return;
            var h=Resources.Load<Texture2D>(Art+"HammerAngles");var c=Resources.Load<Texture2D>(Art+"CrackStages");
            if(!h||!c)throw new System.InvalidOperationException("s5_10 demolition artwork is missing");
            for(int i=0;i<3;i++){
                int x0=Mathf.RoundToInt(i*h.width/3f),x1=Mathf.RoundToInt((i+1)*h.width/3f);
                hammerSprites[i]=Sprite.Create(h,new Rect(x0,0,x1-x0,h.height),new Vector2(i==1?.47f:.5f,.04f),100,0,SpriteMeshType.FullRect);
                crackSprites[i]=Sprite.Create(c,new Rect(i*c.width/3,0,c.width/3,c.height),Vector2.one*.5f,100,0,SpriteMeshType.FullRect);
            }
            white=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),Vector2.one*.5f,1,0,SpriteMeshType.FullRect);
            var concrete=Resources.Load<Texture2D>(Art+"ConcreteShards");
            if(!concrete)throw new System.InvalidOperationException("s5_10 concrete fragment artwork is missing");
            for(int i=0;i<chipSprites.Length;i++)chipSprites[i]=Sprite.Create(concrete,new Rect((i%3)*concrete.width/3,(1-i/3)*concrete.height/2,concrete.width/3,concrete.height/2),Vector2.one*.5f,concrete.width/3f,0,SpriteMeshType.FullRect);
            visuals=new GameObject("Practice s5_10 demolition");SceneManager.MoveGameObjectToScene(visuals,gameObject.scene);
            var mask=new GameObject("Cloth clipping",typeof(SpriteMask));mask.layer=cloth.gameObject.layer;mask.transform.SetParent(visuals.transform,false);
            clip=mask.GetComponent<SpriteMask>();clip.sprite=white;clip.isCustomRangeActive=true;
            clip.backSortingOrder=19990;clip.frontSortingOrder=20030;
            for(int i=0;i<3;i++)cracks[i]=Layer("Crack "+i,i);
            shadow=Layer("Hammer shadow",4);hammer=Layer("Demolition hammer",20,false);
            for(int i=0;i<chips.Length;i++)chips[i]=Layer("Slate chip "+i,10);
            for(int i=0;i<rays.Length;i++){rays[i]=Layer("Impact ray "+i,11);rays[i].sprite=white;}
            overlay=visuals.AddComponent<SentimentSpriteOverlay>();overlay.Initialize(cloth,visuals.GetComponentsInChildren<SpriteRenderer>(true));
        }
        public void Play()
        {
            Stop();if(!cloth)return;
            CreateVisuals();
            // The old scene-authored toy hammer and particle emitter belong only to this actor.
            foreach(Transform child in transform)child.gameObject.SetActive(false);
            width=cloth.TransformVector(Vector3.right).magnitude;depth=cloth.TransformVector(Vector3.forward).magnitude;
            surface=cloth.TransformPoint(Vector3.zero).y+.004f;
            clip.transform.SetPositionAndRotation(new Vector3(cloth.position.x,surface,cloth.position.z),Quaternion.Euler(90,cloth.eulerAngles.y,0));
            clip.transform.localScale=new Vector3(width,depth,1);
            balls.Clear();foreach(var root in gameObject.scene.GetRootGameObjects())foreach(var b in root.GetComponentsInChildren<Ball>(true))if(b.gameObject.activeInHierarchy)balls.Add(b);
            Vector2[] spots={new Vector2(-.27f,.19f),new Vector2(.27f,-.18f),new Vector2(0,.015f)};
            for(int i=0;i<3;i++)points[i]=ChoosePoint(spots[i],i);
            PrepareFragments();
            started=Time.unscaledTime;playing=true;visuals.SetActive(true);Sample(0);
        }
        void PrepareFragments()
        {
            // Sample once per playback, independently of Unity's gameplay random state.
            var random=new System.Random(510+burstNumber++*7919);
            float Next()=> (float)random.NextDouble();
            for(int strike=0;strike<3;strike++)for(int j=0;j<ChipsPerStrike;j++){
                float size=Mathf.Pow((j+.2f+Next()*.6f)/ChipsPerStrike,.85f);
                float roll=Next(),offset;
                if(roll<.68f)offset=(Next()+Next()-1)*48;
                else if(roll<.9f)offset=(Next()<.5f?-1:1)*(50+Next()*48);
                else offset=145+Next()*65;
                float yaw=cloth.eulerAngles.y+(strike==0?-90:90)+offset;
                fragments[strike*ChipsPerStrike+j]=new Fragment{
                    direction=Quaternion.Euler(0,yaw,0)*Vector3.forward,
                    offset=new Vector3(Next()-.5f,0,Next()-.5f)*depth*.025f,
                    size=depth*Mathf.Lerp(.018f,.086f,size),aspect=Mathf.Lerp(.45f,1.12f,Next()),
                    speed=depth*Mathf.Lerp(1.9f,.65f,size),
                    spin=Mathf.Lerp(-300,300,Next()),rotation=Next()*360,
                    life=Mathf.Lerp(.44f,.79f,size)+Next()*.06f,delay=Next()*.03f,
                    lift=depth*Mathf.Lerp(.14f,.065f,size)*Mathf.Lerp(.75f,1.2f,Next()),
                    shade=Mathf.Lerp(.90f,1f,Next()),sprite=(j*5+strike*2+burstNumber)%chipSprites.Length};
            }
        }
        Vector3 ChoosePoint(Vector2 desired,int index)
        {
            Vector3 best=Vector3.zero;float bestScore=float.NegativeInfinity;
            for(int z=-1;z<=1;z++)for(int x=-1;x<=1;x++){
                var uv=new Vector2(Mathf.Clamp(desired.x+x*.065f,-.34f,.34f),Mathf.Clamp(desired.y+z*.065f,-.25f,.25f));
                var candidate=cloth.TransformPoint(new Vector3(uv.x,0,uv.y));candidate.y=surface;
                float clearance=depth*.45f;
                foreach(var ball in balls){var delta=candidate-ball.transform.position;delta.y=0;clearance=Mathf.Min(clearance,delta.magnitude-ball.ballRadius);}
                for(int j=0;j<index;j++)clearance=Mathf.Min(clearance,Vector3.Distance(candidate,points[j])*.7f);
                float score=clearance-Vector2.Distance(uv,desired)*depth*.5f;
                if(score>bestScore){bestScore=score;best=candidate;}
            }
            return best;
        }
        void Update(){if(playing)Sample(Time.unscaledTime-started);}
        public void Sample(float time)
        {
            if(!playing||!visuals)return;
            if(time>=Duration){Stop();return;}
            float alpha=1-Mathf.Clamp01((time-2.6f)/.65f);
            for(int i=0;i<3;i++){
                float age=time-ImpactTime(i);var crack=cracks[i];crack.enabled=age>=0;
                if(age>=0){int stage=age<.09f?0:age<.23f?1:2;crack.sprite=crackSprites[stage];
                    crack.transform.SetPositionAndRotation(points[i]+Vector3.up*i*.0003f,Quaternion.Euler(90,cloth.eulerAngles.y+CrackAngles[i],0));
                    float size=depth*1.16f;crack.transform.localScale=Vector3.one*(size/crack.sprite.bounds.size.x);crack.color=new Color(1,1,1,alpha);}
                for(int j=0;j<ChipsPerStrike;j++){
                    int index=i*ChipsPerStrike+j;var motion=fragments[index];float flightAge=age-motion.delay;
                    var chip=chips[index];chip.enabled=flightAge>=0&&flightAge<motion.life;
                    if(!chip.enabled)continue;
                    chip.sprite=chipSprites[motion.sprite];
                    float life=flightAge/motion.life,travel=motion.speed*(1-Mathf.Exp(-1.8f*flightAge))/1.8f;
                    var p=points[i]+motion.offset+motion.direction*travel+Vector3.up*(Mathf.Sin(life*Mathf.PI)*motion.lift+.004f);
                    float tumble=Mathf.Sin(flightAge*13+motion.rotation)*18;
                    chip.transform.SetPositionAndRotation(p,Quaternion.Euler(90+tumble,motion.rotation+flightAge*motion.spin,0));
                    chip.transform.localScale=new Vector3(motion.size,motion.size*motion.aspect,1);
                    float fade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.48f,1,life));
                    chip.color=new Color(motion.shade,motion.shade*.98f,motion.shade*.94f,alpha*fade);
                }
                for(int j=0;j<6;j++){
                    var ray=rays[i*6+j];ray.enabled=age>=0&&age<.16f;if(!ray.enabled)continue;
                    float angle=j*60+i*17,length=depth*.075f*(1+age*3);var direction=Quaternion.Euler(0,angle,0)*Vector3.forward;
                    ray.transform.SetPositionAndRotation(points[i]+direction*length*.5f+Vector3.up*.007f,Quaternion.Euler(90,angle,0));
                    ray.transform.localScale=new Vector3(depth*.007f,length,1);ray.color=new Color(1,.86f,.48f,alpha*(1-age/.16f));
                }
            }
            int strike=Mathf.Clamp(Mathf.FloorToInt((time-StartDelay)/StrikeInterval),0,2);float phase=time-(StartDelay+strike*StrikeInterval);
            hammer.enabled=shadow.enabled=phase>=0&&time<2.2f;
            if(!hammer.enabled){overlay.Sync();return;}
            float swingDirection=strike==0?-1:1;
            float raised=Mathf.SmoothStep(0,1,Mathf.Clamp01(phase/WindupSeconds));
            float slam=Mathf.Clamp01((phase-WindupSeconds)/StrikeSeconds);slam*=slam;
            float hammerAngle=swingDirection*(38+(phase<WindupSeconds?Mathf.Lerp(-20,-80,raised):Mathf.Lerp(-80,12,slam)));
            float lift=depth*.22f*(phase<WindupSeconds?raised:1-slam);
            if(phase>WindupSeconds+StrikeSeconds){float rebound=Mathf.Clamp01((phase-WindupSeconds-StrikeSeconds)/.14f);hammerAngle=swingDirection*(50-25*Mathf.Sin(rebound*Mathf.PI*.5f));lift=depth*.065f*Mathf.Sin(rebound*Mathf.PI*.5f);}
            float exit=Mathf.Clamp01((time-1.98f)/.22f);lift+=exit*depth*.3f;
            var rotation=Quaternion.Euler(0,cloth.eulerAngles.y+hammerAngle,0)*Quaternion.Euler(90,0,0);
            int view=HammerViews[strike];float height=depth*.66f,scale=height/(hammerSprites[view].texture.height/100f);
            hammer.sprite=shadow.sprite=hammerSprites[view];hammer.transform.localScale=shadow.transform.localScale=Vector3.one*scale;
            var contactRotation=Quaternion.Euler(0,cloth.eulerAngles.y+swingDirection*50,0)*Quaternion.Euler(90,0,0);
            Vector3 grip=points[strike]-contactRotation*(Vector3.up*(height*.80f));
            hammer.transform.SetPositionAndRotation(grip+Vector3.up*(lift+.012f),rotation);hammer.color=new Color(1,1,1,alpha*(1-exit));
            grip.y=surface+.001f;shadow.transform.SetPositionAndRotation(grip,rotation);shadow.color=new Color(0,0,0,.2f*alpha*(1-exit));
            overlay.Sync();
        }
        public void Stop()
        {
            playing=false;
            if(visuals)visuals.SetActive(false);
            foreach(var r in cracks)if(r)r.enabled=false;
        }
        void OnDisable(){Stop();}
        void OnDestroy()
        {
            Stop();Release(visuals);foreach(var s in hammerSprites)Release(s);foreach(var s in crackSprites)Release(s);Release(white);foreach(var s in chipSprites)Release(s);
        }
        static void Release(Object value){if(!value)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
