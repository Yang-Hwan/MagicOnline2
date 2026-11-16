using System;
using Assets.Scripts.Exert.BackSys;
using UnityEngine;

namespace Assets.Scripts.Often
{
    public static class StageMissions
    {
        [Serializable] public sealed class Mission
        {
            public string source, order;
            public int balls, cue, first;
            public bool mirrorX, mirrorZ;
            public Vector2[] layout, route;
        }
        [Serializable] sealed class Catalog { public Mission[] missions; }
        static Catalog catalog;
        public const int Count = 100, Chances = 6;
        public static int Current { get; private set; } = 1;
        public static Mission Get(int stage)
        {
            if (catalog == null) catalog = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("StageMissions/catalog").text);
            return catalog.missions[stage-1];
        }
        static string Key(int stage) => "StageMissions.v1." + GameApiClient.UserId + "." + stage;
        // One saved integer keeps best stars (half-star units) and spent attempts together.
        public static int Best(int stage) => PlayerPrefs.GetInt(Key(stage)) / 10;
        public static int Spent(int stage) => PlayerPrefs.GetInt(Key(stage)) % 10;
        public static bool Open(int stage) => stage >= 1 && stage <= Count && (stage == 1 || Best(stage-1)>0);
        public static void Enter(int stage)
        {
            if (!Open(stage)) return;
            Current = stage;
            PracticeSceneFlow.Enter(PracticeSceneFlow.Mode.Stage);
        }
        static void Save(int best,int spent)
        {
            PlayerPrefs.SetInt(Key(Current),best*10+spent);
            PlayerPrefs.Save();
        }
        public static int BeginAttempt()
        {
            int left=Chances-Spent(Current);
            if (left>0) Save(Best(Current),Spent(Current)+1);
            return left;
        }
        public static void Win(int halfStars) => Save(Mathf.Max(Best(Current),halfStars),0);
        public static void Refill() => Save(Best(Current),0);
        public static string Stars(int halfStars) => (halfStars/2f).ToString("0.#");
    }
}
