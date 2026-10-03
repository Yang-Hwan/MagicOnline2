using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

namespace Assets.Scripts.Sight.Surface.Arise
{
    public class BarProgress : MonoBehaviour
    {

        [SerializeField]
        private Slider sliderProgress;
        [SerializeField]
        private TextMeshProUGUI txtProgress;
        [SerializeField]
        private float progressTime;

        private void Awake()
        {
            sliderProgress = transform.Find("Slider").GetComponent<Slider>();
            txtProgress = transform.Find("TxtLoading").GetComponent<TextMeshProUGUI>();
            progressTime = 1f;
        }

        public void Play(UnityAction action = null)
        {
            StartCoroutine(OnProgress(action));
        }

        public IEnumerator OnProgress(UnityAction action)
        {
            float current = 0;
            float percent = 0;

            while(percent < 1)
            {
                current += Time.deltaTime;
                percent = current / progressTime;
                txtProgress.text = $"Loading .. {sliderProgress.value * 100:F0}%";
                sliderProgress.value = Mathf.Lerp(0, 1, percent);
                yield return null;
            }

            action.Invoke();
        }

    }
}
