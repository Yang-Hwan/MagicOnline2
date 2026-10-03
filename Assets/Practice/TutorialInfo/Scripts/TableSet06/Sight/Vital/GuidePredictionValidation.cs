#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital;

[DefaultExecutionOrder(10000)]
public sealed class GuidePredictionValidation : MonoBehaviour
{
    [Serializable] public class Report { public bool passed; public string error=""; public int frames, targetIndex; public float maxError, predictionMilliseconds, repeatError; public Vector3 sourceTensor, previewTensor, previewSpin, actualSpin; public List<Vector3> predicted, repeated, actual=new List<Vector3>(); }
    PhysicsMng manager; List<Vector3> expected; int frame; Report report;
    [Serializable] class CostReport { public bool passed; public float fullMilliseconds, simplifiedMilliseconds, targetGuideQueryMilliseconds, targetGuideLength; public bool targetGuideHit, stationaryBallEndpointConfirmed, cuePathUnchanged; public int fullFrames, simplifiedFrames, remainingBalls; }
    public static Impulse Prepare(PhysicsMng manager, Impulse fallback)
    {
        if(!File.Exists("Logs/guide-validation.request")) return fallback;
        File.Delete("Logs/guide-validation.request");
        // Remove orphaned DontSave preview objects from the failed initial editor-only prototype.
        var geometryNames=UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)
            .Where(c=>c.gameObject.scene==manager.gameObject.scene && (c.gameObject.layer==8 || c.gameObject.layer==9)).Select(c=>c.name).ToHashSet();
        foreach(var c in Resources.FindObjectsOfTypeAll<Collider>()) {
            if(!c || !string.IsNullOrEmpty(c.gameObject.scene.name) || UnityEditor.EditorUtility.IsPersistent(c)) continue;
            if(c.GetComponent<ShotPredictionContact>() || ((c.gameObject.hideFlags & HideFlags.DontSave)!=0 && (c.name.StartsWith("Preview ball ") || (geometryNames.Contains(c.name) && (c.gameObject.layer==8 || c.gameObject.layer==9)))))
                UnityEngine.Object.DestroyImmediate(c.gameObject);
        }
        var positions=new[] {new Vector2(-.6f,0),new Vector2(-1.1f,-.65f),new Vector2(.8f,-.6f),new Vector2(-.15f,.035f)};
        for(int i=0;i<manager.ballcs.Length;i++) {
            var b=manager.ballcs[i].body; b.position=new Vector3(positions[i].x,b.position.y,positions[i].y);
            b.linearVelocity=Vector3.zero;b.angularVelocity=Vector3.zero;
        }
        Physics.SyncTransforms();
        var shot=manager.shotController;
        foreach(var ball in manager.ballcs) ball.SetCueBall(ball.id==0);
        shot.cueBall=manager.ballcs[0];manager.cuball=shot.cueBall.body;
        shot.cuePivot.position=manager.ballcs[0].body.position;shot.cuePivot.rotation=Quaternion.LookRotation(Vector3.right);
        shot.cueDisplacement.localPosition=new Vector3(.005f,.012f,0);shot.SetFollowThrough(1);
        typeof(ShotCtrl).GetProperty("force").SetValue(shot,.32f);
        var impulse=shot.BuildShotImpulse();
        var probe=manager.gameObject.AddComponent<GuidePredictionValidation>();probe.manager=manager;probe.report=new Report();
        var before=manager.ballcs.Select(b=>b.body.position).ToArray();
        using(var prediction=new ShotPrediction(manager)) {
            var watch=System.Diagnostics.Stopwatch.StartNew();prediction.Calculate(0,impulse,2,100);watch.Stop();
            probe.report.predictionMilliseconds=(float)watch.Elapsed.TotalMilliseconds;
            probe.report.sourceTensor=manager.ballcs[0].body.inertiaTensor;probe.report.previewTensor=prediction.initialTensor;probe.report.previewSpin=prediction.firstSpin;
            probe.expected=new List<Vector3>(prediction.timedCuePath);probe.report.targetIndex=prediction.targetIndex;
            probe.report.predicted=probe.expected;
            using(var repeated=new ShotPrediction(manager)) {
            repeated.Calculate(0,impulse,2,100);
            probe.report.repeated=new List<Vector3>(repeated.timedCuePath);
            probe.Save();
            var leaked=Physics.OverlapSphere(manager.ballcs[0].body.position,10).FirstOrDefault(c=>c.name.StartsWith("Preview ball"));
            if(leaked) throw new Exception("Preview collider entered live physics scene: " + leaked.name + " / " + leaked.gameObject.scene.name + " valid=" + leaked.gameObject.scene.IsValid() + " flags=" + leaked.gameObject.hideFlags + " contact=" + (leaked.GetComponent<ShotPredictionContact>()!=null));
            for(int i=0;i<probe.expected.Count;i++) probe.report.repeatError=Mathf.Max(probe.report.repeatError,Vector3.Distance(probe.expected[i],repeated.timedCuePath[i]));
            }
        }
        var cost=new CostReport(); var guideImpulse=shot.BuildGuideImpulse();
        typeof(ShotCtrl).GetProperty("force").SetValue(shot,.8f);
        var changedPowerGuide=shot.BuildGuideImpulse();
        if(guideImpulse.impulse!=changedPowerGuide.impulse || guideImpulse.point!=changedPowerGuide.point) throw new Exception("Power slider changed fixed reference guide");
        typeof(ShotCtrl).GetProperty("force").SetValue(shot,.32f);
        var timer=System.Diagnostics.Stopwatch.StartNew();
        using(var full=new ShotPrediction(manager)) { full.Calculate(0,guideImpulse); cost.fullFrames=full.timedCuePath.Count; }
        timer.Stop();cost.fullMilliseconds=(float)timer.Elapsed.TotalMilliseconds;timer.Restart();
        Vector3 targetStart=Vector3.zero,targetEnd=Vector3.zero;List<Vector3> originalCuePath=null;
        using(var light=new ShotPrediction(manager)) {
            light.Calculate(0,guideImpulse,firstContactOnly:true);
            cost.simplifiedFrames=light.timedCuePath.Count;cost.remainingBalls=light.ActiveBallCount;
            cost.targetGuideQueryMilliseconds=light.targetGuideQueryMilliseconds;cost.targetGuideHit=light.targetGuideHit;
            cost.targetGuideLength=Vector3.Distance(light.targetPath[0],light.targetPath[1]);
            targetStart=light.targetPath[0];targetEnd=light.targetPath[1];originalCuePath=new List<Vector3>(light.timedCuePath);
            cost.passed=light.targetIndex==3 && cost.remainingBalls==1 && light.targetPath.Count==2 && cost.simplifiedFrames<cost.fullFrames && cost.targetGuideHit && cost.targetGuideLength>.35f;
        }
        timer.Stop();cost.simplifiedMilliseconds=(float)timer.Elapsed.TotalMilliseconds;
        var obstacle=manager.ballcs[2].body;var oldPosition=obstacle.position;bool slept=obstacle.IsSleeping();
        try {
            obstacle.position=Vector3.Lerp(targetStart,targetEnd,.5f);Physics.SyncTransforms();
            using(var blocked=new ShotPrediction(manager)) {
                blocked.Calculate(0,guideImpulse,firstContactOnly:true);
                float contactDistance=manager.ballcs[2].GetComponent<SphereCollider>().radius*Mathf.Abs(manager.ballcs[2].transform.lossyScale.x)
                    +manager.ballcs[3].GetComponent<SphereCollider>().radius*Mathf.Abs(manager.ballcs[3].transform.lossyScale.x);
                cost.stationaryBallEndpointConfirmed=blocked.targetGuideHit && Mathf.Abs(Vector3.Distance(blocked.targetPath[1],obstacle.position)-contactDistance)<.0001f
                    && Vector3.Distance(blocked.targetPath[0],blocked.targetPath[1])<cost.targetGuideLength;
                cost.cuePathUnchanged=blocked.timedCuePath.Count==originalCuePath.Count && !blocked.timedCuePath.Where((p,i)=>Vector3.Distance(p,originalCuePath[i])>.00001f).Any();
            }
        } finally { obstacle.position=oldPosition;Physics.SyncTransforms();if(slept) obstacle.Sleep(); }
        cost.passed &= cost.stationaryBallEndpointConfirmed && cost.cuePathUnchanged;
        File.WriteAllText("Logs/guide-lightweight.json",JsonUtility.ToJson(cost,true));
        if(!cost.passed) throw new Exception("Simplified guide did not exclude secondary balls or reduce its horizon");
        if(manager.ballcs.Where((b,i)=>b.body.position!=before[i] || b.body.linearVelocity!=Vector3.zero).Any()) throw new Exception("Prediction changed live balls");
        probe.Save(); return impulse;
    }
    void FixedUpdate()
    {
        if(expected==null || !manager.inMove) return;
        frame++;
        if(frame==1) report.actualSpin=manager.ballcs[0].body.angularVelocity;
        report.actual.Add(manager.ballcs[0].body.position);
        report.maxError=Mathf.Max(report.maxError,Vector3.Distance(manager.ballcs[0].body.position,expected[frame]));report.frames=frame;
        if(frame<expected.Count-1) return;
        report.passed=report.maxError<.002f && report.repeatError<.002f && report.targetIndex==3;
        if(!report.passed) report.error="Prediction differs from live physics or misses controlled first collision";
        Save(); expected=null;
    }
    void Save() => File.WriteAllText("Logs/guide-validation.json",JsonUtility.ToJson(report,true));
}
#endif
