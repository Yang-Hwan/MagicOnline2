using Assets.Scripts.Often;
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
            if (physicsMng && physicsMng.inMove) GameAudio.Impact(this, collision);
            if (!isCueBall || !physicsMng.inMove || PoolLogic.controlFromNetwork) return;
            int contactLayer = collision.collider.gameObject.layer;
            if (contactLayer == LayerMask.NameToLayer("Board"))
                physicsMng.TechniqueTrace.Hit(-1, physicsMng.TechniqueTrace.RailAt(body.position,collision.contactCount>0?collision.GetContact(0).normal:Vector3.zero), body.position,
                    impactIncomingVelocity, body.linearVelocity, impactIncomingSpin.y);
            else
            {
                var target = collision.collider.GetComponent<Ball>();
                if(target) physicsMng.TechniqueTrace.Hit(target.id,-1,body.position,
                    impactIncomingVelocity,body.linearVelocity,impactIncomingSpin.y,target.body.position,
                    GetComponent<SphereCollider>().radius*Mathf.Abs(transform.lossyScale.x));
            }
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
