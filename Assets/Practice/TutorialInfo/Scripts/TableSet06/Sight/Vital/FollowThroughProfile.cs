using UnityEngine;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{
    public static class FollowThroughProfile
    {
        public static float Thickness(Vector3 incoming,Vector3 towardTarget)
        {
            Vector3 direction=Vector3.ProjectOnPlane(incoming,Vector3.up);
            Vector3 normal=Vector3.ProjectOnPlane(towardTarget,Vector3.up);
            if(direction.sqrMagnitude<.000001f || normal.sqrMagnitude<.000001f)return 1f;
            return Mathf.Clamp01(1f-Mathf.Abs(Vector3.Dot(direction.normalized,Vector3.Cross(Vector3.up,normal.normalized))));
        }
        public static float ThicknessWeight(float thickness) => 1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.5f,1f,thickness));

        public static float Persistence(float power,float tipOffset,float follow)
        {
            power=Mathf.Clamp01(power);tipOffset=Mathf.Clamp01(tipOffset);follow=Mathf.Clamp01(follow);
            float baseline=Mathf.Lerp(.4f,1f,power*tipOffset);
            // Smooth peak at 40% power; preserve the original endpoints and centre hits.
            float powerWeight=power<=.4f ? Mathf.SmoothStep(0f,1f,power/.4f) :
                1f-Mathf.SmoothStep(0f,1f,(power-.4f)/.6f);
            float followWeight=1f-Mathf.SmoothStep(0f,1f,follow);
            return Mathf.Clamp01(baseline*(1f+.2f*powerWeight*followWeight*tipOffset));
        }
        public static float ContactInfluence(float follow) => 1f-Mathf.Clamp01(follow);

        // Gameplay damping using the persistence captured with the shot.
        public static void ApplyPersistence(Vector3 velocity,ref Vector3 spin,float radius,float dt,float persistence,float stopSpeed)
        {
            if(radius<=0 || dt<=0 || persistence<0)return;
            Vector3 planar=Vector3.ProjectOnPlane(velocity,Vector3.up);
            if(planar.magnitude<=stopSpeed)return; // Retain the existing fast stationary-spin settling.
            float decay=Mathf.Exp(-(1f-Mathf.Clamp01(persistence))*1.5f*dt);
            Vector3 slip=planar+Vector3.Cross(spin,Vector3.down*radius);
            if(slip.magnitude>stopSpeed) { spin.x*=decay;spin.z*=decay; }
            spin.y*=decay;
        }

        // Remaining forward surface speed relative to incoming translation, not the selected tip.
        public static float ForwardSpinRatio(Vector3 spin,Vector3 incoming,float radius)
        {
            Vector3 planar=Vector3.ProjectOnPlane(incoming,Vector3.up);
            if(radius<=0 || planar.sqrMagnitude<.000001f)return 0;
            return Mathf.Clamp01(Vector3.Dot(Vector3.Cross(spin,Vector3.up)*radius,planar)/planar.sqrMagnitude);
        }

        // Reduce separation speed at ball contact, returning some normal momentum
        // from the target to the cue. Pair momentum is conserved and energy decreases.
        public static void ForwardCarry(ref Vector3 cueVelocity,ref Vector3 targetVelocity,
            Vector3 towardTarget,float cueMass,float targetMass,float follow,float topspinRatio=0,float topspinGain=0)
        {
            if(follow<=0 || cueMass<=0 || targetMass<=0)return;
            Vector3 normal=Vector3.ProjectOnPlane(towardTarget,Vector3.up).normalized;
            float separation=Vector3.Dot(targetVelocity-cueVelocity,normal);
            if(separation<=0)return;
            // Bounded gameplay restitution adjustment. No angular velocity is added or replaced.
            float carry=Mathf.Clamp(.25f+Mathf.Clamp01(topspinRatio)*Mathf.Clamp(topspinGain,0,.5f),0,.75f);
            float impulse=carry*Mathf.Clamp01(follow)*separation/(1f/cueMass+1f/targetMass);
            cueVelocity+=normal*(impulse/cueMass);
            targetVelocity-=normal*(impulse/targetMass);
        }

        public static void ForwardCarry(Rigidbody cue,Rigidbody target,float follow,Vector3 incoming,float radius,float topspinGain)
        {
            if(!cue || !target || cue.isKinematic || target.isKinematic)return;
            Vector3 a=cue.linearVelocity,b=target.linearVelocity;
            ForwardCarry(ref a,ref b,target.position-cue.position,cue.mass,target.mass,follow,ForwardSpinRatio(cue.angularVelocity,incoming,radius),topspinGain);
            cue.linearVelocity=a;target.linearVelocity=b;
        }
    }
    public partial class PhysicsMng
    {
        [Header("Power/tip persistence and follow-through contact profile")]
        public bool enableFollowThroughProfile=true;
        [Tooltip("Follow/draw comes from cloth friction over time. Off restores the previous instantaneous carry and vertical-tip angle correction.")]
        public bool useNaturalFollowDraw=true;
        [Tooltip("Combine cloth curves with instant correction: full through 50% thickness, smoothly zero at 100%. Off uses Use Natural Follow Draw.")]
        public bool blendFollowDrawByThickness=true;
        public float InstantFollowDrawWeight(Vector3 incoming,Vector3 towardTarget) =>
            blendFollowDrawByThickness ? FollowThroughProfile.ThicknessWeight(FollowThroughProfile.Thickness(incoming,towardTarget)) : (useNaturalFollowDraw ? 0f : 1f);
        [Tooltip("Extra forward carry from remaining topspin. Zero restores the previous 25% carry. Local practice only.")]
        [Range(0f,.5f)] public float topspinForwardCarryGain=.3f;
        public bool FollowThroughProfileActive => enableFollowThroughProfile && useCalibratedPhysics && UsesPracticePhysics;
    }
}
