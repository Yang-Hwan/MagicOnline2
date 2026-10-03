using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;
using Photon.Realtime;
using Photon.Pun;
using BackEnd;
using Assets.Scripts.Exert.BackSys;
using Assets.Scripts.Exert.Match;

namespace Assets.Scripts.Exert.Network
{
    public class PunNetwork : NetworkEngine
    {

        private PhotonView view;
        private Player opponentPlayer;
        private bool tryToConnect;
        private PoolNetMsg messenger;

        private List<RoomInfo> roomList;
        private List<RoomInfo> roomsLocal = new List<RoomInfo>();
        private List<RoomChange> rooms = new List<RoomChange>();
        private Dictionary<int, int> skillRoom = new Dictionary<int, int>();

        private int playerAllCnt
        {
            get
            {
                return PhotonNetwork.CountOfPlayers;
            }
        }

        protected int playerAllCntOld = 0;
        protected int playerLobbyCntOld = 0;

        private long coins = 10;
        TypedLobby typedLobby;
        bool matchRequestPending;
        bool matchRequestCancelled;
        bool IgnoreCancelledTestMatchCallback => Assets.Scripts.Often.PracticeMatchTestRoute.Enabled &&
            (matchRequestCancelled || !PhotonNetwork.InRoom);
        float matchRequestDeadline;

        // Diagnostic context intentionally excludes user IDs, credentials and room names.
        void LogMatchContext(string operation)
        {
            Debug.Log($"[MatchConnection] event={operation} region={PhotonNetwork.CloudRegion} " +
                $"version={PhotonNetwork.GameVersion} state={PhotonNetwork.NetworkClientState} " +
                $"hall={NetworkManager.hallIdx} players={PhotonNetwork.CurrentRoom?.PlayerCount ?? 0} " +
                $"build={Application.version} platform={Application.platform}");
        }

        public override void Initialize()
        {
            if (!GetComponent<PracticeMatchSetupTransport>()) gameObject.AddComponent<PracticeMatchSetupTransport>();
            if (!messenger)
            {
                messenger = gameObject.AddComponent<PoolNetMsg>();
            }
        }

        protected override void Awake()
        {
            base.Awake();
            sendRate = 10;

            PhotonNetwork.SendRate = sendRate;
            PhotonNetwork.SerializationRate = sendRate;

            view = gameObject.AddComponent<PhotonView>();
            view.ObservedComponents = new List<Component>();
            view.ObservedComponents.Add(this);
            view.Synchronization = ViewSynchronization.Off;
            view.ViewID = 1;
            PhotonNetwork.AutomaticallySyncScene = !Assets.Scripts.Often.PracticeMatchTestRoute.Enabled;
            PhotonNetwork.KeepAliveInBackground = BackgroundTimeout;

        }

        public override void Connect()
        {
            Debug.Log($"PunNetwork Connect view : {view} .. reachable : {reachable}");
            isConnectOk = true;

            if (view && reachable)
            {
                if (!PhotonNetwork.IsConnected)
                {
                    // New matches load only after both peers accept the setup protocol.
                    PhotonNetwork.AutomaticallySyncScene = !Assets.Scripts.Often.PracticeMatchTestRoute.Enabled;
                    AuthenticationValues authValues = new AuthenticationValues(NetworkManager.uuid);
                    PhotonNetwork.AuthValues = authValues;
                    Debug.Log($"111.PunNetwork Connect view : {view} .. reachable : {reachable}  .. PhotonNetwork.IsConnected : {PhotonNetwork.IsConnected}");
                    var settings = Assets.Scripts.Often.PracticeMatchTestRoute.ConnectionSettings(
                        PhotonNetwork.PhotonServerSettings.AppSettings, Application.version);
                    PhotonNetwork.ConnectUsingSettings(settings, PhotonNetwork.PhotonServerSettings.StartInOfflineMode);
                    tryToConnect = true;
                }
                else if (!tryToConnect)
                {
                    PhotonNetwork.Disconnect();
                }
            }
        }

        public override void JoinLobby(long coins)
        {
            this.coins = coins;
            int lv = 1;
            string nm = "Lobby_" + lv;
            typedLobby = new TypedLobby(nm, LobbyType.Default);
        }

        public override void LeaveLobby()
        {
            Debug.LogWarning("### PunNetwork LeaveLobby ");
            PhotonNetwork.LeaveLobby();
        }

        public int match_conn_type { get; private set; }   // -1:초기 , 0: 수신대기, 1: 수신처리, 2:송신대기, 3: 송신처리, 4: 수신안됨, 5: 끊김 처리

        protected override void Update()
        {
            base.Update();
            if (matchRequestPending && Time.realtimeSinceStartup >= matchRequestDeadline)
            {
                matchRequestPending = false;
                matchRequestCancelled = true;
                LogMatchContext("JoinTimedOut");
                CallNetworkState(NetworkState.JoinRoomFailed);
                PhotonNetwork.Disconnect();
            }
        }

        // 포톤연결후 OnConnected > OnConnected  > OnJoinedLobby
        public override void OnConnected()
        {
            Debug.Log("... OnConnected state : " + state);
            //if (state == NetworkState.ResultRoom) return;  ..................... 언제 쓰이지???
            CallNetworkState(NetworkState.Connected);
        }

        // 포톤연결후 OnConnected 
        public override void OnConnectedToMaster()
        {
            if (Assets.Scripts.Often.PracticeSceneFlow.OnlineSession != null) return;
            LogMatchContext("ConnectedToMaster");
            Debug.LogWarning("### PunNetwork OnConnectedToMaster ");
            roomsLocal.Clear();
            rooms.Clear();
            PhotonNetwork.JoinLobby();
        }

