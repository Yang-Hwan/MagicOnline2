using System;
using System.Linq;
using Assets.Scripts.Exert.BackSys;
using Assets.Scripts.Exert.Match;
using Assets.Scripts.Often;
using Assets.Scripts.Prack.BackSys.GameData;
using Photon.Pun;
using UnityEngine;

namespace Assets.Scripts.Exert.Network
{
    public partial class PunNetwork
    {
        [Serializable] sealed class HistoryStartOffer
        {
            public string requestId, rulesKey, name0, name1;
            public int actor0, actor1;
            public MatchHistoryData match;
        }
        HistoryStartOffer historyOffer;
        UserMatchHistoryData historyPlayer;
        string historyStartRequest;
        bool historyStarted, historyEnded;
        float historyStartDeadline;
        bool LegacyHistoryEnabled => !PracticeMatchTestRoute.Enabled && PhotonNetwork.InRoom;
        bool HistoryPeerPresent => historyOffer != null && PhotonNetwork.InRoom && PhotonNetwork.PlayerList.Length == 2 &&
            PhotonNetwork.PlayerList.All(p => !p.IsInactive && (p.ActorNumber == historyOffer.actor0 || p.ActorNumber == historyOffer.actor1));

        void RequestHistoryStart()
        {
            if (!LegacyHistoryEnabled || PhotonNetwork.IsMasterClient || opponentPlayer == null ||
                (!historyEnded && (historyStarted || historyStartRequest != null))) return;
            historyStartRequest = Guid.NewGuid().ToString("N");
            historyStartDeadline = Time.realtimeSinceStartup + 30;
            view.RPC(nameof(HistoryStartRequestRPC), PhotonNetwork.MasterClient, historyStartRequest);
        }

        [PunRPC] void HistoryStartRequestRPC(string requestId, PhotonMessageInfo info)
        {
            if (!LegacyHistoryEnabled || !PhotonNetwork.IsMasterClient || info.Sender != opponentPlayer ||
                PhotonNetwork.PlayerList.Length != 2 || !Guid.TryParseExact(requestId, "N", out _) ||
                historyOffer?.requestId == requestId || (historyOffer != null && !historyEnded)) return;
            if (historyOffer != null && historyEnded && !PoolPlayer.IsReMatch()) return;
            try
            {
                var hall = BackendChart.skillMatchData?.FirstOrDefault(h => h.HallIdx == NetworkManager.hallIdx);
                var self = NetworkManager.mainPlayer; var other = NetworkManager.opponentPlayer;
                if (self == null || other == null || self.uuid != GameApiClient.UserId ||
                    string.IsNullOrEmpty(other.uuid) || other.uuid == self.uuid) throw new InvalidOperationException("Invalid match participants.");
                var offer = new HistoryStartOffer {
                    requestId = requestId, actor0 = PhotonNetwork.LocalPlayer.ActorNumber, actor1 = info.Sender.ActorNumber,
                    name0 = self.nickName, name1 = other.nickName, rulesKey = MatchmakingCompatibility.RulesKey(hall),
                    match = new MatchHistoryData {
                        MatchId = Guid.NewGuid().ToString("N"), MatchBall = hall.MatchBall, HallIdx = hall.HallIdx,
                        PlayerAUserId = self.uuid, PlayerBUserId = other.uuid,
                        Rules = MatchRulesContract.FromHall(hall, MatchRulesContract.Pavilion),
                        RulesSnapshot = JsonUtility.ToJson(new MatchRulesSnapshot {
                            HallIdx = hall.HallIdx, HallName = hall.HallName, MatchBall = hall.MatchBall,
                            TargetHit = hall.MatchBall == 4 ? 3 : hall.TargetHit, MatchTotMin = hall.MatchTotMin, MatchCushion = hall.MatchCushion,
                            FinishMission = hall.FinishMission, PrizeCoin = hall.PrizeCoin, MaxCoin = hall.MaxCoin
                        })
                    }
                };
                InstallHistoryOffer(offer, 0);
                BackendMatchHistory.Instance.RecordMatch(offer.match, () => {
                    if (historyOffer != offer || historyEnded || !HistoryPeerPresent ||
                        PhotonNetwork.MasterClient.ActorNumber != offer.actor0) return;
                    view.RPC(nameof(HistoryOfferRPC), opponentPlayer, JsonUtility.ToJson(offer));
                }, error => {
                    if (historyOffer != offer || historyEnded) return;
                    AbortLegacyHistory("RegistrationFailed"); CallNetworkState(NetworkState.JoinRoomFailed);
                });
            }
            catch (Exception e) { Debug.LogException(e); AbortLegacyHistory("RegistrationFailed"); CallNetworkState(NetworkState.JoinRoomFailed); }
        }

