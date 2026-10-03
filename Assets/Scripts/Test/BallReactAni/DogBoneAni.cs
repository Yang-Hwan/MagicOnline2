using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;


namespace Assets.Scripts.Test.BallReactAni
{
    public class DogBoneAni : MonoBehaviour
    {
        Sequence mySequence;
        public Transform psTr;
        public ParticleSystem ps;
        public Vector3 _pos;

        public void Awake()
        {
            psTr = transform.Find("PS");
            ps = transform.Find("PS").GetComponent<ParticleSystem>();
        }


        [ContextMenu(nameof(AniPlay))]
        public void AniPlay()
        {
            Vector3 pos = new Vector3(-0.61f, 0.1f, -0.51f);
            _pos = pos;
            StartSequence();
        }

        [ContextMenu(nameof(AniPlay2))]
        public void AniPlay2()
        {
            Vector3 pos = new Vector3(0.61f, 0.1f, 0.51f);
            _pos = pos;
            StartSequence();
        }

        public void SetupSequence()
        {
            ps.Stop();
            mySequence = DOTween.Sequence();
            mySequence.Append(psTr.DOMove(_pos, 0.01f));
            mySequence.AppendCallback(() => ps.Play());
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
