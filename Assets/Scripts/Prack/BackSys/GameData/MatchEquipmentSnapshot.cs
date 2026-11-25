#nullable disable
using System;

namespace Assets.Scripts.Prack.BackSys.GameData
{
    [Serializable] public sealed class MatchPlayerInfo
    {
        public string UserId = "", Nickname = "", CueName = "", CueDescription = "", CueThumbnail = "";
        public int Level, LevelProgress, TodayStreak;
        public long TotalPrize;
        public PlayerRecord Total = new PlayerRecord(), FourBall = new PlayerRecord(), ThreeBall = new PlayerRecord();
        public MatchEquipmentProfile Equipment = new MatchEquipmentProfile();
    }
    [Serializable] public sealed class PlayerRecord
    {
        public long Wins, Losses, Draws;
        public double WinRate => Wins + Losses == 0 ? 0 : 100.0 * Wins / (Wins + Losses);
    }
    [Serializable] public sealed class MatchEquipmentSnapshot
    {
        public int Version = 1;
        public string MatchId = "";
        public MatchEquipmentProfile[] Players = Array.Empty<MatchEquipmentProfile>();
        public string MessageAt(int seat, int index) => seat >= 0 && seat < Players.Length && index >= 0 && index < Players[seat].Messages.Length ? Players[seat].Messages[index] : null;
    }
    [Serializable] public sealed class MatchEquipmentProfile
    {
        public string UserId = "";
        public long CueId, CharacterId;
        public string CueVisual = "";
        public int Power, FollowThrough, Tip, ShotSeconds;
        public int Guide = 100;
        public float GuideScale => (Ability != null && Ability.HasGuide() ? Ability.Percent(Guide) : Clamp(Guide,0,100))/100f;
        public MatchEquipmentEmotion[] Emotions = Array.Empty<MatchEquipmentEmotion>();
        public string[] Messages = Array.Empty<string>();
        public MatchMessagePack[] MessagePacks = Array.Empty<MatchMessagePack>();
        public CueAbilitySettings Ability;
        public long CueProgress, CueSteps, CuePending;
        public int CueTimeMin = 20, CueTimeMax = 40;
        public float FollowScale => Ability != null ? Ability.Percent(FollowThrough)/100f : 1+Clamp(FollowThrough,-40,20)/100f;
        public float PowerScale => Ability != null ? Ability.Percent(Power)/100f : 1 + Clamp(Power, -40, 20) / 100f;
        public float TipScale => Ability != null ? Ability.Percent(Tip)/100f : 1 + Clamp(Tip, -10, 10) / 100f;
        public float EffectiveFollow(float value) => Math.Max(0, Math.Min(1, value * FollowScale));
        public float TurnSeconds(float basic) => Ability != null
            ? (float)Math.Round(CueTimeMin+(CueTimeMax-CueTimeMin)*Ability.Percent(ShotSeconds)/100.0,MidpointRounding.AwayFromZero)
            : Math.Max(1, basic + Clamp(ShotSeconds, -5, 5));
        static int Clamp(int n, int min, int max) => Math.Max(min, Math.Min(max, n));
    }
    [Serializable] public sealed class CueAbilitySettings
    {
        public string Grade = "A";
        public int[] Initial = {100,100,100,50,100};
        public int[] Maximum = {100,100,100,100,100};
        public int Reach = 100;
        public int[] BowlMaximum = {20,10,30,20,40};
        public bool BowlAuto;
        public bool Valid()
        {
            if((Grade!="A" && Grade!="B" && Grade!="C") || Reach<1 || Reach>1000000 || Initial==null || Maximum==null || (Initial.Length!=4 && Initial.Length!=5) || Maximum.Length!=Initial.Length)return false;
            for(int i=0;i<Initial.Length;i++)if(Initial[i]<0 || Initial[i]>Maximum[i] || Maximum[i]>100)return false;
            return BowlMaximum!=null && BowlMaximum.Length==5 && Array.TrueForAll(BowlMaximum,n=>n>=1&&n<=10000);
        }
        public bool HasGuide() => Initial != null && Initial.Length == 5;
        public static int[] WithGuide(int[] values)
        {
            var result = new int[]{100,100,100,100,100};
            if(values != null) Array.Copy(values,result,Math.Min(values.Length,5));
            return result;
        }
        public int GuidePercent(long steps) => HasGuide() ? Percent(LevelsFromSteps(steps)[4]) : 100;
        public int Percent(int level) => (int)Math.Round(Math.Max(0,Math.Min(100,level))*(Grade=="B"?.9:Grade=="C"?.8:1),MidpointRounding.AwayFromZero);
        public int[] Levels(long shots)
        {
            return LevelsFromSteps(Math.Max(0,shots)/Math.Max(1,Reach));
        }
        public int[] LevelsFromSteps(long earnedSteps)
        {
            var result=new int[Initial.Length];long steps=Math.Min(100,Math.Max(0,earnedSteps));
            for(int i=0;i<result.Length;i++)result[i]=(int)Math.Min(Maximum[i],Initial[i]+steps);
            return result;
        }
    }
    [Serializable] public sealed class MatchEquipmentEmotion { public string Code = "", Asset = ""; }
    [Serializable] public sealed class CueBowlState
    {
        public int[] Maximum={20,10,30,20,40}, Filled=new int[5];
        public bool[] Drunk=new bool[5], Completed=new bool[5];
        public bool Auto;
        public long Cycle;
        public bool Ready(int index)=>index>=0&&index<5&&!Completed[index]&&Filled[index]>=Maximum[index]&&!Drunk[index];
        // Each point visits exactly one non-full bowl. Full rounds discard further points.
        public bool Add(int randomChoice)
        {
            int available=0;for(int i=0;i<5;i++)if(!Completed[i]&&!Drunk[i]&&Filled[i]<Maximum[i])available++;
            if(available==0)return false;
            int pick=randomChoice%available;
            for(int i=0;i<5;i++)if(!Completed[i]&&!Drunk[i]&&Filled[i]<Maximum[i]&&pick--==0){Filled[i]++;if(Auto&&Filled[i]==Maximum[i]){Drunk[i]=true;Filled[i]=0;}return true;}
            return false;
        }
        public bool Complete {
            get{bool eligible=false;for(int i=0;i<5;i++)if(!Completed[i]){eligible=true;if(!Drunk[i])return false;}return eligible;}
        }
        public void SetCompleted(CueAbilitySettings ability,long steps)
        {
            var levels=CueAbilitySettings.WithGuide(ability.LevelsFromSteps(steps));var maximum=CueAbilitySettings.WithGuide(ability.Maximum);
            for(int i=0;i<5;i++){Completed[i]=levels[i]>=maximum[i];if(Completed[i]){Filled[i]=0;Drunk[i]=false;}else if(Drunk[i])Filled[i]=0;}
        }
        public void Reset(int[] maximum){Maximum=(int[])maximum.Clone();Filled=new int[5];Drunk=new bool[5];Cycle++;}
    }
    [Serializable] public sealed class MatchMessagePack
    {
        public string Name = "";
        public int[] Indices = Array.Empty<int>();
    }
}
