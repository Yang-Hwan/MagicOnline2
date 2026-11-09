using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Assets.Scripts.Often;
using Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital;
using Assets.TutorialInfo.Scripts.TableSet06.Often;
using Coach = Assets.TutorialInfo.Scripts.TableSet06.Excert.Match.PoolCoach;
using Logic = Assets.TutorialInfo.Scripts.TableSet06.Excert.Match.PoolLogic;
using Player = Assets.TutorialInfo.Scripts.TableSet06.Excert.Match.PoolPlayer;
using RunPath = Assets.TutorialInfo.Scripts.TableSet06.Often.RunPath;
using HitKind = Assets.TutorialInfo.Scripts.TableSet06.Often.HitKind;

namespace Assets.Scripts.Sight.Surface.Pavilion
{
    // Presentation only: never changes physics, score, turn, or network state.
    public sealed class PracticeSentimentController : MonoBehaviour
    {
        public event Action<int, Vector3> Emotion;
        public event Action ShotStarted;
        public PracticeSentimentActor[] Actors { get; private set; }
        readonly List<PracticeSentimentActor>[] groups = new List<PracticeSentimentActor>[9];
        readonly int[] previous = new int[9];
        readonly List<PracticeSentimentActor>[] animationGroups = new List<PracticeSentimentActor>[9];
        readonly int[] previousAnimation = new int[9];
        readonly bool[] shown = new bool[9];
        readonly bool[] contacts = new bool[16];
        readonly Vector3[] startingPositions = new Vector3[16];
        readonly RaycastHit[] hits = new RaycastHit[16];
        PhysicsMng physics;
        Coach coach;
        Camera worldCamera;
        BallC cue, target;
        RawImage image;
        RectTransform canvasRect, imageRect;
        Canvas canvas;
        Transform clothSpace;
        readonly Vector2[] tableCorners = new Vector2[4];
        readonly Vector2[] ballPoints = new Vector2[16];
        readonly Vector2[] ballAhead = new Vector2[16];
        readonly float[] ballClearance = new float[16];
        int visibleBalls;
        Vector2 reservedSize;
        float nextSafetyCheck, earlyFade;
        PracticeSentimentActor active;
        Vector2 origin;
        float elapsed, nextSample, nearest;
        bool running, resolved, kissed, passed, approaching, subscribed;
        public bool UseLegacy { get; private set; }
        const float SlowSpeed = .7f;
        static readonly string[] EmotionNames = { "구감정", "분노", "격려", "실망", "불만", "좌절", "기쁨", "감사", "안도" };
        readonly System.Text.StringBuilder emotionHistory = new System.Text.StringBuilder(96);
        int shotNumber;
        bool fourBall;
        float foulContactSpeed = -1;

