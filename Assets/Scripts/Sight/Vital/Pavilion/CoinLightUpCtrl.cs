using System.Collections;
using UnityEngine;

namespace Assets.Scripts.Sight.Vital.Pavilion
{
    public class CoinLightUpCtrl : MonoBehaviour
    {

        public float liveSec = 1f;

        private void Awake()
        {

        }

        private void OnEnable()
        {

            StartCoroutine(WaitDeactive());
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
        }


    }

}
