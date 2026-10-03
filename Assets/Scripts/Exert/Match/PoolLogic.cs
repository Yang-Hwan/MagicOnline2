using Assets.Scripts.Often;
using Assets.Scripts.Sight.Vital.Pavilion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Exert.Match
{

    public class PoolState
    {


        public bool gameIsComplete = false;
        public bool isSurvival = false;
        public bool needToChangeTurn = false;
        public bool shotAchieve = false;
        public bool cueBallInHand = true;
        

        public int ballsHitBoardCount = -1;
        public int ballHitBall_1 = -1;
        public int ballHitBall_2 = -1;
        public int coinGain = 0;

        //public int run = 0;            // 연타
        //public int highRun = 0;         //하이런


        public float moveLength = 0;
        public CueBallMoveAreaLook cueBallMoveAreaLook;
        public CueBallHitPart cueBallHitPart;
        public AsideHitStep asideHitStep;
        public CueBallEarlyMoveDirection cueBallMovDir;     // 수구초기이동방향
        public FirstBallThickness firstBallTic;             // 첫번째볼 두께
        public RunPath runPath;                             // 득점경로
        public RectArea rectArea;                          // 득점경로
        public GoalNear goalNear;                           // 득점근처
        public List<HitKind> hitKinds;                      // 충돌목록 

        public PoolState()
        {
            gameIsComplete = false;
            isSurvival = false;
            needToChangeTurn = false;
            shotAchieve = false;
            cueBallInHand = false;
            ballsHitBoardCount = 0;
            ballHitBall_1 = -1;
            ballHitBall_2 = -1;
            //run = 0;
            //highRun = 0;

            cueBallMoveAreaLook = CueBallMoveAreaLook.None;
            cueBallHitPart = CueBallHitPart.None;
            asideHitStep = AsideHitStep.None;
            cueBallMovDir = CueBallEarlyMoveDirection.None;
            firstBallTic = FirstBallThickness.None;
            runPath = RunPath.None;
            moveLength = 0;
            rectArea = RectArea.None;
            goalNear = GoalNear.None;
            hitKinds = new List<HitKind>();
        }
    }

    public class PoolLogic
    {
        private static PoolState _gameState;
        public static PoolState gameState
        {
            get
            {
                if (_gameState == null)
                {
                    _gameState = new PoolState();
                }
                return _gameState;
            }
        }


        private static PoolLogic _instance = null;
        public static PoolLogic Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new PoolLogic();
                }
                return _instance;
            }
        }

        public static bool controlFromNetwork
        {
            get
            {
                return !PoolPlayer.mainPlayer.myTurn && PoolLogic.Instance != null;
            }
        }

        public static bool controlInNetwork
        {
            get
            {
                return PoolPlayer.mainPlayer.myTurn && PoolLogic.Instance != null;
            }
        }


        public void ResetMatchInit()
        {
            gameState.gameIsComplete = false;
            gameState.isSurvival = false;
        }

        // 스트로크마다 실행. 시합 첫시작시 isMatchTimePlay(false) 시작됨
        public void ResetTurnState(bool isMatchTimePlay)
        {
            gameState.cueBallMoveAreaLook = CueBallMoveAreaLook.None;
            gameState.cueBallHitPart = CueBallHitPart.None;
            gameState.asideHitStep = AsideHitStep.None;
            gameState.cueBallMovDir = CueBallEarlyMoveDirection.None;
            gameState.firstBallTic = FirstBallThickness.None;
            gameState.moveLength = 0;
            gameState.rectArea = RectArea.None;
            gameState.runPath = RunPath.None;
            gameState.goalNear = GoalNear.None;
            gameState.hitKinds.Clear();

            gameState.ballHitBall_1 = -1;
            gameState.ballHitBall_2 = -1;
            gameState.coinGain = 0;

            if (!isMatchTimePlay)
            {
                gameState.gameIsComplete = false;
                gameState.isSurvival = false;
                gameState.shotAchieve = false;
                gameState.needToChangeTurn = false;

                //Debug.Log($"PoolLogic.ResetTurnState isMatchTimePlay ::false");
                return;
            }

            gameState.shotAchieve = false;      
            gameState.needToChangeTurn = true;
            gameState.cueBallInHand = false;
            gameState.ballsHitBoardCount = 0;

 



            //Debug.Log("ResetTurnState ... ");

        }

        // 모든공 멈춘직후
        public void ShotEnded()
        {
            //Debug.Log($"ShotEnded ShotEnded ShotEnded isMatchTimePlay : {PoolCoach.Instance.isMatchTimePlay},  changeTurn : {gameState.needToChangeTurn}, ball_1 : {gameState.ballHitBall_1}, ball_2 : {gameState.ballHitBall_2}");
            if (!PoolCoach.Instance.isMatchTimePlay)
            {
                PoolCoach.Instance.SetMatchTimePlay(true);
                return;
            }

            // Four-ball fouls override a pending score, including a complete miss.
            if (PoolCoach.Instance.matchBall == MatchBall.FourBall &&
                (gameState.hitKinds.Contains(HitKind.Ball_Foul) || gameState.ballHitBall_1 == -1))
            {
                if (!gameState.hitKinds.Contains(HitKind.Ball_Foul))
                    gameState.hitKinds.Add(HitKind.Ball_Foul);
                gameState.needToChangeTurn = true;
                gameState.shotAchieve = false;
                PoolPlayer.SetHitMinus(PoolPlayer.currentPlayer.playerId);
                PoolCoach.Instance.CallOnNoticeHitBall(NoticeHitBall.Foul);
            }

            // 경기방식중 모든움직임 멈준 후 득점 처리라면 (4구 해당)
            if (PoolCoach.Instance.isBallStopAfterJudge)
            {
                //턴 변경이 아닌 상태라면(득점한 경우 .. ) 
                if (!gameState.needToChangeTurn)
                {
                    gameState.shotAchieve = true;
                    //  득점처리, 득점종류, 완료판단 
                    BallHitAchieve();
                }
    
            }

            // 득점 실패시 
            if (gameState.needToChangeTurn)
            {
                BallHitFail();
            }

            // 시합시간 종료되면 
            if (PoolCoach.Instance.isMatchTimeEnd)
            {
                gameState.gameIsComplete = true;
                 
                Debug.Log($"PoolLogic.ShotEnded if (PoolCoach.Instance.isMatchTimeEnd) >>>>>>> ");
            }
            // 생존모드의 후구제일때만 가능함(후구 플레이어)
            //ChkSurvivalRearPlayerIsComplete();

            // 시합종료 여부
            if (gameState.gameIsComplete)
            {
                PoolCoach.Instance.CallOnGameComplite();
            }


            //Debug.Log($"PoolLogic.ShotEnded gameIsComplete : {gameState.gameIsComplete}");

        }



        public void OnBallHitBoard(Ball ball, CushionDir cushionDir)
        {
            // 두번째 볼 맞았다면 카운터 필요없음.
            if (gameState.ballHitBall_2 != -1) return;
            HitKind h = HitKind.None;
            switch (cushionDir)
            {
                case CushionDir.T: h = HitKind.Board_T; break;
                case CushionDir.R: h = HitKind.Board_R; break;
                case CushionDir.B: h = HitKind.Board_B; break;
                case CushionDir.L: h = HitKind.Board_L; break;
            }
            gameState.hitKinds.Add(h);

            gameState.ballsHitBoardCount++;

        }


        public void OnCueBallHitBall(Ball cueBall, Ball ball)
        {
            //Debug.Log($"OnCueBallHitBall -- ");
            int reward = 0;
            bool isHit = false;
            //string msg = string.Empty;
            int ballId = ball.id;

            // 4구인 경우 .. 적구외에는 파울임 
            if (PoolCoach.Instance.matchBall == MatchBall.FourBall)
            {

                if (gameState.hitKinds.Contains(HitKind.Ball_Foul))
                {
                    Debug.Log($"EXISTS FOUL!! EXISTS FOUL!! EXISTS FOUL!! EXISTS FOUL!! ");
                    return;
                }

                // 파울 처리 
                if (ballId == 0 || ballId == 1)
                {
                    gameState.hitKinds.Add(HitKind.Ball_Foul);
                    gameState.needToChangeTurn = true;
                    gameState.shotAchieve = false;
                    //msg += "  cccc ";

                    // Apply the deduction once all balls have stopped.
                }
                else
                {
                    
                    if (gameState.ballHitBall_1 == -1)
                    {
                        gameState.ballHitBall_1 = ballId;
                        gameState.hitKinds.Add(HitKind.Ball_1);
                        //msg += "  dddd ";
                    }
                    else if (gameState.ballHitBall_2 == -1)
                    {
                        if (gameState.ballHitBall_1 == ballId)
                        {
                            return;
                        }

                        // (0,1,3) 포함 
                        if (gameState.ballsHitBoardCount >= PoolPlayer.currentPlayer.checkCushion)
                        {
                            gameState.ballHitBall_2 = ballId;
                            gameState.hitKinds.Add(HitKind.Ball_2);
                            gameState.needToChangeTurn = false;
                            // The point remains provisional until ShotEnded.
                            //msg += "  eeee ";
                        }
                            
                    }

                }

                //Debug.Log($"FourBall >> ball_1 : {gameState.ballHitBall_1}, ball_2 : {gameState.ballHitBall_2} ... needToChangeTurn : {gameState.needToChangeTurn} .... ");
                return;

            }


            // 3구인 경우 
            if (gameState.ballHitBall_1 == -1)
            {
                
                gameState.hitKinds.Add(HitKind.Ball_1);
                gameState.ballHitBall_1 = ballId;
            }

            else if (gameState.ballHitBall_2 == -1)
            {
                if (gameState.ballHitBall_1 != ballId)
                {
                    if (gameState.ballsHitBoardCount >= PoolPlayer.currentPlayer.checkCushion)
                    {
                        gameState.hitKinds.Add(HitKind.Ball_2);
                        gameState.ballHitBall_2 = ballId;
                        gameState.needToChangeTurn = false;   // 턴교체 없음
                        gameState.shotAchieve = true;

                        // 공 멈춤 기다리지 않고 바로 처리(3구일때만) 
                        if (!PoolCoach.Instance.isBallStopAfterJudge)
                        {
                            BallHitAchieve();

                            
                        }
                       


                    }
                    // 미리 맞았다면 실패처리 
                    else
                    {
                        gameState.needToChangeTurn = true;
                        gameState.shotAchieve = false;

                    }
                }
            }


            

            //PoolCoach.Instance.SetMatchInfo($"{PoolPlayer.currentPlayer.name} .. 볼  첫번째공 : {gameState.ballHitBall_1}, 두번째공 : {gameState.ballHitBall_2}, 보드 : {gameState.ballsHitBoardCount} / {(int)PoolCoach.Instance.matchCushion}      ");

        }

        // 득점실패시 시합종료처리
        // 
        void BallHitFail()
        {
            // 선구에서 끝나는 경우는 없음.
            // .. 생존매치에서 후구 실패시에 끝나는 경우는 선구가 성공했을 때 
            if (PoolPlayer.ord == 1)
            {
                // 알빼기에서 초구는 다 뺀(쿠션까지) 상태
                if (PoolLogic.gameState.isSurvival)
                {
                    // 알빼기 남은 상태 
                    if (PoolCoach.Instance.targetHit > PoolPlayer.currentPlayer.hitCnt)
                    {
                        if (PoolPlayer.mainPlayer.hitCnt > PoolPlayer.otherPlayer.hitCnt)
                        {
                            PoolPlayer.SetWinner(PoolPlayer.mainPlayer.playerId);
                            PoolLogic.gameState.gameIsComplete = true;
                        }
                        else if (PoolPlayer.mainPlayer.hitCnt < PoolPlayer.otherPlayer.hitCnt)
                        {
                            PoolPlayer.SetWinner(PoolPlayer.otherPlayer.playerId);
                            PoolLogic.gameState.gameIsComplete = true;
                        }
                        // 
                        else if (PoolPlayer.mainPlayer.hitCnt == PoolPlayer.otherPlayer.hitCnt)
                        {
                            PoolLogic.gameState.needToChangeTurn = true;        // 상대턴으로 변경
                        }
                    }
                    // 이전에 알빼기를 다 뺀 상태 
                    else if (PoolCoach.Instance.targetHit <= PoolPlayer.currentPlayer.hitCnt)
                    {
                        // 마무리 미션 없는 경우
                        if (PoolPlayer.currentPlayer.finishStep == FinishStep.None ||
                            PoolPlayer.currentPlayer.finishStep == FinishStep.Task ||
                            PoolPlayer.currentPlayer.finishStep == FinishStep.Push ||
                            PoolPlayer.currentPlayer.finishStep == FinishStep.Done
                            )
                        {
                            if (PoolPlayer.mainPlayer.hitCnt > PoolPlayer.otherPlayer.hitCnt)
                            {
                                PoolPlayer.SetWinner(PoolPlayer.mainPlayer.playerId);
                                PoolLogic.gameState.gameIsComplete = true;
                            }
                            else if (PoolPlayer.mainPlayer.hitCnt < PoolPlayer.otherPlayer.hitCnt)
                            {
                                PoolPlayer.SetWinner(PoolPlayer.otherPlayer.playerId);
                                PoolLogic.gameState.gameIsComplete = true;
                            }
                            else if (PoolPlayer.mainPlayer.hitCnt == PoolPlayer.otherPlayer.hitCnt)
                            {
                                PoolLogic.gameState.needToChangeTurn = true;        // 상대턴으로 변경
                            }
                        }

                    }

                }
            }
        }

        // 득점처리, 득점종류, 완료판단 
        // 
        void BallHitAchieve()
        {
            int cnt = 1;
            // 3구일때 뱅크샷인 경우 2점
            //if (PoolCoach.Instance.matchBall == MatchBall.ThreeBall)

            // 원쿠션,쓰리쿠션 
            if (PoolCoach.Instance.matchBall == MatchBall.ThreeBall)
            {
                if (PoolPlayer.currentPlayer.checkCushion == 3)
                {
                    if (gameState.hitKinds[0] == HitKind.Board_B ||
                        gameState.hitKinds[0] == HitKind.Board_T ||
                        gameState.hitKinds[0] == HitKind.Board_L ||
                        gameState.hitKinds[0] == HitKind.Board_R)
                    {
                        cnt = 2;
                    }
                }
            }

            // 4구 생존모드에서는 2점 없음.
            if (PoolCoach.Instance.matchBall == MatchBall.FourBall)
            {
                if (PoolPlayer.currentPlayer.checkCushion == 3 && !PoolLogic.gameState.isSurvival)
                {
                    if (gameState.hitKinds[0] == HitKind.Board_B ||
                        gameState.hitKinds[0] == HitKind.Board_T ||
                        gameState.hitKinds[0] == HitKind.Board_L ||
                        gameState.hitKinds[0] == HitKind.Board_R)
                    {
                        cnt = 2;

                    }
                }
            }

            int reward = PoolCoach.Instance.hitReward;
            // 득점으로 유저의 포인트 이동
            PoolPlayer.SetHitAdd(PoolPlayer.currentPlayer.playerId, reward, cnt);
            RunPath runPath = GetRunPath();

            // 득점애니, 점수 반영 
            PoolCoach.Instance.ScoreChanged(cnt, reward, runPath);

            // 목표타격개수를 다 맞혔다면
            //Debug.Log($"BallHitAchieve MYTURN : {PoolPlayer.mainPlayer.myTurn}, cnt : {cnt}, targetHit : {PoolCoach.Instance.targetHit}, cur.hitCnt : {PoolPlayer.currentPlayer.hitCnt}");


            // 후구제 시합이 아니라면 바로 완료처리 .. 사용 안함 
            if (!PoolCoach.Instance.isRearQuarter)
            {
                if (PoolCoach.Instance.targetHit <= PoolPlayer.currentPlayer.hitCnt)
                {
                    PoolPlayer.SetWinner(PoolPlayer.currentPlayer.playerId);
                    gameState.gameIsComplete = true;
                }
            }

            // 후구제 시합인 경우 (무조건 후구제임)
            else
            {
                // 선구인 경우  ------------------------------------------------------------------------- 
                if (PoolPlayer.ord == 0)
                {
                    //PoolCoach.Instance.NextScoreStepCheckOrd0();

                    // 알빼기 단계
                    if (!PoolLogic.gameState.isSurvival)
                    {
                        // 목표개수를 달성했다면
                        if (PoolCoach.Instance.targetHit <= PoolPlayer.currentPlayer.hitCnt)
                        {
                            // 마무리 미션 없는 경우
                            if (PoolPlayer.currentPlayer.finishStep == FinishStep.None)
                            {
                                PoolLogic.gameState.isSurvival = true;              // 생존모드로 전환
                                PoolLogic.gameState.needToChangeTurn = true;        // 상대턴으로 변경
                                PoolCoach.Instance.CallOnNoticeHitBall(NoticeHitBall.Survival);

                            }
                            // 마무리 미션 있는 경우
                            else if (PoolPlayer.currentPlayer.finishStep == FinishStep.Task)
                            {
                                PoolPlayer.currentPlayer.finishStep = FinishStep.Push;      // 마무리처리 중
                                if (PoolCoach.Instance.finishMission == FinishMission.Cushion3)
                                {
                                    PoolPlayer.currentPlayer.checkCushion = 3;
                                }
                                PoolLogic.gameState.needToChangeTurn = false;        // 자신턴을 유지

                                PoolCoach.Instance.CallOnNoticeHitBall(NoticeHitBall.Finish);

                            }
                            // 마무리 처리했음 
                            else if (PoolPlayer.currentPlayer.finishStep == FinishStep.Push)
                            {
                                PoolPlayer.currentPlayer.finishStep = FinishStep.Done;  // 마무리완료
                                PoolCoach.Instance.CallOnFinishAchieve(PoolPlayer.currentPlayer.playerId);

                                PoolLogic.gameState.isSurvival = true;                  // 생존모드로 전환
                                PoolLogic.gameState.needToChangeTurn = true;            // 상대턴으로 변경
                                PoolCoach.Instance.CallOnFinishAchieve(PoolPlayer.currentPlayer.playerId);
                                PoolCoach.Instance.CallOnNoticeHitBall(NoticeHitBall.Survival);

                            }
                        }
                    }
                    // 생존 단계라면(성공)
                    else
                    {
                        PoolLogic.gameState.needToChangeTurn = true;        // 상대턴으로 변경
                    }



                }
                // 후구인 경우  -------------------------------------------------------------------------
                else if (PoolPlayer.ord == 1)
                {
                    //PoolCoach.Instance.NextScoreStepCheckOrd1();
                    //Debug.Log($"survival : {PoolLogic.gameState.isSurvival}, finishStep : {PoolPlayer.currentPlayer.finishStep}, PoolPlayer.currentPlayer.checkCushion : {PoolPlayer.currentPlayer.checkCushion}");

                    // 알빼기 단계
                    if (!PoolLogic.gameState.isSurvival)
                    {
                        // 목표개수를 달성했다면
                        if (PoolCoach.Instance.targetHit <= PoolPlayer.currentPlayer.hitCnt)
                        {
                            // 마무리 미션 없는 경우 승패처리
                            if (PoolPlayer.currentPlayer.finishStep == FinishStep.None)
                            {
                                // 승리 결정
                                // 승패 결정
                                
                                if (PoolPlayer.mainPlayer.hitCnt == PoolPlayer.otherPlayer.hitCnt)
                                {
                                    PoolLogic.gameState.needToChangeTurn = true;        // 상대턴으로 변경
                                }
                                else
                                {
                                    PoolLogic.gameState.gameIsComplete = true;
                                }





                            }
                            // 마무리 미션 있는 경우
                            else if (PoolPlayer.currentPlayer.finishStep == FinishStep.Task)
                            {
                                PoolPlayer.currentPlayer.finishStep = FinishStep.Push;      // 마무리처리 중
                                if (PoolCoach.Instance.finishMission == FinishMission.Cushion3)
                                {
                                    PoolPlayer.currentPlayer.checkCushion = 3;
                                }
                                PoolLogic.gameState.needToChangeTurn = false;        // 자신턴을 유지 
                                PoolCoach.Instance.CallOnNoticeHitBall(NoticeHitBall.Finish);
                            }
                            // 마무리 처리했음 
                            else if (PoolPlayer.currentPlayer.finishStep == FinishStep.Push)
                            {
                                PoolPlayer.currentPlayer.finishStep = FinishStep.Done;  // 마무리완료
                                PoolCoach.Instance.CallOnFinishAchieve(PoolPlayer.currentPlayer.playerId);

                                // 승패 결정
                                if (PoolPlayer.mainPlayer.hitCnt == PoolPlayer.otherPlayer.hitCnt)
                                {
                                    PoolLogic.gameState.needToChangeTurn = true;        // 상대턴으로 변경
                                }
                                else
                                {
                                    PoolLogic.gameState.gameIsComplete = true;
                                }

                            }
                        }
                    }
                    // 생존 단계라면(성공)
                    else
                    {
                        // 목표개수에 도달했다면 
                        // 마무리 미션 없는 경우 승패처리
                        if (PoolPlayer.currentPlayer.finishStep == FinishStep.None)
                        {
                            // 승패 결정
                            int winId = PoolPlayer.GetHitWin();
                            if(winId != -1)
                            {
                                PoolLogic.gameState.gameIsComplete = true;
                            }
                            else
                            {
                                PoolLogic.gameState.needToChangeTurn = true;        // 상대턴으로 변경
                            }
                        }
                        // 마무리 미션 있는 경우
                        else if (PoolPlayer.currentPlayer.finishStep == FinishStep.Task)
                        {
                            PoolPlayer.currentPlayer.finishStep = FinishStep.Push;      // 마무리처리 중
                            if (PoolCoach.Instance.finishMission == FinishMission.Cushion3)
                            {
                                PoolPlayer.currentPlayer.checkCushion = 3;
                            }
                            PoolLogic.gameState.needToChangeTurn = false;        // 자신턴을 유지 
                            PoolCoach.Instance.CallOnNoticeHitBall(NoticeHitBall.Finish);
                        }
                        // 마무리 처리했음 
                        else if (PoolPlayer.currentPlayer.finishStep == FinishStep.Push)
                        {
                            PoolPlayer.currentPlayer.finishStep = FinishStep.Done;  // 마무리완료
                            PoolCoach.Instance.CallOnFinishAchieve(PoolPlayer.currentPlayer.playerId);


                            // 승패 결정
                            int winId = PoolPlayer.GetHitWin();
                            if (winId != -1)
                            {
                                PoolLogic.gameState.gameIsComplete = true;
                            } 
                            else  
                            {
                                PoolLogic.gameState.needToChangeTurn = true;        // 상대턴으로 변경
                            }

                        }
                        else if (PoolPlayer.currentPlayer.finishStep == FinishStep.Done)
                        {
                            // 승패 결정
                            int winId = PoolPlayer.GetHitWin();
                            if (winId != -1)
                            {
                                PoolLogic.gameState.gameIsComplete = true;
                            }
                            else
                            {
                                PoolLogic.gameState.needToChangeTurn = true;        // 상대턴으로 변경
                            }
                        }
                    }

                }
            }

 
 
            //PoolCoach.Instance.SetMatchInfo($"{PoolPlayer.currentPlayer.name} .. 볼  첫번째공 : {gameState.ballHitBall_1}, 두번째공 : {gameState.ballHitBall_2}, 보드 : {gameState.ballsHitBoardCount} / {(int)PoolCoach.Instance.matchCushion}   성공     ");

        }

        // 생존모드의 후구제일때만 가능함(후구 플레이어)
        public void ChkSurvivalRearPlayerIsComplete()
        {
            // 목표 점수 도달
            if (gameState.isSurvival)
            {
                if (PoolCoach.Instance.isRearQuarter)
                {
                    // 후구진행자
                    if (PoolPlayer.ord == 1)
                    {
                        // 득점 실패시 완료처리 
                        if (!gameState.shotAchieve)
                        {
                            // 승패 결정
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
                                gameState.gameIsComplete = true;
                            }
                            // 
                        }
                        // 득점 성공시 처리되어 있음
                        else
                        {

                        }
                    }
                }
            }
            // 








        }


        RunPath GetRunPath()
        {
            RunPath ret = RunPath.None;
            // 세워치기
            ret = ChkHorizontalHit(gameState.cueBallMovDir, gameState.hitKinds);
            if (ret != RunPath.None)
            {
                Debug.Log($"RunPath : {ret}");
                PoolCoach.Instance.SetMatchInfo($"{PoolPlayer.currentPlayer.name} ..     RunPath : {ret}      ");
                return ret;
            }

            // 앞돌리기
            ret = ChkFrontTurn(gameState.cueBallMovDir, gameState.hitKinds);
            if (ret != RunPath.None)
            {
                Debug.Log($"RunPath : {ret}");
                PoolCoach.Instance.SetMatchInfo($"{PoolPlayer.currentPlayer.name} ..     RunPath : {ret}      ");
                return ret;
            }

            // 뒤돌리기
            ret = ChkBackTurn(gameState.cueBallMovDir, gameState.cueBallHitPart, gameState.cueBallMoveAreaLook, gameState.hitKinds, gameState.asideHitStep);
            if (ret != RunPath.None)
            {
                Debug.Log($"RunPath : {ret}");
                PoolCoach.Instance.SetMatchInfo($"{PoolPlayer.currentPlayer.name} ..     RunPath : {ret}      ");
                return ret;
            }

            // 비껴치기
            ret = ChkSideShot(gameState.hitKinds, gameState.asideHitStep);
            if (ret != RunPath.None)
            {
                Debug.Log($"RunPath : {ret}");
                PoolCoach.Instance.SetMatchInfo($"{PoolPlayer.currentPlayer.name} ..     RunPath : {ret}      ");
                return ret;
            }

            // 대회전 
            ret = ChkGiantTurn(gameState.hitKinds, gameState.asideHitStep);
            if (ret != RunPath.None)
            {
                Debug.Log($"RunPath : {ret}");
                PoolCoach.Instance.SetMatchInfo($"{PoolPlayer.currentPlayer.name} ..     RunPath : {ret}      ");
                return ret;
            }

            // 더블쿠션(리버스)  
            ret = ChkReverse(gameState.hitKinds);
            if (ret != RunPath.None)
            {
                Debug.Log($"RunPath : {ret}");
                PoolCoach.Instance.SetMatchInfo($"{PoolPlayer.currentPlayer.name} ..     RunPath : {ret}      ");
                return ret;
            }

            // 횡단샷 
            ret = ChkTraverseShot(gameState.hitKinds);
            if (ret != RunPath.None)
            {
                Debug.Log($"RunPath : {ret}");
                PoolCoach.Instance.SetMatchInfo($"{PoolPlayer.currentPlayer.name} ..     RunPath : {ret}      ");
                return ret;
            }

            // 되돌아오기
            ret = ChkComeBack(gameState.hitKinds);
            if (ret != RunPath.None)
            {
                Debug.Log($"RunPath : {ret}");
                PoolCoach.Instance.SetMatchInfo($"{PoolPlayer.currentPlayer.name} ..     RunPath : {ret}      ");
                return ret;
            }

            // 원뱅크
            ret = ChkBankShot01(gameState.hitKinds);
            if (ret != RunPath.None)
            {
                Debug.Log($"RunPath : {ret}");
                PoolCoach.Instance.SetMatchInfo($"{PoolPlayer.currentPlayer.name} ..     RunPath : {ret}      ");
                return ret;
            }

            // 투뱅크 
            ret = ChkBankShot02(gameState.hitKinds);
            if (ret != RunPath.None)
            {
                Debug.Log($"RunPath : {ret}");
                PoolCoach.Instance.SetMatchInfo($"{PoolPlayer.currentPlayer.name} ..     RunPath : {ret}      ");
                return ret;
            }

            // 쓰리뱅크 
            ret = ChkBankShot03(gameState.hitKinds);
            if (ret != RunPath.None)
            {
                Debug.Log($"RunPath : {ret}");
                PoolCoach.Instance.SetMatchInfo($"{PoolPlayer.currentPlayer.name} ..     RunPath : {ret}      ");
                return ret;
            }

            return ret;

        }


        // 01.세워치기 
        RunPath ChkHorizontalHit(CueBallEarlyMoveDirection dir, List<HitKind> hits)
        {
            RunPath ret = RunPath.None;

            if (dir != CueBallEarlyMoveDirection.Horizontal) return ret;
            if (hits.Count != 5) return ret;                         // 정확히 일치여부 확인 
            if (hits[0] != HitKind.Ball_1) return ret;              // 뱅크샷인경우 세워치기 아님 
            if (hits[1] == HitKind.Board_L || hits[1] == HitKind.Board_R)
            {
                if (hits[2] == HitKind.Board_T || hits[2] == HitKind.Board_B)
                {
                    if (hits[3] == HitKind.Board_L || hits[3] == HitKind.Board_R)
                    {
                        if (GetDistinct(hits[3], hits[1]))
                        {
                            if (hits[4] == HitKind.Ball_2)
                            {
                                ret = RunPath.HorizontalHit;
                            }
                        }
                    }
                }
            }
            return ret;
        }

        // 02.앞돌리기
        RunPath ChkFrontTurn(CueBallEarlyMoveDirection dir, List<HitKind> hits)
        {
            RunPath ret = RunPath.None;

            if (dir == CueBallEarlyMoveDirection.Vertical) return ret;
            if (hits.Count < 5 || hits.Count > 6) return ret;                           // 정확히(5 또는 6)아니면 아웃
            if (hits[0] != HitKind.Ball_1) return ret;                                  // 뱅크샷인경우 아웃
            if (hits[1] == HitKind.Board_L || hits[1] == HitKind.Board_R)
            {
                if (hits[2] == HitKind.Board_T || hits[2] == HitKind.Board_B)
                {
                    if (hits[3] == HitKind.Board_T || hits[3] == HitKind.Board_B)
                    {
                        if (GetDistinct(hits[3], hits[2]))   // 2번째 쿠션과 3번째 쿠션 불일치 필요
                        {
                            if (hits[hits.Count - 1] == HitKind.Ball_2)
                            {
                                ret = RunPath.FrontTurn;
                            }
                        }
                    }
                }
            }
            return ret;
        }

        // 03.뒤돌리기
        RunPath ChkBackTurn(CueBallEarlyMoveDirection dir, CueBallHitPart part, CueBallMoveAreaLook look, List<HitKind> hits, AsideHitStep isAsideHit)
        {
            RunPath ret = RunPath.None;

            if (dir == CueBallEarlyMoveDirection.Vertical) return ret;                  // 수직방향이면 아웃
            if (isAsideHit == AsideHitStep.Appoint) return ret;                         // 비껴치기이면 아웃
            if (hits.Count < 5 || hits.Count > 7) return ret;                           // 정확히(5 ~ 7)아니면 아웃
            if (hits[0] != HitKind.Ball_1) return ret;                                  // 뱅크샷인 경우 아웃

            if (look == CueBallMoveAreaLook.Left)
            {
                if (part == CueBallHitPart.Left)
                {
                    if (hits[1] == HitKind.Board_T)
                    {
                        if (hits[2] == HitKind.Board_L)
                        {
                            if (hits[3] == HitKind.Board_B)
                            {
                                if (GetDistinct(hits[3], hits[1]))   // 1번째 쿠션과 3번째 쿠션 불일치 필요
                                {
                                    if (hits[hits.Count - 1] == HitKind.Ball_2)
                                    {
                                        ret = RunPath.BackTurn;
                                    }
                                }
                            }
                        }
                    }
                }
                else if (part == CueBallHitPart.Right)
                {
                    if (hits[1] == HitKind.Board_B)
                    {
                        if (hits[2] == HitKind.Board_L)
                        {
                            if (hits[3] == HitKind.Board_T)
                            {
                                if (GetDistinct(hits[3], hits[1]))   // 1번째 쿠션과 3번째 쿠션 불일치 필요
                                {
                                    if (hits[hits.Count - 1] == HitKind.Ball_2)
                                    {
                                        ret = RunPath.BackTurn;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else if (look == CueBallMoveAreaLook.Right)
            {
                if (part == CueBallHitPart.Right)
                {
                    if (hits[1] == HitKind.Board_T)
                    {
                        if (hits[2] == HitKind.Board_R)
                        {
                            if (hits[3] == HitKind.Board_B)
                            {
                                if (GetDistinct(hits[3], hits[1]))   // 1번째 쿠션과 3번째 쿠션 불일치 필요
                                {
                                    if (hits[hits.Count - 1] == HitKind.Ball_2)
                                    {
                                        ret = RunPath.BackTurn;
                                    }
                                }
                            }
                        }
                    }
                }
                else if (part == CueBallHitPart.Left)
                {
                    if (hits[1] == HitKind.Board_B)
                    {
                        if (hits[2] == HitKind.Board_R)
                        {
                            if (hits[3] == HitKind.Board_T)
                            {
                                if (GetDistinct(hits[3], hits[1]))   // 1번째 쿠션과 3번째 쿠션 불일치 필요
                                {
                                    if (hits[hits.Count - 1] == HitKind.Ball_2)
                                    {
                                        ret = RunPath.BackTurn;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return ret;
        }

        // 04.비껴치기
        RunPath ChkSideShot(List<HitKind> hits, AsideHitStep isAsideHit)
        {
            RunPath ret = RunPath.None;

            if (isAsideHit != AsideHitStep.Appoint) return ret;                                                // 비껴치기가 아니면 아웃
            if (hits.Count < 5 || hits.Count > 7) return ret;                           // 정확히(5 ~ 7)아니면 아웃
            if (hits[0] != HitKind.Ball_1) return ret;                                  // 뱅크샷인 경우 아웃

            if (GetAdjoin(hits[2], hits[1]))         // 인접해야함
            {
                if (GetAdjoin(hits[3], hits[2]))    // 인접해야함
                {
                    if (GetDistinct(hits[3], hits[1]))   // 1번째 쿠션과 3번째 쿠션 불일치 필요
                    {
                        if (hits[hits.Count - 1] == HitKind.Ball_2)
                        {
                            ret = RunPath.AsideShot;
                        }
                    }
                }
            }
            return ret;
        }

        // 05.대회전 
        RunPath ChkGiantTurn(List<HitKind> hits, AsideHitStep isAsideHit)
        {
            RunPath ret = RunPath.None;

            if (isAsideHit == AsideHitStep.Appoint) return ret;                                                 // 비껴치기이면 아웃
            if (hits.Count < 6) return ret;                                             // 최소 4쿠션 이상 터치 필요
            if (hits[0] != HitKind.Ball_1) return ret;                                  // 뱅크샷인 경우 아웃

            if (GetAdjoin(hits[2], hits[1]))            // 인접해야함
            {
                if (GetAdjoin(hits[3], hits[2]))        // 인접해야함
                {
                    if (GetAdjoin(hits[4], hits[3]))    // 인접해야함
                    {
                        if (GetDistinct(hits[3], hits[1]))   // 1번째 쿠션과 3번째 쿠션 불일치 필요
                        {
                            if (GetDistinct(hits[4], hits[2]))   // 2번째 쿠션과 4번째 쿠션 불일치 필요
                            {
                                if (hits[hits.Count - 1] == HitKind.Ball_2)
                                {
                                    ret = RunPath.GiantTurn;
                                }
                            }
                        }
                    }
                }
            }
            return ret;
        }

        // 06.리버스(더블쿠션)
        RunPath ChkReverse(List<HitKind> hits)
        {
            RunPath ret = RunPath.None;
            //if (isAsideHit) return ret;                                                 // 비껴치기이면 아웃
            if (hits.Count < 5) return ret;                                             // 최소 3쿠션 이상 터치 필요
            if (hits[0] != HitKind.Ball_1) return ret;                                  // 뱅크샷인 경우 아웃

            if (!GetAdjoin(hits[2], hits[1]))            // 떨어져있는 쿠션
            {
                if (GetDistinct(hits[2], hits[1]))       //  다른 쿠션   
                {
                    if (GetAdjoin(hits[3], hits[2]))
                    {
                        if (hits[hits.Count - 1] == HitKind.Ball_2)
                        {
                            ret = RunPath.Reverse;
                        }
                    }
                }
            }
            return ret;
        }

        // 07.횡단샷
        RunPath ChkTraverseShot(List<HitKind> hits)
        {
            RunPath ret = RunPath.None;
            //if (isAsideHit) return ret;                                                 // 비껴치기이면 아웃
            if (hits.Count < 5) return ret;                                             // 최소 3쿠션 이상 터치 필요
            if (hits[0] != HitKind.Ball_1) return ret;                                  // 뱅크샷인 경우 아웃

            if (!GetAdjoin(hits[2], hits[1]))               // 2쿠션과 1쿠션이 떨어져있는 쿠션
            {
                if (GetDistinct(hits[2], hits[1]))          // 다른 쿠션   
                {
                    if (!GetAdjoin(hits[3], hits[2]))       // 3쿠션과 2쿠션이 떨어져있는 쿠션
                    {
                        if (hits[hits.Count - 1] == HitKind.Ball_2)
                        {
                            ret = RunPath.TraverseShot;
                        }
                    }
                }
            }
            return ret;
        }

        // 08.되돌아오기
        RunPath ChkComeBack(List<HitKind> hits)
        {
            RunPath ret = RunPath.None;
            if (hits.Count != 5) return ret;                                             // 최소 3쿠션 이상 터치 필요
            int c = hits[0] == HitKind.Ball_1 ? 1 : 0;                       // 첫충돌이 볼인경우 쿠션이 밀림
            // 
            if (GetAdjoin(hits[1 + c], hits[0 + c]))                        // 2쿠션과 1쿠션이 인접
            {
                if (GetAdjoin(hits[2 + c], hits[1 + c]))                    // 3쿠션과 2쿠션이 인접
                {
                    if (!GetDistinct(hits[2 + c], hits[0 + c]))             // 3쿠션과 1쿠션이 동일
                    {
                        if (hits[hits.Count - 1] == HitKind.Ball_2)
                        {
                            ret = RunPath.ComBackShot;
                        }
                    }
                }
            }
            return ret;
        }

        // 09.원뱅크샷
        RunPath ChkBankShot01(List<HitKind> hits)
        {
            RunPath ret = RunPath.None;
            if (hits.Count != 5) return ret;                                             // 최소 3쿠션 이상 터치 필요

            // 첫번째 충돌이 볼이 아닌경우 
            if (hits[0] != HitKind.Ball_1)
            {
                // 두번째 충돌이 볼인 경우 
                if (hits[1] == HitKind.Ball_1)
                {
                    if (hits[hits.Count - 1] == HitKind.Ball_2)
                    {
                        ret = RunPath.BankShot01;
                    }
                }
            }

            return ret;
        }

        // 10.투뱅크샷
        RunPath ChkBankShot02(List<HitKind> hits)
        {
            RunPath ret = RunPath.None;
            if (hits.Count != 5) return ret;                                             // 최소 3쿠션 이상 터치 필요

            // 첫번째 충돌이 볼이 아닌경우 
            if (hits[0] != HitKind.Ball_1)
            {
                // 두번째 충돌이 볼이 아닌경우 
                if (hits[1] != HitKind.Ball_1)
                {
                    // 세번째 충돌이 볼인 경우 
                    if (hits[2] == HitKind.Ball_1)
                    {
                        if (hits[hits.Count - 1] == HitKind.Ball_2)
                        {
                            ret = RunPath.BankShot02;
                        }
                    }
                }
            }
            return ret;
        }

        // 11.쓰리뱅크샷
        RunPath ChkBankShot03(List<HitKind> hits)
        {
            RunPath ret = RunPath.None;
            if (hits.Count != 5) return ret;                                             // 최소 3쿠션 이상 터치 필요

            // 첫번째 충돌이 볼이 아닌경우 
            if (hits[0] != HitKind.Ball_1)
            {
                // 두번째 충돌이 볼이 아닌경우 
                if (hits[1] != HitKind.Ball_1)
                {
                    // 세번째 충돌이 볼이 아닌경우 
                    if (hits[2] != HitKind.Ball_1)
                    {
                        if (hits[hits.Count - 1] == HitKind.Ball_2)
                        {
                            ret = RunPath.BankShot03;
                        }
                    }
                }
            }
            return ret;
        }

        // 인접 쿠션
        bool GetAdjoin(HitKind chk, HitKind pre)
        {
            bool ret = false;

            if (pre == HitKind.Board_T || pre == HitKind.Board_B)
            {
                if (chk == HitKind.Board_L || chk == HitKind.Board_R)
                {
                    ret = true;
                }
            }
            else
            {
                if (chk == HitKind.Board_T || chk == HitKind.Board_B)
                {
                    ret = true;
                }
            }
            return ret;
        }

        // 반복 쿠션
        bool GetRepeat(HitKind chk, HitKind pre)
        {
            bool ret = false;
            if (chk == pre)
                ret = true;
            return ret;
        }

        // 유일 쿠션 이전 1개와 비교
        bool GetDistinct(HitKind chk, HitKind pre)
        {
            bool ret = false;
            if (chk != pre)
                ret = true;
            return ret;
        }




        // 수구 속도가 빠를때는 너무가까이에서 적구가 찾아지기 때문에 메시지(기존 조마조마한 메시지류는 나오기 힘듬)
        public Ball GetTargetBall
        {
            get
            {
                Ball ball = null;
                if (!gameState.shotAchieve)
                {
                    Ball[] balls = PoolCoach.Instance.balls;
                    if (gameState.ballHitBall_2 == -1 && gameState.ballHitBall_1 != -1)
                    {
                        for (int i = 0; i < balls.Length; i++)
                        {
                            if (balls[i].id == PoolPlayer.turnId)
                            {
                                continue;
                            }
                            else if (balls[i].id == gameState.ballHitBall_1)
                            {
                                continue;
                            }
                            else
                            {
                                // 적당한거리 0.2 ~ 0.7
                                float dist_min = 0.05f;
                                float dist_max = 3.8f;
                                float dist = Vector3.Distance(balls[i].transform.position, balls[PoolPlayer.turnId].transform.position);
                                if (dist_min < dist && dist < dist_max)
                                {
                                    ball = balls[i];
                                }
                                else
                                {
                                    //Debug.Log($"dist : {dist} (( no )) ... target.id : {balls[i].id} [min: 0.3f, max: 1.8]");
                                    continue;
                                }
                            }
                        }
                    }
                }
                return ball;
            }
        }




        public void OnMatchAbort(bool isWin)
        {

            gameState.gameIsComplete = true;
            if (isWin)
            {
                PoolPlayer.SetWinner(PoolPlayer.mainPlayer.playerId);
            }

            PoolPlayer.PlayerScoreClear();

        }

    }
}
