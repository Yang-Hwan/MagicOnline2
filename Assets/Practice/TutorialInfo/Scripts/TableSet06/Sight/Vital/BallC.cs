using Assets.TutorialInfo.Scripts.TableSet06.Excert.Match;
using Assets.TutorialInfo.Scripts.TableSet06.Often;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{
    public partial class BallC : MonoBehaviour
    {

        public event System.Action<Vector3> OnCueBallMoveScreen;

        public int id;
        [SerializeField] private float _ballMass = 0.15f;
        [SerializeField] private float _ballMaxVelocity = 7;
        [SerializeField] private float _ballMaxAngularVelocity = 300f;
        [SerializeField] private float _drag = 0.3f;
        [SerializeField] private float _angularDrag = 0.2f;
        public bool isCueball;  // { get; private set; }
        public Rigidbody body;
        [NonSerialized] public Vector3 impactIncomingVelocity, impactIncomingSpin;
        [NonSerialized] public float strokeFollowThrough=-1f;
        [NonSerialized] public float strokeSpinPersistence=-1f;
        public PhysicsMng physicsManager;

        public float radius { get; private set; }
        public float ballRadius { get; private set; }
        public CushionHit cushionHit;
        public Transform ballShadow;
        public Transform lightCentre;
        private bool inMove;
        public Vector3 pos_pre;
        private List<BallMovingStory> movStory;
        private Vector3[] target_pre_pos;
        private int target_pre_idx;

        float remainTimePredicatePos = 0;
        float WaitTimePredicatePos = 2f;

        public StateWeaken edge_state;
        public StateWeaken edge_state_old;
        public bool is_edge_state_first;
        public BallC edge_targetBall;
        float edge_target_dist_old = 10f;
        float Edge_Force_Weaken_Max = 0.7f;                 // 
        float Edge_Force_Weaken_Min = 0.1f;                 // 

        public int clothLayer { get; set; }
        public int boardLayer { get; set; }
        public int ballLayer { get; set; }
        public int cueBallLayer { get; set; }

        // 비껴치기 판단 참조 
        private AsideHitStep asideHitStepRet = AsideHitStep.None;
        private bool isAsideHitRet = false;
        private StateCheck isAsideHitChk = StateCheck.None;                     // 0 : 검토안함, 1 : 검토필요(STEP 3), 2 : 검토완료(STEP 4)               
        private Vector3[] posAside = new Vector3[2];            //  STEP 1
        private BallC ballAside = null;                         //  STEP 1
        private CushionDir cushionDirAside = CushionDir.None;   //  STEP 2


        private float AsideHit_Ball_Cushion_Dist_Max = 0.21f;

        public ShotCtrl shotController;
        public LineRenderer[] cueBallLines;
        private Vector3 shadowSourcePosition;


        private void Awake()
        {

            clothLayer = LayerLib.NameToInt(LayerKind.Cloth);
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

            movStory = new List<BallMovingStory>();
            ballRadius = 0.7f * transform.lossyScale.x;
            //if (id == 0)
            //{
            //    isCueball = true;
            //}

            shotController = FindObjectOfType<ShotCtrl>();

        }



        public void Init()
        {
            //body = GetComponent<Rigidbody>();
            //physicsManager = FindObjectOfType<PhysicsManager>();
            //radius = body.GetComponent<SphereCollider>().radius;
            inMove = physicsManager.inMove;
            //hitShapeId = -2;
            //pocketId = -1;

            //body.drag = 0.2f;
            //body.angularDrag = 0.2f;
            //body.mass = 0.2f;
            //body.maxDepenetrationVelocity = 30f;
            //body.maxAngularVelocity = 300f;
            //body.Sleep();
            //Debug.Log($"_drag : {body.drag}, _angularDrag : {body.angularDrag}, _ballMass : {body.mass}, _ballMaxVelocity : {body.maxDepenetrationVelocity}, _ballMaxAngularVelocity : {body.maxAngularVelocity}");

        }

        private void Start()
        {
            InitializeMotionVisual();
            cueBallLines = shotController.cueBallLines;
            ballShadow.position = CalculateBallShadowPosition();
            shadowSourcePosition = transform.position;

        }
        void Update()
        {
            Vector3 currentPosition = transform.position;
            if (currentPosition != shadowSourcePosition)
            {
                SetBallShadowAndBlickBlick();
                shadowSourcePosition = currentPosition;
            }

#if UNITY_EDITOR
            if (isCueball)
            {
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    Time.timeScale = 1;
                }
                else if (Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    Time.timeScale = 0;
                }
            }
#endif
        }

        Vector3 CalculateBallShadowPosition()
        {
            Vector3 position = VisualPosition;
            Vector3 positionInCloth = new Vector3(position.x, 0.01f * radius, position.z);
            return positionInCloth + 0.007f * radius * position;
        }

        public void SetBallShadowAndBlickBlick()
        {
            ballShadow.position = CalculateBallShadowPosition();
        }

        public void OnTriggerEnter(Collider other)
        {
            int layer = 1 << other.gameObject.layer;
            //if (isCueball)
            //{
            if (layer == LayerLib.NameToInt(LayerKind.Stuff))
            {

                //Debug.Log("보석 터치 posid : " + other.GetComponent<StuffBase>().positionId);
                other.GetComponent<StuffBase>().BallTouch(0);



                //Destroy(other.gameObject);
            }
            else if (layer == LayerLib.NameToInt(LayerKind.Item))
            {
                //Debug.Log("OnTriggerEnter 아이템 터치");
                other.GetComponent<StuffBase>().ItemTouch();

            }
            //}
        }

        // 충돌직전 
        public void OnCollisionEnter(Collision collision)
        {
            bool online = physicsManager.OnlineAdapter != null && physicsManager.OnlineAdapter.CanSimulate;
            if (!PoolCoach.Instance.isMatchTimePlay && !online) return;

            int layer = 1 << collision.collider.gameObject.layer;
            // Online adjudication records contact entry: a ball may settle without an exit event.
            if (online && isCueball)
            {
                var other = collision.collider.GetComponent<BallC>();
                if (other) physicsManager.CallBallHitBall(this, other, true);
                else if (layer == LayerLib.NameToInt(LayerKind.Board))
                    physicsManager.CallBallHitBoard(this, true, CushionDir.None);
            }
            if (isCueball)
            {
                if (layer == LayerLib.NameToInt(LayerKind.Ball))
                {
                    BallC ball = collision.collider.GetComponent<BallC>();
                    float instantWeight=ball && physicsManager.FollowThroughProfileActive
                        ? physicsManager.InstantFollowDrawWeight(impactIncomingVelocity,ball.body.position-body.position) : 1f;
                    if (ball && physicsManager.BallImpactTrialActive && !physicsManager.IsPracticeReplay)
                        body.linearVelocity = BallImpactTrial.Resolve(impactIncomingVelocity, body.linearVelocity,
                            impactIncomingSpin, GetComponent<SphereCollider>().radius * Mathf.Abs(transform.lossyScale.x),
                            body.position - ball.body.position, physicsManager.impactPowerAngle, physicsManager.impactSpinAngle,
                            physicsManager.FollowThroughProfileActive && strokeFollowThrough>=0 ? FollowThroughProfile.ContactInfluence(strokeFollowThrough) : 1f,
                            false,instantWeight);

                    if(ball && physicsManager.FollowThroughProfileActive && instantWeight>0 && !physicsManager.IsPracticeReplay)
                        FollowThroughProfile.ForwardCarry(body,ball.body,strokeFollowThrough*instantWeight,impactIncomingVelocity,
                            GetComponent<SphereCollider>().radius*Mathf.Abs(transform.lossyScale.x),physicsManager.topspinForwardCarryGain);

                    // 수구와 충돌한 개체가 없을때(보드포함)  //
                    if (PoolCoach.Instance.isCueBallNoHit)
                    {
                        float figure;
                        float thick = GetBallWithBallThickness(ball, ballRadius, out figure);        // 두께 확인
                        PoolCoach.Instance.SetFirstBallThickness(thick);

                        //Debug.Log("STEP 1 STEP 1 STEP 1 STEP 1 ");

                        // 비껴치기 판단에 필요한 정보 수집

                        //Vector3 direction = Vector3.ProjectOnPlane(transform.position - pos_pre, Vector3.up).normalized;
                        CueBallMoveAreaLook look = pos_pre.x > transform.position.x ? CueBallMoveAreaLook.Left : CueBallMoveAreaLook.Right;    // 과거위치가 더크면 좌측으로 이동
                        CueBallHitPart part = CueBallHitPart.None;
                        if (figure < 0)
                            part = CueBallHitPart.Left;
                        else if (figure > 0)
                            part = CueBallHitPart.Right;
                        else if (figure == 0)
                            part = CueBallHitPart.None;

                        PoolCoach.Instance.SetCueBallMoveDirArea(part, look);

                        posAside[0] = this.transform.position;
                        posAside[1] = ball.transform.position;
                        ballAside = ball;



                    }

                    // 타겟 적중이 안된상태에서 키스 처리
                    if (!PoolLogic.gameState.shotAchieve)
                    {
                        // 적구가 이동시 수구와 충돌했을때만 키스 인정.
                        float ball_dist = Vector3.Distance(ball.transform.position, ball.pos_pre);
                        if (ball_dist > 0.03f)
                        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            Debug.Log($"키스 ~~~ 적구충돌 :: ball.id : {ball.id}, velocity : {ball.body.linearVelocity.magnitude}, ball_dist : {ball_dist}, ball.pos_pre : {ball.pos_pre}, ball.cur_pos : {ball.transform.position}");
#endif
                            edge_state = StateWeaken.Kiss;
                            string msg_kiss = "쪽~";
                            physicsManager.WordBalloonSend(id, msg_kiss);
                            movStory.Add(BallMovingStory.Kiss);

                        }
                    }
                }


            }
        }

        // 충돌직후
        public void OnCollisionExit(Collision collision)
        {
            // Entry already recorded the online contact. Keep the legacy exit-based analysis local.
            if (physicsManager.OnlineAdapter != null) return;
            int layer = 1 << collision.collider.gameObject.layer;
            pos_pre = transform.position;           // 모든공은 보드, 볼충돌시 위치 변경됐을때 충돌직후 위치 저장

            if (isCueball)
            {
                // 큐볼 충돌이 두번이상충돌되었다면   .. 
                if (PoolCoach.Instance.isCueBallHit3ThOver && isAsideHitChk != StateCheck.End)
                {
                    isAsideHitChk = StateCheck.End;          // STEP 4  비껴치기 체크종료
                    //Debug.Log($" isAsideHitChk = 2  :: END END END END END hits cnt :: {PoolLogic.gameState.hitKinds.Count}");
                }

                if (layer == LayerLib.NameToInt(LayerKind.Board))
                {
                    CushionDir dir = CushionDir.None;
                    // 매치타임이 진행중일때
                    if (PoolCoach.Instance.isMatchTimePlay)
                    {
                        string c_nm = collision.collider.gameObject.name;
                        string d = c_nm.Substring(5, 1);
                        dir = (CushionDir)Enum.Parse(typeof(CushionDir), d);
                        physicsManager.CusionHit(dir);

                        //Debug.Log($"OnCollisionExit  Board  {dir}");

                        physicsManager.CallBallHitBoard(this, true, dir);

                        // 비껴치기 판단에 필요한 정보 수집
                        if (PoolCoach.Instance.isCueBallOneHitBallTwoCushion)
                        {

                            // 보드위치와 적구와 간격이 (0.15)이상 떨어져 있다면 비껴치기 판단해서 제외하기(비껴치기판단 조건이 변수가 많아지는거 방지)
                            float dist = 0;
                            switch (dir)
                            {
                                case CushionDir.T:
                                    dist = physicsManager.max_z - posAside[1].z;
                                    break;
                                case CushionDir.B:
                                    dist = posAside[1].z - physicsManager.min_z;
                                    break;
                                case CushionDir.R:
                                    dist = physicsManager.max_x - posAside[1].x;
                                    break;
                                case CushionDir.L:
                                    dist = posAside[1].x - physicsManager.min_x;
                                    break;
                            }

                            if (dist <= AsideHit_Ball_Cushion_Dist_Max)
                            {
                                cushionDirAside = dir;      // STEP 2
                                isAsideHitChk = StateCheck.Check;          // STEP 2
                            }
                            else
                            {
                                isAsideHitChk = StateCheck.End;          // STEP 4 종료
                            }

                            //Debug.Log($"STEP 2 STEP 2 STEP 2 :: {PoolCoach.Instance.isCueBallOneHitBallTwoCushion}, isAsideHitChk : {isAsideHitChk},  dist : {dist} ");

                        }


                    }

                    //ObjectPooler.instance.SpawnFromPool("BallHit", transform.position);
                }
                else if (layer == LayerLib.NameToInt(LayerKind.Ball))
                {
                    if (!PoolCoach.Instance.isMatchTimePlay) return;

                    BallC ball = collision.collider.GetComponent<BallC>();
                    float instantWeight=ball && physicsManager.FollowThroughProfileActive
                        ? physicsManager.InstantFollowDrawWeight(impactIncomingVelocity,ball.body.position-body.position) : 1f;
                    //Debug.Log($"ball hit velocity : {ball.body.velocity.magnitude} , ball id : {ball.id}");
                    ObjectPooler.instance.SpawnFromPool("BallHit", transform.position);


                    // 두께체크 .. 
                    //Debug.Log("적구 충돌"); 
                    if (ball == edge_targetBall)
                    {
                        if (edge_state == StateWeaken.TargetPosLook)
                        {
                            edge_state = StateWeaken.Achieve;
                        }
                    }

                    //GetBallWithBallThickness(this, ball, physicsManager.ballcs[PoolPlayer.turnId);
                    physicsManager.CallBallHitBall(this, ball, true);

                    if (!movStory.Contains(BallMovingStory.AchieveHit))
                    {
                        if (PoolLogic.gameState.shotAchieve)
                        {
                            movStory.Add(BallMovingStory.AchieveHit);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            Debug.Log($"id : {id} ... PoolLogic.gameState.shotAchieve  PoolLogic.gameState.shotAchieve  PoolLogic.gameState.shotAchieve ballid: {ball.id}");
#endif
                            edge_state = StateWeaken.Achieve;
                            string msg_kiss;
                            // 키스 후 재 검색시 적구를 못찾았다면 원망 메시지
                            msg_kiss = movStory.Contains(BallMovingStory.Kiss) ?"키스 감사" : "나이스~";
                            physicsManager.WordBalloonSend(id, msg_kiss);

                        }
                    }
                }


            }



            string nm = collision.collider.gameObject.layer.ToString();
            //Debug.Log($"nm : {name}, self_layer : {gameObject.layer}, collider : {nm}, collider_layer : {layer} ");
        }

        void FindPredicateMoveLine(bool isBallChk = false)
        {
            if (PoolLogic.gameState.shotAchieve)
            {
                edge_state = StateWeaken.End;
                return;
            }
             
            Vector3 origin = transform.position;
            //Debug.Log($"FindPredicateMoveLine move id : {id}, origin : {origin}, pos_pre : {pos_pre}");
            Vector3 direction = Vector3.ProjectOnPlane(origin - pos_pre, Vector3.up).normalized;
            float lineLength = 1.6f;
            float radius = isBallChk? 2.4f * transform.lossyScale.x : 1.9f * transform.lossyScale.x;
            int find_ball_idx = -1;
            Vector3[] find_pos_lst = new Vector3[5];
            Utility.DrawBallHitLine(cueBallLines, radius, ballLayer, boardLayer, ref find_pos_lst, ref find_ball_idx, edge_targetBall.id, 0, origin, direction, lineLength, false);

            // 찾았다면 일정시간동안은 조회 안한다.
            if (find_ball_idx != -1)
            {
                // 쿠션없이 타겟볼과 첫 충돌예상이라면 초기화
                if (!movStory.Contains(BallMovingStory.TargetBallLook))
                {

                }
                if (find_ball_idx == 0)
                {
                    edge_state = StateWeaken.TargetBallLook;

                  //  Debug.Log($"MoveLine Ball find_idx : {find_ball_idx}, Pos Find : {find_pos_lst[find_ball_idx]}   *********************************************** ");
                }
                else
                {
                    edge_state = StateWeaken.TargetPosLook;
                  //  Debug.Log($"MoveLine Ball find_idx : {find_ball_idx}, Pos Find : {find_pos_lst[find_ball_idx]}   *********************************************** ");

                }
                edge_target_dist_old = 100f;

                // 슛성공확률이 높은상태.
                target_pre_pos = find_pos_lst;
                target_pre_idx = find_ball_idx;
                remainTimePredicatePos = WaitTimePredicatePos;

                //Debug.Log($"FindPredicateMoveLine isBallChk : {isBallChk},... target_pre_idx : {target_pre_idx}");
            }
            else
            {


                //edge_state = StateWeaken.TargetNone;


            }
        }

        float GetBallWithBallThickness(BallC ball, float radius, out float tangent)
        {
            float ret = 0;
            tangent = 0;
            float dist = Vector3.Distance(transform.position, ball.transform.position);
            float _ballRadius = radius;
            // PoolLogic.gameState.
            Vector3 origin = transform.position;
            Vector3 direction = Vector3.ProjectOnPlane(transform.position - pos_pre, Vector3.up).normalized;

            //Debug.Log($"dist : {dist}, dir : {direction}");
            //Time.timeScale = 0;

            RaycastHit targetShapeHit;
            if (Physics.SphereCast(origin, _ballRadius, direction, out targetShapeHit, 1f, ballLayer))
            {
                BallC listener = targetShapeHit.collider.gameObject.GetComponent<BallC>();
                if (listener)
                {
                    // 적구타격시수구위치 = 적구위치 + 수구반지름 * 타격방향
                    Vector3 positionInHit = targetShapeHit.point + radius * targetShapeHit.normal;
                    // 볼체커위치설정 ...
                    Vector3 targetHit_normal = Vector3.ProjectOnPlane(targetShapeHit.normal, Vector3.up).normalized;
                    Vector3 new_direction = Vector3.Cross(Vector3.up, targetHit_normal);
                    tangent = Vector3.Dot(Vector3.ProjectOnPlane(direction, Vector3.up), new_direction);
                    if (tangent < 0) new_direction = -new_direction;
                    float figure = 1 - Mathf.Abs(tangent);
                    //Debug.Log($"BallWithBall Thickness : {figure}, tangent : {tangent}");
                    ret = figure;

                }
                else
                {
                    //     Debug.Log($"00000000 ballLayer : {ballLayer} ");

                }
            }
            else
            {
                //   Debug.Log($"nonononono ballLayer : {ballLayer}, ballRadius : {ballRadius} ");

            }

            return ret;
        }


        public void OnParticleCollision(GameObject other)
        {
            string msg = other.gameObject.name;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"partlce name : " + msg + " .................................................................. ");
