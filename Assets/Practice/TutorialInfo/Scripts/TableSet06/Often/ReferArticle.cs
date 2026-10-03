using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.TutorialInfo.Scripts.TableSet06.Often
{
    public enum PlayType
    {
        PlayerAI = 0,       // AI (인간, ai)
        StepMission,        // 단계미션(인간, x)
        Single,            // 혼자(인간,인간)
        OnLine,             // 온라인(인간,통신)
        Replay              // 재생(x, x) .로직관여없음.
    }

    public enum StateCheck
    {
        None,
        Check,
        End
    }
    
    public enum StateWeaken
    {
        None,
        Start,              // 약함 돌입
        Check,              // 달성여부 확인
        TargetFind,         // 목표로 향함
        TargetPosFind,      // 목표로 향함
        TargetPosLook,      // 목표로 향함
        TargetBallFind,      // 목표로 향함
        TargetBallLook,      // 목표로 향함
        TargetPosFar,          // 목표를 지나침
        TargetBallFar,          // 목표를 지나침
        TargetNone,
        Achieve,            // 목표달성 
        Failure,            // 실패 
        Kiss,               // 상대의 이동으로 충돌
        End                 // 상황 종료 
    }

    public enum BallMovingStory
    {
        None,
        Weaken,             // 힘빠짐
        Kiss,               // 충돌
        TargetFind,      // 타겟향함
        TargetPosFind,      // 타겟향함
        TargetPosLook,      // 타겟향함
        TargetBallFind,      // 타겟향함
        TargetBallLook,     // 타겟향함
        TargetPosFar,         // 타겟패스
        TargetBallFar,         // 타겟패스
        TargetNone,	        // 타겟없음
        AchieveHit,         // 타겟적중
    }

    // 4구, 3구
    public enum MatchBall
    {
        FourBall = 4,
        ThreeBall = 3,
    }

    // 쿠션종류
    public enum MatchCushion
    {
        ZeroCusion = 0,
        OneCusion = 1,
        TwoCusion = 2,
        ThreeCusion = 3,
    }



    //---------------- 해설참조 부터 ----------------------//

    public enum CueBallHitPart
    {
        None,
        Left,
        Right,
        Front,
    }

    public enum CueBallMoveAreaLook
    {
        None,
        Left,
        Right
    }

    // 비껴치기 단계  : 수구와 적구가 충돌시 충돌전과 위치가 같은 방향이면 준비
    public enum AsideHitStep
    {
        None,               // 없음
        Ready,              // 준비
        Appoint,            // 지정
    }
    
    
    // 수구 이동방향
    public enum CueBallEarlyMoveDirection
    {
        None,                   // 
        Horizontal,             // 가로(Width)
        Vertical,               // 세로(Height)
        Diagonal,               // 대각 
    }

    // 첫번째볼 두께
    public enum FirstBallThickness
    {
        None,
        Thin,                       // 얇게치기  0.1
        Medium,
        Heavy,                      // 두껍게치기 0.7
    }

    // 정사각형2개(좌,우)
    public enum RectArea
    {
        None,
        Left,
        Right,
        Both
    }

    // 충돌개체 
    public enum HitKind
    {
        None,
        Ball_1,
        Ball_2,
        Ball_Foul,
        Board_T,
        Board_R,
        Board_B,
        Board_L,
    }

    // 득점근처 
    public enum GoalNear
    {
        None,
        UnChecked,                  // 미점검
        InspectionCompleted,        // 점검완료
    }

    // 득점경로
    public enum RunPath
    {
        None,
        HorizontalHit,              // 세워치기  (단, 장, 단)
        FrontTurn,                  // 앞돌리기
        SideTurnShort,              // 옆돌리기(짧게)
        SideTurnLong,               // 옆돌리기(길게)
        BackTurn,                   // 뒤돌리기    (   )
        AsideShot,                  // 비껴치기    (볼,쿠)
        ComBackShot,                // 되돌아오기   (쿠,볼)
        BankShot01,                 // 원뱅크샷
        BankShot02,                 // 투뱅크샷
        BankShot03,                 // 쓰리뱅크샷 
        GiantTurn,                  // 대회전 (4쿠션이상중복)
        Reverse,                    // 더블쿠션 (장,장,단)
        TraverseShot,               // 횡단샷  (장,장,장)

    }

    //---------------- 해설참조 까지  ----------------------//



    // 마무리
    public enum FinishMission
    {
        None,
        Cushion1,
        Cushion2,
        Cushion3,
        Bank1,
        Bank2,
        Bank3,
    }

    public enum OneMore
    {
        Enable,
        Req,
        Disable
    }

    class ReferArticle
    {
    }
}
