using System;
using System.IO;
using Assets.Scripts.Often;
using Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class ElevatedCuePhysicsValidation
{
    static ElevatedCuePhysicsValidation()
    {
        EditorApplication.update += () => {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (SessionState.GetBool("MasseRestore", false) && !EditorApplication.isPlayingOrWillChangePlaymode) {
                SessionState.SetBool("MasseRestore", false);
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(SessionState.GetString("MasseScene", ""));
            }
            if (!File.Exists("Logs/masse.request")) return;
            if (!EditorApplication.isPlayingOrWillChangePlaymode) {
                var active=SceneManager.GetActiveScene();
                if(active.isDirty || SceneManager.sceneCount!=1) return;
                SessionState.SetString("MasseScene", active.path);
                UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
                EditorApplication.isPlaying=true;
                return;
            }
            if(!EditorApplication.isPlaying) return;
            File.Delete("Logs/masse.request");Run();
            SessionState.SetBool("MasseRestore",true);
            EditorApplication.isPlaying=false;
        };
    }
    sealed class Source : IShotPredictionSource
    {
        public Scene PredictionScene {get;set;}
        public Rigidbody Ball;
        public Rigidbody[] GetPredictionBodies()=>new[]{Ball};
        public ClothPhysicsSettings PredictionCloth {get;}=new ClothPhysicsSettings();
        public bool CalibratedPrediction=>true;
        public bool FollowThroughProfileActive=>true;
        public bool BallImpactTrialActive=>false;
        public float InstantFollowDrawWeight(Vector3 a,Vector3 b)=>1;
        public float PredictionPowerAngle=>0;
        public float PredictionSpinAngle=>0;
        public float PredictionTopspinGain=>0;
    }
    static int checks;
    static void Check(bool value,string why){if(!value)throw new Exception(why);checks++;}
    [MenuItem("Tools/Magic Online/Validate Elevated Cue Physics")]
    public static void Run()
    {
        if(!EditorApplication.isPlaying){File.WriteAllText("Logs/masse.request","run");return;}
        checks=0;var source=new Source();
        try{
            source.PredictionScene=SceneManager.CreateScene("Masse verification",new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            var go=new GameObject("Masse ball",typeof(SphereCollider),typeof(Rigidbody));SceneManager.MoveGameObjectToScene(go,source.PredictionScene);
            go.layer=Mathf.Max(0,LayerMask.NameToLayer("Ball"));
            const float radius=.03f;go.GetComponent<SphereCollider>().radius=radius;
            source.Ball=go.GetComponent<Rigidbody>();source.Ball.mass=.2f;ClothPhysics.ConfigureBody(source.Ball);source.Ball.ResetInertiaTensor();
            var start=new Vector3(0,radius,0);var physics=source.PredictionScene.GetPhysicsScene();
            Vector3 Shot(float elevation,float side,float height=0,float speed=2)
            {
                var b=source.Ball;b.position=start;b.rotation=Quaternion.identity;b.linearVelocity=b.angularVelocity=Vector3.zero;b.WakeUp();
                Physics.SyncTransforms();physics.Simulate(Time.fixedDeltaTime);
                b.position=start;b.rotation=Quaternion.identity;b.linearVelocity=b.angularVelocity=Vector3.zero;b.WakeUp();
                var orientation=Quaternion.Euler(elevation,0,0);
                var direction=ElevatedCuePhysics.Direction(orientation*Vector3.forward);
                var offset=orientation*new Vector3(side*radius,height*radius,0);
                var impulse=new Impulse(start+offset,direction*(b.mass*speed),Vector3.zero,.2f,1);
                Vector3 predicted;int frames;
                using(var preview=new ShotPrediction(source)){
                    preview.Calculate(0,impulse,1f,100);predicted=preview.timedCuePath[preview.timedCuePath.Count-1];frames=preview.timedCuePath.Count-1;
                }
                b.AddForceAtPosition(impulse.impulse,impulse.point,ForceMode.Impulse);
                for(int tick=0;tick<frames;tick++)for(int step=0;step<source.PredictionCloth.substeps;step++){
                    float dt=Time.fixedDeltaTime/source.PredictionCloth.substeps;
                    var v=b.linearVelocity;var spin=b.angularVelocity;
                    FollowThroughProfile.ApplyPersistence(v,ref spin,radius,dt,1,source.PredictionCloth.stopSpeed);
                    ClothPhysics.Step(ref v,ref spin,radius,dt,source.PredictionCloth);b.linearVelocity=v;b.angularVelocity=spin;physics.Simulate(dt);
                }
                Check(Vector3.Distance(predicted,b.position)<.002f,"Guide/live mismatch at "+elevation+": "+Vector3.Distance(predicted,b.position));
                Check(Mathf.Abs(b.position.y-start.y)<.0001f,"Masse became a jump");
                var wire=new Assets.Scripts.Sight.Vital.Pavilion.Impulse(impulse.point,impulse.impulse,Vector3.zero,0,.2f,1);
                var restored=DataManager.ImpulseFromString(DataManager.ImpulseToString(wire));
                Check(Vector3.Distance(restored.impulse,wire.impulse)<.0001f&&Vector3.Distance(restored.point,wire.point)<.0001f,"Wire discarded downward force/contact");
                return b.position-start;
            }
            var flat=Shot(0,.55f);Check(Mathf.Abs(flat.x)<.001f,"Horizontal sidespin starts curving");
            var left=Shot(60,-.55f);var right=Shot(60,.55f);
            Check(left.x<-.03f&&right.x>.03f,"Elevated sidespin did not curve both ways");
            Check(Mathf.Abs(left.x+right.x)<.002f,"Left/right curve symmetry");
            var shallow=Shot(30,.55f);Check(Mathf.Abs(right.x/right.z)>Mathf.Abs(shallow.x/shallow.z),"Steeper cue did not increase curve angle");
            var centre=Shot(60,0);Check(Mathf.Abs(centre.x)<.001f,"Centre hit bends sideways");
            Shot(80,.55f);Shot(60,.55f,0,.8f);Shot(60,.55f,0,4);
            Check(ElevatedCuePhysics.Direction(Vector3.forward)==Vector3.forward,"Zero-angle direction changed");
            var old=JsonUtility.FromJson<MatchShotCommand>("{\"power\":0.5,\"yaw\":0}");Check(old.elevation==0,"Legacy command default");
            File.WriteAllText("Logs/masse-result.txt","PASS "+checks+" checks\n60 degree left/right: "+left+" / "+right+"\n30 degree: "+shallow+"\nflat: "+flat);
        }catch(Exception e){File.WriteAllText("Logs/masse-result.txt","FAIL after "+checks+" checks\n"+e);Debug.LogException(e);}
        finally{if(source.PredictionScene.IsValid())SceneManager.UnloadSceneAsync(source.PredictionScene);}
    }
}
