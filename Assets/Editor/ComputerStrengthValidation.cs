using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Assets.Scripts.Often;
using Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital;
using UnityEngine;

public static class ComputerStrengthValidation
{
    public static bool Finished,Passed;
    public static IEnumerator Run(PhysicsMng manager)
    {
        Finished=Passed=false;
        var originals=Array.ConvertAll(manager.ballcs,b=>b.body.position);
        float oldTimeScale=Time.timeScale;Time.timeScale=0;
        var text=new StringBuilder();
        string report="Logs/computer-strength-"+manager.ballcs.Length+".txt";
        float oldTotal=0,newTotal=0;int oldPoints=0,newPoints=0;
        var shot=manager.GetComponent<ShotCtrl>();
        int cushions=manager.ballcs.Length==3?3:0;
        try
        {
            Rect area=ComputerShotPlanner.PlayingArea(manager);
            var layouts=new[]{
                new[]{new Vector2(.12f,.88f),new Vector2(.22f,.32f),new Vector2(.50f,.46f),new Vector2(.78f,.80f)},
                new[]{new Vector2(.88f,.88f),new Vector2(.26f,.70f),new Vector2(.52f,.48f),new Vector2(.74f,.20f)}};
            for(int fixture=0;fixture<layouts.Length;fixture++)
            {
                for(int i=0;i<manager.ballcs.Length;i++)
                {
                    var uv=layouts[fixture][i];var body=manager.ballcs[i].body;
                    body.position=new Vector3(Mathf.Lerp(area.xMin,area.xMax,uv.x),body.position.y,Mathf.Lerp(area.yMin,area.yMax,uv.y));
                    body.linearVelocity=body.angularVelocity=Vector3.zero;body.Sleep();
                }
                Physics.SyncTransforms();
                var timer=System.Diagnostics.Stopwatch.StartNew();
                var candidates=ComputerShotPlanner.Candidates(manager.ballcs,1,area,cushions);
                timer.Stop();
                if(candidates.Count!=ComputerShotPlanner.ShortlistCount)throw new Exception("Expected a diverse physical shortlist");
                text.AppendLine($"fixture {fixture}: shortlist={candidates.Count}, geometry={timer.Elapsed.TotalMilliseconds:0.0}ms");
                float oldBest=float.NegativeInfinity,newBest=float.NegativeInfinity;
                MatchShotCommand chosen=null;
                using(var prediction=new ShotPrediction(manager))
                {
                    foreach(var baseline in Baseline(manager.ballcs))
                    {
                        prediction.Begin(1,shot.BuildOnlineImpulse(baseline),14,50,caromCushions:cushions);
                        while(!prediction.Advance(20,2048))yield return null;
                        oldBest=Mathf.Max(oldBest,ComputerShotPlanner.Score(prediction,manager.ballcs,1,baseline.power,cushions));
                    }
                    // Equal physical search count, with the previous angle/power order as baseline.
                    for(int i=0;i<8;i++)
                    {
                        var candidate=candidates[i];prediction.Begin(1,shot.BuildOnlineImpulse(candidate),14,50,caromCushions:cushions);
                        while(!prediction.Advance(20,2048))yield return null;
                        float score=ComputerShotPlanner.Score(prediction,manager.ballcs,1,candidate.power,cushions);
                        if(score>newBest){newBest=score;chosen=candidate;}
                    }
                }
                oldTotal+=oldBest;newTotal+=newBest;if(oldBest>=10000)oldPoints++;if(newBest>=10000)newPoints++;
                text.AppendLine($"fixture {fixture}: old={oldBest:0.0}, new={newBest:0.0}, yaw={chosen.yaw:0.000}, power={chosen.power:0.00}, spin={chosen.contact}");
                File.WriteAllText(report,text.ToString());yield return null;
            }
            // The selected off-centre command must be the actual firing impulse too.
            var check=new MatchShotCommand{seat=1,yaw=73,power=.6f,contact=new Vector2(.4f,-.3f),followThrough=.5f};
            var expected=shot.BuildOnlineImpulse(check);shot.PrepareComputerStroke(check);
            var actual=shot.BuildPreparedComputerImpulse();
            if((actual.impulse-expected.impulse).magnitude>1e-6f || (actual.point-expected.point).magnitude>1e-6f || actual.followThrough!=expected.followThrough)
                throw new Exception("Computer prediction and actual shot impulse differ");
            Passed=newTotal>oldTotal && newPoints>=oldPoints;
            text.AppendLine($"{(Passed?"PASS":"FAIL")}: old points={oldPoints}/2, new points={newPoints}/2; score sum {oldTotal:0.0} -> {newTotal:0.0}; firing impulse matches prediction.");
        }
        finally
        {
            for(int i=0;i<manager.ballcs.Length;i++){var body=manager.ballcs[i].body;body.position=originals[i];body.linearVelocity=body.angularVelocity=Vector3.zero;body.Sleep();}
            Physics.SyncTransforms();shot.CueReadyShot();
            Time.timeScale=oldTimeScale;
            File.WriteAllText(report,text.ToString());Finished=true;
        }
    }
    static IEnumerable<MatchShotCommand> Baseline(BallC[] balls)
    {
        int count=0;var cue=balls[1];
        foreach(var target in balls)
        {
            if(target.id==1 || balls.Length==4 && target.id<2)continue;
            Vector3 delta=target.body.position-cue.body.position;
            float radius=(target.GetComponent<SphereCollider>().radius*target.transform.lossyScale.x)+(cue.GetComponent<SphereCollider>().radius*cue.transform.lossyScale.x);
            float yaw=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg,spread=Mathf.Asin(Mathf.Clamp(radius/delta.magnitude,0,.99f))*Mathf.Rad2Deg;
            foreach(float cut in new[]{.65f,-.65f,.35f,-.35f,.9f,-.9f,0f})
            {
                yield return new MatchShotCommand{seat=1,yaw=Mathf.Repeat(yaw+spread*cut,360),power=.55f,contact=Vector2.zero,followThrough=ShotCtrl.DefaultFollowThrough};
                if(++count==8)yield break;
            }
        }
    }
}
