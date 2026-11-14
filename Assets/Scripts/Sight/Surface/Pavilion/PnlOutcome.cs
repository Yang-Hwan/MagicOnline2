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
using System.Linq;
using Assets.Scripts.Prack.BackSys;

namespace Assets.Scripts.Sight.Surface.Pavilion
{
    public class PnlOutcome : MonoBehaviour
    {


        [SerializeField] GesPnl ges;
        [SerializeField] TextMeshProUGUI txtResult;
        [SerializeField] BoxPlayerCard playerSelf;
        [SerializeField] BoxPlayerCard playerOther;
        [SerializeField] TextMeshProUGUI txtCountDown;
        TextMeshProUGUI settlementText;
        [SerializeField] Button btnPlay;
        [SerializeField] Button btnMain;
        Button btnSaveMatchReplay;
        bool opponentUnavailable;
        int localAvatarSeat;
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
            settlementText = UiText(transform, "SettlementStatus", 20, new Vector2(0, -255), new Vector2(760, 45));
            RematchLoungeView.Install(transform, LoungeState,
                () => { if (btnPlay.interactable) btnPlay.onClick.Invoke(); },
                () => { if (btnSaveMatchReplay.interactable) btnSaveMatchReplay.onClick.Invoke(); },
                () => btnMain.onClick.Invoke());

            //NetworkManager.network.OnNetwork += NetworkManager_network_OnNetwork;
            //PoolCoach.Instance.OnSetPlayer += PoolCoach_OnSetPlayer;

        }

        bool checkingEntryCoin, entryCoinChecked, leavingOutcome;
        int outcomeVersion;
        bool returnToLobbyScheduled;
        float nextEntryCoinCheck;

        bool Friendly => NetworkManager.TryGetExistingNetwork(out var net) && net is PunNetwork pun && pun.IsFriendly;

        async void CheckRematchEntryCoin()
        {
            if (Friendly || NetworkManager.TryGetExistingNetwork(out var freeNet) && freeNet is PunNetwork free && free.IsGuildWar) { entryCoinChecked = true; return; }
            var history = Assets.Scripts.Exert.BackSys.BackendMatchHistory.Instance;
            string id = history.LastMatchId;
            if (leavingOutcome || checkingEntryCoin || entryCoinChecked ||
                Time.unscaledTime < nextEntryCoinCheck || !history.IsSettled(id)) return;
            checkingEntryCoin = true;
            int version = outcomeVersion;
            try
            {
                // Read the settled match's entry fee and a fresh wallet, never the in-game preview.
                var state = await history.GetState(id);
                if (state.Phase != "Applied" || state.Rules == null) return;
                await BackendGame.Instance.LoadAsync();
                if (!this || !isActiveAndEnabled || version != outcomeVersion ||
                    history.LastMatchId != id || leavingOutcome) return;
                long balance = BackendGame.Instance.UserMainData.Coin;
                if (balance <= 0 || balance < state.Rules.EntryCoin)
                {
                    Debug.Log($"[재대결] 입장 코인 부족으로 자동 퇴장: 보유 {balance}, 필요 {state.Rules.EntryCoin}");
                    btnMain_Click();
                    return;
                }
                entryCoinChecked = true;
            }
            catch (Exception e) { if (this) Debug.LogWarning("재대결 입장 코인 확인 재시도: " + e.Message); }
            finally
            {
                if (this && version == outcomeVersion)
                {
                    checkingEntryCoin = false;
                    nextEntryCoinCheck = Time.unscaledTime + 3;
                }
            }
        }

        private void OnEnable()
        {
            outcomeVersion++;
            returnToLobbyScheduled = false;
            checkingEntryCoin = entryCoinChecked = leavingOutcome = false;
            nextEntryCoinCheck = 0;
            btnPlay.interactable = false;

            matchReplayPanel = PnlMatch.Instance;
            if (btnSaveMatchReplay) btnSaveMatchReplay.gameObject.SetActive(true);

            NetworkManager.network.OnNetwork += NetworkManager_network_OnNetwork;
            PoolCoach.Instance.OnSetPlayer += PoolCoach_OnSetPlayer;
            BackendGame.Instance.onGameDataLoadEvent.AddListener(RefreshServerCards);

            opponentUnavailable = !PhotonNetwork.InRoom || PhotonNetwork.PlayerList.Length < 2 ||
                PhotonNetwork.PlayerList.Any(p => p.IsInactive);
            if (NetworkManager.TryGetExistingNetwork(out var resultNetwork) && resultNetwork is PunNetwork pun)
            {
                localAvatarSeat = pun.LoungeLocalSeat;
                pun.RecordDisplayedLegacyOutcome();
            }
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
            if (NetworkManager.TryGetExistingNetwork(out var confirmedNetwork) && confirmedNetwork is PunNetwork confirmed && confirmed.HasLegacyMatch)
                txtResult.text = confirmed.ConfirmedResultText ?? "경기 결과 확인 중";
            txtCountDown.text = "";

            Debug.Log($"PnlOutcome OnEnable... ...   ");
            RefreshServerCards();
        }

