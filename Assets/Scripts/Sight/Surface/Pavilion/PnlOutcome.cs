using Assets.Scripts.Exert.Match;
using Assets.Scripts.Exert.Network;
using Assets.Scripts.Often;
using Assets.Scripts.Sight.Surface.Arise;
using Assets.Scripts.Sight.Vital.Pavilion;
using Cysharp.Threading.Tasks;
using Photon.Pun;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Collections.Generic;
using System;

namespace Assets.Scripts.Sight.Surface.Pavilion
{
    public class PnlOutcome : MonoBehaviour
    {


        [SerializeField] GesPnl ges;
        [SerializeField] TextMeshProUGUI txtResult;
        [SerializeField] BoxPlayerCard playerSelf;
        [SerializeField] BoxPlayerCard playerOther;
        [SerializeField] TextMeshProUGUI txtCountDown;
        [SerializeField] Button btnPlay;
        [SerializeField] Button btnMain;
        Button btnSaveMatchReplay;
        PnlMatch matchReplayPanel;
        GameObject matchReplaySavePanel;
        TMP_InputField matchReplayTitle;
        string selectedMatchReplayFolder = "";
        int selectedMatchReplaySlot = -1;
        readonly List<Button> matchReplaySlotButtons = new List<Button>();

        private void Awake()
        {
            if (!NetworkManager.initialized)
            {
                enabled = false;
                return;
            }
            ges = transform.parent.GetComponent<GesPnl>();
            txtResult = transform.Find("TxtResult").GetComponent<TextMeshProUGUI>();
            txtCountDown = transform.Find("TxtCountDown").GetComponent<TextMeshProUGUI>();
            playerSelf = transform.Find("BoxPlayerCardSelf").GetComponent<BoxPlayerCard>();
            playerOther = transform.Find("BoxPlayerCardOther").GetComponent<BoxPlayerCard>();

            btnPlay = transform.Find("BoxBtns/BtnPlay").GetComponent<Button>();
            btnMain = transform.Find("BoxBtns/BtnMain").GetComponent<Button>();
            btnPlay.onClick.AddListener(btnPlay_Click);
            btnMain.onClick.AddListener(btnMain_Click);

            // The online match result is shown by PnlOutcome (승리했습니다 / 패배하였습니다).
            // Clone the existing result button so the save action uses the same visual style.
            var saveObject = Instantiate(btnMain.gameObject, btnMain.transform.parent, false);
            saveObject.name = "BtnSaveMatchReplay";
            saveObject.SetActive(true);
            btnSaveMatchReplay = saveObject.GetComponent<Button>();
            btnSaveMatchReplay.onClick.RemoveAllListeners();
            btnSaveMatchReplay.onClick.AddListener(() =>
            {
                if (!matchReplayPanel) matchReplayPanel = PnlMatch.Instance;
                if (matchReplayPanel && matchReplayPanel.CanSaveMatchReplay) OpenMatchReplaySavePanel();
            });
            var saveRect = saveObject.GetComponent<RectTransform>();
            saveRect.anchoredPosition = btnMain.GetComponent<RectTransform>().anchoredPosition + new Vector2(0, -58);
            var saveLabel = saveObject.GetComponentInChildren<TextMeshProUGUI>(true);
            if (saveLabel) saveLabel.text = "시합 저장";

            //NetworkManager.network.OnNetwork += NetworkManager_network_OnNetwork;
            //PoolCoach.Instance.OnSetPlayer += PoolCoach_OnSetPlayer;

        }

        private void OnEnable()
        {

            matchReplayPanel = PnlMatch.Instance;
            if (btnSaveMatchReplay) btnSaveMatchReplay.gameObject.SetActive(true);

            NetworkManager.network.OnNetwork += NetworkManager_network_OnNetwork;
            PoolCoach.Instance.OnSetPlayer += PoolCoach_OnSetPlayer;

            PoolPlayer.ResetOneMore();
            //NetworkManager.network.OnNetwork += NetworkManager_network_OnNetwork;
            PoolCoach.Instance.Result(PoolPlayer.mainPlayer);
            PoolCoach.Instance.Result(PoolPlayer.otherPlayer);
            int winId = PoolPlayer.GetHitWin();
            if (winId == -1)
            {
                txtResult.text = "비겼습니다";
            }
            else
            {
                //PoolPlayer.SetWinner(winId);
                txtResult.text = PoolPlayer.mainPlayer.isWinner ? "승리했습니다" : "패배하였습니다";
            }
            //txtResult.text = PoolPlayer.mainPlayer.isWinner ? "승리했습니다" : "패배했습니다";
            txtCountDown.text = "";

            Debug.Log($"PnlOutcome OnEnable... ...   ");
        }

        private void Update()
        {
            if (!matchReplayPanel)
                matchReplayPanel = PnlMatch.Instance;
            if (btnSaveMatchReplay)
            {
                btnSaveMatchReplay.gameObject.SetActive(true);
                btnSaveMatchReplay.interactable = matchReplayPanel && matchReplayPanel.CanSaveMatchReplay;
            }
        }

