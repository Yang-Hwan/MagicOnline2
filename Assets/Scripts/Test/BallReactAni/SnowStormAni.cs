using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Test.BallReactAni
{
    public class SnowStormAni : MonoBehaviour
    {

        public Transform pieces;
        public List<Transform> masks_1;
        public List<Transform> masks_2;
        public ParticleSystem snow;

        public List<float> ms_1;
        public List<float> ms_2;
        int len;
        List<Vector3> vm;
        public void Awake()
        {
            len = transform.Find("Mask_1").childCount;
            masks_1 = new List<Transform>();
            masks_2 = new List<Transform>();
            ms_1 = new List<float>();
            ms_2 = new List<float>();
            vm = new List<Vector3>();
            for (int i = 0; i < len; i++)
            {
                Transform tr_1 = transform.Find("Mask_1").GetChild(i);
                Transform tr_2 = transform.Find("Mask_2").GetChild(i);
                masks_1.Add(tr_1);
                masks_2.Add(tr_2);
                ms_1.Add(tr_1.localScale.x);
                ms_2.Add(tr_2.localScale.x);
                vm.Add(tr_2.localScale);

                tr_1.localScale = Vector3.zero;
                tr_2.localScale = new Vector3(0, tr_2.localScale.y, tr_2.localScale.z);
            }
            snow = transform.Find("Snow").GetComponent<ParticleSystem>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                //test01(0.3f);
                change01(0, 2);
            }
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                transform.Find("Mask_1").gameObject.SetActive(true);
                transform.Find("Mask_2").gameObject.SetActive(true);
                change();
            }
            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                SetupSequence();
            }
            if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                StartSequence();
            }
        }

        [ContextMenu(nameof(AniPlay))]
        public void AniPlay()
        {
            AniStart(0).Forget();
        }
        [ContextMenu(nameof(AniPlay2))]
        public void AniPlay2()
        {
            AniStart(1).Forget();
        }


        async UniTaskVoid AniStart(int m = 0)
        {
            for (int i = 0; i < len; i++)
            {
                vm[i] = new Vector3(0, vm[i].y, vm[i].z);
                masks_1[i].localScale = Vector3.one * 0.01f;
                masks_2[i].localScale = vm[i];
            }

            transform.Find("Mask_1").gameObject.SetActive(true);
            transform.Find("Mask_2").gameObject.SetActive(true);
            float t1 = 0;

            if (m == 0)
            {
                while (t1 <= 10f)
                {
                    float t = t1 * 0.1f;
                    Debug.Log($"t1 : {t1} ..  x : {masks_1[0].localScale.x} .. ms_1 : {ms_1[0]} >> t : {t} ");
                    for (int i = 0; i < len; i++)
                    {
                        masks_1[i].localScale = Vector3.Lerp(masks_1[i].localScale, Vector3.one * ms_1[i], t);
                    }
                    await UniTask.Yield();
                    t1 += Time.deltaTime;
                }
            }
            if (m == 1)
            {
                t1 = 0;
                while (t1 <= 10f)
                {
                    for (int i = 0; i < len; i++)
                    {
                        float x = Mathf.Lerp(vm[i].x, ms_2[i], t1 * 0.1f);
                        vm[i] = new Vector3(x, vm[i].y, vm[i].z);
                        masks_2[i].localScale = vm[i];
                    }
                    await UniTask.Yield();
                    t1 += Time.deltaTime;
                }
            }
            Debug.Log($"AniStart END ===");
        }

        async void change()
        {
            masks_1.ForEach(r => { r.localScale = Vector3.zero; });
            masks_2.ForEach(r => { r.localScale = Vector3.zero; });

            // 초기화
            for (int i = 0; i < len; i++)
            {
                masks_1[i].localScale = Vector3.one * 0.0f;
                vm[i] = new Vector3(0, vm[i].y, vm[i].z);
                masks_2[i].localScale = vm[i];
            }

            for (int i = 0; i < len; i++)
            {
                change01(i, 0.5f);
                await UniTask.WaitForSeconds(0.02f);
            }
        }

        private async void change01(int i, float duration)
        {
            Vector3 start = masks_1[i].localScale;
            Vector3 end = Vector3.one * ms_1[i];
            float startTime = Time.time;
            while (Time.time < startTime + duration)
            {
                float fraction = (Time.time - startTime) / duration;
                masks_1[i].localScale = Vector3.Lerp(start, end, fraction);
                await Task.Yield();                             // 다음 프레임까지 대기
            }
            masks_1[i].localScale = end;
            change02(i, 0.8f);
        }

        private async void change02(int i, float duration)
        {
            Vector3 start = masks_2[i].localScale;
            Vector3 end = new Vector3(ms_2[i], vm[i].y, vm[i].z);
            float startTime = Time.time;

            while (Time.time < startTime + duration)
            {
                float fraction = (Time.time - startTime) / duration;
                masks_2[i].localScale = Vector3.Lerp(start, end, fraction);
                await Task.Yield();                             // 다음 프레임까지 대기
            }
            masks_2[i].localScale = end;
        }


        Sequence mySequence;
        public void SetupSequence()
        {
            transform.Find("Mask_1").gameObject.SetActive(true);
            transform.Find("Mask_2").gameObject.SetActive(true);
            masks_1.ForEach(r => { r.localScale = Vector3.zero; });
            masks_2.ForEach(r => { r.localScale = Vector3.zero; });
            snow.Stop();
            mySequence = DOTween.Sequence();
            mySequence.JoinCallback(()=>snow.Play());
            for (int i = 0; i < len; i++)
            {
                mySequence
                    .Join(masks_1[i].DOScale(Vector3.one * ms_1[i], 0.5f))
                    .Join(masks_2[i].DOScale(new Vector3(ms_2[i], vm[i].y, vm[i].z), 1.0f))
                    .SetDelay(0.02f);
            }
            mySequence.AppendInterval(2.5f);
            mySequence.AppendCallback(()=> { masks_1.ForEach(r => { r.localScale = Vector3.zero; }); masks_2.ForEach(r => { r.localScale = Vector3.zero; }); });
            mySequence.SetAutoKill(false).Pause();

        }

        public void StartSequence()
        {
            mySequence.Kill();
            SetupSequence(); // 시퀀스 재설정
            if (mySequence != null && !mySequence.IsPlaying())
            {
                mySequence.Restart();
            }
        }


        void test01(float t = 0.3f)
        {
            masks_1[0].localScale = Vector3.one * 0.01f;
            masks_1[0].localScale = Vector3.Lerp(masks_1[0].localScale, Vector3.one * ms_1[0], t);
            Debug.Log($"test01 t : {t}");
        }
         
    }
}
