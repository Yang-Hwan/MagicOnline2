using System;
using UnityEngine;

namespace Assets.Scripts.Often
{
    public static class PracticeMatchTestRoute
    {
        public const string Argument = "-practice-match-test";
        public const string Protocol = "practice-match-test-10";
        public static bool TargetWin
        {
            get
            {
                if (!Enabled) return false;
#if UNITY_EDITOR
                return UnityEditor.EditorPrefs.GetBool("MagicOnline2.PracticeMatchTargetWin", false);
#else
                return Array.Exists(Environment.GetCommandLineArgs(), a => a == "-practice-match-target-win");
#endif
            }
        }
        public static bool QuickMatch
        {
            get
            {
                if (!Enabled) return false;
#if UNITY_EDITOR
                return UnityEditor.EditorPrefs.GetBool("MagicOnline2.PracticeMatchQuick", false);
#else
                return Array.Exists(Environment.GetCommandLineArgs(), a => a == "-practice-match-quick");
#endif
            }
        }
        public static Assets.Scripts.Prack.BackSys.ChartData.SkillMatchData MatchHall(
            Assets.Scripts.Prack.BackSys.ChartData.SkillMatchData source)
        {
            if (source == null || !Enabled) return source;
            bool twoPointThreeBall = source.MatchBall == 3;
            if (!twoPointThreeBall && !QuickMatch && !TargetWin) return source;
            var copy = JsonUtility.FromJson<Assets.Scripts.Prack.BackSys.ChartData.SkillMatchData>(JsonUtility.ToJson(source));
            if (twoPointThreeBall) copy.TargetHit = 2;
            if (QuickMatch) copy.MatchTotMin = 1;
            if (TargetWin) copy.TargetHit = 1;
            return copy;
        }
        public static bool Enabled
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.EditorPrefs.GetBool("MagicOnline2.PracticeMatchTest", false);
#else
                return Debug.isDebugBuild && Array.Exists(Environment.GetCommandLineArgs(), a => a == Argument);
#endif
            }
        }
        public static string RoomProtocol => Enabled ? Protocol + (QuickMatch ? "-quick" : "") +
            (TargetWin ? "-target1" : "") : MatchmakingCompatibility.Protocol;
        public static string Version(string appVersion) => Enabled ? appVersion + "." + RoomProtocol : MatchmakingCompatibility.GameVersion(appVersion);
        public static Photon.Realtime.AppSettings ConnectionSettings(Photon.Realtime.AppSettings source, string appVersion)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var settings = source.CopyTo(new Photon.Realtime.AppSettings());
            settings.AppVersion = Version(appVersion);
            return settings;
        }
        public static bool AcceptsProtocol(object value) => value is string protocol && protocol == RoomProtocol;
    }
}
