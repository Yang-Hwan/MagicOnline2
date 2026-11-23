using System;
using System.IO;
using System.Reflection;
using System.Collections;
using UnityEngine;
using TMPro;
using Assets.Scripts.Sight.Surface.Pavilion;

public static class MatchResultSequenceValidation
{
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static int checks;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
    public static void Run()
    {
        var root=new GameObject("Result sequence fixture",typeof(RectTransform),typeof(Canvas));
        var popup=new GameObject("Result",typeof(RectTransform));popup.SetActive(false);popup.transform.SetParent(root.transform,false);
        foreach(var name in new[]{"Result","Brief"}){var child=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));child.transform.SetParent(popup.transform,false);}
        var view=popup.AddComponent<PopMatchFinish>();popup.SetActive(true);
        try
        {
            MatchResultPresentation.Reset();
            var present=typeof(PopMatchFinish).GetMethod("PresentResult",Private);
            int displayed=0;
            void Show(bool immediate=false)=>present.Invoke(view,new object[]{(Action)(()=>displayed++),immediate});
            Show();Check(displayed==1&&!view.ResultPending,"No-score result must appear immediately");
            Check(MatchResultPresentation.EffectsBlocked,"Result did not block late effects");
            MatchResultPresentation.Reset();
            var technique=ThreeCushionTechniqueMotion.Install(root.GetComponent<RectTransform>(),view);
            typeof(ThreeCushionTechniqueMotion).GetField("elapsed",Private).SetValue(technique,0f);
            Check(ThreeCushionTechniqueMotion.ResultHold>=1,"Last technique reading time missing");
            Show();Check(displayed==1&&view.ResultPending,"Result overlapped last technique");
            Check(!technique.Play(Assets.Scripts.Often.ThreeCushionTechnique.Back),"Late technique not blocked");
            var routine=(IEnumerator)typeof(PopMatchFinish).GetMethod("FinishLastScore",Private).Invoke(view,new object[]{1f,0f});
            Check(routine.MoveNext()&&routine.Current is WaitForSecondsRealtime&&displayed==1,"Reading delay missing");
            Check(routine.MoveNext()&&routine.Current is WaitForSecondsRealtime&&displayed==1,"Fade delay missing");
            Check(!routine.MoveNext()&&displayed==2&&!view.ResultPending,"Result did not follow fade");
            Check(technique.GetComponent<CanvasGroup>().alpha==0,"Technique visible behind result");
            view.StopAllCoroutines();
            MatchResultPresentation.Reset();typeof(ThreeCushionTechniqueMotion).GetField("elapsed",Private).SetValue(technique,0f);
            Show();Show(true);Check(displayed==3&&!view.ResultPending,"Timeout must bypass pending score animation");
            MatchResultPresentation.Reset();typeof(ThreeCushionTechniqueMotion).GetField("elapsed",Private).SetValue(technique,0f);
            Show();view.MatchResultClose();Check(!view.ResultPending&&displayed==3,"Closed result retained deferred presentation");
            Check(MatchResultPresentation.EffectsBlocked,"Closing result reopened late effects");
            MatchResultPresentation.Reset();Check(!MatchResultPresentation.EffectsBlocked,"New match did not unlock effects");
            File.WriteAllText("Logs/match-result-sequence-result.txt","PASS "+checks+" checks");
        }
        catch(Exception e){File.WriteAllText("Logs/match-result-sequence-result.txt",e.ToString());Debug.LogException(e);}
        finally{UnityEngine.Object.DestroyImmediate(root);MatchResultPresentation.Reset();}
    }
}