        // 포톤연결후   
        public override void OnJoinedLobby()
        {
            LogMatchContext("JoinedLobby");
            Debug.Log("### PunNetwork OnJoinedLobby userName : " + NetworkManager.mainPlayer.uuid);

            PhotonNetwork.LocalPlayer.NickName = Backend.UserInDate;
        }


        public override void SetHandConnect(bool conn)
        {
            isConnectOk = conn;
            //if (!conn)
            //{
            //    PhotonNetwork.Disconnect();
            //}
            //else
            //{
            //    Connect();
            //}
        }

        // 
        public override void LoadPlayers(ref PlayerProfile[] players)
        {
            string nm_m = PhotonNetwork.MasterClient.NickName;
            int cnt = PhotonNetwork.PlayerList.Length;
            string player_str = "nm_m: " + nm_m + " .. ";
            List<PlayerProfile> pps = new List<PlayerProfile>(0);
            for (int i = 0; i < cnt; i++)
            {
                string master = PhotonNetwork.PlayerList[i].IsMasterClient ? "M" : "C";
                string nm = PhotonNetwork.PlayerList[i].NickName;
                string nm_self = NetworkManager.mainPlayer.nickName;
                player_str += i + " : " + nm + "_" + master + ",";
                PlayerState state = PhotonNetwork.PlayerList[i].IsMasterClient ? PlayerState.Master : PlayerState.Away;
                PlayerProfile pp = new PlayerProfile("", -1, false, nm, 0, 0, state);
                pps.Add(pp);
            }
            //RoomPlayerUpdate >> nm_m:  .. _M,_C,
            Debug.Log("RoomPlayerUpdate >> " + player_str);
            players = pps.ToArray();

        }

        // 방생성 
        public override void CreateRoom(string roomNm)
        {
            //rooms.Clear();
            Debug.Log("000.........CreateRoom " + roomNm + ", NetworkClientState : " + PhotonNetwork.NetworkClientState);
            if (PhotonNetwork.NetworkClientState == ClientState.Joined)
            {
                Debug.Log("111.........CreateRoom " + roomNm + ", NetworkClientState : " + PhotonNetwork.NetworkClientState);
                PhotonNetwork.LeaveRoom();
            }
            if (PhotonNetwork.IsConnectedAndReady && (PhotonNetwork.NetworkClientState == ClientState.JoinedLobby || PhotonNetwork.NetworkClientState == ClientState.ConnectedToMasterserver))
            {
                //string player_data = NetworkManager.PlayerToString(NetworkManager.mainPlayer);
                //// Debug.Log("player_data : " + player_data);
                //PhotonNetwork.CreateRoom(roomNm, new RoomOptions() { MaxPlayers = 2 }, typedLobby);
                PhotonNetwork.CreateRoom(roomNm, new RoomOptions() { MaxPlayers = 2 }, null);
            }
            else
            {
                // Debug.Log($"PhotonNetwork.IsConnectedAndReady : {PhotonNetwork.IsConnectedAndReady}, PhotonNetwork.NetworkClientState : {PhotonNetwork.NetworkClientState}");
            }
        }



        public override void JoinRoom(string roomNm)
        {
            //rooms.Clear();
            //Debug.Log("............JoinRoom " + roomNm + ", NetworkClientState : " + PhotonNetwork.NetworkClientState);
            if (PhotonNetwork.NetworkClientState == ClientState.Joined)
            {
                PhotonNetwork.LeaveRoom();
            }
            if (PhotonNetwork.IsConnectedAndReady && (PhotonNetwork.NetworkClientState == ClientState.JoinedLobby || PhotonNetwork.NetworkClientState == ClientState.ConnectedToMasterserver))
            {
                PhotonNetwork.JoinRoom(roomNm);
            }
        }


        // 방정보 생성 또는 조인
        public override void JoinRandomRoom(int hall_idx)
        {
            if (matchRequestPending) return;
            NetworkManager.hallIdx = hall_idx;
            LogMatchContext("MatchRequested");
            if (!PhotonNetwork.IsConnectedAndReady ||
                (PhotonNetwork.NetworkClientState != ClientState.JoinedLobby &&
                 PhotonNetwork.NetworkClientState != ClientState.ConnectedToMasterserver))
            {
                CallNetworkState(NetworkState.JoinRoomFailed);
                return;
            }
            var hall = BackendChart.skillMatchData?.FirstOrDefault(r => r.HallIdx == hall_idx);
            string rules;
            try {
                rules = Assets.Scripts.Often.MatchmakingCompatibility.RulesKey(hall);
                if (Assets.Scripts.Often.PracticeMatchTestRoute.Enabled)
                    Assets.Scripts.Often.MatchConfiguration.Online(hall, "Host", "Guest", 0, 0, 20);
            }
            catch (Exception e) when (e is ArgumentException || e is NotSupportedException || e is OverflowException)
            { CallNetworkState(NetworkState.JoinRoomFailed); return; }
            var properties = new ExitGames.Client.Photon.Hashtable {
                { "hall", hall_idx }, { "rules", rules },
                { "protocol", Assets.Scripts.Often.PracticeMatchTestRoute.RoomProtocol }
            };
            var options = new RoomOptions {
                MaxPlayers = 2, IsOpen = true, IsVisible = true,
                CustomRoomProperties = properties,
                CustomRoomPropertiesForLobby = new[] { "hall", "rules", "protocol" }
            };
            GetComponent<PracticeMatchSetupTransport>()?.AllowNewMatch();
            matchRequestCancelled = false;
            matchRequestPending = true;
            matchRequestDeadline = Time.realtimeSinceStartup + 20f;
            // Server performs the match-or-create operation; no local room-list race.
            if (!PhotonNetwork.JoinRandomOrCreateRoom(properties, 2, roomName: Guid.NewGuid().ToString("N") + ";" + hall_idx,
                roomOptions: options))
            {
                matchRequestPending = false;
                CallNetworkState(NetworkState.JoinRoomFailed);
            }
        }

