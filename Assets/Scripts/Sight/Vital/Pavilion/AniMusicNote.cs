using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Sight.Vital.Pavilion
{
    public class AniMusicNote : MovStoryAni
    {

        ParticleSystem ps;

        public override void Awake()
        {
            ps = transform.Find("PS").GetComponent<ParticleSystem>();
            base.Awake();
        }

        [ContextMenu(nameof(AniPlay))]
        public override void AniPlay(Vector3 pos, CancellationToken _cancellationToken)
        {
        }

        public override void SetupSequence()
        {
            ps.Stop();
            mySequence = DOTween.Sequence();
            mySequence.AppendCallback(() => ps.Play());
            mySequence.SetAutoKill(false).Pause();
        }

    }
}
