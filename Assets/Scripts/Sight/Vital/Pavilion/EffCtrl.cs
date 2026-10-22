using Assets.Scripts.Exert.Match;
using Assets.Scripts.Often;
using Cysharp.Threading.Tasks;
using System.Collections;
using UnityEngine;
using System.Threading;


namespace Assets.Scripts.Sight.Vital.Pavilion
{
    public class EffCtrl : MonoBehaviour
    {


        public bool isAutoDeactive = true;
        public float liveSec = 3f;
        CancellationTokenSource deactivate;
        private void Awake()
        {

            //effectItemLayer = 1 << LayerMask.NameToLayer("EffectItem");

            //            ettManager = FindObjectOfType<EffManager>();

            //Destroy(gameObject, liveSec);

        }

        [ContextMenu("Example2")]
        private void Btn01()
        {
            Debug.Log("Btn01");
            transform.GetComponent<Rigidbody>().AddForce(Vector3.forward * 0.5f, ForceMode.Force);
        }

        private void OnEnable()
        {
            if (isAutoDeactive)
            {
                deactivate = new CancellationTokenSource();
                WaitDeactive(deactivate.Token).Forget();
            }
        }

        async UniTaskVoid WaitDeactive(CancellationToken token)
        {
            int sec = (int)(liveSec * 1000f);
            if (await UniTask.Delay(sec, cancellationToken: token).SuppressCancellationThrow()) return;
            if (this && !token.IsCancellationRequested) DeactiveDelay();
        }

        public void DeactiveDelay()
        {
            gameObject.SetActive(false);
        }


        private void OnDisable()
        {
            deactivate?.Cancel(); deactivate?.Dispose(); deactivate = null;
            if (ObjectPooler.instance && ObjectPooler.instance.poolDictionary != null)
                ObjectPooler.instance.ReturnToPool(gameObject);
        }

        public void OnTriggerEnter(Collider other)
        {
            int layer = 1 << other.gameObject.layer;
            //if (isCueball)
            //{
            if (layer == LayerLib.NameToInt(LayerKind.Stuff))
            {
                int turnId = PoolPlayer.turnId;
                //Debug.Log("보석 터치 posid : " + other.GetComponent<StuffBase>().positionId);
                other.GetComponent<StuffBase>().BallTouch(turnId, 0);
                //Destroy(other.gameObject);
            }
            //else if (layer == itemLayer)
            //{
            //    Debug.Log("OnTriggerEnter 아이템 터치");
            //    other.GetComponent<StuffBase>().ItemTouch();

            //}
            //}
        }

    }

}
