using UnityEngine;
using Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital;
using PreviewImpulse = Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.Impulse;

namespace Assets.Scripts.Sight.Vital.Pavilion
{
    public partial class ShotCtrl
    {
        public const float DefaultFollowThrough = .2f;
        public bool IsShotAnimating { get; private set; }
        Material[] cueBallLineMaterials, targetBallLineMaterials;
        float lastSentPower = -1, lastSentFollowPosition = -1;

        public void SetFollowThrough(float value)
        {
            pull = float.IsNaN(value) ? DefaultFollowThrough : Mathf.Clamp01(value);
            pullSliderYPos = Mathf.Lerp(pullSliderMinYPos, pullSliderMaxYPos, pull);
            pullSliderDisplacementY = pullSliderYPos;
            if (pullBarHandle) pullBarHandle.localPosition = new Vector3(0, pullSliderYPos, 0);
        }

        public Impulse BuildShotImpulse()
        {
            var shot = BuildImpulseForPower(force);
            return new Impulse(shot.point, shot.impulse, Vector3.zero, cueBall.id, shot.followThrough, shot.spinPersistence);
        }

        public PreviewImpulse BuildGuideImpulse() => BuildImpulseForPower(.4f);

        PreviewImpulse BuildImpulseForPower(float power)
        {
            Vector3 forward = Vector3.ProjectOnPlane(cueSlider.forward, Vector3.up).normalized;
            Vector3 impulse = power * (maxVelocity * cueBall.body.mass) * forward;
            Vector3 centre = cueBall.body.worldCenterOfMass;
            Vector3 offset = cueDisplacement.position - centre;
            float radius = cueBall.GetComponent<SphereCollider>().radius * Mathf.Abs(cueBall.transform.lossyScale.x);
            float contact = Mathf.Clamp01(Vector3.ProjectOnPlane(offset, forward).magnitude / Mathf.Max(radius, .001f));
            float blend = pull <= DefaultFollowThrough ? pull / DefaultFollowThrough : (pull - DefaultFollowThrough) / (1 - DefaultFollowThrough);
            float speed = pull <= DefaultFollowThrough ? Mathf.Lerp(1.04f, 1f, blend) : Mathf.Lerp(1f, .92f, blend);
            return new PreviewImpulse(centre + offset * (.46f / speed), impulse * speed, Vector3.zero,
                pull, FollowThroughProfile.Persistence(power, contact, pull));
        }

        void OnDestroy()
        {
            prediction?.Dispose();
            if (cueBallLineMaterials != null) foreach (var material in cueBallLineMaterials) if (material) Destroy(material);
            if (targetBallLineMaterials != null) foreach (var material in targetBallLineMaterials) if (material) Destroy(material);
        }
    }
}
