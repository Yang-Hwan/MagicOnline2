using UnityEngine;

namespace Assets.Scripts.Sight.Vital.Pavilion
{
    public struct Impulse
    {
        public readonly Vector3 point;
        public readonly Vector3 impulse;
        public readonly Vector3 pushVec;
        public int id;
        public readonly float followThrough, spinPersistence;

        public Impulse(Vector3 point, Vector3 impulse, Vector3 pushVec, int id, float followThrough = -1f, float spinPersistence = -1f)
        {
            this.point = point;
            this.impulse = impulse;
            this.pushVec = pushVec;
            this.id = id;
            this.followThrough = followThrough; this.spinPersistence = spinPersistence;
        }
    }

    public struct RandForce4
    {
        public readonly Vector3 pos1;
        public readonly Vector3 pos2;
        public readonly Vector3 pos3;
        public readonly Vector3 pos4;

        public readonly Vector3[] lst;

        public RandForce4(Vector3 pos1, Vector3 pos2, Vector3 pos3, Vector3 pos4)
        {
            lst = new Vector3[4];
            this.pos1 = pos1;
            this.pos2 = pos2;
            this.pos3 = pos3;
            this.pos4 = pos4;
            lst[0] = pos1;
            lst[1] = pos2;
            lst[2] = pos3;
            lst[3] = pos4;
        }
    }
}
