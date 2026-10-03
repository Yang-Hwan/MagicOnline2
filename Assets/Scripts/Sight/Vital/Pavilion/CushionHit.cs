using Assets.Scripts.Often;
using UnityEngine;

namespace Assets.Scripts.Sight.Vital.Pavilion
{

    //public enum CushionDir
    //{
    //    None,
    //    T,
    //    B,
    //    R,
    //    L
    //}

    public class CushionHit : MonoBehaviour
    {

        ParticleSystem lineT;
        ParticleSystem lineB;
        ParticleSystem lineR;
        ParticleSystem lineL;

        private void Awake()
        {
            lineT = transform.GetChild(0).GetComponent<ParticleSystem>();
            lineB = transform.GetChild(1).GetComponent<ParticleSystem>();
            lineR = transform.GetChild(2).GetComponent<ParticleSystem>();
            lineL = transform.GetChild(3).GetComponent<ParticleSystem>();
        }

        public void HitDir(CushionDir dir)
        {
            switch (dir)
            {
                case CushionDir.T:
                    lineT.Play();
                    break;
                case CushionDir.B:
                    lineB.Play();
                    break;
                case CushionDir.R:
                    lineR.Play();
                    break;
                case CushionDir.L:
                    lineL.Play();
                    break;
            }
        }

    }
}
