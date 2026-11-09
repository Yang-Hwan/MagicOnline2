using System.Collections;
using Assets.Scripts.Often;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Sight.Surface.Pavilion
{
    public sealed class MatchAvatarReactions : MonoBehaviour
    {
        static readonly Sprite[] neutralBySeat = new Sprite[2];
        static string Folder(int seat) => "Gameplay/" + (seat == 0 ? "BandanaAvatar/" : "SilverAvatar/");

        public static Sprite NeutralForSeat(int seat)
        {
            if (!neutralBySeat[seat]) neutralBySeat[seat] = Resources.Load<Sprite>(Folder(seat) + "Neutral");
            return neutralBySeat[seat];
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
                return (int)((hash ^ (hash >> 13)) & 1u);
            }
        }

        public void Resolve(MatchShotOutcome outcome)
        {
            if (!shooter || !isActiveAndEnabled) return;
            var faces = shooter == self ? selfFaces : otherFaces;
            shooter.overrideSprite = faces.Reaction(outcome, Variant(outcome));
            var portrait = shooter;
            shooter = null; // Ignore repeat notifications for this shot.
            reset = StartCoroutine(Restore(portrait, faces.neutral));
        }

        IEnumerator Restore(Image portrait, Sprite neutral)
        {
            yield return new WaitForSecondsRealtime(1.3f);
            portrait.overrideSprite = neutral;
            reset = null;
        }

        void OnDisable() => Clear();

        void Clear()
        {
            if (reset != null) StopCoroutine(reset);
            reset = null;
            shooter = null;
            if (self && selfFaces != null) self.overrideSprite = selfFaces.neutral;
            if (other && otherFaces != null) other.overrideSprite = otherFaces.neutral;
        }
    }
}
