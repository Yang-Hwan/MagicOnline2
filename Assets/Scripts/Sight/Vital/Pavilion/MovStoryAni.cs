using DG.Tweening;
using System.Threading;
using UnityEngine;

namespace Assets.Scripts.Sight.Vital.Pavilion
{


    public enum StoryAniPos
    {
        Full,
        Follow,
        FourSplit,
        TwoHorizontal,
        TwoVertical,
    }


    public enum MovStoryBias
    {
        None,           // 없음
        Boost,          // 격려
        Miss,           // 실패
        Kiss,           // 키스
        BallFar,        // 적구 지나감
        KissWell,       // 키스 좋아
        KissHate,       // 키스 서운
        Achieve,        // 성공 
        Foul,           // 파울 
    }

    public enum Table_Rect_Kind
    {
        Fixity,
        Follow,
        Rect2H,
        Rect2V,
        Rect4
    }

    public enum Table_Rect_4
    {
        UpLeft,
        UpRight,
        DownLeft,
        DownRight
    }

    public enum Table_Rect_2H
    {
        Left,
        Right,
    }

    public enum Table_Rect_2V
    {
        Up,
        Down,
    }


    public enum StoryIdentity
    {
        HeHe,
        Ehyu,
        DabDab,
        KissGamsa,
        ANumuhe,
        Ggeung,
        Jjagjjag,
        HeartNiceShot,
        Buglbugl,
        Kissgamsa2,
        Jogmman,
        Akissuu,
        CrowFly,
        GlassBroken,
        DarkCloud,
        Kiss,
        DeadLeaves,
        LightEmit,
        DogBone,
        SnowStorm,
        BombDebris,
        SpotLight,
        MusicNote,
        PongHammer,
        BellyLaugh,
        TearCry,
        Foul,
    }

    public abstract class MovStoryAni : MonoBehaviour
    {
        public Sequence mySequence;
        public bool IsActive { get; private set; }

        public StoryIdentity storyIdentity;     // 독창
        public MovStoryBias movStoryBias;       // 성향
        public StoryAniPos storyAniPos;       // 위치 
        protected Vector3 _pos;

        public abstract void AniPlay(Vector3 pos, CancellationToken _cancellationToken);

        public virtual void Awake()
        {
            _pos = Vector3.zero;
            SetupSequence();
        }

        public virtual void SetupSequence()
        {
            mySequence = DOTween.Sequence()
                .SetAutoKill(false)
                .Pause();
            Debug.Log("MovStoryAni virtual void SetupSequence()");
        }

        public virtual void StartSequence(Vector3 pos)
        {
            IsActive = true;
            _pos = pos;
            mySequence.Kill();
            SetupSequence(); // 시퀀스 재설정

            if (mySequence != null && !mySequence.IsPlaying())
            {
                mySequence.Restart();
            }
        }

        public virtual void CancelSequence()
        {
            if (mySequence != null && mySequence.IsActive())
            {
                IsActive = false;
                _pos = Vector3.zero;
                mySequence.Kill();
                SetupSequence(); // 시퀀스 재설정
            }
        }


    }
}