        //async void WaitMadeRoom()
        //{
        //    await 
        //}

        public override void FindFriend(string[] nicks)
        {
            //nicks[0] = "Guest-94";
            // Debug.Log("FindFriend " + nicks[0]);
            PhotonNetwork.FindFriends(nicks);
        }


        // 친구찾기..   
        public override void OnFriendListUpdate(List<FriendInfo> friendList)
        {
            string onlineId = "";
            base.OnFriendListUpdate(friendList);
            string msg = "";
            foreach (FriendInfo info in friendList)
            {
                msg += "friend " + info.UserId + ", online : " + info.IsOnline + ", Room : " + info.IsInRoom + "..";

                if (info.IsOnline)
                {
                    onlineId = info.UserId;
                    break;
                }
            }

            if (onlineId == "") return;
            CallFriendState(msg);

            Player friend = FindPlayerByNickname(onlineId);   // 
            if (friend != null)
            {
                // 플레이어를 찾은 경우 메시지를 보냅니다.
                photonView.RPC("SendPrivateMessage", friend, "안녕하세요!");
            }
            else
            {
                // 플레이어를 찾지 못한 경우 처리 로직을 추가합니다.
                // Debug.Log("친구를 찾을 수 없습니다.");
            }

        }


        private Player FindPlayerByNickname(string nickname)
        {
            // 로비 또는 마스터 서버에서 플레이어 목록을 가져옵니다. 같은 방에서만 찾기때문에 의미없는 코드임.
            Player[] players = PhotonNetwork.PlayerList;
            // Debug.Log("FindPlayerByNickname len : " + players.Length);
            foreach (Player player in players)
            {
                if (player.NickName == nickname)
                {
                    return player;
                }
            }
            return null;
        }


        [PunRPC]
        private void SendPrivateMessage(string message, PhotonMessageInfo info)
        {
            // 메시지를 받은 플레이어와 내용을 출력합니다.
            // Debug.Log("Received message: " + message + " from: " + info.Sender);
        }

        /// <summary>
        /// 방정보 변경시 발생
        /// </summary>
        /// <param name="roomsPun"></param>
        public override void OnRoomListUpdate(List<RoomInfo> roomsPun)
        {
            foreach (var room in roomsPun)
            {
                // RoomInfo updates are new instances: identify entries by name.
                roomsLocal.RemoveAll(existing => existing.Name == room.Name);
                var parts = room.Name.Split(';');
                if (parts.Length != 2 || !int.TryParse(parts[1], out int hall) || hall < 1) continue;
                rooms.RemoveAll(existing => existing.Name == parts[0] && existing.HallIdx == hall);
                if (room.RemovedFromList) continue;
                roomsLocal.Add(room);
                rooms.Add(new RoomChange(room.MaxPlayers, room.PlayerCount, parts[0], hall, 0, RoomEditType.None));
            }
            GetRoomsInfo();
        }

        public override void GetRoomsInfo()
        {
            // 레벨기준 룸정보 초기화 
            skillRoom.Keys.ToList().ForEach(key => skillRoom[key] = 0);
            //Debug.Log("GetRoomsInfo  roomsLocal.Count : " + roomsLocal.Count);

            for (int i = 0; i < roomsLocal.Count; i++)
            {
                string nm = roomsLocal[i].Name;
                int lv = int.Parse(roomsLocal[i].Name.Split(';')[1]);
                long coin = 0;// long.Parse(roomMemList[i].Name.Split(';')[2]);
                int pc = roomsLocal[i].PlayerCount;
                if (!skillRoom.ContainsKey(lv)) skillRoom.Add(lv, 0);
                int cnt = skillRoom[lv];
                cnt += pc;
                skillRoom[lv] = cnt;
            }

            CallRoomUpdate(skillRoom);
        }

