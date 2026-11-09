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
        public event System.Action<int> OnAvatarShotStarted;

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
            if (_physicsMng == physicsMng && _shotCtrl == shotCtrl) return;
            DetachPhysics(_physicsMng);
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

        public void DetachPhysics(PhysicsMng owner)
        {
            // An old scene must not detach the replacement scene's manager.
            if (!ReferenceEquals(_physicsMng, owner)) return;
            if (!ReferenceEquals(owner, null))
            {
                owner.OnStartShot -= PhysicsManager_OnStartShot;
                owner.OnBallMove -= PhysicsManager_OnBallMove;
                owner.OnBallHitBall -= PhysicsManager_OnBallHitBall;
                owner.OnBallHitBoard -= PhysicsManager_OnBallHitBoard;
                owner.OnBallSleep -= PhysicsManager_OnBallSleep;
                owner.OnEndShot -= PhysicsManager_OnEndShot;
            }
            _physicsMng = null;
            _shotCtrl = null;
            balls = null;
            calculateTime = false;
            isMatchTimePlay = false;
            isMatchTimeEnd = false;
            shotContacts.Clear();
            remoteShotContacts = null;
        }

        public void ApplyBallSkin(string matchId)
        {
            if (balls == null || string.IsNullOrEmpty(matchId)) return;
            int style = Assets.Scripts.Often.MatchBallSkins.Choose(matchId);
            foreach (var ball in balls)
                if (ball) Assets.Scripts.Often.MatchBallSkins.Apply(ball.transform, style);
        }

        public void MatchReset()
        {
            completionNotified = false;
            calculateTime = false;
            playTime = 0;
            isMatchTimePlay = false;
            isMatchTimeEnd = false;
            shotContacts.Clear();
            remoteShotContacts = null;
            PoolLogic.Instance.ResetMatchInit();

            SkillMatchData hall = BackendChart.skillMatchData.Where(r => r.HallIdx.Equals(NetworkManager.hallIdx)).FirstOrDefault();
            //Debug.Log("PnlMatch.MatchInit hallidx : " + NetworkManager.hallIdx + " .. hall : " + hall?.ToString() ?? "xx");


            playType = PlayType.Online;                         // 같이 하기(인간,인간)
            hitReward = NetworkManager.TryGetExistingNetwork(out var gameNet) && gameNet is PunNetwork friend && friend.IsFriendly ? 0 : 200;            
            timeInningSec = 30;                                 // 

            targetHit = hall.TargetHit; // 경기장 차트의 목표 개수 사용
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
            if (NetworkManager.TryGetExistingNetwork(out var skinNetwork) && skinNetwork is PunNetwork skinPun)
                ApplyBallSkin(skinPun.BallSkinMatchId);

            //Debug.Log($"PoolCoach MatchReset ball cnt : {balls.Length}");

            PoolPlayer.ResetMatch(finishMission != FinishMission.None ? FinishStep.Task : FinishStep.None, checkCushion);
            PoolPlayer.ResetOneMore();
            PoolPlayer.SetTurn(PoolPlayer.turnId);
            OnSetPlayer?.Invoke(PoolPlayer.mainPlayer);
            OnSetPlayer?.Invoke(PoolPlayer.otherPlayer);
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


        readonly List<int> shotContacts = new List<int>();
        int[] remoteShotContacts;
        public int[] ShotContacts => shotContacts.ToArray();
        public void StageRemoteShotContacts(int[] contacts) { remoteShotContacts = contacts; }
        bool UsesSharedContacts => NetworkManager.TryGetExistingNetwork(out var net) && net is PunNetwork pun && pun.HasLegacyMatch;

        void PhysicsManager_OnStartShot(string info)
        {
            shotContacts.Clear(); remoteShotContacts = null;
            for (int i = 0; i < (int)matchBall; i++)
            {
                balls[i].OnState(BallState.SetState);
            }
            balls[PoolPlayer.turnId].OnState(BallState.StartMove);
            calculateTime = false;      // 스트로크 후 이닝타이머 중지

            OnStopTime?.Invoke();

            //Debug.Log($"0.PhysicsManager_OnStartShot needToChangeTurn : {PoolLogic.gameState.needToChangeTurn} .. isMatchTimePlay : {isMatchTimePlay} .. gameIsComplete : {PoolLogic.gameState.gameIsComplete}");
            // 슈팅 시작 직후 초기화 
            Assets.Scripts.Sight.Surface.Pavilion.CushionCountMotion.Hide();
            PoolLogic.Instance.ResetTurnState(isMatchTimePlay);
            if (NetworkManager.TryGetExistingNetwork(out var net) && net is PunNetwork pun && pun.HasLegacyMatch)
            {
                int serial = pun.BeginLegacyShot();
                if (isMatchTimePlay && info != "InitRandPosFlutter")
                    OnAvatarShotStarted?.Invoke(serial);
            }
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
                if (UsesSharedContacts && PoolLogic.controlFromNetwork) return;
                shotContacts.Add(hitBall.id);
                PoolLogic.Instance.OnCueBallHitBall(ball, hitBall);
                if (PoolLogic.gameState.ballHitBall_2 != -1 || PoolLogic.gameState.hitKinds.Contains(HitKind.Ball_Foul))
                    PresentCushionCount(0);
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
                if (UsesSharedContacts && PoolLogic.controlFromNetwork) return;
                shotContacts.Add((int)dir);
                _physicsMng.CusionHit(dir);
                int before = PoolLogic.gameState.ballsHitBoardCount;
                PoolLogic.Instance.OnBallHitBoard(ball, dir);
                if (!PoolLogic.gameState.gameIsComplete && !PoolLogic.gameState.hitKinds.Contains(HitKind.Ball_Foul) &&
                    PoolLogic.gameState.ballHitBall_2 == -1 && PoolLogic.gameState.ballsHitBoardCount > before)
                    PresentCushionCount(PoolLogic.gameState.ballsHitBoardCount);
            }
        }

        void PresentCushionCount(int count)
        {
            if (NetworkManager.TryGetExistingNetwork(out var net) && net is PunNetwork pun && pun.HasLegacyMatch)
            {
                if (PoolLogic.controlInNetwork) pun.PublishCushionMotion(count);
                else if (count == 0) Assets.Scripts.Sight.Surface.Pavilion.CushionCountMotion.Hide();
                return;
            }
            if (count > 0) Assets.Scripts.Sight.Surface.Pavilion.CushionCountMotion.Show(count);
            else Assets.Scripts.Sight.Surface.Pavilion.CushionCountMotion.Hide();
        }

        void PhysicsManager_OnBallSleep(int ballId, Vector3 position)
        {
            balls[ballId].OnState(BallState.EndMove);
        }

        void PhysicsManager_OnEndShot(float moveTime)
        {
            PresentCushionCount(0);
            if (isMatchTimePlay && UsesSharedContacts && PoolLogic.controlFromNetwork)
            {
                if (remoteShotContacts == null)
                {
                    Debug.LogError("[ShotContacts] Missing authoritative contacts; refusing independent score judgment.");
                    return;
                }
                var cue = balls.First(b => b.isCueBall);
                foreach (int contact in remoteShotContacts)
                {
                    if (contact >= 100) PoolLogic.Instance.OnBallHitBoard(cue, (CushionDir)contact);
                    else PoolLogic.Instance.OnCueBallHitBall(cue, balls[contact]);
                }
                remoteShotContacts = null;
            }
            bool gameIsEnd;


            bool isHit;
            int reward;
            isHit = false;
            reward = 0;

            PoolLogic.Instance.ShotEnded();
            // Publish terminal emotion before the shot-end packet can change the peer's turn.
            if (Assets.Scripts.Sight.Surface.Pavilion.MultiplayerSentimentController.Active)
                Assets.Scripts.Sight.Surface.Pavilion.MultiplayerSentimentController.Instance.CompleteShot();
            if (UsesSharedContacts)
                Debug.Log($"[ShotContacts] judged local={PoolPlayer.mainPlayer.hitCnt} opponent={PoolPlayer.otherPlayer.hitCnt} turn={PoolPlayer.turnId}");
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
            if (Assets.Scripts.Exert.Network.NetworkManager.TryGetExistingNetwork(out var net) &&
                net is Assets.Scripts.Exert.Network.PunNetwork pun && pun.BlockLegacyShots) return;

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
            if (!calculateTime || PoolLogic.gameState.gameIsComplete || isMatchTimeEnd) return;
            if (NetworkManager.TryGetExistingNetwork(out var net) && net is PunNetwork pun && pun.BlockLegacyShots) return;

            // OnEndTime can synchronously change ord/turn. Judge the player whose clock expired.
            bool finish = PoolLogic.gameState.isSurvival && PoolPlayer.ord == 1 &&
                PoolPlayer.mainPlayer.hitCnt != PoolPlayer.otherPlayer.hitCnt;
            other_play_time_send_cnt = 0;
            playTime = 1.0f;
            calculateTime = false;
            if (finish)
            {
                PoolLogic.gameState.gameIsComplete = true;
                CallOnGameComplite();
                return;
            }
            // Equal scores in sudden death continue; a turn timeout is not a match timeout.
            OnEndTime?.Invoke();
        }
        public void CallMatchTimeout()
        {
            if (isMatchTimeEnd || PoolLogic.gameState.gameIsComplete) return;
            isMatchTimeEnd = true;
            calculateTime = false;

            // 볼의 멈춰있는 상태 
            if (!_physicsMng.inMove && !_physicsMng.IsResolvingShot)
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

        bool completionNotified;
        public void CallOnGameComplite()
        {
            if (!PoolLogic.gameState.gameIsComplete || completionNotified) return;
            completionNotified = true;
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
