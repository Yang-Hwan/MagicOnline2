using UnityEngine;
using System.Linq;
using Cysharp.Threading.Tasks;
using Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital;
using Assets.TutorialInfo.Scripts.TableSet06.Often;

namespace Assets.TutorialInfo.Scripts.TableSet06.Excert.Match
{
    public class PoolCoach
    {
        public void RestorePracticeTurn(float savedPlayTime, float savedMaxTime)
        {
            isMatchTimePlay = true;
            playTime = savedPlayTime;
            maxPlayTime = savedMaxTime;
            calculateTime = true;
            ShotCtrl.canControl = true;
            _shotCtrl.CueReadyShot();
            OnSetActivePlayer?.Invoke(PoolPlayer.turnId);
            OnUpdateTime?.Invoke(playTime);
        }

        public event System.Action<PoolPlayer> OnSetPlayer;             // 회원정보
        public event System.Action<int> OnSetActivePlayer;              // 자신의 턴으로 변경시
        public event System.Action OnStopTime;                          // 큐움직임후 모든공셋팅처리후 실행
        public event System.Action<float> OnUpdateTime;                 // 경기중 시간진행 경과.매프레임마다 실행
        public event System.Action OnEndTime;                           // 턴의 주어진 시간종료 
        public event System.Action<string> OnSetGameInfo;
        public event System.Action OnMatchComplite;                     // 게임결과
        public event System.Action<int, bool, int> OnBallAllStop;                  // 보상
        public event System.Action<int, bool, int, RunPath> OnScoreChanged;
        public event System.Action<bool, bool, int> OnPracticeShotResult;
        public event System.Action<int, string> OnWordBalloon;
        public event System.Action OnStartTime;                 // 턴 변경 후 시간재가동 
        private static PoolCoach _instance = null;
        public static PoolCoach Instance
        {
            get
            {
                if(_instance == null)
                {
                    _instance = new PoolCoach();
                }
                return _instance;
            }
        }

        private PhysicsMng _physicsMng;
        private ShotCtrl _shotCtrl;

        // Each practice visit owns its coach and UI event subscriptions.
        public static void Release(PhysicsMng owner)
        {
            if (_instance == null || _instance._physicsMng != owner) return;
            var coach = _instance;
            owner.OnBallHitBall -= coach.PhysicsManager_OnBallHitBall;
            owner.OnBallHitBoard -= coach.PhysicsManager_OnBallHitBoard;
            owner.OnBallAllStop -= coach.PhysicsManager_OnBallAllStop;
            owner.OnStartShot -= coach.PhysicsManager_OnStartShot;
            owner.OnBallMove -= coach.PhysicsManager_OnBallMove;
            owner.OnBallSleep -= coach.PhysicsManager_OnBallSleep;
            owner.OnWordBalloon -= coach.PhysicsManager_OnWordBalloon;
            _instance = null;
        }

        public int hitReward { get; private set; }                  // 목표개수
        public int targetHit { get; private set; }                  // 목표개수
        public int timeMatchSec { get; private set; }             // 시합시간
        public int timeInningSec { get; private set; }             // 이닝시간

        public MatchBall matchBall { get; private set; }                // 시합볼종류 (4구, 3구)
        public MatchCushion matchCushion { get; private set; }          // 시합종류   (0, 1, 3쿠션)
        public PlayType  playType { get; private set; }              // 대상주체
        public FinishMission finishMission { get; private set; }        // 마무리 미션

        public BallC[] balls { get; private set; }

        public bool isMatchTimePlay { get; private set; }       // 초구 뿌리고 멈춘 후 참으로 변경 
        public bool isBallStopAfterJudge { get; private set; }   // 볼멈춘후 점수판단
        public Assets.Scripts.Often.MatchConfiguration Configuration { get; private set; } =
            Assets.Scripts.Often.PracticeSceneFlow.Configuration;
        public bool isRulePractice => Configuration.Execution == Assets.Scripts.Often.MatchExecutionMode.LocalPractice;
        public int practiceAttempts { get; private set; }
        public int practiceStreak { get; private set; }

        public void RestorePracticeScore(int attempts, int streak)
        {
            practiceAttempts = attempts;
            practiceStreak = streak;
        }

