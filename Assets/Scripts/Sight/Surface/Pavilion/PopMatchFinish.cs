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
        [SerializeField] LevelNumberView levelNumberPrefab;
        RectTransform levelUpContent;
        RawImage resultArtwork;
        Image[] legacyPanels;
        bool[] legacyPanelEnabled;

        // Keep unresolved/cancelled results readable; only confirmed outcomes use art.
        void ShowResultArtwork(string title)
        {
            ClearLevelUp();
            string asset = title.Contains("승리") ? "match-victory" :
                title.Contains("패배") ? "match-defeat" :
                title.Contains("비겼") || title.Contains("무승부") ? "match-draw" : null;
            var texture = asset == null ? null : Resources.Load<Texture2D>("Gameplay/" + asset);
            if (legacyPanels == null)
            {
                legacyPanels = GetComponentsInChildren<Image>(true);
                legacyPanelEnabled = new bool[legacyPanels.Length];
                for (int i = 0; i < legacyPanels.Length; i++) legacyPanelEnabled[i] = legacyPanels[i].enabled;
            }
            for (int i = 0; i < legacyPanels.Length; i++)
                if (legacyPanels[i]) legacyPanels[i].enabled = !texture && legacyPanelEnabled[i];
            txtResult.enabled = !texture;
            txtBrief.enabled = !texture;
            if (!texture)
            {
                if (resultArtwork) resultArtwork.gameObject.SetActive(false);
                return;
            }
            if (!resultArtwork)
            {
                var obj = new GameObject("Result Graffiti", typeof(RectTransform), typeof(RawImage));
                obj.transform.SetParent(transform, false);
                resultArtwork = obj.GetComponent<RawImage>();
                resultArtwork.raycastTarget = false;
            }
            resultArtwork.texture = texture;
            var rect = resultArtwork.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            float width = Mathf.Min(700f, rectTransform.rect.width * .8f);
            rect.sizeDelta = new Vector2(width, width * texture.height / texture.width);
            resultArtwork.gameObject.SetActive(true);
        }

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
            ShowResultArtwork(result);
            //WaitAfterClose(10f).Forget();
        }

        public void MatchStatusView(string title, string detail)
        {
            BindView();
            rectTransform.anchoredPosition = Vector2.zero;
            txtResult.text = title;
            txtBrief.text = detail;
            ShowResultArtwork(title);
        }

        public void ShowLevelUp(Assets.Scripts.Exert.BackSys.BackendMatchHistory.LevelUpAward award)
        {
            var crest = Resources.Load<Texture2D>("Gameplay/level-up-crest");
            string amount = award.Amount > 0 ? "+" + award.Amount.ToString("N0") : "";
            if (!crest || !levelNumberPrefab || !resultArtwork || award.Level > 9999)
            {
                MatchStatusView($"레벨업! Lv. {award.Level}", $"{award.Currency} {amount}".Trim());
                return;
            }

            resultArtwork.gameObject.SetActive(false);
            var obj = new GameObject("Level Up", typeof(RectTransform), typeof(RawImage));
            levelUpContent = (RectTransform)obj.transform;
            levelUpContent.SetParent(transform, false);
            var art = obj.GetComponent<RawImage>();
            art.texture = crest;
            art.raycastTarget = false;
            float width = Mathf.Min(540f, rectTransform.rect.width * .75f);
            float height = width * crest.height / crest.width;
            levelUpContent.sizeDelta = new Vector2(width, height);
            levelUpContent.anchoredPosition = Vector2.zero;

            var number = Instantiate(levelNumberPrefab, levelUpContent);
            var numberRect = (RectTransform)number.transform;
            numberRect.sizeDelta = new Vector2(width * .41f, height * .26f);
            numberRect.anchoredPosition = new Vector2(0, -height * .14f);
            number.SetLevel(award.Level);
            if (award.Amount <= 0) return;

            string iconPath = award.Currency == "Jem" || award.Currency == "Coin"
                ? "Gameplay/level-up-" + award.Currency : "Menu/Mail/" + award.Currency;
            var icon = Resources.Load<Texture2D>(iconPath);
            var label = Instantiate(txtBrief, levelUpContent);
            label.name = "Immediate Reward";
            label.enabled = true;
            label.text = icon ? amount : award.Currency + " " + amount;
            label.fontSize = height * .086f;
            label.enableAutoSizing = false;
            label.fontStyle = FontStyles.Bold;
            label.color = new Color(1f, .91f, .59f);
            label.alignment = TextAlignmentOptions.Center;
            label.margin = Vector4.zero;
            label.raycastTarget = false;
            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            float textWidth = label.GetPreferredValues(label.text).x + 4f;
            float iconWidth = icon ? label.fontSize * 1.15f : 0;
            float gap = icon ? label.fontSize * .15f : 0;
            float y = -height * .56f;
            rect.sizeDelta = new Vector2(textWidth, label.fontSize * 1.6f);
            rect.anchoredPosition = new Vector2((iconWidth + gap) / 2, y);
            if (icon)
            {
                var badge = new GameObject("Reward Currency", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                badge.transform.SetParent(levelUpContent, false);
                badge.texture = icon;
                badge.raycastTarget = false;
                badge.rectTransform.sizeDelta = new Vector2(iconWidth, iconWidth * icon.height / icon.width);
                badge.rectTransform.anchoredPosition = new Vector2(-(textWidth + gap) / 2, y);
            }
        }

        void ClearLevelUp()
        {
            if (!levelUpContent) return;
            levelUpContent.gameObject.SetActive(false);
            Destroy(levelUpContent.gameObject);
            levelUpContent = null;
        }

        void OnDisable() => ClearLevelUp();


        public void PracticeResultView(string winnerName, int targetHit, bool timedOut = false)
        {
            BindView();
            rectTransform.anchoredPosition = Vector2.zero;
            txtResult.text = timedOut ? "연습 종료" : "승리하였습니다";
            txtBrief.text = timedOut ? "연습 시간이 종료되었습니다" : $"{winnerName} · 목표 {targetHit}점 달성";
            ShowResultArtwork(txtResult.text);
            transform.parent.SetAsLastSibling();
            transform.SetAsLastSibling();
        }

        public void OnlineResultView(int outcome, int ownScore, int otherScore)
        {
            BindView();
            rectTransform.anchoredPosition = Vector2.zero;
            txtResult.text = outcome > 0 ? "승리하였습니다" : outcome < 0 ? "패배하였습니다" : "비겼습니다";
            txtBrief.text = $"{ownScore} : {otherScore} · 친선 경기 (코인·전적 정산 없음)";
            ShowResultArtwork(txtResult.text);
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
            onlineStatus.enabled = true;
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
            text.enabled = true;
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
            ClearLevelUp();
            BindView();
            txtResult.text = string.Empty;
            txtBrief.text = string.Empty;
            if (resultArtwork) resultArtwork.gameObject.SetActive(false);
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
