using Assets.Scripts.Exert.Network;
using Assets.Scripts.Often;
using Assets.Scripts.Prack.BackSys;
using Assets.Scripts.Sight.Vital.Pavilion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.Scripts.Exert.Match
{


    public delegate void OnMoreTimeHandler();
   //public delegate void OnChangeTurnReady();
    public delegate void OnChangeTurnWait();
    public delegate void TurnChangeHandler();
    public delegate void PlayerActionHandler(PoolPlayer player);

    public class PoolPlayer
    {

        public static event OnMoreTimeHandler OnOneMoreTime;
        //public static event OnChangeTurnReady OnChangeTurnReady;
        public static event OnChangeTurnWait OnChangeTurnWait;
        public static event TurnChangeHandler OnTurnChanged;
        public static event PlayerActionHandler OnPlayerInitialized;



        public OneMore oneMore
        {
            get;
            private set;
        }

        // 한 게임별
        public int hitCnt
        {
            get;
            private set;
        }

        public FinishStep finishStep
        {
            get;
            set;
        }

        public int checkCushion
        {
            get;
            set;
        }


        // 한 이닝별
        public int hitInning
        {
            get;
            private set;
        }
        // 한 이닝별
        public int hitHigh
        {
            get;
            private set;
        }
        // 한 큐별(점수)
        //public int hitGo
        //{
        //    get;
        //    private set;
        //}


        public int winCnt
        {
            get;
            private set;
        }

        public int playerId
        {
            get;
            private set;
        }

        public string name
        {
            get;
            set;
        }

        public string uuid
        {
            get;
            set;
        }


        public long coin
        {
            get;
            private set;
        }

        public long matchCoin
        {
            get;
            private set;
        }

        public object avatar
        {
            get;
            protected set;
        }

        public string avatarURL
        {
            get;
            private set;
        }

        public List<Ball> balls
        {
            get;
            protected set;
        }

        public static int playersCount
        {
            get;
            set;
        }

        public static PoolPlayer[] players
        {
            get;
            set;
        }

        public static bool initialized
        {
            get
            {
                return players != null;
            }
        }



        // 매치마다 한번 발생.
        public static int idx
        {
            get;
            set;
        }

        // 선수 진행 순서과정체크
        public static int ord
        {
            get;
            set;
        }


        /// <summary>
        /// if 0: Main Player turn els , if 1: Other Player turn.
        /// </summary>
        public static int turnId
        {
            get;
            set;
        }
        public bool isWinner
        {
            get;
            set;
        }
        public bool myTurn
        {
            get;
            private set;
        }

        public static int inning
        {
            get;
            set;
        }

        public static long prize
        {
            get
            {
                if (players == null || players.Length == 0)
                {
                    return 20;
                }
                long prize = players[0].GetPrize();
                if (prize == 0)
                {
                    prize = 20;
                }
                return prize;
            }
            set
            {
                if (players != null && players.Length != 0)
                {

                    long prize = value;
                    if (prize >= 0)
                    {
                        players[0].SavePrize(prize);
                    }
                }
            }
        }

        protected virtual void SavePrize(long prize)
        {
            // NetworkManager.social.SaveMainPlayerPrize(prize);
        }

        protected virtual long GetPrize()
        {
            return 0;// NetworkManager.social.GetMainPlayerPrize();
        }

        public static PoolPlayer mainPlayer
        {
            get { return (players == null || players.Length < 1) ? null : players[0]; }
        }

        public static PoolPlayer player(int PlayerId)
        {
            return (players == null || players.Length < 1) ? null : players[PlayerId]; 
        }


        public static PoolPlayer otherPlayer
        {
            get { return (players == null || players.Length < 2) ? null : players[1]; }
        }

        public static void PlayerScoreClear()
        {
//            players.Where(r => r.playerId == playerId).ToList().ForEach(r => r.winCnt = 0);
            players.ToList().ForEach(r => {
                r.winCnt = 0;
                r.isWinner = false;
            });


            //Debug.Log($"PoolPlayer.PlayerClear ~~~~~~~~~~~~~~~~~~~~~~~~~~ ");
        }

        public static void OneMoreReq(int playerId, OneMore oneMore)
        {
            players.Where(r => r.playerId == playerId).ToList().ForEach(r => r.oneMore = oneMore);
        }

        public static void ResetOneMore()
        {
            players.ToList().ForEach(r => r.oneMore = OneMore.Enable);
        }

        public static bool IsReMatch()
        {
            bool ret = false;
            int len = players.Length;
            int cnt = players.Where(r => r.oneMore == OneMore.Req).Count();
            ret = len == cnt;
            return ret;
        }

        public void SetCoin(long coin)
        {
            this.coin = coin;
        }

        public static void SetPlayerMatchCoin(long main_matchCoin, long other_matchCoin)
        {
            
            //Debug.Log($"SetPlayerMatchCoin myturn : {mainPlayer.myTurn}, main_matchCoin : {main_matchCoin}, other_matchCoin : {other_matchCoin}");
            if (mainPlayer.matchCoin != main_matchCoin || otherPlayer.matchCoin != other_matchCoin)
            {
                Debug.LogWarning($"SetPlayerMatchCoin myturn : {mainPlayer.myTurn}, main_matchCoin : {main_matchCoin}, other_matchCoin : {other_matchCoin}");
                mainPlayer.matchCoin = main_matchCoin;
                otherPlayer.matchCoin = other_matchCoin;
            }
        }

        public static void SetHitMinus(int playerId, int cnt = -1)
        {

            foreach (PoolPlayer player in players)
            {
                if (player.playerId == playerId)
                {
                    if (player.hitCnt > 0)
                    {
                        player.hitCnt += cnt;
                        player.hitInning += cnt;

                        //player.hitGo++;
                    }
                    //player.coin += reward;

                }
                else
                {
                    //player.coin -= reward;
                }
            }
        }

        /// <summary>
        /// 맞춘개수추가
        /// </summary>
        /// <param name="playerId"></param>
        public static void SetHitAdd(int playerId, int reward, int cnt = 0)
        {
            //Debug.Log("PoolPlayer SetHitAdd playerId : " + playerId + ", reward : " + reward);
            //PoolPlayer p = players[playerId];
            //if (p.playerId == playerId)
            //{
            //    if (isBall)
            //    {
            //        p.hitCnt++;
            //        p.hitInning++;
            //        p.hitGo++;
            //    }
            //    //player.coin += reward;
            //    p.matchCoin += reward;
            //}

            foreach (PoolPlayer player in players)
            {
                if (player.playerId == playerId)
                {
                    if (cnt > 0)
                    {
                        player.hitCnt += cnt;
                        player.hitInning += cnt;

                        if(player.hitInning > player.hitHigh)
                        {
                            player.hitHigh = player.hitInning;
                        }
                        //player.hitGo++;
                    }
                    //player.coin += reward;
                    player.matchCoin += reward;

                }
                else
                {
                    //player.coin -= reward;
                }
            }
        }

        /// <summary>
        /// 맞춘개수추가
        /// </summary>
        /// <param name="playerId"></param>
        //public static void SetWinAdd(int playerId)
        //{
        //    foreach (PoolPlayer player in players)
        //    {
        //        if (player.playerId == playerId)
        //        {
        //            player.winCnt++;
        //            Debug.Log($"id : {playerId}, win : {player.winCnt}");
        //        }
        //    }
        //}

        public static bool IsOneMoreTimeOk()
        {
            bool isOk = true;
            UnityEngine.Debug.Log("IsOnMoreTimeOk player cnt : " + players.Length);

            foreach (PoolPlayer player in players)
            {
                if (player.oneMore == OneMore.Req)
                {
                    isOk = false;
                    break;
                }
            }
            return isOk;
        }

        // 게임 시작시
        public static void ResetMatch(FinishStep finish, int cushion)
        {
            if (players == null)
            {
                Debug.LogWarning("player len null");
                return;
            }
            if (players.Length == 0)
            {
                Debug.LogWarning("player len 0");
                return;
            }
            foreach (PoolPlayer player in players)
            {
                player.matchCoin = 0;          //
                player.hitCnt = 0;          //
                player.hitInning = 0;
                player.hitHigh = 0;
                player.finishStep = finish;
                player.checkCushion = cushion;
                //player.winCnt = 0;
                player.isWinner = false;
            }

            PoolPlayer.inning = 0;
            PoolPlayer.ord = 0;
            Debug.Log($"ResetMatch COIN ( MAIN : {mainPlayer.matchCoin}, OTHER : {otherPlayer.matchCoin} ) .... WIN ( MAIN : {mainPlayer.winCnt}, OTHER : {otherPlayer.winCnt}  )");
        }

        // 턴변경때마다 초기화
        public static void TurnHitReset(int playerId)
        {
            foreach (PoolPlayer player in players)
            {
                if (player.playerId == playerId)
                {
                    player.hitInning = 0;
                }
            }
        }

        // 스트로크 마다 초기화.(ShotEnd)
        public static void StrokeHitReset(int playerId)
        {
            foreach (PoolPlayer player in players)
            {
                if (player.playerId == playerId)
                {
                    //player.hitGo = 0;
                }
            }
        }

        // id : -1 무승부
        public static int GetHitWin()
        {
            int id = -1;
            if (mainPlayer.hitCnt == otherPlayer.hitCnt)
            {
                id = -1;
            }
            else if (mainPlayer.hitCnt > otherPlayer.hitCnt)
            {
                id = mainPlayer.playerId;
            }
            else if (mainPlayer.hitCnt < otherPlayer.hitCnt)
            {
                id = otherPlayer.playerId;
            }

            return id;
        }

        public static void SetWinner(int playerId)
        {
            long matchCoin = 0;
            foreach (PoolPlayer player in players)
            {
                if (player.playerId == playerId)
                {
                    matchCoin = player.matchCoin;
                    //player.coin += player.matchCoin;
                    player.isWinner = true;
                    player.winCnt++;
                }
            }

            foreach (PoolPlayer player in players)
            {
                if (player.playerId != playerId)
                {
                    //player.coin -= matchCoin;
                    player.isWinner = false;
                }
            }

        }


        public static PoolPlayer GetWinner()
        {
            foreach (PoolPlayer player in players)
            {
                if (player.isWinner)
                {
                    return player;
                }
            }
            return null;
        }


        public static PoolPlayer currentPlayer
        {
            get
            {
                foreach (PoolPlayer player in players)
                {
                    if (player.playerId == turnId)
                    {
                        return player;
                    }
                }
                return null;
            }
        }




        public static void UpdateCouns(bool startGame)
        {
            foreach (PoolPlayer player in players)
            {
                if (startGame)
                {
                    player.coin -= PoolPlayer.prize;
                }
                else if (player.isWinner)
                {
                    player.coin += 2 * PoolPlayer.prize;
                }
                if (player.coin <= 0)
                {
                    player.coin = 0;
                }
                //player.coins = Mathf.Clamp(player.coins, 0, player.coins);
            }
        }

        public static void SetTurn(int turnId)
        {
            PoolPlayer.turnId = turnId;
           // PoolPlayer.inning = 0;             // 
            //PoolPlayer.ord = 0;             // 
      

            for (int i = 0; i < players.Length; i++)
            {
                //players[i].hitHigh = 0;
                //players[i].hitInning = 0;
                //players[i].matchCoin = 0;
                if (turnId == i)
                {
                    players[i].myTurn = true;
                }
                else
                {
                    players[i].myTurn = false;
                }
                //Debug.Log($"SetTurn i : {i}, turn : {turnId},  {players[i].name} myturn : {players[i].myTurn} ...........................");
            }

            //Debug.Log($"SetTurn hitInn self : {players[0].hitInning}, other : {players[1].hitInning}   ======== ");
            //if (OnTurnChanged != null)
            //{
            //    OnTurnChanged();        // 
            //}
        }

        // 자신의 턴이 아니면 대기함. 상대에게 턴해도 된다고 보냄. 
        public static void ChangeTurnReady()
        {
            //Debug.Log("$$$$$ ChangeTurnReady myturn : " + PoolPlayer.mainPlayer.myTurn + ", (OnChangeTurnReady != null) : " + (OnChangeTurnReady != null));

            //if (OnChangeTurnReady != null)
            //{
            //    OnChangeTurnReady();
            //}

            //if (PoolLogic.controlInNetwork)
            //{
            //    Debug.Log("ChangeTurnReady");
            //    if(OnChangeTurnReady != null)
            //    {
            //        OnChangeTurnReady();
            //    }
            //}
            //else
            //{
            //    if(OnChangeTurnWait != null)
            //    {
            //        OnChangeTurnWait();
            //    }
            //}
        }

        // 상대에게 턴이 넘어감.. 
        public static void ChangeTurn()
        {

            //Debug.Log("ChangeTurn pre turnId : " + turnId + ", myturn : " + PoolPlayer.mainPlayer.myTurn + "......................");
            if (ord < players.Length - 1)
            {
                ord++;
            }
            else
            {
                ord = 0;
                inning++;
            }

            if (turnId < players.Length - 1)
            {
                turnId++;
            }
            else
            {
                turnId = 0;
            }
            for (int i = 0; i < players.Length; i++)
            {
                players[i].hitInning = 0;
                players[i].myTurn = turnId == i;
            }

            //Debug.Log($"ChangeTurn >> turn : {turnId}, myTurn : {PoolPlayer.mainPlayer.myTurn}, InNet : {PoolLogic.controlInNetwork}, ord : {ord}, inning : {inning}");

            PoolCoach.Instance.ActivePlayer();
            //if (OnTurnChanged != null)
            //{
            //    //OnTurnChanged();            // PoolCoach, TimeCtrl
            //}
        }


        public PoolPlayer(int playerId, string name, long coin, string uuid, object avatar, string avatarURL)
        {
            this.playerId = playerId;
            this.uuid = uuid;
            this.name = name;
            this.coin = coin;
            this.avatar = avatar;
            this.avatarURL = avatarURL;
            this.matchCoin = 0;
            this.hitCnt = 0;
            this.hitInning = 0;
            this.winCnt = 0;
            this.oneMore = OneMore.Enable;

            if (OnPlayerInitialized != null)
            {
                OnPlayerInitialized(this);
            }
        }

        public static void OnGotoPlayWithPlayer(PlayerProfile player)
        {
            // 디비에서 호출 필요.  player.nickname : 백앤드 기본키
            UserMainData user = BackendGame.Instance.UserMainOther(player.nickName);

            //UnityEngine.Debug.Log($"OnGotoPlayWithPlayer  other coin : {user.Coin} ====================");
            //long cc = PoolPlayer.players.Length;

            PoolPlayer.players[1] = new PoolPlayer(1, user.NickName, user.Coin, player.nickName, "", "");

        }

        public static void OnMainPlayerLoaded(int playerId, string name, long coin, string uuid, object avatar, string avatarURL, long prize)
        {
            //UnityEngine.Debug.Log("OnMainPlayerLoaded name :" + name);
            if (!PoolPlayer.initialized)
            {
                PoolPlayer.players = new PoolPlayer[2];

            }
            PoolPlayer.players[0] = new PoolPlayer(0, name, coin, uuid, avatar, avatarURL);


        }

        public static void SetIdx(int turnId)
        {
            PoolPlayer.turnId = turnId;
            PoolPlayer.idx = 0;
            PoolPlayer.ord = 0;

        }
    }
}
