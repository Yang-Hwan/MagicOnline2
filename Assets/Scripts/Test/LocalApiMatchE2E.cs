#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Assets.Scripts.Exert.BackSys;
using Assets.Scripts.Exert.Match;
using Assets.Scripts.Exert.Network;
using Assets.Scripts.Often;
using Assets.Scripts.Prack.BackSys;
using Assets.Scripts.Sight.Surface.Come;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;
using Assets.Scripts.Sight.Surface.Consist;
using Assets.Scripts.Sight.Surface.Pavilion;
using Physics = Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.PhysicsMng;
using Adapter = Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.PracticeMatchAdapter;

// Opt-in development player integration runner. No production accounts or stored credentials.
public sealed class LocalApiMatchE2E : MonoBehaviour
{
    [Serializable] public sealed class Round
    {
        public string MatchId, Scene, Phase, Reason, WinnerUserId;
        public int Score0, Score1;
        public long Coin;
        public bool ShotSettled, SawSuspended, SawRestored;
    }
    [Serializable] public sealed class Report
    {
        public string Mode, Role, UserId, Error;
        public bool Ok, ExitedForDisconnect;
        public bool StagedForRestart, Resumed, UsedLoginForm, UsedPublicLobby, SawOutcome, RematchEnabled;
        public int PendingBeforeRestart, PendingAfterRestart;
        public bool JournalRecovered, SawOffline;
        public int CorruptCopies, PendingDuringOutage;
        public long InitialCoin;
        public List<Round> Rounds = new List<Round>();
    }
    Report report;
    [Serializable] sealed class GuardEvidence
    { public bool Ok; public string MatchId, Phase; public int Attempts, Pending; }
    [Serializable] sealed class IsolationEvidence
    { public bool Ok; public string OtherUserId; public int OtherPending, OriginalPending; public long ReadStatus, WriteStatus, OtherCoin; }
    string output;
    int pavilionLoads;
    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    { if(report != null && scene.name == "Pavilion") pavilionLoads++; }
    static string Arg(string name)
    {
        var args = Environment.GetCommandLineArgs(); var index = Array.IndexOf(args, name);
        return index >= 0 && index+1 < args.Length ? args[index+1] : null;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (!Debug.isDebugBuild || Arg("-local-api-e2e") == null) return;
        var go = new GameObject(nameof(LocalApiMatchE2E)); DontDestroyOnLoad(go); go.AddComponent<LocalApiMatchE2E>();
    }
    void Save() => File.WriteAllText(output, JsonUtility.ToJson(report,true));
    static async Task Until(Func<bool> condition, int seconds, string stage)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (!condition()) { if(DateTime.UtcNow > deadline) throw new TimeoutException(stage); await Task.Delay(200); }
    }
    async void Start()
    {
        output = Arg("-e2e-output");
        if(string.IsNullOrEmpty(output)) { Debug.LogError("E2E output path required"); return; }
        bool resume = Arg("-e2e-resume") == "true";
        report = resume ? JsonUtility.FromJson<Report>(File.ReadAllText(output)) : new Report { Mode = Arg("-local-api-e2e"), Role = Arg("-e2e-role") };
        try
        {
            bool publicTest = report.Mode == "public" || report.Mode == "public-guard";
            bool restartTest = report.Mode == "restart" || report.Mode == "isolation" || report.Mode == "corrupt";
            if (!new Uri(GameApiClient.BaseUrl).IsLoopback ||
                (publicTest ? !PracticeMatchTestRoute.LocalPublicIntegrationTest || PracticeMatchTestRoute.Enabled : !PracticeMatchTestRoute.Enabled))
                throw new InvalidOperationException("E2E requires local API and isolated development route");
            Application.runInBackground = true;
            await Until(()=>FindAnyObjectByType<NetworkDuty>() != null,60,"Come scene");
            string login = Environment.GetEnvironmentVariable("MAGIC_E2E_LOGIN"), password = Environment.GetEnvironmentVariable("MAGIC_E2E_PASSWORD");
            if (resume)
            {
                BackendMatchHistory.Instance.PauseWritesForDevelopmentTest = true;
                IsolationEvidence isolated = null;
                if (report.Mode == "isolation")
                {
                    await GameApiClient.Login("e2e_"+Guid.NewGuid().ToString("N").Substring(0,16),"E2E-"+Guid.NewGuid().ToString("N"),true);
                    await BackendAttendance.Instance.LoadAsync(); await BackendGame.Instance.LoadAsync();
                    isolated = new IsolationEvidence { OtherUserId=GameApiClient.UserId, OtherCoin=BackendGame.Instance.UserMainData.Coin };
                    var otherHistory=BackendMatchHistory.Instance;
                    otherHistory.PauseWritesForDevelopmentTest=false;
                    await Task.Delay(6000); // Let the normal owner loader and retry loop run as the other account.
                    isolated.OtherPending=otherHistory.PendingWritesForDevelopmentTest;
                    try { await otherHistory.GetState(report.Rounds[0].MatchId); }
                    catch(GameApiException e) { isolated.ReadStatus=e.Status; }
                    try { await GameApiClient.Request<object>("/api/matches/"+report.Rounds[0].MatchId+"/result","POST",new BackendMatchHistory.ResultBody { ScoreA=0,ScoreB=0,WinnerSeat=-1 }); }
                    catch(GameApiException e) { isolated.WriteStatus=e.Status; }
                    if(isolated.OtherPending != 0 || isolated.OtherCoin != 30 || isolated.ReadStatus != 404 || isolated.WriteStatus != 403)
                        throw new Exception("other account could access the original queue or match");
                    otherHistory.PauseWritesForDevelopmentTest=true;
                }
                await GameApiClient.Login(login,password);
                if(GameApiClient.UserId != report.UserId) throw new Exception("restart account changed");
                var history = BackendMatchHistory.Instance;
                if(history.PendingWritesForDevelopmentTest != report.PendingBeforeRestart) throw new Exception("durable queue was not restored");
                if (report.Mode == "corrupt")
                {
                    report.JournalRecovered = history.LastStatus.Contains("복구");
                    string journalPath = history.JournalPathForDevelopmentTest;
                    report.CorruptCopies = Directory.GetFiles(Path.GetDirectoryName(journalPath), Path.GetFileName(journalPath) + ".corrupt-*").Length;
                    if (!report.JournalRecovered || report.CorruptCopies == 0) throw new Exception("corrupt queue was not recovered and quarantined");
                }
                if(isolated != null)
                {
                    isolated.OriginalPending=history.PendingWritesForDevelopmentTest;
                    if((await history.GetState(report.Rounds[0].MatchId)).Phase != "Playing") throw new Exception("other account changed the match");
                    isolated.Ok=true; File.WriteAllText(output+".isolation.json",JsonUtility.ToJson(isolated,true));
                }
                history.PauseWritesForDevelopmentTest=false;
                await Until(()=>history.IsSettled(report.Rounds[0].MatchId) && history.PendingWritesForDevelopmentTest == 0,35,"queue replay");
                var state = await history.GetState(report.Rounds[0].MatchId);
                if(state.Receipt.Reason != "MutualConfirmation") throw new Exception("restart did not settle the original result");
                await BackendGame.Instance.LoadAsync();
                var restored = report.Rounds[0]; restored.Phase=state.Phase; restored.Reason=state.Receipt.Reason;
                restored.Coin=BackendGame.Instance.UserMainData.Coin;
                if(restored.Coin != report.InitialCoin) throw new Exception("restart draw refund incorrect");
                report.PendingAfterRestart=history.PendingWritesForDevelopmentTest; report.Resumed=true; report.Ok=true; Save();
                Debug.Log("[LocalApiE2E] PASS: "+report.Mode+" role=A"); return;
            }
            if(string.IsNullOrEmpty(login)) { login="e2e_"+Guid.NewGuid().ToString("N").Substring(0,16); password="E2E-"+Guid.NewGuid().ToString("N"); }
            if(publicTest)
            {
                var form = FindAnyObjectByType<PnlLogin>();
                form.transform.Find("InpId").GetComponent<TMP_InputField>().text=login;
                form.transform.Find("InpPw").GetComponent<TMP_InputField>().text=password;
                form.transform.Find("BtnEntry").GetComponent<Button>().onClick.Invoke();
                await Until(()=>PhotonNetwork.InLobby && SceneManager.GetActiveScene().name == "Consist",60,"registration form to lobby");
                report.UsedLoginForm=true;
            }
            else await GameApiClient.Login(login,password,true);
            await BackendChart.LoadAllAsync(); await BackendAttendance.Instance.LoadAsync(); await BackendGame.Instance.LoadAsync();
            report.UserId = GameApiClient.UserId; report.InitialCoin = BackendGame.Instance.UserMainData.Coin; Save();
            if(restartTest && report.Role == "A") BackendMatchHistory.Instance.PauseWritesForDevelopmentTest=true;
            if(!publicTest) FindAnyObjectByType<NetworkDuty>().NetworkSetup();
            await Until(()=>PhotonNetwork.InLobby && SceneManager.GetActiveScene().name == "Consist",60,"Photon lobby");
            int hall = int.Parse(Arg("-e2e-hall"));
            if(publicTest)
            {
                var swipe=FindAnyObjectByType<RoomSwipe>(FindObjectsInactive.Include);
                var menu=swipe.GetComponentInParent<MainComp>(true);
                FindAnyObjectByType<MainSwipe>().SetScrollBarValue(4-menu.transform.GetSiblingIndex());
                string title=BackendChart.skillMatchData.Single(h=>h.HallIdx == hall).HallName;
                Func<HallRoom> findCard=()=>swipe.GetComponentsInChildren<HallRoom>(true)
                    .SingleOrDefault(h=>h.GetComponentsInChildren<TextMeshProUGUI>(true).Any(t=>t.name == "TxtTitle" && t.text == title));
                await Until(()=>swipe.isActiveAndEnabled && findCard() != null,30,"public hall cards");
                swipe.SetScrollBarValue(BackendChart.skillMatchData.FindIndex(h=>h.HallIdx == hall));
                await Task.Delay(700);
                findCard().transform.GetChild(0).GetComponent<Button>().onClick.Invoke(); report.UsedPublicLobby=true;
                await Until(()=>SceneManager.GetActiveScene().name == "Pavilion",100,"public lobby to Pavilion");
                var publicRound=new Round { MatchId=BackendMatchHistory.Instance.LastMatchId, Scene="Pavilion" };
                report.Rounds.Add(publicRound); Save();
                await BackendGame.Instance.LoadAsync();
                if(BackendGame.Instance.UserMainData.Coin != report.InitialCoin - BackendChart.skillMatchData.Single(h=>h.HallIdx == hall).PrizeCoin ||
                    PoolPlayer.mainPlayer.coin != BackendGame.Instance.UserMainData.Coin || NetworkManager.user_coin != BackendGame.Instance.UserMainData.Coin)
                    throw new Exception("public entry wallet display cache is stale");
                if(report.Mode == "public-guard")
                {
                    await Until(()=>PoolCoach.Instance.isMatchTimePlay,45,"public live clock for forged result test");
                    var rpc=PhotonView.Find(1);
                    rpc.RPC("HistoryResultRPC",RpcTarget.Others,publicRound.MatchId,"",0,0,false);
                    rpc.RPC("HistoryResultRPC",RpcTarget.Others,publicRound.MatchId,GameApiClient.UserId,999,0,false);
                    rpc.RPC("HistoryResultRPC",RpcTarget.Others,Guid.NewGuid().ToString("N"),"",0,0,false);
                    ((PunNetwork)NetworkManager.network).RecordDisplayedLegacyOutcome();
                    await Task.Delay(6000);
                    var guarded=await BackendMatchHistory.Instance.GetState(publicRound.MatchId);
                    int pending=BackendMatchHistory.Instance.PendingWritesForDevelopmentTest;
                    if(guarded.Phase != "Playing" || pending != 0 || PoolLogic.gameState.gameIsComplete || FindAnyObjectByType<PnlOutcome>() != null)
                        throw new Exception("forged result ended or confirmed a live match");
                    File.WriteAllText(output+".guard.json",JsonUtility.ToJson(new GuardEvidence { Ok=true, MatchId=publicRound.MatchId, Phase=guarded.Phase, Attempts=4, Pending=pending },true));
                }
                await Until(()=>FindAnyObjectByType<PnlOutcome>() != null,150,"public outcome screen");
                report.SawOutcome=true;
                await Until(()=>BackendMatchHistory.Instance.IsSettled(publicRound.MatchId) && BackendMatchHistory.Instance.PendingWritesForDevelopmentTest == 0,20,"public settlement and durable queue acknowledgement");
                var state=await BackendMatchHistory.Instance.GetState(publicRound.MatchId);
                publicRound.Phase=state.Phase; publicRound.Reason=state.Receipt.Reason;
                await BackendGame.Instance.LoadAsync(); publicRound.Coin=BackendGame.Instance.UserMainData.Coin;
                if(state.Receipt.Reason != "MutualConfirmation" || publicRound.Coin != report.InitialCoin) throw new Exception("public outcome mismatch");
                await Until(()=>FindAnyObjectByType<PnlOutcome>().transform.Find("BoxPlayerCardSelf/TxtCoin").GetComponent<TextMeshProUGUI>().text == publicRound.Coin.ToString(),10,"public wallet label");
                await Until(()=>FindAnyObjectByType<PnlOutcome>().transform.Find("BoxBtns/BtnPlay").GetComponent<Button>().interactable,10,"public rematch button");
                if(pavilionLoads != 1) throw new Exception("public scene loaded more than once");
                Debug.Log("[LocalApiE2E] public PavilionLoads="+pavilionLoads);
                report.RematchEnabled=true; report.Ok=true; Save(); Debug.Log("[LocalApiE2E] PASS: public role="+report.Role); return;
            }
            NetworkManager.network.JoinRandomRoom(hall);
            long entry = BackendChart.skillMatchData.Single(h=>h.HallIdx == hall).PrizeCoin;
            long expectedCoin = report.InitialCoin;
            await Until(()=>PhotonNetwork.InRoom && PhotonNetwork.PlayerList.Length == 2,75,"two participants");
            var peer = PhotonNetwork.PlayerList.Single(p=>p.ActorNumber != PhotonNetwork.LocalPlayer.ActorNumber);
            var profile = new PlayerProfile(peer.NickName,-1,false,peer.NickName,0,0,PlayerState.Away);
            if(!await PoolPlayer.OnGotoPlayWithPlayer(profile)) throw new InvalidOperationException("opponent profile");
            if(PhotonNetwork.IsMasterClient) NetworkManager.network.WaitCountdowned();
            var transport = FindAnyObjectByType<PracticeMatchSetupTransport>();
            int rounds = report.Mode == "rematch" ? 2 : 1;
            for(int n=0;n<rounds;n++)
            {
                string previous = n == 0 ? "" : report.Rounds[n-1].MatchId;
                await Until(()=>transport.Session != null && transport.Session.MatchId != previous && transport.Session.Phase == MatchStartPhase.Started,75,"server start approval");
                var session = transport.Session;
                var round = new Round { MatchId=session.MatchId,Scene=SceneManager.GetActiveScene().name }; report.Rounds.Add(round); Save();
                await Until(()=>transport.Results.Initialized && FindAnyObjectByType<Adapter>() != null,30,"physics bootstrap");
                if(report.Mode == "disconnect" && !PhotonNetwork.IsMasterClient)
                {
                    await Task.Delay(5000);
                    report.ExitedForDisconnect=true; Save();
                    Debug.Log("[LocalApiE2E] disconnecting development guest process");
                    Application.Quit(0); return;
                }
                bool submitted = false, surrendered = false, outage = false;
                var deadline = DateTime.UtcNow.AddSeconds(150);
                while(DateTime.UtcNow < deadline)
                {
                    if(session.Phase == MatchStartPhase.Suspended) round.SawSuspended = true;
                    if(round.SawSuspended && session.Phase == MatchStartPhase.Started) round.SawRestored = true;
                    // One real physics shot through the normal adapter; never inject a score/result.
                    var adapter = FindAnyObjectByType<Adapter>(); var physics = FindAnyObjectByType<Physics>();
                    if(!submitted && session.Configuration.LocalSeat == 0 && adapter && adapter.CanSubmit && physics && !physics.shotController.IsShotAnimating)
                    {
                        var stroke = physics.shotController.CaptureOnlineStroke(); stroke.power=.25f;
                        submitted = adapter.Submit(stroke);
                    }
                    if(transport.Shots?.LastSettled != null && transport.Shots.LastSettled.shotId > 0) round.ShotSettled = true;
                    if(report.Mode == "outage" && round.ShotSettled && !outage)
                    {
                        outage = true;
                        var lossPeer = PhotonNetwork.NetworkingClient.LoadBalancingPeer;
                        bool simulated = lossPeer.IsSimulationEnabled;
                        int incoming = lossPeer.NetworkSimulationSettings.IncomingLossPercentage;
                        int outgoing = lossPeer.NetworkSimulationSettings.OutgoingLossPercentage;
                        try
                        {
                            GameApiClient.OfflineForDevelopmentTest = true;
                            lossPeer.NetworkSimulationSettings.IncomingLossPercentage = 100;
                            lossPeer.NetworkSimulationSettings.OutgoingLossPercentage = 100;
                            lossPeer.IsSimulationEnabled = true;
                            BackendMatchHistory.Instance.QueueSurrender(round.MatchId);
                            report.SawOffline = true; Save();
                            // Exceeds the API's 15s heartbeat timeout + 30s grace + worker tick.
                            await Task.Delay(55000);
                            report.PendingDuringOutage = BackendMatchHistory.Instance.PendingWritesForDevelopmentTest;
                            if(report.PendingDuringOutage != 1) throw new Exception("outage lost a queued request");
                        }
                        finally
                        {
                            GameApiClient.OfflineForDevelopmentTest = false;
                            lossPeer.NetworkSimulationSettings.IncomingLossPercentage = incoming;
                            lossPeer.NetworkSimulationSettings.OutgoingLossPercentage = outgoing;
                            lossPeer.IsSimulationEnabled = simulated;
                        }
                        await Until(()=>BackendMatchHistory.Instance.IsSettled(round.MatchId) && BackendMatchHistory.Instance.PendingWritesForDevelopmentTest == 0,30,"outage receipt recovery");
                        report.PendingAfterRestart = BackendMatchHistory.Instance.PendingWritesForDevelopmentTest;
                    }
                    if(report.Mode == "rematch" && n == 1 && session.Configuration.LocalSeat == 1 && !surrendered)
                    { surrendered=true; transport.LeaveSession(); }
                    round.Score0=transport.Results?.Score0 ?? 0; round.Score1=transport.Results?.Score1 ?? 0;
                    if(restartTest && report.Role == "A" && transport.Results?.Finished == true)
                    {
                        report.PendingBeforeRestart=BackendMatchHistory.Instance.PendingWritesForDevelopmentTest;
                        if(report.PendingBeforeRestart != 1) throw new Exception("result was not durably queued");
                        var pending=await BackendMatchHistory.Instance.GetState(round.MatchId);
                        if(pending.Phase == "Applied") throw new Exception("result was sent before restart");
                        if(report.Mode == "corrupt")
                        {
                            string journalPath=BackendMatchHistory.Instance.JournalPathForDevelopmentTest;
                            if(!File.Exists(journalPath+".bak")) throw new Exception("durable backup missing");
                            File.WriteAllText(journalPath,"{interrupted");
                        }
                        report.StagedForRestart=true; Save(); Application.Quit(0); return;
                    }
                    var state = await BackendMatchHistory.Instance.GetState(round.MatchId); round.Phase=state.Phase;
                    if(state.Phase == "Applied")
                    {
                        round.Reason=state.Receipt.Reason; round.WinnerUserId=state.Receipt.WinnerUserId;
                        expectedCoin = checked(expectedCoin - entry + (session.Configuration.LocalSeat == 0 ? state.Receipt.RewardA : state.Receipt.RewardB));
                        await BackendGame.Instance.LoadAsync(); round.Coin=BackendGame.Instance.UserMainData.Coin;
                        if(round.Coin != expectedCoin) throw new Exception("wallet does not match entry and receipt");
                        Save(); break;
                    }
                    Save(); await Task.Delay(1000);
                }
                if(round.Phase != "Applied") throw new TimeoutException("settlement: "+BackendMatchHistory.Instance.LastStatus);
                await Until(()=>BackendMatchHistory.Instance.IsSettled(round.MatchId) && BackendMatchHistory.Instance.PendingWritesForDevelopmentTest == 0,20,"durable queue acknowledgement");
                string expectedReason = report.Mode == "outage" ? "BothDisconnected" : report.Mode == "disconnect" ? "DisconnectForfeit" : report.Mode == "rematch" && n == 1 ? "Surrender" : "MutualConfirmation";
                if(round.Reason != expectedReason) throw new Exception("unexpected settlement reason: "+round.Reason);
                if(n == 0 && report.Mode != "disconnect" && !round.ShotSettled) throw new Exception("real physics shot was not observed");
                if(report.Mode == "disconnect" && round.WinnerUserId != report.UserId) throw new Exception("remaining player was not awarded the win");
                if(report.Mode == "reconnect" && session.Configuration.LocalSeat == 0 && !round.SawRestored) throw new Exception("reconnect was not observed");
                if(n+1 < rounds)
                {
                    await Until(()=>transport.CanRequestRematch,15,"rematch enabled");
                    if(!transport.RequestRematch()) throw new Exception("rematch request rejected");
                }
            }
            report.Ok=true; Save(); Debug.Log("[LocalApiE2E] PASS: "+report.Mode+" role="+report.Role);
        }
        catch(Exception e) { report.Error=e.Message; Save(); Debug.LogError("[LocalApiE2E] FAIL: "+e); }
    }
}
#endif
