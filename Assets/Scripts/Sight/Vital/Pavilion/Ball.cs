using Assets.Scripts.Exert.Match;
using Assets.Scripts.Exert.Network;
using Assets.Scripts.Often;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.Scripts.Sight.Vital.Pavilion
{
    public partial class Ball : MonoBehaviour
    {
        public event System.Action<Vector3> OnCueBallMoveScreen;

        [SerializeField] private float _ballMass = 0.15f;
        [SerializeField] private float _ballMaxVelocity = 7;
        [SerializeField] private float _ballMaxAngularVelocity = 300f;
        [SerializeField] private float _drag = 0.3f;
        [SerializeField] private float _angularDrag = 0.2f;

        public int id;// { get; private set; }
        public bool isCueBall;// { get; private set; }
        public Rigidbody body { get; private set; }
        public PhysicsMng physicsMng { get; private set; }
        public Transform ballShadow;// { get; set; }
        bool inMove { get; set; }
        public float radius { get; private set; }

        public int boardLayer { get; set; }
        public int ballLayer { get; set; }
        public int cueBallLayer { get; set; }

        public float ballRadius { get; private set; }
        public Vector3 pos_pre { get; private set; }
        private BallState state;
        private List<BallMovingDigest> ballMovDigest;
        private List<BallMovingStory> ballMovStory;

        private Vector3[] target_pre_pos;
        private int target_pre_idx;
        private float remainTimePredicatePos = 0;
        private float WaitTimePredicatePos = 2f;

        private BallMovingDigest edge_state;
        private BallMovingDigest edge_state_old;
        public Ball edge_targetBall { get; private set; }
        float Edge_Force_Weaken_Max = 0.7f;                 // 
        float Edge_Force_Weaken_Min = 0.1f;                 // 
        float edge_target_dist_old = 10f;
        public bool is_edge_state_first;


        public ShotCtrl shotController;
        public LineRenderer[] cueBallLines;

        //// 비껴치기 판단 참조 
        //private AsideHitStep asideHitStepRet = AsideHitStep.None;
        //private bool isAsideHitRet = false;
        //private StateCheck isAsideHitChk = StateCheck.None;                     // 0 : 검토안함, 1 : 검토필요(STEP 3), 2 : 검토완료(STEP 4)               
        //private Vector3[] posAside = new Vector3[2];            //  STEP 1
        //private Ball ballAside = null;                         //  STEP 1
        //private CushionDir cushionDirAside = CushionDir.None;   //  STEP 2

        //private float AsideHit_Ball_Cushion_Dist_Max = 0.21f;


        public string moveData { get; private set; }
        private float savedTime = -1.0f;



        public struct MechanicalState
        {
            public readonly float time;
            public readonly int pocketId;
            public int stuffId;
            public int hitShapeId;
            public readonly Vector3 position;
            public readonly Vector3 velocity;
            public readonly Vector3 angularVelocity;

            public MechanicalState(float time, int pocketId, int stuffId, int hitShapeId, Vector3 position, Vector3 velocity, Vector3 angularVelocity)
            {
                this.time = time;
                this.pocketId = pocketId;
                this.stuffId = stuffId;
                this.hitShapeId = hitShapeId;
                this.position = position;
                this.velocity = velocity;
                this.angularVelocity = angularVelocity;
            }

            public static string StateToString(MechanicalState state)
            {
                return "[" + state.time.ToString4() + ";" + state.pocketId + ";" + state.stuffId + ";" + state.hitShapeId + ";" + DataManager.Vector3ToString(state.position) + ";" + DataManager.Vector3ToString(state.velocity) + ";" + DataManager.Vector3ToString(state.angularVelocity) + "]\n";
            }

            public static MechanicalState StateFromString(string state)
            {
                if (state == "")
                {
                    return new MechanicalState();
                }

                string[] values = DataManager.ConvertDataToStringArray(state);
                float time = values[0].ToFloat4();
                int pocketId = int.Parse(values[1], System.Globalization.NumberStyles.Integer);
                int stuffId = int.Parse(values[2], System.Globalization.NumberStyles.Integer);
                int hitShapeId = int.Parse(values[3], System.Globalization.NumberStyles.Integer);
                Vector3 position = DataManager.Vector3FromString(values[4]);
                Vector3 velocity = DataManager.Vector3FromString(values[5]);
                Vector3 angularVelocity = DataManager.Vector3FromString(values[6]);

                return new MechanicalState(time, pocketId, stuffId, hitShapeId, position, velocity, angularVelocity);

            }

        }


        private bool NeedToSave()
        {
            return false;
            //double gap = Math.Round(Vector3.Distance(new Vector3(position.x, 0, position.z), savedPosition), 4);
            //if (savedTime != listener.physicsManager.moveTime)
            //{
            //    if (gap > minGap)
            //    {
            //        if (saveChk > 10)
            //        {
            //            saveChk = 0;
            //            savedTime = listener.physicsManager.moveTime;
            //            savedPosition = new Vector3(position.x, 0, position.z);
            //            return true;
            //        }
            //        else
            //        {
            //            saveChk++;
            //        }
            //    }
            //    else
            //    {
            //        //    Debug.Log("BALL SLEEP ID : " + id + ", savedPosition : " + savedPosition);
            //    }

            //}

            //return false;
        }




        private void Awake()
        {
            //DontDestroyOnLoad(this.gameObject);
            if (!NetworkManager.initialized)
            {
                enabled = false;
                return;
            }

            boardLayer = LayerLib.NameToInt(LayerKind.Board);
            ballLayer = LayerLib.NameToInt(LayerKind.Ball);
            cueBallLayer = LayerLib.NameToInt(LayerKind.CueBall);
            body = GetComponent<Rigidbody>();
            body.linearDamping = _drag;
            body.angularDamping = _angularDrag;
            body.mass = _ballMass;
            body.maxDepenetrationVelocity = _ballMaxVelocity;
            body.maxAngularVelocity = _ballMaxAngularVelocity;
            body.Sleep();

            radius = body.GetComponent<SphereCollider>().radius;
            physicsMng = PhysicsMng.FindObjectOfType<PhysicsMng>();

            ballMovDigest = new List<BallMovingDigest>();
            ballMovStory = new List<BallMovingStory>();
            ballRadius = 0.7f * transform.lossyScale.x;
            shotController = FindObjectOfType<ShotCtrl>();

        }

        private void Start()
        {
            cueBallLines = shotController.cueBallLines;

            //ballShadow.position = CalculateBallShadowPosition();
        }

        Vector3 CalculateBallShadowPosition()
        {
            Vector3 positionInCloth = new Vector3(transform.position.x, 0.01f * radius, transform.position.z);
            return positionInCloth + 0.007f * radius * transform.position;
        }

        public void SetBallShadowAndBlickBlick()
        {
            ballShadow.position = CalculateBallShadowPosition();
        }

        public void SetCueBall(bool isCueBall)
        {
            this.isCueBall = isCueBall;
            gameObject.layer = isCueBall ? LayerMask.NameToLayer(LayerKind.CueBall.ToString()) : LayerMask.NameToLayer(LayerKind.Ball.ToString());
        }


        public void OnState(BallState state)
        {
            this.state = state;
            //if (isCueBall)
            //{
            //    Debug.Log($"BALL OnState id : {id}, state : {state}");
            //}

            switch (state)
            {
                case BallState.GetSync:

                    moveData = mechanicalStateData;
                    NetworkManager.network.SendRemoteMessage(nameof(NetworkManager.network.SetMechanicalStatesFromNetwork), id, mechanicalStateData);
                    break;
                case BallState.SetState:
                    pos_pre = transform.position;

                    pocketId = -1;
                    stuffId = -1;
                    hitShapeId = -2;
                    moveData = mechanicalStateData;

                    ballMovDigest.Clear();
                    ballMovStory.Clear();
                    edge_state = BallMovingDigest.None;                  
                    edge_state_old = BallMovingDigest.None;                  
                    edge_targetBall = null;
                    edge_target_dist_old = 10f;
                    is_edge_state_first = false;

                    if (PoolLogic.controlInNetwork)
                    {
                        NetworkManager.network.SendRemoteMessage(nameof(NetworkManager.network.SetMechanicalStatesFromNetwork), id, mechanicalStateData);
                    }
                    break;
                case BallState.StartMove:
                    pos_pre = transform.position;

                    break;
                case BallState.Move:
                    hitShapeId = -2;
                    position = body.position;
                    transform.rotation = body.rotation;

                    if (isCueBall)
                    {
                        if (PoolLogic.controlInNetwork)
                        {
                            // 속도가 느려졌을때 반응처리
                            EdgeStateAct();
                        }
                        
                    }

                    break;
                case BallState.HitBall:


                    

                    //Debug.Log($"BALL SEND HitBall id : {id} >>>>>>>>> savedTime : {savedTime} , physicsMng.moveTime : {physicsMng.moveTime} ");
                    if (savedTime != physicsMng.moveTime)
                    {
                        savedTime += physicsMng.moveTime;
                        moveData += mechanicalStateData;
                        if (PoolLogic.controlInNetwork)
                        {
                            NetworkManager.network.SendRemoteMessage(nameof(NetworkManager.network.SetMechanicalStatesFromNetwork), id, mechanicalStateData);
                        }
                    }
                    break;
                case BallState.HitBoard:
                    //Debug.Log($"BALL SEND HitBoard id : {id} >>>>>>>>> savedTime : {savedTime} , physicsMng.moveTime : {physicsMng.moveTime} , mechanicalStateData : {mechanicalStateData}");
                    if (savedTime != physicsMng.moveTime)
                    {
                        savedTime += physicsMng.moveTime;
                        moveData += mechanicalStateData;
                        if (PoolLogic.controlInNetwork)
                        {
                            NetworkManager.network.SendRemoteMessage(nameof(NetworkManager.network.SetMechanicalStatesFromNetwork), id, mechanicalStateData);
                        }
                    }
                    break;
                case BallState.EndMove:
                    moveData += mechanicalStateData;
                    //Debug.Log($"BALL SEND EndMove id : {id} >>>>>>>>>   ");
                    if (PoolLogic.controlInNetwork)
                    {
                        NetworkManager.network.SendRemoteMessage(nameof(NetworkManager.network.SetMechanicalStatesFromNetwork), id, mechanicalStateData);
                    }



                    if (isCueBall && PoolLogic.controlInNetwork)
                    {
                        string msg = string.Empty;
                        for (int i = 0; i < ballMovDigest.Count; i++)
                        {
                            msg += $"> {ballMovDigest[i]} ";
                        }

                        if (msg != string.Empty)
                        {
                        //    Debug.Log($"DIGEST ==>> id : {id},  ___ {msg}");
                        }
                        msg = string.Empty;
                        for (int i = 0; i < ballMovStory.Count; i++)
                        {
                            msg += $"> {ballMovStory[i]} ";
                        }
                        if (msg != string.Empty)
                        {
                        //    Debug.Log($"STORY ==>> id : {id}, ___ {msg}");
                        }

                    }

                    break;

            }
        }


        public string mechanicalStateData
        {
            get
            {
                MechanicalState state = new MechanicalState(DataManager.CutValue(physicsMng.moveTime),
                    pocketId, stuffId, hitShapeId, position, body.linearVelocity, body.angularVelocity);
                return MechanicalState.StateToString(state);
            }
            set
            {
                string val = value;
                MechanicalState state = MechanicalState.StateFromString(val);

                pocketId = state.pocketId;
                stuffId = state.stuffId;
                position = state.position;
                
                body.linearVelocity = state.velocity;
                body.angularVelocity = state.angularVelocity;
                 
            }
        }


        public async UniTaskVoid SetMechanicalStatesFromNetwork(string state)
        {
            MechanicalState mState = MechanicalState.StateFromString(state);

            if (!physicsMng.inMove && physicsMng.moveTime == 0)
            {
                mechanicalStateData = state;
                OnState(BallState.SetState);
                //Debug.Log($"ball id : {id} .. MYTURN : {PoolLogic.controlInNetwork} OnState : {this.state} .. {physicsMng.inMove} .. {physicsMng.pre_isMyturn} .. physicsMng.endFromNetwork : {physicsMng.endFromNetwork} ..  physicsMng.moveTime : { physicsMng.moveTime} .. mState.time : {mState.time} .... state : {state}");
            }

      
            //listener.physicsManager.CallOnPuuu(id);
            while (!physicsMng.endFromNetwork && physicsMng.moveTime < mState.time)
            {
                await UniTask.WaitForFixedUpdate();
            }
            if (!physicsMng.endFromNetwork)
            {
                FollowMoveFromNetwork(mState);
                mechanicalStateData = state;
                //Debug.Log($"SetMechanicalStatesFromNetwork id : {id} .... state : {state}"); 
            }
        }

        void FollowMoveFromNetwork(MechanicalState mState)
        {
            //Debug.Log($"FollowMoveFromNetwork HIT ID : {id} .. mState.hitShapeId : {mState.hitShapeId},  mState.position : {mState.position}");
            if (mState.hitShapeId >= 100)
            {
                OnHitBoard(mState.hitShapeId);
            }
            else if (mState.hitShapeId >= 0)
            {
                OnHitBall(physicsMng.balls[mState.hitShapeId]);
            }
        }


        #region Listener  ####################################################################################

        public int hitShapeId { get; set; }
        public int pocketId { get; set; }
        public int stuffId { get; set; }
        public Vector3 position 
        { 
            get
            {
                SetBallShadowAndBlickBlick();
                return body.position;
            }
            set
            {
                transform.position = value;
                body.position = value;
            }
        }

        public void OnCollisionExit(Collision collision)
        {


            //Debug.Log($"OnCollisionExit id : {id} .. controlFromNetwork : {PoolLogic.controlFromNetwork}");
            if (PoolLogic.controlFromNetwork)
            {
                return;
            }

            int hit_layer = collision.collider.gameObject.layer;
            if (hit_layer == LayerMask.NameToLayer(nameof(LayerKind.CueBall)) || 
                hit_layer == LayerMask.NameToLayer(nameof(LayerKind.Ball)))
            {
                Ball ball = collision.collider.GetComponent<Ball>();
                //Debug.Log($"OnCollisionExit id : {id} ----------- OnHit Ball");
                OnHitBall(ball);
            }
            else if(hit_layer == LayerMask.NameToLayer(nameof(LayerKind.Board)))
            {
                CushionDir dir = (CushionDir)Enum.Parse(typeof(CushionDir), collision.collider.gameObject.name.Split('_')[1]);
                int boardId = (int)dir;
                OnHitBoard(boardId);

                 //Debug.Log($"OnCollisionExit id : {id} ----------- OnHit Boardid int : {(int)dir}, str : {dir}");
                
            }

            pos_pre = transform.position;           // 모든공은 보드, 볼충돌시 위치 변경됐을때 충돌직후 위치 저장

        }

        public void OnTriggerEnter(Collider other)
        {
            if (PoolLogic.controlFromNetwork)
            {
                return;
            }

            int hit_layer = other.gameObject.layer;
            int turnId = PoolPlayer.turnId;
            // 코인 먹음
            if(hit_layer == LayerMask.NameToLayer(nameof(LayerKind.Stuff)))
            {
                other.GetComponent<StuffBase>().BallTouch(turnId, 10);
            }
            else if (hit_layer == LayerMask.NameToLayer(nameof(LayerKind.Item)))
            {
                other.GetComponent<StuffBase>().ItemTouch();
            }
        }



        public void OnHitBall(Ball ball)
        {
            hitShapeId = ball.id;

            if (isCueBall)
            {
                if (PoolLogic.controlInNetwork)
                {
                    if (PoolCoach.Instance.isMatchTimePlay)
                    {
                        // 배팅 성공이 아닌 상태
                        if (!PoolLogic.gameState.shotAchieve)
                        {
                            if (PoolLogic.gameState.ballHitBall_1 == hitShapeId)
                            {
                                Debug.Log($"ballHitBall_1 : {PoolLogic.gameState.ballHitBall_1}, hitShapeId : {hitShapeId}");
                                if (!ballMovDigest.Contains(BallMovingDigest.Kiss))
                                {
                                    edge_state = BallMovingDigest.Kiss;
                                    remainTimePredicatePos = 1.0f;
                                    //movStory.Add(edge_state);
                                }
                            }
                        }
                    }
                }
            }


            physicsMng.CallBallHitBall(this, ball, true);

            // 로직 처리 후 
            if (isCueBall)
            {
                if (PoolLogic.controlInNetwork)
                {
                    if (PoolCoach.Instance.isMatchTimePlay)
                    {
                        // 배팅 성공
                        if (PoolLogic.gameState.shotAchieve)
                        {
                            if (!ballMovDigest.Contains(BallMovingDigest.Achieve))
                            {
                                edge_state = BallMovingDigest.Achieve;
                            }
                        }
                    }
                }
            }

        }

        public void OnHitBoard(int boardId)
        {

            hitShapeId = boardId;
            CushionDir dir = (CushionDir)boardId;
            physicsMng.CallBallHitBoard(this, true, dir);
        }

        void FixedUpdate()
        {
            if (!body.isKinematic && !body.IsSleeping() && physicsMng.inMove)
            {
                if (isCueBall)
                {
                    physicsMng.DrawCueLinePath(body.position);
                    //Debug.Log($"SetMechanicalStatesFromNetwork ball id : {id} ====time : {mState.time}__ pocketId : {mState.pocketId} __ hitShapeId : {mState.hitShapeId}__ position : {mState.position} __ velocity : {mState.velocity}__ ANG : {mState.angularVelocity} ");
                }

                SetBallShadowAndBlickBlick();
                //Debug.Log($"id : {id}, pos : {body.position}, vel : {body.velocity}");
                physicsMng.CallBallMove(id, body.position, body.linearVelocity, body.angularVelocity);
            }

            if(inMove != physicsMng.inMove)
            {
                inMove = physicsMng.inMove;
                if(!inMove && !body.isKinematic)
                {
                    body.Sleep();
                }
            }
        }
        #endregion


        void FindPredicateMoveLine(bool isBallChk = false)
        {
            if (PoolLogic.gameState.shotAchieve)
            {
                edge_state = BallMovingDigest.End;
                return;
            }

            Vector3 origin = transform.position;
            //Debug.Log($"FindPredicateMoveLine move id : {id}, origin : {origin}, pos_pre : {pos_pre}");
            Vector3 direction = Vector3.ProjectOnPlane(origin - pos_pre, Vector3.up).normalized;
            float lineLength = 1.6f;
            float radius = isBallChk ? 2.4f * transform.lossyScale.x : 1.9f * transform.lossyScale.x;
            int find_ball_cnt = -1;
            Vector3[] find_pos_lst = new Vector3[5];
            Utility.DrawBallHitLine(cueBallLines, radius, ballLayer, boardLayer, ref find_pos_lst, ref find_ball_cnt, edge_targetBall.id, 0, origin, direction, lineLength, false);

            // 찾았다면 일정시간동안은 조회 안한다.
            if (find_ball_cnt != -1)
            {
                // 쿠션없이 타겟볼과 첫 충돌예상이라면 초기화
                if (!ballMovDigest.Contains(BallMovingDigest.TargetBallLook))
                {

                }
                if (find_ball_cnt == 0)
                {
                    edge_state = BallMovingDigest.TargetBallLook;
                }
                else
                {
                    edge_state = BallMovingDigest.TargetPosLook;
                }
                //Debug.Log($"MoveLine Ball find_ball_cnt : {find_ball_cnt}, Pos Find : {find_pos_lst[find_ball_cnt]}  .. edge_state : {edge_state}   *********************************************** ");

                edge_target_dist_old = 100f;

                // 슛성공확률이 높은상태.
                target_pre_pos = find_pos_lst;
                target_pre_idx = find_ball_cnt;
                remainTimePredicatePos = WaitTimePredicatePos;

                //Debug.Log($"FindPredicateMoveLine isBallChk : {isBallChk},... target_pre_idx : {target_pre_idx}");
            }
            else
            {


                //edge_state = StateWeaken.TargetNone;


            }
        }

        // 이동중 
        void EdgeStateAct()
        {

            if (body.linearVelocity.magnitude < Edge_Force_Weaken_Max)
            {
                switch (edge_state)
                {
                    case BallMovingDigest.None:
                        edge_state = BallMovingDigest.Start;
                        break;
 
    
                    case BallMovingDigest.Start:
                        //msg = "";
                        
                        edge_state = PoolLogic.gameState.shotAchieve ? BallMovingDigest.End : BallMovingDigest.TargetFind;
                        //physicsManager.WordBalloonSend(id, "힘내~");
                        break;
                    case BallMovingDigest.TargetFind:
                        edge_targetBall = PoolLogic.Instance.GetTargetBall;  // 슛성공안된상태에서  한개충돌했고 충돌될 나머지 한개공 찾기 
                        int targetId = edge_targetBall?.id ?? -1;
                        //physicsManager.WordBalloonSend(id, msg);
                        //msg = $"타겟:{targetId}";

                        if (edge_targetBall != null)
                        {
                            edge_state = BallMovingDigest.TargetPosFind;
                        }



                        // 키스 여부 확인 (목적구 잃어버림 저장조건 확인)
                        if (ballMovDigest.Contains(BallMovingDigest.Kiss))
                        {
                            // 잃어버림 기록 있는지 확인
                            if (!ballMovDigest.Contains(BallMovingDigest.TargetLose))
                            {
                                // 적구로 향했다가 다시 조회한다면 잃어버리고 다시 왔는지 체크
                                if (ballMovDigest.Contains(BallMovingDigest.TargetBallLook) || ballMovDigest.Contains(BallMovingDigest.TargetPosLook))
                                {
                                    edge_state = BallMovingDigest.TargetLose;
                                    remainTimePredicatePos = 1.0f;
                                }
                            }
                        }
      
                           
                       
                        break;
                    case BallMovingDigest.TargetPosFind:
                        // 적구와 거리가 어느정도 떨어져있다면
                        float dist = Vector3.Distance(transform.position, edge_targetBall.transform.position);
                        //Debug.Log($"dist : {dist}, force : {body.velocity.magnitude}");
                        if (dist < 1.0f)
                        {
                            // 힘이 없다면  
                            if (body.linearVelocity.magnitude < 0.05f)
                            {
                                edge_state = BallMovingDigest.ForceOut;

                            }
                            else
                            {

                                FindPredicateMoveLine();

                            }
                        }
                        else
                        {
                            FindPredicateMoveLine();

                        }
                        break;
                    // 목적지 향하는 중
                    case BallMovingDigest.TargetPosLook:
                        remainTimePredicatePos -= Time.deltaTime;
                        if (remainTimePredicatePos < 0)
                        {
                            edge_state = BallMovingDigest.TargetPosFind;
                        }
                        else
                        {
                            // 중간의 쿠션없이 목적구를 향하고 있을때
                            if (target_pre_idx == 0)
                            {
                                edge_state = BallMovingDigest.TargetBallFind;
                            }
                        }

                        break;
                    case BallMovingDigest.TargetBallFind:
                        FindPredicateMoveLine(true);
                        break;
                    case BallMovingDigest.TargetBallFar:
                        remainTimePredicatePos -= Time.deltaTime;
                        if (remainTimePredicatePos < 0)
                        {
                            edge_state = BallMovingDigest.Start;         // 키스후 다시 조회
                        }
                        break;
                    case BallMovingDigest.TargetBallLook:

                        Vector3 cue_dir = transform.position - pos_pre;
                        Vector3 ball_dist = edge_targetBall.transform.position - transform.position;
                        float ang = Vector3.Angle(cue_dir, ball_dist);
                        //edge_dist = Vector3.Distance(edge_targetBall.transform.position, transform.position);
                        if (91 < ang)
                        {
                            edge_state = BallMovingDigest.TargetBallFar;
                            remainTimePredicatePos = 1.0f;
                            // msg = "아 ㅜㅜ";
                        }
                        else
                        {
                            //msg = "힘~";
                        }
                        //edge_target_dist_old = edge_dist;
                        //Debug.Log($"edge_state : {edge_state} , ang : {ang} ");

                        //edge_dist = Vector3.Distance(edge_targetBall.transform.position, transform.position);


                        //}
                        break;
                    case BallMovingDigest.TargetPosFar:
                        remainTimePredicatePos -= Time.deltaTime;
                        if (remainTimePredicatePos < 0)
                        {
                            edge_state = BallMovingDigest.Start;         // 키스후 다시 조회
                        }
                        break;
                    case BallMovingDigest.Kiss:
                        remainTimePredicatePos -= Time.deltaTime;
                        if (remainTimePredicatePos < 0)
                        {
                            edge_state = BallMovingDigest.Start;         // 키스후 다시 조회
                        }
                        //msg = "";
                        break;
                    case BallMovingDigest.TargetLose:
                        remainTimePredicatePos -= Time.deltaTime;
                        if (remainTimePredicatePos < 0)
                        {
                            edge_state = BallMovingDigest.Start;         // 키스후 다시 조회
                        }

                        //msg = "";
                        break;
                    case BallMovingDigest.ForceOut:
                        remainTimePredicatePos -= Time.deltaTime;
                        if (remainTimePredicatePos < 0)
                        {
                            edge_state = BallMovingDigest.End;
                            remainTimePredicatePos = 1.0f;
                        }
                        break;
                    case BallMovingDigest.Achieve:
                        //     Debug.Log($"edge   Achieve Achieve Achieve");

                        //msg = "굿";

                        break;
                    case BallMovingDigest.End:
                        // msg = "종료";
                        // Debug.Log(msg);
                        // 키스 후 재 검색시 적구를 못찾았다면 원망 메시지


                        break;
                }
            }

            ChkEdgeStateOneSend("");


        }

        int edge_cnt = 0;
        void ChkEdgeStateOneSend(string msg)
        {
            if(edge_state != edge_state_old)
            {
                edge_state_old = edge_state;

                BallMovingStory story = BallMovingStory.None;
                switch (edge_state)
                {
                    // 키스.힘이 약해진상태에 따라 종료 변경
                    case BallMovingDigest.Kiss:
                        story = !ballMovDigest.Contains(BallMovingDigest.Start) ? BallMovingStory.KissNormal : BallMovingStory.KissWeaken;
                        break;
                    // 적구 근접
                    case BallMovingDigest.TargetPosLook:
                        story = BallMovingStory.TargetPosLook;
                        break;
                    // 적구 근접
                    case BallMovingDigest.TargetBallLook:
                        story = BallMovingStory.TargetBallLook;
                        break;
                    // 적구 지나침 
                    case BallMovingDigest.TargetBallFar:
                        story = BallMovingStory.TargetBallFar;
                        break;
                    // 배팅 성공. 키스여부확인
                    case BallMovingDigest.Achieve:
                        if (ballMovDigest.Last() == BallMovingDigest.TargetBallFar)
                        {
                            story = BallMovingStory.AchieveRelax;
                        }
                        else
                        {
                            story = ballMovDigest.Contains(BallMovingDigest.Kiss) ? BallMovingStory.AchieveKiss : BallMovingStory.AchieveNormal;
                        }
                        break;
                    case BallMovingDigest.ForceOut:
                        story = BallMovingStory.ForceOut;
                        break;

                }
                
                ballMovDigest.Add(edge_state);
                ballMovStory.Add(story);

                if (story != BallMovingStory.None)
                {
                    physicsMng.CallBallMovStory(edge_state, story, edge_cnt, transform.position);
                }
                edge_cnt = 0;
            }
            else
            {
                edge_cnt++;
            }
        }


        public void SetMechanicalState(int number = -1)
        {
 
        }
    }
}
