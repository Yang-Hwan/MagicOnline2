using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{
    // Both match scenes use the same isolated prediction engine and cloth model.
    public interface IShotPredictionSource
    {
        Scene PredictionScene { get; }
        Rigidbody[] GetPredictionBodies();
        ClothPhysicsSettings PredictionCloth { get; }
        bool CalibratedPrediction { get; }
        bool FollowThroughProfileActive { get; }
        bool BallImpactTrialActive { get; }
        float InstantFollowDrawWeight(Vector3 incoming, Vector3 towardTarget);
        float PredictionPowerAngle { get; }
        float PredictionSpinAngle { get; }
        float PredictionTopspinGain { get; }
    }

    public partial class PhysicsMng : IShotPredictionSource
    {
        public Scene PredictionScene => gameObject.scene;
        public Rigidbody[] GetPredictionBodies() => System.Array.ConvertAll(ballcs, ball => ball.body);
        public ClothPhysicsSettings PredictionCloth => clothPhysics;
        public bool CalibratedPrediction => useCalibratedPhysics;
        public float PredictionPowerAngle => impactPowerAngle;
        public float PredictionSpinAngle => impactSpinAngle;
        public float PredictionTopspinGain => topspinForwardCarryGain;
    }
}
