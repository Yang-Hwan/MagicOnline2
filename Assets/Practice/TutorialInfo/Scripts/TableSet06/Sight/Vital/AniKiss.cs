using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class AniKiss : MonoBehaviour
{
    Transform msg;
    void Start()
    {
        msg = transform.Find("Msg");
        msg.gameObject.SetActive(false);
    }


#if UNITY_EDITOR
    private void Update()
    {

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            AniPlay();
        }

    }
#endif

    void AniPlay()
    {
        AniStart().Forget();
    }


    /// <summary>
    /// Vector3.Lerp
    /// </summary>
    /// <returns></returns>
    //private void Update()
    //{
    //    currentTime += Time.deltaTime;
    //    if (currentTime >= lerpTime)
    //    {
    //        currentTime = lerpTime;
    //    }

    //    float t = currentTime / lerpTime;
    //    //t = t*t*t*(t*(6f*t-15f) + 10f);
    //    t = Mathf.Sin(t * Mathf.PI * 0.5f);
    //    Vector3.Lerp(transform.position, transform.position + Vector3.forward, t);
    //}



    async UniTaskVoid AniStart()
    {
        //gameObject.SetActive(true);
        Debug.Log($"AniStart >>> start");
        Sequence mySequence = DOTween.Sequence();
        mySequence

            .Append(msg.DOScale(new Vector3(0.3f, 0.3f, 0.3f), 0.1f).OnComplete(() => msg.gameObject.SetActive(true)))
            .Append(msg.DOScale(new Vector3(0.25f, 0.33f, 0.3f), 0.5f).SetEase(Ease.OutQuad))
            .Append(msg.DOScale(new Vector3(0.3f, 0.3f, 0.3f), 0.5f).SetEase(Ease.OutQuad).SetDelay(0.5f).OnComplete(() => msg.gameObject.SetActive(false)));


        Debug.Log($"AniStart >>> end");
    }

}