        void OpenMatchReplaySavePanel()
        {
            if (!matchReplayPanel || !matchReplayPanel.CanSaveMatchReplay) return;
            if (matchReplaySavePanel) Destroy(matchReplaySavePanel);
            selectedMatchReplayFolder = "";
            selectedMatchReplaySlot = -1;
            matchReplaySlotButtons.Clear();
            matchReplaySavePanel = UiRect(transform, "MatchReplaySavePanel", new Vector2(820, 500), Vector2.zero).gameObject;
            var bg = matchReplaySavePanel.AddComponent<Image>(); bg.color = new Color(.12f, .1f, .08f, .97f);
            matchReplaySavePanel.transform.SetAsLastSibling();
            UiText(matchReplaySavePanel.transform, "시합 리플레이 저장", 30, new Vector2(0, 215), new Vector2(760, 45));

            string root = Path.Combine(Application.persistentDataPath, "MatchReplays");
            var folders = new List<(string id, string name)> { ("", "기본") };
            string folderRoot = Path.Combine(root, "Folders");
            try
            {
                if (Directory.Exists(folderRoot))
                    foreach (var folder in Directory.GetDirectories(folderRoot))
                    {
                        string id = Path.GetFileName(folder), nameFile = Path.Combine(folder, "name.txt");
                        if (Guid.TryParseExact(id, "N", out _) && File.Exists(nameFile))
                            folders.Add((id, File.ReadAllText(nameFile).Trim()));
                    }
            }
            catch (IOException) { }
            for (int i = 0; i < folders.Count && i < 7; i++)
            {
                var folder = folders[i]; int index = i;
                MakeUiButton(matchReplaySavePanel.transform, "Folder" + i, folder.name,
                    new Vector2(-300 + i * 100, 165), new Vector2(94, 38), () =>
                    { selectedMatchReplayFolder = folders[index].id; RefreshMatchReplaySlotButtons(root); });
            }

            var titleRect = UiRect(matchReplaySavePanel.transform, "ReplayTitleInput", new Vector2(540, 42), new Vector2(0, 112));
            titleRect.gameObject.AddComponent<Image>().color = Color.white;
            matchReplayTitle = titleRect.gameObject.AddComponent<TMP_InputField>();
            matchReplayTitle.targetGraphic = titleRect.GetComponent<Image>();
            var titleText = UiText(titleRect, "TitleText", 23, Vector2.zero, new Vector2(520, 38));
            titleText.alignment = TextAlignmentOptions.MidlineLeft; titleText.color = Color.black;
            titleText.margin = new Vector4(12, 0, 8, 0);
            matchReplayTitle.textComponent = titleText;
            matchReplayTitle.text = DefaultMatchReplayTitle();

            for (int i = 0; i < 20; i++)
            {
                int slot = i;
                var slotButton = MakeUiButton(matchReplaySavePanel.transform, "ReplaySlot" + (i + 1), $"슬롯 {i + 1:00}",
                    new Vector2(-300 + (i % 5) * 150, 44 - (i / 5) * 62), new Vector2(136, 50),
                    () => { selectedMatchReplaySlot = slot; RefreshMatchReplaySlotButtons(root); });
                matchReplaySlotButtons.Add(slotButton);
            }
            RefreshMatchReplaySlotButtons(root);
            MakeUiButton(matchReplaySavePanel.transform, "SaveMatchReplay", "저장",
                new Vector2(-100, -220), new Vector2(150, 48), SaveSelectedMatchReplay);
            MakeUiButton(matchReplaySavePanel.transform, "CancelMatchReplay", "취소",
                new Vector2(100, -220), new Vector2(150, 48), () => Destroy(matchReplaySavePanel));
        }

        void RefreshMatchReplaySlotButtons(string root)
        {
            string dir = string.IsNullOrEmpty(selectedMatchReplayFolder) ? root : Path.Combine(root, "Folders", selectedMatchReplayFolder);
            for (int i = 0; i < matchReplaySlotButtons.Count; i++)
            {
                bool filled = File.Exists(Path.Combine(dir, $"slot{i + 1:00}.bin"));
                var image = matchReplaySlotButtons[i].GetComponent<Image>();
                image.color = selectedMatchReplaySlot == i ? new Color(.72f, .56f, .26f, 1) :
                    filled ? new Color(.48f, .38f, .24f, 1) : new Color(.18f, .32f, .35f, 1);
            }
        }

        void SaveSelectedMatchReplay()
        {
            if (selectedMatchReplaySlot < 0 || !matchReplayPanel) return;
            string root = Path.Combine(Application.persistentDataPath, "MatchReplays");
            string directory = string.IsNullOrEmpty(selectedMatchReplayFolder) ? root : Path.Combine(root, "Folders", selectedMatchReplayFolder);
            if (matchReplayPanel.SaveMatchReplay(selectedMatchReplaySlot, directory, matchReplayTitle ? matchReplayTitle.text : "대전 리플레이"))
            {
                Destroy(matchReplaySavePanel);
                if (btnSaveMatchReplay) btnSaveMatchReplay.gameObject.SetActive(false);
            }
        }