        // ** 방정보 변경시 호출 **
        public   void OnRoomListUpdate2(List<RoomInfo> roomsPun)
        {
            //Debug.Log("OnRoomListUpdate cnt : " + roomsPun.Count);
            int roomPunCount = roomsPun.Count;
            int roomMemCount = roomsLocal.Count;
            int roomSCount = rooms.Count;
            string room_str = "OnRoomListUpdate roomCount : (" + roomPunCount + ", " + roomMemCount + ", " + roomSCount + ") >>>>> ";
            string update_data = "";
            List<RoomChange> roomChange = new List<RoomChange>();
            for (int i = 0; i < roomPunCount; i++)
            {
                room_str += "i:" + i + ", name : " + roomsPun[i].Name + "... ";

                int mc = roomsPun[i].MaxPlayers;
                int pc = roomsPun[i].PlayerCount;
                update_data += roomsPun[i].Name + ">>> ";
                update_data += mc.ToString() + "_" + pc.ToString() + "_";
                update_data += roomsPun[i].Name + "_";
                RoomEditType roomEditType = RoomEditType.None;
                //Debug.Log(i + " >> roomsPun[i].Name : " + roomsPun[i].Name);
                string nm = roomsPun[i].Name.Split(';')[0];
                int lv = int.Parse(roomsPun[i].Name.Split(';')[1]);
                long coin = 0;// long.Parse(roomsPun[i].Name.Split(';')[2]);

                if (!skillRoom.ContainsKey(lv)) skillRoom.Add(lv, 0);

                // 펀룸의 삭제가 아니다. 있어야할 항목
                if (!roomsPun[i].RemovedFromList)
                {
                    room_str += "RemovedFromList false, ";
                    // 램룸의 없다면
                    if (!roomsLocal.Contains(roomsPun[i]))
                    {
                        room_str += "myList no Contains, ";
                        update_data += "add_";
                        roomEditType = RoomEditType.Add;
                        roomsLocal.Add(roomsPun[i]);
                        rooms.Add(new RoomChange(mc, pc, nm, lv, coin, roomEditType));
                    }
                    // 램룸의 있다면 재설정
                    else
                    {
                        room_str += "myList ok Contains, ";
                        update_data += "edit_";
                        roomEditType = RoomEditType.Edit;
                        roomsLocal[roomsLocal.IndexOf(roomsPun[i])] = roomsPun[i];
                        rooms.Where(r => r.Name == nm && r.HallIdx == lv).ToList().ForEach(r => r.PlayerCnt = pc);
                    }
                }
                // 펀룸의 삭제 임. 램룸의 항목이 있다면 .. 삭제시킴
                else if (roomsLocal.IndexOf(roomsPun[i]) != -1)
                {

                    room_str += "RemovedFromList true, ";
                    update_data += "del_";
                    roomEditType = RoomEditType.Remove;
                    roomsLocal.RemoveAt(roomsLocal.IndexOf(roomsPun[i]));
                    rooms.RemoveAll(r => r.Name == nm && r.HallIdx == lv);

                }
                // 
                else
                {
                    room_str += "---------, ";

                }
                update_data += "^";
                //Debug.Log(update_data);
                update_data = "";
                roomChange.Add(new RoomChange(mc, pc, nm, lv, coin, roomEditType));
            }
            // Debug.Log(room_str + update_data);
            skillRoom.Keys.ToList().ForEach(key => skillRoom[key] = 0);

            string msg = "room mem : " + roomsPun.Count + " >>>>> ";
            for (int i = 0; i < roomsLocal.Count; i++)
            {
                string nm = roomsLocal[i].Name;
                int lv = int.Parse(roomsLocal[i].Name.Split(';')[1]);
                long coin = 0;// long.Parse(roomMemList[i].Name.Split(';')[2]);
                int pc = roomsLocal[i].PlayerCount;
                if (!skillRoom.ContainsKey(lv)) skillRoom.Add(lv, 0);
                int cnt = skillRoom[lv];
                cnt += pc;
                skillRoom[lv] = cnt;
                //msg += $"nm : {nm}, lv : {lv}, pc : {pc}, coin : {coin}";
            }
            //Debug.Log(msg);
            //msg = "rooms : " + rooms.Count + " >>>>> ";
            //for (int i = 0; i < rooms.Count; i++)
            // {
            // msg += rooms[i].Name + ",";
            //}

            // Debug.Log("OnRoomListUpdate : " + msg);

            CallRoomUpdate(skillRoom);

        }



        // ** 방지정후 플레이 변경시 호출 **
        public override void RoomPlayerUpdate()
        {
            string nm_m = PhotonNetwork.MasterClient.NickName;
            int cnt = PhotonNetwork.PlayerList.Length;
            string player_str = "nm_m: " + nm_m + " .. ";
            PlayerProfile[] pps = new PlayerProfile[cnt];
            for (int i = 0; i < cnt; i++)
            {
                string master = PhotonNetwork.PlayerList[i].IsMasterClient ? "M" : "C";
                string userId = PhotonNetwork.PlayerList[i].UserId;
                string nickNm = PhotonNetwork.PlayerList[i].NickName;
                //long coin = PhotonNetwork.PlayerList[i].c
                //string nm_self = NetworkManager.mainPlayer.nickname;
                //player_str += nm + "_" + master + ",";
                //string remark = $"{PhotonNetwork.LocalPlayer.NickName}_{PhotonNetwork.MasterClient.NickName}";
                string remark = "";
                PlayerState state = PhotonNetwork.PlayerList[i].IsMasterClient ? PlayerState.Master : PlayerState.Away;
                //if(nickNm != NetworkManager.mainPlayer.nickname)
                //{
                //}

                PlayerProfile pp = new PlayerProfile("", -1, false, nickNm, 0, 0, state, remark);
                pps[i] = pp;
                //Debug.Log($"photon player {i}  UserId : {userId}, nickNm : {nickNm}, master : {master} ");
            }
            //RoomPlayerUpdate >> nm_m:  .. _M,_C,
            //Debug.Log("RoomPlayerUpdate >> " + player_str);

            CallRoomPlayers(pps.ToArray());
        }



        public override void BattleResult2(int isWin = 1)
        {
            int prize = 20;
            int cnt = PhotonNetwork.PlayerList.Length;
            string msg = "";

            msg = isWin == 0 ? "lose" : "win";
            BattleResultRPC2(msg);
            // Debug.Log("BattleResult2 opponentPlayer " + opponentPlayer.NickName);

            msg = isWin == 1 ? "lose" : "win";
            view.RPC("BattleResultRPC2", opponentPlayer, msg);


        }

        [PunRPC]
        void BattleResultRPC2(string msg)
        {
            CallBattleResultRPC2(msg);
        }



        public override void BattleResult(int isWin = 1)
        {
            int prize = 1000;
            int cnt = PhotonNetwork.PlayerList.Length;
            string msg = "";


            for (int i = 0; i < cnt; i++)
            {
                //NetworkManager.network.
                if (PhotonNetwork.PlayerList[i].NickName != PhotonNetwork.LocalPlayer.NickName)
                {
                    msg = isWin == 1 ? "lose" : "win";
                    view.RPC("BattleResultRPC", PhotonNetwork.PlayerList[i], msg);
                }
                else
                {
                    msg = isWin == 1 ? "win" : "lose";
                    BattleResultRPC(msg);
                }
            }
        }


