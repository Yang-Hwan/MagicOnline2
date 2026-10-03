using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital;

namespace Assets.Scripts.Sight.Vital.Pavilion
{
    public partial class PhysicsMng : IShotPredictionSource
    {
        public readonly ClothPhysicsSettings clothPhysics = new ClothPhysicsSettings();
        public bool useCalibratedPhysics => true;
        public bool FollowThroughProfileActive => true;
        public bool BallImpactTrialActive => true;
        public bool blendFollowDrawByThickness => true;
        public bool useNaturalFollowDraw => true;
        public float impactPowerAngle => 12f;
        public float impactSpinAngle => 8f;
        public float topspinForwardCarryGain => .3f;
        public Scene PredictionScene => gameObject.scene;
        public Rigidbody[] GetPredictionBodies() => System.Array.ConvertAll(balls, ball => ball.body);
        public ClothPhysicsSettings PredictionCloth => clothPhysics;
        public bool CalibratedPrediction => true;
        public float PredictionPowerAngle => impactPowerAngle;
        public float PredictionSpinAngle => impactSpinAngle;
        public float PredictionTopspinGain => topspinForwardCarryGain;
        public float InstantFollowDrawWeight(Vector3 incoming, Vector3 towardTarget) =>
            FollowThroughProfile.ThicknessWeight(FollowThroughProfile.Thickness(incoming, towardTarget));

        readonly List<Collider> practiceSurfaces = new List<Collider>();
        readonly List<PhysicsMaterial> originalMaterials = new List<PhysicsMaterial>();
        readonly List<float> originalOffsets = new List<float>();
        PhysicsMaterial practiceCloth, practiceBall, practiceBoard;
        SimulationMode previousSimulation;
        float previousStep, previousBounce, previousSleep, previousContact;
        int previousSolver, previousVelocitySolver;
        bool practicePhysicsReady, capturedPhysics;
        float settledTime;
        int trailVersion;

        // Match opening runs before the first simulation step. Updating only the
        // Transform leaves network snapshots and Ball.OnState(Move) at the old pose.
        void PlaceBall(int index, Vector3 position)
        {
            var ball = balls[index];
            var body = ball.body ? ball.body : ball.GetComponent<Rigidbody>();
            body.position = position;
            ball.transform.position = position;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.Sleep();
            }
            ball.strokeSpinPersistence = -1;
            ball.strokeFollowThrough = -1;
            settledTime = 0;
        }

        void CapturePhysicsSettings()
        {
            previousSimulation = Physics.simulationMode; previousStep = Time.fixedDeltaTime;
            previousBounce = Physics.bounceThreshold; previousSleep = Physics.sleepThreshold;
            previousContact = Physics.defaultContactOffset;
            previousSolver = Physics.defaultSolverIterations;
            previousVelocitySolver = Physics.defaultSolverVelocityIterations;
            capturedPhysics = true;
        }

        void InitializePracticePhysics()
        {
            if (practicePhysicsReady) return;
            Physics.SyncTransforms();
            practiceCloth = ClothPhysics.CreateClothMaterial();
            practiceBall = ClothPhysics.CreateBallMaterial(clothPhysics);
            practiceBoard = ClothPhysics.CreateCushionMaterial(clothPhysics);
            foreach (var collider in FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (collider.gameObject.scene != gameObject.scene) continue;
                int layer = collider.gameObject.layer;
                PhysicsMaterial material = layer == LayerMask.NameToLayer("Cloth") ? practiceCloth :
                    layer == LayerMask.NameToLayer("Board") ? practiceBoard :
                    layer == LayerMask.NameToLayer("Ball") || layer == LayerMask.NameToLayer("CueBall") ? practiceBall : null;
                if (!material) continue;
                practiceSurfaces.Add(collider); originalMaterials.Add(collider.sharedMaterial);
                originalOffsets.Add(collider.contactOffset);
                collider.sharedMaterial = material; collider.contactOffset = .0001f;
            }
            foreach (var ball in balls)
            {
                ClothPhysics.ConfigureBody(ball.body);
                float radius = ball.GetComponent<SphereCollider>().radius * Mathf.Abs(ball.transform.lossyScale.x);
                if (Physics.Raycast(ball.body.position + Vector3.up * .1f, Vector3.down, out var hit, .3f,
                    1 << LayerMask.NameToLayer("Cloth")))
                {
                    var position = ball.body.position; position.y = hit.point.y + radius;
                    ball.body.position = position; ball.transform.position = position;
                }
            }
            Physics.SyncTransforms();
            practicePhysicsReady = true;
        }

        void StepPracticePhysics()
        {
            if (!practicePhysicsReady || !inMove) return;
            int steps = Mathf.Clamp(clothPhysics.substeps, 1, 8);
            float dt = Time.fixedDeltaTime / steps;
            for (int step = 0; step < steps; step++)
            {
                for (int i = 0; i < ballLen; i++)
                {
                    var ball = balls[i]; var body = ball.body;
                    if (body.isKinematic || !ball.gameObject.activeInHierarchy || body.IsSleeping()) continue;
                    float radius = ball.GetComponent<SphereCollider>().radius * Mathf.Abs(ball.transform.lossyScale.x);
                    var velocity = body.linearVelocity; var spin = body.angularVelocity;
                    if (ball.strokeSpinPersistence >= 0)
                        FollowThroughProfile.ApplyPersistence(velocity, ref spin, radius, dt, ball.strokeSpinPersistence, clothPhysics.stopSpeed);
                    ClothPhysics.Step(ref velocity, ref spin, radius, dt, clothPhysics);
                    body.linearVelocity = velocity; body.angularVelocity = spin;
                    ball.impactIncomingVelocity = velocity; ball.impactIncomingSpin = spin;
                }
                Physics.Simulate(dt);
            }
        }

        bool PracticeBallsSettled()
        {
            for (int i = 0; i < ballLen; i++)
            {
                var body = balls[i].body;
                if (!body.isKinematic && (body.linearVelocity.magnitude > clothPhysics.stopSpeed ||
                    body.angularVelocity.magnitude > clothPhysics.stopSpin))
                { settledTime = 0; return false; }
            }
            settledTime += Time.fixedDeltaTime;
            return settledTime >= clothPhysics.stopDelay;
        }

        void OnDestroy()
        {
            for (int i = 0; i < practiceSurfaces.Count; i++)
                if (practiceSurfaces[i]) { practiceSurfaces[i].sharedMaterial = originalMaterials[i]; practiceSurfaces[i].contactOffset = originalOffsets[i]; }
            if (practiceCloth) Destroy(practiceCloth);
            if (practiceBall) Destroy(practiceBall);
            if (practiceBoard) Destroy(practiceBoard);
            if (!capturedPhysics) return;
            Physics.simulationMode = previousSimulation; Time.fixedDeltaTime = previousStep;
            Physics.bounceThreshold = previousBounce; Physics.sleepThreshold = previousSleep;
            Physics.defaultContactOffset = previousContact; Physics.defaultSolverIterations = previousSolver;
            Physics.defaultSolverVelocityIterations = previousVelocitySolver;
        }
    }
}
