using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Threading;
using UnityEngine;

namespace Assets.Scripts.Sight.Vital.Pavilion
{
    public class AniGlassBroken : MovStoryAni
    {


        public SpriteRenderer hole1;
        public SpriteRenderer hole2;
        public ParticleSystem broken;

        public override void Awake()
        {
            hole1 = transform.Find("pos1").GetComponent<SpriteRenderer>();
            hole2 = transform.Find("pos2").GetComponent<SpriteRenderer>();
            broken = transform.Find("brokenAni").GetComponent<ParticleSystem>();
            hole1.enabled = false;
            hole2.enabled = false;
            base.Awake();
        }

        public override void SetupSequence()
        {
            hole1.enabled = false;
            hole2.enabled = false;
            mySequence = DOTween.Sequence()
                .AppendCallback(() => { hole1.enabled = true; })
                .AppendInterval(0.5f)
                .AppendCallback(() => { hole2.enabled = true; })
                .AppendInterval(1.0f)
                .AppendCallback(() => broken.Play())
                .AppendInterval(0.2f)
                .AppendCallback(() => { hole1.enabled = false; hole2.enabled = false; })
                .SetAutoKill(false)
                .Pause();
            //Debug.Log("AniGlassBroken override void SetupSequence()");
        }

 


        [ContextMenu(nameof(AniPlay))]
        public override void AniPlay(Vector3 pos, CancellationToken _cancellationToken)
        {
            AniStart(_cancellationToken).Forget();
        }

        async UniTaskVoid AniStart(CancellationToken _cancellationToken)
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                //Debug.Log($"{this.name} .._cancellationToken.IsCancellationRequested : {_cancellationToken.IsCancellationRequested}");
                hole1.enabled = false;
                hole2.enabled = false;
                broken.Stop();
                return;
            }

            hole1.enabled = true;
            await UniTask.Delay(TimeSpan.FromSeconds(0.5f), cancellationToken: _cancellationToken);
            hole2.enabled = true;
            await UniTask.Delay(TimeSpan.FromSeconds(1.0f), cancellationToken: _cancellationToken);
            broken.Play();
            await UniTask.Delay(TimeSpan.FromSeconds(0.2f), cancellationToken: _cancellationToken);
            hole1.enabled = false;
            hole2.enabled = false;
        }


    }
}