        [PunRPC]
        void BattleResultRPC(string msg)
        {
            CallBattleResultRPC(msg);
        }




        public override void OnCreatedRoom()
        {
            Debug.Log("### PunNetwork OnCreatedRoom NetworkState.CreatedRoom : " + NetworkState.CreatedRoom);
            CallNetworkState(NetworkState.CreatedRoom);
            //Debug.Log("OnCreatedRoom state " + state);

        }






        // 방입장후 콜백.
        public override void OnJoinedRoom()
        {
            if (Assets.Scripts.Often.PracticeSceneFlow.OnlineSession != null) return;
            matchRequestPending = false;
            if (matchRequestCancelled) { PhotonNetwork.LeaveRoom(); return; }
            LogMatchContext("JoinedRoom");
            OtherDisconnect = 0;
            CallNetworkState(NetworkState.JoinedToRoom);
            //Debug.Log("OnJoinedRoom state ... ismaster : " + PhotonNetwork.IsMasterClient);


            string msg = "";
            for (int i = 0; i < PhotonNetwork.PlayerList.Length; i++)
            {
                string nick = PhotonNetwork.PlayerList[i].NickName;
                //msg += $"i : {i}, nick : {nick},";
            }
            string curRoom = PhotonNetwork.CurrentRoom.Name;
            int curPlayCnt = PhotonNetwork.CurrentRoom.PlayerCount;
            int curMaxCnt = PhotonNetwork.CurrentRoom.MaxPlayers;
            //msg += $".... cur room : {curRoom}, {curPlayCnt} / {curMaxCnt}";
            //Debug.Log(msg);


            RoomPlayerUpdate();

            // 손님인경우 (자신이 방장이 아니라면)
            if (PhotonNetwork.LocalPlayer != PhotonNetwork.MasterClient)
            {
                opponentPlayer = PhotonNetwork.MasterClient;    // 방장
                //OnOpponentReadToPlay("");           // 손님이 자신에게
                view.RPC(nameof(OnOpponentReadToPlay), opponentPlayer, NetworkManager.PlayerToString(NetworkManager.mainPlayer)); // 방장에게 자신(손님)정보 전달
            }


        }

        // 방장만 받음. 손님정보
        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            if (IgnoreCancelledTestMatchCallback) return;
            if (Assets.Scripts.Often.PracticeSceneFlow.OnlineSession != null) return;
            // Debug.Log("OnPlayerEnteredRoom 방장만 받음.. 손님이 조인시 방장에게 알림 IsMasterClient : " + newPlayer.IsMasterClient);
            opponentPlayer = newPlayer;
            RoomPlayerUpdate();
        }


        public override void WaitCountdowned()
        {
            if (IgnoreCancelledTestMatchCallback) return;
            if (Assets.Scripts.Often.PracticeMatchTestRoute.Enabled)
            {
                var transport = GetComponent<PracticeMatchSetupTransport>();
                if (PhotonNetwork.IsMasterClient && transport && transport.Session == null &&
                    !transport.BeginPracticeSetup(NetworkManager.hallIdx)) CallNetworkState(NetworkState.JoinRoomFailed);
                return;
            }
            // 방장이 아니라면
            if (PhotonNetwork.LocalPlayer != PhotonNetwork.MasterClient)
            {
                //int turnId = Random.Range(0, 2);
                int turnId = 1; // 손님이 1,  방장은 0 .. 테스트로 방장이 무조건 먼저
                int turnIdForSend = turnId == 1 ? 0 : 1;
                OnOpponentStartToPlay(turnId);
                view.RPC(nameof(OnOpponentStartToPlay), opponentPlayer, turnIdForSend);    // 방장에게 0값 전송
            }

        }

        /// <summary>
        /// 한게임더요청 
        /// </summary>
        public override void ReMatchReq()
        {

        }

        public override void SendRemoteMessage(string message, params object[] args)
        {
            //if (message != "OnSendTime")
            //{
            //    Debug.Log("SendRemoteMessage : " + message + ", arg.len: " + args.Length);
            //}
            view.RPC(message, opponentPlayer, args);
        }

        public override void OnGoToPLayWithPlayer(PlayerProfile player)
        {
            // 상대.. 
            if (PhotonNetwork.LocalPlayer != PhotonNetwork.MasterClient)
            {
                //adapter.mainScenario.UpdatePrize(player.prize);
                string joinroomstr = NetworkManager.PlayerToString(NetworkManager.opponentPlayer);
                Debug.Log("PunNetwork OnGoToPLayWithPlayer str : " + joinroomstr);
                NetworkManager.opponentPlayer = player;
                PhotonNetwork.JoinRoom(joinroomstr);
            }
            // qh
            else
            {
                // The client who first created the room 
                view.RPC("OnOpponenReadToPlay", opponentPlayer, NetworkManager.PlayerToString(NetworkManager.mainPlayer));
            }
        }

        // RPC 손님 -> 방장 
        [PunRPC]
        public override void OnOpponentReadToPlay(string playerData)
        {
            if (IgnoreCancelledTestMatchCallback) return;
            NetworkManager.opponentPlayer = NetworkManager.PlayerFromString(playerData);
            CallNetworkState(NetworkState.OpponentReadToPlay);
            //Debug.Log("OnOpponentReadToPlay 손님 -> 방장 : " + playerData);

            //string uuid = NetworkManager.opponentPlayer.uuid;
            //string nick = NetworkManager.opponentPlayer.nickname;
            //long prize = NetworkManager.opponentPlayer.prize;
            //long coin = NetworkManager.opponentPlayer.coins;
            //Debug.Log($"손님 -> 방장 .. uuid : {uuid}, nick : {nick}, prize : {prize}, coin : {coin} ");

            // 방장이 손님에게 보냄.
            view.RPC(nameof(OnMasterInfoToGuest), opponentPlayer, NetworkManager.PlayerToString(NetworkManager.mainPlayer));
        }



