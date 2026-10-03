using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Test.BallReactAni
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
        Boost,          // 격려
        Miss,           // 유감
        Kiss,           // 키스
        BallFar,        // 적구 지나감
        KissWell,       // 키스좋아
        KissHate,       // 키스서운
        Achieve,        // 성공 
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
        EHyu,
        DabDab,
        KissGamsa,
        ANumuhe,
        GgEung,
        JjagJjag,
        NiceShot,
        BugleBugle,
        KissGamsa2,
        JogeumManThe,
        AKissUU,
        CrowFly,
        GrassBroken,
        DarkCloud,
    }

    public abstract class BallMovStoryAni : MonoBehaviour
    {
        public StoryIdentity storyIdentity;     // 독창
        public MovStoryBias movStoryBias;       // 성향
        public StoryAniPos storyAniPos;       // 위치 



        public virtual void Awake()
        {
            
        }

        public virtual void AniPlay(Vector3 pos)
        {
            Debug.Log("00" + nameof(AniPlay));
            //AniStart().Forget();
        }

    }



 
}
