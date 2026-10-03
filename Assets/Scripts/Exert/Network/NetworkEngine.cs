using UnityEngine;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Assets.Scripts.Exert.Network
{

    public enum NetworkState
    {
        Disconnected = 0,           // 연결안됨
        Connected,                  // 연결됨
        LostConnection,             // 연결끊김
        CreatedRoom,                // 방생성
        JoinedToRoom,               // 방조인
        LeftRoom,                   // 방나감
        OpponentLeftRoom,           // 상대 방나감
        ResultRoom,                 // 대전종료
        RoomCreateFailed,           // 방생성실패
        JoinRoomFailed,             // 방조인실패
        OpponentReadToPlay,         // 상대활동읽기
        StartToPlay,                // 대전시작
        OpponentOneMoreReq,         // 한게임 더 요청
    }


    public delegate void NetworkWaitingHandler(string msg);
    public delegate void NetworkHandler(NetworkState state);

    public delegate void NetworkPlayerHandler(int playerCnt);
    public delegate void NetworkPlayersHandler(PlayerProfile[] players);
    public delegate void NetworkRoomCntHandler(Dictionary<int, int> dic);
    public delegate void NetworkRoomHandler(RoomChange[] roomData, Dictionary<int, int> dic);
    public delegate void NetworkMsgHandler(string msg);

    public abstract class NetworkEngine : MonoBehaviourPunCallbacks
    {

        public event NetworkWaitingHandler OnNetworkWaiting;
        public event NetworkHandler OnNetwork;


        public event NetworkPlayerHandler OnNetworkAllPlayer;
        public event NetworkPlayerHandler OnNetworkLobbyPlayer;
        public event NetworkPlayersHandler OnNetworkPlayerList;
        public event NetworkRoomCntHandler OnNetworkRoomUpdate;

        public event NetworkMsgHandler OnBattleResult;
        public event NetworkMsgHandler OnBattleResult2;
        public event NetworkMsgHandler OnFriendState;

        public event System.Action<int> OnConnectLost;
        public event System.Action OnConnectLostCancel;

        protected const float BackgroundTimeout = 10f;

        public int sendRate { get; protected set; }
        private float reachableTimer = 0.0f;
        protected bool oldReachable = false;
        protected bool isConnectOk = false;
        public bool opponenWaitingForYourTurn { get; protected set; }
        protected int OtherDisconnect = 0;
        protected int OtherDisconnectMax = 1;

        protected bool isMatchConn = false;
        protected bool reachable
        {
            get
            {
                return Application.internetReachability != NetworkReachability.NotReachable;
            }
        }

        public NetworkState state { get; private set; }


        public virtual void Initialize()
        {
            opponenWaitingForYourTurn = false;
        }

        public void SetMatchPlayerChk(bool ok)
        {
            isMatchConn = ok;
        }

        protected virtual void Awake()
        {
            DontDestroyOnLoad(gameObject);
            state = NetworkState.Disconnected;
        }

        public virtual void Disable()
        {
            OnNetwork = null;
        }

        public abstract void Disconnect();
        public abstract void SetHandConnect(bool conn);

        async UniTaskVoid Start()
        {
            var token = this.GetCancellationTokenOnDestroy();
            if (reachable && isConnectOk)
            {
                Connect();
            }

            while (true)
            {
                if (await UniTask.Delay(3000, cancellationToken: token).SuppressCancellationThrow()) return;

                if (state == NetworkState.Disconnected || state == NetworkState.LostConnection)
                {
                    if (reachable && isConnectOk)
                    {
                        Debug.LogWarning("NetworkEngine Start state " + state);
                        Connect();
                    }
                }
            }
        }


        public abstract void Connect();


        protected virtual void Update()
        {
            // 전상태와 현상태가 다르고 현상태가 불가이고 도달상태가 지났다면 이전상태도 불가처리 현상태가 가능이면 바로 이전상태도 가능.
            // 현상태가 불가이면  10초기다린후 3초마다 연결시도한다.

            // 이전상태와 현재상태가 다르면
            if (!isConnectOk) return;
            if (oldReachable != reachable)
            {
                // 현재불가상태이면
                if (!reachable)
                {
                    // 백타임보다 도달시간이 크면
                    if (reachableTimer > BackgroundTimeout)
                    {
                        // 도달시간 초기화
                        reachableTimer = 0f;
                        oldReachable = reachable;
                    }
                    else
                    {
                        reachableTimer += Time.deltaTime;
                    }
                }
                else
                {
                    // 현재가능 이전 불가였다면 현재기준으로 셋팅
                    oldReachable = reachable;
                    reachableTimer = 0;
                }
                // 이전불가라면 끊김알림
                if (!oldReachable)
                {
                    CallNetworkState(NetworkState.LostConnection);
                }
            }
            else
            {
                // 도달시간 초기화
                reachableTimer = 0f;
            }
        }


        protected void CallNetworkState(NetworkState state)
        {
            Debug.Log($"NetworkEngine.CallNetworkState state : {state}");
            this.state = state;
            if (OnNetwork != null)
            {
                OnNetwork(state);
            }
        }

        protected void CallConnectLost(int wait)
        {
            if (OnConnectLost != null)
            {
                OnConnectLost(wait);
            }
        }

        protected void CallConnectLostCancel()
        {
            if (OnConnectLostCancel != null)
            {
                OnConnectLostCancel();
            }
        }

        protected void CallPlayerAllChanged(int playerCnt)
        {
            if (OnNetworkAllPlayer != null)
            {
                OnNetworkAllPlayer(playerCnt);
            }
        }

        protected void CallPlayerLobbyChanged(int playerCnt)
        {
            if (OnNetworkLobbyPlayer != null)
            {
                OnNetworkLobbyPlayer(playerCnt);
            }
        }

        protected void CallRoomUpdate(Dictionary<int, int> dic = null)
        {
            if (OnNetworkRoomUpdate != null)
            {
                OnNetworkRoomUpdate(dic);
            }
        }

        protected void CallRoomPlayers(PlayerProfile[] players)
        {
            if (OnNetworkPlayerList != null)
            {
                OnNetworkPlayerList(players);
            }
        }


        public abstract void LoadPlayers(ref PlayerProfile[] players);
        public abstract void CreateRoom(string roomNm);
        public abstract void JoinRoom(string roomNm);
        public abstract void LeaveMatch();
        public abstract void LeaveMatch2();
        public abstract void RoomPlayerUpdate();
        public abstract void BattleResult(int isWin = 1);
        public abstract void BattleResult2(int isWin = 1);

        public abstract void JoinLobby(long coins);
        public abstract void JoinRandomRoom(int r_lv);
        public abstract void LeaveRoom();

        public abstract void LeaveLobby();

        public abstract void WaitCountdowned();
        public abstract void ReMatchReq();
        public abstract void OnOpponentReadToPlay(string playerData);
        public abstract void OnOpponentStartToPlay(int turnId);

        public abstract int OneMoreTimeReq();
        public abstract void OnOneMoreTimeReady(int playerId);
        public abstract void OnSendTime(float time);
        public abstract void GetRoomsInfo();




        public abstract void FindFriend(string[] nicks);

        protected void CallFriendState(string msg)
        {
            if (OnFriendState != null)
            {
                OnFriendState(msg);
            }
        }
        protected void CallBattleResultRPC(string msg)
        {
            if (OnBattleResult != null)
            {
                OnBattleResult(msg);
            }
        }

        protected void CallBattleResultRPC2(string msg)
        {
            //// Debug.Log("CallBattleResultRPC2 " + msg);

            if (OnBattleResult2 != null)
            {
                OnBattleResult2(msg);
            }
        }

        public abstract void SendRemoteMessage(string message, params object[] args);
        public abstract void OnGoToPLayWithPlayer(PlayerProfile player);

        public virtual void OnOpponentWaitingForYourTurn(string msg)
        {
            opponenWaitingForYourTurn = true;
            if (OnNetworkWaiting != null)
            {
                OnNetworkWaiting(msg);
            }
        }



        public abstract void OnOpponenInGameScene();

        public abstract void StartSimulate(string impulse);
        public abstract void InitRandForceFlutter(string randForce);

        public abstract void OnOpponentLeftMatch();
        public abstract void SetMechanicalStatesFromNetwork(int ballId, string mechanicalStateData);
        public abstract void SetStuffCreateFromNetwork(float moveTime, string StuffData);
        public abstract void SetStuffVanishFromNetwork(float moveTime, int StuffData, int coin_ch);
        public abstract void SetStuffEffect01FromNetwork(float moveTime, int StuffId);
        public abstract void SetStuffEffect02FromNetwork(float moveTime, int StuffId, Vector3 to);
        public abstract void SetStuffEffect03FromNetwork(float moveTime, int StuffId, int[] ord);



        public abstract void SetStuffAllGainFromNetwork(float moveTime, string StuffData, int coin_ch);

        public abstract void SetStuffPieceCreateFromNetwork(int posId, float moveTime, string position, string StuffData);
        public abstract void SetStuffPieceMechanicalStatesFromNetwork(int pieceId, string mechanicalStateData);
        public abstract void SetStuffPieceVanishFromNetwork(int pieceId, float moveTime);

        public abstract void WaitAndStopMoveFromNetwork(float moveTime);
        public abstract void WaitChangeTurnReadyFromNetwork();

        public abstract void OnSendCueControl(float cuePivotLocalRotationY, float cueVerticalLocalRotationX, Vector2 cueDisplacementLocalPositionXY, float cueSliderLocalPositionZ, float force, float pullBarHandleLocalPositionY);


        public abstract void SetBallMovStoryFromNetwork(float moveTime, string StoryData);

        public abstract void SetMatchSummaryFromNetwork(string StuffData);



        /// <summary>
        /// 상대 기다리는중 ..당신의차례를 기다리는 상대:거짖
        /// </summary>
        public void OnMadeTurn()
        {
            opponenWaitingForYourTurn = false;
            //Debug.Log(">>> OnMadeTurn opponenWaitingForYourTurn = false ");
        }


    }
}
