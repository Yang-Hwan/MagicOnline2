using Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Surface
{
    public class BallPointSmall : MonoBehaviour
    {

        public GameObject pointBig;
        public RectTransform bigBallFrm;
        public GameObject blindBack;
        private RectTransform point;
        float cr;
        PhysicsMng physics;
        ShotCtrl shot;
        bool wasMoving;

        private void LateUpdate()
        {
            if (!physics) physics = FindAnyObjectByType<PhysicsMng>();
            if (!physics) return;
            if (physics.IsPracticeReplay) { wasMoving = false; return; }
            bool moving = physics.inMove;
            if (wasMoving && !moving)
            {
                if (!shot) shot = FindAnyObjectByType<ShotCtrl>();
                pointBig.GetComponent<BallPointBig>().ResetToCenter(shot);
                point.anchoredPosition = Vector2.zero;
            }
            wasMoving = moving;
        }

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(pointBigOpen_Click);
            bigBallFrm = pointBig.transform.Find("BallFrm").GetComponent<RectTransform>();
            point = transform.Find("Point").GetComponent<RectTransform>();
            float bc = bigBallFrm.sizeDelta.x;
            float tc = this.GetComponent<RectTransform>().sizeDelta.x;
            cr = tc / bc;

            //Debug.Log($"cr : {cr}, tc : {tc}, bc : {bc}");
        }

        private void Start()
        {
        }

        void pointBigOpen_Click()
        {
            var physics = FindAnyObjectByType<PhysicsMng>();
            if (physics && physics.IsPracticeReplay) return;
            blindBack.SetActive(true);
            pointBig.SetActive(true);
            pointBig.GetComponent<BallPointBig>().CueRotateState(transform.rotation);
        }


        private void OnEnable()
        {
            wasMoving = false;
            ShotCtrl.OnCueRotateState += ShotController_OnCueRotateState;
            BallPointBig.OnCueSpinState += BallPointBig_OnCueSpinState;

        }

        private void OnDisable()
        {
            ShotCtrl.OnCueRotateState -= ShotController_OnCueRotateState;
            BallPointBig.OnCueSpinState -= BallPointBig_OnCueSpinState;
        }

        void ShotController_OnCueRotateState(Quaternion quaternion)
        {
            Quaternion qua = new Quaternion(quaternion.x, 0, -quaternion.y, quaternion.w);
            transform.rotation = qua;
        }

        void BallPointBig_OnCueSpinState(Vector2 spin)
        {
            Vector3 point_small = new Vector2(spin.x * cr, spin.y * cr);
            point.anchoredPosition = point_small;
        }
    }

}
