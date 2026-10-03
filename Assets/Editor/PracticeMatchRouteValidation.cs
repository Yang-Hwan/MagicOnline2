using System;
using Assets.Scripts.Often;
using Photon.Pun;
using UnityEditor;

public static class PracticeMatchRouteValidation
{
    public static void Run()
    {
        if (PhotonNetwork.IsConnected) throw new InvalidOperationException("Route validation requires a disconnected client.");
        bool previous = PracticeMatchTestRoute.Enabled;
        bool previousQuick = EditorPrefs.GetBool("MagicOnline2.PracticeMatchQuick", false);
        bool previousTarget = EditorPrefs.GetBool("MagicOnline2.PracticeMatchTargetWin", false);
        try
        {
            EditorPrefs.SetBool("MagicOnline2.PracticeMatchQuick", false);
            EditorPrefs.SetBool("MagicOnline2.PracticeMatchTargetWin", false);
            EditorPrefs.SetBool("MagicOnline2.PracticeMatchTest", false);
            var source = new Photon.Realtime.AppSettings { AppVersion = "asset-value", FixedRegion = "kr" };
            string normal = PracticeMatchTestRoute.Version("1.0");
            Require(PracticeMatchTestRoute.ConnectionSettings(source, "1.0").AppVersion == normal, "normal connection version missing");
            Require(PracticeMatchTestRoute.AcceptsProtocol(MatchmakingCompatibility.Protocol), "normal route rejected normal rooms");
            Require(!PracticeMatchTestRoute.AcceptsProtocol(PracticeMatchTestRoute.Protocol), "normal route accepted test rooms");
            EditorPrefs.SetBool("MagicOnline2.PracticeMatchTest", true);
            ValidateCancelledCallbacks();
            ValidateCancellationOrdering(3);
            ValidateCancellationOrdering(4);
            var connection = PracticeMatchTestRoute.ConnectionSettings(source, "1.0");
            Require(connection.AppVersion == PracticeMatchTestRoute.Version("1.0") && connection.FixedRegion == "kr" &&
                source.AppVersion == "asset-value" && !ReferenceEquals(source, connection), "connection settings lost version/region or modified shared asset");
            Require(PracticeMatchTestRoute.Version("1.0") != normal, "test client shared normal matchmaking version");
            Require(PracticeMatchTestRoute.AcceptsProtocol(PracticeMatchTestRoute.Protocol), "test route rejected test rooms");
            Require(!PracticeMatchTestRoute.AcceptsProtocol(MatchmakingCompatibility.Protocol) &&
                !PracticeMatchTestRoute.AcceptsProtocol(null), "test route accepted old/unknown protocol");
            var hall = new Assets.Scripts.Prack.BackSys.ChartData.SkillMatchData {
                HallIdx = 1, MatchBall = 3, MatchCushion = 3, TargetHit = 10, MatchTotMin = 20 };
            var ordinaryHall = PracticeMatchTestRoute.MatchHall(hall);
            Require(ordinaryHall.TargetHit == 2 && hall.TargetHit == 10 && ordinaryHall.MatchTotMin == 20 &&
                ordinaryHall.MatchCushion == 3 && !ReferenceEquals(hall, ordinaryHall), "3-ball test target or source isolation failed");
            var fourBallHall = new Assets.Scripts.Prack.BackSys.ChartData.SkillMatchData {
                HallIdx = 2, MatchBall = 4, MatchCushion = 0, TargetHit = 10, MatchTotMin = 20 };
            Require(ReferenceEquals(fourBallHall, PracticeMatchTestRoute.MatchHall(fourBallHall)), "ordinary 4-ball test changed hall");
            EditorPrefs.SetBool("MagicOnline2.PracticeMatchQuick", true);
            var quickHall = PracticeMatchTestRoute.MatchHall(hall);
            Require(quickHall.MatchTotMin == 1 && hall.MatchTotMin == 20 && quickHall.TargetHit == 2 &&
                quickHall.MatchCushion == 3 && !ReferenceEquals(hall, quickHall), "quick match modified source or scoring rules");
            Require(PracticeMatchTestRoute.RoomProtocol == PracticeMatchTestRoute.Protocol + "-quick" &&
                !PracticeMatchTestRoute.AcceptsProtocol(PracticeMatchTestRoute.Protocol) &&
                PracticeMatchTestRoute.Version("1.0") != connection.AppVersion, "quick match isolation failed");
            Require(MatchConfiguration.Online(quickHall, "A", "B", 0, 0, 20).MatchSeconds == 60,
                "quick match did not use normal 60-second deadline");
            EditorPrefs.SetBool("MagicOnline2.PracticeMatchTargetWin", true);
            Require(PracticeMatchTestRoute.RoomProtocol == PracticeMatchTestRoute.Protocol + "-quick-target1",
                "combined options share another protocol");
            EditorPrefs.SetBool("MagicOnline2.PracticeMatchQuick", false);
            var targetHall = PracticeMatchTestRoute.MatchHall(hall);
            Require(targetHall.TargetHit == 1 && hall.TargetHit == 10 && targetHall.MatchTotMin == 20 &&
                targetHall.MatchCushion == 3 && !ReferenceEquals(hall, targetHall), "target test mutated original or rules");
            Require(PracticeMatchTestRoute.RoomProtocol == PracticeMatchTestRoute.Protocol + "-target1" &&
                !PracticeMatchTestRoute.AcceptsProtocol(PracticeMatchTestRoute.Protocol) &&
                !PracticeMatchTestRoute.AcceptsProtocol(PracticeMatchTestRoute.Protocol + "-quick") &&
                MatchConfiguration.Online(targetHall, "A", "B", 0, 0, 20).TargetScore == 1,
                "target score or matchmaking isolation failed");
            EditorPrefs.SetBool("MagicOnline2.PracticeMatchTest", false);
            Require(!PracticeMatchTestRoute.QuickMatch && !PracticeMatchTestRoute.TargetWin && ReferenceEquals(hall, PracticeMatchTestRoute.MatchHall(hall)),
                "quick option affected normal route");
        }
        finally
        {
            EditorPrefs.SetBool("MagicOnline2.PracticeMatchTest", previous);
            EditorPrefs.SetBool("MagicOnline2.PracticeMatchQuick", previousQuick);
            EditorPrefs.SetBool("MagicOnline2.PracticeMatchTargetWin", previousTarget);
        }
    }
    static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException("Practice test route: " + message); }

    static void ValidateCancellationOrdering(int count)
    {
        var hall = new Assets.Scripts.Prack.BackSys.ChartData.SkillMatchData {
            HallIdx = 1, MatchBall = count, MatchCushion = count == 3 ? 3 : 0, TargetHit = 7, MatchTotMin = 20 };
        // Cancel before agreement, while loading, after ready, or immediately after start.
        for (int stage = 0; stage < 4; stage++)
        {
            var session = new MatchStartSession("cancel-" + stage, "key",
                MatchConfiguration.Online(hall, "A", "B", 0, 1, 20), 11, 22, 0);
            if (stage >= 1)
            {
                session.AcceptConfiguration(11, session.MatchId, "key");
                session.AcceptConfiguration(22, session.MatchId, "key");
            }
            if (stage >= 2)
            {
                session.MarkSceneReady(11, session.MatchId, "key");
                session.MarkSceneReady(22, session.MatchId, "key");
            }
            if (stage == 3) Require(session.CommitStart(11, session.MatchId), "race setup never started");
            session.Abort("user cancelled");
            int changes = 0, readyCallbacks = 0;
            session.Changed += () => changes++;
            session.LocalSceneReady += (_, _, _) => readyCallbacks++;
            Require(!session.AcceptConfiguration(11, session.MatchId, "key") &&
                !session.MarkSceneReady(22, session.MatchId, "key") &&
                !session.CommitStart(11, session.MatchId) && !PracticeSceneFlow.EnterOnline(session),
                "late agreement/ready/start revived cancelled session at stage " + stage);
            session.ReportLocalSceneReady();
            session.Tick(1000);
            Require(session.Phase == MatchStartPhase.Aborted && session.AbortReason == "user cancelled" &&
                changes == 0 && readyCallbacks == 0, "cancelled session emitted delayed transitions");
        }
    }

    static void ValidateCancelledCallbacks()
    {
        var owner = new UnityEngine.GameObject("CancelledMatchValidation");
        owner.SetActive(false); // Do not initialize a real PhotonView or connect.
        int previousHall = Assets.Scripts.Exert.Network.NetworkManager.hallIdx;
        try
        {
            var transport = owner.AddComponent<Assets.Scripts.Exert.Network.PracticeMatchSetupTransport>();
            var network = owner.AddComponent<Assets.Scripts.Exert.Network.PunNetwork>();
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var leaving = transport.GetType().GetField("leaving", flags);
            var cancelled = network.GetType().GetField("matchRequestCancelled", flags);
            leaving.SetValue(transport, true);
            cancelled.SetValue(network, true);
            int callbacks = 0;
            network.OnNetwork += _ => callbacks++;
            // Delayed data must be ignored before deserialization or UI notification.
            network.OnOpponentReadToPlay(null);
            network.OnMasterInfoToGuest(null);
            network.OnOpponentStartToPlay(0);
            network.OnPlayerEnteredRoom(null);
            network.WaitCountdowned();
            Require(callbacks == 0 && transport.Session == null, "cancelled callbacks restarted preparation");
            network.JoinRandomRoom(previousHall); // Disconnected: retry must not rearm the transport.
            Require((bool)leaving.GetValue(transport) && (bool)cancelled.GetValue(network) &&
                transport.Session == null && callbacks == 1, "invalid retry cleared cancellation guard");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
            Assets.Scripts.Exert.Network.NetworkManager.hallIdx = previousHall;
        }
    }
}
