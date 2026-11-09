using UnityEngine;
using Assets.Scripts.Exert.Match;
using Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital;

namespace Assets.Scripts.Sight.Vital.Pavilion
{
    public partial class Ball
    {
        [System.NonSerialized] public Vector3 impactIncomingVelocity, impactIncomingSpin;
        [System.NonSerialized] public float strokeFollowThrough = -1f, strokeSpinPersistence = -1f;

        [System.NonSerialized] public int strokeRailHits;

        void OnCollisionEnter(Collision collision)
        {
            if (!isCueBall || !physicsMng.inMove || PoolLogic.controlFromNetwork) return;
            if (strokeSpinPersistence >= 0 && (1 << collision.collider.gameObject.layer) == boardLayer)
            {
                FollowThroughProfile.ApplySecondRailEnglish(ref strokeRailHits, body, collision,
                    impactIncomingVelocity, impactIncomingSpin.y, GetComponent<SphereCollider>().radius * Mathf.Abs(transform.lossyScale.x));
                return;
            }
            var other = collision.collider.GetComponent<Ball>();
            if (!other) return;
            physicsMng.ObserveSentimentContact(this, other);
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
