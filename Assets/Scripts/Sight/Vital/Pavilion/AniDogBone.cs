using DG.Tweening;
using System.Threading;
using UnityEngine;


namespace Assets.Scripts.Sight.Vital.Pavilion
{
    public class AniDogBone : MovStoryAni
    {
        ParticleSystem ps;
        public Transform psTr;

        public override void Awake()
        {
            psTr = transform.Find("PS");
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
            mySequence.Append(psTr.DOMove(_pos, 0.01f));
            mySequence.AppendCallback(() => ps.Play());
            mySequence.SetAutoKill(false).Pause();
        }

    }
}
