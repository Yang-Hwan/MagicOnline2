using BackEnd;
using Cysharp.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Assets.Scripts.Exert.Network
{

    public delegate void LoadPlayerHandler(PlayerProfile player);
    public delegate void LoadPlayersHandler(PlayerProfile[] player);
    public delegate bool ChackIsFriend(string id);




    public enum RoomEditType
    {
        None,
        Add,
        Remove,
        Edit,
    }

    public enum PlayerState
    {
        Master = 0,
        Away,
        Busy,
        Offline,
        Playing
    }

    public class PlayerProfile
    {
        public string uuid
        {
            get;
            private set;
        }
        
        public int roomId
        {
            get;
            set;
        }

        public bool isFriend
        {
            get;
            private set;
        }

        public string nickName
        {
            get;
            private set;
        }

        public long coin
        {
            get;
            private set;
        }

        public long prize
        {
            get;
            set;
        }

        public Texture2D image
        {
            get;
            private set;
        }

        public string imageURL
        {
            get;
            private set;
        }

        public PlayerState state
        {
            get;
            set;
        }


        public string remark
        {
            get;
            private set;
        }

        public bool canPlay
        {
            get
            {
                return coin > prize;
            }
        }


        public PlayerProfile(string uuid, int roomId, bool isFriend, string nickname, long coins, long prize, PlayerState state, string remark = "")
        {
            //Debug.Log($"PlayerProfile nick : {nickname}, coin : {coin}, room : {roomId}, state : {state}");
            this.uuid = uuid;
            this.roomId = roomId;
            this.isFriend = isFriend;
            this.nickName = nickname;
            this.coin = coins;
            this.prize = prize;
            this.state = state;
            this.remark = remark;

        }

    }


    public class RoomChange
    {
        public int LimitCnt { get; private set; }
        public int PlayerCnt { get; set; }
        public string Name { get; private set; }
        public int HallIdx { get; private set; }
        public long Coin { get; private set; }
        public RoomEditType ChangeType { get; private set; }

        public RoomChange(int limitCnt, int playerCnt, string name, int hall_idx, long coin, RoomEditType changeType)
        {
            LimitCnt = limitCnt;
            PlayerCnt = playerCnt;
            Name = name;
            HallIdx = hall_idx;
            Coin = coin;
            ChangeType = changeType;
        }
    }



    public class Room
    {
        public int id
        {
            get;
            private set;
        }

        public long prize
        {
            get;
            set;
        }

        public PlayerProfile mainPlayer
        {
            get;
            private set;
        }

        public List<PlayerProfile> players
        {
            get;
            private set;
        }


        public Room(int id, long prize, List<PlayerProfile> players)
        {
            this.id = id;
            this.prize = prize;
            this.mainPlayer = players == null || players.Count == 0 ? null : players[0];
            this.players = players;
        }
    }


    public class NetworkManager
    {

        public static event LoadPlayerHandler OnMainPlayerLoaded;
        public static event LoadPlayersHandler OnMainPlayersLoaded;
        public static bool initialized = false;
        
        private static NetworkEngine _network;
        private static long prize_min = 1000;

        public static string uuid { get; set; }
        public static string nickName { get; set; }
        public static long user_coin { get; set; }
        public static int hallIdx { get; set; }


        public static PlayerProfile mainPlayer
        {
            get;
            private set;
        }

        public static PlayerProfile opponentPlayer
        {
            get;
            set;
        }


        private static PlayerProfile[] _players;

        public static PlayerProfile[] players
        {
            get
            {
                if (_players == null)
                {

                }
                return _players;
            }
        }


        // Teardown must never use the lazy getter: it could recreate Network/PhotonMono.
        public static bool TryGetExistingNetwork(out NetworkEngine existing)
        {
            existing = _network;
            return existing != null;
        }

        public static NetworkEngine network
        {
            get
            {
                if (!_network)
                {
                    _network = (new GameObject("Network")).AddComponent<PunNetwork>();
                    _network.Initialize();
                }

                return _network;
            }
        }

        public static void Disable()
        {
            if (_network)
            {
                _network.Disable();
            }
        }


        public async static UniTaskVoid LoadMainPlayer()
        {
            await UniTask.Yield();
            string msg = "";
            if (NetworkManager.mainPlayer == null)
            {
                Debug.Log($"NetworkManager.mainPlayer == null    .....  uuid : {uuid}, nick : {nickName}, user_coin : {user_coin}, prize_min : {prize_min} ");
                string nick = Backend.UserInDate;
                long coin = 0;// BackendGame.Instance.UserMainData.Coin;
                mainPlayer = new PlayerProfile(uuid, -1, false, nick, coin, prize_min, PlayerState.Master);

                //// Debug.Log(msg);
                if (OnMainPlayerLoaded != null)
                {
                    OnMainPlayerLoaded(mainPlayer);
                }
            }
            else
            {
                Debug.Log($"NetworkManager.mainPlayer != null    .....  ");
            }


        }

        public static IEnumerator LoadMainPlayer2()
        {

            yield return null;
            string msg = "";
            if (NetworkManager.mainPlayer == null)
            {
                //Debug.Log($"uuid : {uuid}, nick : {nickName}, user_coin : {user_coin}, prize_min : {prize_min} ");
                string nick = Backend.UserInDate;
                long coin = 0;// BackendGame.Instance.UserMainData.Coin;
                mainPlayer = new PlayerProfile(uuid, -1, false, nick, coin, prize_min, PlayerState.Master);
            }

            //// Debug.Log(msg);
            if (OnMainPlayerLoaded != null)
            {
                OnMainPlayerLoaded(mainPlayer);
            }
        }


        public static void UpdatePlayers()
        {
            _players = null;
            LoadPlayers();
        }

        private static void LoadPlayers()
        {
            //NetworkManager.network.LoadPlayers(ref _players);
            //if (OnPlayersLoaded != null)
            //{
            //    OnPlayersLoaded(_players);
            //}
        }



        public static PlayerProfile PlayerFromString(string playerData, ChackIsFriend friendChecker = null)
        {
            string str = "";
            int step = 0;

            string id = "-1";
            string imageURL = "";
            string imageName = "";
            bool isFrient = false;
            string userName = "";
            PlayerState state = PlayerState.Offline;
            long coins = 0;
            long prize = 0;
            int roomId = -1;
            foreach (char item in playerData)
            {
                if (item != ';')
                {
                    str += item;
                }
                else
                {
                    step++;
                    switch (step)
                    {
                        case 1:
                            id = str;
                            isFrient = friendChecker != null ? friendChecker(id) : false;
                            break;
                        case 2:
                            imageURL = str;
                            break;
                        case 3:
                            imageName = str;
                            break;
                        case 4:
                            userName = str;
                            break;
                        case 5:
                            state = (PlayerState)int.Parse(str);
                            break;
                        case 6:
                            coins = long.Parse(str);
                            break;
                        case 7:
                            prize = long.Parse(str);
                            break;
                        default:
                            break;
                    }
                    str = "";
                }
            }
            return new PlayerProfile(id, roomId, isFrient, userName, coins, prize, state);
        }


        public static string PlayerToString(PlayerProfile playerProfile)
        {
            if (playerProfile == null)
            {
                return "";
            }
            return playerProfile.uuid + ";" + "" + ";" + "" + ";" + playerProfile.nickName + ";" + (int)playerProfile.state + ";" + playerProfile.coin + ";" + playerProfile.prize + ";";
        }


    }
}
