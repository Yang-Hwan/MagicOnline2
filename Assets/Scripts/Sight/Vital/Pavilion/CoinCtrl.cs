using Assets.Scripts.Exert.Match;
using Assets.Scripts.Often;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Sight.Vital.Pavilion
{
    public class CoinCtrl : MonoBehaviour
    {

        [Header("UI references")]
        [SerializeField] TextMeshProUGUI[] coinTxts;
        [SerializeField] Transform[] targetCoin;
        [SerializeField] TMP_Text coinUIText;
        [SerializeField] GameObject animatedCoinPrefab;

        [Space]
        [Header("Available coins : (coins to pool)")]
        [SerializeField] int maxCoins;
        Queue<GameObject> coinsQueue = new Queue<GameObject>();
        [Space]
        [Header("Animation settings")]
        [SerializeField] [Range(0.2f, 0.5f)] float minAnimDuration;
        [SerializeField] [Range(0.5f, 2.0f)] float maxAnimDuration;

        [SerializeField] Ease easeType;
        [SerializeField] float spread;
        protected DrawStuffPos drawStuff;

        public int coin_ea_val {get; private set;}
        private void Awake()
        {
            coinTxts = new TextMeshProUGUI[2];
            coinTxts[0] = GameObject.Find("Canvas/GesPnl/PnlMatch/PlayerBoxs/PlayerSelf/TxtMatchCoin").GetComponent<TextMeshProUGUI>();
            coinTxts[1] = GameObject.Find("Canvas/GesPnl/PnlMatch/PlayerBoxs/PlayerOther/TxtMatchCoin").GetComponent<TextMeshProUGUI>();
            targetCoin = new Transform[2];
            targetCoin[0] = GameObject.Find("Table/Addition/Position/TargetCoinSelf").transform;
            targetCoin[1] = GameObject.Find("Table/Addition/Position/TargetCoinOther").transform;
            drawStuff = FindObjectOfType<DrawStuffPos>();

            coin_ea_val = 10;

            PrepareCoins();

        }

        void PrepareCoins()
        {
            GameObject coin;
            for (int i = 0; i < maxCoins; i++)
            {
                coin = Instantiate(animatedCoinPrefab);
                coin.transform.parent = transform;
                coin.SetActive(false);
                coinsQueue.Enqueue(coin);
            }
        }

        public int turnId { get; private set; }

        public int Coins
        {
            get { return (int)PoolPlayer.player(turnId).matchCoin; }
            set
            {
                //PoolPlayer.currentPlayer.coin = 
                //_c[PoolPlayer.turnId] = value;
                PoolLogic.gameState.coinGain += value;
                PoolPlayer.SetHitAdd(turnId, value);
                //Debug.Log($"Coins value :{value}, playerid : {PoolPlayer.player(turnId).playerId},  matchCoin : {PoolPlayer.player(turnId).matchCoin} ");
                //update UI text whenever "Coins" variable is changed
                coinTxts[turnId].text = Utility.CoinNumToStr(Coins);
                //coinUIText.text = Coins.ToString();
            }
        }

        public void AddCoins(Vector3 collectedCoinPosition, int amount, int current_turn)
        {
            CoinPickUpAnimate(collectedCoinPosition, amount, current_turn).Forget();
            //Debug.Log("ADD COIN ~~~~~~~ " + amount + ", collectedCoinPosition : " + collectedCoinPosition);
        }


        async UniTaskVoid CoinPickUpAnimate(Vector3 collectedCoinPosition, int amount, int current_turn)
        {
            turnId = current_turn;
            if (current_turn != PoolPlayer.currentPlayer.playerId)
            {
                Debug.Log($"ERROR !!! CoinPickUpAnimate current_turn : {current_turn} ... current_id : {PoolPlayer.currentPlayer.playerId}    ---------------------------  ");
            }

            Transform _targetCoin = targetCoin[current_turn];
            for (int i = 0; i < amount; i++)
            {
                //check if there's coins in the pool
                if (coinsQueue.Count > 0)
                {
                    //extract a coin from the pool
                    GameObject coin = coinsQueue.Dequeue();
                    coin.SetActive(true);
                    //move coin to the collected coin pos
                    coin.transform.position = collectedCoinPosition + new Vector3(UnityEngine.Random.Range(-spread, spread), 0f, 0f);

                    //animate coin to target position
                    float duration = UnityEngine.Random.Range(minAnimDuration, maxAnimDuration);
                    //Debug.Log($"COIN Animate :::::: {coin.name}, duration : {duration}, start pos : {coin.transform.position}, end pos : {target.position}");
                    coin.transform.DOMove(_targetCoin.position, duration)
                    .SetEase(easeType)
                    .OnComplete(() =>
                    {
                        //executes whenever coin reach target position
                        coin.SetActive(false);
                        coinsQueue.Enqueue(coin);

                        Coins = coin_ea_val;
                        //Debug.Log($"I : {i} .. Coins = 10;");
                        //Coins++;
                    });
                }
                await UniTask.Yield();
            }
        }

        public async UniTask CoinPickUpAnimate2(Vector3 collectedCoinPosition, int amount, int current_turn)
        {
            turnId = current_turn;
            if (current_turn != PoolPlayer.currentPlayer.playerId)
            {
                Debug.LogError($"CoinPickUpAnimate current_turn : {current_turn} ... current_id : {PoolPlayer.currentPlayer.playerId}    ---------------------------  ");
            }

            Transform _targetCoin = targetCoin[current_turn];
            for (int i = 0; i < amount; i++)
            {
                //check if there's coins in the pool
                if (coinsQueue.Count > 0)
                {
                    //extract a coin from the pool
                    GameObject coin = coinsQueue.Dequeue();
                    coin.SetActive(true);
                    //move coin to the collected coin pos
                    coin.transform.position = collectedCoinPosition + new Vector3(UnityEngine.Random.Range(-spread, spread), 0f, 0f);

                    //animate coin to target position
                    float duration = UnityEngine.Random.Range(minAnimDuration, maxAnimDuration);
                    //Debug.Log($"COIN Animate :::::: {coin.name}, duration : {duration}, start pos : {coin.transform.position}, end pos : {target.position}");
                    coin.transform.DOMove(_targetCoin.position, duration)
                    .SetEase(easeType)
                    .OnComplete(() =>
                    {
                        //executes whenever coin reach target position
                        coin.SetActive(false);
                        coinsQueue.Enqueue(coin);

                        Coins = coin_ea_val;
                        //Debug.Log($"I : {i} .. Coins = 10;");
                        //Coins++;
                    });
                }
                await UniTask.Yield();
            }

            drawStuff.effectStuffEa--;

        }

    }
}
