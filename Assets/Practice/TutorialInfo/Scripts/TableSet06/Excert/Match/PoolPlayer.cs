using Assets.TutorialInfo.Scripts.TableSet06.Often;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.TutorialInfo.Scripts.TableSet06.Excert.Match
{

    public delegate void TurnChangeHandler();


    // 선수정보 
    public class PoolPlayer
    {
        public static void ApplyOnlineScores(int score0, int score1, int winner)
        {
            players[0].hitCnt = score0; players[1].hitCnt = score1;
            players[0].isWinner = winner == 0; players[1].isWinner = winner == 1;
        }
        public static PoolPlayer[] CapturePracticePlayers() =>
            players.Select(player => (PoolPlayer)player.MemberwiseClone()).ToArray();

        public static void RestorePracticePlayers(PoolPlayer[] snapshot, int turn, int order, int savedInning)
        {
            players = snapshot.Select(player => (PoolPlayer)player.MemberwiseClone()).ToArray();
            turnId = turn;
            ord = order;
            inning = savedInning;
        }
        public static event TurnChangeHandler OnTurnChanged;

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

        // 보유 코인
        public long coin
        {
            get;
            private set;
        }

        // 시합중 얻은 코인 
        public long matchCoin
        {
            get;
            private set;
        }

        // 승리한 수
        public int winCnt
        {
            get;
            private set;
        }

        // 자신의 턴일때 연속 맞춘개수
        public int hitTrain
        {
            get;
            private set;
        }

        // 시합당 맞춘누적갯수
        public int hitCnt
        {
            get;
            private set;
        }

        // 한 이닝별
        public int hitInning
        {
            get;
            private set;
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

        // 한 큐별(점수)
        public int hitGo
        {
            get;
            private set;
        }


        public PoolPlayer(int playerId, string name, long coin)
        {
            this.playerId = playerId;
            this.name = name;
            this.coin = coin;
        }


        //////////////////////////////////////////////////////////////////////////

        public static void OnMainPlayerLoaded(string name, long coin)
        {
            EnsurePlayerSlots();
            PoolPlayer.players[0] = new PoolPlayer(0, name, coin);
        }

        public static void OnGotoPlayWithPlayer(string name, long coin)
        {
            EnsurePlayerSlots();
            PoolPlayer.players[1] = new PoolPlayer(1, name, coin);
        }

        private static void EnsurePlayerSlots()
        {
            if (players != null && players.Length >= 2) return;
            var roster = players;
            System.Array.Resize(ref roster, 2);
            players = roster;
        }

        public static int ord
        {
            get;
            set;
        }

        public OneMore oneMore
        {
            get;
            private set;
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

        // 선수 진행 순서과정체크
        public static int turnId
        {
            get;
            set;
        }

        public static int inning
        {
            get;
            set;
        }

        public static PoolPlayer mainPlayer
        {
            get { return (players == null || players.Length < 1) ? null : players[0]; }
        }

        public static PoolPlayer otherPlayer
        {
            get { return (players == null || players.Length < 2) ? null : players[1]; }
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

        public static void SetWinner(int playerId)
        {
            long matchCoin = 0;
            foreach (PoolPlayer player in players)
            {
                if (player.playerId == playerId)
                {
                    matchCoin = player.matchCoin;
                    player.coin += player.matchCoin;
                    player.isWinner = true;
                    player.winCnt++;
                }
            }

            foreach (PoolPlayer player in players)
            {
                if (player.playerId != playerId)
                {
                    player.coin -= matchCoin;
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
                players[i].myTurn = turnId == i;
            }

            //Debug.Log($"ChangeTurn >> turn : {turnId}, myTurn : {PoolPlayer.mainPlayer.myTurn}, InNet : {PoolLogic.controlInNetwork}, ord : {ord}, inning : {inning}");

            PoolCoach.Instance.TurnChanged();
            if (OnTurnChanged != null)
            {
                //OnTurnChanged();            // PoolCoach, TimeCtrl
            }
        }

        public static void SetHitAdd(int playerId, int reward, bool isBall = true)
        {
            //Debug.Log("PoolPlayer SetHitAdd playerId : " + playerId + ", reward : " + reward);
            foreach (PoolPlayer player in players)
            {
                if (player.playerId == playerId)
                {
                    if (isBall)
                    {
                        player.hitTrain++;
                        player.hitCnt++;
                        player.hitInning++;  // 오류??? 연타와 일치?? 맞추면 이닝 추가 없는데??
                        player.hitGo++;
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

        public static void SetHitMinus(int playerId)
        {
            var player = players.First(p => p.playerId == playerId);
            if (player.hitCnt > 0)
            {
                player.hitCnt--;
                player.hitInning--;
            }
        }

        // 게임 시작시
        public static void ResetMatch()
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
                player.hitTrain = 0;        // 연타
                player.hitCnt = 0;          // 점수
                player.hitInning = 0;
                player.hitGo = 0;            // 큐별점수(보통 1점)
                player.isWinner = false;
            }
        }

        public static void SetTurn(int turnId)
        {
            //Debug.Log($"turnId : {turnId}");
            PoolPlayer.turnId = turnId;
            PoolPlayer.ord = 0;

            for (int i = 0; i < players.Length; i++)
            {
                players[i].hitInning = 0;
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
            
            //OnTurnChanged?.Invoke();        // 
        }

 

        // 턴변경때마다 초기화
        public static void TurnHitReset(int playerId)
        {
            foreach (PoolPlayer player in players)
            {
                if (player.playerId == playerId)
                {
                    player.hitInning = 0;
                    player.hitTrain = 0;
                    player.matchCoin = 0;
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
                    player.hitGo = 0;
                }
            }
        }



    }
}
