using Assets.Scripts.Sight.Vital.Pavilion;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Sight.Surface.Pavilion
{
    public class BallPointSmall : MonoBehaviour
    {

        public GameObject pointBig;
        public RectTransform bigBallFrm;
        public GameObject blindBack;
        private RectTransform point;
        float cr;

        private void Awake()
        {
            blindBack = transform.parent.Find("BlindBack").gameObject;
            pointBig = transform.parent.Find("PointBig").gameObject;
            bigBallFrm = pointBig.transform.Find("BallFrm").GetComponent<RectTransform>();

            GetComponent<Button>().onClick.AddListener(pointBigOpen_Click);
            point = transform.Find("Point").GetComponent<RectTransform>();
            float bc = bigBallFrm.sizeDelta.x;
            float tc = this.GetComponent<RectTransform>().sizeDelta.x;
            cr = tc / bc;

            //Debug.Log($"cr : {cr}, tc : {tc}, bc : {bc}");
        }


        void pointBigOpen_Click()
        {
            blindBack.SetActive(true);
            pointBig.SetActive(true);
            pointBig.GetComponent<BallPointBig>().CueRotateState(transform.rotation);
        }

        private void OnEnable()
        {
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
