using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Assets.Scripts.Sight.Vital.Pavilion;
using Assets.Scripts.Often;
using Assets.Scripts.Exert.Network;

namespace Assets.Scripts.Sight.Surface.Pavilion
{
    public class ShotAchieveAni : MonoBehaviour
    {


        public GameObject sideBeamObj;
        public GameObject achieveMsgObj;
        public GameObject runPathMsgObj;
        public Transform achieveBoxPos;

        int cnt_max_coin = 30;
        public GameObject[] coins;

        PhysicsMng physicsManager;
        CoinCtrl coinCtrl;
        CancellationTokenSource coinAnimationCancellation;

        private int[] _c = new int[2];


        [Serializable]
        public class AchievePool
        {
            public string kind_msg;
            public Color color;
            public Sprite sprite;
        }




        [Serializable]
        public class RunPathPool
        {
            public RunPath runPath;
            public Sprite sprite;
        }

        [SerializeField] AchievePool[] achieveMsgs;
        [SerializeField] RunPathPool[] runPathMsgs;

        private void Awake()
        {
            if (!NetworkManager.initialized)
            {
                enabled = false;
                return;
            }

            //physicsManager = FindObjectOfType<PhysicsMng>();
            coinCtrl = FindObjectOfType<CoinCtrl>();
            coins = new GameObject[cnt_max_coin];

            sideBeamObj = transform.Find("SideBeam").gameObject;
            achieveMsgObj = transform.Find("AchieveMsg").gameObject;
            runPathMsgObj = transform.Find("RunPathMsg").gameObject;
            achieveBoxPos = transform;
            //Debug.Log("ShotAchieveAni Awake ---------");


        }


        void StopAnimation()
        {
            coinAnimationCancellation?.Cancel();
            coinAnimationCancellation?.Dispose();
            coinAnimationCancellation = null;
            if(coinCtrl)coinCtrl.StopVisualCoins();
            if (coins != null)
                for (int i = 0; i < coins.Length; i++)
                {
                    // Unity destroyed objects are not handled by the ?. operator.
                    if (coins[i]) coins[i].SetActive(false);
                    coins[i] = null;
                }
            if (sideBeamObj) sideBeamObj.transform.DOKill();
            if (achieveMsgObj) achieveMsgObj.transform.DOKill();
            if (runPathMsgObj) runPathMsgObj.transform.DOKill();
        }

        void OnDisable() => StopAnimation();
        void OnDestroy() => StopAnimation();

        async UniTask CoinBring(int turnId, int cnt, CancellationToken cancellationToken,Action<float> progress=null)
        {
            cnt = Mathf.Clamp(cnt, 0, coins.Length);
            int arrived=0;
            Action onArrival=()=> {
                if(cancellationToken.IsCancellationRequested)return;
                arrived++;
                progress?.Invoke((float)arrived/Mathf.Max(1,cnt));
            };
            bool dispatched=false;
            try {
            for (int i = 0; i < cnt; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!ObjectPooler.instance || !coinCtrl) return;
                int r = UnityEngine.Random.Range(0, 6);
                float pos_d = r == 0 ? 3 : 1;
                float x = UnityEngine.Random.Range(-0.1f * pos_d, 0.1f * pos_d);
                float y = 0.1f + (0.01f * i);
                float z = UnityEngine.Random.Range(-0.07f * pos_d, 0.07f * pos_d);
                Vector3 pos = new Vector3(x, y, z);
                coins[i] = ObjectPooler.instance.SpawnFromPool(ObjectPool.GoldCoin.ToString(), pos);
                if (!coins[i]) continue;
                coins[i].transform.localRotation = Quaternion.Euler(90f, 0, 0);
                coins[i].transform.localScale = Vector3.one * 0.4f;
                if (i % 5 == 0)
                {
                    await UniTask.Delay(100, cancellationToken: cancellationToken);
                }
            }

            await UniTask.Delay(1000, cancellationToken: cancellationToken);
            for (int i = 0; i < cnt; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!ObjectPooler.instance || !coinCtrl) return;
                if (!coins[i]) { if(progress!=null)onArrival(); continue; }
                Vector3 pos = coins[i].transform.position;
                Vector3 pos_light = pos;
                pos_light.y = 0.3f;
                if(progress!=null)coinCtrl.AddVisualCoin(pos,turnId,onArrival);
                else coinCtrl.AddCoins(pos, 1, turnId);
                ObjectPooler.instance.SpawnFromPool(ObjectPool.CoinLightUp.ToString(), pos_light);
                coins[i].SetActive(false);
                coins[i] = null;
                await UniTask.Delay(70, cancellationToken: cancellationToken);
            }

