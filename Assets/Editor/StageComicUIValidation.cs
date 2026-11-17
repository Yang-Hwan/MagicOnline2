using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Assets.Scripts.Often;
using Assets.Scripts.Exert.BackSys;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Physics=Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.PhysicsMng;
using Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital;

// Opt-in real-scene UI regression. Restores the stage progress it temporarily exercises.
[InitializeOnLoad]
public static class StageComicUIValidation
{
    const string Root="Logs/StageComicUI/";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static int step, saved, guideCaptures;
    static string progressKey;
    static double next, deadline;
    static StageComicUIValidation()
    {
        EditorApplication.update+=Tick;
        EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.ExitingPlayMode)Restore();};
    }
    static object Field(Physics p,string name)=>typeof(Physics).GetField(name,Flags).GetValue(p);
    static object Call(Physics p,string name,params object[] args)=>typeof(Physics).GetMethod(name,Flags).Invoke(p,args);
    static void Click(Physics p,string name)
    {
        var button=GameObject.FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(b=>b.name==name);
        if(!button.isActiveAndEnabled || !button.interactable)throw new Exception("Inactive button: "+name);
        button.onClick.Invoke();
    }
    static void Capture(string name)=>ScreenCapture.CaptureScreenshot(Root+name+".png");
    static void Restore()
    {
        if(progressKey==null)return;
        if(saved<0)PlayerPrefs.DeleteKey(progressKey);else PlayerPrefs.SetInt(progressKey,saved);
        PlayerPrefs.Save();progressKey=null;
    }
    static void Tick()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup<next)return;
        try
        {
            if(step==0)
            {
                if(!File.Exists(Root+"run.request") || !EditorApplication.isPlaying)return;
                File.Delete(Root+"run.request");
                ValidateStarTimeline();
                progressKey="StageMissions.v1."+GameApiClient.UserId+".1";
                saved=PlayerPrefs.GetInt(progressKey,-1);PlayerPrefs.SetInt(progressKey,0);
                StageMissions.Enter(1);step=1;deadline=EditorApplication.timeSinceStartup+120;next=EditorApplication.timeSinceStartup+3;return;
            }
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("UI validation timeout, step "+step);
            var p=UnityEngine.Object.FindAnyObjectByType<Physics>();
            if(step==10)
            {
                if(p)return;
                Restore();File.WriteAllText(Root+"result.json","{\"passed\":true,\"start\":true,\"fullDemo\":true,\"retry\":true,\"success\":true,\"failure\":true,\"listReturn\":true,\"firstCushion\":1}");step=0;return;
            }
            if(!p)return;
            string phase=Field(p,"stagePhase").ToString();
            if(step==1)
            {
                if(phase!="Intro")return;
                if(!((RectTransform)Field(p,"introCard")).gameObject.activeSelf)throw new Exception("No intro");
                var intro=(RectTransform)Field(p,"introCard");
                if(intro.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.name=="Directions" || t.name=="Number"))
                    throw new Exception("Intro still uses font digits or mission description");
                if(!intro.Find("Illustrated Stage Number").GetComponentsInChildren<Image>().All(i=>i.sprite && i.sprite.name.StartsWith("Stage digit")))
                    throw new Exception("Stage number does not use illustrated sprites");
                Capture("01-start");step=2;next=EditorApplication.timeSinceStartup+.5;
                guideCaptures=0;
            }
            else if(step==2)
            {
                Click(p,"StageStart");
                if(((RectTransform)Field(p,"guideBadge")).gameObject.activeSelf || (ParticleSystem)Field(p,"guideLine"))
                    throw new Exception("Guide started without its one-second pause");
                step=3;
            }
            else if(step==3)
            {
                if(phase=="Guide")
                {
                    var marker=(StageCushionNumber)Field(p,"guideMarker");
                    if(marker.gameObject.activeInHierarchy && (float)Field(p,"markerAge")>.25f && (guideCaptures&(1<<marker.Number))==0)
                    {
                        if(marker.GetComponent<Image>().sprite.name!="Cushion guide "+marker.Number)
                            throw new Exception("Guide number uses incorrect artwork");
                        Capture("guide-"+marker.Number);guideCaptures|=1<<marker.Number;
                    }
                }
                if(phase!="Aim")return;
                Capture("02-hud");
                // Reproduce the last visible contact ghost before entering the recorded demo.
                var checker=(MeshRenderer)typeof(ShotCtrl).GetField("ballCheckerRenderer",Flags).GetValue(p.shotController);
                checker.enabled=true;
                Click(p,"StageAction");
                if(checker.enabled)throw new Exception("Predicted cue-ball ghost survived replay entry");
                if(!p.IsPracticeReplay)throw new Exception("Demo button failed");step=4;
            }
            else if(step==4)
            {
                if(p.IsPracticeReplay)
                {
                    var checker=(MeshRenderer)typeof(ShotCtrl).GetField("ballCheckerRenderer",Flags).GetValue(p.shotController);
                    if(checker.enabled)throw new Exception("Predicted cue-ball ghost reappeared during replay");
                    return;
                }
                if(phase!="Aim")throw new Exception("Demo did not return to aim");
                Call(p,"BeginStageShot");Call(p,"RecordStageRail",0,"BoardT");
                var shot=(StageMissions.Shot)Field(p,"stageShot");
                if(shot.Cushions!=1)throw new Exception("First contact did not count as one");
                p.CallBallHitBoard(p.ballcs[0],true,Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.CushionDir.T);
                if(Assets.TutorialInfo.Scripts.TableSet06.Excert.Match.PoolLogic.gameState.ballsHitBoardCount!=0)
                    throw new Exception("Legacy callback still mutates stage count");
                Call(p,"FinishStageShot");step=5;next=EditorApplication.timeSinceStartup+.5;
            }
            else if(step==5)
            {
                if(phase!="Failed")throw new Exception("No failure result");Capture("03-failure");
                if(((RectTransform)Field(p,"resultCard")).GetComponentsInChildren<Image>().Any(i=>i.name.StartsWith("AwardStar")))
                    throw new Exception("Failure result displays stars");
                step=51;next=EditorApplication.timeSinceStartup+.5;
            }
            else if(step==51)
            {
                Click(p,"StageRetry");if(Field(p,"stagePhase").ToString()!="Aim")throw new Exception("Retry failed");
                // Exercise a genuine 1.5-star award rather than only a whole-star rating.
                StageMissions.BeginAttempt();StageMissions.BeginAttempt();
                Call(p,"BeginStageShot");int red=2;
                foreach(char c in StageMissions.Get(1).order)
                    if(c=='O')Call(p,"RecordStageBall",0,red++);else Call(p,"RecordStageRail",0,"Board"+c);
                Call(p,"FinishStageShot");step=6;next=EditorApplication.timeSinceStartup+.5;
            }
            else if(step==6)
            {
                if(phase!="Won")throw new Exception("No success result");Capture("04-first-star");
                step=61;next=EditorApplication.timeSinceStartup+.8;
            }
            else if(step==61)
            {
                var card=(RectTransform)Field(p,"resultCard");
                var stars=card.GetComponentsInChildren<Image>().Where(i=>i.name.StartsWith("AwardStar")).OrderBy(i=>i.name).ToArray();
                if(stars.Length!=2 || stars[0].fillAmount!=1 || stars[1].fillAmount!=.5f || stars.Any(i=>i.color.a<.99f || Mathf.Abs(i.rectTransform.localScale.x-1)>.01f))
                    throw new Exception("1.5-star staggered landing did not finish correctly");
                if(card.Find("StageNumber"))throw new Exception("Result still has upper stage badge");
                Capture("04-success");step=7;next=EditorApplication.timeSinceStartup+.5;
            }
            else if(step==7)
            {
                Click(p,"StageRetry");if(Field(p,"stagePhase").ToString()!="Aim")throw new Exception("Success retry failed");
                Click(p,"StageList");step=10;next=EditorApplication.timeSinceStartup+2;
            }
        }
        catch(Exception ex){Directory.CreateDirectory(Root);File.WriteAllText(Root+"error.txt",ex.ToString());Restore();step=0;}
    }
    static void ValidateStarTimeline()
    {
        void Check(int index,float time,bool visible,float? expectedScale=null,float? expectedAlpha=null)
        {
            bool actual=StageStarStamp.Sample(index,3,time,out float alpha,out float scale,out _);
            if(actual!=visible || expectedScale.HasValue && Mathf.Abs(scale-expectedScale.Value)>.001f || expectedAlpha.HasValue && Mathf.Abs(alpha-expectedAlpha.Value)>.001f)
                throw new Exception("Star timeline mismatch at "+index+":"+time);
        }
        Check(0,0,true,26f,.12f);Check(0,.3f,true,1,1);
        Check(1,.3f,false);Check(1,.79f,false);Check(1,.8f,true,26f,.12f);
        Check(1,1.1f,true,1,1);Check(2,5,false);
        var burstObject=new GameObject("Star impact regression",typeof(RectTransform),typeof(StageStarImpact));
        try
        {
            var impact=burstObject.GetComponent<StageStarImpact>();impact.SetAge(.06f);
            using(var mesh=new VertexHelper())
            {
                typeof(StageStarImpact).GetMethod("OnPopulateMesh",Flags,null,new[]{typeof(VertexHelper)},null).Invoke(impact,new object[]{mesh});
                if(mesh.currentVertCount<StageStarImpact.FragmentCount*4)throw new Exception("Missing radial impact fragments");
            }
            impact.SetAge(StageStarImpact.Duration);
            if(burstObject.activeSelf)throw new Exception("Impact fragments did not expire");
        }
        finally{UnityEngine.Object.DestroyImmediate(burstObject);}
        for(int i=0;i<10;i++)if(!StagePresentationArt.Digit(i))throw new Exception("Missing stage digit "+i);
        for(int i=1;i<=9;i++)if(!StagePresentationArt.CushionNumber(i))throw new Exception("Missing cushion number "+i);
        for(int i=0;i<=5;i++)if(!StagePresentationArt.Get((StagePresentationArt.Piece)i))throw new Exception("Missing popup artwork");
    }
}