        // RPC 방장 -> 손님
        [PunRPC]
        public void OnMasterInfoToGuest(string playerData)
        {
            if (IgnoreCancelledTestMatchCallback) return;
            //Debug.Log("방장 -> 손님 : " + playerData);
            NetworkManager.opponentPlayer = NetworkManager.PlayerFromString(playerData);
            string uuid = NetworkManager.opponentPlayer.uuid;
            string nick = NetworkManager.opponentPlayer.nickName;
            long prize = NetworkManager.opponentPlayer.prize;
            long coin = NetworkManager.opponentPlayer.coin;
            //Debug.Log($"OnMasterInfoToGuest 방장 -> 손님 .. uuid : {uuid}, nick : {nick}, prize : {prize}, coin : {coin} ");
            CallNetworkState(NetworkState.OpponentReadToPlay);
        }



        // RPC 플레이됨 알림
        [PunRPC]
        public override void OnOpponentStartToPlay(int turnId)
        {
            // New matches start only through the match-id/ready handshake.
            // A delayed legacy RPC must never overwrite their current turn.
            if (Assets.Scripts.Often.PracticeMatchTestRoute.Enabled) return;
            if (IgnoreCancelledTestMatchCallback) return;

            PoolPlayer.SetIdx(turnId);
            //Debug.Log(">>>>> OnOpponentStartToPlay turnId : " + turnId + ", master : " + PhotonNetwork.IsMasterClient);
            CallNetworkState(NetworkState.StartToPlay);

        }

        public override int OneMoreTimeReq()
        {
            //Debug.Log($"player len : {PhotonNetwork.PlayerList.Length}");
            int len = PhotonNetwork.PlayerList.Length;
            if (len == 2)
            {
                view.RPC(nameof(OnOneMoreTimeReady), opponentPlayer, PoolPlayer.otherPlayer.playerId);
            }
            return len;
        }

        /// <summary>
        /// 한게임더 요청 
        /// </summary>
        [PunRPC]
        public override void OnOneMoreTimeReady(int playrId)
        {
            PoolPlayer.OneMoreReq(playrId, Often.OneMore.Req);
            CallNetworkState(NetworkState.OpponentOneMoreReq);
            Debug.Log($"OnOneMoreTimeReady : {playrId}");
        }

        /// <summary>
        /// 큐정보 수신받음
        /// </summary>
        /// <param name="cuePivotLocalRotationY"></param>
        /// <param name="cueVerticalLocalRotationX"></param>
        /// <param name="cueDisplacementLocalPositionXY"></param>
        /// <param name="cueSliderLocalPositionZ"></param>
        /// <param name="force"></param>
        [PunRPC]
        public override void OnSendCueControl(float cuePivotLocalRotationY, float cueVerticalLocalRotationX, Vector2 cueDisplacementLocalPositionXY, float cueSliderLocalPositionZ, float force, float pullBarHandleLocalPositionY)
        {
            //Debug.Log("PunNetwork OnSendCueControl cuePivotLocalRotationY : " + cuePivotLocalRotationY);
            messenger.OnSendCueControl(cuePivotLocalRotationY, cueVerticalLocalRotationX, cueDisplacementLocalPositionXY, cueSliderLocalPositionZ, force, pullBarHandleLocalPositionY);
        }

        [PunRPC]
        public override void OnSendTime(float time)
        {
            //if(time01 >= 1f)
            //{
            //    Debug.Log($".... OnSendTime TIME : {time01} .. InNet : {PoolLogic.controlInNetwork}, FromNet : {PoolLogic.controlFromNetwork}");
            //}
            messenger.SetTime(time);
            //Debug.Log($"++++ [PunRPC] OnSendTime TIME : {time01} .. InNet : {CushionPoolLogic.controlInNetwork}, FromNet : {CushionPoolLogic.controlFromNetwork}");
        }

        [PunRPC]
        public override void OnOpponentWaitingForYourTurn(string msg)
        {
            base.OnOpponentWaitingForYourTurn(msg);
            //Debug.Log("[PunRPC] OnOpponenWaitingForYourTurn");
        }

        [PunRPC]
        public override void OnOpponenInGameScene()
        {
            //      StartCoroutine(messenger.OnOpponenInGameScene());
        }

        [PunRPC]
        public void OnMoveBall(Vector3 ballPosition)
        {
            //messenger.OnMoveBall(ballPosition);
        }

        // RPC : 상대에게 움직임 멈춤 알림
        [PunRPC]
        public override void WaitAndStopMoveFromNetwork(float moveTime)
        {
            //Debug.Log("RPC : 상대에게 움직임 멈춤 알림 WaitAndStopMoveFromNetwork .............................moveTime " + moveTime);
            messenger.WaitAndStopMoveFromNetwork(moveTime);
        }


        [PunRPC]
        public override void WaitChangeTurnReadyFromNetwork()
        {
            messenger.WaitChangeTurnReadyFromNetwork();
        }

        [PunRPC]
        public override void StartSimulate(string impulse)
        {
            //Debug.Log("StartSimulate " + impulse + " >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>> ");
            messenger.StartSimulate(impulse);
        }

        [PunRPC]
        public override void InitRandForceFlutter(string randForce)
        {
            messenger.InitRandForceFlutter(randForce);
        }