        public bool isCueBallNoHit {
            get
            {
                return PoolLogic.gameState.hitKinds.Count == 0;
            }
        }

        // 충돌을 한번한 상태이고 적구인 경우
        public bool isCueBallOneHitBallTwoCushion
        {
            get
            {
                bool ret = false;
                //Debug.Log($"PoolLogic.gameState.hitKinds.Count >> {PoolLogic.gameState.hitKinds.Count}");
                if(PoolLogic.gameState.hitKinds.Count == 2)
                {
                    //Debug.Log($"PoolLogic.gameState.hitKinds 0 >> { PoolLogic.gameState.hitKinds[0]}");

                    if (PoolLogic.gameState.hitKinds[0] == HitKind.Ball_1)
                    {
                        if (PoolLogic.gameState.hitKinds[1] == HitKind.Board_T 
                            || PoolLogic.gameState.hitKinds[1] == HitKind.Board_R
                            || PoolLogic.gameState.hitKinds[1] == HitKind.Board_B
                            || PoolLogic.gameState.hitKinds[1] == HitKind.Board_L)
                        {
                            ret = true;
                        }
                    }
                        
                }
                return ret;
            }
        }

        public bool isCueBallHit3ThOver
        {
            get
            {
                bool ret = false;
                //Debug.Log($"PoolLogic.gameState.hitKinds.Count >> {PoolLogic.gameState.hitKinds.Count}");
                if (PoolLogic.gameState.hitKinds.Count >= 3)
                {
                    //Debug.Log($"PoolLogic.gameState.hitKinds 0 >> { PoolLogic.gameState.hitKinds[0]}");
                    ret = true;
                }

                return ret;
            }
        }


        public float maxPlayTime
        {
            get;
            set;
        }
        // 이닝타임 작동시간
        public bool calculateTime
        {
            get;
            private set;
        }
        public float playTime
        {
            get;
            set;
        }



        // 물리관리의 이벤트 연결
        public void Initialize(PhysicsMng physicsMng, ShotCtrl shotCtrl)
        {
            // OnEnable may bind again after a script/domain reload. Remove the
            // previous handlers first so a shot is never judged more than once.
            if (_physicsMng != null)
            {
                _physicsMng.OnBallHitBall -= PhysicsManager_OnBallHitBall;
                _physicsMng.OnBallHitBoard -= PhysicsManager_OnBallHitBoard;
                _physicsMng.OnBallAllStop -= PhysicsManager_OnBallAllStop;
                _physicsMng.OnStartShot -= PhysicsManager_OnStartShot;
                _physicsMng.OnBallMove -= PhysicsManager_OnBallMove;
                _physicsMng.OnBallSleep -= PhysicsManager_OnBallSleep;
                _physicsMng.OnWordBalloon -= PhysicsManager_OnWordBalloon;
            }
            _physicsMng = physicsMng;
            _shotCtrl = shotCtrl;
            if (_physicsMng == null || _shotCtrl == null)
            {
                balls = null;
                return;
            }
            balls = _physicsMng.ballcs;

            // 회원구성 체크 
            tempMatchPlayerCheck();

            _physicsMng.OnBallHitBall += PhysicsManager_OnBallHitBall;
            _physicsMng.OnBallHitBoard += PhysicsManager_OnBallHitBoard;
            _physicsMng.OnBallAllStop += PhysicsManager_OnBallAllStop;
            _physicsMng.OnStartShot += PhysicsManager_OnStartShot;
            _physicsMng.OnBallMove += PhysicsManager_OnBallMove;
            _physicsMng.OnBallSleep += PhysicsManager_OnBallSleep;
            _physicsMng.OnWordBalloon += PhysicsManager_OnWordBalloon;

        }

        //public void SetMatchTimeStart()
        //{
        //    // 초구 뿌리기의 끝남시점의 매치타임 시작
        //    if (!isMatchTimePlay)
        //    {
        //        isMatchTimePlay = true;

        //        // 실제 매치 시작
        //        TurnChanged();

        //    }
        //}

