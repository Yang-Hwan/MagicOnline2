using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Threading;
using UnityEngine;

namespace Assets.Scripts.Sight.Vital.Pavilion
{
    public class AniKiss : MovStoryAni
    {
        Transform msg_tr;
        MovStoryCtrl movStoryCtrl;
        public Sprite sprite;

        public override void Awake()
        {
            msg_tr = transform.Find("Msg");
            msg_tr.GetComponent<SpriteRenderer>().sprite = sprite;

            msg_tr.gameObject.SetActive(false);

            storyAniPos = StoryAniPos.FourSplit;
            movStoryCtrl = FindObjectOfType<MovStoryCtrl>();
            mySequence = DOTween.Sequence();
        }


        public override void SetupSequence()
        {
            msg_tr.gameObject.SetActive(false);
            mySequence = DOTween.Sequence()
                .Append(msg_tr.DOScale(new Vector3(0.3f, 0.3f, 0.3f), 0.1f).OnComplete(() => msg_tr.gameObject.SetActive(true)))
                .Append(msg_tr.DOScale(new Vector3(0.25f, 0.33f, 0.3f), 0.5f).SetEase(Ease.OutQuad))
                .Append(msg_tr.DOScale(new Vector3(0.3f, 0.3f, 0.3f), 0.5f).SetEase(Ease.OutQuad).SetDelay(0.5f).OnComplete(() => msg_tr.gameObject.SetActive(false)))
                //.AppendCallback(() => { msg_tr.gameObject.SetActive(true); msg_tr.position = _pos; })
                //.AppendInterval(2.0f)
                //.AppendCallback(() => msg_tr.gameObject.SetActive(false))
                .SetAutoKill(false)
                .Pause();
            Debug.Log("AniKiss override void SetupSequence()");
        }


        [ContextMenu(nameof(AniPlay))]
        public override void AniPlay(Vector3 pos, CancellationToken _cancellationToken)
        {
            Debug.Log("11" + nameof(AniPlay));
            if (_cancellationToken.IsCancellationRequested)
            {
                Debug.Log($"{this.name} .._cancellationToken.IsCancellationRequested : {_cancellationToken.IsCancellationRequested}");
                mySequence.Kill();
                msg_tr.gameObject.SetActive(false);
                return;
            }

            mySequence
                .Append(msg_tr.DOScale(new Vector3(0.3f, 0.3f, 0.3f), 0.1f).OnComplete(() => msg_tr.gameObject.SetActive(true)))
                .Append(msg_tr.DOScale(new Vector3(0.25f, 0.33f, 0.3f), 0.5f).SetEase(Ease.OutQuad))
                .Append(msg_tr.DOScale(new Vector3(0.3f, 0.3f, 0.3f), 0.5f).SetEase(Ease.OutQuad).SetDelay(0.5f).OnComplete(() => msg_tr.gameObject.SetActive(false)));

            //AniStart(pos).Forget();
        }

        //async UniTaskVoid AniStart(Vector3 pos)
        //{
        //    msg_tr.gameObject.SetActive(true);
        //    msg_tr.localPosition = pos;
        //    await UniTask.Delay(TimeSpan.FromSeconds(2.0f));
        //    msg_tr.gameObject.SetActive(false);
        //}
    }

}
