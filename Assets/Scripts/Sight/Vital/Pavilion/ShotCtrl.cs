using Assets.Scripts.Exert.Match;
using Assets.Scripts.Exert.Network;
using Assets.Scripts.Often;
using Assets.Scripts.Sight.Surface.Pavilion;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Sight.Vital.Pavilion
{
    public partial class ShotCtrl : MonoBehaviour
    {

        public static bool canControl = true;
        private TargetEquip _targetEquip = TargetEquip.None;
        public Vector2 pushNormal { get; private set; }
        private Vector3 cueDisplacementOnBall;
        private float cueBallRadius;

        [SerializeField] private Transform ballChecker;
        [SerializeField] private MeshRenderer ballCheckerRenderer;
        [SerializeField] private float lineLength;
        [SerializeField] public LineRenderer[] cueBallLines;
        [SerializeField] private LineRenderer[] targetBallLines;
        [SerializeField] private Transform clothSpace;

        public int clothLayer { get; set; }
        public int boardLayer { get; set; }
        public int ballLayer { get; set; }
        public int cueBallLayer { get; set; }

        public delegate void OnCueRotate(Quaternion quaternion);
        public static event OnCueRotate OnCueRotateState;

        public Ball cueBall { get; private set; }

        public Transform ringCueBallTran;
        public Material ringCueMat;
        public Material dirCueMat;


        [SerializeField] PhysicsMng physicsManager;
        public Transform cuePivotAfterShotPosition;
        public Transform cuePivot;
        public Transform cueVertical;
        public Transform cueDisplacement;
        public Transform cueSlider;

        public Transform turnHandle;
        public Transform forceHandle;
        public SpriteRenderer forceGage;
        public Transform pullBarHandle;


        public bool inShot { get; private set; }
        public bool inMove { get; private set; }        // 볼 이동중일때 참
        float checkTime = 0f;
        [NonSerialized] public Vector3 shotPoint;
        private float cueBallMaxVelocity = 6f;
        private float plusForce = 1f;
        private float minVertical = 5f;
        private float maxVertical = 200f;
        private float cueBallJumpVelocity = 24f;
        private bool isDrawLineEnd = true;
        CancellationTokenSource shotCancel;

        private float pullForceHavey = 50;




        private Vector3 detailTurnStartPos;
        private float detailTurnStartAng;
        private Vector3 detailTurnPrePos;
        private float detailTurnPreAng;
        private bool isRightDir = true;    // 오른쪽방향(상단)
        private bool isIn = true;

        private float changeAagle = 0.1f;



        // 보드회전 boardTurn 
        private Vector3 boardTurnStartPos;
        private float boardTurnStartAng;
        private float boardTurnBasAng;
        private Vector3 boardTurnPrePos;
        private float boardTurnPreAng;
        private bool isBoardTurnBallFind = false;





        float forceSliderMinYPos = 0f;
        float forceSliderMaxYPos = -0.91f;
        float forceSliderYPos;
        float forceSliderToCueRate = 0.5f;
        public float force { get; private set; }
        public float cueSliderDisplacementY { get; private set; }



        float pullSliderBasYPos = 0.31f * DefaultFollowThrough;
        float pullSliderMinYPos = 0f;
        float pullSliderMaxYPos = 0.31f;
        float pullSliderYPos = 0.31f * DefaultFollowThrough;

        public float pull { get; private set; }
        public float pullSliderDisplacementY { get; private set; }


        // 큐 변경사항 체크 용도
        public float cuePivotLocalRotationY { get; private set; }
        public float cueVerticalLocalRotationX { get; private set; }
        public Vector2 cueDisplacementLocalPositionXY { get; private set; }
        public float cueSliderLocalPositionZ{ get; private set; }
        public float pullBarHandleLocalPositionY { get; private set; }

        private PnlMatch pnlMatch;
        //private Impulse impulseFromNetwork;

        private void Awake()
        {

            if (!NetworkManager.initialized)
            {
                enabled = false;
                return;
            }

            Debug.Log($"{ this.GetType().Name } ===== ");

            ballChecker = GameObject.Find("Table/Addition/SupportLines/ChkBall").transform;
            ballCheckerRenderer = GameObject.Find("Table/Addition/SupportLines/ChkBall").GetComponent<MeshRenderer>();
            cueBallLines = new LineRenderer[7];
            cueBallLines[0] = GameObject.Find("Table/Addition/SupportLines/CueLine01").GetComponent<LineRenderer>();
            cueBallLines[1] = GameObject.Find("Table/Addition/SupportLines/CueLine02").GetComponent<LineRenderer>();
            cueBallLines[2] = GameObject.Find("Table/Addition/SupportLines/CueLine03").GetComponent<LineRenderer>();
            cueBallLines[3] = GameObject.Find("Table/Addition/SupportLines/CueLine04").GetComponent<LineRenderer>();
            cueBallLines[4] = GameObject.Find("Table/Addition/SupportLines/CueLine05").GetComponent<LineRenderer>();
            cueBallLines[5] = GameObject.Find("Table/Addition/SupportLines/CueLine06").GetComponent<LineRenderer>();
            cueBallLines[6] = GameObject.Find("Table/Addition/SupportLines/CueLine07").GetComponent<LineRenderer>();
            targetBallLines = new LineRenderer[4];
            targetBallLines[0] = GameObject.Find("Table/Addition/SupportLines/BallLine01").GetComponent<LineRenderer>();
            targetBallLines[1] = GameObject.Find("Table/Addition/SupportLines/BallLine02").GetComponent<LineRenderer>();
            targetBallLines[2] = GameObject.Find("Table/Addition/SupportLines/BallLine03").GetComponent<LineRenderer>();
            targetBallLines[3] = GameObject.Find("Table/Addition/SupportLines/BallLine04").GetComponent<LineRenderer>();
            physicsManager = PhysicsMng.FindObjectOfType<PhysicsMng>();
            cuePivotAfterShotPosition = GameObject.Find("Table/CuePivotAfterShotPosition").transform;
            cuePivot = GameObject.Find("Table/CuePivot").transform;
            cueVertical = GameObject.Find("Table/CuePivot/CueVertical").transform;
            cueDisplacement = GameObject.Find("Table/CuePivot/CueVertical/CueDisplacement").transform;
            cueSlider = GameObject.Find("Table/CuePivot/CueVertical/CueDisplacement/CueSlider").transform;
            turnHandle = GameObject.Find("Table/DetailTurn/DetailTurnPos").transform;
            forceHandle = GameObject.Find("Table/PowerPullSlide/PowerBtn").transform;
            forceGage = GameObject.Find("Table/PowerPullSlide/PowerGage").GetComponent<SpriteRenderer>();
            pullBarHandle = GameObject.Find("Table/PowerPullSlide/PullBar").transform;


            clothLayer = LayerLib.NameToInt(LayerKind.Cloth);
            boardLayer = LayerLib.NameToInt(LayerKind.Board);
            ballLayer = LayerLib.NameToInt(LayerKind.Ball);
            cueBallLayer = LayerLib.NameToInt(LayerKind.CueBall);

            SetFollowThrough(DefaultFollowThrough);
            lineLength = 1.6f;


            inShot = false;
            inMove = false;

        }

        private void OnEnable()
        {
            InputOutput.OnMouseState += InputOutput_OnMouseState;

        }

        private void OnDisable()
        {
            InputOutput.OnMouseState -= InputOutput_OnMouseState;
            prediction?.Cancel(); pendingPreview = false; predictionDirty = true;

        }

        private void Start()
        {
            physicsManager = PhysicsMng.FindObjectOfType<PhysicsMng>();
            pnlMatch = PnlMatch.FindObjectOfType<PnlMatch>();
            cueBallLineMaterials = Array.ConvertAll(cueBallLines, item => item.material);
            targetBallLineMaterials = Array.ConvertAll(targetBallLines, item => item.material);
            CreateGaugeValueLabels(); CreateOverlapPreview();

        }


        void InputOutput_OnMouseState(MouseState mouseState)
        {
            if (!canControl) return;

            if (PoolLogic.controlFromNetwork)
            {
                return;
            }

            if (mouseState == MouseState.Down)
            {
                // 길게치기
                if(InputOutput.targetEquip == TargetEquip.SlidePull)
                {
                    _targetEquip = TargetEquip.SlidePull;
                }
                // 파워조절
                else if(InputOutput.targetEquip == TargetEquip.SlidePower)
                {
                    _targetEquip = TargetEquip.SlidePower;
                }
                // 표적방향
                else if(InputOutput.targetEquip == TargetEquip.TableBoard)
                {
                    boardTurnStartPos = InputOutput.mouseWorldPosition;
                    float boardTurnStartAng = CalculateAngle2(cuePivot.position, boardTurnStartPos);
                    boardTurnBasAng = boardTurnStartAng - cuePivot.localRotation.eulerAngles.y;
                    if (Mathf.Abs(boardTurnBasAng) > 180) if (boardTurnBasAng > 0) boardTurnBasAng = boardTurnBasAng - 360; else boardTurnBasAng = 360 + boardTurnBasAng;

                    isBoardTurnBallFind = ChkBallPos(boardTurnStartPos);
                    boardTurnPrePos = boardTurnStartPos;
                    boardTurnPreAng = boardTurnBasAng;

                    _targetEquip = TargetEquip.TableBoard;
                }
                // 세부방향
                else if (InputOutput.targetEquip == TargetEquip.DetailTurn)
                {
                    detailTurnStartPos = InputOutput.mouseWorldPosition;
                    isRightDir = detailTurnStartPos.z >= turnHandle.position.z;
                    detailTurnStartAng = CalculateAngle(turnHandle.position, detailTurnStartPos);
                    detailTurnPrePos = detailTurnStartPos;
                    detailTurnPreAng = detailTurnStartAng;
                    _targetEquip = TargetEquip.DetailTurn;
                }
            }

            if (mouseState == MouseState.PressAndMove || mouseState == MouseState.PressAndStay || mouseState == MouseState.Up)
            {
                // 길게치기
                if (_targetEquip == TargetEquip.SlidePull)
                {
                    OnSlidePull(mouseState);
                }
                // 파워조절
                else if (_targetEquip == TargetEquip.SlidePower)
                {
                    OnSlidePower(mouseState);
                }
                // 표적방향
                else if (_targetEquip == TargetEquip.TableBoard)
                {
                    OnTableBoard(mouseState);
                    TryCalculateShot(cueChanged);
                }
                // 세부방향
                else if (_targetEquip == TargetEquip.DetailTurn)
                {
                    OnDetailTurn(mouseState);
                    TryCalculateShot(true);
                }
            }

            if (mouseState == MouseState.Up)
            {
                _targetEquip = TargetEquip.None;
            }


        }


        // 큐대를 조준 안하고 잠시둔다. (추후 UI파워 비활성화 처리하기)
        public void CuePutAside()
        {
            cueVertical.parent = cuePivotAfterShotPosition;
            cueVertical.localPosition = Vector3.zero;
            cueVertical.localRotation = Quaternion.identity;
            cueDisplacement.localPosition = Vector3.zero;

            GuideLineReset();

            //Debug.Log("CuePutAside");
        }

        // 조준하기위해 큐대를 큐볼위치로 이동 
        public void CueReadyShot()
        {
            // 선수별 큐볼지정
            cueBall?.SetCueBall(false);
            cueBall = null;
            int id = PoolPlayer.turnId;
            cueBall = physicsManager.balls[id];
            //physicsManager.cuball = cueBall.GetComponent<Rigidbody>();
            cueBall.SetCueBall(true);
            //Debug.Log($"CueReadyShot PoolPlayer.turnId : {PoolPlayer.turnId} .. cueBall : {cueBall.id}  ....");

            cuePivot.position = cueBall.transform.position;
            cueVertical.parent = cuePivot;
            cueVertical.localPosition = Vector3.zero;
            cueVertical.localRotation = Quaternion.identity;
            cueDisplacement.localPosition = Vector3.zero;
            //Debug.Log($" CueReadyShot parent : {cuePivot.parent.name} ");


        }


        // 세부방향 조절 (드래그, 클릭)
        void OnDetailTurn(MouseState mouseState)
        {
            //Debug.Log($"OnDetailTurn mouseState : {mouseState}");
            bool isDrag = true;
            float angleCueY = cuePivot.localRotation.eulerAngles.y;
            float anglesZ = turnHandle.localRotation.eulerAngles.z;
            float ang_up;
            float ang_gap;
            float ang_gap_pan;
            float rot;
            if (mouseState == MouseState.PressAndMove)
            {

                isRightDir = InputOutput.mouseWorldPosition.z >= detailTurnPrePos.z;
                rot = isRightDir ? -1f : 1f;
                ang_up = CalculateAngle(turnHandle.position, InputOutput.mouseWorldPosition);
                ang_gap = Mathf.Abs(ang_up - detailTurnPreAng);

                int ang_new = (int)(anglesZ + ang_gap * rot);
                //Debug.Log($"ang_new : {ang_new}, ang_gap : {(ang_gap * rot)}, anglesZ : {anglesZ}");

                if ((int)ang_gap == 0)
                {
                    //Debug.Log("ang_gap == 0");
                    return;
                }
                else
                {
                    cuePivot.localRotation = Quaternion.Euler(0, (int)(angleCueY + ang_gap * rot), 0);
                    turnHandle.localRotation = Quaternion.Euler(0, 0, ang_new);
                    detailTurnPrePos = InputOutput.mouseWorldPosition;
                    detailTurnPreAng = CalculateAngle(turnHandle.position, detailTurnPrePos);

                    OnCueRotateState?.Invoke(cuePivot.rotation.normalized);
                }

            }
            // 세부조절 종료 또는 클릭처리
            else if (mouseState == MouseState.Up)
            {
                if (detailTurnStartPos == InputOutput.mouseWorldPosition)
                {
                    isDrag = false;
                }
                if (!isDrag)
                {
                    ang_gap = .05f;
                    ang_gap_pan = 1f;
                    rot = isRightDir ? -1f : 1f;
                    turnHandle.localRotation = Quaternion.Euler(0, 0, anglesZ + (ang_gap_pan * rot));
                    cuePivot.localRotation = Quaternion.Euler(0, angleCueY + (ang_gap * rot), 0);
                    OnCueRotateState?.Invoke(cuePivot.rotation.normalized);
                    physicsManager.ringCue.ArrowDir(rot);
                    //DetailRotationArrowFade(rot).Forget();
                }
            }
        }


        // 세부조절시 방향 표시하기
        async UniTaskVoid DetailRotationArrowFade(float dir)
        {
            float a = 1f;
            float timer = 0;
            dirCueMat.SetFloat("_Dir", dir);
            //Debug.Log($"dir : {dir} ============ ");
            while (0 < a)
            {
                timer += Time.deltaTime;
                //Debug.Log($"dir : {dir}, a : {a} ");
                dirCueMat.SetFloat("_Alpha", a);
                a -= 0.05f;
                await UniTask.Yield();
            }
            //Debug.Log($"timer : {timer} .... ");

        }

        // 당점처리 
        public void SetCueTargetingPosition(Vector3 normalizedPosition, Vector2 absoluteNormal)
        {

            pushNormal = absoluteNormal;
            Vector3 spin = cueDisplacementOnBall - normalizedPosition * cueBallRadius;
            cueDisplacement.localPosition = spin;
        }





        // 구간에 해당 각도 계산 
        public float CalculateAngle(Vector3 from, Vector3 to)
        {
            to.y = 0;
            from.y = 0;
            Vector3 vec = Quaternion.FromToRotation(Vector3.right, to - from).eulerAngles;
            return vec.y;
        }

        public float CalculateAngle2(Vector3 from, Vector3 to)
        {
            to.y = 0;
            from.y = 0;

            float ang = Mathf.Atan2(to.z - from.z, to.x - from.x) * Mathf.Rad2Deg;
            ang = ang - 90;
            if (0 < ang && ang <= 90)
            {
                ang = ang - 360;
            }
            //Debug.Log($"CalculateAngle to : {to}, from : {from} ... vec : {vec.y}");
            return Mathf.Abs(ang);
        }

        public float CalculateAngle3(Vector3 from, Vector3 to)
        {
            to.y = 0;
            from.y = 0;
            float ang = Vector3.SignedAngle(from, to, Vector3.up);

            //Debug.Log($"CalculateAngle to : {to}, from : {from} ... vec : {vec.y}");
            return ang - 00;
        }

        // 길게치기 
        void OnSlidePull(MouseState mouseState)
        {
            //Debug.Log($"OnSlidePull mouseState : {mouseState}");
            // 드래그 중
            if (mouseState == MouseState.PressAndMove)
            {
                //Debug.Log($"pullSliderMinYPos : {pullSliderMinYPos}, pullSliderYPos : {pullSliderYPos}, speed : {InputOutput.mouseWorldSpeed.z}");

                pullSliderYPos += InputOutput.mouseWorldSpeed.z * Time.deltaTime;
                if (pullSliderYPos > pullSliderMaxYPos)
                {
                    pullSliderYPos = pullSliderMaxYPos;
                }
                else if (pullSliderYPos <= pullSliderMinYPos)
                {
                    pullSliderYPos = pullSliderMinYPos;
                }

                pull = Normal(Mathf.Abs(pullSliderYPos), Mathf.Abs(pullSliderMaxYPos));         // 정규화(0~1)
                pullSliderDisplacementY = Mathf.Lerp(pullSliderMinYPos, pullSliderMaxYPos, pull);
                //Debug.Log($"pull : {pull}, pullSliderMinYPos : {pullSliderMinYPos}, pullSliderMaxYPos : {pullSliderMaxYPos}");
                pullBarHandle.localPosition = new Vector3(0, pullSliderDisplacementY, 0);

            }
            else if (mouseState == MouseState.Up)
            {
                //force = Normal(Mathf.Abs(forceSliderYPos), Mathf.Abs(forceSliderMaxYPos));
                //Debug.Log($"SlidePower Up - force : {force} *********** ");
                ///StartCoroutine(WaitAndStartShot());
            }

        }

        // 파워조절
        void OnSlidePower(MouseState mouseState)
        {
            //Debug.Log($"OnSlidePower mouseState : {mouseState}");

            // 파워스위치 드래그 중 
            if (mouseState == MouseState.PressAndMove)
            {
                //Debug.Log($"forceSliderYPos : {forceSliderYPos}, speed : {InputOutput.mouseWorldSpeed.z}");

                forceSliderYPos += InputOutput.mouseWorldSpeed.z * Time.deltaTime;
                if (forceSliderYPos <= forceSliderMaxYPos)
                {
                    forceSliderYPos = forceSliderMaxYPos;
                }
                else if (forceSliderYPos > forceSliderMinYPos)
                {
                    forceSliderYPos = forceSliderMinYPos;
                }

                force = Normal(Mathf.Abs(forceSliderYPos), Mathf.Abs(forceSliderMaxYPos));
                cueSliderDisplacementY = Mathf.Lerp(forceSliderMinYPos, forceSliderMaxYPos, force);
                forceHandle.localPosition = new Vector3(0, cueSliderDisplacementY, 0);
                Vector3 disZ = new Vector3(0, 0, cueSliderDisplacementY * forceSliderToCueRate);
                cueSlider.localPosition = disZ;
                float gage_height = Mathf.Lerp(1f, 6.5f, force);
                forceGage.size = new Vector2(0.75f, gage_height);

                float gage_y = Mathf.Lerp(0.050f, -0.415f, force);
                forceGage.transform.localPosition = new Vector3(0.0f, gage_y, 0);
                float concave = Mathf.Lerp(3, 0.7f, force);
                forceGage.material.SetVector("_Remap", new Vector2(-1, concave));

                //ringCueMat.SetFloat("_Percentage", force);
                physicsManager.ringCue.PercentageVal(force);
            }
            else if (mouseState == MouseState.Up)
            {
                force = Normal(Mathf.Abs(forceSliderYPos), Mathf.Abs(forceSliderMaxYPos));
                // Debug.Log($"SlidePower Up - force : {force} *********** ");
                if (force == 0) return;
                WaitAndStartShot("").Forget();
            }

        }

        float Normal(float val, float max, float min = 0)
        {
            float ret = 0;
            float rate = 1.0f / (max - min);
            if (val <= min) ret = 0;
            if (val >= max) ret = 1;
            ret = val * rate;
            //Debug.Log($"rate : {rate}, val : {val}, ret : {ret}");
            return ret;
        }




        // 스트로크 실행
        public async UniTaskVoid WaitAndStartShot(string impulse_str)
        {

            physicsManager.moveTime = 0;
            physicsManager.endFromNetwork = false;

            inShot = true;
            IsShotAnimating = true;
            inMove = true;

            // 라인초기화 
            //GuideLineReset();
            //Debug.Log("WaitAndStartShot force : " + force + ", pullSliderYPos : " + pullSliderYPos);
            float checkTime = 0f;
            int stepCnt = 4;
            Impulse impulse;

            forceGage.size = new Vector2(0.75f, 0f);
            forceGage.transform.localPosition = new Vector3(0.0f, 0.065f, 0);
            forceGage.material.SetVector("_Remap", new Vector2(-1, 10));


            forceSliderMinYPos = pullSliderYPos;


            float forceOneStep = Mathf.Abs((forceHandle.localPosition.y - forceSliderMinYPos) / stepCnt);
            // 파워스위치 애니
            while (checkTime < .4f && forceHandle.localPosition.y <= forceSliderMinYPos)
            {
                checkTime += Time.deltaTime;
                Vector3 ff = new Vector3(0, 0, forceOneStep * forceSliderToCueRate);// Vector3.forward * force;
                cueSlider.localPosition += ff;
                Vector3 fh = new Vector3(0, forceOneStep, 0);
                forceHandle.localPosition += fh;
                if (forceHandle.localPosition.y > forceSliderMinYPos)
                {
                    forceHandle.localPosition = new Vector3(0, forceSliderMinYPos, 0);
                    break;
                }
                await UniTask.Yield();
            }
            /////////////////////////////////////////////////////////////////////////////
            float ang = cuePivot.localEulerAngles.y;


            if (PoolLogic.controlInNetwork)
            {
                impulse = BuildShotImpulse();

                physicsManager.StartReplayShot(DataManager.ImpulseToString(impulse));
                await UniTask.Yield();

            }
            else
            {
                impulse = DataManager.ImpulseFromString(impulse_str); ;
            }


            physicsManager.StartShot(cueBall, impulse, "");

            /////////////////////////////////////////////////////////////////////////////
            //WaitAndStartShotStart();
            await UniTask.Delay(500);

            stepCnt = 15;
            forceSliderMinYPos = 0;
            forceOneStep = Mathf.Abs((forceHandle.localPosition.y - forceSliderMinYPos) / stepCnt);
            checkTime = 0f;
            while (checkTime < .5f && forceHandle.localPosition.y >= 0)
            {
                checkTime += Time.deltaTime;
                Vector3 ff = new Vector3(0, 0, -forceOneStep * forceSliderToCueRate);
                cueSlider.localPosition += ff;
                Vector3 fh = new Vector3(0, -forceOneStep, forceSliderMinYPos);
                forceHandle.localPosition += fh;
                if (pullBarHandle.localPosition.y > pullSliderBasYPos)
                {
                    pullBarHandle.localPosition += fh;
                }

                if (forceHandle.localPosition.y < forceSliderMinYPos)
                {
                    forceHandle.localPosition = Vector3.zero;
                    break;
                }
                await UniTask.Yield();
            }

            inMove = false;
            pullSliderYPos = pullSliderBasYPos;
            //forceSliderMinYPos = pullSliderYPos;
            forceSliderYPos = forceSliderMinYPos;
            SetFollowThrough(DefaultFollowThrough);
            pullBarHandle.localPosition = new Vector3(0, pullSliderBasYPos, 0);

            forceHandle.localPosition = Vector3.zero;
            cueSlider.localPosition = Vector3.zero;

            forceGage.size = new Vector2(0.75f, 1f);
            forceGage.transform.localPosition = new Vector3(0.0f, 0.065f, 0);

            IsShotAnimating = false;
            force = 0;
            CuePutAside(); // 

            //cuePosHanger();
        }


        // 예상 안내선 초기화 
        void GuideLineReset()
        {

            ballChecker.position = new Vector3(0, 0, 2f);
            for (int i = 0; i < cueBallLines.Length; i++) cueBallLines[i].positionCount = 0;
            for (int i = 0; i < targetBallLines.Length; i++) targetBallLines[i].positionCount = 0;

            //Debug.Log($"ShotCtrl.GuideLineReset");
            //pnlMatch.PoolCoach_OnSetGameInfo($"GuideLineReset  cueBallLines.Length : {cueBallLines.Length} ");
            //Debug.Log($"GuideLineReset  cueBallLines.Length : {cueBallLines.Length} ");
        }

        // 
        private float maxVelocity
        {
            get
            {
                float jumpFactor = 0;
                float verticalAngleFactor = cueVertical.localRotation.eulerAngles.x / (maxVertical - minVertical) > 0.7f ? 1.0f : 0.0f;
                //Debug.Log($"angX : {cueVertical.localRotation.eulerAngles.x}, max: {maxVertical}, min : {minVertical}, verticalAngleFactor : {verticalAngleFactor}, jumpFactor : {jumpFactor}, cueBallMaxVelocity : {cueBallMaxVelocity}, cueBallJumpVelocity : {cueBallJumpVelocity} ");
                return Mathf.Lerp(cueBallMaxVelocity, cueBallJumpVelocity, jumpFactor * verticalAngleFactor);
            }
        }

        public bool cueChanged
        {
            get
            {
                if(cuePivotLocalRotationY != cuePivot.localRotation.eulerAngles.y || 
                    cueVerticalLocalRotationX != cueVertical.localRotation.eulerAngles.x ||
                    cueDisplacementLocalPositionXY != new Vector2(cueDisplacement.localPosition.x, cueDisplacement.localPosition.y) ||
                    cueSliderLocalPositionZ != cueSlider.localPosition.z ||
                    lastSentPower != force || lastSentFollowPosition != pullBarHandle.localPosition.y)
                {
                    //Debug.Log($"cueChanged {cuePivotLocalRotationY != cuePivot.localRotation.eulerAngles.y} ... {cuePivotLocalRotationY} :: {cuePivot.localRotation.eulerAngles.y}");

                    cuePivotLocalRotationY = cuePivot.localRotation.eulerAngles.y;
                    cueVerticalLocalRotationX = cueVertical.localRotation.eulerAngles.x;
                    cueDisplacementLocalPositionXY = new Vector2(cueDisplacement.localPosition.x, cueDisplacement.localPosition.y);
                    cueSliderLocalPositionZ = cueSlider.localPosition.z;
                    lastSentPower = force; lastSentFollowPosition = pullBarHandle.localPosition.y;
                    return true;
                }
                else
                {
                    //Debug.Log($"cueChanged false");
                    return false;
                }
            }
        }

 

        void OnTableBoard(MouseState mouseState)
        {
            //Debug.Log($"OnTableBoard mouseState : {mouseState}");

            if (mouseState == MouseState.PressAndStay)
            {
                if (isBoardTurnBallFind)
                {
                    Vector3 mouseWorldDirectionCuePivot = Vector3.ProjectOnPlane(InputOutput.mouseWorldPosition - cuePivot.position, Vector3.up).normalized;
                    Quaternion cuePivotRotation = Quaternion.identity;
                    cuePivotRotation.SetLookRotation(mouseWorldDirectionCuePivot);
                    //Quaternion cuePivotRotationLerp = Quaternion.Lerp(cuePivot.rotation, cuePivotRotation, 10.0f * Time.deltaTime);
                    cuePivot.rotation = cuePivotRotation;
                    OnCueRotateState?.Invoke(cuePivotRotation);
                    physicsManager.ringCue.transform.rotation = cuePivotRotation;
                    isBoardTurnBallFind = false;
                }
            }

            if (mouseState == MouseState.PressAndMove)
            {
                isBoardTurnBallFind = false;
                boardTurnStartPos = InputOutput.mouseWorldPosition;
                float boardTurnStartAng = CalculateAngle2(cuePivot.position, boardTurnStartPos);
                boardTurnBasAng = boardTurnStartAng - cuePivot.localRotation.eulerAngles.y;
                if (Mathf.Abs(boardTurnBasAng) > 180) if (boardTurnBasAng > 0) boardTurnBasAng = boardTurnBasAng - 360; else boardTurnBasAng = 360 + boardTurnBasAng;


                float boardTurnBasAng_a = boardTurnBasAng;
                if (Mathf.Abs(boardTurnBasAng) > 100 && Mathf.Abs(boardTurnPreAng) > 100)
                {
                    if (boardTurnBasAng > 0 && boardTurnPreAng < 0)
                    {
                        boardTurnBasAng_a = boardTurnBasAng - 360;
                    }
                    else if (boardTurnBasAng < 0 && boardTurnPreAng > 0)
                    {
                        boardTurnBasAng_a = boardTurnBasAng + 360;
                    }
                }

                float ang_gap = boardTurnBasAng_a - boardTurnPreAng;
                //Debug.Log($"절대각도 : {boardTurnStartAng}, 큐대각도 : {cuePivot.localRotation.eulerAngles.y}, 상대각도 : {boardTurnBasAng}, 변경각도 : {ang_gap}");
                Quaternion cuePivotRotation = Quaternion.Euler(0, ang_gap * 0.2f, 0) * cuePivot.rotation;
                Quaternion cuePivotRotationLerp = Quaternion.Lerp(cuePivot.rotation, cuePivotRotation, 20.0f * Time.deltaTime);

                cuePivot.rotation = cuePivotRotation;// cuePivotRotationLerp;
                OnCueRotateState?.Invoke(cuePivotRotation);
                physicsManager.ringCue.transform.rotation = cuePivotRotation;

                boardTurnPrePos = boardTurnStartPos;
                boardTurnPreAng = boardTurnBasAng;
            }

            //if (mouseState == MouseState.PressAndStay || mouseState == MouseState.PressAndMove)
            //{
            //    Vector3 mouseWorldDirectionCuePivot = Vector3.ProjectOnPlane(InputOutput.mouseWorldPosition - cuePivot.position, Vector3.up).normalized;
            //    Quaternion cuePivotRotation = Quaternion.identity;
            //    cuePivotRotation.SetLookRotation(mouseWorldDirectionCuePivot);

            //    Quaternion cuePivotRotationLerp = Quaternion.Lerp(cuePivot.rotation, cuePivotRotation, 10.0f * Time.deltaTime);
            //    cuePivot.rotation = cuePivotRotationLerp;
            //    OnCueRotateState?.Invoke(cuePivotRotationLerp);
            //    physicsManager.ringCue.transform.rotation = cuePivotRotationLerp;

            //}
        }


        void TryCalculateShot(bool forceCalculate)
        {
            // The shared prediction renderer detects aim/contact changes in LateUpdate.
            if (forceCalculate) predictionDirty = true;
        }


        // isBallHit 적구 타격 여부, isCueball
        void DrawBallHitLine(int cnt, Vector3 origin, Vector3 direction, float line_len, bool isBallHit = false, bool isCueball = true)
        {
            LineRenderer[] lines = isCueball ? cueBallLines : targetBallLines;
            //Debug.Log($"DrawBallHitLine cnt : {cnt}");

            RaycastHit targetShapeHit;
            // isBallHit || !isCueball : 보드만 확인하면됨.  ... !isBallHit : 적구가 타격전 상태, !isCueball : 적구임
            if (Physics.SphereCast(origin, cueBallRadius, direction, out targetShapeHit, cnt == 0 && isCueball ? 3f : line_len, isBallHit || !isCueball ? boardLayer : (ballLayer | boardLayer)))
            {
                Ball listener = targetShapeHit.collider.gameObject.GetComponent<Ball>();

                // 적구와 충돌시 
                if (listener)
                {
                    isBallHit = true;       // 적구는 한번만 체크 함.
                    // 적구타격시수구위치 = 적구위치 + 수구반지름 * 타격방향
                    Vector3 positionInHit = targetShapeHit.point + cueBallRadius * targetShapeHit.normal;
                    // 볼체커위치설정 ...

                    Vector3 targetHit_normal = Vector3.ProjectOnPlane(targetShapeHit.normal, Vector3.up).normalized;
                    Vector3 new_direction = Vector3.Cross(Vector3.up, targetHit_normal);
                    float tangent = Vector3.Dot(Vector3.ProjectOnPlane(direction, Vector3.up), new_direction);
                    if (tangent < 0) new_direction = -new_direction;
                    float moved_line_dist = Vector3.Distance(origin, positionInHit);                    // 출발점에서 충돌지점간 거리
                    float line_remain = cnt == 0 ? line_len : line_len - moved_line_dist;                 // 첫시작에서는 남은거리 계산 필요없음.
                    float new_line_len = Mathf.Clamp(Mathf.Abs(tangent), 0.2f, 1.0f) * line_remain;     // 적구와 충돌시 데미지 적용
                    //Vector3 new_target_position = positionInHit + new_line_len * new_direction;
                    float figure = 1 - Mathf.Abs(tangent);

                    //Debug.Log($"figure : { figure }  , target.id : {listener.id}, target_name : {listener.name} ---------------------------------  ");
                    // ringCueMat.SetFloat("_Figure", 0.5f);
                    physicsManager.ringCue.FigureVal(figure);

                    lines[cnt].positionCount = 2;
                    lines[cnt].SetPosition(0, origin);
                    lines[cnt].SetPosition(1, positionInHit);

                    //ballChecker.position = positionInHit;
                    SetBallChecker(positionInHit, listener.id);

                    float cueTiling = lineDistToTiling(moved_line_dist);
                    lines[cnt].material.SetVector("_Tiling", new Vector2(cueTiling, 1));

                    Vector3 targetPosition = listener.GetComponent<Rigidbody>().position;
                    Vector3 targetDirection = (listener.GetComponent<Rigidbody>().position - positionInHit).normalized;
                    float target_len = Mathf.Clamp(1.0f - Mathf.Abs(tangent), 0.2f, 1.0f) * line_len;
                    DrawBallHitLine(0, targetPosition, targetDirection, target_len, true, false);     // 적구 충돌체크

                    //string mm = "";
                    //if (listener.id == 1)
                    //{
                    //    mm = "    ...........";
                    //    physicsManager.ringBalls[0].GetComponent<MeshRenderer>().material.SetFloat("_Alpha", 1.0f);
                    //    physicsManager.ringBalls[1].GetComponent<MeshRenderer>().material.SetFloat("_Alpha", 0.2f);
                    //}
                    //else if (listener.id == 2)
                    //{
                    //    physicsManager.ringBalls[1].GetComponent<MeshRenderer>().material.SetFloat("_Alpha", 1.0f);
                    //    physicsManager.ringBalls[0].GetComponent<MeshRenderer>().material.SetFloat("_Alpha", 0.2f);
                    //    mm = "   ****************";
                    //}

                    //Debug.Log($"listener.id : " + listener.id + mm);

                    //Debug.Log($"cueball and target hit cnt : {cnt}, line_len : {line_len}, target_len : {target_len}  ");

                    cnt++;
                    DrawBallHitLine(cnt, positionInHit, new_direction, new_line_len, isBallHit, isCueball);
                }
                // 보더와 충돌시
                else
                {



                    //physicsManager.ringBalls[0].GetComponent<MeshRenderer>().material.SetFloat("_Alpha", 0.2f);
                    //physicsManager.ringBalls[1].GetComponent<MeshRenderer>().material.SetFloat("_Alpha", 0.2f);

                    Vector3 positionInHit = targetShapeHit.point + cueBallRadius * targetShapeHit.normal;

                    lines[cnt].positionCount = 2;
                    lines[cnt].SetPosition(0, origin);
                    lines[cnt].SetPosition(1, positionInHit);

                    Vector3 projectOnNormal = Vector3.Project(Vector3.ProjectOnPlane(direction, Vector3.up), Vector3.ProjectOnPlane(targetShapeHit.normal, Vector3.up).normalized);
                    Vector3 new_direction = Vector3.ProjectOnPlane(direction, Vector3.up) - 2.0f * projectOnNormal;

                    float moved_line_dist = Vector3.Distance(origin, positionInHit);                    // 출발점에서 충돌지점간 거리
                    float line_remain = cnt == 0 && isCueball ? line_len : line_len - moved_line_dist;                 // 첫시작에서는 남은거리 계산 필요없음.
                    float new_line_len = line_remain;                                                   // 

                    float cueTiling = lineDistToTiling(moved_line_dist);
                    lines[cnt].material.SetVector("_Tiling", new Vector2(cueTiling, 1));

                    //Debug.Log($"cueball board hit cnt : {cnt}, line_len : {line_len}  ");

                    //if (listener.id == 1)
                    //{
                    //    physicsManager.ringBalls[0].GetComponent<MeshRenderer>().material.SetFloat("_Alpha", 0.3f);
                    //}
                    //else if (listener.id == 2)
                    //{
                    //    physicsManager.ringBalls[1].GetComponent<MeshRenderer>().material.SetFloat("_Alpha", 0.3f);
                    //}

                    cnt++;
                    DrawBallHitLine(cnt, positionInHit, new_direction, new_line_len, isBallHit, isCueball);

                }

            }
            // 충돌이 없다면
            else
            {
                Vector3 line_end_position = origin + line_len * Vector3.ProjectOnPlane(direction, Vector3.up);
                lines[cnt].positionCount = 2;
                lines[cnt].SetPosition(0, origin);
                lines[cnt].SetPosition(1, line_end_position);

                float moved_line_dist = Vector3.Distance(origin, line_end_position);                    // 출발점에서 종료지점간 거리

                // 적구와 부딪히지 않았다면 안 보이게 위치를 초기화 , 적구라인 초기화
                if (!isBallHit)
                {
                    SetBallChecker(Vector3.zero, -1);

                    //ballChecker.position = new Vector3(0, 0, 2f);

                    if (isCueball)
                    {
                        for (int i = 0; i < targetBallLines.Length; i++)
                        {
                            targetBallLines[i].positionCount = 0;
                        }
                    }
                }

                // 처음이 아니면 화살표의 개수를 지정
                if ((cnt > 0 && isCueball) || !isCueball)
                {
                    float cueTiling = lineDistToTiling(moved_line_dist);
                    lines[cnt].material.SetVector("_Tiling", new Vector2(cueTiling, 1));
                }

                //Debug.Log($"cueball line end >>>> cnt : {cnt}, moved_line_dist : {moved_line_dist}  ");

                // 종료 후 남은 라인들은 초기화
                cnt++;
                for (int i = cnt; i < lines.Length; i++)
                {
                    //Debug.Log($"cueball line reset {i}");
                    lines[i].positionCount = 0;
                }

                isDrawLineEnd = true;
            }
        }

        float lineDistToTiling(float dist)
        {
            float ret = 1;
            ret = dist * 20;

            return ret;
        }



        void SetBallChecker(Vector3 pos, int id)
        {

            if (id == cueBall.id || id == -1)
            {
                physicsManager.ringBalls[0].GetComponent<MeshRenderer>().material.SetFloat("_Alpha", 0.3f);
                physicsManager.ringBalls[1].GetComponent<MeshRenderer>().material.SetFloat("_Alpha", 0.3f);
                ballChecker.position = new Vector3(0, 0, 2f);
                //ringCueMat.SetFloat("_Figure", 0);
                physicsManager.ringCue.FigureVal(0);
            }
            else
            {
                ballChecker.position = pos;
                if (id == 0 || id == 1)
                {
                    physicsManager.ringBalls[0].GetComponent<MeshRenderer>().material.SetFloat("_Alpha", 1.0f);
                    physicsManager.ringBalls[1].GetComponent<MeshRenderer>().material.SetFloat("_Alpha", 0.3f);
                }
                else
                {
                    physicsManager.ringBalls[0].GetComponent<MeshRenderer>().material.SetFloat("_Alpha", 0.3f);
                    physicsManager.ringBalls[1].GetComponent<MeshRenderer>().material.SetFloat("_Alpha", 1.0f);
                }
            }

            //Debug.Log("SetBallChecker _Figure 00 ");



        }


        // 큐 사용여부 지정
        // 가이드라인 안보이게 시작
        public void OnEnableControl(bool value)
        {
            if (value)
            {

            }
            else
            {

            }

            SetCueBall();
            //if (cueChanged)
            //{
            //}

            //this.cuePivotLocalRotationY = cuePivot.localRotation.eulerAngles.y;
            //this.cueVerticalLocalRotationX = cueVertical.localRotation.eulerAngles.x;
            //this.cueDisplacementLocalPositionXY = new Vector2(cueDisplacement.localPosition.x, cueDisplacement.localPosition.y);
            //this.cueSliderLocalPositionZ = cueSlider.localPosition.z;
            ////this.force = force;
            //this.pullBarHandleLocalPositionY = pullBarHandle.localPosition.y;


            //this.cuePivotLocalRotationY = cuePivotLocalRotationY;
            //this.cueVerticalLocalRotationX = cueVerticalLocalRotationX;
            //this.cueDisplacementLocalPositionXY = cueDisplacementLocalPositionXY;
            //this.cueSliderLocalPositionZ = cueSliderLocalPositionZ;
            //this.force = force;
            //this.pullBarHandleLocalPositionY = pullBarHandle.localPosition.y;

        }


        /// <summary>
        /// 큐움직임 수신
        /// </summary>
        /// <param name="cuePivotLocalRotationY"></param>
        /// <param name="cueVerticalLocalRotationX"></param>
        /// <param name="cueDisplacementLocalPositionXY"></param>
        /// <param name="cueSliderLocalPositionZ"></param>
        /// <param name="force"></param>
        public void CueControlFromNetwork(float cuePivotLocalRotationY, float cueVerticalLocalRotationX,
            Vector2 cueDisplacementLocalPositionXY, float cueSliderLocalPositionZ, float force, float pullBarHandleLocalPositionY)
        {

            if (this.cuePivotLocalRotationY != cuePivotLocalRotationY ||
                   this.cueVerticalLocalRotationX != cueVerticalLocalRotationX ||
                   this.cueDisplacementLocalPositionXY != cueDisplacementLocalPositionXY ||
                   this.cueSliderLocalPositionZ != cueSliderLocalPositionZ ||
                   this.force != force || this.pullBarHandleLocalPositionY != pullBarHandleLocalPositionY)
            {

            }
            else
            {
                return;
            }
            
            //Debug.Log("CueControlFromNetwork cuePivotLocalRotationY : " + cuePivotLocalRotationY + ", cueVerticalLocalRotationX : " + cueVerticalLocalRotationX + ", " + ", cueSliderLocalPositionZ : " + cueSliderLocalPositionZ + ", force : " + force);
            this.cuePivotLocalRotationY = cuePivotLocalRotationY;
            this.cueVerticalLocalRotationX = cueVerticalLocalRotationX;
            this.cueDisplacementLocalPositionXY = cueDisplacementLocalPositionXY;
            this.cueSliderLocalPositionZ = cueSliderLocalPositionZ;
            this.force = force;
            this.pullBarHandleLocalPositionY = pullBarHandleLocalPositionY;
            //this.cueSliderDisplacementZ = Mathf.Lerp(0, -cueSlidingMaxDisplacement, force);
        }


        public void UpdateFromNetwork()
        {
            //if (cueChanged) 
            //{
            //    Debug.Log($"UpdateFromNetwork cueChanged true");
            //}

            cuePivot.localRotation = Quaternion.Lerp(cuePivot.localRotation, Quaternion.Euler(0.0f, cuePivotLocalRotationY, 0.0f), 5.0f * Time.deltaTime);
            cueVertical.localRotation = Quaternion.Lerp(cueVertical.localRotation, Quaternion.Euler(cueVerticalLocalRotationX, 0.0f, 0.0f), 5.0f * Time.deltaTime);
            cueDisplacement.localPosition = Vector3.Lerp(cueDisplacement.localPosition, new Vector3(cueDisplacementLocalPositionXY.x, cueDisplacementLocalPositionXY.y, 0.0f), 5.0f * Time.deltaTime);
            //targeting2DManager.SetPointTargetingPosition(-cueDisplacement.localPosition / cueBallRadius);
            cueSlider.localPosition = Vector3.Lerp(cueSlider.localPosition, new Vector3(0.0f, 0.0f, cueSliderLocalPositionZ), 5.0f * Time.deltaTime);
            pullBarHandle.localPosition = Vector3.Lerp(pullBarHandle.localPosition, new Vector3(0.0f, pullBarHandleLocalPositionY, 0.0f), 5.0f * Time.deltaTime);
            pullSliderYPos = pullBarHandle.localPosition.y;
            pull = Normal(Mathf.Abs(pullSliderYPos), Mathf.Abs(pullSliderMaxYPos));         // 정규화(0~1)


            float chGap = cuePivot.localRotation.eulerAngles.y - cuePivotLocalRotationY;
            if(Mathf.Abs(chGap) > 0.05f)
            {
                TryCalculateShot(true);
            }

            //Debug.Log($"shotController UpdateFromNetwork cuePivotLocalRotationY : {cuePivot.localRotation.eulerAngles.y - cuePivotLocalRotationY}");
            //if (Mathf.Abs(cuePivot.localRotation.eulerAngles.y - cuePivotLocalRotationY) > 0.1f)
            //{
            //    TryCalculateShot(true);
            //}
            //if (Mathf.Abs(cuePivot.localRotation.eulerAngles.y - cuePivotLocalRotationY) < 0.1f && cuePivot.localRotation.eulerAngles.y != cuePivotLocalRotationY)
            //{
            //    TryCalculateShot(true);
            //}
        }

        // 큐볼 변경과 큐대 큐볼위치로 변경
        public void SetCueBall()
        {
            cueBall?.SetCueBall(false);

            //Debug.Log($"ShotCtrl.SetCueBall");

            int cueball_id = PoolPlayer.ord;
            cueBall = physicsManager.balls[cueball_id];
            cueBall.SetCueBall(true);

            cuePivot.position = cueBall.position;
            cueVertical.parent = cuePivot;
            cueVertical.localPosition = Vector3.zero;
            cueVertical.localRotation = Quaternion.identity;
            cueDisplacement.localPosition = Vector3.zero;
            cueSlider.localPosition = Vector3.zero;
            physicsManager.SetBallSign();

            cueBallMaxVelocity = 6f;
            cueBallRadius = 0.5f * cueBall.transform.lossyScale.x;

            GuideLineReset();


            //ringCueBallTran = physicsManager.ringCue.transform.GetChild(0);
            //ringCueMat = physicsManager.ringCue.transform.GetChild(0).GetComponent<MeshRenderer>().material;
            //dirCueMat = physicsManager.ringCue.transform.GetChild(1).GetComponent<MeshRenderer>().material;

            //ringCueMat.SetFloat("_Figure", 0.6f);

            //pnlMatch.PoolCoach_OnSetGameInfo($"SetCueBall myturn : {PoolPlayer.mainPlayer.myTurn}, turnId : {PoolPlayer.turnId}, ord : {PoolPlayer.ord}");

        }

        bool ChkBallPos(Vector3 origin)
        {
            origin.y = 0.5f;
            bool isFind = false;
            RaycastHit targetShapeHit;
            Vector3 direction = new Vector3(0, -1, 0);
            if (Physics.SphereCast(origin, cueBallRadius * 2f, direction, out targetShapeHit, 1f, ballLayer))
            {
                Ball listener = targetShapeHit.collider.gameObject.GetComponent<Ball>();
                if (listener)
                {
                    isFind = true;
                    //Debug.Log($"Ball Find id : {listener.id} , ball pos : {listener.body.position}, orgin : {origin}");
                }
            }
            else
            {
                //Debug.Log($"orgin : {origin}");
            }


            return isFind;
        }




    }
}
