using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using System.Collections;
using Assets.Scripts.Exert.Network;
using Assets.Scripts.Often;
using Assets.Scripts.Exert.Match;
using Assets.Scripts.Prack.BackSys.ChartData;
using Assets.Scripts.Exert.BackSys;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace Assets.Scripts.Sight.Surface.Hall
{
    public class PnlWaitRoom : MonoBehaviour
    {
        [SerializeField] Button btnRoomOut;
        [SerializeField] GesHall ges;
        [SerializeField] TextMeshProUGUI txtTitle;
        [SerializeField] TextMeshProUGUI txtCountDown;
        [SerializeField] Image imgPlayerSelf;
        [SerializeField] Image imgPlayerOther;
        [SerializeField] TextMeshProUGUI txtNickNmSelf;
        [SerializeField] TextMeshProUGUI txtNickNmOther;
        CancellationTokenSource cancelSource;

        private void Awake()
        {
            //Debug.Log("PnlWaitRoom Awake");
            txtTitle = transform.Find("TxtTitle").GetComponent<TextMeshProUGUI>();
            txtCountDown = transform.Find("TxtCountDown").GetComponent<TextMeshProUGUI>();
            imgPlayerSelf = transform.Find("BoxPlayerCardSelf").GetComponent<Image>();
            imgPlayerOther = transform.Find("BoxPlayerCardOther").GetComponent<Image>();
            txtNickNmSelf = transform.Find("BoxPlayerCardSelf/TxtNickNm").GetComponent<TextMeshProUGUI>();
            txtNickNmOther = transform.Find("BoxPlayerCardOther/TxtNickNm").GetComponent<TextMeshProUGUI>();

            ges = transform.parent.GetComponent<GesHall>();
            btnRoomOut = transform.Find("BtnRoomOut").GetComponent<Button>();
            btnRoomOut.onClick.AddListener(btnRoomOut_Click);

        }



        private void OnEnable()
        {
            txtCountDown.text = "입장 중";
            txtNickNmSelf.text = UserInfo.DisplayName;
            txtNickNmOther.text = string.Empty;
            //Debug.Log("PnlWaitRoom OnEnable hallidx : " + NetworkManager.hallIdx);

            //NetworkManager.OnMainPlayerLoaded += NetworkManager_OnMainPlayerLoaded;
            NetworkManager.network.OnNetwork += NetworkManager_network_OnNetwork;
            NetworkManager.network.OnNetworkPlayerList += NetworkManager_OnNetworkPlayerList;
            SkillMatchData hall = BackendChart.skillMatchData.Where(r => r.HallIdx.Equals(NetworkManager.hallIdx)).FirstOrDefault();
            if (hall == null) return;
            //Debug.Log(hall?.ToString() ?? "xx");
            txtTitle.text = "";
            txtTitle.text = hall.HallName;
        }

        private void OnDisable()
        {
            CountDownAbort();
            //NetworkManager.OnMainPlayerLoaded -= NetworkManager_OnMainPlayerLoaded;
            if (NetworkManager.TryGetExistingNetwork(out var existingOnNetwork))
                existingOnNetwork.OnNetwork -= NetworkManager_network_OnNetwork;
            if (NetworkManager.TryGetExistingNetwork(out var existingOnNetworkPlayerList))
                existingOnNetworkPlayerList.OnNetworkPlayerList -= NetworkManager_OnNetworkPlayerList;
        }

        void NetworkManager_OnMainPlayerLoaded(PlayerProfile player)
        {
            Debug.Log("PnlWaitRoom NetworkManager_OnMainPlayerLoaded OK OK OK OK OK OK NEXT SCENE");
            //PoolPlayer.OnMainPlayerLoaded(0, player.nickname, player.coin, player.image, player.imageURL, player.prize);

        }

        // 방지정 후 플레이정보 변경시 PunNetwork.RoomPlayerUpdate
        void NetworkManager_OnNetworkPlayerList(PlayerProfile[] players)
        {
            txtCountDown.text = "0";
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
                    txtCountDown.text = "상대 대기";
                    break;
                case NetworkState.RoomCreateFailed:
                case NetworkState.JoinRoomFailed:
                    CountDownAbort();
                    txtCountDown.text = "입장 실패\n뒤로 가서 다시 시도해 주세요";
                    break;
                case NetworkState.LostConnection:
                case NetworkState.Disconnected:
                    CountDownAbort();
                    txtCountDown.text = "연결 끊김\n뒤로 가서 다시 시도해 주세요";
                    break;
                case NetworkState.StartToPlay:
                    GotoPitch();
                    break;
                case NetworkState.LeftRoom:
                    Debug.Log("NetworkManager_network_OnNetwork " + NetworkState.LeftRoom.ToString());
                    ges.Open(GesHall.Pnl.Lobby);
                    break;
            }
        }

        //int iii = 0;
        //IEnumerator coroutine;
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

        void CountDownAbort()
        {
            cancelSource?.Cancel();
            cancelSource?.Dispose();
            cancelSource = null;


            //if (coroutine != null)
            //    StopCoroutine(coroutine);
        }

        async UniTaskVoid AsyncWaitCountDown(int sec)
        {
            var token = cancelSource.Token;
            txtCountDown.text = sec.ToString();
            while(sec > 0)
            {
                if (await UniTask.Delay(1000, cancellationToken: token).SuppressCancellationThrow()) return;
                sec--;
                txtCountDown.text = sec.ToString();
            }
            if (!token.IsCancellationRequested && isActiveAndEnabled)
                NetworkManager.network.WaitCountdowned();
        }

        IEnumerator WaitCountDownload(int sec)
        {
            txtCountDown.text = sec.ToString();
            while (sec > 0)
            {
                yield return new WaitForSeconds(1);
                sec--;
                txtCountDown.text = sec.ToString();
            }

            NetworkManager.network.WaitCountdowned();
            //Debug.Log("GoToPlay >>>>> ");

        }

        void btnRoomOut_Click()
        {
            NetworkManager.network.LeaveMatch();

        }

        // 경기장으로 이동
        void GotoPitch()
        {
            SceneMove.LoadScene(SceneNames.Pavilion);
        }
    }
}
