using System;
using Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital;
using PreviewImpulse = Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.Impulse;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.Sight.Vital.Pavilion
{
    public partial class ShotCtrl
    {
        ShotPrediction prediction;
        bool predictionDirty=true;
        PreviewImpulse previousPreviewImpulse;
        int previousPreviewCue=-1;
        Vector3[] previousPreviewPositions;
        float nextPreviewTime;
        float quickPreviewDistance;
        public int PreviewPartialUpdates { get; private set; }
        bool pendingPreview;
        bool[] previousPreviewActive;
        int previousPhysicsKey;
        readonly List<Vector3> quickPreview=new List<Vector3>(2);
        Vector3[] previewRenderBuffer=new Vector3[1024];
        public int PreviewSceneBuilds { get; private set; }
        public int PreviewCancellations { get; private set; }
        public int PreviewCompleted { get; private set; }
        public bool PreviewIsCalculating => prediction!=null && prediction.IsCalculating;
        public double PreviewMaxSliceMs => prediction?.MaxSliceMilliseconds ?? 0;

        // Call when board geometry changes; ordinary aim/ball changes only reset the actors.
        public void InvalidatePredictionGeometry() { prediction?.Dispose();prediction=null;predictionDirty=true;pendingPreview=false; }


        int PreviewPhysicsKey()
        {
            var c=physicsManager.clothPhysics;
            unchecked {
                int key=c.slidingFriction.GetHashCode();
                key=key*31+c.rollingResistance.GetHashCode();key=key*31+c.spinDeceleration.GetHashCode();
                key=key*31+c.stationarySpinDamping.GetHashCode();key=key*31+c.stopSpeed.GetHashCode();
                key=key*31+c.stopSpin.GetHashCode();key=key*31+c.stopDelay.GetHashCode();
                key=key*31+c.substeps;key=key*31+Time.fixedDeltaTime.GetHashCode();
                key=key*31+Physics.gravity.GetHashCode();return key*31+lineLength.GetHashCode();
            }
        }

        void RenderQuickPreview()
        {
            Vector3 origin=cueBall.body.position,dir=Vector3.ProjectOnPlane(cueSlider.forward,Vector3.up).normalized;
            float radius=cueBall.GetComponent<SphereCollider>().radius*Mathf.Abs(cueBall.transform.lossyScale.x);
            float nearest=3+lineLength;Ball target=null;float thickness=0;
            foreach(var ball in physicsManager.balls) {
                if(ball==cueBall || !ball.gameObject.activeInHierarchy)continue;
                float r=ball.GetComponent<SphereCollider>().radius*Mathf.Abs(ball.transform.lossyScale.x);
                if(TryAimOverlap(origin,dir,radius,ball.body.position,r,out var distance,out _,out var amount) && distance<nearest)
                {nearest=distance;target=ball;thickness=amount;}
            }
            if(physicsManager.gameObject.scene.GetPhysicsScene().SphereCast(origin,radius,dir,out var rail,nearest,boardLayer,QueryTriggerInteraction.Ignore))
            {nearest=rail.distance;target=null;}
            quickPreviewDistance=nearest;
            quickPreview.Clear();quickPreview.Add(origin);quickPreview.Add(origin+dir*nearest);
            RenderPredictedPath(cueBallLines,cueBallLineMaterials,quickPreview,nearest);
            foreach(var line in targetBallLines)line.positionCount=0;
            SetBallChecker(origin+dir*nearest,target ? target.id : -1);
            if(target)physicsManager.ringCue.FigureVal(thickness);
        }
        bool previousImpactTrial;
        bool previousFollowProfile;
        bool previousNaturalFollowDraw;
        bool previousThicknessBlend;
        float previousTopspinCarry;
        float previousImpactPowerAngle, previousImpactSpinAngle;

        void LateUpdate()
        {
            if (cueBallLineMaterials == null) return;
            UpdateGaugeValueLabels();
            UpdateOverlapPreview();
            if(!physicsManager || !physicsManager.useCalibratedPhysics || !cueBall || !cueBall.isCueBall || cueVertical.parent!=cuePivot || !canControl || IsShotAnimating ||
                physicsManager.inMove) {
                prediction?.Cancel();pendingPreview=false;predictionDirty=true;
                foreach(var line in cueBallLines)if(line)line.positionCount=0;
                foreach(var line in targetBallLines)if(line)line.positionCount=0;
                return;
            }
            // Reference guide at 40% power; moving the power handle does not invalidate it.
            var impulse=BuildGuideImpulse();
            int physicsKey=PreviewPhysicsKey();
            if(previousPhysicsKey!=physicsKey)predictionDirty=true;
            if(previousThicknessBlend!=physicsManager.blendFollowDrawByThickness)predictionDirty=true;
            if(previousNaturalFollowDraw!=physicsManager.useNaturalFollowDraw)predictionDirty=true;
            if(previousTopspinCarry!=physicsManager.topspinForwardCarryGain)predictionDirty=true;
            if(previousFollowProfile!=physicsManager.FollowThroughProfileActive || previousPreviewImpulse.followThrough!=impulse.followThrough || previousPreviewImpulse.spinPersistence!=impulse.spinPersistence)predictionDirty=true;
            if(previousImpactTrial!=physicsManager.BallImpactTrialActive || previousImpactPowerAngle!=physicsManager.impactPowerAngle ||
                previousImpactSpinAngle!=physicsManager.impactSpinAngle) predictionDirty=true;
            if(impulse.point!=previousPreviewImpulse.point || impulse.impulse!=previousPreviewImpulse.impulse || previousPreviewCue!=cueBall.id) predictionDirty=true;
            if(previousPreviewPositions==null || previousPreviewPositions.Length!=physicsManager.balls.Length)
            { previousPreviewPositions=new Vector3[physicsManager.balls.Length];previousPreviewActive=new bool[previousPreviewPositions.Length]; InvalidatePredictionGeometry(); }
            for(int i=0;i<previousPreviewPositions.Length;i++)
                if(previousPreviewPositions[i]!=physicsManager.balls[i].body.position || previousPreviewActive[i]!=physicsManager.balls[i].gameObject.activeInHierarchy) predictionDirty=true;
            if(predictionDirty) {
                if(prediction!=null && prediction.IsCalculating)PreviewCancellations++;
                prediction?.Cancel();pendingPreview=true;nextPreviewTime=Time.unscaledTime;
                RenderQuickPreview();
                previousPreviewImpulse=impulse;previousPreviewCue=cueBall.id;
                previousFollowProfile=physicsManager.FollowThroughProfileActive;
                previousNaturalFollowDraw=physicsManager.useNaturalFollowDraw;
                previousThicknessBlend=physicsManager.blendFollowDrawByThickness;
                previousTopspinCarry=physicsManager.topspinForwardCarryGain;
                previousImpactTrial=physicsManager.BallImpactTrialActive;
                previousImpactPowerAngle=physicsManager.impactPowerAngle;previousImpactSpinAngle=physicsManager.impactSpinAngle;
                for(int i=0;i<previousPreviewPositions.Length;i++) {
                    previousPreviewPositions[i]=physicsManager.balls[i].body.position;
                    previousPreviewActive[i]=physicsManager.balls[i].gameObject.activeInHierarchy;
                }
                previousPhysicsKey=physicsKey;predictionDirty=false;
                return;
            }
            if(pendingPreview && Time.unscaledTime>=nextPreviewTime) {
                if(prediction==null) {prediction=new ShotPrediction(physicsManager);PreviewSceneBuilds++;}
                prediction.Begin(Array.IndexOf(physicsManager.balls,cueBall),previousPreviewImpulse,8,3+lineLength,true,lineLength);
                pendingPreview=false;
            }
            if(prediction!=null && prediction.IsCalculating) {
                // Spend more work only after input stabilizes; publish without waiting for the full path.
                bool complete=prediction.Advance(8,64);
                // Keep the already visible full approach line until the simulation reaches its endpoint.
                if(!complete && prediction.targetIndex<0 && prediction.PathDistance<quickPreviewDistance)return;
                if(prediction.cuePath.Count<2)return;
                RenderPredictedPath(cueBallLines,cueBallLineMaterials,prediction.cuePath,3+lineLength);
                RenderPredictedPath(targetBallLines,targetBallLineMaterials,prediction.targetPath,float.PositiveInfinity);
                SetBallChecker(prediction.contactCentre,prediction.targetIndex<0 ? -1 : physicsManager.balls[prediction.targetIndex].id);
                if(prediction.targetIndex>=0)physicsManager.ringCue.FigureVal(prediction.contactThickness);
                if(complete)PreviewCompleted++;else PreviewPartialUpdates++;
            }
        }

        void RenderPredictedPath(LineRenderer[] lines,Material[] materials,List<Vector3> points,float limit)
        {
            foreach(var line in lines) line.positionCount=0;
            if(points.Count<2 || lines.Length==0) return;
            if(previewRenderBuffer.Length<points.Count+1)Array.Resize(ref previewRenderBuffer,Mathf.NextPowerOfTwo(points.Count+1));
            int count=1;previewRenderBuffer[0]=points[0];float length=0;
            for(int i=1;i<points.Count;i++)
            {
                float segment=Vector3.Distance(points[i-1],points[i]);
                if(segment<.000001f) continue;
                if(length+segment>limit) { previewRenderBuffer[count++]=Vector3.Lerp(points[i-1],points[i],(limit-length)/segment); length=limit; break; }
                previewRenderBuffer[count++]=points[i];length+=segment;
            }
            lines[0].positionCount=count;
            lines[0].SetPositions(previewRenderBuffer);
            materials[0].SetVector("_Tiling",new Vector2(length*20,1));
        }
    }
}