        void InstallHistoryOffer(HistoryStartOffer offer, int seat)
        {
            historyOffer = offer; historyStarted = historyEnded = false;
            historyStartDeadline = Time.realtimeSinceStartup + 30;
            historyPlayer = new UserMatchHistoryData {
                MatchId = offer.match.MatchId, SeatNo = seat, MatchBall = offer.match.MatchBall, HallIdx = offer.match.HallIdx,
                UserId = seat == 0 ? offer.match.PlayerAUserId : offer.match.PlayerBUserId,
                OpponentUserId = seat == 0 ? offer.match.PlayerBUserId : offer.match.PlayerAUserId,
                NicknameSnapshot = seat == 0 ? offer.name0 : offer.name1,
                OpponentNicknameSnapshot = seat == 0 ? offer.name1 : offer.name0
            };
        }

        [PunRPC] async void HistoryOfferRPC(string json, PhotonMessageInfo info)
        {
            if (!LegacyHistoryEnabled || PhotonNetwork.IsMasterClient || info.Sender != PhotonNetwork.MasterClient ||
                historyStartRequest == null || json == null || json.Length > 16384 || (historyStarted && !historyEnded)) return;
            try
            {
                var offer = JsonUtility.FromJson<HistoryStartOffer>(json);
                var hall = BackendChart.skillMatchData?.FirstOrDefault(h => h.HallIdx == NetworkManager.hallIdx);
                if (offer?.match == null || hall == null || offer.requestId != historyStartRequest ||
                    !Guid.TryParseExact(offer.match.MatchId, "N", out _) ||
                    offer.actor0 != info.Sender.ActorNumber || offer.actor1 != PhotonNetwork.LocalPlayer.ActorNumber ||
                    offer.match.PlayerBUserId != GameApiClient.UserId || offer.match.PlayerAUserId != NetworkManager.opponentPlayer?.uuid ||
                    offer.match.HallIdx != hall.HallIdx || offer.match.MatchBall != hall.MatchBall ||
                    offer.rulesKey != MatchmakingCompatibility.RulesKey(hall)) return;
                if (historyOffer != null && !historyEnded) return;
                InstallHistoryOffer(offer, 1);
                // Readiness claims come from our own runtime/chart, never the peer's contract.
                await BackendMatchHistory.Instance.ReadyAsync(offer.match.MatchId, MatchRulesContract.FromHall(hall, MatchRulesContract.Pavilion));
                if (historyEnded || historyOffer != offer || !HistoryPeerPresent) return;
                view.RPC(nameof(HistoryReadyRPC), info.Sender, offer.match.MatchId, offer.requestId);
            }
            catch (Exception e) { Debug.LogWarning(e is GameApiException api ? api.UserMessage : e.Message); AbortLegacyHistory("RegistrationFailed"); CallNetworkState(NetworkState.JoinRoomFailed); }
        }

