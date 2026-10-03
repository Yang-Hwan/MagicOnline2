using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using System.Linq;

using Assets.Scripts.Often;
using Assets.Scripts.Sight.Vital.Pavilion;
using Assets.Scripts.Exert.Network;
using Assets.Scripts.Prack.BackSys.ChartData;
using Assets.Scripts.Exert.BackSys;

namespace Assets.Scripts.Exert.Match
{
    public class PoolCoach
    {

        // 
        public event System.Action<int> OnFinishAchieve;
        public event System.Action<NoticeHitBall> OnNoticeHitBall;

        //public event System.Action<bool> OnEnableControl;               // 대체됨>OnSetActivePlayer shotController 자신의 턴일때 활성화처리
        //public event System.Action<PoolPlayer, bool> OnSetActivePlayer; // 새로운 턴 유저
        public event System.Action<int> OnSetActivePlayer;              // 자신의 턴으로 변경시
        public event System.Action<float> OnUpdateTime;                 // 경기중 시간진행 경과.매프레임마다 실행
        public event System.Action OnStartTime;                         // 턴 변경 후 시간재가동 
        public event System.Action OnStopTime;                          // 큐움직임후 모든공셋팅처리후 실행
        public event System.Action<float> OnShotEnded;                  // 모든공의 움직임이 멈춘후 실행
        public event System.Action OnEndTime;                           // 턴의 주어진 시간종료 
        public event System.Action<int> OnMatchTimeStart;                           // 턴의 주어진 시간종료 

        public event System.Action OnMatchComplite;                     // 게임결과
        public event System.Action OnMatchBegin;                        // 게임시작

        //public event System.Action OnCalculateAI;
        //public event System.Action OnShotAI;
        //public event System.Action<int> OnSetPrize;
        public event System.Action<PoolPlayer> OnSetPlayer;             // 플레이어정보 화면셋팅. 
        //public event System.Action<PoolPlayer> OnSetAvatar;             //
        public event System.Action<PoolPlayer> OnSetActiveBallsIds;     //
        
        public event System.Action<string> OnSetGameInfo;
        public event System.Action OnSecTime;

        public event System.Action<int, string> OnWordBalloon;
        public event System.Action<int, int, int, RunPath> OnScoreChanged;
        public event System.Action<int, bool, int, float> OnEndShot;                  // 보상

        public bool isMatchTimePlay { get; private set; }       // 초구 뿌리고 멈춘 후 참으로 변경 
        public bool isMatchTimeEnd { get; private set; }       // 시합제한시간 종료 후 




        public bool calculateTime{ get; private set; }
        public float playTime { get; set; }

        public int hitReward { get; private set; }                  // 목표개수
        public int targetHit { get; private set; }                  // 목표개수
        public int timeMatchSec { get; private set; }             // 시합시간
        public float timeInningSec { get; private set; }             // 이닝시간

        public bool isRearQuarter { get; private set; }             // 후구까지 

        public MatchBall matchBall { get; private set; }                // 시합볼종류 (4구, 3구)
        public MatchCushion matchCushion { get; private set; }          // 시합종류   (0, 1, 3쿠션)
        public PlayType playType { get; private set; }              // 대상주체
        public FinishMission finishMission { get; private set; }        // 마무리 미션
        public bool isBallStopAfterJudge { get; private set; }      // 볼멈춘후 점수판단
        public float maxPlayTime { get; private set; }

        public int checkCushion { get; set; }
        //public ScoreStep scoreStep { get; private set; }

