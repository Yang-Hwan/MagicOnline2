using Cysharp.Threading.Tasks;
using System;
using UnityEngine;


namespace Assets.Scripts.Test.BallReactAni
{
    public class HeHeAni : BallMovStoryAni
    {
        Transform msg_tr;
        MovStoryCtrl  movStoryCtrl;

        public override void Awake()
        {
            msg_tr = transform.Find("Msg");
            msg_tr.gameObject.SetActive(false);
            storyAniPos = StoryAniPos.FourSplit;
            movStoryCtrl = FindObjectOfType<MovStoryCtrl>();
        }

        [ContextMenu(nameof(AniPlay))]
        public override void AniPlay(Vector3 pos)
        {
            Debug.Log("11" + nameof(AniPlay));
            AniStart(pos).Forget();
        }

        async UniTaskVoid AniStart(Vector3 pos)
        {
            msg_tr.gameObject.SetActive(true);
            msg_tr.localPosition = pos;
            await UniTask.Delay(TimeSpan.FromSeconds(2.0f));
            msg_tr.gameObject.SetActive(false);

        }




    }
}
