using Assets.Scripts.Sight.Vital.Pavilion;
using UnityEngine;

namespace Assets.Scripts.Sight.Surface.Pavilion
{
    public class BallPointBig : MonoBehaviour
    {

        enum OutLineType
        {
            None,
            InSide,
            OutSide,
        }



        public delegate void OnCueSpin(Vector2 point);
        public static event OnCueSpin OnCueSpinState;

        const float x_unfold = 0.5625f;     // 16:9 산출식
        const float y_unfold = 1.7777f;     // 16:9 산출식
        const float ball_point_viewport_boundary = 0.19f;        // 당점 허용 가능한 거리 최대치
        const float ball_point_ui_boundary = 370f;        // 당점 허용 가능한 거리 최대치
        private float radius;
        private bool canControl = false;

        private OutLineType outLineTypeStart;
        private OutLineType outLineTypeIng;
        private bool isDrag = false;
        private bool isOff = false;
        private Vector2 scrMidPos;
        private Vector2 scrMaxPos;
        private float dragDistUI = 140;          // 드래그 가능 거리 UI 기준
        private float WidthMaxUI = 1600;         // 넓이 UI 기준
        private float HeightMaxUI = 0;         // 넓이 UI 기준
        private float movAngle;
        private float ballSpin;
        private float dragDistScr;
        private float dragDistRate;
        private float WidthMidUI;               // 중앙넓이 UI 기준
        private float HeightMidUI;               // 중앙높이 UI 기준
        private RectTransform bigBallFrm;

        [SerializeField] private RectTransform canvas;
        [SerializeField] private RectTransform point_v;
        [SerializeField] private RectTransform point;
        [SerializeField] private ShotCtrl shotController;
        [SerializeField] private GameObject blindBack;
        private void Awake()
        {
            canvas = transform.parent.parent.parent.GetComponent<RectTransform>();
            blindBack = transform.parent.Find("BlindBack").gameObject;
            point_v = transform.Find("Point_V").GetComponent<RectTransform>();
            point = transform.Find("BallFrm/Point").GetComponent<RectTransform>();
            shotController = FindObjectOfType<ShotCtrl>();
            bigBallFrm = transform.Find("BallFrm").GetComponent<RectTransform>();
            ballSpin = 0;
            RectTransform rectTransform = GetComponent<RectTransform>();
            radius = 0.5f * (bigBallFrm.sizeDelta.x - point.sizeDelta.x);
            //Debug.Log($"rt.x : {bigBallFrm.sizeDelta.x}, po.x : {point.sizeDelta.x}");

        }

        private void Start()
        {
            scrMidPos = InputOutput.mouseScreenMidPosition;
            scrMaxPos = InputOutput.mouseScreenMaxPosition;
            WidthMaxUI = canvas.sizeDelta.x;
            HeightMaxUI = canvas.sizeDelta.y;
            dragDistRate = dragDistUI / WidthMaxUI;     // 드래그시 너무 작게 움직인거리는 움직임으로 인정 안함.
            dragDistScr = scrMaxPos.x * dragDistRate;
            WidthMidUI = WidthMaxUI * 0.5f;
            HeightMidUI = HeightMaxUI * 0.5f;
            outLineTypeStart = OutLineType.None;
            //Debug.Log($"scrMidPos : ({scrMidPos.x}, {scrMidPos.y}), MaxUI : ({WidthMaxUI}, {HeightMaxUI}), MidUI : ({WidthMidUI}, {HeightMidUI})");
        }

        private void OnEnable()
        {
            canControl = true;
            ShotCtrl.canControl = false;
            InputOutput.OnMouseState += InputOutput_OnMouseState;
        }

        private void OnDisable()
        {
            canControl = false;
            ShotCtrl.canControl = true;
            InputOutput.OnMouseState -= InputOutput_OnMouseState;
            gameObject.SetActive(false);
        }

        bool IsInSide(out float dist_v)
        {
            bool isIn = false;
            float vPx = InputOutput.mouseViewportPoint.x;
            float vPy = InputOutput.mouseViewportPoint.y;
            float vx = vPx - 0.5f;
            float vy = (vPy - 0.5f) * x_unfold;
            float dist = Mathf.Sqrt(vx * vx + vy * vy);
            dist_v = dist;
            if (dist <= ball_point_viewport_boundary) isIn = true;
            //Debug.Log($"vp: ({vPx}, {vPy}), v : ({vx}, {vy}), dist : {dist}");
            return isIn;
        }

        Vector3 ScreenPosToRectPos(Vector3 scrPos)
        {
            Vector3 retPos = new Vector3();
            float rateX = scrPos.x / scrMaxPos.x;
            float rateY = scrPos.y / scrMaxPos.y;
            float rectX = WidthMaxUI * rateX;           // 스크린좌표를 ui좌표로 변환
            float rectY = HeightMaxUI * rateY;
            rectX = rectX - WidthMidUI;
            rectY = rectY - HeightMidUI;
            retPos.x = rectX;
            retPos.y = rectY;
            //Debug.Log($"rate: ({rateX}, {rateY}), scrPos: ({scrPos.x}, {scrPos.y}), retPos : ({rectX}, {rectY}),  ");
            return retPos;
        }