        public void Initialize(Transform actorRoot)
        {
            var config = PracticeSceneFlow.Configuration;
            if (config.Execution != MatchExecutionMode.LocalPractice || (config.BallCount != 3 && config.BallCount != 4)) { enabled = false; return; }
            fourBall=config.BallCount==4;
            Actors = actorRoot.GetComponentsInChildren<PracticeSentimentActor>(true);
            // ClothSpace is authored as the full playable width/depth, excluding the cushions.
            clothSpace=actorRoot.parent.parent.Find("ClothSpace");
            Array.Sort(Actors, (a,b) => string.CompareOrdinal(a.name,b.name));
            for (int i=0;i<9;i++) {
                groups[i]=new List<PracticeSentimentActor>(); animationGroups[i]=new List<PracticeSentimentActor>();
                previous[i]=previousAnimation[i]=-1;
            }
            foreach (var actor in Actors) {
                actor.PrepareAnimation();
                if (actor.category>=1 && actor.category<=8)
                    (actor.animation ? animationGroups[actor.category] : groups[actor.category]).Add(actor);
                actor.gameObject.SetActive(false);
            }
            canvas = GetComponentInParent<Canvas>().rootCanvas;
            canvasRect = (RectTransform)canvas.transform;
            var go = new GameObject("PracticeSentimentDisplay",typeof(RectTransform),typeof(RawImage));
            go.layer=canvas.gameObject.layer;
            go.transform.SetParent(canvas.transform,false);
            image=go.GetComponent<RawImage>(); image.raycastTarget=false; image.enabled=false;
            imageRect=(RectTransform)go.transform;
            imageRect.anchorMin=imageRect.anchorMax=new Vector2(.5f,.5f);
            physics=FindFirstObjectByType<PhysicsMng>(); coach=Coach.Instance; worldCamera=Camera.main;
            if (!physics) return;
            physics.OnStartShot+=StartShot;
            physics.OnPracticeSentimentContact+=Contact;
            physics.OnBallHitBall+=AfterContactAdjudication;
            coach.OnScoreChanged+=Scored;
            coach.OnPracticeShotResult+=Finished;
            subscribed=true;
        }
        public void SetLegacy(bool value) { UseLegacy=value; Hide(); }
        public Vector3 PreviewPosition {
            get {
                if(physics) foreach(var ball in physics.ballcs)
                    if(ball && ball.id==Player.turnId)return ball.transform.position;
                return Vector3.zero;
            }
        }
        void StartShot(float angle)
        {
            shotNumber++;emotionHistory.Clear();
            ShotStarted?.Invoke();
            Hide(); Array.Clear(shown,0,shown.Length); Array.Clear(contacts,0,contacts.Length);
            cue=null; target=null;
            foreach(var ball in physics.ballcs) if(ball) {
                if(ball.id>=0 && ball.id<startingPositions.Length) startingPositions[ball.id]=ball.transform.position;
                if(ball.id==Player.turnId) cue=ball;
            }
            running=cue!=null; resolved=kissed=passed=approaching=false;
            nearest=float.MaxValue; nextSample=0;
            foulContactSpeed=-1;
        }
        void Contact(BallC ball,BallC other)
        {
            if(!running || resolved || !cue) return;
            BallC touched=ball==cue ? other : other==cue ? ball : null;
            if(!touched || touched.id<0 || touched.id>=contacts.Length) return;
            if(fourBall && touched.id==1-cue.id) {
                // Preserve impact speed; the ball may slow sharply before the official contact result.
                foulContactSpeed=cue.impactIncomingVelocity.sqrMagnitude>0
                    ? cue.impactIncomingVelocity.magnitude : cue.body.linearVelocity.magnitude;
                return; // The opponent's cue is a foul, never a kiss or scoring target.
            }
            if(fourBall && touched.id!=2 && touched.id!=3)return;
            // Once both reds are hit, later harmless kisses cannot spoil the success emotion.
            // Contact observation runs before collision-exit adjudication, so also use observed hits.
            // Keep the opponent-cue check above active until the shot's final result.
            if(fourBall && (Logic.gameState.ballHitBall_2>=0 || (contacts[2] && contacts[3])))return;
            // A repeat contact with an already contacted object is a kiss, not the first valid hit.
            if(contacts[touched.id] || (touched.transform.position-startingPositions[touched.id]).sqrMagnitude>.03f*.03f) {
                kissed=true;
                if(cue.body.linearVelocity.sqrMagnitude<=SlowSpeed*SlowSpeed) Emit(4);
            }
            contacts[touched.id]=true;
        }
        void Scored(int player,bool success,int reward,RunPath path)
        {
            if(!running || resolved || !success || fourBall) return;
            Resolve(true,false);
        }
        void AfterContactAdjudication(BallC ball,BallC other,bool inMove)
        {
            if(!running || resolved || !inMove || ball!=cue)return;
            // PoolCoach subscribed first and has already adjudicated this contact.
            var state=Logic.gameState;
            if(!fourBall) {
                // A premature final object hit is anger even at low speed. Presentation only;
                // do not turn a three-ball miss into a score-deducting four-ball foul.
                if(other && other!=cue && !state.shotAchieve && (int)coach.matchCushion==3 &&
                    state.ballHitBall_1>=0 && other.id!=state.ballHitBall_1 &&
                    state.ballsHitBoardCount>=1 && state.ballsHitBoardCount<=2) {
                    resolved=true;
                    Emit(1);
                }
                return;
            }
            if(state.hitKinds.Contains(HitKind.Ball_Foul))Resolve(false,true);
        }
        void Finished(bool success,bool foul,int cushions)
        {
            if(!running) return;
            if(!resolved) Resolve(success && !foul,foul);
            LogShotHistory();
            running=false;
        }
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public void LogDisplayed(int category,string detail,bool preview=false)
        {
            string label=category>=0 && category<EmotionNames.Length ? EmotionNames[category] : "알 수 없음";
            if(!preview && running) {
                if(emotionHistory.Length>0)emotionHistory.Append(" > ");
                emotionHistory.Append(label);
            }
            Debug.Log($"[감정{(preview ? " 미리보기" : "")}] {label} · {detail}",this);
        }
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        void LogShotHistory()
        {
            Debug.Log($"[감정 흐름 · {shotNumber}번째 샷 종료] {(emotionHistory.Length>0 ? emotionHistory.ToString() : "표현 없음")}",this);
        }
        public static int SuccessCategory(bool hasKiss,bool hasPassed) => hasKiss ? 7 : hasPassed ? 8 : 6;
        void Resolve(bool success,bool foul)
        {
            if(resolved) return;
            resolved=true;
            float failureSpeed=foul && foulContactSpeed>=0 ? foulContactSpeed : cue ? cue.body.linearVelocity.magnitude : 0;
            int category=success ? SuccessCategory(kissed,passed) : foul && failureSpeed>SlowSpeed ? 1 : 5;
            Emit(category);
        }
        void Update()
        {
            Animate();
            if(!running || resolved || !cue || Time.unscaledTime<nextSample) return;
            nextSample=Time.unscaledTime+.1f;
            var state=Logic.gameState;
            if(state.hitKinds.Contains(HitKind.Ball_Foul)) { Resolve(false,true);return; }
            if(state.shotAchieve && !fourBall) { Resolve(true,false); return; }
            if(state.shotFailed) {
                // Three-cushion shortfall is a miss, not a four-ball foul.
                bool foul=state.hitKinds.Contains(HitKind.Ball_Foul);
                if(foul || cue.body.linearVelocity.sqrMagnitude<=SlowSpeed*SlowSpeed) Resolve(false,foul);
                return;
            }
            // Both reds have been contacted, but a later opponent-cue contact can still invalidate it.
            if(fourBall && state.ballHitBall_2>=0)return;
            // Only track a near miss after the three-ball cushion requirement is met.
            // Discard earlier approaches so a pre-cushion pass cannot trigger disappointment later.
            if(!fourBall && state.ballsHitBoardCount<(int)coach.matchCushion) {
                nearest=float.MaxValue;
                approaching=false;
                return;
            }
            if(cue.body.linearVelocity.sqrMagnitude>SlowSpeed*SlowSpeed || state.ballHitBall_1<0) return;
            if(!target) foreach(var ball in physics.ballcs)
                if(ball && ball.gameObject.activeInHierarchy && ball!=cue && ball.id!=state.ballHitBall_1 &&
                    (!fourBall || ball.id==2 || ball.id==3)) { target=ball; break; }
            if(!target) return;
            Vector3 delta=Vector3.ProjectOnPlane(target.transform.position-cue.transform.position,Vector3.up);
            Vector3 velocity=Vector3.ProjectOnPlane(cue.body.linearVelocity,Vector3.up);
            float distance=delta.magnitude;
            if(distance>1.6f || velocity.sqrMagnitude<.0001f) return;
            float along=Vector3.Dot(delta,velocity.normalized);
            if(along>0) {
                nearest=Mathf.Min(nearest,distance);
                float across=(delta-velocity.normalized*along).magnitude;
                float contactRadius=cue.ballRadius+target.ballRadius;
                // Four-ball has no cushion minimum; three-ball retains its configured minimum.
                if(across<contactRadius*1.25f && (fourBall || state.ballsHitBoardCount>=(int)coach.matchCushion) && DirectTarget(velocity.normalized,distance)) {
                    approaching=true; Emit(2);
                }
            } else if((approaching || nearest<.35f) && distance<.65f) {
                passed=true; Emit(3);
            }
        }
        bool DirectTarget(Vector3 direction,float distance)
        {
            int mask=cue.boardLayer|cue.ballLayer|cue.cueBallLayer;
            int count=Physics.SphereCastNonAlloc(cue.transform.position,Mathf.Max(.01f,cue.ballRadius),direction,hits,distance,mask,QueryTriggerInteraction.Ignore);
            float best=float.MaxValue; BallC first=null;
            for(int i=0;i<count;i++) {
                var ball=hits[i].collider.GetComponentInParent<BallC>();
                if(ball==cue || hits[i].distance>=best) continue;
                best=hits[i].distance; first=ball;
            }
            return count<hits.Length && first==target;
        }
        void Emit(int category)
        {
            if(shown[category]) return;
            shown[category]=true;
            Vector3 position=cue ? cue.transform.position : Vector3.zero;
            Emotion?.Invoke(category,position);
            if(UseLegacy) return;
            bool animated=animationGroups[category].Count>0 &&
                (groups[category].Count==0 || UnityEngine.Random.value<.5f);
            var choices=animated ? animationGroups[category] : groups[category];
            var last=animated ? previousAnimation : previous;
            int count=choices.Count;
            if(count==0)return;
            int index=UnityEngine.Random.Range(0,count);
            if(count>1 && index==last[category]) index=(index+UnityEngine.Random.Range(1,count))%count;
            last[category]=index;
            Play(choices[index],position);
        }
        public void Play(PracticeSentimentActor actor,Vector3 position,bool preview=false)
        {
            Hide(); if(!actor) return;
            if(actor.animation) {
                active=actor;elapsed=0;
                actor.PlayAnimation(position);
                LogDisplayed(actor.category,$"{actor.name} · {actor.phrase}",preview);
                return;
            }
            if(!actor.artwork || !image || !worldCamera) return;
            float ratio=actor.artwork.height*actor.uv.height/(actor.artwork.width*actor.uv.width);
            if(!ProjectTable() || !Project(position,out var source)) return;
            CollectBallClearance();
            Vector2 direction=Vector2.zero;
            if(cue && cue.body && Project(position+cue.body.linearVelocity*.35f,out var ahead)) direction=(ahead-source).normalized;
            bool found=false;
            Vector2 size=Vector2.zero;
            for(int attempt=0;attempt<3;attempt++) {
                size=new Vector2(actor.width,actor.width*ratio)*(1-attempt*.15f);
                // Reserve the peak pop (1.14), horizontal shake and the full upward exit drift.
                reservedSize=size*1.14f+new Vector2(8,34);
                if(TryPlace(tableCorners,canvasRect.rect,source,direction,reservedSize,
                    ballPoints,ballAhead,ballClearance,visibleBalls,out origin)) { found=true;break; }
            }
            // With no safe location, omit this cosmetic reaction rather than cover a ball.
            if(!found)return;
            active=actor; active.gameObject.SetActive(true);
            image.texture=actor.artwork; image.uvRect=actor.uv;
            imageRect.sizeDelta=size;imageRect.anchoredPosition=origin;imageRect.localScale=Vector3.one;
            image.color=Color.white;
            elapsed=0;earlyFade=-1;nextSafetyCheck=0;image.enabled=true;
            LogDisplayed(actor.category,$"{actor.name} · {actor.phrase}",preview);
        }
        bool Project(Vector3 world,out Vector2 point)
        {
            Vector3 screen=worldCamera.WorldToScreenPoint(world);
            point=Vector2.zero;
            return screen.z>0 && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,screen,
                canvas.renderMode==RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,out point);
        }
        bool ProjectTable()
        {
            if(!clothSpace)return false;
            for(int i=0;i<4;i++) {
                var local=new Vector3(i==0||i==3 ? -.5f : .5f,0,i<2 ? -.5f : .5f);
                if(!Project(clothSpace.TransformPoint(local),out tableCorners[i]))return false;
            }
            return true;
        }
        void CollectBallClearance()
        {
            visibleBalls=0;
            if(!physics || physics.ballcs==null)return;
            foreach(var ball in physics.ballcs) {
                if(!ball || !ball.gameObject.activeInHierarchy || visibleBalls==ballPoints.Length)continue;
                var world=ball.transform.position;
                if(!Project(world,out var point))continue;
                float radius=8;
                if(Project(world+worldCamera.transform.right*ball.ballRadius,out var edge))radius=Mathf.Max(radius,Vector2.Distance(point,edge));
                if(Project(world+worldCamera.transform.up*ball.ballRadius,out edge))radius=Mathf.Max(radius,Vector2.Distance(point,edge));
                ballPoints[visibleBalls]=point;
                ballAhead[visibleBalls]=point;
                if(ball.body && Project(world+ball.body.linearVelocity*.35f,out var ahead))ballAhead[visibleBalls]=ahead;
                ballClearance[visibleBalls]=radius+(ball==cue ? 38 : 24);
                visibleBalls++;
            }
        }
        // Allocation-free bounded search: 8 nearby candidates + 35 table-grid points at each scale.
        public static bool TryPlace(Vector2[] table,Rect viewport,Vector2 source,Vector2 travel,Vector2 size,
            Vector2[] balls,Vector2[] ahead,float[] radii,int count,out Vector2 result)
        {
            result=Vector2.zero;float best=float.PositiveInfinity;
            Vector2 min=table[0],max=table[0];
            for(int i=1;i<4;i++){min=Vector2.Min(min,table[i]);max=Vector2.Max(max,table[i]);}
            for(int i=0;i<43;i++) {
                Vector2 candidate;
                if(i<8) {
                    float angle=i*Mathf.PI/4;
                    candidate=source+new Vector2(Mathf.Cos(angle)*(size.x*.5f+80),Mathf.Sin(angle)*(size.y*.5f+80));
                } else {
                    int cell=i-8;
                    candidate=new Vector2(Mathf.Lerp(min.x,max.x,(cell%7+.5f)/7),Mathf.Lerp(min.y,max.y,(cell/7+.5f)/5));
                }
                var box=new Rect(candidate-size*.5f,size);
                if(!FitsTable(table,viewport,box))continue;
                if(IntersectsBalls(box,balls,ahead,radii,count))continue;
                Vector2 offset=candidate-source;
                // Prefer nearby empty space behind the cue's current travel direction.
                float score=offset.magnitude+Mathf.Max(0,Vector2.Dot(offset.normalized,travel))*220;
                if(score<best){best=score;result=candidate;}
            }
            return !float.IsPositiveInfinity(best);
        }
        public static bool FitsTable(Vector2[] table,Rect viewport,Rect box)
        {
            for(int i=0;i<4;i++) {
                Vector2 point=new Vector2(i==0||i==3?box.xMin:box.xMax,i<2?box.yMin:box.yMax);
                if(!viewport.Contains(point))return false;
                float sign=0;
                for(int j=0;j<4;j++) {
                    Vector2 edge=table[(j+1)%4]-table[j],delta=point-table[j];
                    float cross=edge.x*delta.y-edge.y*delta.x;
                    // Eight canvas units of inset from the projected cushion boundary.
                    if(Mathf.Abs(cross)<8*edge.magnitude)return false;
                    if(j==0)sign=Mathf.Sign(cross);
                    else if(sign*cross<=0)return false;
                }
            }
            return true;
        }
        public static bool IntersectsBalls(Rect box,Vector2[] balls,Vector2[] ahead,float[] radii,int count)
        {
            for(int i=0;i<count;i++) {
                // Expand the box by the ball's radius and spacing, then test its short travel segment.
                var area=Rect.MinMaxRect(box.xMin-radii[i],box.yMin-radii[i],box.xMax+radii[i],box.yMax+radii[i]);
                Vector2 from=balls[i],delta=ahead[i]-from;
                float enter=0,exit=1;
                if(ClipAxis(from.x,delta.x,area.xMin,area.xMax,ref enter,ref exit) &&
                   ClipAxis(from.y,delta.y,area.yMin,area.yMax,ref enter,ref exit))return true;
            }
            return false;
        }
        static bool ClipAxis(float start,float delta,float min,float max,ref float enter,ref float exit)
        {
            if(Mathf.Abs(delta)<.00001f)return start>=min&&start<=max;
            float a=(min-start)/delta,b=(max-start)/delta;
            if(a>b){float temp=a;a=b;b=temp;}
            enter=Mathf.Max(enter,a);exit=Mathf.Min(exit,b);return enter<=exit;
        }
        void Animate()
        {
            if(!active || !image) return;
            elapsed+=Time.unscaledDeltaTime;
            if(active.animation) {
                if(active.AnimationFinished(elapsed))Hide();
                return;
            }
            if(earlyFade<0 && Time.unscaledTime>=nextSafetyCheck) {
                nextSafetyCheck=Time.unscaledTime+.05f;
                CollectBallClearance();
                var box=new Rect(origin-reservedSize*.5f,reservedSize);
                if(!ProjectTable() || !FitsTable(tableCorners,canvasRect.rect,box) ||
                    IntersectsBalls(box,ballPoints,ballAhead,ballClearance,visibleBalls))earlyFade=0;
            }
            if(earlyFade>=0) {
                earlyFade+=Time.unscaledDeltaTime;
                if(earlyFade>=.18f){Hide();return;}
            }
            float t=elapsed/active.duration;
            if(t>=1) { Hide(); return; }
            float pop=elapsed<.22f ? 1+.14f*Mathf.Sin(elapsed/.22f*Mathf.PI) : 1;
            imageRect.localScale=Vector3.one*pop;
            float shake=active.category==1 || active.category==4 ? Mathf.Sin(elapsed*45)*3*(1-t) : 0;
            imageRect.anchoredPosition=origin+new Vector2(shake,Mathf.Max(0,t-.65f)*45);
            float alpha=1-Mathf.Clamp01((t-.7f)/.3f);
            if(earlyFade>=0)alpha=Mathf.Min(alpha,1-earlyFade/.18f);
            image.color=new Color(1,1,1,alpha);
        }
        public void Hide() {
            if(active) { active.StopAnimation(); active.gameObject.SetActive(false); }
            active=null; if(image)image.enabled=false;
        }
        void OnDisable() { running=false; Hide(); }
        void OnDestroy()
        {
            if(subscribed) {
                if(physics) { physics.OnStartShot-=StartShot; physics.OnPracticeSentimentContact-=Contact; physics.OnBallHitBall-=AfterContactAdjudication; }
                coach.OnScoreChanged-=Scored; coach.OnPracticeShotResult-=Finished;
            }
            if(image) Destroy(image.gameObject);
        }
    }
}
