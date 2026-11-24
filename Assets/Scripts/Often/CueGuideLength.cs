using System.Collections.Generic;
using UnityEngine;
namespace Assets.Scripts.Often
{
    public static class CueGuideLength
    {
        public static bool ReachesContact(IList<Vector3> points, float originalLimit, float scale, int contactIndex)
        {
            if(scale<=0 || contactIndex<1 || contactIndex>=points.Count)return false;
            float contactDistance=0;
            for(int i=1;i<=contactIndex;i++)contactDistance+=Vector3.Distance(points[i-1],points[i]);
            return Limit(points,originalLimit,scale)+.00001f>=contactDistance;
        }
        // Clip the already predicted polyline; never change impulse or simulation distance.
        public static float Limit(IList<Vector3> points, float originalLimit, float scale)
        {
            float length=0;
            for(int i=1;i<points.Count;i++)length+=Vector3.Distance(points[i-1],points[i]);
            return Mathf.Min(length,originalLimit)*Mathf.Clamp01(scale);
        }
    }
}