        // 경계선 밖으로 나간경우에도 인식
        Vector3 OutSideToRectPos(Vector3 scrPos, float dist_vp)
        {
            Vector3 uiPos = ScreenPosToRectPos(scrPos);
            float angle = Mathf.Atan2(uiPos.y, uiPos.x) * Mathf.Rad2Deg;
            float radian = angle * Mathf.Deg2Rad;
            float dist = uiPos.magnitude;
            float dist_limit = dist > ball_point_ui_boundary ? ball_point_ui_boundary : dist;
            Vector2 pos_ch = dist_limit * new Vector2(Mathf.Cos(radian), Mathf.Sin(radian));
            //Debug.Log($"pos_ch : {pos_ch}, dist_vp : {dist_vp}, dist : {dist}, angle : {angle}, uiPos : {uiPos}");
            return pos_ch;
        }

        Vector2 ChAngToPos(Vector2 pos, float dir = 1)
        {
            float panAngle = movAngle * dir;
            float angle_basic = Mathf.Atan2(pos.y, pos.x) * Mathf.Rad2Deg; //Vector3.Angle(Vector3.right, pos);
            float angle = angle_basic + panAngle;
            float angle_apply = 0 + angle;
            float radian = angle_apply * Mathf.Deg2Rad;
            float dist = pos.magnitude;
            Vector2 pos_ch = dist * new Vector2(Mathf.Cos(radian), Mathf.Sin(radian));

            //Debug.Log($"pos: {pos}, angle_basic:{angle_basic}, movAngle: {movAngle}, angle : {angle}, dist : {dist}");
            return pos_ch;
        }


        void SetCuePosition(Vector3 normalizedPosition, Vector2 absoluteNormal)
        {
            shotController.SetCueTargetingPosition(normalizedPosition, absoluteNormal);
        }

        public void CueRotateState(Quaternion quaternion)
        {
            Quaternion qua = quaternion;
            transform.rotation = qua;
            this.movAngle = qua.eulerAngles.z;

            Vector3 pos_v = ChAngToPos(point.anchoredPosition);
            point_v.anchoredPosition = pos_v;
            //Debug.Log($"movAngle : {movAngle}, point : {point.anchoredPosition}, pos_v : {pos_v} ");
        }

        void InputOutput_OnMouseState(MouseState mouseState)
        {
            float dist;
            if (mouseState == MouseState.Down)
            {
                //Debug.Log("BallPointBig DOWN");

                if (outLineTypeStart != OutLineType.None) return;
                bool isSide = IsInSide(out dist);
                if (isSide)
                {
                    outLineTypeStart = OutLineType.InSide;
                }
                else
                {
                    outLineTypeStart = OutLineType.OutSide;
                }
            }
            else if (mouseState == MouseState.PressAndStay || mouseState == MouseState.PressAndMove)
            {
                //Debug.Log("BallPointBig PRESS");

                if (mouseState == MouseState.PressAndMove)
                {
                    isDrag = true;
                }
                bool isSide = IsInSide(out dist);
                if (isSide)
                {
                    outLineTypeIng = OutLineType.InSide;
                }
                else
                {
                    outLineTypeIng = OutLineType.OutSide;

                }
                if (outLineTypeStart == OutLineType.InSide)
                //if(outLineTypeIng == OutLineType.InSide)
                {
                    Vector2 uiPos = OutSideToRectPos(Input.mousePosition, dist);
                    Vector2 uiPosIng = Vector2.Lerp(point_v.anchoredPosition, uiPos, Time.deltaTime * 10f);
                    Vector2 uiPosIng_rat = ChAngToPos(uiPosIng, -1);
                    //Debug.Log($"uiPosIng:{uiPos}, uiPosIng:{uiPosIng}, uiPosIng_rat : {uiPosIng_rat}, ABSOLUTE_NORMAL : {uiPosIng / radius}, radius : {radius}");
                    point_v.anchoredPosition = uiPosIng;
                    point.anchoredPosition = uiPosIng_rat;
                    OnCueSpinState?.Invoke(uiPosIng_rat);
                    SetCuePosition(-uiPosIng_rat / radius, uiPosIng / radius);
                }
            }
            else if (mouseState == MouseState.Up)
            {
                //Debug.Log("BallPointBig Up");
                if (outLineTypeStart == OutLineType.None)
                {
                    return;
                }

                if (outLineTypeStart == OutLineType.OutSide)
                {
                    if (isDrag)
                    {
                        isOff = false;
                    }
                    else
                    {
                        isOff = true;
                    }
                }
                else
                {
                    isOff = true;
                }

                outLineTypeStart = OutLineType.None;
                canControl = false;
                isDrag = false;

                if (isOff)
                {
                    gameObject.SetActive(false);
                    blindBack.SetActive(false);
                }
            }

        }



    }
}
