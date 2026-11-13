#nullable disable
using System;

namespace Assets.Scripts.Prack.BackSys.GameData
{
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
        public float PowerScale => 1 + Clamp(Power, -40, 20) / 100f;
        public float TipScale => 1 + Clamp(Tip, -10, 10) / 100f;
        public float EffectiveFollow(float value) => Math.Max(0, Math.Min(1, value * (1 + Clamp(FollowThrough, -40, 20) / 100f)));
        public float TurnSeconds(float basic) => Math.Max(1, basic + Clamp(ShotSeconds, -5, 5));
        static int Clamp(int n, int min, int max) => Math.Max(min, Math.Min(max, n));
    }
    [Serializable] public sealed class MatchEquipmentEmotion { public string Code = "", Asset = ""; }
}
