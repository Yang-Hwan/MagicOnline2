using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital;
using MatchShot = Assets.Scripts.Sight.Vital.Pavilion.ShotCtrl;
using MatchBall = Assets.Scripts.Sight.Vital.Pavilion.Ball;

public static class MultiplayerGuidePowerValidation
{
    sealed class Source : IShotPredictionSource
    {
        public Rigidbody Body;
        public Scene PredictionScene => Body.gameObject.scene;
        public Rigidbody[] GetPredictionBodies() => new[]{Body};
        public ClothPhysicsSettings PredictionCloth { get; } = new ClothPhysicsSettings();
        public bool CalibratedPrediction => true;
        public bool FollowThroughProfileActive => true;
        public bool BallImpactTrialActive => true;
        public float InstantFollowDrawWeight(Vector3 a,Vector3 b) => 0;
        public float PredictionPowerAngle => 12;
        public float PredictionSpinAngle => 8;
        public float PredictionTopspinGain => .3f;
    }
    static void Set(object target,string name,object value) => target.GetType().GetProperty(name).GetSetMethod(true).Invoke(target,new[]{value});
    public static void Run()
    {
        var root=new GameObject("Guide power regression"); root.SetActive(false);
        var ballObject=new GameObject("Source ball",typeof(SphereCollider),typeof(Rigidbody));
        var source=new Source {Body=ballObject.GetComponent<Rigidbody>()};
        source.Body.useGravity=false;source.Body.mass=.15f;
        ShotPrediction prediction=null;
        try
        {
            var shot=root.AddComponent<MatchShot>();
            var ball=root.AddComponent<MatchBall>();
            root.AddComponent<SphereCollider>();Set(ball,"body",source.Body);Set(shot,"cueBall",ball);
            shot.cueSlider=root.transform;shot.cueDisplacement=root.transform;shot.cueVertical=root.transform;
            shot.SetFollowThrough(.2f);
            if(Application.isPlaying) prediction=new ShotPrediction(source);
            // Reproduce the previous zero-power simulation: many coincident points,
            // but no drawable distance. Publishing this removes the quick guide.
            Set(shot,"force",0f);
            var real=shot.BuildShotImpulse();
            if(real.impulse.sqrMagnitude!=0)throw new Exception("Zero-power shot must remain zero");
            if(prediction!=null)
            {
                prediction.Begin(0,new Impulse(Vector3.zero,Vector3.zero,Vector3.zero),8,4.6f,true,1.6f);
                while(!prediction.Advance(double.PositiveInfinity,10000)){}
                if(prediction.PathDistance>=.0001f)throw new Exception("Zero-power reproduction failed");
            }
            foreach(float power in new[]{0f,.1f,.5f,1f,0f})
            {
                Set(shot,"force",power);
                var guide=shot.BuildGuideImpulse();
                if(guide.impulse.sqrMagnitude<=0)throw new Exception("Guide impulse disappeared after power reset");
                if(power>0 && guide.impulse!=shot.BuildShotImpulse().impulse)throw new Exception("Selected shot power changed");
                if(prediction!=null)
                {
                    prediction.Begin(0,guide,8,4.6f,true,1.6f);
                    while(!prediction.Advance(double.PositiveInfinity,10000)){}
                    if(prediction.cuePath.Count<2 || prediction.PathDistance<.0001f)throw new Exception("Invisible guide: "+power);
                }
                if(shot.force!=power)throw new Exception("Preview mutated shot power");
            }
            if(shot.BuildShotImpulse().impulse.sqrMagnitude!=0)throw new Exception("Zero-power shot was modified");
            Debug.Log("PASS multiplayer guide power: nonzero guide impulses at 0/0.1/0.5/1/reset; real shot unchanged; physics simulation="+(prediction!=null));
        }
        finally { prediction?.Dispose();UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(ballObject); }
    }
}
