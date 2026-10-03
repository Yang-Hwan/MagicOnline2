using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Threading;
using UnityEngine;

namespace Assets.Scripts.Sight.Vital.Pavilion
{
    public class AniCrowFly : MovStoryAni
    {
        Vector3 pos_st, pos_en;
        public Transform egg;
        public Transform bird;
        float st = -2.1f;

        public override void Awake()
        {
            egg = transform.Find("egg");
            bird = transform.Find("bird");

            pos_st = new Vector3(-1.9f, 0.1f, 0.31f);
            pos_en = new Vector3(2.2f, 0.1f, 0.31f);

            egg.localPosition = pos_st;
            bird.localPosition = pos_st;
   

            base.Awake();

        }

        public override void SetupSequence()
        {
            bird.gameObject.SetActive(false);  
            egg.gameObject.SetActive(false);
            egg.localPosition = pos_st;
            bird.localPosition = pos_st;
            mySequence = DOTween.Sequence()
                .AppendCallback(() => { bird.gameObject.SetActive(true); bird.localPosition = pos_st; })
                .JoinCallback(() => { egg.gameObject.SetActive(true); egg.localPosition = pos_st; })
                .Append(bird.DOLocalMove(pos_en, 4f))
                .Join(egg.DOLocalMove(pos_en, 4f))
                .AppendCallback(() => { bird.gameObject.SetActive(false); bird.localPosition = pos_st; })
                .JoinCallback(() => {egg.gameObject.SetActive(false); egg.localPosition = pos_st; })
                .SetAutoKill(false)
                .Pause();
            //Debug.Log("AniCrowFly override void SetupSequence()");
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
                bird.gameObject.SetActive(false);
                egg.gameObject.SetActive(false);
                return;
            }

            float des = 2.0f;
            Vector3 destination = new Vector3(des, 0.1f, 0.31f);
            bird.gameObject.SetActive(true);
            egg.gameObject.SetActive(true);
            bird.localPosition = new Vector3(st, 0.1f, 0.31f);
            egg.localPosition = new Vector3(st, 0.1f, 0.31f);

            while (bird.localPosition.x < des)
            {
                egg.localPosition = Vector3.MoveTowards(egg.position, destination, 0.01f);
                bird.localPosition = Vector3.MoveTowards(bird.position, destination, 0.01f);
                await UniTask.Yield(cancellationToken: _cancellationToken);
            }
            await UniTask.Delay(TimeSpan.FromSeconds(0.1f), cancellationToken: _cancellationToken);
            bird.gameObject.SetActive(false);
            egg.gameObject.SetActive(false);
        }


    }
}