        /// <summary>
        /// 볼 정보수신 받음
        /// </summary>
        /// <param name="ballId"></param>
        /// <param name="mechanicalStateData"></param>
        [PunRPC]
        public override void SetMechanicalStatesFromNetwork(int ballId, string mechanicalStateData)
        {
            //Debug.Log($"SetMechanicalStatesFromNetwork id : {ballId}, data : {mechanicalStateData}");
            messenger.SetMechanicalStatesFromNetwork(ballId, mechanicalStateData);
        }


        [PunRPC]
        public override void SetStuffCreateFromNetwork(float moveTime, string stuffData)
        {
            //Debug.Log($"PunRPC SetStuffCreateFromNetwork  moveTime : {moveTime}, stuffData : {stuffData} ");
            messenger.SetStuffCreateFromNetwork(moveTime, stuffData);
        }
        [PunRPC]
        public override void SetStuffVanishFromNetwork(float moveTime, int stuffData, int coin_ch)
        {
            //Debug.Log($"PunRPC SetStuffVanishFromNetwork  moveTime : {moveTime}, stuffData : {stuffData} , coin_ch : {coin_ch} ");
            messenger.SetStuffVanishFromNetwork(moveTime, stuffData, coin_ch);
        }

        [PunRPC]
        public override void SetStuffEffect01FromNetwork(float moveTime, int StuffId)
        {
            //Debug.Log($"PunRPC SetStuffVanishFromNetwork  moveTime : {moveTime}, stuffData : {stuffData} , coin_ch : {coin_ch} ");
            messenger.SetStuffEffect01FromNetwork(moveTime, StuffId);
        }

        [PunRPC]
        public override void SetStuffEffect02FromNetwork(float moveTime, int StuffId, Vector3 to)
        {
            //Debug.Log($"PunRPC SetStuffVanishFromNetwork  moveTime : {moveTime}, stuffData : {stuffData} , coin_ch : {coin_ch} ");
            messenger.SetStuffEffect02FromNetwork(moveTime, StuffId, to);
        }

        [PunRPC]
        public override void SetStuffEffect03FromNetwork(float moveTime, int StuffId, int[] ord)
        {
            //Debug.Log($"PunRPC SetStuffVanishFromNetwork  moveTime : {moveTime}, stuffData : {stuffData} , coin_ch : {coin_ch} ");
            messenger.SetStuffEffect03FromNetwork(moveTime, StuffId, ord);
        }

        [PunRPC]
        public override void SetStuffAllGainFromNetwork(float moveTime, string stuffData, int coin_ch)
        {
            //Debug.Log($"PunRPC SetStuffVanishFromNetwork  moveTime : {moveTime}, stuffData : {stuffData} , coin_ch : {coin_ch} ");
            //messenger.SetStuffAllGainFromNetwork(moveTime, stuffData, coin_ch);
        }


        // STUFF PIECE ----------------------------------------------------------------
        [PunRPC]
        public override void SetStuffPieceCreateFromNetwork(int posId, float moveTime, string position, string stuffData)
        {
            //Debug.Log($"PunRPC SetStuffPieceCreateFromNetwork posId : {posId}, moveTime : {moveTime}, position : {position}, stuffData : {stuffData} ");
            //messenger.SetStuffPieceCreateFromNetwork(posId, moveTime, position, stuffData);
        }
        [PunRPC]
        public override void SetStuffPieceMechanicalStatesFromNetwork(int pieceId, string mechanicalStateData)
        {
            //Debug.Log($"PunRPC SetStuffPieceMechanicalStatesFromNetwork pieceId : {pieceId}, data : {mechanicalStateData}");
            //messenger.SetStuffPieceMechanicalStatesFromNetwork(pieceId, mechanicalStateData);
        }

        [PunRPC]
        public override void SetStuffPieceVanishFromNetwork(int pieceId, float moveTime)
        {
            //Debug.Log($"PunRPC SetStuffPieceVanishFromNetwork pieceId : {pieceId}, moveTime : {moveTime} ");
            //messenger.SetStuffPieceVanishFromNetwork(pieceId, moveTime);
        }

        // ----------------------------------------------------------------
        public override void LeaveRoom()
        {
            PhotonNetwork.LeaveRoom();
        }

        // 자신이 시합에서 나감.
        public override void LeaveMatch()
        {
            if (Assets.Scripts.Often.PracticeMatchTestRoute.Enabled)
            {
                matchRequestPending = false; matchRequestCancelled = true; opponentPlayer = null;
                GetComponent<PracticeMatchSetupTransport>()?.LeaveSession();
                CallNetworkState(NetworkState.LeftRoom);
                return;
            }
            if (matchRequestPending)
            {
                matchRequestPending = false;
                matchRequestCancelled = true;
                PhotonNetwork.Disconnect();
            }
            Debug.Log($"### PunNetwork LeaveMatch  PhotonNetwork.NetworkClientState : {PhotonNetwork.NetworkClientState}");
            if (PhotonNetwork.NetworkClientState == ClientState.Joined)
            {
                Debug.Log("LeaveRoom");
                PhotonNetwork.RemoveRPCs(PhotonNetwork.LocalPlayer);
                PhotonNetwork.DestroyPlayerObjects(PhotonNetwork.LocalPlayer);
                PhotonNetwork.LeaveRoom();

            }
            opponentPlayer = null;
            CallNetworkState(NetworkState.LeftRoom);

            // 
            //string homeScene = "c__10";
            //SceneManager.LoadScene(homeScene);

        }
        public override void LeaveMatch2()
        {
            Debug.Log("### PunNetwork LeaveMatch2 ");
            CallNetworkState(NetworkState.LeftRoom);
            //       string homeScene = "c__10";
            //SceneManager.LoadScene(homeScene);
        }




