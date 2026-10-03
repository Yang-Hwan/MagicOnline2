using Cysharp.Threading.Tasks;
using System;
using UnityEngine;

namespace Assets.Scripts.Test.BallReactAni
{
    public class DarkCloudAni : BallMovStoryAni
    {


        public Transform targetPos;
        public GameObject cloud;
        public GameObject rain;

        private void Start()
        {
            cloud = transform.Find("cloud").gameObject;
            rain = transform.Find("rain").gameObject;
            rain.SetActive(false);
        }

        //private void Update()
        //{
        //    if (Input.GetKeyDown(KeyCode.Alpha1))
        //    {
        //        AniPlay(new Vector3(-0.8f, 0, 0.2f));
        //    }
        //    else if (Input.GetKeyDown(KeyCode.Alpha2))
        //    {
        //        AniPlay(new Vector3(0.0f, 0, 0));
        //    }
        //    else if (Input.GetKeyDown(KeyCode.Alpha3))
        //    {
        //        AniPlay(new Vector3(0.8f, 0, -0.6f));
        //    }
        //}

        public override void AniPlay(Vector3 pos)
        {
            AniStart(pos).Forget();
        }

        async UniTaskVoid AniStart(Vector3 pos)
        {
            //Debug.Log($"=======");
            cloud.SetActive(true);
            rain.SetActive(false);
            await UniTask.Yield();
            float dist = Vector3.Distance(transform.position, pos);
            //Debug.Log($"0 dist : {Vector3.Distance(transform.position, pos)}");
            while (dist > 0.002f)
            {
                dist = Vector3.Distance(transform.position, pos);
                //Debug.Log($"dist : {Vector3.Distance(transform.position, pos)}");
                transform.position = Vector3.Lerp(transform.position, pos, 0.05f);
                await UniTask.Yield();
            }
            await UniTask.Yield();
            //Debug.Log($"aaaaaaaaaaaa");
            transform.position = pos;
            cloud.SetActive(true);

            rain.SetActive(true);
            await UniTask.Delay(TimeSpan.FromSeconds(2.5f));
            cloud.SetActive(false);
            rain.SetActive(false);
        }
    }
}
