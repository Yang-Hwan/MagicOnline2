using Assets.TutorialInfo.Scripts.TableSet06.Excert.Match;
using Assets.TutorialInfo.Scripts.TableSet06.Often;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{

    public enum EffType
    {
        Effect01,
        Effect02,
        Effect03,
        Effect04,
    }

    public enum BallState
    {
        Non = 0,
        SetState,
        StartMove,
        Move,
        HitBall,
        HitBoard,
        HitStuff,
        EnterInPocket,
        MoveInPocket,
        ExitFromPocket,
        EndMove
    }

    public delegate void BallHitBallHandler<BallC, Boolean>(BallC ball, BallC hitBall, bool inMove);
    public delegate void BallHitBoardHandler<BallC, Boolean>(BallC ball, bool inMove, CushionDir cushionDir);
    public delegate void BallShotHandler<Float>(float data);
    public delegate void BallMoveHandler<Int, Vector3>(int ballId, Vector3 position, Vector3 velocity, Vector3 angularVelocity);
    public delegate void BallSleepHandler<Int, Vector3>(int ballId, Vector3 position);

    [DefaultExecutionOrder(-100)]
    public partial class PhysicsMng : MonoBehaviour
    {

        public event BallHitBallHandler<BallC, bool> OnBallHitBall;
        public event BallHitBoardHandler<BallC, bool> OnBallHitBoard;
        public event BallShotHandler<float> OnStartShot;                       // 큐대움직임 처리완료후 ShotController.WaitAndStartShot
        public event BallMoveHandler<int, Vector3> OnBallMove;
        public event BallSleepHandler<int, Vector3> OnBallSleep;
        public event System.Action<int, string> OnWordBalloon;

        public Button btnMatchEnd;

        public CushionHit cushionHit;
        public BallC[] ballcs;
        public Transform[] ballcPosResets;
        public ShotCtrl shotController;

        [Header("Opening scatter")]
        [SerializeField, Min(0.1f)] float scatterMinSpeed = 1.5f;
        [SerializeField, Min(0.1f)] float scatterMaxSpeed = 3.5f;
        bool scatterPending;
        int scatterVersion;

        public bool inMove { get; private set; }
        public PracticeMatchAdapter OnlineAdapter { get; private set; }
        public bool UsesPracticePhysics => IsLocalPractice || OnlineAdapter != null;
        internal void FreezeOnlineMotion()
        {
            if (OnlineAdapter == null) return;
            simulationVersion++; inMove = false;
            foreach (var ball in ballcs)
            {
                if (!ball.body || ball.body.isKinematic) continue;
                ball.body.linearVelocity = Vector3.zero; ball.body.angularVelocity = Vector3.zero;
                ball.body.Sleep();
            }
        }


        public Rigidbody cuball;
        public Vector3[] resetPos;
        public Vector3[] resetPosPre;
        public Quaternion[] resetQuatPre;

        private bool checkInProgress = false;
        public float moveTime { get; set; }
        public event Action<string> OnBallAllStop;
        CancellationTokenSource stopCancel;
 
 

        public Vector3 pushNormalPre;
        public Impulse impulsePre;
        bool isTestshot = false;

        public DrawStuffPos drawStuffPos;
        float dist_belt_min = 0.4f;


        public GameObject ringCue;
        public GameObject[] ringBalls;

        public LineRenderer line;
        float dist_min = 0.02f;
        Vector3 pos_pre = Vector3.zero;


        public float addWaitSecond = 0.1f;
        public float addForce = 100f;
        public ForceMode addForceMode = ForceMode.Force;




        [Header("UI references")]
        [SerializeField] TextMeshProUGUI[] coinTxts;
        [SerializeField] TMP_Text coinUIText;
        [SerializeField] GameObject animatedCoinPrefab;
        [SerializeField] Transform[] targetCoin;

        [Space]
        [Header("Available coins : (coins to pool)")]
        [SerializeField] int maxCoins;
        Queue<GameObject> coinsQueue = new Queue<GameObject>();
        readonly List<GameObject> animatedCoins = new List<GameObject>();
        [Space]
        [Header("Animation settings")]
        [SerializeField] [Range(0.2f, 0.5f)] float minAnimDuration;
        [SerializeField] [Range(0.5f, 2.0f)] float maxAnimDuration;

        [SerializeField] Ease easeType;
        [SerializeField] float spread;

        [SerializeField] Transform[] ballShadows;

        private int[] _c = new int[2];

        public float cueBallEarlyAngle { get; private set; }
        public int Coins => (int)(PoolPlayer.currentPlayer?.matchCoin ?? 0);

        public float min_x, min_z, max_x, max_z;

        private float previousFixedDeltaTime;
        private bool previousAutoSimulation;
        private float previousBounceThreshold;
        private float previousSleepThreshold;
        private float previousDefaultContactOffset;
        private int previousSolverIterations;
        private int previousSolverVelocityIterations;


        [Obsolete]
        private void Awake()
        {
            if (Assets.Scripts.Often.PracticeSceneFlow.Configuration.BallCount == 3)
            {
                // IDs 0/1/2 are white/yellow/red; ID 3 is the second red ball.
                ballcs[3].gameObject.SetActive(false);
                ballShadows[3].gameObject.SetActive(false);
                System.Array.Resize(ref ballcs, 3);
            }
            previousFixedDeltaTime = Time.fixedDeltaTime;
            previousAutoSimulation = Physics.autoSimulation;
            previousBounceThreshold = Physics.bounceThreshold;
            previousSleepThreshold = Physics.sleepThreshold;
            previousDefaultContactOffset = Physics.defaultContactOffset;
            previousSolverIterations = Physics.defaultSolverIterations;
            previousSolverVelocityIterations = Physics.defaultSolverVelocityIterations;

            // 100 Hz is sufficient for the four billiard balls and halves the
            // FixedUpdate overhead of the previous 200 Hz setting.
            Time.fixedDeltaTime = 0.01f;
            Physics.autoSimulation = false;
            Physics.bounceThreshold = 0.01f;
            Physics.sleepThreshold = 0.01f;
            Physics.defaultContactOffset = 0.0005f;
            Physics.defaultSolverIterations = 1;
            Physics.defaultSolverVelocityIterations = 1;
            int len = ballcs.Length;
            resetPos = new Vector3[len];
            resetPosPre = new Vector3[len];
            resetQuatPre = new Quaternion[len];
            for (int i = 0; i < len; i++)
            {
                resetPos[i] = ballcs[i].transform.position;
                resetPosPre[ballcs[i].id] = ballcs[i].transform.position;
                resetQuatPre[ballcs[i].id] = ballcs[i].transform.rotation;
                ballcs[i].ballShadow = ballShadows[i];
            }
            // Awake order between PhysicsMng and BallC is not guaranteed.
            cuball = ballcs[0].GetComponent<Rigidbody>();
            shotController = GetComponent<ShotCtrl>();

            inMove = Assets.Scripts.Often.PracticeSceneFlow.Configuration.Execution != Assets.Scripts.Often.MatchExecutionMode.OnlineMatch;
            if (!inMove) OnlineAdapter = gameObject.AddComponent<PracticeMatchAdapter>();

            ringCue.SetActive(false);
            for (int i = 0; i < ringBalls.Length; i++)
            {
                ringBalls[i].SetActive(false);
            }

            PrepareCoins();
            CreateUndoButton();
            CreatePracticeOptions();
            if (Items == null) BeginLocalItemSession();

            min_x = drawStuffPos.st.position.x;
            max_x = drawStuffPos.en.position.x;
            min_z = drawStuffPos.en.position.z;
            max_z = drawStuffPos.st.position.z;


            //btnMatchEnd.onClick.AddListener(tempMatchPlay);
        }

        public void SetCueball()
        {
            ringCue.SetActive(false);
            for (int i = 0; i < ringBalls.Length; i++)
            {
                ringBalls[i].SetActive(false);
            }

        }
        // 임시버튼 실행
        void tempMatchPlay()
        {
            Debug.Log($"tempMatchPlay");
            PoolCoach.Instance.Initialize(this, shotController);

            string s1, s2, s3, s4, s5;
            s1 = s2 = s3 = s4 = s5 = "";
           // s1 = Utility.CoinIntToStr(2);
           // s2 = Utility.CoinIntToStr(320);
            //s3 = Utility.CoinIntToStr(763320);
            //s4 = Utility.CoinIntToStr(921763320000);
            //s5 = Utility.CoinIntToStr(1121763320000);
            s5 = Utility.CoinNumToStr(4320021763320760);

            Debug.Log($"{s1} .. {s2} .. {s3} .. {s4} .. {s5} ");
        }

        async UniTaskVoid Start()
        {
            InitializeClothPhysics();
            if (IsLocalPractice && !IsReplayBrowser) PrepareRecordingBuffer();
            while (true)
            {
                await UniTask.WaitForFixedUpdate(cancellationToken: this.GetCancellationTokenOnDestroy());
                if (inMove && !scatterPending && !shotController.IsShotAnimating &&
                    (OnlineAdapter == null || OnlineAdapter.CanSimulate))
                {
                    int version = simulationVersion;
                    if (CheckIsSleeping(false))
                    {
                        await UniTask.WaitForSeconds(0.1f, cancellationToken: this.GetCancellationTokenOnDestroy());
                        if (version != simulationVersion || !inMove || shotController.IsShotAnimating) continue;
                        if (CheckIsSleeping(true))
                        {
                            await StopMove();
                        }
                        else
                        {
                            //Debug.Log("stopCancel?.Cancel()   ------------- ");
                            stopCancel?.Cancel();
                        }
                        checkInProgress = false;
                    }
                }
            }
        }

        async UniTaskVoid FadeLinePath()
        {
            int version = simulationVersion;
            await UniTask.WaitForSeconds(3f, cancellationToken: this.GetCancellationTokenOnDestroy());
            if (version == simulationVersion && line.positionCount > 0)
            {
                line.positionCount = 0;
                //Debug.Log("FadeLinePath");
            }
        }


        bool CheckIsSleeping(bool forceSleep)
        {
            if (useCalibratedPhysics) return CalibratedBallsSettled();
            float minEnergy = 0.5f;
            float minEnergyAng = 0.9f;
            bool isSleep = true;
            foreach (BallC ball in ballcs)
            {
                //if (ball == null) break;
                //if (ball.id != 0) continue;
                Rigidbody body = ball.body;

                bool isChk1 = body.isKinematic;
                bool isChk2 = body.linearVelocity.magnitude < minEnergy && 0.5f * body.angularVelocity.magnitude < minEnergyAng;
                bool isSleeping = isChk1 || isChk2;
                //Debug.Log($"isChk1 : {isChk1}, isChk2 : {isChk2} ........ magnitude : {body.velocity.magnitude}, angular : {body.angularVelocity.magnitude} ");
                if (!isSleeping)
                {
                    isSleep = false;
                }
            }
            return isSleep;
        }

        public void BeginOnlineTrail(int cueId)
        {
            Vector3 position = ballcs[cueId].body.position;
            position.y = 0;
            pos_pre = position;
            line.positionCount = 2;
            line.SetPosition(0, position);
            line.SetPosition(1, position);
        }

        public void DrawCueLinePath(Vector3 pos_cur)
        {
            pos_cur.y = 0f;
            if (line.positionCount < 2) return;
            // Keep a live endpoint even before the next spaced sample is committed.
            line.SetPosition(line.positionCount - 1, pos_cur);
            if (Vector3.Distance(pos_cur, pos_pre) > dist_min)
            {
                line.positionCount++;
                line.SetPosition(line.positionCount - 1, pos_cur);
                pos_pre = pos_cur;
            }
        }



        async UniTask StopMove()
        {
            int version = simulationVersion;
            //stopCancel?.Cancel();
            //stopCancel = new CancellationTokenSource();
            //CancellationToken token = stopCancel.Token;

            //Debug.Log("stop move");
            inMove = false;

 

            foreach (BallC ball in ballcs)
            {
                if (ball == null)
                {
                    break;
                }
                Rigidbody body = ball.body;
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                    body.Sleep();
                }

                if (ball.isCueball)
                {
                    ball.isCueball = false;
                }

                OnBallSleep?.Invoke(ball.id, ball.transform.position);
            }

            await UniTask.WaitForSeconds(0.1f, cancellationToken: this.GetCancellationTokenOnDestroy());
            // Undo or a new shot invalidates the previous shot's delayed result.
            if (version != simulationVersion) return;
            //await UniTask.WaitForSeconds(0.1f, cancellationToken: token);
            //shotController.CueReadyShot();
            moveTime = 0.0f;
            //StuffSpray(ballcs[0].transform.position);
            FadeLinePath().Forget();

            //Debug.Log($"PhysicsMng   StopMove");
            FinishShotRecording();
            OnBallAllStop?.Invoke("");
        }


        // 큐볼, 적구 등 디스플레이 적용  ..모든 공이 멈추었을 때
        public void SetBallSign()
        {

            float ang = -90f;  //유니티기본각도오차설정 
            float ang_rnd;
            float radian;
            Quaternion qua;
            int cueball_id = -1;
            int ring_idx = 0;

            
            foreach (BallC ball in ballcs)
            {
                //Debug.Log($"ball id : {ball.id} ... ball ... ");

                if (ball.isCueball)
                {
                    //Debug.Log($"ball id : {ball.id} <<<< --------------------- cueball... ");
                    ang_rnd = UnityEngine.Random.Range(0, 360);
                    radian = ang_rnd * Mathf.Deg2Rad;
                    qua = Quaternion.Euler(90, ang_rnd, 0);

                    ringCue.SetActive(true);
                    ringCue.transform.position = ball.transform.position;
                    ringCue.transform.localRotation = qua;
                    ringCue.transform.GetChild(0).GetComponent<MeshRenderer>().material.SetFloat("_Percentage", 0);
                    break;
                }
            }


            foreach (BallC ball in ballcs)
            {
                //Debug.Log($"__ ball id : {ball.id} ... ball ... is cueball : {ball.isCueball} ");

                ang_rnd = UnityEngine.Random.Range(0, 360);
                radian = ang_rnd * Mathf.Deg2Rad;
                qua = Quaternion.Euler(90, ang_rnd, 0);

                if (ball.isCueball) continue;

                ringBalls[ring_idx].SetActive(true);
                ringBalls[ring_idx].transform.position = ball.transform.position;
                ringBalls[ring_idx].transform.localRotation = qua;
                ring_idx++;
            }


        }
        // 
  

        private void FixedUpdate()
        {
            RecordCuePreparation();
            if (recordingShot && !inMove && !IsPracticeReplay)
            {
                RecordShotFrame();
                if (!shotController.IsShotAnimating) FinishShotRecording();
            }
            if (PoolCoach.Instance.Configuration.Execution == Assets.Scripts.Often.MatchExecutionMode.OnlineMatch &&
                (OnlineAdapter == null || !OnlineAdapter.CanSimulate)) return;
            if (inMove && !scatterPending)
            {
                foreach (var ball in ballcs) ball.CaptureMotionVisual();
                int steps = useCalibratedPhysics ? Mathf.Clamp(clothPhysics.substeps, 1, 8) : 1;
                float dt = Time.fixedDeltaTime / steps;
                for (int i = 0; i < steps; i++)
                {
                    if (useCalibratedPhysics) StepClothPhysics(dt);
                    Physics.Simulate(dt);
                }
                RecordShotFrame();
                if (PoolCoach.Instance.isMatchTimePlay) DrawCueLinePath(cuball.position);
            }
        }


        public async UniTask StartShot(Impulse impulse, float angle = 0)
        {
            if (PoolCoach.Instance.Configuration.Execution == Assets.Scripts.Often.MatchExecutionMode.OnlineMatch) return;
            await StartShotCore(impulse, angle);
        }

        internal async UniTask StartApprovedShot(Assets.Scripts.Often.MatchShotCommand shot)
        {
            if (OnlineAdapter == null || !OnlineAdapter.CanSimulate || inMove) return;
            cuball = ballcs[shot.seat].body;
            await StartShotCore(shotController.BuildOnlineImpulse(shot), shot.yaw);
        }

        async UniTask StartShotCore(Impulse impulse, float angle)
        {
            if (IsPracticeReplay || IsBallPlacement || PoolLogic.gameState.gameIsComplete) return;
            if (impulse.impulse != Vector3.zero) { CaptureUndoState(); BeginShotRecording(); }
            simulationVersion++;
            settledTime = 0;
            line.positionCount = 0;
            moveTime = 0.0f;
            //pushNormalPre = pushNormal;
            ringCue.SetActive(false);
            foreach (var ring in ringBalls) ring.SetActive(false);

            foreach (BallC ball in ballcs)
            {
                resetPosPre[ball.id] = ball.transform.position;
                resetQuatPre[ball.id] = ball.transform.rotation;
            }

            if (impulse.impulse != Vector3.zero)
            {
                impulsePre = impulse;
                foreach(var ball in ballcs) {
                    ball.strokeFollowThrough=ball.body==cuball ? impulse.followThrough : -1f;
                    ball.strokeSpinPersistence=ball.body==cuball ? impulse.spinPersistence : -1f;
                }
                if (PoolCoach.Instance.isMatchTimePlay && !scatterPending)
                {
                    pos_pre = cuball.position; pos_pre.y = 0f;
                    line.positionCount = 2;
                    line.SetPosition(0, pos_pre);
                    line.SetPosition(1, pos_pre);
                }
                ringCue.SetActive(false);
                foreach (var ring in ringBalls) ring.SetActive(false);
 
                inMove = true;
                //Debug.Log($"***StartShot impulse : ({impulse.impulse.x},{impulse.impulse.y},{impulse.impulse.z}),  point : ({impulse.point.x},{impulse.point.y},{impulse.point.z})");
                //shotController.cuePosHanger();

                

                cuball.AddForceAtPosition(impulse.impulse, impulse.point, ForceMode.Impulse);
            }


            //Debug.Log($"***1 AddFore >>>>> addForce : {addForce} ");


            // Legacy pushVec is retained in Impulse for saved-shot compatibility.
            // A follow-through is cue motion, not a second force on the ball.

            cueBallEarlyAngle = angle;

            OnStartShot?.Invoke(angle);

            await UniTask.Yield();



        }


        void CueReady()
        {
        //    shotController.cuePosHanger(false);
        }

        public void CusionHit(CushionDir dir)
        {
            if (!HasItemAuthority || !ItemsEnabled) return;
            Items.Cushion((int)dir);
            //Debug.Log($"CusionHit : 1");
            drawStuffPos.StuffCreate(5, 1);



        }

        public void CallBallHitBall(BallC ball, BallC hitBall, bool inReplay)
        {
            OnBallHitBall?.Invoke(ball, hitBall, inReplay);
        }

        public void CallBallHitBoard(BallC ball, bool inReplay, CushionDir cushionDir)
        {
            OnBallHitBoard?.Invoke(ball, inReplay, cushionDir);
        }



        public void CreateEff(StuffType stuffType, Vector3 pos, int playerId = -1)
        {
            if (playerId < 0) playerId = PoolPlayer.currentPlayer?.playerId ?? -1;
            string tag = stuffType == StuffType.StuffItem02 ? "Effect02" :
                stuffType == StuffType.StuffItem03 ? "Effect03" : "Effect01";
            var effect = ObjectPooler.instance.SpawnFromPool<EffCtrl>(tag, pos);
            effect.Setup(playerId, HasItemAuthority);
            if (stuffType == StuffType.StuffItem02) effect.Run(MoveComet(effect, pos));
            if (stuffType == StuffType.StuffItem03) effect.Run(ChainCollect(effect, pos));
        }
        IEnumerator MoveComet(EffCtrl effect, Vector3 origin)
        {
            int session = itemSessionVersion;
            // Same visible destination on both peers; physics/collection still belong to authority.
            Vector3 target = origin;
            float farthest = 0;
            foreach (var position in drawStuffPos.stuffListPos)
            {
                float distance = (position - origin).sqrMagnitude;
                if (distance > farthest) { farthest = distance; target = position; }
            }
            yield return new WaitForSeconds(.2f);
            while (session == itemSessionVersion && (effect.transform.position - target).sqrMagnitude > .000025f)
            {
                effect.transform.position = Vector3.MoveTowards(effect.transform.position, target, 1.5f * Time.deltaTime);
                yield return null;
            }
        }
        IEnumerator ChainCollect(EffCtrl effect, Vector3 origin)
        {
            int session = itemSessionVersion;
            var lineRenderer = effect.GetComponent<LineRenderer>();
            var candidates = new List<StuffBase>(itemViews.Values);
            candidates.Sort((left, right) => left.positionId.CompareTo(right.positionId));
            var selected = new List<long>(9);
            var points = new List<Vector3>(10) { origin + Vector3.up * .1f };
            var visited = new HashSet<long>();
            for (int step = 0; step < 9; step++)
            {
                StuffBase nearest = null;
                float distance = dist_belt_min * dist_belt_min;
                foreach (var candidate in candidates)
                {
                    if (candidate.typeId != StuffType.StuffCoin01 || visited.Contains(candidate.SpawnId)) continue;
                    float squareDistance = (candidate.transform.position - origin).sqrMagnitude;
                    if (squareDistance > distance) continue;
                    distance = squareDistance; nearest = candidate;
                }
                if (!nearest) break;
                visited.Add(nearest.SpawnId); selected.Add(nearest.SpawnId);
                origin = nearest.transform.position; points.Add(origin + Vector3.up * .1f);
            }
            lineRenderer.positionCount = 1; lineRenderer.SetPosition(0, points[0]);
            for (int i = 0; i < selected.Count; i++)
            {
                yield return new WaitForSeconds(.1f);
                if (session != itemSessionVersion) yield break;
                lineRenderer.positionCount = i + 2; lineRenderer.SetPosition(i + 1, points[i + 1]);
                // Stable spawn ids prevent collecting a recycled object or paying twice.
                if (HasItemAuthority) Items.Collect(selected[i], effect.OwnerPlayerId);
            }
            float elapsed = 0;
            while (elapsed < .8f && session == itemSessionVersion)
            {
                elapsed += Time.deltaTime;
                effect.LineMaterial.color = Color.yellow * Mathf.Lerp(5f, .1f, elapsed / .8f);
                yield return null;
            }
            effect.DeactiveDelay();
        }

        public void AddEff04()
        {

        }


        void PrepareCoins()
        {
            GameObject coin;
            for (int i = 0; i < maxCoins; i++)
            {
                coin = Instantiate(animatedCoinPrefab);
                animatedCoins.Add(coin);
                coin.transform.parent = transform;
                coin.SetActive(false);
                coinsQueue.Enqueue(coin);
            }
        }


        void CoinPickUpAnimate(Vector3 collectedCoinPosition, int amount, int playerId)
        {
            if (playerId < 0 || playerId >= targetCoin.Length || !targetCoin[playerId]) return;
            Transform _targetCoin = targetCoin[playerId];
            for (int i = 0; i < amount; i++)
            {
                //check if there's coins in the pool
                if (coinsQueue.Count > 0)
                {
                    //extract a coin from the pool
                    GameObject coin = coinsQueue.Dequeue();
                    coin.SetActive(true);
                    //move coin to the collected coin pos
                    coin.transform.position = collectedCoinPosition + new Vector3(UnityEngine.Random.Range(-spread, spread), 0f, 0f);

                    //animate coin to target position
                    float duration = UnityEngine.Random.Range(minAnimDuration, maxAnimDuration);
                    //Debug.Log($"COIN Animate :::::: {coin.name}, duration : {duration}, start pos : {coin.transform.position}, end pos : {target.position}");
                    coin.transform.DOMove(_targetCoin.position, duration)
                    .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                    .SetEase(easeType)
                    .OnComplete(() =>
                    {
                        //executes whenever coin reach target position
                        coin.SetActive(false);
                        coinsQueue.Enqueue(coin);

                        // Score was committed at collection, never by this animation.
                        //Coins++;
                    });
                }
            }
        }


        public void AddCoins(Vector3 collectedCoinPosition, int amount)
        {
            int playerId = PoolPlayer.currentPlayer?.playerId ?? -1;
            AddCoins(collectedCoinPosition, amount, playerId);
            //Debug.Log("ADD COIN ~~~~~~~ " + collectedCoinPosition + ", target : " + target.position);
        }





        public void PlayCoinFeedback(Vector3 position, int amount, int playerId) => CoinPickUpAnimate(position, amount, playerId);

        public void AddCoins(Vector3 position, int amount, int playerId)
        {
            if (!HasItemAuthority || amount <= 0 || playerId < 0) return;
            if (Items.AwardBonus(playerId, checked(amount * 10))) CoinPickUpAnimate(position, amount, playerId);
        }

        #if UNITY_EDITOR
        private void Update()
        {

            //if (Input.GetKeyDown(KeyCode.Return))
            //{
            //    StartBallScatter();
            //}
            //if (Input.GetKeyDown(KeyCode.Space))
            //{
            //    //CueReady();
            //}
            if ((!replayTitle || !replayTitle.isFocused) && Input.GetKeyDown(KeyCode.Alpha2))
            {
                isTestshot = false;
                TestResetPre();
            }
            //if (Input.GetKeyDown(KeyCode.Alpha3))
            //{
            //    if (isTestshot) return; isTestshot = true;
            //    TestShotPre().Forget();
            //}

            //if (Input.GetKeyDown(KeyCode.Alpha4))
            //{
            //    if (isTestshot) return; isTestshot = true;
            //    TestShotPre(2).Forget();
            //}
            //if (Input.GetKeyDown(KeyCode.Alpha5))
            //{
            //    if (isTestshot) return; isTestshot = true;
            //    TestShotPre(3).Forget();
            //}
            //if (Input.GetKeyDown(KeyCode.Alpha6))
            //{
            //    TestStuffCreate();
            //}
        }
        #endif



        void TestStuffCreate()
        {
            drawStuffPos.StuffCreate(5, 1);
        }

        public void StartBallScatter()
        {
            if (PoolCoach.Instance.Configuration.Execution == Assets.Scripts.Often.MatchExecutionMode.OnlineMatch) return;
            ClearShotReplay();
            undoSnapshot = null; simulationVersion++;
            scatterPending = true;
            ScatterBallsAsync(++scatterVersion).Forget();
        }

        async UniTask ScatterBallsAsync(int version)
        {
            // PnlMatch.OnEnable can run before the balls' Awake methods.
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, this.GetCancellationTokenOnDestroy());
            if (version != scatterVersion) return;
            if (!externalItemSession) BeginLocalItemSession();

            float radius = 0f;
            foreach (BallC ball in ballcs)
            {
                var sphere = ball.GetComponent<SphereCollider>();
                Vector3 scale = ball.transform.lossyScale;
                radius = Mathf.Max(radius, sphere.radius * Mathf.Max(Mathf.Abs(scale.x),
                    Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            }
            // A small triangle centred on the cloth, with space between colliders.
            float formationRadius = (radius * 2f + 0.01f) /
                (2f * Mathf.Sin(Mathf.PI / ballcs.Length));
            Vector3 centre = (drawStuffPos.st.position + drawStuffPos.en.position) * 0.5f;
            float rotation = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            for (int i = 0; i < ballcs.Length; i++)
            {
                Rigidbody body = ballcs[i].body;
                float angle = rotation + i * Mathf.PI * 2f / ballcs.Length;
                Vector3 position = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * formationRadius;
                position.y = resetPos[i].y;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.position = position;
                ballcs[i].transform.position = position;
            }
            Physics.SyncTransforms();
            shotController.CuePutAside();
            await StartShot(new Impulse(Vector3.zero, Vector3.zero, Vector3.zero));
            if (version != scatterVersion) return;

            float minSpeed = Mathf.Max(0.1f, Mathf.Min(scatterMinSpeed, scatterMaxSpeed));
            float maxSpeed = Mathf.Max(minSpeed, scatterMaxSpeed);
            foreach (BallC ball in ballcs)
            {
                float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float speed = UnityEngine.Random.Range(minSpeed, maxSpeed);
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                ball.body.WakeUp();
                ball.body.AddForce(direction * (speed * ball.body.mass), ForceMode.Impulse);
            }
            inMove = true;
            scatterPending = false;
        }

        //async void StartShotScatter(Impulse impulse, Vector3 vec, float force)
        //{
        //}

        void TestResetPre()
        {
            if (!IsLocalPractice) return;
            //Debug.Log("TestResetPre");

            for (int i = 0; i < ballcs.Length; i++)
            {
                Rigidbody body = ballcs[i].body;
                Vector3 currentVelocity = body.linearVelocity;
                body.AddForce(-currentVelocity, ForceMode.VelocityChange);
                ballcs[i].transform.position = resetPosPre[i];
                ballcs[i].transform.rotation = resetQuatPre[i];
                body.Sleep();
                ballcs[i].body.position = resetPosPre[i];
            }
        }



        async UniTaskVoid TestShotPre(int k = 0)
        {
            switch (k)
            {
                case 0:
                    await StartShot(impulsePre);
                    break;
                //case 2:
                //    await StartShot2(impulsePre, pushNormalPre);
                //    break;
                //case 3:
                //    await StartShot3(impulsePre, pushNormalPre);
                //    break;

            }
        }



        public void CallBallMove(int ballId, Vector3 position, Vector3 velocity, Vector3 angularVelocity)
        {
            OnBallMove?.Invoke(ballId, position, velocity, angularVelocity);
        }

         
        public void WordBalloonSend(int id, string msg)
        {
            //Debug.Log($"WordBalloonSend id : {id} , msg : {msg}");
            OnWordBalloon?.Invoke(id, msg);
        }

        [Obsolete]
        private void OnDestroy()
        {
            PoolCoach.Release(this);
            DisposeClothPhysics();
            if (IsBallPlacement) { Time.timeScale = placementTimeScale; ShotCtrl.canControl = placementControl; }
            if (IsPracticeReplay) { Time.timeScale = replayTimeScale; ShotCtrl.canControl = replayControl; }
            foreach (var coin in animatedCoins) if (coin) { coin.transform.DOKill(); Destroy(coin); }
            Time.fixedDeltaTime = previousFixedDeltaTime;
            Physics.autoSimulation = previousAutoSimulation;
            Physics.bounceThreshold = previousBounceThreshold;
            Physics.sleepThreshold = previousSleepThreshold;
            Physics.defaultContactOffset = previousDefaultContactOffset;
            Physics.defaultSolverIterations = previousSolverIterations;
            Physics.defaultSolverVelocityIterations = previousSolverVelocityIterations;
        }

    }



}
