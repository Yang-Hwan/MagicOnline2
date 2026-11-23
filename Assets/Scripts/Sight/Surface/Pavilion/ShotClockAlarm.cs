using System;
using System.Collections.Generic;
using Assets.Scripts.Exert.Network;
using Assets.Scripts.Often;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PracticePhysics=Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.PhysicsMng;
using PracticeShot=Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.ShotCtrl;
using MatchPhysics=Assets.Scripts.Sight.Vital.Pavilion.PhysicsMng;
using MatchShot=Assets.Scripts.Sight.Vital.Pavilion.ShotCtrl;

namespace Assets.Scripts.Sight.Surface.Pavilion
{
    // Reads the existing shot clock only. No score, timeout, turn or input authority is changed.
    public sealed class ShotClockAlarm : MonoBehaviour
    {
        public static ShotClockAlarm MultiplayerInstance {get;private set;}
        readonly Sprite[,] frames=new Sprite[3,8];
        readonly Sprite[] eyeFrames=new Sprite[8];
        [Serializable] sealed class Alignment {public Vector2[] offsets;}
        Vector2[] frameOffsets=new Vector2[24];
        MatchHudView hud;
        bool localPractice,hasAnchor,remoteAnchor,fallback,shotReleased;
        Transform cloth,cueSlider;
        MatchPhysics matchPhysics;
        PracticePhysics practicePhysics;
        MatchShot matchShot;
        PracticeShot practiceShot;
        RectTransform screen,bodyRect,eyesRect;
        Image body,eyes;
        Camera camera;
        ShotClockAlarmSound sound;
        Vector2 anchor;
        int turnSeed,lastStage=-1,lastBell=-1;
        float nextPublish;
        public int Stage {get;private set;}=-1;
        public Canvas OverlayCanvas {get;private set;}
        public Vector2 Anchor=>anchor;
        public Image Body=>body;
        public int BellCount {get;private set;}
        public static ShotClockAlarm Install(MatchHudView owner,bool practice)
        {
            var go=new GameObject("Shot clock alarm overlay",typeof(RectTransform),typeof(Canvas),typeof(ShotClockAlarm));go.layer=owner.gameObject.layer;SceneManager.MoveGameObjectToScene(go,owner.gameObject.scene);
            var alarm=go.GetComponent<ShotClockAlarm>();alarm.hud=owner;alarm.localPractice=practice;alarm.Initialize();owner.BindShotClockAlarm(alarm);return alarm;
        }
        void Initialize()
        {
            screen=(RectTransform)transform;OverlayCanvas=GetComponent<Canvas>();OverlayCanvas.renderMode=RenderMode.ScreenSpaceOverlay;OverlayCanvas.overrideSorting=true;OverlayCanvas.sortingOrder=32766;
            string[] names={"AlarmBored","AlarmAnnoyed","AlarmAngry"};
            var alignment=Resources.Load<TextAsset>("Gameplay/ShotClock/Alignment");if(alignment){var value=JsonUtility.FromJson<Alignment>(alignment.text);if(value?.offsets?.Length==24)frameOffsets=value.offsets;}
            for(int stage=0;stage<3;stage++){
                var texture=Resources.Load<Texture2D>("Gameplay/ShotClock/"+names[stage]);if(!texture)throw new InvalidOperationException("Shot clock artwork missing: "+names[stage]);
                for(int i=0;i<8;i++){
                    int x0=Mathf.RoundToInt(i%4*texture.width/4f),x1=Mathf.RoundToInt((i%4+1)*texture.width/4f),y0=Mathf.RoundToInt((1-i/4)*texture.height/2f),y1=Mathf.RoundToInt((2-i/4)*texture.height/2f);
                    frames[stage,i]=Sprite.Create(texture,new Rect(x0,y0,x1-x0,y1-y0),Vector2.one*.5f,x1-x0,0,SpriteMeshType.FullRect);
                    if(stage==0)eyeFrames[i]=Sprite.Create(texture,new Rect(x0+(x1-x0)*.24f,y0+(y1-y0)*.36f,(x1-x0)*.52f,(y1-y0)*.25f),Vector2.one*.5f,x1-x0,0,SpriteMeshType.FullRect);
                }
            }
            Image Graphic(string name){var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));go.layer=gameObject.layer;go.transform.SetParent(transform,false);var image=go.GetComponent<Image>();image.raycastTarget=false;image.enabled=false;return image;}
            body=Graphic("Shot clock alarm sprite");bodyRect=body.rectTransform;bodyRect.pivot=new Vector2(.5f,0);
            eyes=Graphic("Shot clock bored eyelids");eyesRect=eyes.rectTransform;
            sound=gameObject.AddComponent<ShotClockAlarmSound>();camera=Camera.main;
            foreach(var root in gameObject.scene.GetRootGameObjects()){var table=root.name=="Table"?root.transform:root.transform.Find("Table");if(table){cloth=table.Find("ClothSpace");break;}}
            matchPhysics=FindAnyObjectByType<MatchPhysics>();practicePhysics=FindAnyObjectByType<PracticePhysics>();matchShot=FindAnyObjectByType<MatchShot>();practiceShot=FindAnyObjectByType<PracticeShot>();
            if(matchPhysics)matchPhysics.OnStartShot+=MatchReleased;if(practicePhysics)practicePhysics.OnStartShot+=PracticeReleased;
            if(!localPractice)MultiplayerInstance=this;
        }
        void MatchReleased(string value){shotReleased=true;Hide();}
        void PracticeReleased(float value){shotReleased=true;Hide();}
        public void ResetTurn(){turnSeed++;shotReleased=false;hasAnchor=remoteAnchor=false;nextPublish=0;Hide();}
        public void Hide(){Stage=-1;lastStage=lastBell=-1;if(body)body.enabled=false;if(eyes)eyes.enabled=false;if(sound)sound.Silence();}
        public void SetView(Camera view,Transform table){camera=view;cloth=table;}
        public void ReceiveAnchor(Vector2 value,bool useFallback)
        {
            if(localPractice||!hud||shotReleased)return;
            anchor=value;fallback=useFallback;hasAnchor=remoteAnchor=true;
        }
        void LateUpdate(){if(!hud){Destroy(gameObject);return;}if((matchPhysics&&matchPhysics.inMove)||(practicePhysics&&practicePhysics.inMove)){Hide();return;}Present(hud.RemainingSeconds,hud.TimerRunning,localPractice||hud.ActiveSide==0);}
        public static int StageFor(float seconds,bool running)=>!running||float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<=0||seconds>10?-1:seconds<=4?2:seconds<=8?1:0;
        public void Present(float seconds,bool running,bool self)
        {
            int stage=StageFor(seconds,running);
            if(stage<0||shotReleased||!isActiveAndEnabled||!hud||!hud.isActiveAndEnabled){Hide();return;}
            if(!camera)camera=Camera.main;if(!cloth||!camera){Hide();return;}
            PunNetwork pun=null;if(!localPractice&&NetworkManager.TryGetExistingNetwork(out var network))pun=network as PunNetwork;
            bool online=pun&&pun.HasLegacyMatch;
            if(!hasAnchor){
                Collect(out var positions,out var radii,out var cue,out var aim);
                int seed=online?pun.ShotClockPlacementSeed:turnSeed*7919;
                // A delayed placement RPC must never suppress the opponent's warning.
                // Use a safe local position until the authoritative placement arrives.
                if(online&&!self)aim=Vector3.zero;
                fallback=!TryChoose(cloth,positions,radii,cue,aim,seed,out anchor);hasAnchor=true;
            }
            if(online&&self&&Time.unscaledTime>=nextPublish){pun.PublishShotClockAnchor(anchor,fallback);nextPublish=Time.unscaledTime+1;}
            Stage=stage;
            // Keep the chosen location fixed; temporarily hide if a later aim sweep crosses it.
            Transform slider=matchShot?matchShot.cueSlider:practiceShot?practiceShot.cueSlider:null;
            Transform cueBall=matchShot&&matchShot.cueBall?matchShot.cueBall.transform:practiceShot&&practiceShot.cueBall?practiceShot.cueBall.transform:null;
            if(self&&!fallback&&slider&&cueBall){
                float d=cloth.TransformVector(Vector3.forward).magnitude;var forward=Vector3.ProjectOnPlane(slider.forward,Vector3.up).normalized;
                var from=cueBall.position-forward*d*.8f;var delta=forward*d*3.3f;var world=cloth.TransformPoint(new Vector3(anchor.x,0,anchor.y));
                if(delta.sqrMagnitude>.01f){var diff=world-(from+delta*Mathf.Clamp01(Vector3.Dot(world-from,delta)/delta.sqrMagnitude));diff.y=0;if(diff.magnitude<d*.14f){body.enabled=eyes.enabled=false;sound.Silence();return;}}
            }
            float phase=Mathf.Max(0,10-seconds),size=Mathf.Clamp(screen.rect.height*.18f,96,164);
            if(fallback)size=72;
            var extent=screen.rect.size;Vector2 point;
            if(fallback)point=new Vector2(0,extent.y*.5f-90);
            else{var world=cloth.TransformPoint(new Vector3(anchor.x,0,anchor.y));var p=camera.WorldToViewportPoint(world);point=Vector2.Scale(new Vector2(p.x-.5f,p.y-.5f),extent);}
            float stagePhase=stage==0?phase:Mathf.Max(0,phase-(stage==1?2:6));
            int frame=stage==0?(int)(phase*3)%8:(int)(stagePhase*(stage==1?10:16))%8;
            body.enabled=true;body.sprite=frames[stage,stage==0?0:frame];
            float wiggle=stage==0?0:Mathf.Sin(stagePhase*(stage==1?43:72))*(stage==1?.75f:1.6f);
            float squash=stage==0?0:Mathf.Sin(stagePhase*(stage==1?35:57))*(stage==1?.006f:.013f);
            bodyRect.anchoredPosition=point+new Vector2(wiggle,-size*.42f)+frameOffsets[stage*8+(stage==0?0:frame)]*size;bodyRect.sizeDelta=Vector2.one*size;bodyRect.localScale=new Vector3(1+squash,1-squash,1);bodyRect.localRotation=Quaternion.identity;
            eyes.enabled=stage==0;eyes.sprite=eyeFrames[frame];eyesRect.anchoredPosition=point+new Vector2(0,size*.065f)+frameOffsets[frame]*size;eyesRect.sizeDelta=new Vector2(size*.52f,size*.25f);
            // Bored keeps the entire body fixed; only its cropped eyelids change.
            float interval=stage==0?20:stage==1?1.4f:.55f;int bell=(int)(stagePhase/interval);
            if(lastStage!=stage||bell>lastBell){lastStage=stage;lastBell=bell;BellCount++;float volume=self?.26f:.09f;sound.Bell(stage,volume);MatchReplayRecorder.RecordShotClockBell(gameObject.scene,stage,volume);}
        }
        void Collect(out Vector3[] positions,out float[] radii,out Vector3 cue,out Vector3 aim)
        {
            var points=new List<Vector3>(4);var sizes=new List<float>(4);cue=cloth.position;aim=Vector3.zero;
            if(matchPhysics&&matchPhysics.balls!=null)foreach(var b in matchPhysics.balls)if(b&&b.gameObject.activeInHierarchy){points.Add(b.transform.position);sizes.Add(b.ballRadius);}
            if(practicePhysics&&practicePhysics.ballcs!=null)foreach(var b in practicePhysics.ballcs)if(b&&b.gameObject.activeInHierarchy){points.Add(b.transform.position);sizes.Add(b.ballRadius);}
            if(matchShot&&matchShot.cueBall&&matchShot.cueSlider){cue=matchShot.cueBall.transform.position;aim=matchShot.cueSlider.forward;}
            if(practiceShot&&practiceShot.cueBall&&practiceShot.cueSlider){cue=practiceShot.cueBall.transform.position;aim=practiceShot.cueSlider.forward;}
            positions=points.ToArray();radii=sizes.ToArray();
        }
        public static bool TryChoose(Transform table,Vector3[] balls,float[] radii,Vector3 cue,Vector3 aim,int seed,out Vector2 result)
        {
            var random=new System.Random(seed);float depth=table.TransformVector(Vector3.forward).magnitude,clearance=depth*.14f;aim=Vector3.ProjectOnPlane(aim,Vector3.up).normalized;
            for(int i=0;i<96;i++){
                var p=new Vector2((float)random.NextDouble()*.68f-.34f,(float)random.NextDouble()*.54f-.27f);var world=table.TransformPoint(new Vector3(p.x,0,p.y));bool safe=true;
                for(int j=0;j<balls.Length;j++){var d=world-balls[j];d.y=0;if(d.magnitude<clearance+radii[j]){safe=false;break;}}
                if(!safe)continue;
                if(aim.sqrMagnitude>.5f){var from=cue-aim*depth*.8f;var delta=aim*depth*3.3f;var nearest=from+delta*Mathf.Clamp01(Vector3.Dot(world-from,delta)/delta.sqrMagnitude);var d=world-nearest;d.y=0;if(d.magnitude<clearance)continue;}
                result=p;return true;
            }
            result=Vector2.zero;return false;
        }
        void OnDisable(){Hide();hasAnchor=remoteAnchor=false;}
        void OnDestroy(){if(MultiplayerInstance==this)MultiplayerInstance=null;if(matchPhysics)matchPhysics.OnStartShot-=MatchReleased;if(practicePhysics)practicePhysics.OnStartShot-=PracticeReleased;foreach(var sprite in frames)Release(sprite);foreach(var sprite in eyeFrames)Release(sprite);}
        static void Release(UnityEngine.Object value){if(!value)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
