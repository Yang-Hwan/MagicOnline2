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



    public delegate void BallShotHandler<String>(string impulse);
    public delegate void BallMoveHandler<Int, Vector3>(int ballId, Vector3 position, Vector3 velocity, Vector3 angularVelocity);
    public delegate void BallHitBallHandler<Ball, Boolean>(Ball ball, Ball hitBall, bool inMove);
    public delegate void BallHitBoardHandler<Ball, Boolean>(Ball ball, bool inMove, CushionDir dir);
    public delegate void BallSleepHandler<Int, Vector3>(int ballId, Vector3 position);
  
    public delegate void BallStory<BallMovingDigest, Int>(BallMovingDigest txt1, BallMovingStory txt2, int cnt, Vector3 pos);
    public delegate void ChoiceStory<StoryIdentity, Vector3>(StoryIdentity idx, Vector3 pos);

    public partial class PhysicsMng : MonoBehaviour
    {
        
        public event Action<string> OnInitRandForce;
        public event BallShotHandler<string> OnStartShot;

        public event BallMoveHandler<int, Vector3> OnBallMove;
        public event BallHitBallHandler<Ball, bool> OnBallHitBall;
        public event BallHitBoardHandler<Ball, bool> OnBallHitBoard;
        public event BallSleepHandler<int, Vector3> OnBallSleep;
        public event Action<float> OnEndShot;
        public event BallShotHandler<string> OnStartReplayShot;
        public event Action<Ball, Impulse> OnMatchReplayShot;

        public event BallStory<BallMovingDigest, int> OnBallMovStory;
        public event ChoiceStory<StoryIdentity, Vector3> OnChoiceStory;

        public float moveTime { get; set; }
        public bool inMove { get; private set; }
        public bool endFromNetwork { get; set; }    // 상대로부터 종료 알림설정됨.


        public DrawStuffPos drawStuffPos;
        public CushionHit cushionHit;
        public Transform clothSpace;
        CancellationToken lifetimeToken;

        public Ball cuball { get; set; }
        public Ball[] balls;
        //private RandForce4 randForce4 { get; set; }

        public LineRenderer line;
        float dist_min = 0.02f;
        Vector3 pos_pre = Vector3.zero;






        [Header("Addition")]
        [SerializeField] public RingBall ringCue;
        [SerializeField] public GameObject[] ringBalls;
        [SerializeField] public Transform[] ballShadows;
        [SerializeField] public Vector3[] ballResetPos;



        public bool pre_isMyturn { get; private set; }

        private PnlMatch pnlMatch;
        CancellationTokenSource cancel;

        int ballLen = 4;
        Vector3[] _posInitLst = new Vector3[4];
        float _pos_y = 0.0285f;

        //[Obsolete]
        private void Awake()
        {
            lifetimeToken = this.GetCancellationTokenOnDestroy();
            Debug.Log($"{ this.GetType().Name } ================== ");
            line = GameObject.Find("Table/Addition/SupportLines/CueLinePath").GetComponent<LineRenderer>();
            ringCue = GameObject.Find("Table/Addition/Effects/RingCueBall").GetComponent< RingBall>();
            ringBalls = new GameObject[2];
            ringBalls[0] = GameObject.Find("Table/Addition/Effects/RingBall1");
            ringBalls[1] = GameObject.Find("Table/Addition/Effects/RingBall2");
            ballShadows = new Transform[4];
            ballShadows[0] = GameObject.Find("Table/Addition/Effects/BallShadow01").transform;
            ballShadows[1] = GameObject.Find("Table/Addition/Effects/BallShadow02").transform;
            ballShadows[2] = GameObject.Find("Table/Addition/Effects/BallShadow03").transform;
            ballShadows[3] = GameObject.Find("Table/Addition/Effects/BallShadow04").transform;

            ballResetPos = new Vector3[4];
            for (int i = 0; i < 4; i++) ballResetPos[i] = GameObject.Find($"Table/Addition/Position/Balls/ball_{i}").transform.position;

            CapturePhysicsSettings();
            Physics.simulationMode = SimulationMode.Script;
            //Physics.autoSimulation = false;

            inMove = false;
            Time.fixedDeltaTime = 0.01f;
            Physics.bounceThreshold = 0.01f;
            Physics.sleepThreshold = 0.01f;
            Physics.defaultContactOffset = 0.0005f;
            Physics.defaultSolverIterations = 1;
            Physics.defaultSolverVelocityIterations = 1;
            Transform table = GameObject.Find("Table").transform;
            drawStuffPos = GetComponent<DrawStuffPos>();
            cushionHit = table.Find("CushionHit").GetComponent<CushionHit>();
            clothSpace = table.Find("Collider/Bottom").GetComponent<Transform>();
            
            balls = new Ball[table.Find("Balls").childCount];
            for (int i = 0; i < balls.Length; i++)
            {
                balls[i] = table.Find("Balls").GetChild(i).GetComponent<Ball>();
                balls[i].ballShadow = ballShadows[i];
            }

            
            _posInitLst[0] = new Vector3(0, _pos_y, 0);
            _posInitLst[1] = new Vector3(-0.035f, _pos_y, 0.055f);
            _posInitLst[2] = new Vector3(0.035f, _pos_y, 0.055f);
            _posInitLst[3] = new Vector3(0, _pos_y, 0.100f);
        }

        public void SetBalls(int cnt)
        {
            ballLen = cnt;
            //Debug.Log($"PhysicsMng.SetBalls cnt : {cnt}");
            //Transform table = GameObject.Find("Table").transform;
            //balls = new Ball[cnt];

            //bool isLastBall = ballLen == 3 ? false : true;
            //balls[3].gameObject.SetActive(isLastBall);

            // InitRandPosFlutter .. 먼저 시작되었다면 패스
            if (inMove)
            {
                Debug.LogWarning($"SetBalls InitRandPosFlutter .. 먼저 시작되었다면 패스 myturn : {PoolPlayer.mainPlayer.myTurn}");
                return; 
            }
            for (int i = 0; i < 4; i++)
            {
                PlaceBall(i, ballResetPos[i]);
                //Debug.Log($"PhysicsMng.SetBalls {i} : {ballResetPos[i]}");
            }

            for (int i = 0; i < ballLen; i++)
            {
                PlaceBall(i, _posInitLst[i]);
            }

        }

        private void OnEnable()
        {
            Debug.Log($"Physics OnEnable...");
            if (!NetworkManager.initialized)
            {
                enabled = false;
                return;
            }
            pnlMatch = pnlMatch??PnlMatch.FindObjectOfType<PnlMatch>();
        }

        private void OnDisable()
        {
            Debug.Log($"Physics OnDisable...");
            if (cancel != null)
            {
                cancel.Cancel();
            }
        }



        bool CheckIsSleeping(bool forceSleep)
        {
            return PracticeBallsSettled();
        }

        public void DrawCueLinePath(Vector3 pos_cur)
        {
            // 공 뿌릴때는 라인 안 그림
            if (!PoolCoach.Instance.isMatchTimePlay) return;
            
            pos_cur.y = 0f;
            if (line.positionCount < 2) return;
            line.SetPosition(line.positionCount - 1, pos_cur);
            if (Vector3.Distance(pos_cur, pos_pre) > dist_min)
            {
                line.positionCount++;
                line.SetPosition(line.positionCount - 1, pos_cur);
                pos_pre = pos_cur;
            }
        }

 
        async UniTaskVoid Start()
        {
            InitializePracticePhysics();
            cancel = new CancellationTokenSource();
            
            while (true)
            {
                await UniTask.WaitForFixedUpdate(cancellationToken: cancel.Token);

                if (inMove)
                {
                    moveTime += Time.fixedDeltaTime;
                    if (PoolLogic.controlInNetwork)
                    {
                        if (CheckIsSleeping(false))
                        {
                            await UniTask.WaitForSeconds(0.1f, cancellationToken: cancel.Token);
                            if (CheckIsSleeping(true))
                            {
                                StopMove().Forget();
                            }

                        }
                    }
                }
            }
        }



        async UniTaskVoid StopMove()
        {
            lifetimeToken.ThrowIfCancellationRequested();
            inMove = false;
            for (int i = 0; i < ballLen; i++)
            {
                Ball ball = balls[i];
                if (!ball.body.isKinematic)
                {
                    ball.body.linearVelocity = Vector3.zero;
                    ball.body.angularVelocity = Vector3.zero;
                    ball.body.Sleep();
                }
                OnBallSleep?.Invoke(ball.id, ball.body.position);
            }

            await UniTask.WaitForSeconds(1f, cancellationToken: lifetimeToken);

            //if (PoolLogic.controlInNetwork)
            //{
            if (drawStuffPos.effectIng > 0)
                {
                    while (drawStuffPos.effectIng > 0)
                    {
                        Debug.LogWarning($"ON drawStuffPos.isEffectIng TRUE ... myturn : {PoolPlayer.mainPlayer.myTurn} ...  drawStuffPos.effectIng  :  {drawStuffPos.effectIng }");
                        await UniTask.WaitForFixedUpdate(cancellationToken: lifetimeToken);
                    }
                    await UniTask.WaitForSeconds(1f, cancellationToken: lifetimeToken);
                }

            if (drawStuffPos.effectStuffEa > 0)
            {
                while (drawStuffPos.effectStuffEa > 0)
                {
                    Debug.LogWarning($"ON drawStuffPos.effectStuffEa TRUE ... myturn : {PoolPlayer.mainPlayer.myTurn} ...  drawStuffPos.effectStuffEa  :  {drawStuffPos.effectStuffEa }");
                    await UniTask.WaitForFixedUpdate(cancellationToken: lifetimeToken);
                }
                await UniTask.WaitForSeconds(.5f, cancellationToken: lifetimeToken);
            }
            //}

            pre_isMyturn = PoolPlayer.mainPlayer.myTurn;

            //Debug.Log($"PhysicsMng.StopMove  PoolLogic.gameState.gameIsComplete : {PoolLogic.gameState.gameIsComplete}");
            //Debug.Log($"StopMove MYTURN : {PoolLogic.controlInNetwork}  =================================================== ");

            OnEndShot?.Invoke(moveTime);

            moveTime = 0.0f;
            FadeLinePath().Forget();


        }


        void FixedUpdate() => StepPracticePhysics();

        public void InitRandForceFromNetwork(string randForce)
        {
            if (PoolLogic.controlFromNetwork)
            {
                //pnlMatch.PoolCoach_OnSetGameInfo($"[상대]에게 메시지 시작 뿌리기 받음.");
                InitRandPosFlutter(randForce);
            }
        }

        //public void InitRandForceReplay(string randForce)
        //{
        //    OnInitRandForce?.Invoke(randForce);     // PnlMatch : 보냄 
        //}

        public void InitRandPosFlutter(string rf4_str = "")
        {
            inMove = true;
            moveTime = 0;
            endFromNetwork = false;



            Vector3[] force = new Vector3[4];
            RandForce4 rf4;
            if (PoolLogic.controlInNetwork)
            {
                float x = UnityEngine.Random.Range(-0.9f,  0.9f);
                float z = UnityEngine.Random.Range(-0.18f, 0.28f);
                force[3] = Vector3.zero;
                for (int i = 0; i < ballLen; i++)
                {
                    Vector3 rnd = new Vector3(x, 0, z);
                    float randomForce = UnityEngine.Random.Range(0.8f, 1.8f);
                    force[i] = rnd  * randomForce;
                }
                rf4 = new RandForce4(force[0], force[1], force[2], force[3]);

            }
            else
            {
                rf4 = DataManager.RandForceFromString(rf4_str);
                //Debug.Log($"InitRandPosFlutter myturn false .. rf4.lst[3] : {rf4.lst[3]}");
                if (rf4.lst[3] == Vector3.zero)
                {
                    ballLen = 3;
                    PlaceBall(3, ballResetPos[3]);
                }
            }

            for (int i = 0; i < ballLen; i++)
            {
                PlaceBall(i, _posInitLst[i]);
            }


            //Debug.Log($"InitRandPosFlutter myturn : {PoolLogic.controlInNetwork} .. ballLen : {ballLen}");

            for (int i = 0; i < ballLen; i++)
            {
   
                balls[i].body.AddForce(rf4.lst[i], ForceMode.Impulse);

                //Debug.Log($"InitRandPosFlutter myturn : {PoolLogic.controlInNetwork} .. {i} : force : {rf4.lst[i]}");
            }
   

            // 자신의 턴일때 흩뿌림정보를 상대에게 보냄
            if (PoolLogic.controlInNetwork)
            {
                OnInitRandForce?.Invoke(DataManager.RandForceToString(rf4));     // PnlMatch : 보냄 
                //InitRandForceReplay(DataManager.RandForceToString(rf4));
            }

            // 모든 볼들 상태값및 시간처리
            OnStartShot?.Invoke("InitRandPosFlutter");
        }


        //void InitBallFlutterStart(Ball ball, Vector3 force)
        //{
        //    ball.body.AddForce(force, ForceMode.Impulse);
        //}




        public void CallBallMove(int ballId, Vector3 position, Vector3 velocity, Vector3 angularVelocity)
        {
            OnBallMove?.Invoke(ballId, position, velocity, angularVelocity);
        }

        public void CallBallHitBall(Ball ball, Ball hitBall, bool isMove)
        {
            OnBallHitBall?.Invoke(ball, hitBall, isMove);
        }
        
        public void CallBallHitBoard(Ball ball, bool isMove, CushionDir dir)
        {
            OnBallHitBoard?.Invoke(ball, isMove, dir);
        }

        // 상대로부터 모든공 정지되었으니 모든 볼 정지하기요청 받음.
        public async UniTaskVoid WaitAndStopMoveFromNetwork(float time)
        {
            lifetimeToken.ThrowIfCancellationRequested();
            while(moveTime < time)
            {
                await UniTask.WaitForFixedUpdate(cancellationToken: lifetimeToken);
            }

            StopMove().Forget();
            endFromNetwork = true;
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

            for (int i = 0; i < ballLen; i++)
            {
                Ball ball = balls[i];
                if (ball.isCueBall)
                {
                    //Debug.Log($"SetBallSign cueball id : {ball.id} <<<< --------------------- cueball.name : {ball.name}  ");
                    ang_rnd = UnityEngine.Random.Range(0, 360);
                    radian = ang_rnd * Mathf.Deg2Rad;
                    qua = Quaternion.Euler(90, ang_rnd, 0);

                    ringCue.gameObject.SetActive(true);
                    ringCue.transform.position = ball.transform.position;
                    ringCue.transform.localRotation = qua;
                    ringCue.ZeroVal();
                    break;
                }
            }

            for (int i = 0; i < ballLen; i++)
            {
                Ball ball = balls[i];
                ang_rnd = UnityEngine.Random.Range(0, 360);
                radian = ang_rnd * Mathf.Deg2Rad;
                qua = Quaternion.Euler(90, ang_rnd, 0);

                if (ball.isCueBall) continue;
                if(ballLen == 4)
                {
                    if (i == 0 || i == 1) continue;
                }
                ringBalls[ring_idx].SetActive(true);
                ringBalls[ring_idx].transform.position = ball.transform.position;
                ringBalls[ring_idx].transform.localRotation = qua;
                ring_idx++;
            }

 


        }

        public void StartShot(Ball cueBall, Impulse impulse, string info)
        {
            // 시합종료일때는 자신과 상대에게 데이터이동이 없게 한다.
            if (PoolCoach.Instance.isMatchTimeEnd)
            {
                Debug.Log($"PhysicsMng.StartShot  if (PoolCoach.Instance.isMatchTimeEnd)");
                return;
            }

            line.positionCount = 0;
            inMove = true;
            moveTime = 0.0f;
            ringCue.gameObject.SetActive(false);
            ringBalls[0].SetActive(false);
            ringBalls[1].SetActive(false);

            settledTime = 0; trailVersion++;
            pos_pre = cueBall.body.position; pos_pre.y = 0;
            line.positionCount = 2; line.SetPosition(0, pos_pre); line.SetPosition(1, pos_pre);
            foreach (var ball in balls)
            {
                ball.strokeFollowThrough = ball == cueBall ? impulse.followThrough : -1;
                ball.strokeSpinPersistence = ball == cueBall ? impulse.spinPersistence : -1;
            }
            cueBall.body.AddForceAtPosition(impulse.impulse, impulse.point, ForceMode.Impulse);
            OnMatchReplayShot?.Invoke(cueBall, impulse);

            //Debug.Log($"PhysicsMng.StartShot  myturn : {PoolLogic.controlInNetwork} ... PoolLogic.gameState.gameIsComplete : {PoolLogic.gameState.gameIsComplete}");
            //Debug.Log($"PhysicsMng StartShot ==== ");
            OnStartShot?.Invoke(info);
        }

        public void StartReplayShot(string impulse)
        {
            OnStartReplayShot?.Invoke(impulse);
        }


        public void StartShotFromNetwork(string impulse)
        {
            StartReplayShot(impulse);
        }

        public void CusionHit(CushionDir dir)
        {
            
            cushionHit.HitDir(dir);
            //Debug.Log($"CusionHit : 1");
            ObjectPooler.instance.SpawnFromPool(ObjectPool.Cosmos.ToString(), Vector3.zero);
            if (PoolLogic.controlInNetwork)
            {
                drawStuffPos.StuffCreate(8, 3);
            }

        }


        public void CosmosAllDeactive()
        {
            List<CosmosCtrl> cosmosCtrls = ObjectPooler.instance.GetActivePools<CosmosCtrl>(nameof(CosmosCtrl));
            Debug.Log($"CosmosAllDeactive.cosmosCtrls.Count : {cosmosCtrls.Count}");
            for (int i = 0; i < cosmosCtrls.Count; i++)
            {
                cosmosCtrls[i].DeactiveDelay();
            }
        }

        public void CallBallMovStory(BallMovingDigest digest, BallMovingStory story, int cnt, Vector3 pos)
        {
            if (!PoolCoach.Instance.isMatchTimePlay) return;
            OnBallMovStory?.Invoke(digest, story, cnt, pos);
        }


        public async UniTaskVoid SetBallMovStoryFromNetwork(float netMoveTime, string storyData)
        {
            lifetimeToken.ThrowIfCancellationRequested();
            if (storyData == "") return;
            while (!endFromNetwork && moveTime < netMoveTime)
            {
                await UniTask.WaitForFixedUpdate(cancellationToken: lifetimeToken);
            }

            string[] values = DataManager.ConvertDataToStringArray(storyData);
            StoryIdentity idx = (StoryIdentity)(int.Parse(values[0]));
            Vector3 pos = DataManager.Vector3FromString(values[1]);

            OnChoiceStory?.Invoke(idx, pos);
           // CallBallMovStory(digest, story, cnt, pos);

        }



        public void SetBallsSync()
        {
            for (int i = 0; i < ballLen; i++)
            {
                //Debug.Log($"PhysicsMng.SetBallsSync {i} : GetSync ");
                balls[i].OnState(BallState.GetSync);
            }
        }


        async UniTaskVoid FadeLinePath()
        {
            lifetimeToken.ThrowIfCancellationRequested();
            int version = trailVersion;
            await UniTask.WaitForSeconds(3f, cancellationToken: lifetimeToken);
            if (version == trailVersion && line.positionCount > 0)
            {
                line.positionCount = 0;
                //Debug.Log("FadeLinePath");
            }
        }


    }
}