        #region LEFT, DISCONNECT

        // 상대의 의해 방 나감.
        [PunRPC]
        public override void OnOpponentLeftMatch()
        {
            if (Assets.Scripts.Often.PracticeSceneFlow.OnlineSession != null) return;
            //adapter.homeMenuManager.RoomResult(1);
            Debug.Log("### PunNetwork OnOpponentLeftMatch ");
            PhotonNetwork.LeaveRoom();        // 상대가 나간후 자신도 나감.
            //opponentPlayer = null;
            PhotonNetwork.RemoveRPCs(PhotonNetwork.LocalPlayer);
            PhotonNetwork.DestroyPlayerObjects(PhotonNetwork.LocalPlayer);  // 네트워크 플레이어의 모든 네트워크 객체를 소멸 처리.
            CallNetworkState(NetworkState.ResultRoom);
            //messenger.OnOpponentLeftMatch();
            //CallNetworkState(NetworkState.ResultRoom);
        }


        // 방을 나가면
        public override void OnLeftRoom()
        {
            if (Assets.Scripts.Often.PracticeSceneFlow.OnlineSession != null) return;
            Debug.LogWarning("PunNetwork OnLeftRoom  state : " + state);

            opponentPlayer = null;
            CallNetworkState(NetworkState.LeftRoom);

        }


        public override void Disconnect()
        {
            Debug.LogWarning("PunNetwork Disconnect    ");
            if (PhotonNetwork.NetworkClientState != ClientState.Disconnected)
            {
                PhotonNetwork.Disconnect();
            }
            CallNetworkState(NetworkState.Disconnected);
            isConnectOk = false;

        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            if (Assets.Scripts.Often.PracticeSceneFlow.OnlineSession != null) return;
            matchRequestPending = false;
            LogMatchContext("Disconnected:" + cause);
            //Debug.LogWarning("PunNetwork OnDisconnected    ");
            CallNetworkState(NetworkState.LostConnection);
            tryToConnect = false;
            Debug.LogWarning(state);
        }

        public override void OnCreateRoomFailed(short returnCode, string message)
        {
            matchRequestPending = false;
            LogMatchContext("CreateRoomFailed:" + returnCode);
            CallNetworkState(NetworkState.RoomCreateFailed);
        }

        public override void OnJoinRoomFailed(short returnCode, string message)
        {
            if (Assets.Scripts.Often.PracticeSceneFlow.OnlineSession != null) return;
            matchRequestPending = false;
            LogMatchContext("JoinRoomFailed:" + returnCode);
            CallNetworkState(NetworkState.JoinRoomFailed);
        }

        public override void OnJoinRandomFailed(short returnCode, string message)
        {
            matchRequestPending = false;
            LogMatchContext("JoinRandomFailed:" + returnCode);
            CallNetworkState(NetworkState.JoinRoomFailed);
        }



        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            if (Assets.Scripts.Often.PracticeSceneFlow.OnlineSession != null) return;
            OtherDisconnect++;
            bool isOut = OtherDisconnectMax <= OtherDisconnect;
            //Debug.Log("OnPlayerLeftRoom otherPlayer : " + otherPlayer.NickName + ", OtherDisconnect : " + OtherDisconnect + ", isOut : " + isOut);

            //PhotonNetwork.RemoveRPCs(otherPlayer);   // 네트워크 플레이어의 모든 Buffered RPC를 소멸처리.
            //PhotonNetwork.DestroyPlayerObjects(otherPlayer);  // 네트워크 플레이어의 모든 네트워크 객체를 소멸 처리.
            //CallNetworkState(NetworkState.OpponentLeftRoom);
            //PhotonNetwork.LeaveRoom();        // 상대가 나간후 자신도 나감.

            if (isOut)
            {
                RoomPlayerUpdate();
                PhotonNetwork.RemoveRPCs(otherPlayer);   // 네트워크 플레이어의 모든 Buffered RPC를 소멸처리.
                PhotonNetwork.DestroyPlayerObjects(otherPlayer);  // 네트워크 플레이어의 모든 네트워크 객체를 소멸 처리.
                Debug.LogWarning("OnPhotonPlayerDisconnected ........................... 끊김 또는 나감");

                CallNetworkState(NetworkState.OpponentLeftRoom);
            }
            else
            {
                // 재접속전까지 대기.

            }
            Debug.LogWarning($"PunNetwork OnPlayerLeftRoom  OtherDisconnect : {OtherDisconnect} , isOut : {isOut} ");

        }

        #endregion


        // RPC 상대에게 테스트맞춤 보냄
        //[PunRPC]
        //public void SetTestHitFromNetwork(bool value)
        //{
        //    messenger.SetTestHitFromNetwork(value);
        //}

        // RPC 상대에게 테스트맞춤 보냄
        [PunRPC]
        public void SetTestHit(int value)
        {
            Debug.Log("PunRPC SetTestHit  : ---------------------------------------------- " + value + ", match_conn : " + match_conn_type + ", isMatchConn : " + isMatchConn);
        }



        [PunRPC]
        public override void SetBallMovStoryFromNetwork(float moveTime, string StoryData)
        {
            //Debug.Log($"PunRPC SetStuffCreateFromNetwork  moveTime : {moveTime}, stuffData : {stuffData} ");
            messenger.SetBallMovStoryFromNetwork(moveTime, StoryData);
        }
        [PunRPC]
        public override void SetMatchSummaryFromNetwork(string summaryData)
        {
            //Debug.Log($"PunRPC SetStuffCreateFromNetwork  moveTime : {moveTime}, stuffData : {stuffData} ");
            messenger.SetMatchSummaryFromNetwork(summaryData);
        }
    }
}
