using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Threading;
using UnityEngine;

namespace Assets.Scripts.Sight.Vital.Pavilion
{
    public class CosmosCtrl : MonoBehaviour
    {

        public float liveSec = 60f;
        ParticleSystem ps;
        public Ball[] balls;
        public PhysicsMng physicsManager;
        CancellationTokenSource cancelTime;

        private void Awake()
        {
            ps = GetComponent<ParticleSystem>();



        }

        private void OnEnable()
        {

            if (physicsManager == null)
            {
                physicsManager = FindObjectOfType<PhysicsMng>();
                balls = physicsManager.balls;
            }

            ps = GetComponent<ParticleSystem>();
            var trigger = ps.trigger;
            trigger.enabled = true;
            //Debug.Log($"{name} .. ball len : {physicsManager.balls.Length}");
            for (int i = 0; i < physicsManager.balls.Length; i++)
            {
                trigger.SetCollider(i, physicsManager.balls[i].transform.GetChild(0).GetComponent<Collider>());
            }
            //StartCoroutine(WaitDeactive());

            cancelTime = new CancellationTokenSource();
            WaitDeactiveAsync(cancelTime.Token).Forget();
        }

        async UniTaskVoid WaitDeactiveAsync(CancellationToken _cancellationToken)
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                Debug.Log($"{this.name} .._cancellationToken.IsCancellationRequested : {_cancellationToken.IsCancellationRequested}");
                return;
            }
            await UniTask.Delay(TimeSpan.FromSeconds(liveSec), cancellationToken: _cancellationToken);
            DeactiveDelay();
        }

        IEnumerator WaitDeactive()
        {
            yield return new WaitForSeconds(liveSec);
            DeactiveDelay();
        }

        public void DeactiveDelay()
        {
            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            ObjectPooler.instance.ReturnToPool(gameObject);
            if (cancelTime != null)
            {
                cancelTime.Cancel();
            }
        }

 

        //private void OnDestroy()
        //{
        //    Debug.Log($"PnlMatch OnDestroy...");
        //    if (cancelTime != null)
        //    {
        //        cancelTime.Cancel();
        //        cancelTime.Dispose();
        //    }
        //}


    }

}
