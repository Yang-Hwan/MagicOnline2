using Assets.Scripts.Sight.Surface.Pavilion;
using Assets.Scripts.Sight.Vital.Pavilion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Exert.Match
{
    public class PoolNetMsg : MonoBehaviour
    {
        private PhysicsMng _physicsMng;
        private PhysicsMng physicsMng
        {
            get
            {
                if (_physicsMng == null)
                {
                    _physicsMng = FindObjectOfType<PhysicsMng>();
                }
                return _physicsMng;
            }
        }

        private DrawStuffPos _drawStuffPos;
        private DrawStuffPos drawStuffPos
        {
            get
            {
                if (_drawStuffPos == null)
                {
                    _drawStuffPos = FindObjectOfType<DrawStuffPos>();
                }
                return _drawStuffPos;
            }
        }

        private ShotCtrl _shotCtrl;
        private ShotCtrl shotCtrl
        {
            get
            {
                //Debug.Log($"PoolNetMsg _shotCtrl : {_shotCtrl == null}");
                if (_shotCtrl==null)
                {
                    _shotCtrl = ShotCtrl.FindObjectOfType<ShotCtrl>();
                }
                return _shotCtrl;
            }
        }


        private PnlMatch _pnlMatch;
        private PnlMatch pnlMatch
        {
            get
            {
                if (_pnlMatch == null)
                {
                    _pnlMatch = PnlMatch.FindObjectOfType<PnlMatch>();
                }
                return _pnlMatch;
            }
        }

        public void InitRandForceFlutter(string randForce)
        {
            physicsMng.InitRandForceFromNetwork(randForce);
        }

        public void SetMechanicalStatesFromNetwork(int ballId, string mechanicalStateData)
        {
            //Debug.Log($"SetMechanicalStatesFromNetwork physicsMng.balls len : {physicsMng.balls.Length} ..ballId : {ballId} .. mechanicalStateData : {mechanicalStateData}");
            if (physicsMng.balls.Length == 0) return;
            if (physicsMng.balls[ballId] == null) { Debug.LogWarning("if (physicsMng.balls[ballId] == null)"); return; }
            physicsMng.balls[ballId].SetMechanicalStatesFromNetwork(mechanicalStateData).Forget();
            //StartCoroutine(physicsManager.balls[ballId].SetMechanicalStatesFromNetwork(mechanicalStateData));
        }

        public void WaitAndStopMoveFromNetwork(float time)
        {
            physicsMng.WaitAndStopMoveFromNetwork(time).Forget();
        }

        public void WaitChangeTurnReadyFromNetwork()
        {
            PoolCoach.Instance.WaitChangeTurnReadyFromNetwork();
        }

        public void SetTime(float time01)
        {
            if (PoolLogic.controlFromNetwork)
            {
                PoolCoach.Instance.SetPlayTime(time01);
            }
        }

        public void OnSendCueControl(float cuePivotLocalRotationY, float cueVerticalLocalRotationX, Vector2 cueDisplacementLocalPositionXY, float cueSliderLocalPositionZ, float force, float pullBarHandleLocalPositionY)
        {
            if (shotCtrl)
            {
                shotCtrl.CueControlFromNetwork(cuePivotLocalRotationY, cueVerticalLocalRotationX, cueDisplacementLocalPositionXY, cueSliderLocalPositionZ, force, pullBarHandleLocalPositionY);
            }
            else
            {
                Debug.Log("_shotCtrl NULL");
            }
        }

        public void StartSimulate(string impulse)
        {
            physicsMng.StartShotFromNetwork(impulse);
        }

        public void SetStuffCreateFromNetwork(float moveTime, string stuffData)
        {
            drawStuffPos.SetStuffCreateFromNetwork(moveTime, stuffData).Forget();
        }

        public void SetStuffVanishFromNetwork(float moveTime, int stuffData, int coin_ch)
        {
            drawStuffPos.SetStuffVanishFromNetwork(moveTime, stuffData, coin_ch).Forget();
        }

        public void SetStuffEffect01FromNetwork(float moveTime, int stuffData)
        {
            drawStuffPos.SetStuffEffect01FromNetwork(moveTime, stuffData).Forget();
        }
        public void SetStuffEffect02FromNetwork(float moveTime, int stuffData, Vector3 to)
        {
            drawStuffPos.SetStuffEffect02FromNetwork(moveTime, stuffData, to).Forget();
        }
        public void SetStuffEffect03FromNetwork(float moveTime, int stuffData, int[] ord)
        {
            drawStuffPos.SetStuffEffect03FromNetwork(moveTime, stuffData, ord).Forget();
        }

        public void SetBallMovStoryFromNetwork(float moveTime, string StoryData)
        {
            physicsMng.SetBallMovStoryFromNetwork(moveTime, StoryData).Forget();
        }

        public void SetMatchSummaryFromNetwork(string summaryData)
        {
            pnlMatch.SetMatchSummaryFromNetwork(summaryData);
        }

    }



}
