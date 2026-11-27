using Assets.Scripts.Often;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{
    public partial class ShotCtrl
    {
        MatchShotCommand preparedComputerStroke;
        public void PrepareComputerStroke(MatchShotCommand shot)
        {
            CueReadyShot();
            preparedComputerStroke=shot;
            cuePivot.rotation=Quaternion.Euler(0,shot.yaw,0);
            cueDisplacement.localPosition=Vector3.zero;
            Vector3 forward=Quaternion.Euler(0,shot.yaw,0)*Vector3.forward;
            float radius=cueBall.GetComponent<SphereCollider>().radius*Mathf.Abs(cueBall.transform.lossyScale.x);
            cueDisplacement.position+=radius*(Vector3.Cross(Vector3.up,forward)*shot.contact.x+Vector3.up*shot.contact.y);
            pushNormal=shot.contact;
            ShowOnlineStroke(shot.power,shot.followThrough);
            cueSlider.localPosition=new Vector3(0,0,forceSliderYPos*forceSliderToCueRate);
            OnCueRotateState?.Invoke(cuePivot.localRotation);
        }
        public bool PlayComputerStroke()
        {
            if(!physicsManager.IsComputerTurn || physicsManager.inMove || IsShotAnimating ||
                physicsManager.IsPracticeReplay || physicsManager.IsBallPlacement || force<=0 || preparedComputerStroke==null)return false;
            WaitAndStartShot().Forget();
            return true;
        }
        public Impulse BuildPreparedComputerImpulse() => BuildOnlineImpulse(preparedComputerStroke);
    }
}
