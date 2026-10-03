using Assets.Scripts.Often;
using Assets.TutorialInfo.Scripts.TableSet06.Often;
using UnityEngine;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{
    public partial class ShotCtrl
    {
        public MatchShotCommand CaptureOnlineStroke()
        {
            Vector3 forward = Vector3.ProjectOnPlane(cueSlider.forward, Vector3.up).normalized;
            Vector3 offset = cueDisplacement.position - cueBall.body.worldCenterOfMass;
            float radius = cueBall.GetComponent<SphereCollider>().radius * cueBall.transform.lossyScale.x;
            var contact = new Vector2(Vector3.Dot(offset, Vector3.Cross(Vector3.up, forward)), offset.y) / radius;
            return new MatchShotCommand { power = force, followThrough = pull,
                yaw = Mathf.Repeat(Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg, 360),
                contact = Vector2.ClampMagnitude(contact, 1) };
        }

        // Same stroke model as practice, reconstructed using the authority's ball mass/radius/position.
        public Impulse BuildOnlineImpulse(MatchShotCommand shot)
        {
            var ball = physicsManager.ballcs[shot.seat];
            Vector3 forward = Quaternion.Euler(0, shot.yaw, 0) * Vector3.forward;
            float radius = ball.GetComponent<SphereCollider>().radius * ball.transform.lossyScale.x;
            Vector3 offset = radius * (Vector3.Cross(Vector3.up, forward) * shot.contact.x + Vector3.up * shot.contact.y);
            float blend = shot.followThrough <= DefaultFollowThrough ? shot.followThrough / DefaultFollowThrough :
                (shot.followThrough - DefaultFollowThrough) / (1f - DefaultFollowThrough);
            float speedScale = shot.followThrough <= DefaultFollowThrough ? Mathf.Lerp(1.04f, 1f, blend) : Mathf.Lerp(1f, .92f, blend);
            return new Impulse(ball.body.worldCenterOfMass + offset * (.46f / speedScale),
                shot.power * cueBallMaxVelocity * ball.body.mass * speedScale * forward, Vector3.zero,
                shot.followThrough, FollowThroughProfile.Persistence(shot.power, shot.contact.magnitude, shot.followThrough));
        }
    }
}
