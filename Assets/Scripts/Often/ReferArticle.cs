namespace Assets.Scripts.Often
{

    // 서비스별 코드(뒤끝)
    public static class Constants
    {
     //   public static readonly string SceneNamePrefix = "";
        public static readonly string CommonCode_Id = "42867";      // 차트.공통코드
        public static readonly string SkillMatch_Id = "42868";    //90477";      // 차트.시합장
        public static readonly string Attendance_Id = "42869";      // 차트.출석부
    }

 

    public enum PlayType
    {
        PlayerAI = 0,       // AI (인간, ai)
        StepMission,        // 단계미션(인간, x)
        Single,            // 혼자(인간,인간)
        Online,             // 온라인(인간,통신)
        Replay              // 재생(x, x) .로직관여없음.
    }

    // 씬 명칭 
    public enum SceneNames
    {
        Arise,
        Come,
        Consist,
        Hall,
        Pavilion,
    }

    // 종료후 재경기 상태
    public enum OneMore
    {
        Enable,
        Req,
        Disable
    }

    // 보드 종류
    public enum CushionDir
    {
        None,
        T = 100,
        B,
        R,
        L
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



    // 마무리
    public enum FinishMission
    {
        None    = 0,
        Cushion3,                   // 3쿠션 1개
        Bank1,                      // 1뱅크 1개
        Bank2,                      // 2뱅크 1개
    }


    // 볼의 속도가 약해진 상태
    public enum BallMovingDigest
    {
        None,
        Start,              // 약함 돌입
        Check,              // 달성여부 확인
        TargetFind,         // 목표로 향함
        TargetPosFind,      // 목표로 향함
        TargetPosLook,      // 목표로 향함
        TargetBallFind,      // 목표로 향함
        TargetBallLook,      // 목표로 향함
        TargetPosFar,        // 목표를 지나침
        TargetBallFar,       // 적구를 지나침
        ForceOut,
        TargetLose,
        Achieve,            // 목표달성 
        Failure,            // 실패 
        Kiss,               // 상대의 이동으로 충돌
        End                 // 상황 종료 
    }


    // 볼이동중 변화한 상태
    public enum BallMovingStory
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
        TargetBallFar,      // 목표를 지나침
        ForceOut,
        AchieveNormal,      // 목표달성 
        AchieveKiss,        // 목표달성 
        AchieveRelax,       // 목표달성 안심.
        Failure,            // 실패 
        KissNormal,         // 상대의 이동으로 충돌
        KissWeaken,         // 상대의 이동으로 충돌
        End                 // 상황 종료 
    }

 


    public enum BallState
    {
        Non = 0,
        GetSync,
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


    //---------------- 해설참조 부터 ----------------------//

    public enum CueBallHitPart
    {
        None,
        Left,
        Right,
        Front,
    }

    public enum StateCheck
    {
        None,
        Check,
        End
    }


    // 비껴치기 단계  : 수구와 적구가 충돌시 충돌전과 위치가 같은 방향이면 준비
    public enum AsideHitStep
    {
        None,               // 없음
        Ready,              // 준비
        Appoint,            // 지정
    }

    // 수구향하고 있는 방향(좌,우)
    public enum CueBallMoveAreaLook
    {
        None,
        Left,
        Right
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

    // 보드 정사각형2개(좌,우)
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


    public enum ObjectPool
    {
        StuffCoin01,
        StuffItem01,
        StuffItem02,
        StuffItem03,
        StuffItem04,
        Effect01,
        Effect02,
        Effect03,
        Effect04,
        CoinLightUp,
        Cosmos,
        BallHit,
        GoldCoin,
    }


    public enum EffType
    {
        Effect01,
        Effect02,
        Effect03,
        Effect04,
    }

    // 사용안함 . 생존모드여부만 체크
    public enum ScoreStep
    {
        Egg,
        Finish,
        Survival
    }

    // 선수 마무리처리별 단계 
    public enum FinishStep
    {
        None,       // 없음
        Task,       // 과제
        Push,       // 추진
        Done        // 완료
    }

    public enum NoticeHitBall
    {
        None,
        Foul,       // 파울(감점)
        Finish,     // 마무리 
        Survival,   // 생존모드
    }

    class ReferArticle
    {
    }
}
