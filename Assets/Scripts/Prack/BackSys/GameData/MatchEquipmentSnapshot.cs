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
        public float PowerScale => 1 + Clamp(Power, -40, 20) / 100f;
        public float TipScale => 1 + Clamp(Tip, -10, 10) / 100f;
        public float EffectiveFollow(float value) => Math.Max(0, Math.Min(1, value * (1 + Clamp(FollowThrough, -40, 20) / 100f)));
        public float TurnSeconds(float basic) => Math.Max(1, basic + Clamp(ShotSeconds, -5, 5));
        static int Clamp(int n, int min, int max) => Math.Max(min, Math.Min(max, n));
    }
    [Serializable] public sealed class MatchEquipmentEmotion { public string Code = "", Asset = ""; }
    [Serializable] public sealed class MatchMessagePack
    {
        public string Name = "";
        public int[] Indices = Array.Empty<int>();
    }
}
