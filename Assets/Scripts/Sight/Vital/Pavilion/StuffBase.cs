using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Assets.Scripts.Often;
using Assets.Scripts.Exert.Match;

namespace Assets.Scripts.Sight.Vital.Pavilion
{
    public enum StuffType
    {
        StuffCoin01,
        StuffItem01,
        StuffItem02,
        StuffItem03,
        StuffItem04,
    }

    public class StuffBase : MonoBehaviour
    {

        public StuffType typeId { get; private set; }
        public int positionId { get; private set; }
        public int lifeSec { get; private set; }
        public int touchId { get; private set; }
        public bool myTurn { get; private set; }


        protected PhysicsMng physicsManager;
        protected CoinCtrl coinCtrl;
        protected DrawStuffPos drawStuff;

        Material mat;
        string colorName;
        float resetScale = 1f;

        private void Awake()
        {
            physicsManager = FindObjectOfType<PhysicsMng>();
            coinCtrl = FindObjectOfType<CoinCtrl>();
            drawStuff = FindObjectOfType<DrawStuffPos>();

            colorName = "_MainColor";
            float intensity = 10.0f;     // 1.1>1.6   ... 3.8>10
            mat = transform.GetChild(0).GetComponent<MeshRenderer>().material;
            Color c1 = Color.white * intensity;
            mat.SetColor(colorName, c1);
            resetScale = transform.localScale.x;


        }


        public virtual void Setup(StuffType typeId, int positionId, int lifeSec)
        {
            this.typeId = typeId;
            this.positionId = positionId;
            this.lifeSec = lifeSec;
            this.myTurn = false;
             
            this.touchId = -10;     // 기본값 : -10, 볼터치 : -1, 특수효과미리 : -3, 특수효과 : -2, 특수효과당함 : 0 ~ max
            transform.localScale = Vector3.one * this.resetScale;
            //StopAllCoroutines();
            WaitOpenEnd().Forget();
        }

        public void SetEffectAhead()
        {
            this.touchId = -3;
            //EffectAheadColor().Forget();
        }

        async UniTaskVoid EffectAheadColor()
        {
            float intensity = 10.0f;     // 1.1>1.6   ... 3.8>10
            float deduct = 0.1f;
   
            while (intensity > 1.6)
            {
                if (mat == null)
                {
                    break;
                }
                intensity -= deduct;
                Color c1 = Color.red * intensity;
                mat.SetColor(colorName, c1);
                await UniTask.Yield();
            }
  
        }

        async UniTaskVoid WaitOpenEnd(bool isActive = true)
        {
            float intensity = 10.0f;     // 1.1>1.6   ... 3.8>10
            float deduct = 0.1f;
            if (!isActive)
            {
                deduct = 0.3f;
                transform.localScale = Vector3.one * this.resetScale * 1.3f;
            }

            while (intensity > 1.6)
            {
                if (mat == null)
                {
                    break;
                }
                intensity -= deduct;
                Color c1 = Color.white * intensity;
                mat.SetColor(colorName, c1);
                await UniTask.Yield();
            }
            //Debug.Log("ItensityChange end ");
            if (!isActive)
            {
                DeactiveDelay();    // 자신 비활성화.
            }
        }

        public void ItemTouch()
        {
            this.touchId = -2;
            this.myTurn = PoolLogic.controlInNetwork;
            DeactiveDelay();    // 자신 비활성화.

            //Debug.Log($"ItemTouch positionId : {positionId}, myturn : {PoolLogic.controlInNetwork}");
            drawStuff.CreateEff(positionId, typeId);
        }



        public virtual void BallTouch(int turnId, int coin_ch = 0, bool isSend = true)
        {
            if (this.touchId != -10) return;

            this.touchId = -1;
            this.myTurn = PoolLogic.controlInNetwork;

            if (isSend)
            {
                drawStuff.StuffVanishNotice(positionId, coin_ch);
            }
            DeactiveDelay();    // 자신 비활성화.
            //Debug.Log($"BallTouch positionId : {positionId}, myturn : {PoolLogic.controlInNetwork}, isSend : {isSend}, active : {this.gameObject.activeSelf}");
            Vector3 pos = transform.position + new Vector3(0f, 0.1f, 0f);
            coinCtrl.AddCoins(pos, 2, turnId);
            //Debug.Log("BallTouch positionId : " + positionId);

            ObjectPooler.instance.SpawnFromPool(ObjectPool.CoinLightUp.ToString(), transform.position);
            //StartCoroutine(WaitOpenEnd(false));
        }

        public async UniTask EffectTouch(int coin_ch, int touchId, int turnId)
        {
            this.touchId = touchId;
            this.myTurn = PoolLogic.controlInNetwork;
            //Debug.Log($"EffectTouch positionId : {positionId}, myturn : {PoolLogic.controlInNetwork}, isSend : {isSend}, active : {this.gameObject.activeSelf}");
            Vector3 pos = transform.position + new Vector3(0f, 0.1f, 0f);
            drawStuff.effectStuffEa++;
            await coinCtrl.CoinPickUpAnimate2(pos, 2, turnId);
            //Debug.Log("BallTouch positionId : " + positionId);
            DeactiveDelay();    // 자신 비활성화.

            ObjectPooler.instance.SpawnFromPool(ObjectPool.CoinLightUp.ToString(), transform.position);
            //StartCoroutine(WaitOpenEnd(false));

        }


        //public void EffectTouch()
        //{
        //    Debug.Log($"EffectTouch positionId : {positionId}, active : {this.gameObject.activeSelf}");
        //    DeactiveDelay();    // 자신 비활성화.

        //}

        public void DeactiveDelay() => gameObject.SetActive(false);
        private void OnDisable() => ObjectPooler.instance.ReturnToPool(gameObject);


    }
}