        // 
        public void tempMatchPlayerCheck()
        {
            if(PoolPlayer.mainPlayer == null)
            {
                PoolPlayer.OnMainPlayerLoaded("Player 1", 0);
            }
            if(PoolPlayer.otherPlayer == null)
            {
                PoolPlayer.OnGotoPlayWithPlayer("Player 2", 0);
            }

        }

        // 선수 정보 초기화, 현재턴 설정 
        public void MatchReset()
        {
            MatchReset(Assets.Scripts.Often.PracticeSceneFlow.Configuration);
        }

        public void MatchReset(Assets.Scripts.Often.MatchConfiguration configuration)
        {
            if (configuration == null) throw new System.ArgumentNullException(nameof(configuration));
            if (configuration.BallCount != _physicsMng.ballcs.Length)
                throw new System.InvalidOperationException("Match configuration does not match the loaded ball set.");
            Configuration = configuration;
            // OnEnable can run again after a script reload without Awake.
            // Rebuild missing local players before publishing UI events or assigning turns.
            PoolPlayer.players = new[] { new PoolPlayer(0, configuration.Seat0Name, 0), new PoolPlayer(1, configuration.Seat1Name, 0) };
            PoolPlayer.inning = 0;
            PoolPlayer.ResetMatch();
            PoolPlayer.SetTurn(configuration.FirstSeat);
            // Online input stays locked until the session/authority handshake is implemented.
            ShotCtrl.canControl = configuration.AllowsPracticeTools;
            hitReward = 100;
            targetHit = configuration.TargetScore;
            timeMatchSec = configuration.MatchSeconds;
            timeInningSec = configuration.TurnSeconds;
            matchBall = (MatchBall)configuration.BallCount;
            matchCushion = (MatchCushion)configuration.CushionCount;
            playType = configuration.Execution == Assets.Scripts.Often.MatchExecutionMode.OnlineMatch ? PlayType.OnLine :
                configuration.Execution == Assets.Scripts.Often.MatchExecutionMode.Replay ? PlayType.Replay : PlayType.Single;
            finishMission = FinishMission.None;         // 마무 리없음
            isBallStopAfterJudge = matchBall == MatchBall.FourBall;
            practiceAttempts = 0;
            practiceStreak = 0;

            maxPlayTime = timeInningSec;
            //Debug.Log(".... CallOnSetPlayer OnSetPlayer " + (OnSetPlayer==null?"xxx" : player.playerId.ToString()));
            OnSetPlayer?.Invoke(PoolPlayer.mainPlayer);
            OnSetPlayer?.Invoke(PoolPlayer.otherPlayer);

            PoolLogic.Instance.MatchBegin();

            isMatchTimePlay = false;                    // true일때 실제 시작 .. 초구뿌리기 완료후 참


            //Debug.Log($"PoolCoach MatchReset ===  {PoolPlayer.turnId}");
        }



        // 턴이 변경된후 처리 
        public void TurnChanged()
        {

            PoolPlayer.TurnHitReset( PoolPlayer.currentPlayer.playerId );  // 자신의 턴일때마다 초기화

            OnSetActivePlayer?.Invoke(PoolPlayer.currentPlayer.playerId);

            _shotCtrl.CueReadyShot();                                   // 큐볼지정
            CallOnEnableControl();                                       // 시간 진행 시작
        }


        // 스트로그 전의 이닝타이머 작동
        void CallOnEnableControl()
        {

            calculateTime = true;
            playTime = 0;           // 진행율

            OnStartTime?.Invoke();
        }

        
        protected void CallOnUpdateTime(float deltaTime)
        {

            //if (calculateTime)
            //{
            playTime = Mathf.Clamp01(playTime + deltaTime / maxPlayTime);
            OnUpdateTime?.Invoke(playTime);
            if (playTime >= 1f) EndTime();

            //Debug.Log($"maxPlayTime : {maxPlayTime}, tt : {tt}, playTime : {playTime}");

            //}
        }


        void EndTime()
        {
            playTime = 1.0f;
            //PoolLogic.Instance.OnEndTime();
            calculateTime = false;
            OnEndTime?.Invoke();
            //PoolPlayer.ChangeTurn();
            //PoolPlayer.ChangeTurnReady();
            practiceStreak = 0;
            SetMatchInfo($"{PoolPlayer.currentPlayer.name} 시간 초과");
            PoolLogic.Instance.ShotEnded();
            ChangeTurnReady();
        }