        private static PoolCoach _instance = null;
        public static PoolCoach Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new PoolCoach();
                }
                return _instance;
            }
        }

        private PhysicsMng _physicsMng;
        private ShotCtrl _shotCtrl;
        public Ball[] balls { get; private set; }


        float other_maxTime;
        float other_play_time_send_tot;
        int other_play_time_send_cnt;
        float playTime_step;
        float other_playTime_step;
        bool other_play_time_step_complite;
        float other_play_time_st;
        float other_play_time_en;
        float other_play_time_i;


        public void Initialize(PhysicsMng physicsMng, ShotCtrl shotCtrl)
        {
            if (_physicsMng != null) return;
            _physicsMng = physicsMng;
            _shotCtrl = shotCtrl;

            _physicsMng.OnStartShot += PhysicsManager_OnStartShot;
            _physicsMng.OnBallMove += PhysicsManager_OnBallMove;
            _physicsMng.OnBallHitBall += PhysicsManager_OnBallHitBall;
            _physicsMng.OnBallHitBoard += PhysicsManager_OnBallHitBoard;
            _physicsMng.OnBallSleep += PhysicsManager_OnBallSleep;
            _physicsMng.OnEndShot += PhysicsManager_OnEndShot;

            //Debug.Log($"PoolCoach Initialize ball cnt : {balls?.Length}");

        }

        public void MatchReset()
        {

            SkillMatchData hall = BackendChart.skillMatchData.Where(r => r.HallIdx.Equals(NetworkManager.hallIdx)).FirstOrDefault();
            //Debug.Log("PnlMatch.MatchInit hallidx : " + NetworkManager.hallIdx + " .. hall : " + hall?.ToString() ?? "xx");


            playType = PlayType.Online;                         // 같이 하기(인간,인간)
            hitReward = 200;            
            timeInningSec = 30;                                 // 

            targetHit = hall.MatchBall == (int)MatchBall.FourBall ? 3 : hall.TargetHit; // 4구 대전 목표는 3개
            timeMatchSec = hall.MatchTotMin * 60;               // 대전시간(초) 
            matchBall = (MatchBall)hall.MatchBall;              // 4구 또는 3구..  MatchBall.ThreeBall;            // 3볼 개수
            matchCushion = (MatchCushion)hall.MatchCushion;     // 미션쿠션  MatchCushion.OneCusion;      // 시합룰 . 쿠션 1번
            finishMission = (FinishMission)hall.FinishMission;  // 마무리미션 // FinishMission.None;         // 마무 리없음
            isBallStopAfterJudge = matchBall == MatchBall.FourBall ? true : false;                       // 4구여부 false : 볼 멈추기 전의 점수처리    
            isRearQuarter = true;                               // 무조건 후구 있음
            maxPlayTime = timeInningSec;

            //scoreStep = ScoreStep.Egg;
            checkCushion = (int)matchCushion;

            other_maxTime = timeInningSec;                                      // 상대 제한시간
            other_play_time_send_cnt = 0;                                       // 상대 전송횟수
            float send_sec = 0.6f;                                              // 몇초마다 전송  지정값  
            other_play_time_send_tot = other_maxTime / send_sec;                // 전체시간 전송되는 횟수
            other_playTime_step = 1 / Mathf.Round(other_play_time_send_tot);    // 단계별 전송시마다 이동간극
            other_play_time_step_complite = false;                              // 단계별 초기화
            other_play_time_st = 0;
            other_play_time_en = other_playTime_step;
            other_play_time_i = 0;


            _physicsMng.SetBalls((int)matchBall);
            balls = _physicsMng.balls;

            //Debug.Log($"PoolCoach MatchReset ball cnt : {balls.Length}");

            OnSetPlayer?.Invoke(PoolPlayer.mainPlayer);
            OnSetPlayer?.Invoke(PoolPlayer.otherPlayer);

            PoolLogic.Instance.ResetMatchInit();
            PoolPlayer.ResetMatch(finishMission != FinishMission.None ? FinishStep.Task : FinishStep.None, checkCushion);
            PoolPlayer.SetTurn(PoolPlayer.turnId);
            isMatchTimePlay = false;                    // true일때 실제 시작 .. 초구뿌리기 완료후 참
            isMatchTimeEnd = false;


        }
 


        // 턴 변경직후 선수 활성화 
        public void ActivePlayer()
        {

            //_shotCtrl.SetCueBall();
            calculateTime = true;
            playTime = 0;
            OnStartTime?.Invoke();

            OnSetActivePlayer?.Invoke(PoolPlayer.currentPlayer.playerId);                                      // 시간 진행 시작
        }


        void PhysicsManager_OnStartShot(string info)
        {
            for (int i = 0; i < (int)matchBall; i++)
            {
                balls[i].OnState(BallState.SetState);
            }
            balls[PoolPlayer.turnId].OnState(BallState.StartMove);
            calculateTime = false;      // 스트로크 후 이닝타이머 중지

            OnStopTime?.Invoke();

            //Debug.Log($"0.PhysicsManager_OnStartShot needToChangeTurn : {PoolLogic.gameState.needToChangeTurn} .. isMatchTimePlay : {isMatchTimePlay} .. gameIsComplete : {PoolLogic.gameState.gameIsComplete}");
            // 슈팅 시작 직후 초기화 
            PoolLogic.Instance.ResetTurnState(isMatchTimePlay);
            //Debug.Log($"1.PhysicsManager_OnStartShot myturn : {PoolPlayer.mainPlayer.myTurn} ... Inn : {PoolPlayer.inning} ... Ord : {PoolPlayer.ord} ...  needToChangeTurn : {PoolLogic.gameState.needToChangeTurn} .. isMatchTimePlay : {isMatchTimePlay} .. gameIsComplete : {PoolLogic.gameState.gameIsComplete}");

        }


        void PhysicsManager_OnBallMove(int ballId, Vector3 position, Vector3 velocity, Vector3 angularVelocity)
        {
            balls[ballId].OnState(BallState.Move);
        }

        void PhysicsManager_OnBallHitBall(Ball ball, Ball hitBall, bool inMove)
        {
            //Debug.Log($"PoolCoach PhysicsManager_OnBallHitBall ball : {ball.id}, inMove : {inMove}");
            if (!inMove)
            {
                return;
            }

            balls[ball.id].OnState(BallState.HitBall);
            if (!isMatchTimePlay) return;      // 초구뿌리기 완료가 아니라면 

            // 
            if (ball.isCueBall)
            {
                PoolLogic.Instance.OnCueBallHitBall(ball, hitBall);
            }

        }

        void PhysicsManager_OnBallHitBoard(Ball ball, bool inMove, CushionDir dir)
        {
            //Debug.Log($"PoolCoach PhysicsManager_OnBallHitBoard ball : {ball.id}, inMove : {inMove}");
            if (!inMove)
            {
                return;
            }

            balls[ball.id].OnState(BallState.HitBoard);
            //Debug.Log($"PhysicsManager_OnBallHitBoard isMatchTimePlay : {isMatchTimePlay} .. myturn : {PoolLogic.controlInNetwork} .. OnMatchComplite : {PoolLogic.gameState.gameIsComplete}  ");     
            if (!isMatchTimePlay)
            {

                return;      // 초구뿌리기 완료가 아니라면 
            }
            if (ball.isCueBall)
            {
                _physicsMng.CusionHit(dir);
                PoolLogic.Instance.OnBallHitBoard(ball, dir);
            }
        }

        void PhysicsManager_OnBallSleep(int ballId, Vector3 position)
        {
            balls[ballId].OnState(BallState.EndMove);
        }

        void PhysicsManager_OnEndShot(float moveTime)
        {
            bool gameIsEnd;


            bool isHit;
            int reward;
            isHit = false;
            reward = 0;

            PoolLogic.Instance.ShotEnded();
            OnEndShot?.Invoke(PoolPlayer.turnId, isHit, reward, moveTime);   // 상대에게 마무리 알림 메시지보냄

            
       
            //Debug.Log($"PoolCoach .. PhysicsManager_OnEndShot isMatchTimePlay : {isMatchTimePlay},  isSurvival : {PoolLogic.gameState.isSurvival} ... needToChangeTurn : {PoolLogic.gameState.needToChangeTurn}...  gameIsComplete : {PoolLogic.gameState.gameIsComplete}  ... ");

            //if (PoolLogic.gameState.isPenaltyShot)
            //{
            //    if(PoolPlayer.ord == 1)
            //    {
            //        if (PoolPlayer.mainPlayer.hitCnt != PoolPlayer.otherPlayer.hitCnt)
            //        {
            //            if (PoolPlayer.mainPlayer.hitCnt > PoolPlayer.otherPlayer.hitCnt)
            //            {
            //                PoolPlayer.SetWinner(PoolPlayer.mainPlayer.playerId);
            //            }
            //            else if (PoolPlayer.mainPlayer.hitCnt < PoolPlayer.otherPlayer.hitCnt)
            //            {
            //                PoolPlayer.SetWinner(PoolPlayer.otherPlayer.playerId);
            //            }
            //            PoolLogic.gameState.gameIsComplete = true;
            //        }
            //    }
            //}

            //if (!isMatchTimePlay)
            //{
            //    isMatchTimePlay = true;
            //    TurnChanged();

            //    return;
            //}


        }

        // 공뿌리기후 시합시작
        public void SetMatchTimePlay(bool isMatchPlay)
        {
            //Debug.Log($"PoolCoach.SetMatchTimePlay isMatchPlay : {isMatchPlay}   ------------");
            isMatchTimePlay = isMatchPlay;
            if (isMatchPlay)
            {
                OnMatchTimeStart?.Invoke(timeMatchSec);
            }
        }

        public void SetCalculateTimeEnable(bool c)
        {
            calculateTime = c;
        }

        // 배팅안하고 시간이 지나버리거나 배팅실패시 호출
        public void ChangeTurnReady()
        {
            //Debug.Log($"ChangeTurnReady id : {PoolPlayer.currentPlayer.playerId}");
            if (PoolLogic.controlInNetwork)
            {
                PoolPlayer.ChangeTurn();
            }
        }

        public void WaitChangeTurnReadyFromNetwork()
        {
            //Debug.Log("**** PoolCoach.WaitChangeTurnReadyFromNetwork");
            PoolPlayer.ChangeTurn();
        }



        public void Update(float deltaTime)
        {
            if (!isMatchTimePlay) return;

            if (calculateTime)
            {
                if (timeInningSec > 0)
                {
                    timeInningSec -= deltaTime;

                }
                if (PoolPlayer.mainPlayer.myTurn)
                {
                    CallOnUpdateTime(deltaTime);
                }
                else
                {
                    OtherUpdateTime(deltaTime);
                }
            }
        }

        // 상대와 자신의 스트로크전 타임종료시 호출
        void EndTime()
        {

            other_play_time_send_cnt = 0;
            playTime = 1.0f;
            //PoolLogic.Instance.OnEndTime();
            calculateTime = false;
            OnEndTime?.Invoke();

            // 승부치기 중에 배팅을 안한상태.
            if (PoolLogic.gameState.isSurvival)
            {
                // 후구턴인 상태
                if (PoolPlayer.ord == 1)
                {
                    // 상대와 동일 개수가 아니라면
                    if (PoolPlayer.mainPlayer.hitCnt != PoolPlayer.otherPlayer.hitCnt)
                    {
                        if (PoolPlayer.mainPlayer.hitCnt > PoolPlayer.otherPlayer.hitCnt)
                        {
                            PoolPlayer.SetWinner(PoolPlayer.mainPlayer.playerId);
                        }
                        else
                        {
                            PoolPlayer.SetWinner(PoolPlayer.otherPlayer.playerId);
                        }
                        PoolLogic.gameState.gameIsComplete = true;
                        //PoolCoach.Instance.CallOnGameComplite();
                    }
                    CallOnGameComplite();
                }
                    
            }
            //PoolPlayer.ChangeTurn();
            //PoolPlayer.ChangeTurnReady();
            //ChangeTurnReady();
        }

        public void CallMatchTimeout()
        {
            isMatchTimeEnd = true;
            calculateTime = false;

            // 볼의 멈춰있는 상태 
            if (!_physicsMng.inMove)
            {
                other_play_time_send_cnt = 0;
                playTime = 1.0f;
                calculateTime = false;

                //if (PoolLogic.controlInNetwork)
                //{
                    //int winId = PoolPlayer.GetHitWin();
                    //if (winId >= 0)
                    //{
                    //    PoolPlayer.SetWinner(winId);
                    //}
                    PoolLogic.gameState.gameIsComplete = true;

                    int p0 = PoolPlayer.otherPlayer.hitCnt;
                    int p1 = PoolPlayer.mainPlayer.hitCnt;
                    string msg = $"{p0}_{p1}";


                    CallOnGameComplite();

                //}


            }
            else
            {

            }
        }

        protected void CallOnUpdateTime(float deltaTime)
        {
            //Debug.Log("calculateTime " + calculateTime);

            if (playTime < 1.0f)
            {
                playTime += deltaTime / maxPlayTime;
                OnUpdateTime?.Invoke(playTime);
            }
            else
            {
                EndTime();
            }
        }

        protected void OtherUpdateTime(float deltaTime)
        {
            if (other_play_time_step_complite)
            {
                //Debug.Log($"OtherUpdateTime ... 현단계 시작값 : {other_play_time_st}, 현단계 종료값 : {other_play_time_en}, 전송횟수 : {other_play_time_send_cnt}, 진행값 : {playTime}");
                return;
            }
            //float st_val = other_playTime_step * other_play_time_send_cnt;
            //float en_val = st_val + other_playTime_step;


            //Debug.Log($"OtherUpdateTime  현단계 시작값 : {other_play_time_st}, 현단계 종료값 : {other_play_time_en}, 진행값 : {playTime}, 전송횟수 : {other_play_time_send_cnt}, i : {other_play_time_i}");

            if (playTime < other_play_time_en)
            {
                other_play_time_i++;
                playTime += deltaTime / other_maxTime;
                OnUpdateTime?.Invoke(playTime);
            }
            else
            {
                //Debug.Log($"OtherUpdateTime 완료 현단계 시작값 : {other_play_time_st}, 현단계 종료값 : {other_play_time_en}, playTime : {playTime}, 전송횟수 : {other_play_time_send_cnt}");
                other_play_time_step_complite = true;
            }

        }


        // 상대턴일때 타임을 받아서 시간흐름 처리
        public void SetPlayTime(float time01)
        {
            other_play_time_step_complite = false;
            other_play_time_send_cnt++;
            other_play_time_i = 0;

            other_play_time_st = other_playTime_step * other_play_time_send_cnt;
            other_play_time_en = other_play_time_st + other_playTime_step;
            other_play_time_en = other_play_time_en > 1 ? 1 : other_play_time_en;

            //Debug.Log("SetPlayTime 현단계 진행값 " + time01 + ", 전송횟수 : " + other_play_time_send_cnt + ",  진행값 : (" + other_play_time_st + ", " + other_play_time_en + ")"   );
            if (other_play_time_st < time01)
            {
                if (other_play_time_send_cnt == 1)
                {
                    other_playTime_step = time01;
                }
                other_play_time_st = time01;
                other_play_time_en = time01 + other_playTime_step;
                //Debug.Log("SetPlayTime 재조정 >>> 현단계 진행값 " + time01 + ", 전송횟수 : " + other_play_time_send_cnt + ",  진행값 : (" + other_play_time_st + ", " + other_play_time_en + ")");
            }

            if (time01 < 1.0f)
            {
                playTime = time01;
                OnUpdateTime?.Invoke(playTime);
            }
            else
            {
                EndTime();
            }
        }

        public void SetMatchInfo(string info)
        {
            OnSetGameInfo?.Invoke(info);
        }

        public void ScoreChanged(int cnt, int reward, RunPath runPath)
        {
            OnScoreChanged?.Invoke(PoolPlayer.turnId, cnt, reward, runPath);   // 몇점, 누적점수, 코인 
        }

        public void SelfAbort()
        {
            PoolLogic.Instance.OnMatchAbort(false);
            calculateTime = false;
            Debug.Log($"PoolCoach.SelfAbort ~~~~~~ ");
        }

        // 상대가 떠남. 
        public void OtherLefted()
        {
            PoolLogic.Instance.OnMatchAbort(true);
            Debug.Log($"PoolCoach.OtherLefted ~~~~~~ ");
            CallOnGameComplite();
        }

        public void CallOnGameComplite()
        {
            Debug.Log("CallOnGameComplite");
            calculateTime = false;
            //PoolLogic.gameState.gameIsComplete = true;

            // 프론트에 알림.
            OnMatchComplite?.Invoke();
        }


        public void Result(PoolPlayer player)
        {
            OnSetPlayer?.Invoke(player);
        }


        public void CallOnNoticeHitBall(NoticeHitBall notice)
        {
            OnNoticeHitBall?.Invoke(notice);
        }

        public void CallOnFinishAchieve(int playerId)
        {
            OnFinishAchieve?.Invoke(playerId);
        }

    }
}
