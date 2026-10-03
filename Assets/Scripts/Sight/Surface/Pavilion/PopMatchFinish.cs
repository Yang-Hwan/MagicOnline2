using System;
using Assets.Scripts.Often;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using System.Threading;
using Assets.Scripts.Exert.Network;

namespace Assets.Scripts.Sight.Surface.Pavilion
{
    public class PopMatchFinish : MonoBehaviour
    {
        [SerializeField] RectTransform rectTransform;
        [SerializeField] TextMeshProUGUI txtResult;
        [SerializeField] TextMeshProUGUI txtBrief;

        private void Awake()
        {
            MatchResultClose();
        }

        void BindView()
        {
            txtResult = transform.Find("Result").GetComponent<TextMeshProUGUI>();
            txtBrief = transform.Find("Brief").GetComponent<TextMeshProUGUI>();
            rectTransform = transform.GetComponent<RectTransform>();
        }

        public void MatchResultView(int isWin, int matchCoin, int inning, int minute, float average, int highRun)
        {
            BindView();
            rectTransform.anchoredPosition = new Vector2(0, 0);
            string result = string.Empty;
            switch (isWin)
            {
                case -1: result = "패배 하였습니다";break;
                case 0: result = "비겼습니다"; break;
                case 1: result = "승리 하였습니다"; break;
            }
            string brief = $"상금: {matchCoin}      이닝: {inning}       시간: {minute}       평균: {average}       하이런: {highRun}점";
            txtResult.text = result;
            txtBrief.text = brief;
            //WaitAfterClose(10f).Forget();
        }


        public void PracticeResultView(string winnerName, int targetHit)
        {
            BindView();
            rectTransform.anchoredPosition = Vector2.zero;
            txtResult.text = "승리하였습니다";
            txtBrief.text = $"{winnerName} · 목표 {targetHit}점 달성";
            transform.parent.SetAsLastSibling();
            transform.SetAsLastSibling();
        }

        public void OnlineResultView(int outcome, int ownScore, int otherScore)
        {
            BindView();
            rectTransform.anchoredPosition = Vector2.zero;
            txtResult.text = outcome > 0 ? "승리하였습니다" : outcome < 0 ? "패배하였습니다" : "비겼습니다";
            txtBrief.text = $"{ownScore} : {otherScore} · 친선 경기 (코인·전적 정산 없음)";
            transform.parent.SetAsLastSibling(); transform.SetAsLastSibling();
        }

        Button rematchButton, archiveButton;
        TMP_Text onlineStatus;
        Func<bool> canRematch, canArchive;
        Func<string> rematchStatus, archiveStatus;
        public void ConfigureOnlineActions(Action rematch, Func<bool> canRequest, Func<string> status,
            Action archive, Func<bool> canSave, Func<string> savedStatus)
        {
            canRematch = canRequest; canArchive = canSave; rematchStatus = status; archiveStatus = savedStatus;
            if (rematchButton) return;
            rematchButton = OnlineButton("Rematch", "재대전 요청", -210, rematch);
            archiveButton = OnlineButton("Archive", "시합 저장", 0, archive);
            OnlineButton("Leave", "나가기", 210, PracticeSceneFlow.ReturnToMenu);
            onlineStatus = Instantiate(txtBrief, transform);
            onlineStatus.name = "Online Actions Status";
            onlineStatus.rectTransform.anchoredPosition = new Vector2(0, -160);
            onlineStatus.rectTransform.sizeDelta = new Vector2(900, 80);
            onlineStatus.fontSize = 20; onlineStatus.raycastTarget = false;
            onlineStatus.alignment = TextAlignmentOptions.Center;
            Update();
        }
        Button OnlineButton(string name, string label, float x, Action clicked)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(transform, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(x, -95); rect.sizeDelta = new Vector2(195, 48);
            obj.GetComponent<Image>().color = new Color(.08f, .3f, .35f, 1);
            var button = obj.GetComponent<Button>(); button.targetGraphic = obj.GetComponent<Image>();
            button.onClick.AddListener(() => clicked());
            var text = Instantiate(txtBrief, obj.transform);
            text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            text.fontSize = 23; text.alignment = TextAlignmentOptions.Center; text.text = label; text.raycastTarget = false;
            return button;
        }
        void Update()
        {
            if (!rematchButton) return;
            rematchButton.interactable = canRematch(); archiveButton.interactable = canArchive();
            onlineStatus.text = rematchStatus() + "\n" + archiveStatus();
        }

        public void MatchResultClose()
        {
            BindView();
            txtResult.text = string.Empty;
            txtBrief.text = string.Empty;
            rectTransform.anchoredPosition = new Vector2(0, 1000f);
        }

        async UniTaskVoid WaitAfterClose(float sec)
        {
            await UniTask.WaitForSeconds(sec);
            txtResult.text = string.Empty;
            txtBrief.text = string.Empty;
            rectTransform.anchoredPosition = new Vector2(0, 1000f);

        }
    }
}
