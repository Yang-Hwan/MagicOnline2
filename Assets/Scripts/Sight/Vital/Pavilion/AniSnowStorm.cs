using DG.Tweening;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Assets.Scripts.Sight.Vital.Pavilion
{
    public class AniSnowStorm : MovStoryAni
    {

        public Transform pieces;
        public List<Transform> masks_1;
        public List<Transform> masks_2;
        public ParticleSystem snow;

        public List<float> ms_1;
        public List<float> ms_2;
        int len;
        List<Vector3> vm;

        public override void Awake()
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
            transform.Find("Pieces").gameObject.SetActive(true);
            snow = transform.Find("Snow").GetComponent<ParticleSystem>();
            base.Awake();
        }

        [ContextMenu(nameof(AniPlay))]
        public override void AniPlay(Vector3 pos, CancellationToken _cancellationToken)
        {
        }

        public override void SetupSequence()
        {
            transform.Find("Mask_1").gameObject.SetActive(true);
            transform.Find("Mask_2").gameObject.SetActive(true);
            masks_1.ForEach(r => { r.localScale = Vector3.zero; });
            masks_2.ForEach(r => { r.localScale = Vector3.zero; });
            snow.Stop();
            mySequence = DOTween.Sequence();
            mySequence.JoinCallback(() => snow.Play());
            for (int i = 0; i < len; i++)
            {
                mySequence
                    .Join(masks_1[i].DOScale(Vector3.one * ms_1[i], 0.5f))
                    .Join(masks_2[i].DOScale(new Vector3(ms_2[i], vm[i].y, vm[i].z), 1.0f))
                    .SetDelay(0.02f);
            }
            mySequence.AppendInterval(2.5f);
            mySequence.AppendCallback(() => { masks_1.ForEach(r => { r.localScale = Vector3.zero; }); masks_2.ForEach(r => { r.localScale = Vector3.zero; }); });
            mySequence.SetAutoKill(false).Pause();
        }
    }
}
