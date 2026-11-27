using UnityEngine;
using UnityEngine.SceneManagement;
using Assets.Scripts.Sight.Surface.Arise;

namespace Assets.Scripts.Often
{
    public static class PracticeSceneFlow
    {
        public enum Mode { ThreeBall, FourBall, Replay, MatchReplay, Stage }
        public static Mode SelectedMode { get; private set; } = Mode.FourBall;
        public static MatchConfiguration Configuration { get; private set; } = MatchConfiguration.Practice(4);
        public static MatchStartSession OnlineSession { get; private set; }
        public static bool ReturnToPracticeMenu { get; private set; }
        static bool returnInProgress;
        static int entryRequest;
        public const string ScenePath = "Assets/Practice/Scenes/TableSet07.unity";

        public static async void Enter(Mode mode = Mode.FourBall)
        {
            int request=++entryRequest;string owner=Assets.Scripts.Exert.BackSys.GameApiClient.UserId;
            if (!Application.CanStreamedLevelBeLoaded(ScenePath))
            {
                Debug.LogError("Practice scene is missing from the build: " + ScenePath);
                return;
            }
            if(Application.isPlaying && (mode==Mode.ThreeBall || mode==Mode.FourBall) && !string.IsNullOrEmpty(Assets.Scripts.Exert.BackSys.GameApiClient.UserId))
            {
                try{await Assets.Scripts.Exert.Network.MatchEquipmentRuntime.RefreshLocalAsync();}
                catch(System.Exception e){Debug.LogWarning("큐대 장비 정보를 불러오지 못했습니다: "+e.Message);return;}
            }
            if(request!=entryRequest || owner!=Assets.Scripts.Exert.BackSys.GameApiClient.UserId)return;
            SelectedMode = mode;
            returnInProgress = false;
            OnlineSession = null;
            bool replay = mode == Mode.Replay || mode == Mode.MatchReplay;
            int balls = mode == Mode.Stage ? StageMissions.Get(StageMissions.Current).balls : mode == Mode.ThreeBall ? 3 : 4;
            Configuration = !replay && mode != Mode.Stage ? MatchConfiguration.VersusComputer(balls) : MatchConfiguration.Practice(balls, replay);
            ReturnToPracticeMenu = false;
            SceneManager.LoadScene(ScenePath);
        }

        internal static void ConfigureWaitingPractice(int balls)
        {
            entryRequest++;
            OnlineSession = null;
            returnInProgress = false;
            ReturnToPracticeMenu = false;
            SelectedMode = balls == 3 ? Mode.ThreeBall : Mode.FourBall;
            Configuration = MatchConfiguration.Practice(balls);
        }
        internal static void EndWaitingPractice()
        {
            returnInProgress = false;
            ReturnToPracticeMenu = false;
            OnlineSession = null;
            Configuration = MatchConfiguration.Practice(4);
            SelectedMode = Mode.FourBall;
        }

        public static void ConsumeMenuReturn() { ReturnToPracticeMenu = false; returnInProgress = false; }

        // Called by the session transport only after both configurations are accepted.
        // Existing matchmaking deliberately retains Pavilion until shot sync is connected.
        public static bool EnterOnline(MatchStartSession session)
        {
            if (session == null || session.Phase != MatchStartPhase.Loading ||
                !Application.CanStreamedLevelBeLoaded(ScenePath)) return false;
            entryRequest++;
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
            if (WaitingRoomPractice.ReturnToLobby()) return;
            entryRequest++;
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
            if (BackendDuty.Instance != null) BackendDuty.Instance.ConsistPage(SelectedMode == Mode.MatchReplay ? 0 : 1);
            SceneManager.LoadScene("Consist");
        }
    }
}
