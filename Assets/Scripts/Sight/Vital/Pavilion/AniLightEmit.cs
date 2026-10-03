using DG.Tweening;
using System.Threading;
using UnityEngine;

namespace Assets.Scripts.Sight.Vital.Pavilion
{
    public class AniLightEmit : MovStoryAni
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