        // 턴변경 준비(시간초과, 득점실패)
        public void ChangeTurnReady()
        {
            PoolPlayer.ChangeTurn();
        }

        public void Update(float deltaTime)
        {
            if (Configuration.Execution == Assets.Scripts.Often.MatchExecutionMode.OnlineMatch) return;
            if (!isMatchTimePlay || PoolLogic.gameState.gameIsComplete || _physicsMng.inMove || _shotCtrl.IsShotAnimating) return;

            if (calculateTime)
            {
                CallOnUpdateTime(deltaTime);
            }
        }

        void PhysicsManager_OnStartShot(float angle)
        {
            for (int i = 0; i < balls.Length; i++)
            {
                balls[i].OnState(BallState.SetState);
                // 좌우영역 체크 
                
            }

            PoolLogic.gameState.rectArea = GetArea(balls[PoolPlayer.turnId].transform.position);
            balls[PoolPlayer.turnId].OnState(BallState.StartMove);
            calculateTime = false;      // 스트로크 후 이닝타이머 중지

            PoolLogic.gameState.cueBallMovDir = GetCuallDir(angle);
            //Debug.Log($"angle : {angle} .. {PoolLogic.gameState.cueBallMovDir }");

            //OnStopTime?.Invoke();
        }

  

        public RectArea GetArea(Vector3 pos)
        {
            RectArea ret = RectArea.None;
            ret = 0 <= pos.x ? RectArea.Right : RectArea.Left;
            return ret;
        }

        CueBallEarlyMoveDirection GetCuallDir(float angle)
        {
            CueBallEarlyMoveDirection ret = CueBallEarlyMoveDirection.None;
            float gap = 20f;

            if ( angle < 0 + gap  )
            {
                ret = CueBallEarlyMoveDirection.Vertical;
            }
            else if ( 90 - gap < angle && angle < 90 + gap)
            {
                ret = CueBallEarlyMoveDirection.Horizontal;
            }
            else if (180 - gap < angle && angle < 180 + gap)
            {
                ret = CueBallEarlyMoveDirection.Vertical;
            }  
            else if (270 - gap < angle && angle < 270 + gap)
            {
                ret = CueBallEarlyMoveDirection.Horizontal;
            }
            else if(360 - gap < angle)
            {
                ret = CueBallEarlyMoveDirection.Vertical;
            }
            else
            {
                ret = CueBallEarlyMoveDirection.Diagonal;
            }
            return ret;
        }


        public void SetCueBallMoveDirArea(CueBallHitPart part, CueBallMoveAreaLook area)
        {
            PoolLogic.gameState.cueBallHitPart = part;
            PoolLogic.gameState.cueBallMoveAreaLook = area;
        }

        // 비껴치기 확인
        public void SetIsAsideHit(AsideHitStep isAsideHit, CushionDir dir)
        {
            PoolLogic.gameState.asideHitStep = isAsideHit;
        //    Debug.Log($"id {PoolPlayer.turnId} : PoolLogic.gameState.isAsideHit : {isAsideHit} .. cushion dir : {dir}  ");
        }

        public void SetFirstBallThickness(float thick)
        {
            if (thick < 0.1f)
                PoolLogic.gameState.firstBallTic = FirstBallThickness.Thin;
            else if (0.1f <= thick && thick < 0.7f)
                PoolLogic.gameState.firstBallTic = FirstBallThickness.Medium;
            else if (0.7 <= thick)
                PoolLogic.gameState.firstBallTic = FirstBallThickness.Heavy;
        }

        void PhysicsManager_OnBallHitBall(BallC ball, BallC hitBall, bool inMove)
        {
            if (!inMove)
            {
                return;
            }

            balls[ball.id].OnState(BallState.HitBall);

            if (!isMatchTimePlay) return;      // 초구뿌리기 완료가 아니라면 
            if (ball.isCueball)
            {
                PoolLogic.Instance.OnCueBallHitBall(ball, hitBall);
            }

        }

