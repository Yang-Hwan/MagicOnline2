using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{
    // Geometry-only local scene: never simulates the match or invokes its score/item callbacks.
    public sealed class ShotPrediction : IDisposable
    {
        readonly Scene scene;
        readonly Scene resetScene;
        readonly PhysicsScene physics;
        readonly IShotPredictionSource manager;
        readonly Rigidbody[] sourceBodies;
        readonly Rigidbody[] balls;
        readonly float[] radii;
        readonly Vector3[] startPositions;
        readonly bool[] startActive;
        IEnumerator<int> work;
        public bool IsCalculating => work!=null;
        public int CompletedCalculations { get; private set; }
        public float PathDistance { get; private set; }
        public int CompletedSlices { get; private set; }
        public double LastSliceMilliseconds { get; private set; }
        public double MaxSliceMilliseconds { get; private set; }
        public Scene PreviewScene => scene;
        public void Cancel() { work?.Dispose(); work=null; simulatingShot=false; }
        public void Begin(int cue,Impulse impulse,float maxTime=8f,float maxDistance=4.6f,bool firstContactOnly=false,float afterContactLength=1.6f)
        {
            Cancel();PathDistance=0;FreeFlightSteps=0;ContactSolverSteps=0;resolvedContact=false;contactReleaseSteps=0;work=CalculateSteps(cue,impulse,maxTime,maxDistance,firstContactOnly,afterContactLength);
        }
        public bool Advance(double budgetMilliseconds=2,int maxTicks=16)
        {
            if(work==null)return true;
            long start=System.Diagnostics.Stopwatch.GetTimestamp();
            int ticks=0;
            do {
                if(!work.MoveNext()) { Cancel();CompletedCalculations++;break; }
                ticks++;
            }while(ticks<maxTicks && (System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency<budgetMilliseconds);
            LastSliceMilliseconds=(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency;
            MaxSliceMilliseconds=Math.Max(MaxSliceMilliseconds,LastSliceMilliseconds);CompletedSlices++;
            return work==null;
        }
        public readonly List<Vector3> cuePath = new List<Vector3>();
        public readonly List<Vector3> targetPath = new List<Vector3>();
        public readonly List<Vector3> timedCuePath = new List<Vector3>();
        public int targetIndex = -1;
        public Vector3 contactCentre;
        public float contactThickness;
        public Vector3 initialTensor, firstSpin;
        public float targetGuideQueryMilliseconds;
        public bool targetGuideHit;
        public int ActiveBallCount { get { int count=0; foreach(var ball in balls) if(ball.gameObject.activeSelf) count++; return count; } }
        int cueIndex;
        Vector3 impactIncomingVelocity, impactIncomingSpin;
        bool simulatingShot;
        bool resolvedContact;
        int contactReleaseSteps;
        float strokeFollowThrough=-1f;
        float strokeSpinPersistence=-1f;

        public ShotPrediction(IShotPredictionSource source)
        {
            manager = source; sourceBodies = source.GetPredictionBodies();
            resetScene=SceneManager.CreateScene("Shot reset " + Guid.NewGuid().ToString("N"),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            scene = SceneManager.CreateScene("Shot preview " + Guid.NewGuid().ToString("N"), new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            physics = scene.GetPhysicsScene();
            if(physics==Physics.defaultPhysicsScene) throw new InvalidOperationException("Preview did not create a local physics scene");
            foreach (var c in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (c.gameObject.scene != source.PredictionScene || !c.enabled || c.isTrigger || !c.gameObject.activeInHierarchy) continue;
                if (c.gameObject.layer != LayerMask.NameToLayer("Cloth") && c.gameObject.layer != LayerMask.NameToLayer("Board")) continue;
                var go = Make(c.name, c.gameObject.layer);
                go.transform.SetPositionAndRotation(c.transform.position, c.transform.rotation);
                go.transform.localScale = c.transform.lossyScale;
                Collider copy = null;
                if (c is BoxCollider box) { var b=go.AddComponent<BoxCollider>(); b.center=box.center; b.size=box.size; copy=b; }
                else if (c is MeshCollider mesh) { var m=go.AddComponent<MeshCollider>(); m.sharedMesh=mesh.sharedMesh; m.convex=mesh.convex; copy=m; }
                if (copy) { copy.sharedMaterial=c.sharedMaterial; copy.contactOffset=c.contactOffset; }
                SceneManager.MoveGameObjectToScene(go,scene);
                if(copy) { copy.sharedMaterial=c.sharedMaterial; copy.contactOffset=c.contactOffset; }
            }
            balls = new Rigidbody[sourceBodies.Length]; radii = new float[balls.Length];
            startPositions=new Vector3[balls.Length];startActive=new bool[balls.Length];
            for (int i=0;i<balls.Length;i++)
            {
                var original=sourceBodies[i]; var sphere=original.GetComponent<SphereCollider>();
                var go=Make("Preview ball " + i, original.gameObject.layer);
                var c=go.AddComponent<SphereCollider>();
                c.radius=sphere.radius*Mathf.Abs(original.transform.lossyScale.x); radii[i]=c.radius;
                c.sharedMaterial=sphere.sharedMaterial; c.contactOffset=sphere.contactOffset;
                balls[i]=go.AddComponent<Rigidbody>();
                var contact=go.AddComponent<ShotPredictionContact>(); contact.index=i; contact.owner=this;
                SceneManager.MoveGameObjectToScene(go,scene);
            }
        }

        GameObject Make(string name, int layer)
        {
            var go=new GameObject(name);
            // HideFlags can remove/reinsert objects into physics; keep the local scene membership intact.
            go.layer=layer; return go;
        }

        public void Contact(int index, Collision collision)
        {
            if (!simulatingShot || index!=cueIndex)return;
            if(collision.collider.gameObject.layer==LayerMask.NameToLayer("Board")) {
                resolvedContact=true;contactReleaseSteps=Mathf.Clamp(manager.PredictionCloth.substeps,1,8);
                return;
            }
            if(!collision.collider.TryGetComponent<ShotPredictionContact>(out var other))return;
            float instantWeight=manager.FollowThroughProfileActive
                ? manager.InstantFollowDrawWeight(impactIncomingVelocity,balls[other.index].position-balls[index].position) : 1f;
            if (manager.BallImpactTrialActive)
                balls[index].linearVelocity = BallImpactTrial.Resolve(impactIncomingVelocity, balls[index].linearVelocity,
                    impactIncomingSpin, radii[index], balls[index].position-balls[other.index].position,
                    manager.PredictionPowerAngle, manager.PredictionSpinAngle,
                    manager.FollowThroughProfileActive && strokeFollowThrough>=0 ? FollowThroughProfile.ContactInfluence(strokeFollowThrough) : 1f,
                    false,instantWeight);
            if(manager.FollowThroughProfileActive && instantWeight>0)
                FollowThroughProfile.ForwardCarry(balls[index],balls[other.index],strokeFollowThrough*instantWeight,impactIncomingVelocity,radii[index],manager.PredictionTopspinGain);
            if (targetIndex>=0) return;
            targetIndex=other.index; contactCentre=balls[cueIndex].position;
            var normal=(balls[targetIndex].position-contactCentre).normalized;
            var tangent=Vector3.Cross(Vector3.up,normal).normalized;
            contactThickness=1-Mathf.Abs(Vector3.Dot(collision.relativeVelocity.normalized,tangent));
            cuePath.Add(contactCentre); targetPath.Add(balls[targetIndex].position);
        }

        void DrawTargetFirstSegment(Vector3 direction)
        {
            long started=System.Diagnostics.Stopwatch.GetTimestamp();
            Vector3 origin=balls[targetIndex].position;
            float distance=10f;
            targetGuideHit=false;
            if(direction.sqrMagnitude>.000001f)
            {
                // Query only the board in the prediction scene. Never alter the cue body's collider.
                if(physics.SphereCast(origin,radii[targetIndex],direction,out var hit,distance,
                    1<<LayerMask.NameToLayer("Board"),QueryTriggerInteraction.Ignore))
                { distance=hit.distance;targetGuideHit=true; }
                // Other balls are stationary obstacles at their live, pre-shot positions.
                // Ray vs expanded sphere gives the moving target centre at first contact.
                for(int i=0;i<balls.Length;i++)
                {
                    if(i==cueIndex || i==targetIndex || !startActive[i]) continue;
                    Vector3 relative=origin-startPositions[i];
                    float radius=radii[targetIndex]+radii[i];
                    float b=Vector3.Dot(relative,direction), c=relative.sqrMagnitude-radius*radius;
                    float discriminant=b*b-c;
                    if(discriminant<0) continue;
                    float entry=c<=0 ? 0 : -b-Mathf.Sqrt(discriminant);
                    if(entry>=0 && entry<=distance) { distance=entry;targetGuideHit=true; }
                }
            }
            else distance=0;
            targetPath.Clear();targetPath.Add(origin);targetPath.Add(origin+direction*distance);
            targetGuideQueryMilliseconds=(float)((System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000.0/System.Diagnostics.Stopwatch.Frequency);
        }

        // A calibrated free sphere needs no contact solver while comfortably away from obstacles.
        // Keep PhysX for impulses and every near-contact step; cloth integration remains identical.
        bool CanIntegrateFreeFlight(float dt)
        {
            var body=balls[cueIndex];
            if(!manager.CalibratedPrediction || body.useGravity || body.linearDamping!=0 || body.angularDamping!=0 ||
                body.constraints!=RigidbodyConstraints.FreezePositionY || Mathf.Abs(body.linearVelocity.y)>.00001f)return false;
            for(int i=0;i<balls.Length;i++) {
                if(i==cueIndex || !balls[i].gameObject.activeSelf)continue;
                if(balls[i].linearVelocity.sqrMagnitude>.00000001f || balls[i].angularVelocity.sqrMagnitude>.00000001f)return false;
                Vector3 relative=Vector3.ProjectOnPlane(balls[i].position-body.position,Vector3.up);
                Vector3 delta=Vector3.ProjectOnPlane(body.linearVelocity*dt,Vector3.up);
                float t=delta.sqrMagnitude>0 ? Mathf.Clamp01(Vector3.Dot(relative,delta)/delta.sqrMagnitude) : 0;
                float safeRadius=radii[i]+radii[cueIndex]+.01f;
                if((relative-delta*t).sqrMagnitude<safeRadius*safeRadius)return false;
            }
            // Expanded overlap also catches a resting/contacting cushion when speed is near zero.
            if(physics.OverlapSphere(body.position,radii[cueIndex]+.01f,nearbySurfaces,
                1<<LayerMask.NameToLayer("Board"),QueryTriggerInteraction.Ignore)>0)return false;
            Vector3 velocity=body.linearVelocity;
            float speed=velocity.magnitude;
            return speed<.000001f || !physics.SphereCast(body.position,radii[cueIndex],velocity/speed,out _,
                speed*dt+.01f,1<<LayerMask.NameToLayer("Board"),QueryTriggerInteraction.Ignore);
        }
        readonly Collider[] nearbySurfaces=new Collider[1];
        public int FreeFlightSteps { get; private set; }
        public int ContactSolverSteps { get; private set; }
        void IntegrateFreeFlight(float dt)
        {
            var body=balls[cueIndex];body.position+=body.linearVelocity*dt;
            Vector3 spin=body.angularVelocity;float speed=spin.magnitude;
            if(speed>.000001f)body.rotation=Quaternion.AngleAxis(speed*dt*Mathf.Rad2Deg,spin/speed)*body.rotation;
            FreeFlightSteps++;
        }

        public void Calculate(int cue, Impulse impulse, float maxTime=8f, float maxDistance=4.6f, bool firstContactOnly=false, float afterContactLength=1.6f)
        {
            Begin(cue,impulse,maxTime,maxDistance,firstContactOnly,afterContactLength);
            while(!Advance(double.PositiveInfinity,int.MaxValue)) {}
        }

        IEnumerator<int> CalculateSteps(int cue,Impulse impulse,float maxTime,float maxDistance,bool firstContactOnly,float afterContactLength)
        {
            // Disable every actor before restoring any; use the retained reset scene for native state.
            simulatingShot=false;
            foreach(var ball in balls)ball.gameObject.SetActive(false);

            contactCentre=Vector3.zero;contactThickness=0;targetGuideHit=false;
            cueIndex=cue; targetIndex=-1; cuePath.Clear(); targetPath.Clear(); timedCuePath.Clear();
            strokeFollowThrough=impulse.followThrough;
            strokeSpinPersistence=impulse.spinPersistence;
            for(int i=0;i<balls.Length;i++)
            {
                var src=sourceBodies[i]; var dst=balls[i];
                startPositions[i]=src.position;startActive[i]=src.gameObject.activeInHierarchy;
                dst.gameObject.SetActive(false); dst.gameObject.layer=src.gameObject.layer;
                dst.transform.SetPositionAndRotation(src.position,src.rotation);
                dst.position=src.position; dst.rotation=src.rotation; dst.mass=src.mass;
                dst.constraints=src.constraints; dst.useGravity=src.useGravity;
                dst.linearDamping=src.linearDamping; dst.angularDamping=src.angularDamping;
                dst.maxAngularVelocity=src.maxAngularVelocity; dst.maxDepenetrationVelocity=src.maxDepenetrationVelocity;
                dst.solverIterations=src.solverIterations; dst.solverVelocityIterations=src.solverVelocityIterations;
                dst.sleepThreshold=src.sleepThreshold; dst.collisionDetectionMode=src.collisionDetectionMode;
                dst.centerOfMass=src.centerOfMass; dst.inertiaTensor=src.inertiaTensor; dst.inertiaTensorRotation=src.inertiaTensorRotation;
                dst.linearVelocity=src.linearVelocity; dst.angularVelocity=src.angularVelocity; dst.WakeUp();
                dst.gameObject.SetActive(src.gameObject.activeInHierarchy);
                dst.position=src.position;dst.rotation=src.rotation;
                dst.linearVelocity=src.linearVelocity;dst.angularVelocity=src.angularVelocity;dst.WakeUp();
                // Recreate only ball actors in a retained empty scene to discard PhysX cached state.
                // Never insert prediction bodies into the live match scene.
                SceneManager.MoveGameObjectToScene(dst.gameObject,resetScene);
                SceneManager.MoveGameObjectToScene(dst.gameObject,scene);
                dst.constraints=src.constraints;dst.useGravity=src.useGravity;
                dst.linearDamping=src.linearDamping;dst.angularDamping=src.angularDamping;
                dst.maxAngularVelocity=src.maxAngularVelocity;dst.maxDepenetrationVelocity=src.maxDepenetrationVelocity;
                dst.solverIterations=src.solverIterations;dst.solverVelocityIterations=src.solverVelocityIterations;
                dst.sleepThreshold=src.sleepThreshold;dst.collisionDetectionMode=src.collisionDetectionMode;
                var collider=dst.GetComponent<SphereCollider>();var sourceCollider=src.GetComponent<SphereCollider>();
                collider.sharedMaterial=sourceCollider.sharedMaterial;collider.contactOffset=sourceCollider.contactOffset;
                collider.radius=sourceCollider.radius*Mathf.Abs(src.transform.lossyScale.x);radii[i]=collider.radius;
                yield return -i-1; // Reset one actor at a time instead of blocking on the entire rack.
            }
            Physics.SyncTransforms();
            // Establish resting contacts before applying the shot, as on the live settled table.
            simulatingShot=false;
            physics.Simulate(Time.fixedDeltaTime);
            for(int i=0;i<balls.Length;i++) {
                var src=sourceBodies[i]; var dst=balls[i];
                dst.position=src.position;dst.rotation=src.rotation;
                dst.linearVelocity=src.linearVelocity;dst.angularVelocity=src.angularVelocity;dst.WakeUp();
                if(src.IsSleeping()) dst.Sleep();
            }
            targetIndex=-1;cuePath.Clear();targetPath.Clear();
            initialTensor=balls[cue].inertiaTensor;
            // Use the live body's centre for the identical impact moment, independent of
            // the cloned actor's deferred centre-of-mass update after scene transfer.
            balls[cue].AddForce(impulse.impulse,ForceMode.Impulse);
            balls[cue].AddTorque(Vector3.Cross(impulse.point-sourceBodies[cue].worldCenterOfMass,impulse.impulse),ForceMode.Impulse);
            cuePath.Add(balls[cue].position); timedCuePath.Add(balls[cue].position);
            simulatingShot=true;
            yield return 0; // Actor reset/warmup is its own bounded unit.
            int steps=Mathf.Clamp(manager.PredictionCloth.substeps,1,8);
            float dt=Time.fixedDeltaTime/steps, distance=0, still=0;
            float firstContactDistance=-1;
            contactReleaseSteps=0;
            Vector3 previous=balls[cue].position;
            for(int tick=0;tick<Mathf.CeilToInt(maxTime/Time.fixedDeltaTime);tick++)
            {
                for(int step=0;step<steps;step++)
                {
                    for(int i=0;i<balls.Length;i++)
                    {
                        var b=balls[i]; if(!b.gameObject.activeSelf || b.IsSleeping()) continue;
                        var v=b.linearVelocity; var w=b.angularVelocity;
                        if(i==cue && manager.FollowThroughProfileActive && strokeSpinPersistence>=0)
                            FollowThroughProfile.ApplyPersistence(v,ref w,radii[i],dt,strokeSpinPersistence,manager.PredictionCloth.stopSpeed);
                        ClothPhysics.Step(ref v,ref w,radii[i],dt,manager.PredictionCloth); b.linearVelocity=v; b.angularVelocity=w;
                    }
                    impactIncomingVelocity=balls[cue].linearVelocity;
                    impactIncomingSpin=balls[cue].angularVelocity;
                    if(firstContactOnly && resolvedContact && contactReleaseSteps==0 && CanIntegrateFreeFlight(dt))IntegrateFreeFlight(dt);
                    else {physics.Simulate(dt);ContactSolverSteps++;if(contactReleaseSteps>0)contactReleaseSteps--;}
                    if(tick==0 && step==0) firstSpin=balls[cue].angularVelocity;
                    if(firstContactOnly && targetIndex>=0 && firstContactDistance<0)
                    {
                        // Resolve the first impact with the real masses, then predict only the cue ball.
                        // Other balls cannot rebound into the cue path or collide with each other.
                        firstContactDistance=distance+Vector3.Distance(previous,balls[cue].position);
                        Vector3 targetDirection=Vector3.ProjectOnPlane(balls[targetIndex].linearVelocity,Vector3.up).normalized;
                        DrawTargetFirstSegment(targetDirection);
                        for(int i=0;i<balls.Length;i++) if(i!=cue) balls[i].gameObject.SetActive(false);
                        resolvedContact=true;contactReleaseSteps=steps; // Flush removed target contact pairs before numeric free flight.
                    }
                }
                var p=balls[cue].position; distance+=Vector3.Distance(previous,p);PathDistance=distance;previous=p;
                cuePath.Add(p);timedCuePath.Add(p);
                if(targetIndex>=0 && !firstContactOnly) targetPath.Add(balls[targetIndex].position);
                bool settled=true;
                foreach(var b in balls) if(b.gameObject.activeSelf && (b.linearVelocity.magnitude>manager.PredictionCloth.stopSpeed || b.angularVelocity.magnitude>manager.PredictionCloth.stopSpin)) settled=false;
                still=settled ? still+Time.fixedDeltaTime : 0;
                if(distance>=maxDistance || still>=manager.PredictionCloth.stopDelay) break;
                if(firstContactOnly && ((firstContactDistance>=0 && distance-firstContactDistance>=afterContactLength) ||
                    (balls[cue].linearVelocity.magnitude<manager.PredictionCloth.stopSpeed &&
                     Vector3.ProjectOnPlane(balls[cue].angularVelocity,Vector3.up).magnitude*radii[cue]<manager.PredictionCloth.stopSpeed))) break;
                yield return tick+1;
            }
        }

        public void Dispose() {
            Cancel();
            if(!scene.IsValid() || !scene.isLoaded) return;
            foreach(var root in scene.GetRootGameObjects()) root.SetActive(false);
            SceneManager.UnloadSceneAsync(scene);
            if(resetScene.IsValid() && resetScene.isLoaded)SceneManager.UnloadSceneAsync(resetScene);
        }
    }

    public sealed class ShotPredictionContact : MonoBehaviour
    {
        public int index; public ShotPrediction owner;
        void OnCollisionEnter(Collision collision) => owner.Contact(index,collision);
    }
}
