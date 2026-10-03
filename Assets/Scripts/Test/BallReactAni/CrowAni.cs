using Cysharp.Threading.Tasks;
using System;
using UnityEngine;

namespace Assets.Scripts.Test.BallReactAni
{
    public class CrowAni : BallMovStoryAni
    {



        public Transform egg;
        public Transform bird;
        float st = -2.1f;


        public override void Awake()
        {
            egg = transform.Find("egg");
            bird = transform.Find("bird");

            egg.localPosition = new Vector3(st, 0.1f, 0.31f);
            bird.localPosition = new Vector3(st, 0.1f, 0.31f);
        }

        [ContextMenu(nameof(AniPlay))]
        public override void AniPlay(Vector3 pos)
        {
            AniStart().Forget();
        }


        async UniTaskVoid AniStart()
        {
            float des = 2.0f;
            Vector3 destination = new Vector3(des, 0.1f, 0.31f);
            bird.gameObject.SetActive(true);
            egg.gameObject.SetActive(true);
            egg.localPosition = new Vector3(st, 0.1f, 0.31f);
            bird.localPosition = new Vector3(st, 0.1f, 0.31f);
            while (bird.localPosition.x < des)
            {
                egg.localPosition = Vector3.MoveTowards(egg.position, destination, 0.01f);
                bird.localPosition = Vector3.MoveTowards(bird.position, destination, 0.01f);
                await UniTask.Yield();
            }
            await UniTask.Delay(TimeSpan.FromSeconds(0.1f));
            bird.gameObject.SetActive(false);
            egg.gameObject.SetActive(false);
        }

    }
}
