using System;
using System.Collections;
using System.IO;
using System.Text;
using Assets.TutorialInfo.Scripts.TableSet06.Excert.Match;
using Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital;
using UnityEngine;

// Uses the production, time-limited decision and the real score judge.
public static class ComputerExpertValidation
{
    public static bool Finished,Passed;
    public static IEnumerator Run(PhysicsMng manager)
    {
        Finished=Passed=false;
        string path=$"Logs/computer-expert-{manager.ballcs.Length}.txt";
        var report=new StringBuilder();
        var originals=Array.ConvertAll(manager.ballcs,b=>b.body.position);
        float scale=Time.timeScale,skill=manager.ComputerSkill;bool enabled=manager.ComputerOpponentEnabled;
        manager.ComputerSkill=1; // This benchmark checks the planner's maximum strength.
        var coach=PoolCoach.Instance;var shot=manager.GetComponent<ShotCtrl>();
        int results=0,points=0,wrongPredictions=0;
        bool hit=false,foul=false;
        Action<bool,bool,int> judged=(success,failed,rails)=>
        {
            if(PoolPlayer.turnId!=1)return;
            hit=success;foul=failed;results++;
            manager.SetComputerOpponentEnabled(false);
        };
        coach.OnPracticeShotResult+=judged;
        manager.SetComputerOpponentEnabled(false);Time.timeScale=0;
        try
        {
            Rect area=ComputerShotPlanner.PlayingArea(manager);
            var layouts=new[]{
                new[]{new Vector2(.12f,.88f),new Vector2(.22f,.32f),new Vector2(.50f,.46f),new Vector2(.78f,.80f)},
                new[]{new Vector2(.88f,.88f),new Vector2(.26f,.70f),new Vector2(.52f,.48f),new Vector2(.74f,.20f)},
                new[]{new Vector2(.40f,.55f),new Vector2(.18f,.25f),new Vector2(.58f,.62f),new Vector2(.76f,.28f)}};
            for(int fixture=0;fixture<layouts.Length;fixture++)
            {
                Time.timeScale=0;
                for(int i=0;i<manager.ballcs.Length;i++)
                {
                    var uv=layouts[fixture][i];var body=manager.ballcs[i].body;
                    body.position=new Vector3(Mathf.Lerp(area.xMin,area.xMax,uv.x),body.position.y,Mathf.Lerp(area.yMin,area.yMax,uv.y));
                    body.linearVelocity=body.angularVelocity=Vector3.zero;body.Sleep();
                }
                Physics.SyncTransforms();
                if(fixture==0)
                {
                    var candidates=ComputerShotPlanner.Candidates(manager.ballcs,1,area,coach.Configuration.CushionCount);
                    for(int sample=0;sample<2;sample++)
                    {
                        var impulse=shot.BuildOnlineImpulse(candidates[sample*5]);
                        using(var full=new ShotPrediction(manager))using(var fast=new ShotPrediction(manager))
                        {
                            full.Begin(1,impulse,12,50,caromCushions:coach.Configuration.CushionCount,accelerateCarom:false);
                            while(!full.Advance(20,2048))yield return null;
                            fast.Begin(1,impulse,12,50,caromCushions:coach.Configuration.CushionCount);
                            while(!fast.Advance(20,2048))yield return null;
                            float error=0;int count=Math.Min(full.timedCuePath.Count,fast.timedCuePath.Count);
                            for(int i=0;i<count;i++)error=Mathf.Max(error,Vector3.Distance(full.timedCuePath[i],fast.timedCuePath[i]));
                            report.AppendLine($"parity {sample}: error={error:F6}m, full={full.ContactSolverSteps}, fast={fast.ContactSolverSteps}, free={fast.FreeFlightSteps}, mask={full.HitBallMask}/{fast.HitBallMask}, rails={full.RailsAtSecondBall}/{fast.RailsAtSecondBall}");
                            File.WriteAllText(path,report.ToString());
                            if(error>.001f || full.HitBallMask!=fast.HitBallMask || full.RailsAtSecondBall!=fast.RailsAtSecondBall || Math.Abs(full.timedCuePath.Count-fast.timedCuePath.Count)>1)
                                throw new Exception("Accelerated prediction differs from the full contact solver; see "+path);
                            if(fast.FreeFlightSteps==0)throw new Exception("Free-flight acceleration was not exercised");
                        }
                    }
                }
                PoolLogic.Instance.RessetTurnState();PoolPlayer.SetTurn(1);coach.TurnChanged();
                int before=results;float started=Time.realtimeSinceStartup;
                manager.SetComputerOpponentEnabled(true);Time.timeScale=1;
                while(results==before)
                {
                    if(Time.realtimeSinceStartup-started>95)throw new Exception("Real computer shot timed out");
                    yield return null;
                }
                if(hit&&!foul)points++;
                if(manager.ComputerBestScore>=10000 && (!hit||foul))wrongPredictions++;
                if(manager.ComputerCandidatesCompleted<1)throw new Exception("A partially simulated shot was selected");
                report.AppendLine($"actual {fixture}: point={hit&&!foul}, foul={foul}, prediction={manager.ComputerBestScore:F1}, completed={manager.ComputerCandidatesCompleted}, search={manager.ComputerSearchSeconds:F2}s, free={manager.ComputerFreeFlightSteps}");
                File.WriteAllText(path,report.ToString());yield return null;
            }
            Passed=points>=2 && wrongPredictions==0;
            report.AppendLine($"{(Passed?"PASS":"FAIL")}: real points={points}/3; false point predictions={wrongPredictions}; full-solver parity and completed-only selection checked.");
        }
        finally
        {
            coach.OnPracticeShotResult-=judged;
            manager.SetComputerOpponentEnabled(false);
            for(int i=0;i<manager.ballcs.Length;i++)
            {
                var body=manager.ballcs[i].body;body.position=originals[i];body.linearVelocity=body.angularVelocity=Vector3.zero;body.Sleep();
            }
            Physics.SyncTransforms();coach.MatchReset();coach.RestorePracticeTurn(0,coach.maxPlayTime);
            manager.SetComputerOpponentEnabled(enabled);Time.timeScale=scale;
            manager.ComputerSkill=skill;
            File.WriteAllText(path,report.ToString());Finished=true;
        }
    }
}
