using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Test.BallReactAni
{
    public class GrassBrokenAni : BallMovStoryAni
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
        }

 

        [ContextMenu(nameof(AniPlay))]
        public override void AniPlay(Vector3 pos)
        {
            AniStart().Forget();
        }

        async UniTaskVoid AniStart()
        {
            hole1.enabled = true;
            await UniTask.Delay(TimeSpan.FromSeconds(0.5f));
            hole2.enabled = true;
            await UniTask.Delay(TimeSpan.FromSeconds(1.0f));
            broken.Play();
            await UniTask.Delay(TimeSpan.FromSeconds(0.2f));
            hole1.enabled = false;
            hole2.enabled = false;
        }
    }
}
