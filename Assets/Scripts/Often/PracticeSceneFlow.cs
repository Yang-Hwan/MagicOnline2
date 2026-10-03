using UnityEngine;
using UnityEngine.SceneManagement;
using Assets.Scripts.Sight.Surface.Arise;

namespace Assets.Scripts.Often
{
    public static class PracticeSceneFlow
    {
        public enum Mode { ThreeBall, FourBall, Replay, MatchReplay }
        public static Mode SelectedMode { get; private set; } = Mode.FourBall;
        public static MatchConfiguration Configuration { get; private set; } = MatchConfiguration.Practice(4);
        public static MatchStartSession OnlineSession { get; private set; }
        public static bool ReturnToPracticeMenu { get; private set; }
        static bool returnInProgress;
        public const string ScenePath = "Assets/Practice/Scenes/TableSet07.unity";

        public static void Enter(Mode mode = Mode.FourBall)
        {
            if (!Application.CanStreamedLevelBeLoaded(ScenePath))
            {
                Debug.LogError("Practice scene is missing from the build: " + ScenePath);
                return;
            }
            SelectedMode = mode;
            returnInProgress = false;
            OnlineSession = null;
            bool replay = mode == Mode.Replay || mode == Mode.MatchReplay;
            Configuration = MatchConfiguration.Practice(mode == Mode.ThreeBall ? 3 : 4, replay);
            ReturnToPracticeMenu = false;
            SceneManager.LoadScene(ScenePath);
        }

        public static void ConsumeMenuReturn() { ReturnToPracticeMenu = false; returnInProgress = false; }

        // Called by the session transport only after both configurations are accepted.
        // Existing matchmaking deliberately retains Pavilion until shot sync is connected.
        public static bool EnterOnline(MatchStartSession session)
        {
            if (session == null || session.Phase != MatchStartPhase.Loading ||
                !Application.CanStreamedLevelBeLoaded(ScenePath)) return false;
            OnlineSession = session;
            returnInProgress = false;
            Configuration = session.Configuration;
            SelectedMode = Configuration.BallCount == 3 ? Mode.ThreeBall : Mode.FourBall;
            ReturnToPracticeMenu = false;
            SceneManager.LoadScene(ScenePath);
            return true;
        }

        public static void ReturnToMenu()
        {
            if (returnInProgress) return;
            returnInProgress = true;
            if (OnlineSession != null)
            {
                var transport = Object.FindAnyObjectByType<Assets.Scripts.Exert.Network.PracticeMatchSetupTransport>();
                bool handled = transport && transport.Session != null;
                if (handled) transport.LeaveSession();
                OnlineSession.Abort("경기장을 나갔습니다.");
                OnlineSession = null;
                Configuration = MatchConfiguration.Practice(4);
                SelectedMode = Mode.FourBall;
                ReturnToPracticeMenu = false;
                if (!handled && Assets.Scripts.Exert.Network.NetworkManager.TryGetExistingNetwork(out var network)) network.LeaveMatch();
                if (BackendDuty.Instance != null) BackendDuty.Instance.ConsistPage(2);
                SceneManager.LoadScene("Consist");
                return;
            }
            ReturnToPracticeMenu = true;
            if (BackendDuty.Instance != null) BackendDuty.Instance.ConsistPage(1);
            SceneManager.LoadScene("Consist");
        }
    }
}
