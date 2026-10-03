using System;
using UnityEngine;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{
    [Serializable]
    public sealed class ClothPhysicsSettings
    {
        [Range(.05f, .4f)] public float slidingFriction = .2f;
        [Range(.002f, .03f)] public float rollingResistance = .0045f;
        [Range(.01f, .1f)] public float ballFriction = .05f;
        [Range(.01f, .3f)] public float cushionFriction = .075f;
        [Range(.01f, .6f)] public float cushionStaticFriction = .285f;
        [Range(.6f, .98f)] public float cushionRestitution = .9f;
        [Range(1f, 20f)] public float spinDeceleration = 1.5f;
        [Tooltip("Extra side-spin damping only below stopSpeed with negligible cloth slip. Set 0 to disable.")]
        [Range(0f, 12f)] public float stationarySpinDamping = 6f;
        [Range(1, 8)] public int substeps = 4;
        public float stopSpeed = .008f;
        public float stopSpin = .15f;
        public float stopDelay = .25f;
    }

    // Homogeneous sphere on level cloth. Contact friction dissipates total kinetic energy;
    // it may convert existing spin energy into translation. No target/direction assistance.
    public static class ClothPhysics
    {
        public static void Step(ref Vector3 velocity, ref Vector3 spin, float radius, float dt, ClothPhysicsSettings settings)
        {
            if (radius <= 0 || dt <= 0) return;
            float gravity = Physics.gravity.magnitude;
            Vector3 planar = Vector3.ProjectOnPlane(velocity, Vector3.up);
            Vector3 slip = planar + Vector3.Cross(spin, Vector3.down * radius);
            float remaining = dt;
            if (slip.magnitude > .00001f)
            {
                // du/dt = -(1 + m R^2 / I) mu g = -3.5 mu g, I = 2/5 m R^2.
                float slideTime = Mathf.Min(dt, slip.magnitude / (3.5f * settings.slidingFriction * gravity));
                Vector3 delta = -slip.normalized * (settings.slidingFriction * gravity * slideTime);
                planar += delta;
                spin += Vector3.Cross(Vector3.down * radius, delta) * (2.5f / (radius * radius));
                remaining -= slideTime;
            }
            if (remaining > 0)
            {
                planar = Vector3.MoveTowards(planar, Vector3.zero, settings.rollingResistance * gravity * remaining);
                float sideSpin = spin.y;
                spin = Vector3.Cross(Vector3.up, planar) / radius;
                spin.y = sideSpin;
            }
            // Smooth gameplay settling aid, not a measured cloth coefficient. Preserve moving
            // English and horizontal-axis spin that can still drive follow/draw translation.
            Vector3 residualSlip = planar + Vector3.Cross(spin, Vector3.down * radius);
            float activity = Mathf.Max(planar.magnitude, residualSlip.magnitude);
            float restBlend = settings.stopSpeed > 0
                ? Mathf.SmoothStep(0, 1, 1 - Mathf.Clamp01(activity / settings.stopSpeed)) : 0;
            spin.y *= Mathf.Exp(-Mathf.Max(0, settings.stationarySpinDamping) * restBlend * dt);
            spin.y = Mathf.MoveTowards(spin.y, 0, settings.spinDeceleration * dt);
            velocity = planar + Vector3.up * velocity.y;
        }

        public static void ConfigureBody(Rigidbody body)
        {
            body.linearDamping = 0; body.angularDamping = 0;
            // The shot controller delivers horizontal impulses; this profile models level-table shots.
            body.useGravity = false; body.constraints |= RigidbodyConstraints.FreezePositionY;
            body.maxAngularVelocity = 1000;
            body.sleepThreshold = 0; // Low-speed tail is ended by the explicit settled test.
            body.solverIterations = 8; body.solverVelocityIterations = 4;
        }

        public static PhysicsMaterial CreateBallMaterial(ClothPhysicsSettings settings)
        {
            return new PhysicsMaterial("Calibrated ball (runtime)") { dynamicFriction=settings.ballFriction, staticFriction=Mathf.Max(.07f,settings.ballFriction),
                bounciness=.95f, frictionCombine=PhysicsMaterialCombine.Average, bounceCombine=PhysicsMaterialCombine.Average };
        }
        public static PhysicsMaterial CreateCushionMaterial(ClothPhysicsSettings settings)
        {
            // Average combination with the ball produces the specified ball-rail pair coefficients.
            return new PhysicsMaterial("Calibrated cushion (runtime)") { dynamicFriction=Mathf.Max(0,2*settings.cushionFriction-settings.ballFriction),
                staticFriction=Mathf.Max(0,2*settings.cushionStaticFriction-Mathf.Max(.07f,settings.ballFriction)), bounciness=Mathf.Clamp01(2*settings.cushionRestitution-.95f),
                frictionCombine=PhysicsMaterialCombine.Average, bounceCombine=PhysicsMaterialCombine.Average };
        }
        public static PhysicsMaterial CreateClothMaterial()
        {
            return new PhysicsMaterial("Calibrated cloth (runtime)") {
                dynamicFriction = 0, staticFriction = 0, bounciness = 0,
                frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum
            };
        }
    }
}
