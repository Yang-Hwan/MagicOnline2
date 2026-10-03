using System.Collections.Generic;
using UnityEngine;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{
    public partial class PhysicsMng
    {
        [Header("Calibrated cloth physics — disable to use legacy behavior")]
        [Tooltip("Choose before entering Play mode. Off restores legacy damping/contact behavior.")]
        public bool useCalibratedPhysics = true;
        public ClothPhysicsSettings clothPhysics = new ClothPhysicsSettings();
        readonly List<Collider> changedSurfaces = new List<Collider>();
        readonly List<PhysicsMaterial> oldSurfaceMaterials = new List<PhysicsMaterial>();
        readonly List<float> oldContactOffsets = new List<float>();
        PhysicsMaterial runtimeCloth, runtimeBall, runtimeCushion;
        float settledTime;
        bool clothInitialized;

        void InitializeClothPhysics()
        {
            if (!useCalibratedPhysics || clothInitialized) return;
            clothInitialized = true;
            runtimeCloth = ClothPhysics.CreateClothMaterial();
            runtimeBall = ClothPhysics.CreateBallMaterial(clothPhysics); runtimeCushion=ClothPhysics.CreateCushionMaterial(clothPhysics);
            foreach (var collider in FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (collider.gameObject.scene != gameObject.scene) continue;
                int layer = collider.gameObject.layer;
                if (layer != LayerMask.NameToLayer("Cloth") && layer != LayerMask.NameToLayer("Board") &&
                    layer != LayerMask.NameToLayer("Ball") && layer != LayerMask.NameToLayer("CueBall")) continue;
                changedSurfaces.Add(collider); oldSurfaceMaterials.Add(collider.sharedMaterial); oldContactOffsets.Add(collider.contactOffset);
                collider.contactOffset = .0001f;
                if (layer == LayerMask.NameToLayer("Cloth")) collider.sharedMaterial = runtimeCloth;
                else if (layer == LayerMask.NameToLayer("Board")) collider.sharedMaterial=runtimeCushion;
                else collider.sharedMaterial=runtimeBall;
            }
            foreach (var ball in ballcs)
            {
                ClothPhysics.ConfigureBody(ball.body);
                float radius = ball.GetComponent<SphereCollider>().radius * Mathf.Abs(ball.transform.lossyScale.x);
                if (Physics.Raycast(ball.body.position + Vector3.up*.1f, Vector3.down, out var hit, .3f, 1 << LayerMask.NameToLayer("Cloth")))
                {
                    Vector3 position = ball.body.position; position.y = hit.point.y + radius;
                    ball.body.position = position; ball.transform.position = position;
                    resetPos[ball.id] = position;
                }
            }
            Physics.SyncTransforms();
        }

        void StepClothPhysics(float dt)
        {
            foreach (var ball in ballcs)
            {
                var body = ball.body;
                if (!body || body.isKinematic || !ball.gameObject.activeInHierarchy || body.IsSleeping()) continue;
                float radius = ball.GetComponent<SphereCollider>().radius * Mathf.Abs(ball.transform.lossyScale.x);
                // This profile constrains every ball to the cloth plane; contact is continuous.
                Vector3 velocity = body.linearVelocity, spin = body.angularVelocity;
                if(FollowThroughProfileActive && ball.strokeSpinPersistence>=0)
                    FollowThroughProfile.ApplyPersistence(velocity,ref spin,radius,dt,ball.strokeSpinPersistence,clothPhysics.stopSpeed);
                ClothPhysics.Step(ref velocity, ref spin, radius, dt, clothPhysics);
                body.linearVelocity = velocity; body.angularVelocity = spin;
                ball.impactIncomingVelocity = velocity; ball.impactIncomingSpin = spin;
            }
        }

        bool CalibratedBallsSettled()
        {
            foreach (var ball in ballcs)
                if (ball.body.linearVelocity.magnitude > clothPhysics.stopSpeed || ball.body.angularVelocity.magnitude > clothPhysics.stopSpin)
                { settledTime = 0; return false; }
            settledTime += Time.fixedDeltaTime;
            return settledTime >= clothPhysics.stopDelay;
        }

        void DisposeClothPhysics()
        {
            for (int i = 0; i < changedSurfaces.Count; i++)
                if (changedSurfaces[i]) { changedSurfaces[i].sharedMaterial = oldSurfaceMaterials[i]; changedSurfaces[i].contactOffset = oldContactOffsets[i]; }
            if (runtimeCloth) Destroy(runtimeCloth);
            if (runtimeBall) Destroy(runtimeBall);
            if (runtimeCushion) Destroy(runtimeCushion);
        }
    }
}
