using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Test.BallReactAni
{
    public class BombDebrisAni : MonoBehaviour
    {
        public ParticleSystem ps;

        public void Awake()
        {
            ps = transform.Find("PS").GetComponent<ParticleSystem>();
        }


        [ContextMenu(nameof(AniPlay))]
        public void AniPlay()
        {
            StartSequence();
        }

        Sequence mySequence;
        public void SetupSequence()
        {
            ps.Stop();
            mySequence = DOTween.Sequence();
            mySequence.JoinCallback(()=> ps.Play());
            mySequence.SetAutoKill(false).Pause();

        }

        public void StartSequence()
        {
            mySequence.Kill();
            SetupSequence(); // 시퀀스 재설정
            if (mySequence != null && !mySequence.IsPlaying())
            {
                mySequence.Restart();
            }
        }
    }
}
