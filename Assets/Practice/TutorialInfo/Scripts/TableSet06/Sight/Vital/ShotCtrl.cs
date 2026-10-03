using Assets.TutorialInfo.Scripts.TableSet06.Excert.Match;
using Assets.TutorialInfo.Scripts.TableSet06.Often;
using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Threading;
using UnityEngine;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{
    public partial class ShotCtrl : MonoBehaviour
    {


        public static bool canControl = true;
        public bool IsShotAnimating { get; private set; }
        public struct PracticeAim
        {
            public Quaternion pivot, vertical;
            public Vector3 contact;
            public Vector2 normal;
            public float follow;
        }
        public PracticeAim CapturePracticeAim() => new PracticeAim {
            pivot = cuePivot.localRotation, vertical = cueVertical.localRotation,
            contact = cueDisplacement.localPosition, normal = pushNormal, follow = pull };
        public void RestorePracticeAim(PracticeAim aim)
        {
            cuePivot.localRotation = aim.pivot;
            cueVertical.localRotation = aim.vertical;
            cueDisplacement.localPosition = aim.contact;
            pushNormal = aim.normal;
            force = 0; forceSliderYPos = 0; forceSliderMinYPos = 0;
            forceHandle.localPosition = Vector3.zero;
            cueSlider.localPosition = Vector3.zero;
            SetFollowThrough(aim.follow);
            GuideLineReset();
            OnCueRotateState?.Invoke(cuePivot.localRotation);
            canControl = true;
        }
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
        private Material[] cueBallLineMaterials;
        private Material[] targetBallLineMaterials;
        private Material forceGageMaterial;

        public int clothLayer { get; set; }
        public int boardLayer { get; set; }
        public int ballLayer { get; set; }
        public int cueBallLayer { get; set; }

        public delegate void OnCueRotate(Quaternion quaternion);
        public static event OnCueRotate OnCueRotateState;

        public Transform ringCueBall;
        public Material ringCue;
        public Material dirCue;


        [SerializeField] PhysicsMng physicsManager;
        public Transform cuePivotAfterShotPosition;
        public Transform cuePivot;
        public Transform cueVertical;
        public Transform cueDisplacement;
        public Transform cueSlider;

        public BallC cueBall;



        bool inShot = true;
        bool inMove = true;
        float checkTime = 0f;
        [NonSerialized] public Vector3 shotPoint;
        private float cueBallMaxVelocity = 6f;
        private float minVertical = 5f;
        private float maxVertical = 10f;
        private float cueBallJumpVelocity = 24f;
        private bool isDrawLineEnd = true;
        CancellationTokenSource shotCancel;





        private Vector3 detailTurnStartPos;
        private Vector3 boardTurnStartPos;
        private float boardTurnBasAng, boardTurnPreAng;
        private bool isBoardTurnBallFind;
        private float detailTurnStartAng;
        private Vector3 detailTurnPrePos;
        private float detailTurnPreAng;
        private bool isRightDir = true;    // 오른쪽방향(상단)
        public Transform TurnHandle;
        private bool isIn = true;

        private float changeAagle = 0.1f;









        float forceSliderMinYPos = 0f;
        float forceSliderMaxYPos = -0.91f;
        float forceSliderYPos;
        float forceSliderToCueRate = 0.5f;
        public float force { get; private set; }
        public float cueSliderDisplacementY { get; private set; }
        public Transform forceHandle;
        public SpriteRenderer forceGage;


        public const float DefaultFollowThrough = 0.2f;
        const float pullSliderMinYPos = 0f;
        const float pullSliderMaxYPos = 0.31f;
        const float pullSliderBasYPos = pullSliderMaxYPos * DefaultFollowThrough;
        float pullSliderYPos = pullSliderBasYPos;

        public float pull { get; private set; } = DefaultFollowThrough;
        public float pullSliderDisplacementY { get; private set; }
        public Transform pullBarHandle;




        // 큐 변경사항 체크 용도
        private float cuePivotLocalRotationY;
        private float cueVerticalLocalRotationX;
        private Vector2 cueDisplacementLocalPositionXY;
        private float cueSliderLocalPositionZ;

        private void Awake()
        {
            canControl = true;
            Debug.Log($"{ this.GetType().Name}  ===== ");
            clothLayer = LayerLib.NameToInt(LayerKind.Cloth);
            boardLayer = LayerLib.NameToInt(LayerKind.Board);
            ballLayer = LayerLib.NameToInt(LayerKind.Ball);
            cueBallLayer = LayerLib.NameToInt(LayerKind.CueBall);

            lineLength = 1.6f;
            InputOutput.OnMouseState += InputOutput_OnMouseState;
        }

        private void Start()
        {
            cueBallMaxVelocity = 6f;
            cueBall = physicsManager.ballcs[0];
            cueBallRadius = cueBall.GetComponent<SphereCollider>().radius * Mathf.Abs(cueBall.transform.lossyScale.x);
            ringCueBall = physicsManager.ringCue.transform.GetChild(0);
            ringCue = physicsManager.ringCue.transform.GetChild(0).GetComponent<MeshRenderer>().material;
            dirCue = physicsManager.ringCue.transform.GetChild(1).GetComponent<MeshRenderer>().material;
            cueBallLineMaterials = CacheLineMaterials(cueBallLines);
            targetBallLineMaterials = CacheLineMaterials(targetBallLines);
            forceGageMaterial = forceGage.material;
            SetFollowThrough(DefaultFollowThrough);
            CreateGaugeValueLabels();
            CreateOverlapPreview();
        }

        private static Material[] CacheLineMaterials(LineRenderer[] lines)
        {
            Material[] materials = new Material[lines.Length];
            for (int i = 0; i < lines.Length; i++)
            {
                materials[i] = lines[i].material;
            }
            return materials;
        }

        private void OnDestroy()
        {
            prediction?.Dispose();
            InputOutput.OnMouseState -= InputOutput_OnMouseState;
        }

        void InputOutput_OnMouseState(MouseState mouseState)
        {
            if (Assets.Scripts.Often.PracticeMatchTestRoute.Enabled &&
                (mouseState == MouseState.Down || mouseState == MouseState.Up))
                Debug.Log($"[PracticeMatchInput] event={mouseState} control={canControl} canSubmit={physicsManager.OnlineAdapter?.CanSubmit} equip={InputOutput.targetEquip} blocked={physicsManager.IsPointerOverUndoButton()} power={force}");
            if (mouseState == MouseState.Down && physicsManager.IsPointerOverUndoButton())
            {
                _targetEquip = TargetEquip.None;
                return;
            }
            if (!canControl || IsShotAnimating ||
                (PoolCoach.Instance.Configuration.Execution == Assets.Scripts.Often.MatchExecutionMode.OnlineMatch &&
                (physicsManager.OnlineAdapter == null || !physicsManager.OnlineAdapter.CanSubmit))) return;

            if (mouseState == MouseState.Down)
            {
                if (InputOutput.targetEquip == TargetEquip.SlidePull)
                {
                    _targetEquip = TargetEquip.SlidePull;

                }
                else if (InputOutput.targetEquip == TargetEquip.SlidePower)
                {
                    _targetEquip = TargetEquip.SlidePower;
                    forceSliderYPos = forceSliderMinYPos;
                }
                else if (InputOutput.targetEquip == TargetEquip.TableBoard)
                {
                    boardTurnStartPos = InputOutput.mouseWorldPosition;
                    float startAngle = CalculateAngle2(cuePivot.position, boardTurnStartPos);
                    boardTurnBasAng = startAngle - cuePivot.localRotation.eulerAngles.y;
                    if (Mathf.Abs(boardTurnBasAng) > 180)
                        boardTurnBasAng += boardTurnBasAng > 0 ? -360 : 360;
                    isBoardTurnBallFind = ChkBallPos(boardTurnStartPos);
                    boardTurnPreAng = boardTurnBasAng;
                    _targetEquip = TargetEquip.TableBoard;
                }
                else if (InputOutput.targetEquip == TargetEquip.DetailTurn)
                {
                    //Debug.Log($"turn Down mouse pos : {InputOutput.mouseWorldPosition.z}, pan pos : {TurnHandle.position.z}");
                    detailTurnStartPos = InputOutput.mouseWorldPosition;
                    isRightDir = detailTurnStartPos.z >= TurnHandle.position.z;
                    detailTurnStartAng = CalculateAngle(TurnHandle.position, detailTurnStartPos);
                    detailTurnPrePos = detailTurnStartPos;
                    detailTurnPreAng = detailTurnStartAng;
                    _targetEquip = TargetEquip.DetailTurn;
                }
            }

            if (mouseState == MouseState.PressAndMove || mouseState == MouseState.PressAndStay || mouseState == MouseState.Up)
            {
                if (_targetEquip == TargetEquip.SlidePull)
                {
                    OnSlidePull(mouseState);
                }
                else if (_targetEquip == TargetEquip.SlidePower)
                {
                    OnSlidePower(mouseState);
                }
                else if (_targetEquip == TargetEquip.TableBoard)
                {
                    OnTableBoard(mouseState);
                    TryCalculateShot(true);
                }
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

        // 큐대를 조준 안하고 잠시둔다.
        public void CuePutAside()
        {
            cueVertical.parent = cuePivotAfterShotPosition;
            cueVertical.localPosition = Vector3.zero;
            cueVertical.localRotation = Quaternion.identity;
            cueDisplacement.localPosition = Vector3.zero;
        
            GuideLineReset();

            //Debug.Log("CuePutAside");
        }

        // 큐대를 조준하기위해 준비한다.
        public void CueReadyShot()
        {
            if (physicsManager.OnlineAdapter != null) ShowOnlineStroke(0, DefaultFollowThrough);
            // 선수별 큐볼지정
            cueBall?.SetCueBall(false);
            cueBall = null;
            int id = PoolPlayer.turnId;
            cueBall = physicsManager.ballcs[id];
            physicsManager.cuball = cueBall.body;
            cueBall.SetCueBall(true);
            //Debug.Log($"CueReadyShot PoolPlayer.turnId : {PoolPlayer.turnId} .. cueBall : {cueBall.id}  ....");
            
            cuePivot.position = cueBall.transform.position;
            cueVertical.parent = cuePivot;
            cueVertical.localPosition = Vector3.zero;
            cueVertical.localRotation = Quaternion.identity;
            cueDisplacement.localPosition = Vector3.zero;
            //Debug.Log($" CueReadyShot parent : {cuePivot.parent.name} ");

            
        }
      

        public void cuePosHanger(bool _isHit = false)
        {
            Debug.Log("cuePosHanger ... ");
            if (_isHit)
            {
                cuePivot.position = cueBall.transform.position;
                cueVertical.localPosition = Vector3.zero;
                cueVertical.localRotation = Quaternion.identity;
                cueDisplacement.localPosition = Vector3.zero;
            }
            else
            {
                isIn = !isIn;
                if (isIn)
                {
                    cueVertical.parent = cuePivotAfterShotPosition;
                    cueVertical.localPosition = Vector3.zero;
                    cueVertical.localRotation = Quaternion.identity;
                    cueDisplacement.localPosition = Vector3.zero;


                }
                else
                {
                    cuePivot.position = cueBall.transform.position;
                    cueVertical.parent = cuePivot;
                    cueVertical.localPosition = Vector3.zero;
                    cueVertical.localRotation = Quaternion.identity;
                }
            }
        }




        void OnDetailTurn(MouseState mouseState)
        {
            //Debug.Log($"OnDetailTurn mouseState : {mouseState}");
            bool isDrag = true;
            float angleCueY = cuePivot.localRotation.eulerAngles.y;
            float anglesZ = TurnHandle.localRotation.eulerAngles.z;
            float ang_up;
            float ang_gap;
            float ang_gap_pan;
            float rot;
            if (mouseState == MouseState.PressAndMove)
            {

                isRightDir = InputOutput.mouseWorldPosition.z >= detailTurnPrePos.z;
                rot = isRightDir ? -1f : 1f;
                ang_up = CalculateAngle(TurnHandle.position, InputOutput.mouseWorldPosition);
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
                    TurnHandle.localRotation = Quaternion.Euler(0, 0, ang_new);
                    detailTurnPrePos = InputOutput.mouseWorldPosition;
                    detailTurnPreAng = CalculateAngle(TurnHandle.position, detailTurnPrePos);

                    OnCueRotateState?.Invoke(cuePivot.rotation.normalized);
                }

            }
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
                    TurnHandle.localRotation = Quaternion.Euler(0, 0, anglesZ + (ang_gap_pan * rot));
                    cuePivot.localRotation = Quaternion.Euler(0, angleCueY + (ang_gap * rot), 0);
                    OnCueRotateState?.Invoke(cuePivot.rotation.normalized);
                    DetailRotationArrowFade(rot).Forget();
                }
            }
        }

        async UniTaskVoid DetailRotationArrowFade(float dir)
        {
            float a = 1f;
            float timer = 0;
            dirCue.SetFloat("_Dir", dir);
            //Debug.Log($"dir : {dir} ============ ");
            while (0 < a)
            {
                timer += Time.deltaTime;
                //Debug.Log($"dir : {dir}, a : {a} ");
                dirCue.SetFloat("_Alpha", a);
                a -= 0.05f;
                await UniTask.Yield();
            }
            //Debug.Log($"timer : {timer} .... ");

        }

        public void SetCueTargetingPosition(Vector3 normalizedPosition, Vector2 absoluteNormal)
        {

            pushNormal = absoluteNormal;
            Vector3 spin = cueDisplacementOnBall - normalizedPosition * cueBallRadius;
            cueDisplacement.localPosition = spin;
        }






        public float CalculateAngle(Vector3 from, Vector3 to)
        {
            Vector3 vec = Quaternion.FromToRotation(Vector3.right, to - from).eulerAngles;
            return vec.y;
        }




        // Capture the stroke controls for both the initial impulse and the ball-contact profile.
        public void SetFollowThrough(float value)
        {
            pull = float.IsNaN(value) ? DefaultFollowThrough : Mathf.Clamp01(value);
            pullSliderYPos = Mathf.Lerp(pullSliderMinYPos, pullSliderMaxYPos, pull);
            pullSliderDisplacementY = pullSliderYPos;
            if (pullBarHandle) pullBarHandle.localPosition = new Vector3(0, pullSliderYPos, 0);
        }

        public Impulse BuildShotImpulse() => BuildImpulseForPower(force);

        public Impulse BuildGuideImpulse() => BuildImpulseForPower(.4f);

        Impulse BuildImpulseForPower(float power)
        {
            Vector3 forward = Vector3.ProjectOnPlane(cueSlider.forward, Vector3.up).normalized;
            Vector3 impulse = power * (maxVelocity * cueBall.body.mass) * forward;
            Vector3 centre = cueBall.body.worldCenterOfMass;
            Vector3 offset = cueDisplacement.position - centre;
            float radius = cueBall.GetComponent<SphereCollider>().radius * cueBall.transform.lossyScale.x;
            float contactAmount = Mathf.Clamp01(Vector3.ProjectOnPlane(offset, forward).magnitude / Mathf.Max(radius, .001f));
            float blend = pull <= DefaultFollowThrough ? pull / DefaultFollowThrough :
                (pull - DefaultFollowThrough) / (1f - DefaultFollowThrough);
            const float spinScale = .46f; // Preserve the previous default stroke's spin, independent of follow.
            float speedScale = pull <= DefaultFollowThrough ? Mathf.Lerp(1.04f, 1f, blend) : Mathf.Lerp(1f, .92f, blend);
            // Effective lever arm is an arcade impact model, not a moved aiming point.
            // Its torque scales independently of the linear impulse; centre hits gain no spin.
            Vector3 effectivePoint = centre + offset * (spinScale / speedScale);
            return new Impulse(effectivePoint, impulse * speedScale, Vector3.zero, pull, FollowThroughProfile.Persistence(power,contactAmount,pull));
        }

        void OnSlidePull(MouseState mouseState)
        {
            //Debug.Log($"OnSlidePull mouseState : {mouseState}");


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

                SetFollowThrough(Mathf.InverseLerp(pullSliderMinYPos, pullSliderMaxYPos, pullSliderYPos));

            }
            else if (mouseState == MouseState.Up)
            {
                //force = Normal(Mathf.Abs(forceSliderYPos), Mathf.Abs(forceSliderMaxYPos));
                //Debug.Log($"SlidePower Up - force : {force} *********** ");
                ///StartCoroutine(WaitAndStartShot());
            }

        }

        void OnSlidePower(MouseState mouseState)
        {
            //Debug.Log($"OnSlidePower mouseState : {mouseState}");


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
                forceGageMaterial.SetVector("_Remap", new Vector2(-1, concave));

                ringCue.SetFloat("_Percentage", force);


            }
            else if (mouseState == MouseState.Up)
            {
                force = Normal(Mathf.Abs(forceSliderYPos), Mathf.Abs(forceSliderMaxYPos));
                // Debug.Log($"SlidePower Up - force : {force} *********** ");
                if (force == 0) return;
                WaitAndStartShot().Forget();
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





        async UniTaskVoid WaitAndStartShot()
        {
            if (IsShotAnimating || physicsManager.inMove) return;
            if (physicsManager.OnlineAdapter != null && !physicsManager.OnlineAdapter.CanSubmit) return;
            physicsManager.BeginReplayCueStroke();
            IsShotAnimating = true;
            try
            {
                GuideLineReset();
                var token = this.GetCancellationTokenOnDestroy();
                float follow = pull;
                float endZ = pullSliderMaxYPos * forceSliderToCueRate * follow * 1.3f;
                forceGage.size = new Vector2(0.75f, 0f);
                forceGage.transform.localPosition = new Vector3(0.0f, 0.065f, 0);
                forceGageMaterial.SetVector("_Remap", new Vector2(-1, 10));

                // Strike at contact, then extend from that fixed position, independently of shot power.
                await AnimateCueSlide(cueSlider.localPosition.z, 0f, .08f, token);
                WaitAndStartShotAsync().Forget();
                await AnimateCueSlide(0f, endZ, .12f, token);
                await UniTask.Delay(500, cancellationToken: token);
                await AnimateCueSlide(endZ, 0f, .25f, token);
                forceSliderMinYPos = 0f;

                SetFollowThrough(DefaultFollowThrough);

                forceHandle.localPosition = Vector3.zero;
                cueSlider.localPosition = Vector3.zero;

                forceGage.size = new Vector2(0.75f, 1f);
                forceGage.transform.localPosition = new Vector3(0.0f, 0.065f, 0);

                CuePutAside(); // 

                //cuePosHanger();

            }
            finally { IsShotAnimating = false; }
        }

        async UniTask AnimateCueSlide(float start, float end, float duration, CancellationToken token)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float z = Mathf.Lerp(start, end, Mathf.Clamp01(elapsed / duration));
                cueSlider.localPosition = new Vector3(0, 0, z);
                forceHandle.localPosition = new Vector3(0, z / forceSliderToCueRate, 0);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            cueSlider.localPosition = new Vector3(0, 0, end);
            forceHandle.localPosition = new Vector3(0, end / forceSliderToCueRate, 0);
        }

        public void SetPlacementGuidesHidden(bool hidden)
        {
            hidden |= !physicsManager.CueGuideEnabled;
            foreach (var guide in cueBallLines) guide.enabled = !hidden;
            foreach (var guide in targetBallLines) guide.enabled = !hidden;
            ballCheckerRenderer.enabled = !hidden;
            if (hidden) GuideLineReset();
            else { cueSliderLocalPositionZ = float.NaN; TryCalculateShot(true); }
        }

        void GuideLineReset()
        {
            predictionDirty = true;
            ballChecker.position = new Vector3(0, 0, 2f);
            for (int i = 0; i < cueBallLines.Length; i++) cueBallLines[i].positionCount = 0;
            for (int i = 0; i < targetBallLines.Length; i++) targetBallLines[i].positionCount = 0;
        }

        public bool cueChanged
        {
            get
            {
                if (cuePivotLocalRotationY != cuePivot.localRotation.eulerAngles.y ||
                    cueVerticalLocalRotationX != cueVertical.localRotation.eulerAngles.x ||
                    cueDisplacementLocalPositionXY != new Vector2(cueDisplacement.localPosition.x, cueDisplacement.localPosition.y) ||
                    cueSliderLocalPositionZ != cueSlider.localPosition.z)
                {
                    cuePivotLocalRotationY = cuePivot.localRotation.eulerAngles.y;
                    cueVerticalLocalRotationX = cueVertical.localRotation.eulerAngles.x;
                    cueDisplacementLocalPositionXY = new Vector2(cueDisplacement.localPosition.x, cueDisplacement.localPosition.y);
                    cueSliderLocalPositionZ = cueSlider.localPosition.z;
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

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

        async UniTaskVoid WaitAndStartShotAsync()
        {
            shotCancel?.Cancel();
            shotCancel?.Dispose();
            shotCancel = new CancellationTokenSource();
            shotPoint = cueDisplacement.position;
            if (physicsManager.OnlineAdapter != null)
            {
                physicsManager.OnlineAdapter.Submit(CaptureOnlineStroke());
                return;
            }
            Impulse impulse = BuildShotImpulse();
            float angle = cuePivot.localEulerAngles.y;
            await physicsManager.StartShot(impulse, angle);
        }

        bool ChkBallPos(Vector3 origin)
        {
            origin.y = .5f;
            return Physics.SphereCast(origin, cueBallRadius * 2f, Vector3.down,
                out var hit, 1f, ballLayer) && hit.collider.GetComponent<BallC>() != null;
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
            return Mathf.Abs(ang);
        }

        void OnTableBoard(MouseState mouseState)
        {

            if (mouseState == MouseState.PressAndStay)
            {
                if (isBoardTurnBallFind)
                {
                    Vector3 mouseWorldDirectionCuePivot = Vector3.ProjectOnPlane(InputOutput.mouseWorldPosition - cuePivot.position, Vector3.up).normalized;
                    Quaternion cuePivotRotation = Quaternion.identity;
                    cuePivotRotation.SetLookRotation(mouseWorldDirectionCuePivot);
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
                Quaternion cuePivotRotation = Quaternion.Euler(0, ang_gap * 0.2f, 0) * cuePivot.rotation;

                cuePivot.rotation = cuePivotRotation;
                OnCueRotateState?.Invoke(cuePivotRotation);
                physicsManager.ringCue.transform.rotation = cuePivotRotation;

                boardTurnPreAng = boardTurnBasAng;
            }

        }

        void TryCalculateShot(bool forceCalculate)
        {
            if(!physicsManager.CueGuideEnabled) { GuideLineReset();return; }
            // The prediction cache detects aim/contact/spin changes independently of the power handle.
            if (physicsManager.useCalibratedPhysics) return;
            if (!forceCalculate)
            {

                return;
            }

            if (!cueChanged)
            {
                //Debug.Log($"same same");
                return;
            }
            cueBallRadius = cueBall.GetComponent<SphereCollider>().radius * Mathf.Abs(cueBall.transform.lossyScale.x);
            Vector3 origin = cueBall.body.position;
            Vector3 direction = Vector3.ProjectOnPlane(cueSlider.forward, Vector3.up).normalized;
            if (isDrawLineEnd)
            {
                isDrawLineEnd = false;
                DrawBallHitLine(0, origin, direction, lineLength, false);
                if (physicsManager.useCalibratedPhysics)
                {
                    // No power has been selected: only geometric approach/thickness is meaningful.
                    for (int i=1;i<cueBallLines.Length;i++) cueBallLines[i].positionCount=0;
                    foreach (var line in targetBallLines) line.positionCount=0;
                }

            }
            else
            {
                //Debug.Log("TryCalculateShot ing");
                ringCue.SetFloat("_Figure", 0);
                //Debug.Log("TryCalculateShot _Figure 00 ");

            }

        }



        // isBallHit 적구 타격 여부, isCueball
        void DrawBallHitLine(int cnt, Vector3 origin, Vector3 direction, float line_len, bool isBallHit = false, bool isCueball = true)
        {
            LineRenderer[] lines = isCueball ? cueBallLines : targetBallLines;
            Material[] lineMaterials = isCueball ? cueBallLineMaterials : targetBallLineMaterials;
            if ((uint)cnt >= (uint)lines.Length)
            {
                isDrawLineEnd = true;
                return;
            }
            //Debug.Log($"DrawBallHitLine cnt : {cnt}");

            RaycastHit targetShapeHit;
            // isBallHit || !isCueball : 보드만 확인하면됨.  ... !isBallHit : 적구가 타격전 상태, !isCueball : 적구임
            if (Physics.SphereCast(origin, cueBallRadius, direction, out targetShapeHit, cnt == 0 && isCueball ? 3f : line_len, isBallHit || !isCueball ? boardLayer : (ballLayer | boardLayer)))
            {
                BallC listener = targetShapeHit.collider.gameObject.GetComponent<BallC>();

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

                    //Debug.Log($"cnt : {cnt}, figure : { figure }  , target.id : {listener.id}  ---------------------------------  ");
                    ringCue.SetFloat("_Figure", figure);

                    lines[cnt].positionCount = 2;
                    lines[cnt].SetPosition(0, origin);
                    lines[cnt].SetPosition(1, positionInHit);

                    //ballChecker.position = positionInHit;
                    SetBallChecker(positionInHit, listener.id);

                    float cueTiling = lineDistToTiling(moved_line_dist);
                    lineMaterials[cnt].SetVector("_Tiling", new Vector2(cueTiling, 1));

                    Vector3 targetPosition = listener.body.position;
                    Vector3 targetDirection = (targetPosition - positionInHit).normalized;
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
                    lineMaterials[cnt].SetVector("_Tiling", new Vector2(cueTiling, 1));

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
                    lineMaterials[cnt].SetVector("_Tiling", new Vector2(cueTiling, 1));
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

            int ringIndex = 0;
            foreach (var ball in physicsManager.ballcs)
            {
                if (ball.isCueball) continue;
                physicsManager.ringBalls[ringIndex++].GetComponent<MeshRenderer>().material.SetFloat("_Alpha", ball.id == id ? 1f : .3f);
            }
            if (id == cueBall.id || id == -1)
            {
                ballChecker.position = new Vector3(0, 0, 2f);
                ringCue.SetFloat("_Figure", 0);
            }
            else ballChecker.position = pos;

            //Debug.Log("SetBallChecker _Figure 00 ");

         

        }



    }



    public struct Impulse
    {
        public readonly Vector3 point;
        public readonly Vector3 impulse;
        public readonly Vector3 pushVec;
        public readonly float followThrough;
        public readonly float spinPersistence;

        public Impulse(Vector3 point, Vector3 impulse, Vector3 pushVec, float followThrough = -1f,float spinPersistence = -1f)
        {
            this.point = point;
            this.impulse = impulse;
            this.pushVec = pushVec;
            this.followThrough = followThrough;
            this.spinPersistence = spinPersistence;
        }

        public override string ToString()
        {
            return point.ToString() + ".." + impulse.ToString() + ".." + pushVec.ToString();
        }
    }


}
