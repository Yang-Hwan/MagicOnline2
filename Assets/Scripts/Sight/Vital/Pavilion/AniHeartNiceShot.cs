using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Threading;
using UnityEngine;

namespace Assets.Scripts.Sight.Vital.Pavilion
{
    public class AniHeartNiceShot : MovStoryAni
    {


        public ParticleSystem heart;
        public SpriteRenderer bg;
        public SpriteRenderer msg;


        public override void Awake()
        {
            heart = transform.Find("heart").GetComponent<ParticleSystem>();
            bg = transform.Find("bg").GetComponent<SpriteRenderer>();
            msg = transform.Find("msg").GetComponent<SpriteRenderer>();
            bg.enabled = false;
            msg.enabled = false;
            base.Awake();
        }

        public override void SetupSequence()
        {
            bg.enabled = false;
            msg.enabled = false;
            mySequence = DOTween.Sequence()
                .AppendCallback(() => { bg.enabled = true; msg.enabled = true; })
                .AppendInterval(0.5f)
                .AppendCallback(() => { heart.Play(); })
                .Join(bg.DOFade(1f, 1f))
                .AppendInterval(2.0f)
                .Append(bg.DOFade(0f, 0.1f)) 
                .AppendCallback(() => { bg.enabled = false; msg.enabled = false; })
                .SetAutoKill(false)
                .Pause();
            //Debug.Log("AniHeartNiceShot override void SetupSequence()");
        }

        [ContextMenu(nameof(AniPlay))]
        public override void AniPlay(Vector3 pos, CancellationToken _cancellationToken)
        {
            Debug.Log("11" + nameof(AniPlay));
            AniStart(_cancellationToken).Forget();
        }

        async UniTaskVoid AniStart(CancellationToken _cancellationToken)
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                //Debug.Log($"{this.name} .._cancellationToken.IsCancellationRequested : {_cancellationToken.IsCancellationRequested}");
                bg.enabled = false;
                msg.enabled = false;
                heart.Stop();
                return;
            }

            float a = 0;
            heart.Play();
            bg.enabled = true;
            msg.enabled = true;

            while (a < 1)
            {
                a += 0.05f;
                bg.color = new Color(1, 1, 1, a);
                await UniTask.Yield(cancellationToken: _cancellationToken);
            }
            await UniTask.Delay(TimeSpan.FromSeconds(3.0f), cancellationToken: _cancellationToken);
            heart.Stop();
            bg.color = new Color(1, 1, 1, 0);
            bg.enabled = false;
            msg.enabled = false;
        }


    }
}
