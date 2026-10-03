using Assets.Scripts.Exert.Match;
using Assets.Scripts.Often;
using Cysharp.Threading.Tasks;
using System.Collections;
using UnityEngine;


namespace Assets.Scripts.Sight.Vital.Pavilion
{
    public class EffCtrl : MonoBehaviour
    {


        public bool isAutoDeactive = true;
        public float liveSec = 3f;
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
                WaitDeactive().Forget();
        }

        async UniTaskVoid WaitDeactive()
        {
            int sec = (int)(liveSec * 1000f);
            await UniTask.Delay(sec);
            DeactiveDelay();
        }

        public void DeactiveDelay()
        {
            gameObject.SetActive(false);
        }


        private void OnDisable()
        {
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
