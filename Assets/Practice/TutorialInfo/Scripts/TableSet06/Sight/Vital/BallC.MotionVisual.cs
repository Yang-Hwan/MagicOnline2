using UnityEngine;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{
    public partial class BallC
    {
        Transform motionVisual;
        Vector3 previousPosition;
        Quaternion previousRotation;
        bool hasMotionSample;

        public Vector3 VisualPosition => motionVisual ? motionVisual.position : transform.position;

        void InitializeMotionVisual()
        {
            var source = GetComponent<MeshRenderer>();
            var mesh = GetComponent<MeshFilter>();
            if (!source || !mesh || !source.enabled) return;
            var visual = new GameObject("Interpolated Ball", typeof(MeshFilter), typeof(MeshRenderer));
            motionVisual = visual.transform;
            motionVisual.SetParent(transform, false);
            visual.layer = gameObject.layer;
            visual.GetComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
            var renderer = visual.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = source.sharedMaterials;
            renderer.shadowCastingMode = source.shadowCastingMode;
            renderer.receiveShadows = source.receiveShadows;
            renderer.lightProbeUsage = source.lightProbeUsage;
            renderer.reflectionProbeUsage = source.reflectionProbeUsage;
            renderer.renderingLayerMask = source.renderingLayerMask;
            source.enabled = false;
        }

        // Sample once before the whole fixed tick, not before each physics substep.
        public void CaptureMotionVisual()
        {
            previousPosition = body.position;
            previousRotation = body.rotation;
            hasMotionSample = true;
        }

        public void UpdateMotionVisual(bool interpolate, float alpha)
        {
            if (!motionVisual) return;
            motionVisual.gameObject.layer = gameObject.layer;
            if (interpolate && hasMotionSample)
                motionVisual.SetPositionAndRotation(Vector3.Lerp(previousPosition, body.position, alpha),
                    Quaternion.Slerp(previousRotation, body.rotation, alpha));
            else
            {
                // Placement, undo, replay and settled shots must snap immediately.
                motionVisual.localPosition = Vector3.zero;
                motionVisual.localRotation = Quaternion.identity;
                hasMotionSample = false;
            }
            // Only the mesh and shadow move. Never feed a render pose into the collider.
            SetBallShadowAndBlickBlick();
        }
    }
}
