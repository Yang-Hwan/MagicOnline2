#nullable disable
using System;

namespace Assets.Scripts.Prack.BackSys.GameData
{
    [Serializable] public sealed class MatchPlayerInfo
    {
        public string UserId = "", Nickname = "", CueName = "", CueDescription = "", CueThumbnail = "";
        public int Level, TodayStreak;
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
        public int[] Initial = {100,100,100,50};
        public int[] Maximum = {100,100,100,100};
        public int Reach = 100;
        public bool Valid()
        {
            if((Grade!="A" && Grade!="B" && Grade!="C") || Reach<1 || Reach>1000000 || Initial==null || Maximum==null || Initial.Length!=4 || Maximum.Length!=4)return false;
            for(int i=0;i<4;i++)if(Initial[i]<0 || Initial[i]>Maximum[i] || Maximum[i]>100)return false;
            return true;
        }
        public int Percent(int level) => (int)Math.Round(Math.Max(0,Math.Min(100,level))*(Grade=="B"?.9:Grade=="C"?.8:1),MidpointRounding.AwayFromZero);
        public int[] Levels(long shots)
        {
            return LevelsFromSteps(Math.Max(0,shots)/Math.Max(1,Reach));
        }
        public int[] LevelsFromSteps(long earnedSteps)
        {
            var result=new int[4];long steps=Math.Min(100,Math.Max(0,earnedSteps));
            for(int i=0;i<4;i++)result[i]=(int)Math.Min(Maximum[i],Initial[i]+steps);
            return result;
        }
    }
    [Serializable] public sealed class MatchEquipmentEmotion { public string Code = "", Asset = ""; }
    [Serializable] public sealed class MatchMessagePack
    {
        public string Name = "";
        public int[] Indices = Array.Empty<int>();
    }
}
