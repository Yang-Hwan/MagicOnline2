using Cysharp.Threading.Tasks;
using System;
using UnityEngine;

namespace Assets.Scripts.Test.BallReactAni
{
    public class HeartAni : BallMovStoryAni
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
        }

        [ContextMenu(nameof(AniPlay))]
        public override void AniPlay(Vector3 pos)
        {
            Debug.Log("11" + nameof(AniPlay));
            AniStart().Forget();
        }

        async UniTaskVoid AniStart()
        {
            float a = 0;
            heart.Play();
            bg.enabled = true;
            msg.enabled = true;

            while (a < 1)
            {
                a += 0.05f;
                bg.color = new Color(1, 1, 1, a);
                await UniTask.Yield();
            }
            await UniTask.Delay(TimeSpan.FromSeconds(3.0f));
            heart.Stop();
            bg.color = new Color(1, 1, 1, 0);
            bg.enabled = false;
            msg.enabled = false;
        }

    }
}
