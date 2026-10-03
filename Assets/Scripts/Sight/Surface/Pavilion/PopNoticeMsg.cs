using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using System.Threading;
using Assets.Scripts.Exert.Network;
using Assets.Scripts.Often;

namespace Assets.Scripts.Sight.Surface.Pavilion
{
    public class PopNoticeMsg : MonoBehaviour
    {

        [SerializeField] RectTransform rectTransform;
        [SerializeField] TextMeshProUGUI txtMsg;

        private void Awake()
        {
            //rectTransform = transform.GetComponent<RectTransform>();
            rectTransform.anchoredPosition = new Vector2(0, 1300f);
            //txtMsg = transform.Find("TxtMsg").GetComponent<TextMeshProUGUI>();
            txtMsg.text = string.Empty;
        }

        public void NoticeView(NoticeHitBall notice)
        {
            WaitAfterClose(notice, 3f).Forget();
        }

        async UniTaskVoid WaitAfterClose(NoticeHitBall notice, float sec)
        {
            rectTransform.anchoredPosition = new Vector2(0, 0);
            string msg = string.Empty;
            switch (notice)
            {
                case NoticeHitBall.Foul:
                    msg = "파울 -1";
                    break;
                case NoticeHitBall.Finish:
                    msg = "3쿠션으로 끝내기 매치입니다.";
                    break;
                case NoticeHitBall.Survival:
                    msg = "초구는 후구 공격까지 기회가 주어집니다.";
                    break;
            }
            txtMsg.text = msg;

            await UniTask.WaitForSeconds(sec);
            txtMsg.text = string.Empty;
            rectTransform.anchoredPosition = new Vector2(0, 1300f);

        }

    }
}
