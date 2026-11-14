using System.Collections;
using Assets.Scripts.Often;
using Assets.Scripts.Exert.Network;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Sight.Surface.Pavilion
{
    public sealed class MatchAvatarReactions : MonoBehaviour
    {
        static readonly Sprite[] neutralBySeat = new Sprite[2];
        static readonly Sprite[] winnerBySeat = new Sprite[2], loserBySeat = new Sprite[2];
        static string Folder(int seat) => "Gameplay/" + (seat == 0 ? "BandanaAvatar/" : "SilverAvatar/");

        public static Sprite NeutralForSeat(int seat)
        {
            if (!neutralBySeat[seat]) neutralBySeat[seat] = Resources.Load<Sprite>(Folder(seat) + "Neutral");
            return neutralBySeat[seat];
        }

        public static Sprite ResultForSeat(int seat, bool won, bool lost)
        {
            if (won == lost) return NeutralForSeat(seat);
            var faces = won ? winnerBySeat : loserBySeat;
            if (!faces[seat]) faces[seat] = Resources.Load<Sprite>(Folder(seat) + (won ? "Point" : "MissB"));
            return faces[seat] ? faces[seat] : NeutralForSeat(seat);
        }

        sealed class Faces
        {
            public readonly Sprite neutral;
            readonly Sprite[] point, miss, foul;

            public Faces(int seat)
            {
                string path = Folder(seat);
                neutral = NeutralForSeat(seat);
                Sprite[] Pair(string name) => new[] { Resources.Load<Sprite>(path + name), Resources.Load<Sprite>(path + name + "B") };
                point = Pair("Point");
                miss = Pair("Miss");
                foul = Pair("Foul");
            }

            public Sprite Reaction(MatchShotOutcome result, int variant) =>
                (result == MatchShotOutcome.Point ? point : result == MatchShotOutcome.Foul ? foul : miss)[variant];
        }

        Image self, other, shooter;
        EquipmentPortrait selfEquipment, otherEquipment;
        int selfReactionVersion, otherReactionVersion;
        int Advance(Image portrait) => portrait == self ? ++selfReactionVersion : ++otherReactionVersion;
        static readonly string[] Scored = { "감사", "미안", "미소", "환희" };
        static readonly string[] NotScored = { "짜증", "우울", "좌절", "분노" };
        Faces selfFaces, otherFaces;
        int localSeat = -1;
        string matchId;
        int shotId, shooterSeat;
        Coroutine reset;

        void Awake()
        {
            self = transform.Find("PlayerBoxs/PlayerSelf/Avatar").GetComponent<Image>();
            other = transform.Find("PlayerBoxs/PlayerOther/Avatar").GetComponent<Image>();
            self.preserveAspect = other.preserveAspect = true;
            selfEquipment = self.gameObject.AddComponent<EquipmentPortrait>();
            otherEquipment = other.gameObject.AddComponent<EquipmentPortrait>();
        }

        public void Configure(string id, int seat)
        {
            if (matchId == id && localSeat == seat) return;
            var bandana = new Faces(0);
            var silver = new Faces(1);
            selfFaces = seat == 0 ? bandana : silver;
            otherFaces = seat == 0 ? silver : bandana;
            matchId = id;
            localSeat = seat;
            selfEquipment.SetProfile(MatchEquipmentRuntime.ForSeat(seat));
            otherEquipment.SetProfile(MatchEquipmentRuntime.ForSeat(1 - seat));
            Clear();
        }

        public void BeginShot(int serial, int seat)
        {
            Clear();
            shotId = serial;
            shooterSeat = seat;
            shooter = seat == localSeat ? self : other;
        }

        // Stable across Unity processes and independent of their random generators.
        int Variant(MatchShotOutcome outcome)
        {
            unchecked
            {
                uint hash = 2166136261u;
                foreach (char c in matchId) hash = (hash ^ c) * 16777619u;
                hash = (hash ^ (uint)shotId) * 16777619u;
                hash = (hash ^ (uint)shooterSeat) * 16777619u;
                hash = (hash ^ (uint)outcome) * 16777619u;
                hash ^= hash >> 16;
                hash *= 0x85ebca6bu;
                return (int)((hash ^ (hash >> 13)) & 3u);
            }
        }

        public void Resolve(MatchShotOutcome outcome)
        {
            if (!shooter || !isActiveAndEnabled) return;
            var faces = shooter == self ? selfFaces : otherFaces;
            int variant = Variant(outcome);
            string emotion = (outcome == MatchShotOutcome.Point ? Scored : NotScored)[variant];
            (shooter == self ? selfEquipment : otherEquipment).Show(emotion, faces.Reaction(outcome, variant % 2));
            var portrait = shooter;
            shooter = null; // Ignore repeat notifications for this shot.
            reset = StartCoroutine(Restore(portrait, faces.neutral, Advance(portrait)));
        }

        IEnumerator Restore(Image portrait, Sprite neutral, int version)
        {
            yield return new WaitForSecondsRealtime(1.3f);
            if (version != (portrait == self ? selfReactionVersion : otherReactionVersion)) yield break;
            (portrait == self ? selfEquipment : otherEquipment).Show("기본", neutral);
            reset = null;
        }

        void OnDisable() => Clear();

        void Clear()
        {
            selfReactionVersion++; otherReactionVersion++;
            if (reset != null) StopCoroutine(reset);
            reset = null;
            shooter = null;
            if (self && selfFaces != null) selfEquipment.Show("기본", selfFaces.neutral);
            if (other && otherFaces != null) otherEquipment.Show("기본", otherFaces.neutral);
        }
    }
}