        string DefaultMatchReplayTitle()
        {
            var players = PoolPlayer.players;
            string left = players != null && players.Length > 0 ? players[0]?.name : null;
            string right = players != null && players.Length > 1 ? players[1]?.name : null;
            return $"{(string.IsNullOrWhiteSpace(left) ? "플레이어 1" : left)} vs {(string.IsNullOrWhiteSpace(right) ? "플레이어 2" : right)}";
        }

        RectTransform UiRect(Transform parent, string name, Vector2 size, Vector2 position)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>(); rect.sizeDelta = size; rect.anchoredPosition = position;
            return rect;
        }

        TextMeshProUGUI UiText(Transform parent, string name, float fontSize, Vector2 position, Vector2 size)
        {
            var rect = UiRect(parent, name, size, position);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = txtResult.font; text.fontSize = fontSize; text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
            return text;
        }

        Button MakeUiButton(Transform parent, string name, string label, Vector2 position, Vector2 size, Action click)
        {
            var rect = UiRect(parent, name, size, position);
            var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.18f, .32f, .35f, 1);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            button.onClick.AddListener(() => click());
            var text = UiText(rect, "Label", 19, Vector2.zero, size); text.text = label;
            return button;
        }

        private void OnDisable()
        {
            ges.Open(GesPnl.Pnl.Zero);

            if (NetworkManager.TryGetExistingNetwork(out var existingOnNetwork))
                existingOnNetwork.OnNetwork -= NetworkManager_network_OnNetwork;
            PoolCoach.Instance.OnSetPlayer -= PoolCoach_OnSetPlayer;
            //NetworkManager.network.OnNetwork -= NetworkManager_network_OnNetwork;
            //PoolCoach.Instance.OnSetPlayer -= PoolCoach_OnSetPlayer;
            Debug.Log($"PnlOutcome OnDisable... OnDisable... OnDisable... OnDisable... OnDisable... OnDisable...");

        }


        void NetworkManager_network_OnNetwork(NetworkState state)
        {
            Debug.Log("00.PnlOutcome NetworkManager_network_OnNetwork : " + state);
            switch (state)
            {
                case NetworkState.OpponentOneMoreReq:
                    OneMore more = PoolPlayer.otherPlayer.oneMore;
                    playerOther.SetPlayerMsg(more);

                    bool isOk = PoolPlayer.IsReMatch();
                    Debug.Log("Other IsReMatch : " + isOk);
                    if (isOk)
                    {
                        CountDownStart(3).Forget();
                    }
                    break;
                case NetworkState.LeftRoom:
                    PoolPlayer.PlayerScoreClear();
                    Debug.Log("11.PnlOutcome NetworkManager_network_OnNetwork : " + state);
                    WaitLoadScene(state);
                    break;
                case NetworkState.StartToPlay:
                    GotoPitch();
                    break;
            }
        }

        async void WaitLoadScene(NetworkState state)
        {
            await Task.Delay(1000);
            BackendDuty.Instance.ConsistPage(2);
            SceneMove.LoadScene(SceneNames.Consist);
            Debug.Log("22.PnlOutcome NetworkManager_network_OnNetwork : " + state);
        }

        void GotoPitch()
        {
            ges.Open(GesPnl.Pnl.Zero);
        }

        void PoolCoach_OnSetPlayer(PoolPlayer player)
        {
            //Debug.Log("PoolCoach_OnSetPlayer " + player.playerId);

            if (player.playerId == 0)
            {
                //player.name = NetworkManager.mainPlayer.nickname;
                playerSelf.SetPlayerInfo(player);
            }
            else
            {
                //player.name = NetworkManager.opponentPlayer.nickname;
                playerOther.SetPlayerInfo(player);
            }
        }

        void btnPlay_Click()
        {
            OneMore more = OneMore.Req;
            PoolPlayer.OneMoreReq(PoolPlayer.mainPlayer.playerId, more);
            playerSelf.SetPlayerMsg(more);
            int len = NetworkManager.network.OneMoreTimeReq();
            if(len < 2)
            {
                playerOther.SetPlayerMsg( OneMore.Disable);
                return;
            }
            bool isOk = PoolPlayer.IsReMatch();
            Debug.Log("Self IsReMatch : " + isOk);
            if (isOk)
            {
                CountDownStart(3).Forget();
            }
        }

        void btnMain_Click()
        {
            NetworkManager.network.LeaveMatch();
        }


        async UniTaskVoid CountDownStart(int sec)
        {
            txtCountDown.text = $"{sec}";
            while(sec > 0)
            {
                await UniTask.WaitForSeconds(1);
                sec--;
                txtCountDown.text = $"{sec}";
            }

            bool isMaster = PhotonNetwork.LocalPlayer == PhotonNetwork.MasterClient;
            txtResult.text = $"Master : {isMaster}";
            if (!isMaster)
            {
                NetworkManager.network.WaitCountdowned();
            }

        }
    }
}