        [PunRPC] async void HistoryReadyRPC(string matchId, string requestId, PhotonMessageInfo info)
        {
            if (!LegacyHistoryEnabled || !PhotonNetwork.IsMasterClient || !HistoryPeerPresent || historyEnded || historyStarted ||
                info.Sender.ActorNumber != historyOffer.actor1 || historyOffer.match.MatchId != matchId || historyOffer.requestId != requestId) return;
            try
            {
                await BackendMatchHistory.Instance.WaitPlayingAsync(matchId);
                if (historyEnded || historyStarted || historyOffer?.match.MatchId != matchId || !HistoryPeerPresent) return;
                long startedAt = BackendMatchHistory.Now;
                view.RPC(nameof(HistoryCommitRPC), info.Sender, matchId, requestId, startedAt);
                CommitLegacyHistory(startedAt);
            }
            catch (Exception e) { Debug.LogWarning(e.Message); AbortLegacyHistory("ServerStartFailed"); CallNetworkState(NetworkState.JoinRoomFailed); }
        }

        [PunRPC] void HistoryCommitRPC(string matchId, string requestId, long startedAt, PhotonMessageInfo info)
        {
            if (!LegacyHistoryEnabled || !HistoryPeerPresent || historyEnded || historyStarted || startedAt <= 0 ||
                info.Sender.ActorNumber != historyOffer.actor0 || matchId != historyOffer.match.MatchId ||
                requestId != historyStartRequest || requestId != historyOffer.requestId) return;
            CommitLegacyHistory(startedAt);
        }

        void CommitLegacyHistory(long startedAt)
        {
            historyStarted = true;
            BackendMatchHistory.Instance.Track(historyOffer.match.MatchId);
            historyOffer.match.StartedAt = startedAt; historyOffer.match.Status = "Playing";
            try {
                if (historyPlayer.SeatNo == 0) BackendMatchHistory.Instance.RecordMatch(historyOffer.match);
                BackendMatchHistory.Instance.RecordPlayer(historyPlayer);
            }
            catch (Exception e) { Debug.LogException(e); AbortLegacyHistory("StartSaveFailed"); CallNetworkState(NetworkState.JoinRoomFailed); return; }
            PoolPlayer.SetIdx(historyPlayer.SeatNo);
            CallNetworkState(NetworkState.StartToPlay);
        }

        void PublishHistoryResult(int isWin, bool secondChannel)
        {
            // Caller flags and peer RPCs are not evidence of a local terminal outcome.
            if (!CanReportLocalOutcome) return;
            RecordDisplayedLegacyOutcome();
        }

        bool CanReportLocalOutcome => LegacyHistoryEnabled && historyStarted && !historyEnded && historyOffer != null &&
            PoolLogic.gameState != null && PoolLogic.gameState.gameIsComplete &&
            PoolPlayer.mainPlayer != null && PoolPlayer.otherPlayer != null;

        // The public game normally opens PnlOutcome directly rather than calling BattleResult.
        public void RecordDisplayedLegacyOutcome()
        {
            if (!CanReportLocalOutcome) return;
            int winningPlayer = PoolPlayer.GetHitWin();
            string winner = winningPlayer == -1 ? "" : winningPlayer == PoolPlayer.mainPlayer.playerId ? historyPlayer.UserId : historyPlayer.OpponentUserId;
            int score0 = historyPlayer.SeatNo == 0 ? PoolPlayer.mainPlayer.hitCnt : PoolPlayer.otherPlayer.hitCnt;
            int score1 = historyPlayer.SeatNo == 0 ? PoolPlayer.otherPlayer.hitCnt : PoolPlayer.mainPlayer.hitCnt;
            view.RPC(nameof(HistoryResultRPC), RpcTarget.Others, historyOffer.match.MatchId, winner, score0, score1, false);
            EndLegacyHistory("Completed", "LegacyOutcome", winner, score0, score1);
        }

