using UnityEngine;
using Assets.Scripts.Exert.Match;
using Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital;

namespace Assets.Scripts.Sight.Vital.Pavilion
{
    public partial class Ball
    {
        [System.NonSerialized] public Vector3 impactIncomingVelocity, impactIncomingSpin;
        [System.NonSerialized] public float strokeFollowThrough = -1f, strokeSpinPersistence = -1f;

        void OnCollisionEnter(Collision collision)
        {
            if (!isCueBall || !physicsMng.inMove || PoolLogic.controlFromNetwork) return;
            var other = collision.collider.GetComponent<Ball>();
            if (!other) return;
            float radius = GetComponent<SphereCollider>().radius * Mathf.Abs(transform.lossyScale.x);
            float weight = physicsMng.InstantFollowDrawWeight(impactIncomingVelocity, other.body.position - body.position);
            body.linearVelocity = BallImpactTrial.Resolve(impactIncomingVelocity, body.linearVelocity,
                impactIncomingSpin, radius, body.position - other.body.position,
                physicsMng.impactPowerAngle, physicsMng.impactSpinAngle,
                strokeFollowThrough >= 0 ? FollowThroughProfile.ContactInfluence(strokeFollowThrough) : 1f, false, weight);
            if (weight > 0)
                FollowThroughProfile.ForwardCarry(body, other.body, strokeFollowThrough * weight,
                    impactIncomingVelocity, radius, physicsMng.topspinForwardCarryGain);
        }
    }
}
