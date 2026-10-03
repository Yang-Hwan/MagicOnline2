using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{
    public partial class ShotCtrl
    {
        RectTransform overlapPanel, overlapCue;
        OverlapDisc overlapTargetDisc, overlapCueDisc;
        TextMeshProUGUI overlapLabel;
        float nextOverlapUpdate;
        int overlapPercent=-1;

        void CreateOverlapPreview()
        {
            var go=new GameObject("Aim Overlap Preview",typeof(RectTransform),typeof(Image));
            overlapPanel=go.GetComponent<RectTransform>();overlapPanel.SetParent(gaugeValueCanvas,false);
            overlapPanel.anchorMin=overlapPanel.anchorMax=new Vector2(.5f,1);
            overlapPanel.pivot=new Vector2(.5f,1);overlapPanel.anchoredPosition=new Vector2(0,-185);
            overlapPanel.sizeDelta=new Vector2(210,100);
            var background=go.GetComponent<Image>();background.color=new Color(.025f,.045f,.06f,.85f);background.raycastTarget=false;
            var label=new GameObject("Thickness",typeof(RectTransform),typeof(TextMeshProUGUI));label.transform.SetParent(overlapPanel,false);
            overlapLabel=label.GetComponent<TextMeshProUGUI>();overlapLabel.font=powerValueLabel.font;
            overlapLabel.fontSize=18;overlapLabel.alignment=TextAlignmentOptions.Center;overlapLabel.raycastTarget=false;
            overlapLabel.rectTransform.sizeDelta=new Vector2(210,28);overlapLabel.rectTransform.anchoredPosition=new Vector2(0,33);
            overlapTargetDisc=CreateOverlapDisc("Target Ball",new Color(1,.12f,.12f,.95f));
            overlapCueDisc=CreateOverlapDisc("Cue Ball",new Color(1,1,1,.6f));overlapCue=overlapCueDisc.rectTransform;
            overlapPanel.gameObject.SetActive(false);
        }

        OverlapDisc CreateOverlapDisc(string name,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(OverlapDisc));go.transform.SetParent(overlapPanel,false);
            var disc=go.GetComponent<OverlapDisc>();disc.color=color;disc.raycastTarget=false;
            disc.rectTransform.sizeDelta=new Vector2(48,48);disc.rectTransform.anchoredPosition=new Vector2(0,-9);
            return disc;
        }

        // View along the shot direction: projected overlap / combined radii.
        public static bool TryAimOverlap(Vector3 origin,Vector3 direction,float cueRadius,Vector3 target,
            float targetRadius,out float distance,out float signedOffset,out float thickness)
        {
            distance=0;signedOffset=0;thickness=0;
            direction=Vector3.ProjectOnPlane(direction,Vector3.up).normalized;
            float sum=cueRadius+targetRadius;
            if(sum<=0 || direction.sqrMagnitude<.5f)return false;
            Vector3 delta=Vector3.ProjectOnPlane(target-origin,Vector3.up);
            float along=Vector3.Dot(delta,direction);
            if(along<0)return false;
            float side=Vector3.Dot(delta,Vector3.Cross(Vector3.up,direction));
            if(Mathf.Abs(side)>=sum)return false;
            distance=Mathf.Max(0,along-Mathf.Sqrt(Mathf.Max(0,sum*sum-side*side)));
            signedOffset=side/sum;thickness=1-Mathf.Abs(signedOffset);return true;
        }

        void UpdateOverlapPreview()
        {
            if(!overlapPanel)return;
            // Replay keeps the original aiming overlap, not a fresh query against moving balls.
            if(physicsManager && physicsManager.IsPracticeReplay)return;
            bool aiming=physicsManager && cueBall && cueBall.isCueball && cueSlider && cueVertical &&
                cueVertical.parent==cuePivot && canControl && !IsShotAnimating && !physicsManager.inMove &&
                !physicsManager.IsPracticeReplay && !physicsManager.IsBallPlacement;
            if(!aiming) {overlapPanel.gameObject.SetActive(false);nextOverlapUpdate=0;return;}
            // At most 20 cheap geometry queries per second; never advances physics.
            if(Time.unscaledTime<nextOverlapUpdate)return;
            nextOverlapUpdate=Time.unscaledTime+.05f;
            RenderAimOverlap(cueBall.id, cueBall.body.position, cueSlider.forward, null);
        }

        public void ShowReplayAimOverlap(int cueId, Vector3[] positions, Quaternion direction)
        {
            if(!overlapPanel || positions == null || cueId < 0 || cueId >= positions.Length)return;
            RenderAimOverlap(cueId, positions[cueId], direction * Vector3.forward, positions);
        }

        public void HideReplayAimOverlap()
        {
            if(overlapPanel)overlapPanel.gameObject.SetActive(false);
            nextOverlapUpdate=0;
        }

        void RenderAimOverlap(int cueId, Vector3 origin, Vector3 direction, Vector3[] recordedPositions)
        {
            Vector3 dir=Vector3.ProjectOnPlane(direction,Vector3.up).normalized;
            var cue=physicsManager.ballcs[cueId];
            float radius=cue.GetComponent<SphereCollider>().radius*Mathf.Abs(cue.transform.lossyScale.x);
            BallC target=null;float nearest=float.PositiveInfinity,side=0,thickness=0;
            foreach(var ball in physicsManager.ballcs) {
                if(!ball || ball.id==cueId)continue;
                if(recordedPositions!=null ? ball.id>=recordedPositions.Length : !ball.gameObject.activeInHierarchy)continue;
                float otherRadius=ball.GetComponent<SphereCollider>().radius*Mathf.Abs(ball.transform.lossyScale.x);
                if(TryAimOverlap(origin,dir,radius,recordedPositions!=null ? recordedPositions[ball.id] : ball.body.position,otherRadius,out var distance,out var offset,out var amount) && distance<nearest)
                {target=ball;nearest=distance;side=offset;thickness=amount;}
            }
            // A cushion blocks a direct ball overlap; no cushion-path simulation is needed.
            if(target && Physics.SphereCast(origin,radius,dir,out var rail,nearest,boardLayer,QueryTriggerInteraction.Ignore))target=null;
            overlapPanel.gameObject.SetActive(target!=null);
            if(!target)return;
            int percent=Mathf.RoundToInt(thickness*100);
            if(percent!=overlapPercent) {overlapLabel.text="예상 두께 "+percent+"%";overlapPercent=percent;}
            overlapCue.anchoredPosition=new Vector2(-side*48,-9);
            overlapCueDisc.color=cueId==1 ? new Color(1,.85f,.15f,.6f) : new Color(1,1,1,.6f);
            overlapTargetDisc.color=target.id==1 ? new Color(1,.85f,.15f,.95f) : target.id==0 ? Color.white : new Color(1,.12f,.12f,.95f);
        }
    }
}
