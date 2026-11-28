using UnityEngine;

namespace Assets.Scripts.Often
{
    // Constrained-table masse: downward impulse creates horizontal-axis torque.
    // FreezePositionY absorbs vertical translation; ClothPhysics converts slip to curve.
    // No scripted sideways force or target attraction is added.
    public static class ElevatedCuePhysics
    {
        public static float Angle(Vector3 cueForward)
            => Mathf.Clamp(Mathf.Atan2(-cueForward.y,Vector3.ProjectOnPlane(cueForward,Vector3.up).magnitude)*Mathf.Rad2Deg,0,80);
        public static Vector3 Direction(Vector3 cueForward)
        {
            var planar=Vector3.ProjectOnPlane(cueForward,Vector3.up).normalized;
            float angle=Angle(cueForward);
            if(angle<=.001f)return planar; // Preserve the existing horizontal shot exactly.
            float radians=angle*Mathf.Deg2Rad;
            return planar*Mathf.Cos(radians)-Vector3.up*Mathf.Sin(radians);
        }
    }
}