#endif
        }


        // 통신,저장 용도
        public void OnState(BallState state)
        {
            switch (state)
            {
                case BallState.SetState:
                    pos_pre = transform.position;
                    movStory.Clear();
                    //Debug.Log($"{id}... {movStory.Count} ");
                    if (isCueball)
                    {


                        // 이동시 끝부분
                        edge_state = StateWeaken.None;                 // 0: 힘쎔, 1:힘약함, 2:미션진행중, 힘이 빠진 상태여부
                        edge_state_old = StateWeaken.None;                 // 0: 힘쎔, 1:힘약함, 2:미션진행중, 힘이 빠진 상태여부
                        edge_targetBall = null;
                        edge_target_dist_old = 10f;
                        is_edge_state_first = false;
                        // 비껴치기 참조
                        asideHitStepRet = AsideHitStep.None;
                        isAsideHitRet = false;              // STEP 0 결정
                        isAsideHitChk = StateCheck.None;     // 0 : 검토준비, 1 : 검토필요, 2 : 검토종료 
                        posAside[0] = Vector3.zero;         // 충돌시 수구위치
                        posAside[1] = Vector3.zero;         // 충돌시 적구위치
                        ballAside = null;                   // 충돌시 적구번호
                        cushionDirAside = CushionDir.None;  // 첫 보드 충돌시 보드정보 
                        //Debug.Log($"id : {id},turnid : {PoolPlayer.turnId} ... isAsideHitChk : {isAsideHitChk}, hits : {PoolLogic.gameState.hitKinds.Count}");
                    }

                    break;
                case BallState.StartMove:
                    pos_pre = transform.position;
                    break;
                case BallState.Move:
                    if (isCueball)
                    {
                        // 속도가 느려졌을때 반응처리
                        EdgeStateAct();

                        // 적구충돌 > 첫보드충돌 > 비켜치기여부 체크    
                        AsideHitChk();

                        // 
                        MsgView();
                    }
                    break;

                case BallState.EndMove:
                    //Debug.Log($" BallState.EndMove BallState.EndMove BallState.EndMove ID : {id}, isCueball : {isCueball}");
                    //if (movStory.Count > 0)
                    //{
                    //    string log = $"ID : {id}  ....  LOG :: ";
                    //    for (int i = 0; i < movStory.Count; i++)
                    //    {
                    //        log += $"{movStory[i]} > ";
                    //    }
                    //    Debug.Log(log);
                    //}
                    //else
                    //{

                    //}

                    break;
                case BallState.HitBall:
                    break;
                case BallState.HitBoard:
                    break;
                case BallState.HitStuff:
                    break;
                default:
                    break;
            }
        }


        void MsgView()
        {
            Vector3 screen = InputOutput.usedCamera.WorldToScreenPoint(transform.position);
            OnCueBallMoveScreen?.Invoke(screen);
        }

        string msg = "";

        void EdgeStateAct()
        {
            //Debug.Log($"id : {id}, body.velocity.magnitude : {body.velocity.magnitude} ,  StateWeaken.TargetFind  >>>> edge_targetBall >>> { edge_targetBall?.id} ==============");
            if (body.linearVelocity.magnitude < Edge_Force_Weaken_Max)
            {
                switch (edge_state)
                {
                    case StateWeaken.None:

                        edge_state = StateWeaken.Start;
                        break;
                    case StateWeaken.Start:
                        //msg = "";
                        edge_state = PoolLogic.gameState.shotAchieve ? StateWeaken.End : StateWeaken.TargetFind;
                        //physicsManager.WordBalloonSend(id, "힘내~");
                        break;
                    case StateWeaken.TargetFind:
                        edge_targetBall = PoolLogic.Instance.GetTargetBall;  // 슛성공안된상태에서  한개충돌했고 충돌될 나머지 한개공 찾기 
                        int targetId = edge_targetBall?.id ?? -1;
                        //physicsManager.WordBalloonSend(id, msg);
                        //msg = $"타겟:{targetId}";
                        if (edge_targetBall != null)
                        {
                            edge_state = StateWeaken.TargetPosFind;
                            //msg = $"타겟:{targetId}";
                        }
                        break;
                    case StateWeaken.TargetPosFind:
                        // 적구와 거리가 어느정도 떨어져있다면
                        if(Vector3.Distance(transform.position, edge_targetBall.transform.position) > 1.0f)
                        {
                            // 힘이 없다면  
                            if(body.linearVelocity.magnitude < 0.2f)
                            {
                                edge_state = StateWeaken.TargetNone;

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
                    case StateWeaken.TargetPosLook:
                        remainTimePredicatePos -= Time.deltaTime;
                        if (remainTimePredicatePos < 0)
                        {
                            edge_state = StateWeaken.TargetPosFind;
                        }
                        else
                        {
                            //Debug.Log($"Time.timeScale = 0; id : {id},  pos : {transform.position} ....  body.velocity.magnitude : {body.velocity.magnitude}");
                            //Time.timeScale = 0;
                            //float figure;
                            //float edge_dist = 0;
                            //double gap = 0;
                            //float edge_tick = GetBallWithBallThickness(edge_targetBall, ballRadius * 4, out figure);   // 
                            Vector3 target_pos = Vector3.zero;
                            target_pos = target_pre_pos[0];


                            // 중간의 쿠션없이 목적구를 향하고 있을때
                            if (target_pre_idx == 0)
                            {
                                edge_state = StateWeaken.TargetBallFind;
                                //edge_target_dist_old = 100f;
                            //    Debug.Log($"StateWeaken.TargetPosLook .. target_pre_idx : {target_pre_idx}  ================================================= ");
                            }
                            else
                            {
                                target_pos = target_pre_pos[0];
                                //edge_dist = Vector3.Distance(target_pos, transform.position);
                               // gap = Math.Round(edge_target_dist_old - edge_dist, 4);
                                //   Debug.Log($"__TargetPosLook gap : {gap}  ...   edge_target_dist_old : {edge_target_dist_old}, edge_dist : {edge_dist}");
                                //Debug.Log($"TargetPosLook gap : {gap}  ... target.id : {edge_targetBall.id} ....   .......  edge_target_dist_old : {edge_target_dist_old}, edge_dist : {edge_dist}, ...cue : {transform.position}, ball : {edge_targetBall.transform.position}");

                                //if (gap < -0.002)
                                //{
                                //    //edge_state = StateWeaken.TargetFar;
                                //    // 낙담
                                //}
                                //else if (gap < 0)
                                //{
                                //    //dist_msg = "..";
                                //}
                                //else
                                //{
                                //    msg = "가즈아~";
                                //    //dist_msg = "가까워진다";
                                //}
                                //edge_target_dist_old = edge_dist;
                            }
                            //edge_dist = Vector3.Distance(edge_targetBall.transform.position, transform.position);


                        }

                        //Debug.Log($"edge : {edge_state} , force : {body.velocity.magnitude } ... .. ID : {edge_targetBall?.id} ... edge edge_tick : {edge_tick}, edge_dist : {edge_dist}.. gap : {gap} ... {dist_msg}");
                        break;
                    case StateWeaken.TargetBallFind:
                        FindPredicateMoveLine(true);
                        break;
                    case StateWeaken.TargetBallFar:
                        //edge_state = StateWeaken.TargetBallFind;
                    case StateWeaken.TargetBallLook:
                        //remainTimePredicatePos -= Time.deltaTime;
                       // if (remainTimePredicatePos < 0)
                        //{
                       //     edge_state = StateWeaken.TargetBallFind;
                       // }
                       // else
                       // {
                            //Debug.Log($"Time.timeScale = 0; id : {id},  pos : {transform.position} ....  body.velocity.magnitude : {body.velocity.magnitude}");
                            //Time.timeScale = 0;
                            //float figure;
                            //float edge_dist = 0;
                       //     double gap = 0;
                            //float edge_tick = GetBallWithBallThickness(edge_targetBall, ballRadius * 4, out figure);   // 
                            //Vector3 target_pos = Vector3.zero;
                            //target_pos = target_pre_pos[0];


                            // 중간의 쿠션없이 목적구를 향하고 있을때
                           
                        Vector3 cue_dir = transform.position - pos_pre;
                        Vector3 ball_dist = edge_targetBall.transform.position - transform.position;
                        float ang = Vector3.Angle(cue_dir, ball_dist);
                        //edge_dist = Vector3.Distance(edge_targetBall.transform.position, transform.position);
                        if (91 < ang)
                        {
                            edge_state = StateWeaken.TargetBallFar;
                            msg = "아 ㅜㅜ";
                        }
                        else
                        {
                            msg = "힘~";
                        }
                        //edge_target_dist_old = edge_dist;
                        //Debug.Log($"edge_state : {edge_state} , ang : {ang} ");
                    
                            //edge_dist = Vector3.Distance(edge_targetBall.transform.position, transform.position);


                        //}
                        break;
                    case StateWeaken.TargetPosFar:
                        //edge_state = StateWeaken.TargetPosFind;
                        break;
             
                    case StateWeaken.Kiss:
                        edge_state = StateWeaken.Start;         // 키스후 다시 조회

                        //msg = "";
                        break;
                    case StateWeaken.Achieve:
                        //     Debug.Log($"edge   Achieve Achieve Achieve");

                        msg = "굿";

                        break;
                    case StateWeaken.End:
                        // msg = "종료";
                        // Debug.Log(msg);
                        // 키스 후 재 검색시 적구를 못찾았다면 원망 메시지
                    
                        break;

                }

                //Debug.Log($"3 edge_state : {edge_state} ... edge_state_old : {edge_state_old} ... is_edge_state_on : {is_edge_state_on} >>> msg :_{(msg==""?"xxx":msg)}_");


                ChkEdgeStateOneSend(msg);
            }
        }

        //

        void ChkEdgeStateOneSend(string msg)
        {
            if (edge_state != edge_state_old)
            {
                edge_state_old = edge_state;
                is_edge_state_first = true;


            }
            else
            {
                if (is_edge_state_first)
                {
                    msg = "";
                    switch (edge_state)
                    {
                        //case StateWeaken.Start:
                        //    movStory.Add(BallMovingStory.Weaken);
                        //    break;
                        case StateWeaken.TargetFind:
                            movStory.Add(BallMovingStory.TargetFind);
                            break;
                        case StateWeaken.TargetPosFind:
                            movStory.Add(BallMovingStory.TargetPosFind);

                            break;
                        case StateWeaken.TargetPosLook:
                            movStory.Add(BallMovingStory.TargetPosLook);

                            if (movStory.Where(r => r == BallMovingStory.TargetPosLook).Count() == 1)
                            {
                                if(edge_targetBall != null)
                                {
                                    float dist = Vector3.Distance(transform.position, edge_targetBall.transform.position);
                                    if(dist < 2f && body.linearVelocity.magnitude > 1f)
                                    {
                                        msg = "가즈아~";
                                    }
                                }
                            }

                            break;
                        case StateWeaken.TargetBallFind:
                            movStory.Add(BallMovingStory.TargetBallFind);
                            break;
                        case StateWeaken.TargetBallLook:
                            movStory.Add(BallMovingStory.TargetBallLook);
                            msg = "힘~";

                            break;
                        case StateWeaken.TargetPosFar:
                            movStory.Add(BallMovingStory.TargetPosFar);
                            break;
                        case StateWeaken.TargetBallFar:
                            movStory.Add(BallMovingStory.TargetBallFar);
                            msg = "아까비";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            Debug.Log($"StateWeaken.TargetBallFar: 아까비");
#endif

                            break;
                        case StateWeaken.TargetNone:
                            // 마지막이 키스이고 이전의 볼방향으로 이동중이라면 키스메시지
                            if (movStory.Count > 0)
                            {
                                if (movStory.Last() == BallMovingStory.Kiss && movStory.Contains(BallMovingStory.TargetBallLook))
                                {
                                    string kiss_msg = "아~ 키스";
                                    physicsManager.WordBalloonSend(id, kiss_msg);
                                }
                                else
                                {
                                    // 힘이 있다면 더 찾아본다
                                     
                                    if (body.linearVelocity.magnitude < Edge_Force_Weaken_Min)
                                    {
                                        string kiss_msg = "ㅡ.ㅡ";
                                        if(movStory.Contains( BallMovingStory.Kiss) && movStory.Last() == (BallMovingStory.TargetPosFind))
                                        {
                                            kiss_msg = "키스 ㅜㅜ";
                                        }
                                        physicsManager.WordBalloonSend(id, kiss_msg);
                                    }
                                }
                            }
                            else
                            {
                                // 힘이 있다면 더 찾아본다
                                //if (body.velocity.magnitude < Edge_Force_Weaken_Min)
                                //{
                                    string kiss_msg = "ㅜㅜ";
                                    physicsManager.WordBalloonSend(id, kiss_msg);
                                //}
                            }
                            break;
                        case StateWeaken.End:
   
                            break;
                    }

                    //Debug.Log($"============edge_state_old : {edge_state_old}, edge_state : {edge_state}, msg = >{msg}< , power : {body.velocity.magnitude}, ball_dist : {Vector3.Distance(transform.position, (edge_targetBall==null?Vector3.zero:edge_targetBall.transform.position))}");
                    if (msg != "")
                    {
                        //Debug.Log("---------------------------------------------------------------------------------");
                        physicsManager.WordBalloonSend(id, msg);
                    }
                    is_edge_state_first = false;
                }
            }
        }

        void AsideHitChk()
        {
            // 적구충돌 > 첫보드충돌 > (*비켜치기여부 체크*) > 체크종료   
            if (isAsideHitChk == StateCheck.Check)         // STEP 3
            {
                int preDir, aftCueDir, aftBallDir, aftDir;
                bool isChk = false;
                switch (cushionDirAside)
                {
                    case CushionDir.T:
                        if (posAside[0].z >= transform.position.z) isChk = true; break;
                    case CushionDir.B:
                        if (posAside[0].z <= transform.position.z) isChk = true; break;
                    case CushionDir.R:
                        if (posAside[0].x >= transform.position.x) isChk = true; break;
                    case CushionDir.L:
                        if (posAside[0].x <= transform.position.x) isChk = true; break;
                }

                if (isChk)
                {
                    if (cushionDirAside == CushionDir.T || cushionDirAside == CushionDir.B)
                    {
                        preDir = posAside[0].x > posAside[1].x ? -1 : 1;                        // posAside[1]이 목적구이고 그 뱡향이 좌측이면 -1 , 우측이면 1
                        aftCueDir = posAside[0].x > transform.position.x ? -1 : 1;              // 수구현재가 목적위치이고 좌측이면 -1 , 우측이면 1
                        aftBallDir = posAside[1].x > ballAside.transform.position.x ? -1 : 1;   // 적구현재가 목적위치이고 좌측이면 -1 , 우측이면 1
                        aftDir = transform.position.x > ballAside.transform.position.x ? -1 : 1;
                    }
                    else
                    {
                        preDir = posAside[0].z > posAside[1].z ? -1 : 1;                        // posAside[1]이 목적구이고 그 뱡향이 하단이면 -1 , 상단이면 1
                        aftCueDir = posAside[0].z > transform.position.z ? -1 : 1;              // 수구현재가 목적위치이고 하단이면 -1 , 상단이면 1
                        aftBallDir = posAside[1].z > ballAside.transform.position.z ? -1 : 1;   // 적구현재가 목적위치이고 하단이면 -1 , 상단이면 1
                        aftDir = transform.position.z > ballAside.transform.position.z ? -1 : 1;
                    }

                    isAsideHitRet = preDir == aftCueDir && preDir == aftBallDir && preDir == aftDir;            // 지정된 방향으로 수구와적구의 방향이 같아야한다.
                    asideHitStepRet = isAsideHitRet ? AsideHitStep.Appoint : asideHitStepRet;
                    isAsideHitChk = StateCheck.End;      // STEP 4   체크종료
                    PoolCoach.Instance.SetIsAsideHit(asideHitStepRet, cushionDirAside);
                }

            }
        }

        void FixedUpdate()
        {

            if (!body.isKinematic && !body.IsSleeping() && physicsManager.inMove)
            {
                physicsManager.CallBallMove(id, body.position, body.linearVelocity, body.angularVelocity);
            }

            if (inMove != physicsManager.inMove)
            {
                inMove = physicsManager.inMove;
                if (!inMove && !body.isKinematic)
                {
                    //Debug.Log("cccccccccccccccccc");

                    body.Sleep();
                }
            }

        }

        public void SetCueBall(bool isCue)
        {
            this.isCueball = isCue;
            //Debug.Log($"SetCueBall LayerKind.CueBall : {LayerKind.CueBall} .. LayerMask.NameToLayer(LayerKind.CueBall.ToString()) : {LayerMask.NameToLayer(LayerKind.CueBall.ToString())}");
            gameObject.layer = isCue ? LayerMask.NameToLayer(LayerKind.CueBall.ToString()) : LayerMask.NameToLayer(LayerKind.Ball.ToString());
        }
    }
}
