using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{
    public enum CushionDir
    {
        None,
        T,
        B,
        R,
        L
    }

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

        public void HitDir(CushionDir dir) => PlayLine(dir);

        public ParticleSystem PlayLine(CushionDir dir, bool useUnscaledTime = false)
        {
            ParticleSystem line;
            switch (dir)
            {
                case CushionDir.T: line = lineT; break;
                case CushionDir.B: line = lineB; break;
                case CushionDir.R: line = lineR; break;
                case CushionDir.L: line = lineL; break;
                default: return null;
            }
            // Stage demos freeze physics (timeScale == 0) and advance recorded balls manually.
            // Their cushion flashes must continue on the same real-time clock as the replay.
            // The green cushion strip is a child particle system; configuring only the
            // parent advances the smoke while the actual light remains frozen in replay.
            foreach(var particles in line.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main=particles.main;
                main.useUnscaledTime=useUnscaledTime;
            }
            line.Play(true);
            return line;
        }
    }
}