        RematchLoungeView.State LoungeState()
        {
            var own = PoolPlayer.mainPlayer; var other = PoolPlayer.otherPlayer;
            var hall = Assets.Scripts.Exert.BackSys.BackendChart.skillMatchData?.FirstOrDefault(h => h.HallIdx == NetworkManager.hallIdx);
            string Vote(PoolPlayer p) => p == null || p.oneMore == OneMore.Disable ? "재대결 불가" : p.oneMore == OneMore.Req ? "재대결 요청!" : "선택 대기 중";
            bool counting = int.TryParse(txtCountDown.text, out int seconds);
            bool unavailable = opponentUnavailable || other == null || other.oneMore == OneMore.Disable;
            bool requested = own != null && own.oneMore == OneMore.Req;
            NetworkManager.TryGetExistingNetwork(out var resultNet);
            var series = resultNet as PunNetwork;
            return new RematchLoungeView.State {
                Friendly = Friendly,
                Arena = hall?.HallName ?? "경기 결과", Result = txtResult.text.StartsWith("Master :") ? "경기 준비 중" : txtResult.text,
                Score = series != null ? series.ResultSeriesScore : "0 : 0",
                PrizeCoin = series?.ResultPrizeCoin,
                LocalAvatarSeat = localAvatarSeat,
                SelfName = own?.name ?? "플레이어", OtherName = other?.name ?? "상대 플레이어",
                SelfInfo = $"{own?.hitHigh ?? 0}연타",
                OtherInfo = series != null ? series.ResultDurationText : "--:--",
                SelfVote = counting ? "재대결 준비 완료" : Vote(own), OtherVote = unavailable ? "상대방이 나갔습니다" : counting ? "재대결 준비 완료" : Vote(other),
                Status = (series?.IsGuildWar??false)?"공식 길드전 종료 · 새 상대는 길드전에서 찾아주세요.": unavailable ? "상대와 재대결할 수 없습니다." :
                    counting ? (seconds > 0 ? $"{seconds}초 후 재대결을 시작합니다." : "재대결을 준비하고 있습니다.") :
                    requested ? "상대의 재대결 응답을 기다리고 있습니다." :
                    other?.oneMore == OneMore.Req ? "상대가 재대결을 요청했습니다" : "재대결을 선택해 주세요",
                Settlement = settlementText ? settlementText.text : "",
                CanRematch = !(series?.IsGuildWar??false) && btnPlay.interactable && !requested && !unavailable && !counting,
                CanSave = btnSaveMatchReplay.interactable, CanLeave = btnMain.interactable,
                Waiting = requested && !unavailable && !counting
            };
        }

        async void RefreshServerCards()
        {
            if (!isActiveAndEnabled || PoolPlayer.mainPlayer == null || PoolPlayer.otherPlayer == null) return;
            PoolCoach.Instance.Result(PoolPlayer.mainPlayer);
            string opponent = PoolPlayer.otherPlayer.uuid;
            try
            {
                await BackendGame.Instance.LoadOtherAsync(opponent);
                if (this && isActiveAndEnabled && PoolPlayer.otherPlayer?.uuid == opponent)
                    PoolCoach.Instance.Result(PoolPlayer.otherPlayer);
            }
            catch(Exception e) { if(this) Debug.LogWarning("상대 정산 표시 갱신 대기: " + e.Message); }
        }

        private void Update()
        {
            if (!settlementText)
                settlementText = UiText(transform, "SettlementStatus", 20, new Vector2(0, -255), new Vector2(760, 45));
            settlementText.text = Friendly ? "친선 경기 · 코인 및 공식 기록 미반영" : Assets.Scripts.Exert.BackSys.BackendMatchHistory.Instance.LastStatus;
            var history = Assets.Scripts.Exert.BackSys.BackendMatchHistory.Instance;
            CheckRematchEntryCoin();
            btnPlay.interactable = !leavingOutcome && entryCoinChecked && (Friendly || history.IsSettled(history.LastMatchId));
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
            BackendGame.Instance.onGameDataLoadEvent.RemoveListener(RefreshServerCards);
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
                case NetworkState.OpponentLeftRoom:
                    opponentUnavailable = true;
                    break;
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
                    // LeaveMatch and Photon's OnLeftRoom can both report this departure.
                    if (returnToLobbyScheduled) break;
                    returnToLobbyScheduled = true;
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
            int version = outcomeVersion;
            await Task.Delay(1000);
            if (!this || !isActiveAndEnabled || version != outcomeVersion) return;
            BackendDuty.Instance.ConsistPage(Friendly ? 0 : 2);
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
            if(NetworkManager.TryGetExistingNetwork(out var officialNet) && officialNet is PunNetwork official && official.IsGuildWar)return;
            if (leavingOutcome || !entryCoinChecked ||
                !Friendly && !Assets.Scripts.Exert.BackSys.BackendMatchHistory.Instance.IsSettled(
                    Assets.Scripts.Exert.BackSys.BackendMatchHistory.Instance.LastMatchId)) return;
            OneMore more = OneMore.Req;
            PoolPlayer.OneMoreReq(PoolPlayer.mainPlayer.playerId, more);
            playerSelf.SetPlayerMsg(more);
            int len = NetworkManager.network.OneMoreTimeReq();
            if(len < 2)
            {
                opponentUnavailable = true;
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
            if (leavingOutcome) return;
            leavingOutcome = true;
            entryCoinChecked = false;
            btnPlay.interactable = false;
            NetworkManager.network.LeaveMatch();
        }


        async UniTaskVoid CountDownStart(int sec)
        {
            if (leavingOutcome || !entryCoinChecked) return;
            int version = outcomeVersion;
            txtCountDown.text = $"{sec}";
            while(sec > 0)
            {
                await UniTask.WaitForSeconds(1);
                if (!this || !isActiveAndEnabled || version != outcomeVersion || leavingOutcome) return;
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