        [PunRPC] void HistoryResultRPC(string matchId, string winner, int score0, int score1, bool secondChannel, PhotonMessageInfo info)
        {
            if (!LegacyHistoryEnabled || historyOffer == null || !historyStarted || historyEnded || matchId != historyOffer.match.MatchId ||
                info.Sender.ActorNumber != (historyPlayer.SeatNo == 0 ? historyOffer.actor1 : historyOffer.actor0) ||
                (winner != "" && winner != historyPlayer.UserId && winner != historyPlayer.OpponentUserId) || score0 < 0 || score1 < 0) return;
            // Intentionally no local completion, UI transition, or authenticated report here.
            // Even a matching 0:0 draw can be forged while the game is still running.
            // Each replica reports only through its own terminal outcome path.
            if (Debug.isDebugBuild) Debug.Log("[MatchResultGuard] peer announcement ignored match=" + matchId);
        }

        void EndLegacyHistory(string status, string reason, string winner = "", int score0 = 0, int score1 = 0)
        {
            if (historyOffer == null || historyEnded) return;
            var match = historyOffer.match;
            match.EndedAt = BackendMatchHistory.Now; match.HistoryExpiresAt = BackendMatchHistory.Expires(match.EndedAt);
            match.DurationSec = historyStarted ? (int)Math.Max(0, (match.EndedAt - match.StartedAt) / 1000) : 0;
            match.Status = status; match.EndReason = reason; match.WinnerUserId = winner;
            historyPlayer.EndedAt = match.EndedAt; historyPlayer.HistoryExpiresAt = match.HistoryExpiresAt;
            historyPlayer.Score = historyPlayer.SeatNo == 0 ? score0 : score1;
            historyPlayer.OpponentScore = historyPlayer.SeatNo == 0 ? score1 : score0;
            historyPlayer.Result = string.IsNullOrEmpty(winner) ? (status == "Completed" ? "Draw" : "") : winner == historyPlayer.UserId ? "Win" : "Lose";
            historyEnded = true;
            historyStartRequest = null;
            try { if (historyPlayer.SeatNo == 0) BackendMatchHistory.Instance.RecordMatch(match); }
            catch (Exception e) { Debug.LogException(e); }
            try { BackendMatchHistory.Instance.RecordPlayer(historyPlayer); }
            catch (Exception e) { Debug.LogException(e); }
        }

        void AbortLegacyHistory(string reason)
        {
            if (historyOffer != null && !historyEnded)
            {
                if (reason == "Surrender") BackendMatchHistory.Instance.QueueSurrender(historyOffer.match.MatchId);
                else if (reason.StartsWith("Disconnected:") || reason == "StartTimeout" || reason == "RegistrationFailed")
                    BackendMatchHistory.Instance.StopHeartbeat(historyOffer.match.MatchId);
                int own = historyStarted ? PoolPlayer.mainPlayer?.hitCnt ?? 0 : 0;
                int other = historyStarted ? PoolPlayer.otherPlayer?.hitCnt ?? 0 : 0;
                EndLegacyHistory("Aborted", reason, "", historyPlayer.SeatNo == 0 ? own : other, historyPlayer.SeatNo == 0 ? other : own);
            }
            historyStartRequest = null;
        }

        void TickLegacyHistory()
        {
            if (PracticeMatchTestRoute.Enabled || (historyStarted && !historyEnded) ||
                (historyOffer == null && historyStartRequest == null) ||
                (historyEnded && historyStartRequest == null) || Time.realtimeSinceStartup < historyStartDeadline) return;
            AbortLegacyHistory("StartTimeout");
            CallNetworkState(NetworkState.JoinRoomFailed);
            if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();
        }

        void ResetLegacyHistory()
        {
            AbortLegacyHistory("RoomChanged");
            historyOffer = null; historyPlayer = null; historyStartRequest = null;
            historyStarted = historyEnded = false;
        }

        public override void OnMasterClientSwitched(Photon.Realtime.Player newMaster)
        {
            if (!LegacyHistoryEnabled || historyOffer == null || historyEnded || newMaster.ActorNumber == historyOffer.actor0) return;
            AbortLegacyHistory("AuthorityChanged");
            CallNetworkState(NetworkState.OpponentLeftRoom);
        }
    }
}
