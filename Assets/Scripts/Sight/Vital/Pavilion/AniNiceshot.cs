using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Threading;
using UnityEngine;

namespace Assets.Scripts.Sight.Vital.Pavilion
{
    public class AniNiceshot : MovStoryAni
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
            base.Awake();
        }

        public override void SetupSequence()
        {
            msg_tr.gameObject.SetActive(false);
            mySequence = DOTween.Sequence()
                .AppendCallback(() => { msg_tr.gameObject.SetActive(true); msg_tr.position = _pos; })
                .AppendInterval(2.0f)
                .AppendCallback(() => msg_tr.gameObject.SetActive(false))
                .SetAutoKill(false)
                .Pause();
            Debug.Log("AniNiceshot override void SetupSequence()");
        }

        [ContextMenu(nameof(AniPlay))]
        public override void AniPlay(Vector3 pos, CancellationToken _cancellationToken)
        {
            Debug.Log("11" + nameof(AniPlay));
            AniStart(pos, _cancellationToken).Forget();
        }

        async UniTaskVoid AniStart(Vector3 pos, CancellationToken _cancellationToken)
        {
            msg_tr.gameObject.SetActive(true);
            msg_tr.localPosition = pos;
            await UniTask.Delay(TimeSpan.FromSeconds(2.0f), cancellationToken: _cancellationToken);
            msg_tr.gameObject.SetActive(false);
        }
    }
}
