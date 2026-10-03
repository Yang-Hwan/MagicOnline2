using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System;
using Assets.TutorialInfo.Scripts.TableSet06.Often;
using System.Linq;
using Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital;
using Cysharp.Threading.Tasks;
using Assets.TutorialInfo.Scripts.TableSet06.Excert.Match;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Surface
{

    public class AniCtrl : MonoBehaviour
    {
        public GameObject sideBeamObj;
        public GameObject achieveMsgObj;
        public GameObject runPathMsgObj;
        public Transform achieveBoxPos;

        int cnt_coin = 20; 
        public GameObject[] coins;

        PhysicsMng physicsManager;

        private int[] _c = new int[2];


        public int ccc
        {
            get
            {
                return _c[0];
            }

        }

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
            physicsManager = FindObjectOfType<PhysicsMng>();
            coins = new GameObject[cnt_coin];

        }

        void Ani02()
        {

            CoinBring().Forget();
            Debug.Log($"Ani02");
        }

        async UniTask CoinBring()
        {
            int ownerPlayerId = PoolPlayer.currentPlayer.playerId;
            var session = physicsManager.Items;
            // Achievement bonus is gameplay, independent of delayed coin presentation.
            session.AwardBonus(ownerPlayerId, cnt_coin * 10);

            for (int i = 0; i < cnt_coin; i++)
            {
                coins[i]?.SetActive(false);
            }

            for (int i = 0; i < cnt_coin; i++)
            {
                int r = UnityEngine.Random.Range(0, 6);
                float pos_d = r == 0 ? 3 : 1;
                float x = UnityEngine.Random.Range(-0.1f * pos_d, 0.1f * pos_d);
                float y = 0.1f + (0.01f * i);
                float z = UnityEngine.Random.Range(-0.07f * pos_d, 0.07f * pos_d);
                Vector3 pos = new Vector3(x, y, z);
                coins[i] = ObjectPooler.instance.SpawnFromPool("GoldCoin", pos);
                coins[i].transform.localRotation = Quaternion.Euler(90f, 0, 0);
                coins[i].transform.localScale = Vector3.one * 0.4f;
                if (i % 5 == 0)
                {
                    await UniTask.Delay(100, cancellationToken: this.GetCancellationTokenOnDestroy());
                    if (session != physicsManager.Items || !isActiveAndEnabled) return;
                }
            }

            await UniTask.Delay(1000, cancellationToken: this.GetCancellationTokenOnDestroy());
                    if (session != physicsManager.Items || !isActiveAndEnabled) return;
            for (int i = 0; i < cnt_coin; i++)
            {
                Vector3 pos = coins[i].transform.position;
                Vector3 pos_light = pos;
                pos_light.y = 0.3f;
                physicsManager.PlayCoinFeedback(pos, 1, ownerPlayerId);
                ObjectPooler.instance.SpawnFromPool("CoinLightUp", pos_light);
                coins[i]?.SetActive(false);
                await UniTask.Delay(70, cancellationToken: this.GetCancellationTokenOnDestroy());
                    if (session != physicsManager.Items || !isActiveAndEnabled) return;
            }
        }

        // 0.03 .. 0.01
        public void AniAchieve(int idx, RunPath _runPath = RunPath.None)
        {
            if(idx == -1)
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
            CoinBring().Forget();

        }

    }
}