            dispatched=true;
            } finally {
                if(!dispatched && !cancellationToken.IsCancellationRequested)progress?.Invoke(1);
            }
        }

        public bool PlayCoinGain(int playerId,long amount,Action<float> progress)
        {
            if(!isActiveAndEnabled || !coinCtrl || coins==null)return false;
            StopAnimation();
            if(sideBeamObj)sideBeamObj.transform.localScale=Vector3.zero;
            if(achieveMsgObj)achieveMsgObj.transform.localScale=Vector3.zero;
            if(runPathMsgObj)runPathMsgObj.transform.localScale=Vector3.zero;
            coinAnimationCancellation=CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            int count=(int)Math.Max(1,Math.Min(cnt_max_coin,amount/Math.Max(1,coinCtrl.coin_ea_val)));
            CoinBring(playerId,count,coinAnimationCancellation.Token,progress).Forget();
            return true;
        }



        // 0.03 .. 0.01
        public void AniAchieve(int turnId, int idx, int reward , RunPath _runPath = RunPath.None)
        {
            if (!isActiveAndEnabled || !coinCtrl || coinCtrl.coin_ea_val <= 0) return;
            StopAnimation();
            coinAnimationCancellation = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            int coin_len = reward / (1 * coinCtrl.coin_ea_val);
            if(MultiplayerSentimentController.Active) {
                sideBeamObj.transform.localScale=Vector3.zero;
                achieveMsgObj.transform.localScale=Vector3.zero;
                runPathMsgObj.transform.localScale=Vector3.zero;
                CoinBring(turnId,coin_len,coinAnimationCancellation.Token).Forget();
                return;
            }
            if (idx == -1)
            {
                idx = UnityEngine.Random.Range(0, achieveMsgs.Length);

                //int idx_r = UnityEngine.Random.Range(0, runPathMsgs.Length);
                //if(idx_r != 0) _runPath = runPathMsgs[idx_r].runPath;
            }

            Image sideBeamColor = sideBeamObj.GetComponent<Image>();
            sideBeamColor.color = achieveMsgs[idx].color;

            Image achieveMsgImg = achieveMsgObj.GetComponent<Image>();
            achieveMsgImg.sprite = achieveMsgs[idx].sprite;

            RectTransform sideBeamRect = sideBeamObj.GetComponent<RectTransform>();
            RectTransform achieveRect = achieveMsgObj.GetComponent<RectTransform>();

            //Debug.Log("111111111111111111111111");
            if (_runPath != RunPath.None)
            {
                Image runPathMsgImg = runPathMsgObj.GetComponent<Image>();
                runPathMsgImg.sprite = runPathMsgs.Where(r => r.runPath == _runPath).Select(r => r.sprite).FirstOrDefault();
                //Debug.Log($"{_runPath} .. runPathMsgImg.sprite : {runPathMsgImg.sprite.name}");
            }
            RectTransform runPathRect = runPathMsgObj.GetComponent<RectTransform>();

            //Debug.Log("333333333333333333");


            sideBeamRect.localScale = Vector3.zero;
            achieveRect.localScale = Vector3.zero;
            runPathRect.localScale = Vector3.zero;

            sideBeamRect.DOScale(1.5f, .2f).SetEase(Ease.OutSine).OnComplete(() =>
            {
                sideBeamRect.DOScale(0, .2f).SetEase(Ease.OutSine);
            });
            achieveRect.DOScale(2.5f, .1f).SetEase(Ease.InSine).OnComplete(() => {
                achieveRect.DOScale(1.5f, .2f).SetEase(Ease.InSine).OnComplete(() => {
                    achieveRect.DOScale(0, .2f).SetDelay(1f).OnComplete(() => {
                        if (_runPath == RunPath.None) return;
                        runPathRect.localScale = Vector3.one * 4f;
                        runPathRect.DOScale(1, .2f).SetEase(Ease.OutElastic).OnComplete(() => {
                            runPathRect.DOScale(0, .2f).SetDelay(1f);
                        });

                    });
                });
            });

            CoinBring(turnId, coin_len, coinAnimationCancellation.Token).Forget();

        }

    }
}
