using System;
using System.Linq;
using Assets.Scripts.Exert.BackSys;
using Assets.Scripts.Often;
using Assets.Scripts.Prack.BackSys.ChartData;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace Assets.Scripts.Exert.Network
{
    // Installed on the persistent Network object. Not selected by the public lobby
    // until authoritative shot transport is connected and verified.
    public sealed class PracticeMatchSetupTransport : MonoBehaviourPunCallbacks, IOnEventCallback
    {
        const byte OfferCode = 150, AcceptCode = 151, LoadCode = 152, ReadyCode = 153, StartCode = 154, AbortCode = 155;
        const byte ShotRequestCode = 156, ShotCommitCode = 157, BallStateCode = 158;
        const byte ResultCode = 159;
        const byte RematchVoteCode = 163;
        const byte RecoveryChallengeCode = 160, RecoveryRequestCode = 161, RecoveryStateCode = 162;
        [Serializable] sealed class Offer
        {
            public string id, key, name0, name1, room, previousId;
            public int actor0, actor1, firstSeat, turnSeconds;
            public SkillMatchData hall;
        }
        public MatchStartSession Session { get; private set; }
        public MatchShotSession Shots { get; private set; }
        public MatchResultSession Results { get; private set; }
        public MatchRecoverySession Recovery { get; private set; }
        Offer currentOffer;
        public MatchRematchSession Rematch { get; private set; }
        bool reconnecting, leaving;
        bool ParticipantsPresent => PhotonNetwork.InRoom && Session != null &&
            PhotonNetwork.MasterClient.ActorNumber == Session.AuthorityActor && PhotonNetwork.PlayerList.Length == 2 &&
            PhotonNetwork.PlayerList.All(p => !p.IsInactive &&
                (p.ActorNumber == Session.ActorForSeat(0) || p.ActorNumber == Session.ActorForSeat(1)));
        public bool CanRequestRematch => ParticipantsPresent && Results != null && Results.Finished &&
            Session.Phase == MatchStartPhase.Started && Rematch != null && !Rematch.Closed && !Rematch.LocalRequested;
        public string RematchStatus => Rematch == null ? "" : Rematch.Closed ? "재대전 대기 종료 · 나가서 다시 입장하세요" :
            Rematch.LocalRequested ? "상대방 재대전 동의 대기 중 (최대 30초)" : "양쪽이 재대전을 선택하면 새 경기가 시작됩니다";
        public bool RequestRematch()
        {
            if (!CanRequestRematch || !Send(RematchVoteCode, Session.MatchId)) return false;
            if (!Rematch.Vote(Session.LocalActor, Session.MatchId, Time.realtimeSinceStartupAsDouble)) return false;
            TryRematch(); return true;
        }
        void TryRematch()
        {
            if (!ParticipantsPresent || !PhotonNetwork.IsMasterClient || !Rematch.Both) return;
            var offer = JsonUtility.FromJson<Offer>(JsonUtility.ToJson(currentOffer));
            offer.previousId = Session.MatchId; offer.id = Guid.NewGuid().ToString("N");
            offer.firstSeat = 1 - offer.firstSeat; offer.key = Key(offer);
            if (!Install(offer)) { Rematch.Close(); return; }
            Session.AcceptConfiguration(Session.AuthorityActor, offer.id, offer.key);
            if (!Send(OfferCode, JsonUtility.ToJson(offer))) Session.Abort("재대전 설정을 전송하지 못했습니다.");
        }
        public void AllowNewMatch()
        {
            if (Session != null && Session.Phase == MatchStartPhase.Aborted && PracticeSceneFlow.OnlineSession != Session)
                Clear("이전 경기 준비가 종료되었습니다.");
            if (Session == null) leaving = false;
        }
        bool sceneEntered;
        bool cancelOfferTestTriggered;
        bool CancelOnOfferForTest(Offer offer)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!cancelOfferTestTriggered && Session == null && !PhotonNetwork.IsMasterClient &&
                RecoveryTestOption("-practice-match-cancel-offer-test") &&
                NetworkManager.TryGetExistingNetwork(out var network))
            {
                cancelOfferTestTriggered = true;
                Debug.Log($"[MatchSetupTest] match={offer.id} event=CancelOnOffer hall={offer.hall?.HallIdx}");
                // Exercise the normal cancellation path, then let the received offer
                // reach Install so its cancellation guard must reject it.
                network.LeaveMatch();
                return true;
            }
#endif
            return false;
        }
        bool recoveryTestTriggered, recoveryTestActive;
        double recoveryTestAt;
        int previousIncomingLoss, previousOutgoingLoss;
        bool RecoveryTestOption(string option) => PracticeMatchTestRoute.Enabled && Debug.isDebugBuild &&
            Array.Exists(Environment.GetCommandLineArgs(), a => a == option);
        void TraceRecovery(string detail)
        {
            if (PracticeMatchTestRoute.Enabled && Session != null)
                Debug.Log($"[MatchRecovery] match={Session.MatchId} local={Session.Configuration.LocalSeat} {detail} realtime={Time.realtimeSinceStartupAsDouble:F3}");
        }
        void StopRecoveryTest()
        {
            if (!recoveryTestActive) return;
            var peer = PhotonNetwork.NetworkingClient.LoadBalancingPeer;
            PhotonNetwork.NetworkingClient.SimulateConnectionLoss(false);
            peer.NetworkSimulationSettings.IncomingLossPercentage = previousIncomingLoss;
            peer.NetworkSimulationSettings.OutgoingLossPercentage = previousOutgoingLoss;
            recoveryTestActive = false;
        }
        void TraceRestored(MatchRecoveryCheckpoint checkpoint) => TraceRecovery(
            $"event=Restored score={checkpoint.score0}:{checkpoint.score1} turn={checkpoint.seat} revision={checkpoint.turnRevision} " +
            $"sequence={checkpoint.sequence} matchRemaining={checkpoint.matchDeadline - checkpoint.time:F3} turnRemaining={checkpoint.turnDeadline - checkpoint.time:F3}");
        void TickRecoveryTest()
        {
            if (recoveryTestActive && Time.realtimeSinceStartupAsDouble - recoveryTestAt > 25)
            { StopRecoveryTest(); TraceRecovery("event=TestWatchdogRestoredNetwork"); }
            if (recoveryTestTriggered || Session.LocalActor == Session.AuthorityActor ||
                Session.Phase != MatchStartPhase.Started) return;
            if (RecoveryTestOption("-practice-match-moving-disconnect-test") && Shots?.Phase == MatchShotPhase.Moving)
            {
                recoveryTestTriggered = true;
                TraceRecovery($"event=TestMovingDisconnect shot={Shots.ShotId}");
                PhotonNetwork.Disconnect();
                return;
            }
            bool movingLoss = RecoveryTestOption("-practice-match-moving-loss-test");
            if (Results == null || Shots == null) return;
            if (movingLoss)
            {
                if (Shots.Phase != MatchShotPhase.Moving) return;
            }
            else if ((!RecoveryTestOption("-practice-match-recovery-test") &&
                !RecoveryTestOption("-practice-match-recovery-expiry-test") &&
                !RecoveryTestOption("-practice-match-scored-recovery-test")) ||
                !Results.CanShoot(PhotonNetwork.Time) || Shots.Phase != MatchShotPhase.Ready ||
                Results.TurnDeadline - PhotonNetwork.Time < 10) return;
            if (!movingLoss && RecoveryTestOption("-practice-match-scored-recovery-test") &&
                (Shots.ShotId == 0 || (Results.Score0 == 0 && Results.Score1 == 0))) return;
            var peer = PhotonNetwork.NetworkingClient.LoadBalancingPeer;
            if (peer.IsSimulationEnabled) return;
            recoveryTestTriggered = recoveryTestActive = true;
            recoveryTestAt = Time.realtimeSinceStartupAsDouble;
            previousIncomingLoss = peer.NetworkSimulationSettings.IncomingLossPercentage;
            previousOutgoingLoss = peer.NetworkSimulationSettings.OutgoingLossPercentage;
            TraceRecovery($"event=TestLossStarted score={Results.Score0}:{Results.Score1} turn={Shots.TurnSeat} revision={Shots.TurnRevision} shot={Shots.ShotId} shotPhase={Shots.Phase}");
            PhotonNetwork.NetworkingClient.SimulateConnectionLoss(true);
        }

        public bool BeginPracticeSetup(int hallId)
        {
            if (!PracticeMatchTestRoute.Enabled || leaving || Session != null || !PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || PhotonNetwork.PlayerList.Length != 2)
                return false;
            var hall = BackendChart.skillMatchData?.FirstOrDefault(h => h.HallIdx == hallId);
            var self = Assets.Scripts.Exert.Match.PoolPlayer.mainPlayer;
            var other = Assets.Scripts.Exert.Match.PoolPlayer.otherPlayer;
            if (hall == null || self == null || other == null) return false;
            var peer = PhotonNetwork.PlayerList.Single(p => p.ActorNumber != PhotonNetwork.LocalPlayer.ActorNumber);
            var offer = new Offer { id = Guid.NewGuid().ToString("N"), hall = PracticeMatchTestRoute.MatchHall(hall), room = PhotonNetwork.CurrentRoom.Name,
                actor0 = PhotonNetwork.LocalPlayer.ActorNumber, actor1 = peer.ActorNumber,
                name0 = self.name, name1 = other.name, firstSeat = 0, turnSeconds = 20 };
            offer.key = Key(offer);
            if (!Install(offer)) return false;
            PhotonNetwork.CurrentRoom.IsOpen = false;
            PhotonNetwork.CurrentRoom.PlayerTtl = 30000;
            Session.AcceptConfiguration(offer.actor0, offer.id, offer.key);
            if (!Send(OfferCode, JsonUtility.ToJson(offer))) { Session.Abort("경기 설정을 전송하지 못했습니다."); return false; }
            return true;
        }

        static string Key(Offer offer) => MatchmakingCompatibility.RulesKey(offer.hall) +
            "/bootstrap-2/" + offer.room + "/" + offer.actor0 + "/" + offer.actor1 + "/" + offer.firstSeat + "/" + offer.turnSeconds;

        bool Install(Offer offer)
        {
            try
            {
                if (!PracticeMatchTestRoute.Enabled || leaving || !PhotonNetwork.InRoom ||
                    !PracticeMatchTestRoute.AcceptsProtocol(PhotonNetwork.CurrentRoom.CustomProperties["protocol"]) ||
                    offer == null || offer.hall == null || string.IsNullOrEmpty(offer.id) || offer.id.Length > 64 ||
                    offer.room != PhotonNetwork.CurrentRoom.Name ||
                    offer.key != Key(offer) || offer.actor0 != PhotonNetwork.MasterClient.ActorNumber ||
                    !PhotonNetwork.CurrentRoom.Players.ContainsKey(offer.actor1) ||
                    PhotonNetwork.CurrentRoom.PlayerCount != 2 || offer.hall.HallIdx != NetworkManager.hallIdx) return false;
                var localHall = BackendChart.skillMatchData?.FirstOrDefault(h => h.HallIdx == offer.hall.HallIdx);
                if (MatchmakingCompatibility.RulesKey(PracticeMatchTestRoute.MatchHall(localHall)) != MatchmakingCompatibility.RulesKey(offer.hall)) return false;
                int local = PhotonNetwork.LocalPlayer.ActorNumber;
                if (local != offer.actor0 && local != offer.actor1) return false;
                var config = MatchConfiguration.Online(offer.hall, offer.name0, offer.name1,
                    offer.firstSeat, local == offer.actor0 ? 0 : 1, offer.turnSeconds);
                var next = new MatchStartSession(offer.id, offer.key, config, offer.actor0, offer.actor1, Time.realtimeSinceStartupAsDouble);
                if (Session != null) Session.LocalSceneReady -= OnLocalSceneReady;
                Session = next; currentOffer = offer; sceneEntered = abortSent = false;
                Shots = new MatchShotSession(Session);
                Results = new MatchResultSession(Session, Shots);
                Recovery = new MatchRecoverySession(Session, Shots, Results);
                Rematch = new MatchRematchSession(Session, Results);
                leaving = false;
                Session.LocalSceneReady += OnLocalSceneReady;
                return true;
            }
            catch (Exception e) when (e is ArgumentException || e is NotSupportedException || e is OverflowException)
            { return false; }
        }

        bool Send(byte code, object data) => PhotonNetwork.RaiseEvent(code, data,
            new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);

        // Scene input will call this once its authority/physics adapter is installed.
        public bool RequestShot(MatchShotCommand shot)
        {
            if (!PhotonNetwork.InRoom || Session == null || Shots == null || shot == null ||
                Session.Phase != MatchStartPhase.Started || shot.matchId != Session.MatchId ||
                shot.seat != Session.Configuration.LocalSeat) return false;
            if (Session.LocalActor == Session.AuthorityActor) return ApproveShot(Session.LocalActor, shot);
            return Send(ShotRequestCode, JsonUtility.ToJson(shot));
        }

        bool ApproveShot(int sender, MatchShotCommand shot)
        {
            if (Shots == null || Results == null || !Results.CanShoot(PhotonNetwork.Time) || !Shots.CanApprove(sender, shot)) return false;
            if (!Send(ShotCommitCode, JsonUtility.ToJson(shot)))
            { Session.Abort("타격 승인을 전송하지 못했습니다."); return false; }
            return Shots.CommitShot(Session.LocalActor, shot);
        }

        public bool PublishBallState(MatchBallSnapshot snapshot)
        {
            if (!PhotonNetwork.InRoom || Session == null || Shots == null ||
                Session.LocalActor != Session.AuthorityActor || !Shots.ApplySnapshot(Session.LocalActor, snapshot)) return false;
            // Initial and terminal states must arrive; intermediate motion may drop without blocking later frames.
            bool sent = PhotonNetwork.RaiseEvent(BallStateCode, JsonUtility.ToJson(snapshot),
                new RaiseEventOptions { Receivers = ReceiverGroup.Others },
                new SendOptions { Reliability = snapshot.settled });
            if (!sent) Session.Abort("공 상태를 전송하지 못했습니다.");
            return sent;
        }

        public bool PublishResult(MatchResultMessage result)
        {
            if (!PhotonNetwork.InRoom || Session == null || Results == null || Session.LocalActor != Session.AuthorityActor) return false;
            if (!Send(ResultCode, JsonUtility.ToJson(result)))
            { Session.Abort("경기 결과를 전송하지 못했습니다."); return false; }
            return Results.Apply(Session.LocalActor, result);
        }

        void ReceiveShotEvent(EventData data)
        {
            if (Shots == null || Session.Phase != MatchStartPhase.Started ||
                !(data.CustomData is string json) || json.Length > 8192) return;
            try
            {
                if (data.Code == ShotRequestCode && Session.LocalActor == Session.AuthorityActor)
                    ApproveShot(data.Sender, JsonUtility.FromJson<MatchShotCommand>(json));
                else if (data.Code == ShotCommitCode && data.Sender == Session.AuthorityActor)
                    Shots.CommitShot(data.Sender, JsonUtility.FromJson<MatchShotCommand>(json));
                else if (data.Code == BallStateCode && data.Sender == Session.AuthorityActor)
                    Shots.ApplySnapshot(data.Sender, JsonUtility.FromJson<MatchBallSnapshot>(json));
                else if (data.Code == ResultCode && data.Sender == Session.AuthorityActor)
                    Results.Apply(data.Sender, JsonUtility.FromJson<MatchResultMessage>(json));
            }
            catch (ArgumentException) { /* Malformed network payload: do not change match state. */ }
        }

        void OnLocalSceneReady(int actor, string id, string key)
        {
            if (leaving || Session == null || actor != Session.LocalActor ||
                id != Session.MatchId || key != Session.ConfigurationKey ||
                (Session.Phase != MatchStartPhase.Loading && Session.Phase != MatchStartPhase.Ready)) return;
            if (!Send(ReadyCode, new[] { id, key })) Session.Abort("준비 상태를 전송하지 못했습니다.");
            TryStart();
        }

        void TryStart()
        {
            if (Session == null || !PhotonNetwork.IsMasterClient || Session.Phase != MatchStartPhase.Ready) return;
            if (!Send(StartCode, Session.MatchId)) { Session.Abort("시작 승인을 전송하지 못했습니다."); return; }
            Session.CommitStart(PhotonNetwork.LocalPlayer.ActorNumber, Session.MatchId);
        }

        public void OnEvent(EventData data)
        {
            if (!PracticeMatchTestRoute.Enabled || !PhotonNetwork.InRoom || leaving || data.Code < OfferCode || data.Code > RematchVoteCode) return;
            if (data.Code == RematchVoteCode)
            {
                if (ParticipantsPresent && Rematch != null && data.CustomData is string vote &&
                    Rematch.Vote(data.Sender, vote, Time.realtimeSinceStartupAsDouble)) TryRematch();
                return;
            }
            if (data.Code >= RecoveryChallengeCode)
            {
                ReceiveRecovery(data); return;
            }
            if (data.Code >= ShotRequestCode)
            {
                if (Session != null) ReceiveShotEvent(data);
                return;
            }
            if (data.Code == OfferCode)
            {
                if (data.Sender != PhotonNetwork.MasterClient.ActorNumber ||
                    !(data.CustomData is string json) || json.Length > 16384) return;
                Offer offer;
                try { offer = JsonUtility.FromJson<Offer>(json); } catch (ArgumentException) { return; }
                if (offer == null) return;
                if (Session != null)
                {
                    Rematch?.Tick(Time.realtimeSinceStartupAsDouble);
                    if (!ParticipantsPresent || Rematch == null || !Rematch.Both || offer.previousId != Session.MatchId ||
                        offer.id == Session.MatchId || offer.actor0 != currentOffer.actor0 || offer.actor1 != currentOffer.actor1 ||
                        offer.firstSeat != 1 - currentOffer.firstSeat || offer.turnSeconds != currentOffer.turnSeconds ||
                        MatchmakingCompatibility.RulesKey(offer.hall) != MatchmakingCompatibility.RulesKey(currentOffer.hall)) return;
                }
                else if (!string.IsNullOrEmpty(offer.previousId)) return;
                bool cancelledForTest = CancelOnOfferForTest(offer);
                bool installed = Install(offer);
                if (cancelledForTest)
                    Debug.Log($"[MatchSetupTest] match={offer.id} event=OfferAfterCancel installed={installed} sessionPresent={Session != null} leaving={leaving}");
                if (!installed) return;
                Session.AcceptConfiguration(offer.actor0, offer.id, offer.key);
                Session.AcceptConfiguration(Session.LocalActor, offer.id, offer.key);
                if (!Send(AcceptCode, new[] { offer.id, offer.key })) Session.Abort("설정 확인을 전송하지 못했습니다.");
                return;
            }
            if (Session == null) return;
            if (data.Code == AcceptCode && PhotonNetwork.IsMasterClient && data.CustomData is string[] ack && ack.Length == 2)
            {
                if (!Session.AcceptConfiguration(data.Sender, ack[0], ack[1]) || Session.Phase != MatchStartPhase.Loading) return;
                if (!Send(LoadCode, Session.MatchId)) Session.Abort("경기장을 불러올 수 없습니다.");
                else EnterSceneOnce();
            }
            else if (data.Code == LoadCode && data.Sender == Session.AuthorityActor && data.CustomData as string == Session.MatchId)
            {
                EnterSceneOnce();
            }
            else if (data.Code == ReadyCode && data.CustomData is string[] ready && ready.Length == 2)
            {
                if (Session.MarkSceneReady(data.Sender, ready[0], ready[1])) TryStart();
            }
            else if (data.Code == StartCode && data.CustomData is string id) Session.CommitStart(data.Sender, id);
            else if (data.Code == AbortCode && data.CustomData as string == Session.MatchId &&
                PhotonNetwork.CurrentRoom.Players.ContainsKey(data.Sender)) {
                    if (Results.Finished) Rematch.Close();
                    else Session.Abort("경기 준비가 취소되었습니다.");
                }
        }

        bool abortSent;
        void EnterSceneOnce()
        {
            if (sceneEntered || Session.Phase != MatchStartPhase.Loading) return;
            sceneEntered = true;
            if (!PracticeSceneFlow.EnterOnline(Session)) Session.Abort("경기장을 불러올 수 없습니다.");
        }
        void Update()
        {
            if (Session == null) return;
            TickRecoveryTest();
            Session.Tick(Time.realtimeSinceStartupAsDouble);
            bool recoveryWasSuspended = Session.Phase == MatchStartPhase.Suspended;
            Recovery?.Tick(Time.realtimeSinceStartupAsDouble);
            if (recoveryWasSuspended && Session.Phase == MatchStartPhase.Aborted)
                TraceRecovery("event=RecoveryExpired");
            Rematch?.Tick(Time.realtimeSinceStartupAsDouble);
            if (Session.Phase == MatchStartPhase.Aborted && !abortSent && PhotonNetwork.InRoom)
            { abortSent = true; Send(AbortCode, Session.MatchId); }
            if (Session.Phase == MatchStartPhase.Aborted && reconnecting)
            { reconnecting = false; if (PhotonNetwork.IsConnected) PhotonNetwork.Disconnect(); }
        }
        public override void OnPlayerLeftRoom(Player player)
        {
            if (Results != null && Results.Finished) { Rematch?.Close(); return; }
            if (Session == null || (player.ActorNumber != Session.ActorForSeat(0) && player.ActorNumber != Session.ActorForSeat(1))) return;
            if (Session.Phase == MatchStartPhase.Suspended && player.ActorNumber != Session.AuthorityActor && player.IsInactive) return;
            if (player.ActorNumber != Session.AuthorityActor && player.IsInactive &&
                Recovery.Suspend(PhotonNetwork.Time, Time.realtimeSinceStartupAsDouble))
            { TraceRecovery("event=AuthoritySuspended"); return; }
            Session.Abort(player.IsInactive ? "타격 중 또는 진행자 연결이 끊겨 경기를 중단했습니다." : "상대방이 경기장을 나갔습니다.");
        }
        public override void OnMasterClientSwitched(Player player)
        { if (Session != null && !Results.Finished && player.ActorNumber != Session.AuthorityActor) Session.Abort("경기 진행자가 변경되었습니다."); }
        public override void OnDisconnected(DisconnectCause cause)
        {
            StopRecoveryTest();
            TraceRecovery($"event=Disconnected cause={cause}");
            if (Session == null || leaving) return;
            if (Results != null && Results.Finished) { Rematch?.Close(); return; }
            bool transient = cause == DisconnectCause.ClientTimeout || cause == DisconnectCause.ServerTimeout || cause == DisconnectCause.Exception;
            if (!reconnecting && transient && Session.LocalActor != Session.AuthorityActor &&
                Recovery.Suspend(PhotonNetwork.Time, Time.realtimeSinceStartupAsDouble))
            {
                reconnecting = true;
                // Leave the real timeout and 30-second recovery clock intact; only
                // withhold this test client's rejoin request to exercise expiration.
                if (recoveryTestTriggered && RecoveryTestOption("-practice-match-recovery-expiry-test"))
                { TraceRecovery("event=TestRejoinWithheld"); return; }
                if (PhotonNetwork.ReconnectAndRejoin()) { TraceRecovery("event=RejoinRequested"); return; }
            }
            reconnecting = false;
            Session.Abort("연결을 복구하지 못했습니다. 나가기를 눌러 다시 입장해 주세요.");
        }
        public override void OnJoinedRoom()
        {
            if (!reconnecting || Session == null) return;
            TraceRecovery("event=RejoinedRoom");
            if (Session.Phase != MatchStartPhase.Suspended || PhotonNetwork.LocalPlayer.ActorNumber != Session.LocalActor ||
                PhotonNetwork.MasterClient.ActorNumber != Session.AuthorityActor)
            { Session.Abort("이전 경기의 참가자 정보를 복구할 수 없습니다."); PhotonNetwork.LeaveRoom(false); return; }
            if (!Send(RecoveryChallengeCode, new[] { Session.MatchId, "" })) Session.Abort("복구 요청을 보낼 수 없습니다.");
        }
        public override void OnJoinRoomFailed(short code, string message)
        { if (reconnecting) { reconnecting = false; Session?.Abort("기존 경기장에 재입장하지 못했습니다."); } }

        void ReceiveRecovery(EventData data)
        {
            if (Session == null || Recovery == null || Session.Phase != MatchStartPhase.Suspended) return;
            if (data.Code == RecoveryChallengeCode && data.CustomData is string[] challenge && challenge.Length == 2 && challenge[0] == Session.MatchId)
            {
                if (Session.LocalActor == Session.AuthorityActor && data.Sender == Session.ActorForSeat(1) && challenge[1] == "")
                    Send(RecoveryChallengeCode, new[] { Session.MatchId, Recovery.RequestId });
                else if (data.Sender == Session.AuthorityActor && Session.LocalActor != Session.AuthorityActor && challenge[1]?.Length == 32)
                    Send(RecoveryRequestCode, new[] { Session.MatchId, challenge[1], Recovery.RequestId });
            }
            else if (data.Code == RecoveryRequestCode && data.CustomData is string[] request && request.Length == 3 &&
                request[0] == Session.MatchId && request[1] == Recovery.RequestId && Session.LocalActor == Session.AuthorityActor)
            {
                var checkpoint = Recovery.Capture(data.Sender, request[2], PhotonNetwork.Time, Time.realtimeSinceStartupAsDouble);
                if (checkpoint == null) return;
                if (!Send(RecoveryStateCode, JsonUtility.ToJson(checkpoint)) ||
                    !Recovery.Restore(Session.AuthorityActor, checkpoint, request[2], Time.realtimeSinceStartupAsDouble))
                    Session.Abort("경기 상태를 복구하지 못했습니다.");
                else TraceRestored(checkpoint);
            }
            else if (data.Code == RecoveryStateCode && data.Sender == Session.AuthorityActor &&
                Session.LocalActor != Session.AuthorityActor && data.CustomData is string json && json.Length <= 8192)
            {
                MatchRecoveryCheckpoint checkpoint;
                try { checkpoint = JsonUtility.FromJson<MatchRecoveryCheckpoint>(json); } catch (ArgumentException) { return; }
                if (Recovery.Restore(data.Sender, checkpoint, Recovery.RequestId, Time.realtimeSinceStartupAsDouble))
                { reconnecting = false; TraceRestored(checkpoint); }
            }
        }

        public void LeaveSession()
        {
            StopRecoveryTest();
            if (leaving) return;
            leaving = true; reconnecting = false;
            if (Session != null && PhotonNetwork.InRoom) Send(AbortCode, Session.MatchId);
            Clear("경기장을 나갔습니다.");
            if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom(false);
            else if (PhotonNetwork.IsConnected) PhotonNetwork.Disconnect();
        }
        public override void OnLeftRoom()
        {
            // Photon invokes this before OnDisconnected on unexpected game-server disconnects.
            if (!leaving && Session != null) return;
            Clear("경기장을 나갔습니다.");
        }
        void OnDestroy() { StopRecoveryTest(); Clear("경기가 종료되었습니다."); }
        void Clear(string reason)
        {
            if (Session == null) return;
            Session.LocalSceneReady -= OnLocalSceneReady;
            Session.Abort(reason); Session = null; Shots = null; Results = null; Recovery = null;
            reconnecting = false; abortSent = false; sceneEntered = false; Rematch = null; currentOffer = null;
        }
    }
}
