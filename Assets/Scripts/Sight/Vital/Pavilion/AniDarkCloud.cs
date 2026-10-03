using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Threading;
using UnityEngine;

namespace Assets.Scripts.Sight.Vital.Pavilion
{
    public class AniDarkCloud : MovStoryAni
    {



        public Transform targetPos;
        public Transform cloudPos;
        public GameObject cloud;
        public GameObject rain;

        public override void Awake()
        {
            cloudPos = transform.Find("cloudPos");
            cloud = transform.Find("cloudPos/cloud").gameObject;
            rain = transform.Find("cloudPos/rain").gameObject;


            base.Awake();

        }

        public override void SetupSequence()
        {
            cloud.SetActive(false); 
            rain.SetActive(false);
            mySequence = DOTween.Sequence()
                .AppendCallback(() => { cloud.SetActive(true); rain.SetActive(false); })
                .Append(cloudPos.DOLocalMove(_pos, 1.0f))
                //.AppendCallback(() => Debug.Log($" SetupSequence darkcloud pos : {_pos}... cloudPos local pos : {cloudPos.localPosition}"))
                .AppendCallback(() => { rain.SetActive(true); })
                .AppendInterval(2.0f)
                .AppendCallback(() => { cloud.SetActive(false); rain.SetActive(false); })
                .SetAutoKill(false)
                .Pause();
            //Debug.Log($"AniDarkCloud override void SetupSequence()  _pos : {_pos}");
        }

        public override void AniPlay(Vector3 pos, CancellationToken _cancellationToken)
        {
            AniStart(pos, _cancellationToken).Forget();
        }

        async UniTaskVoid AniStart(Vector3 pos, CancellationToken _cancellationToken)
        {

            if (_cancellationToken.IsCancellationRequested)
            {
                Debug.Log($"{this.name} .._cancellationToken.IsCancellationRequested : {_cancellationToken.IsCancellationRequested}");
                cloud.SetActive(false);
                rain.SetActive(false);
                return;
            }

            //Debug.Log($"=======");
            cloud.SetActive(true);
            rain.SetActive(false);
            await UniTask.Yield(cancellationToken: _cancellationToken);
            float dist = Vector3.Distance(cloudPos.localPosition, pos);
            //Debug.Log($"0 dist : {Vector3.Distance(transform.position, pos)}");
            while (dist > 0.002f)
            {
                dist = Vector3.Distance(cloudPos.localPosition, pos);
                //Debug.Log($"dist : {Vector3.Distance(transform.position, pos)}");
                cloudPos.localPosition = Vector3.Lerp(cloudPos.localPosition, pos, 0.05f);
                await UniTask.Yield(cancellationToken: _cancellationToken);
            }
     
            //Debug.Log($"aaaaaaaaaaaa");
            rain.SetActive(true);
           //rain.transform.localPosition = pos;
            await UniTask.Delay(TimeSpan.FromSeconds(2.5f), cancellationToken: _cancellationToken);
            cloud.SetActive(false);
            rain.SetActive(false);
        }
    

    }
}