        void PhysicsManager_OnBallHitBoard(BallC ball, bool inMove, CushionDir cushion)
        {
            //Debug.Log("PhysicsManager_OnBallHitBoard");

            if (!inMove)
            {
                return;
            }

            balls[ball.id].OnState(BallState.HitBoard);

            if (!isMatchTimePlay) return;      // 초구뿌리기 완료가 아니라면 
            if (ball.isCueball)
            {

                PoolLogic.Instance.OnBallHitBoard(ball, cushion);
            }
        }


        
        // 공의 움직임 모두 멈추었을때(볼뿌리기 포함)
        void PhysicsManager_OnBallAllStop(string data)
        {
            // Online results/turns belong to the result protocol, never local practice completion.
            if (Configuration.Execution == Assets.Scripts.Often.MatchExecutionMode.OnlineMatch) return;
            //_shotCtrl.CueReadyShot();
            //Debug.Log($"  PhysicsManager_OnBallAllStop ");

            //SetMatchTimeStart();            // 공뿌리기 체크
            if (!isMatchTimePlay)
            {
                isMatchTimePlay = true;
                // 실제 매치 시작
                TurnChanged();
                return;
            }

            bool gameIsEnd;
            bool isHit;
            int reward;
            calculateTime = false;
            playTime = 0;
            PoolLogic.Instance.OnEndShot(out gameIsEnd, out isHit, out reward);

            if (isRulePractice)
            {
                isHit = PoolLogic.gameState.shotAchieve && !PoolLogic.gameState.shotFailed;
                practiceAttempts++;
                practiceStreak = isHit ? practiceStreak + 1 : 0;
                bool foul = PoolLogic.gameState.hitKinds.Contains(HitKind.Ball_Foul);
                OnPracticeShotResult?.Invoke(isHit, foul, PoolLogic.gameState.ballsHitBoardCount);
            }

            //int prize = PoolLogic.gameState.needToChangeTurn ? 0 : 100;
            //CallOnEndShot(prize);
            //Debug.Log($"PoolCoach   PhysicsManager_OnBallAllStop");
            OnBallAllStop?.Invoke(PoolPlayer.turnId, isHit, reward);   // 몇점, 누적점수, 코인 


            // 게임종료
            if (gameIsEnd)
            {
                calculateTime = false;
                ShotCtrl.canControl = false;
                _shotCtrl.CuePutAside();
                SetMatchInfo($"{PoolPlayer.currentPlayer.name} 승리 · 목표 {targetHit}점 달성");
                OnMatchComplite?.Invoke();
                return;
            }

            // 초기화
            PoolLogic.Instance.ShotEnded();                                 // 타격공,타격보드 초기화, 공잡기무효 
            PoolPlayer.StrokeHitReset(PoolPlayer.currentPlayer.playerId);   // 스트로크 마다 초기화



            if (PoolLogic.gameState.needToChangeTurn)
            {
                PoolCoach.Instance.ChangeTurnReady();
                //PoolPlayer.ChangeTurnReady();
            }
            else
            {
                _shotCtrl.CueReadyShot();
                CallOnEnableControl();
            }



        }



        void PhysicsManager_OnBallMove(int ballId, Vector3 position, Vector3 velocity, Vector3 angularVelocity)
        {
            balls[ballId].OnState(BallState.Move);
            if (balls[ballId].isCueball)
            {
                //if (velocity.magnitude < 0.2f)
                //{

                //}
            }
        }

        void PhysicsManager_OnBallSleep(int ballId, Vector3 position)
        {
            balls[ballId].OnState(BallState.EndMove);
        }


        void PhysicsManager_OnWordBalloon(int ballId, string msg)
        {
            OnWordBalloon?.Invoke(ballId, msg);
        }

        public void SetMatchInfo(string info)
        {
            OnSetGameInfo?.Invoke(info);
        }

        public void ScoreChanged(bool isHit, int reward, RunPath runPath)
        {
            OnScoreChanged?.Invoke(PoolPlayer.turnId, isHit, reward, runPath);   // 몇점, 누적점수, 코인 
        }

    }
}
