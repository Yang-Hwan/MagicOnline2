using Assets.TutorialInfo.Scripts.TableSet06.Often;
using Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.TutorialInfo.Scripts.TableSet06.Excert.Match
{


    public class PoolState
    {
        public PoolState Copy()
        {
            var copy = (PoolState)MemberwiseClone();
            copy.hitKinds = new List<HitKind>(hitKinds);
            return copy;
        }
        public bool gameIsComplete = false;
        public bool needToChangeTurn = false;
        public bool shotAchieve = false;
        public bool shotFailed = false;

        public bool tableIsOpened = true;
        public bool cueBallInHand = true;
        public int ballsHitBoardCount = 0;
        public int ballHitBall_1 = -1;
        public int ballHitBall_2 = -1;


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
            needToChangeTurn = false;
            shotAchieve = false;
            shotFailed = false;

            tableIsOpened = true;
            cueBallInHand = true;
            ballsHitBoardCount = 0;
            ballHitBall_1 = -1;
            ballHitBall_2 = -1;

            cueBallMoveAreaLook = CueBallMoveAreaLook.None;
            cueBallHitPart = CueBallHitPart.None;
            asideHitStep =  AsideHitStep.None;
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
        public static void RestorePracticeState(PoolState state) => _gameState = state.Copy();

        private static PoolState _gameState;

        public static PoolState gameState
        {
            get
            {
                if(_gameState == null)
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
                if(_instance == null)
                {
                    _instance = new PoolLogic();
                }
                return _instance;
            }
        }


        // 수구 속도가 빠를때는 너무가까이에서 적구가 찾아지기 때문에 메시지(기존 조마조마한 메시지류는 나오기 힘듬)
        public BallC GetTargetBall
        {
            get
            {
                BallC ball = null;
                if (!gameState.shotAchieve)
                {
                    BallC[] balls = PoolCoach.Instance.balls;
                    if (gameState.ballHitBall_2 == -1 && gameState.ballHitBall_1 != -1)
                    {
                        for (int i = 0; i < balls.Length; i++)
                        {
                            if(balls[i].id == PoolPlayer.turnId)
                            {
                                continue;
                            }
                            else if(balls[i].id == gameState.ballHitBall_1)
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


        public void MatchBegin( )
        {
                                                // 볼 4구, 3구, 15구
                                                // 타격대기시간
            RessetTurnState();
        }

        public void RessetTurnState()
        {
            //UnityEngine.Debug.Log("RessetState ^___^");
            gameState.gameIsComplete = false;
            gameState.needToChangeTurn = true;
            gameState.shotAchieve = false;
            gameState.shotFailed = false;
            gameState.cueBallInHand = false;
            gameState.ballsHitBoardCount = 0;
            gameState.ballHitBall_1 = -1;
            gameState.ballHitBall_2 = -1;               //

            gameState.cueBallMoveAreaLook = CueBallMoveAreaLook.None;
            gameState.cueBallHitPart = CueBallHitPart.None;
            gameState.asideHitStep =  AsideHitStep.None;
            gameState.cueBallMovDir = CueBallEarlyMoveDirection.None;
            gameState.firstBallTic = FirstBallThickness.None;
            gameState.moveLength = 0;
            gameState.rectArea = RectArea.None;
            gameState.runPath = RunPath.None;
            gameState.goalNear = GoalNear.None;
            gameState.hitKinds.Clear();

        }






        public void OnBallHitBoard(BallC ball, CushionDir cushionDir)
        {
            if (!gameState.tableIsOpened) return;
            // 두번째 볼 맞았다면 카운터 필요없음.
            if (gameState.ballHitBall_2 != -1) return;

            HitKind h = HitKind.None;
            switch( cushionDir)
            {
                case CushionDir.T: h = HitKind.Board_T; break;
                case CushionDir.R: h = HitKind.Board_R; break;
                case CushionDir.B: h = HitKind.Board_B; break;
                case CushionDir.L: h = HitKind.Board_L; break;
            }
            gameState.hitKinds.Add(h);
            gameState.ballsHitBoardCount++;


           // string firstBall = gameState.ballHitBall_1 == -1 ? " .. " : $", 첫번째 공 : {gameState.ballHitBall_1}";
           // string msg = $"{PoolPlayer.currentPlayer.name} ..  보드 : {gameState.ballsHitBoardCount} , 볼 : {firstBall}, 힘 : {ball.body.velocity.magnitude} ]";
           // PoolCoach.Instance.SetMatchInfo(msg);
        }


        void test()
        {

            
        }

        // 쿠션체크 후 공미션 맞춤 성공여부확인
        public void OnCueBallHitBall(BallC cueBall, BallC ball)
        {
            int reward = 0;
            bool isHit = false;
            string msg = string.Empty;
            if (!gameState.tableIsOpened || gameState.gameIsComplete) return;
            int ballId = ball.id;

            // Each player uses their own cue ball (white 0 / yellow 1).
            // Both reds score; contacting the other player's cue ball is a foul.
            if (PoolCoach.Instance.matchBall == MatchBall.FourBall)
            {
                if (gameState.hitKinds.Contains(HitKind.Ball_Foul)) return;
                if (ballId == 1 - cueBall.id)
                {
                    gameState.hitKinds.Add(HitKind.Ball_Foul);
                    gameState.shotFailed = true;
                    gameState.shotAchieve = false;
                    gameState.needToChangeTurn = true;
                    return;
                }
                if (ballId != 2 && ballId != 3) return;
            }
            if (gameState.shotAchieve || gameState.shotFailed) return;
            if(gameState.ballHitBall_1 == -1)
            {


                gameState.hitKinds.Add(HitKind.Ball_1);
                gameState.ballHitBall_1 = ballId;

                //msg = $"hit ball exist first ball : {ball.id}, first ball pos : {ball.transform.position}, cueball pos : {cueBall.transform.position},  ";
                //UnityEngine.Debug.Log(msg);
                //PoolCoach.Instance.SetMatchInfo($"{PoolPlayer.currentPlayer.name} .. 볼  첫번째공 : {gameState.ballHitBall_1}, 힘 : {cueBall.body.velocity.magnitude} ");
            }
            else if (gameState.ballHitBall_2 == -1)
            {
                if (gameState.ballHitBall_1 == ballId)
                {
                }
                else
                {
                    // 쿠션 목표 달성 체크
                    //Debug.Log($"cushion hit : {gameState.ballsHitBoardCount} .. matchcushion : {(int)PoolCoach.Instance.matchCushion}");
                    if(gameState.ballsHitBoardCount >= (int)PoolCoach.Instance.matchCushion)
                    {
                        gameState.hitKinds.Add(HitKind.Ball_2);
                        gameState.ballHitBall_2 = ballId;
                        gameState.needToChangeTurn = false;   // 턴교체 없음
                        gameState.shotAchieve = !PoolCoach.Instance.isBallStopAfterJudge;

                        // 공 멈추기 전에 득점 처리
                        if (!PoolCoach.Instance.isBallStopAfterJudge)
                        {
                            isHit = true;
                            reward = PoolCoach.Instance.hitReward;
                            // 득점으로 유저의 포인트 이동
                            PoolPlayer.SetHitAdd(PoolPlayer.currentPlayer.playerId, reward);
                            // 득점명칭 
                            RunPath runPath = GetRunPath();

                            PoolCoach.Instance.ScoreChanged(isHit, reward, runPath);

                        }
                        //PoolCoach.Instance.SetMatchInfo($"{PoolPlayer.currentPlayer.name} .. 볼  첫번째공 : {gameState.ballHitBall_1}, 두번째공 : {gameState.ballHitBall_2}, 보드 : {gameState.ballsHitBoardCount} / {(int)PoolCoach.Instance.matchCushion}, 힘 : {cueBall.body.velocity.magnitude}   성공     ");
                    }
                    else
                    {
                        // Contacting the second object ball before the required
                        // cushions completes the miss for this shot.
                        gameState.shotFailed = true;
                        gameState.needToChangeTurn = true;
                        //PoolCoach.Instance.SetMatchInfo($"{PoolPlayer.currentPlayer.name} .. 볼  첫번째공 : {gameState.ballHitBall_1}, 두번째공 : {ballId}, 보드 : {gameState.ballsHitBoardCount} / {(int)PoolCoach.Instance.matchCushion}, 힘 : {cueBall.body.velocity.magnitude} 실패      ");
                    }
                }
            }
        }

        public void OnEndShot(out bool gameIsEnd, out bool isHit, out int reward, bool init = false)
        {
            reward = 0;
            isHit = false;
            gameIsEnd = false;
            string msgFirst = "";
            string msgArea = "";
            string msgDir = "";
            string msgHit = "";
             
            // 초구 뿌리기라면
            if (init)
            {
                gameState.needToChangeTurn = false;
                gameState.shotAchieve = false;
                return;
            }

            // Resolve fouls before awarding a pending point or checking for a win.
            if (PoolCoach.Instance.matchBall == MatchBall.FourBall &&
                Assets.Scripts.Often.FourBallRules.Resolve(gameState.ballHitBall_1 != -1,
                    gameState.ballHitBall_2 != -1, gameState.hitKinds.Contains(HitKind.Ball_Foul)) ==
                    Assets.Scripts.Often.FourBallShotOutcome.Foul)
            {
                if (!gameState.hitKinds.Contains(HitKind.Ball_Foul))
                    gameState.hitKinds.Add(HitKind.Ball_Foul);
                gameState.shotFailed = true;
                gameState.shotAchieve = false;
                gameState.needToChangeTurn = true;
                PoolPlayer.SetHitMinus(PoolPlayer.currentPlayer.playerId);
                PoolCoach.Instance.ScoreChanged(false, 0, RunPath.None);
            }

            if(gameState.ballHitBall_2 != -1 && !gameState.shotFailed)
            {
             //   msg += $" 두번째 공 : {gameState.ballHitBall_2}";
                gameState.needToChangeTurn = false;

                // 
                if (PoolCoach.Instance.isBallStopAfterJudge)
                {
                    isHit = true;
                    gameState.shotAchieve = true;
                    // 득점명칭 
                   RunPath runPath = GetRunPath();

                    reward = PoolCoach.Instance.hitReward;
                    // 득점으로 유저의 포인트 이동
                    PoolPlayer.SetHitAdd(PoolPlayer.currentPlayer.playerId, reward);
                    PoolCoach.Instance.ScoreChanged(true, reward, runPath);
            
                }

                // 다 맞쳐서 종료상태라면
                if (PoolPlayer.currentPlayer.hitCnt >= PoolCoach.Instance.targetHit)
                {
                    gameState.shotAchieve = true;
                    gameState.gameIsComplete = true;
                    gameIsEnd = true;
                    // Practice has no wallet settlement or deductions from the opponent.
                    if (PoolCoach.Instance.isRulePractice) PoolPlayer.currentPlayer.isWinner = true;
                    else PoolPlayer.SetWinner(PoolPlayer.currentPlayer.playerId);
                }
            }
            else
            {
        //        msg += $" 두번째 공 : .. ";
                gameState.needToChangeTurn = true;
            }


            for (int i = 0; i < gameState.hitKinds.Count; i++)
            {
                HitKind h = gameState.hitKinds[i];
                string s = string.Empty;
                switch (h)
                {
                    case HitKind.Ball_1: s = "볼1";break;
                    case HitKind.Ball_2: s = "볼2"; break;
                    case HitKind.Ball_Foul: s = "파울"; break;
                    case HitKind.Board_T: s = "T"; break;
                    case HitKind.Board_R: s = "R"; break;
                    case HitKind.Board_B: s = "B"; break;
                    case HitKind.Board_L: s = "L"; break;
                }
                msgHit += $"{s} > ";
            }



            switch (gameState.firstBallTic)
            {
                case FirstBallThickness.Thin: msgFirst = "얇게"; break;
                case FirstBallThickness.Medium: msgFirst = "보통"; break;
                case FirstBallThickness.Heavy: msgFirst = "두껍게"; break;
            }

            switch (gameState.cueBallMovDir)
            {
                case CueBallEarlyMoveDirection.Horizontal : msgDir = "수평"; break;
                case CueBallEarlyMoveDirection.Vertical : msgDir = "수직"; break;
                case CueBallEarlyMoveDirection.Diagonal : msgDir = "대각"; break;
            }

            switch (gameState.rectArea)
            {
                case RectArea.Left: msgArea = "좌측"; break;
                case RectArea.Right: msgArea = "우측"; break;
                case RectArea.Both: msgArea = "양쪽"; break;
            }

            string msg = $"{PoolPlayer.currentPlayer.name} .. 멈춤 >> 득점 : {isHit}, 영역 : {msgArea}, 두께 : {msgFirst}, 방향 : {msgDir}, 로그 : {msgHit} ";

            if (!PoolCoach.Instance.isRulePractice) PoolCoach.Instance.SetMatchInfo(msg);

            Debug.Log(msg);
        }


        RunPath GetRunPath()
        {
            RunPath ret = RunPath.None;
            // 세워치기
            ret = ChkHorizontalHit(gameState.cueBallMovDir, gameState.hitKinds);
            if(ret != RunPath.None) {
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
            ret = ChkBackTurn(gameState.cueBallMovDir, gameState.cueBallHitPart, gameState.cueBallMoveAreaLook,  gameState.hitKinds, gameState.asideHitStep);
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


        //

        // 01.세워치기 
        RunPath ChkHorizontalHit(CueBallEarlyMoveDirection dir, List<HitKind> hits)
        {
            RunPath ret = RunPath.None;

            if (dir != CueBallEarlyMoveDirection.Horizontal) return ret;
            if(hits.Count != 5) return ret;                         // 정확히 일치여부 확인 
            if (hits[0] != HitKind.Ball_1) return ret;              // 뱅크샷인경우 세워치기 아님 
            if (hits[1] == HitKind.Board_L || hits[1] == HitKind.Board_R)
            {
                if (hits[2] == HitKind.Board_T || hits[2] == HitKind.Board_B)
                {
                    if (hits[3] == HitKind.Board_L || hits[3] == HitKind.Board_R)
                    {
                        if(GetDistinct(hits[3], hits[1]))
                        {
                            if(hits[4] == HitKind.Ball_2)
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
                            if (hits[hits.Count-1] == HitKind.Ball_2)
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
        RunPath ChkBackTurn(CueBallEarlyMoveDirection dir, CueBallHitPart part, CueBallMoveAreaLook look, List<HitKind> hits,  AsideHitStep isAsideHit)
        {
            RunPath ret = RunPath.None;

            if (dir == CueBallEarlyMoveDirection.Vertical) return ret;                  // 수직방향이면 아웃
            if (isAsideHit == AsideHitStep.Appoint) return ret;                         // 비껴치기이면 아웃
            if (hits.Count < 5 || hits.Count > 7) return ret;                           // 정확히(5 ~ 7)아니면 아웃
            if (hits[0] != HitKind.Ball_1) return ret;                                  // 뱅크샷인 경우 아웃

            if(look == CueBallMoveAreaLook.Left)
            {
                if(part == CueBallHitPart.Left)
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
                else if(part == CueBallHitPart.Right)
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

            if (isAsideHit != AsideHitStep.Appoint ) return ret;                                                // 비껴치기가 아니면 아웃
            if (hits.Count < 5 || hits.Count > 7) return ret;                           // 정확히(5 ~ 7)아니면 아웃
            if (hits[0] != HitKind.Ball_1) return ret;                                  // 뱅크샷인 경우 아웃

            if(GetAdjoin(hits[2], hits[1]))         // 인접해야함
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
            int c = hits[0] == HitKind.Ball_1? 1 : 0;                       // 첫충돌이 볼인경우 쿠션이 밀림
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

            if(pre == HitKind.Board_T || pre == HitKind.Board_B)
            {
                if(chk == HitKind.Board_L || chk == HitKind.Board_R)
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
        // 유일 쿠션 이전 2개와 비교
        bool GetDistinct2(HitKind chk, HitKind pre0, HitKind pre1)
        {
            bool ret = false;
 
            if (chk != pre0)
                if (chk != pre1)
                    ret = true;
            return ret;
        }
        // 유일 쿠션 이전 3개와 비교
        bool GetDistinct3(HitKind chk, HitKind pre0, HitKind pre1, HitKind pre2)
        {
            bool ret = false;

            if(chk != pre0)
                if (chk != pre1)
                    if (chk != pre2)
                        ret = true;
            return ret;
        }


        public void ShotEnded()
        {
            gameState.cueBallInHand = false;
            gameState.ballsHitBoardCount = 0;
            gameState.ballHitBall_1 = -1;
            gameState.ballHitBall_2 = -1;

            gameState.shotAchieve = false;  // 샷 성공 
            gameState.shotFailed = false;
            gameState.cueBallMoveAreaLook = CueBallMoveAreaLook.None;
            gameState.cueBallHitPart = CueBallHitPart.None;
            gameState.asideHitStep =  AsideHitStep.None ;
            gameState.cueBallMovDir = CueBallEarlyMoveDirection.None;
            gameState.firstBallTic = FirstBallThickness.None;
            gameState.moveLength = 0;
            gameState.rectArea = RectArea.None;
            gameState.runPath = RunPath.None;
            gameState.goalNear = GoalNear.None;
            gameState.hitKinds.Clear();

            //PoolCoach.Instance.SetMatchInfo($"{PoolPlayer.currentPlayer.name} ..      ShotEnded   ");

        }


    }


}
