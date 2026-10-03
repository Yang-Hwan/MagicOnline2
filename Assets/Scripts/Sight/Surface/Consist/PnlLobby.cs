using Assets.Scripts.Exert.BackSys;
using Assets.Scripts.Exert.Match;
using Assets.Scripts.Exert.Network;
using Assets.Scripts.Often;
using Assets.Scripts.Prack.BackSys.ChartData;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Sight.Surface.Consist
{
    public class PnlLobby : MonoBehaviour
    {

        [SerializeField] GameObject pnlMainSwipe;
        
        Button btnBack;
        Image imgPlayerSelf;
        Image imgPlayerOther;
        TextMeshProUGUI txtNickNmSelf;
        TextMeshProUGUI txtNickNmOther;
        TextMeshProUGUI txtTitle;
        TextMeshProUGUI txtCounter;
        CancellationTokenSource cancelSource;

        private void Awake()
        {
            txtTitle = transform.Find("TxtTitle").GetComponent<TextMeshProUGUI>();
            txtCounter = transform.Find("TxtCounter").GetComponent<TextMeshProUGUI>();
            // Counter's original numeric font has no Korean glyphs for preparation/failure messages.
            txtCounter.font = txtTitle.font;
            txtCounter.enableAutoSizing = true; txtCounter.fontSizeMin = 22; txtCounter.fontSizeMax = 64;
            txtTitle.enableAutoSizing = true; txtTitle.fontSizeMin = 24;
            imgPlayerSelf = transform.Find("PnlPlayerSelf/Avatar").GetComponent<Image>();
            imgPlayerOther = transform.Find("PnlPlayerOther/Avatar").GetComponent<Image>();
            txtNickNmSelf = transform.Find("PnlPlayerSelf/TxtName").GetComponent<TextMeshProUGUI>();
            txtNickNmOther = transform.Find("PnlPlayerOther/TxtName").GetComponent<TextMeshProUGUI>();

            btnBack = transform.Find("BtnBack").GetComponent<Button>();
            btnBack.onClick.AddListener(btnBack_Click);

        }

        private void OnEnable()
        {
            txtCounter.text = "입장 중";
            txtNickNmSelf.text = UserInfo.DisplayName;
            txtNickNmOther.text = string.Empty;
            NetworkManager.network.OnNetwork += NetworkManager_network_OnNetwork;
            NetworkManager.network.OnNetworkPlayerList += NetworkManager_OnNetworkPlayerList;
            SkillMatchData hall = BackendChart.skillMatchData.Where(r => r.HallIdx.Equals(NetworkManager.hallIdx)).FirstOrDefault();
            Debug.Log("PnlLobby.OnEnable hallidx : "+ NetworkManager.hallIdx + " .. hall : " + hall?.ToString() ?? "xx");
            if (hall == null) return;
            //txtTitle.text = "";
            txtTitle.text = hall.HallName + (PracticeMatchTestRoute.TargetWin ? "\n1점 승패 검증 · 친선" : PracticeMatchTestRoute.QuickMatch ? "\n1분 종료 검증 · 친선" : PracticeMatchTestRoute.Enabled ? "\n새 대전 테스트 · 친선" : "");
        }

        private void OnDisable()
        {
            CountDownAbort();
            if (NetworkManager.TryGetExistingNetwork(out var existingOnNetwork))
                existingOnNetwork.OnNetwork -= NetworkManager_network_OnNetwork;
            if (NetworkManager.TryGetExistingNetwork(out var existingOnNetworkPlayerList))
                existingOnNetworkPlayerList.OnNetworkPlayerList -= NetworkManager_OnNetworkPlayerList;

        }

        void NetworkManager_network_OnNetwork(NetworkState state)
        {
            //Debug.Log("PnlWaitRoom NetworkManager_network_OnNetwork state : " + state.ToString());
            switch (state)
            {
                case NetworkState.OpponentReadToPlay:
                    //Debug.Log("NetworkManager_network_OnNetwork " + NetworkManager.mainPlayer.ToString());
                    CountDownStart();
                    break;
                case NetworkState.OpponentLeftRoom:
                    CountDownAbort();
                    break;
                case NetworkState.JoinedToRoom:
                    txtCounter.text = "상대 대기";
                    break;
                case NetworkState.RoomCreateFailed:
                case NetworkState.JoinRoomFailed:
                    CountDownAbort();
                    txtCounter.text = "입장 실패\n뒤로 가서 다시 시도해 주세요";
                    break;
                case NetworkState.LostConnection:
                case NetworkState.Disconnected:
                    CountDownAbort();
                    txtCounter.text = "연결 끊김\n뒤로 가서 다시 시도해 주세요";
                    break;
                case NetworkState.StartToPlay:
                    GotoPitch();
                    break;
                case NetworkState.LeftRoom:
                    Debug.Log("NetworkManager_network_OnNetwork " + NetworkState.LeftRoom.ToString());
         

                    pnlMainSwipe.SetActive(true);
                    gameObject.SetActive(false);

                    break;
            }
        }

        void CountDownAbort()
        {
            cancelSource?.Cancel();
            cancelSource?.Dispose();
            cancelSource = null;


            //if (coroutine != null)
            //    StopCoroutine(coroutine);
        }

        void NetworkManager_OnNetworkPlayerList(PlayerProfile[] players)
        {
            txtCounter.text = players.Length < 2 ? "상대 대기" : "상대 확인 중";
            txtNickNmSelf.text = UserInfo.DisplayName;
            txtNickNmOther.text = "";

            for (int i = 0; i < players.Length; i++)
            {
                bool isSelf = players[i].nickName == NetworkManager.mainPlayer.nickName;
                Debug.Log($"NetworkManager_OnNetworkPlayerList {i}, p.nick : {players[i].nickName}, main.nick : {NetworkManager.mainPlayer.nickName}.. isSelf : {isSelf} ");
                if (isSelf)
                {
                    txtNickNmSelf.text = UserInfo.DisplayName;    // players[i].nickname;
                }
                else
                {
                    PoolPlayer.OnGotoPlayWithPlayer(players[i]);
                    txtNickNmOther.text = PoolPlayer.otherPlayer.name;  // players[i].nickname;
                }
            }
        }

        void CountDownStart()
        {
            CountDownAbort();
            cancelSource = new CancellationTokenSource();
            AsyncWaitCountDown(3).Forget();
            //iii++;
            //Debug.Log("CountDownStart iii : " + iii);
            //coroutine = WaitCountDownload(3);
            //StartCoroutine(coroutine);
        }

        async UniTaskVoid AsyncWaitCountDown(int sec)
        {
            var token = cancelSource.Token;
            txtCounter.text = sec.ToString();
            while (sec > 0)
            {
                if (await UniTask.Delay(1000, cancellationToken: token).SuppressCancellationThrow()) return;
                sec--;
                txtCounter.text = sec.ToString();
            }
            if (!token.IsCancellationRequested && isActiveAndEnabled)
                NetworkManager.network.WaitCountdowned();
        }


        void btnBack_Click()
        {
            CountDownAbort();
            NetworkManager.network.LeaveMatch();

            Debug.Log("btnBack_Click .. btnBack_Click : " + pnlMainSwipe.name);
 
        }

        void Update()
        {
            if (!PracticeMatchTestRoute.Enabled) return;
            var transport = FindAnyObjectByType<PracticeMatchSetupTransport>();
            if (!transport || transport.Session == null) return;
            txtCounter.text = transport.Session.Phase == MatchStartPhase.Aborted ?
                transport.Session.AbortReason + "\n뒤로 가서 다시 시도해 주세요" : "새 경기장 준비 중";
        }

        // 경기장으로 이동
        void GotoPitch()
        {
            if (!PracticeMatchTestRoute.Enabled) SceneMove.LoadScene(SceneNames.Pavilion);
        }

    }
}
