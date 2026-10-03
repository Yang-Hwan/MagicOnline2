using UnityEngine;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{
    // Explicit arcade rule, not a physical friction/restitution coefficient.
    // Rotates existing velocity only; neither speed nor spin energy is increased.
    public static class BallImpactTrial
    {
        public static Vector3 Resolve(Vector3 incoming, Vector3 outgoing, Vector3 spin,
            float radius, Vector3 normal, float powerAngle, float spinAngle, float tipInfluence = 1f, bool naturalFollowDraw = false, float verticalTipWeight = 1f)
        {
            Vector3 before = Vector3.ProjectOnPlane(incoming, Vector3.up);
            Vector3 after = Vector3.ProjectOnPlane(outgoing, Vector3.up);
            float speed = after.magnitude;
            if (before.sqrMagnitude < .0001f || speed < .008f) return outgoing;
            float angle = Vector3.SignedAngle(before, after, Vector3.up);
            // Preserve the natural cut side, with no invented side for a straight hit.
            float extra = Mathf.Min(Mathf.Abs(angle) * .25f, Mathf.Max(0, powerAngle)) *
                Mathf.InverseLerp(.5f, 4f, before.magnitude);
            after = Quaternion.AngleAxis(Mathf.Sign(angle) * extra, Vector3.up) * after;
            Vector3 right = Vector3.Cross(Vector3.up, before.normalized);
            // Horizontal-axis spin bends the path through cloth friction, not an instant angle snap.
            Vector3 rolling = naturalFollowDraw ? Vector3.zero : Vector3.Cross(spin, Vector3.up) * (radius * Mathf.Clamp01(verticalTipWeight));
            Vector3 tipEffect = rolling - right * (spin.y * radius);
            // Use surviving spin at impact, never a stale UI tip or a target position.
            tipInfluence=Mathf.Clamp01(tipInfluence);
            Vector3 desired = after + tipEffect * (.2f*tipInfluence);
            if (desired.sqrMagnitude > .000001f)
                after = Vector3.RotateTowards(after, desired, Mathf.Max(0, spinAngle) * tipInfluence * Mathf.Deg2Rad, 0);
            normal = Vector3.ProjectOnPlane(normal, Vector3.up).normalized;
            // Do not rotate back into the contacted ball.
            if (Vector3.Dot(after, normal) < 0) after -= normal * Vector3.Dot(after, normal);
            if (after.sqrMagnitude < .000001f) return outgoing;
            return after.normalized * speed + Vector3.up * outgoing.y;
        }
    }

    public partial class PhysicsMng
    {
        [Header("Ball impact arcade trial — OFF restores original collision behavior")]
        public bool enableBallImpactTrial = true;
        [Range(0, 20)] public float impactPowerAngle = 12f;
        [Range(0, 20)] public float impactSpinAngle = 8f;
        public bool BallImpactTrialActive => enableBallImpactTrial && useCalibratedPhysics && UsesPracticePhysics;
    }
}
