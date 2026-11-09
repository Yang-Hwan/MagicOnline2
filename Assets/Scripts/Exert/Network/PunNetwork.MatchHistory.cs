using System;
using System.Linq;
using Assets.Scripts.Exert.BackSys;
using Assets.Scripts.Exert.Match;
using Assets.Scripts.Often;
using Assets.Scripts.Prack.BackSys.GameData;
using Photon.Pun;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace Assets.Scripts.Exert.Network
{
    public partial class PunNetwork
    {
        [Serializable] sealed class HistoryStartOffer
        {
            public string requestId, rulesKey, name0, name1;
            public int actor0, actor1;
            public int startingSeat = -1;
            public MatchHistoryData match;
        }
        HistoryStartOffer historyOffer;
        string friendlyHostWinner;
        UserMatchHistoryData historyPlayer;
        string historyStartRequest;
        bool historyStarted, historyEnded;
        ScoreCoinLedger scoreCoins;
        ScoreCoinLedger previewScoreCoins;
        ScoreCoinLedger DisplayedScoreCoins => previewScoreCoins ?? scoreCoins;
        public bool HasScoreCoins => HasLegacyMatch && scoreCoins != null;
        public long SelfCoinBalance => historyPlayer.SeatNo == 0 ? DisplayedScoreCoins.BalanceA : DisplayedScoreCoins.BalanceB;
        public long OtherCoinBalance => historyPlayer.SeatNo == 0 ? DisplayedScoreCoins.BalanceB : DisplayedScoreCoins.BalanceA;
        public long SelfCoinGain => historyPlayer.SeatNo == 0 ? DisplayedScoreCoins.NetA : -DisplayedScoreCoins.NetA;
        public string ScoringCoinEvents => scoreCoins?.Events ?? "";
        public int CoinWinnerPlayer => !HasScoreCoins || scoreCoins.BustWinner < 0 ? -1 :
            scoreCoins.BustWinner == historyPlayer.SeatNo ? PoolPlayer.mainPlayer.playerId : PoolPlayer.otherPlayer.playerId;
        public void AddScoringCoins(int playerId, int scoreBefore, int firstStreak, int points)
        {
            if (!HasScoreCoins || historyEnded || points <= 0 || scoreCoins.LastShot == legacyShotSerial || scoreCoins.BustWinner >= 0) return;
            points = Math.Min(points, Math.Max(0, historyOffer.match.Rules.TargetScore - scoreBefore));
            if (points == 0) return; // Cushion finish is not a scoring point.
            int seat = playerId == PoolPlayer.mainPlayer.playerId ? historyPlayer.SeatNo : 1 - historyPlayer.SeatNo;
            scoreCoins.Add(legacyShotSerial, seat, firstStreak, points);
            previewScoreCoins = null;
            BackendMatchHistory.Instance.QueueCoinCheckpoint(LegacyMatchId, scoreCoins.Events);
        }
        async System.Threading.Tasks.Task LoadScoreCoins(string id)
        {
            var state = await BackendMatchHistory.Instance.GetState(id);
            if (historyOffer?.match.MatchId != id || historyEnded) throw new InvalidOperationException("Match changed during coin setup.");
            if (historyStarted) return; // Duplicate commit must not reset an active round's ledger.
            if (state.Phase != "Playing" || state.Rules == null || !state.Rules.SameAs(historyOffer.match.Rules))
                throw new InvalidOperationException("경기 코인 정보를 확인할 수 없습니다.");
            scoreCoins = state.Rules.CoinPolicy == 2 ? new ScoreCoinLedger(state.StartCoinA, state.StartCoinB,
                state.Rules.EntryCoin, state.Rules.TargetScore) : null;
        }
        float historyStartDeadline;
        int seriesWinsA, seriesWinsB;
        bool seriesRematch;
        string seriesCountedMatch;
        int confirmedLoserSeat = -1;
        public long? ResultPrizeCoin { get; private set; }
        public string SeriesSelfWinsText => HasLegacyMatch && seriesRematch ?
            (historyPlayer.SeatNo == 0 ? seriesWinsA : seriesWinsB).ToString() : "";
        public string SeriesOtherWinsText => HasLegacyMatch && seriesRematch ?
            (historyPlayer.SeatNo == 0 ? seriesWinsB : seriesWinsA).ToString() : "";

        public string ResultSeriesScore => HasLegacyMatch ?
            (historyPlayer.SeatNo == 0 ? $"{seriesWinsA} : {seriesWinsB}" : $"{seriesWinsB} : {seriesWinsA}") : "0 : 0";
        public string ResultDurationText => HasLegacyMatch ?
            TimeSpan.FromSeconds(Math.Max(0, historyOffer.match.DurationSec)).ToString(@"mm\:ss") : "--:--";

        public void RecordConfirmedSeriesWin(BackendMatchHistory.State state)
        {
            var receipt = state?.Receipt;
            if (!HasLegacyMatch || state == null || state.Disputed || state.Phase != "Applied" || state.MatchId != LegacyMatchId ||
                receipt == null || receipt.MatchId != LegacyMatchId || seriesCountedMatch == LegacyMatchId ||
                receipt.UserA != historyOffer.match.PlayerAUserId || receipt.UserB != historyOffer.match.PlayerBUserId) return;
            if (receipt.ResultA == "Win" && receipt.ResultB == "Lose" && receipt.WinnerUserId == receipt.UserA)
            {
                ResultPrizeCoin = receipt.RewardA - receipt.EntryA;
                seriesWinsA++;
                confirmedLoserSeat = 1;
            }
            else if (receipt.ResultB == "Win" && receipt.ResultA == "Lose" && receipt.WinnerUserId == receipt.UserB)
            {
                ResultPrizeCoin = receipt.RewardB - receipt.EntryB;
                seriesWinsB++;
                confirmedLoserSeat = 0;
            }
            else if (receipt.ResultA != "Draw" || receipt.ResultB != "Draw") return;
            else { confirmedLoserSeat = -1; ResultPrizeCoin = 0; }
            seriesCountedMatch = LegacyMatchId;
        }
        int legacyShotSerial, receivedShotSerial;
        int lastCushionMotion;
        bool cushionMotionClosed;
        int sentimentMask;
        bool sentimentShotAuthority;
        public int LegacyLocalSeat => historyPlayer.SeatNo;
        public int LoungeLocalSeat => historyPlayer?.SeatNo ?? (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient ? 1 : 0);
        public int BeginLegacyShot()
        {
            legacyShotSerial++;
            sentimentMask=0;
            sentimentShotAuthority=PoolLogic.controlInNetwork;
            previewScoreCoins = null;
            lastCushionMotion=0;
            cushionMotionClosed=false;
            Assets.Scripts.Sight.Surface.Pavilion.CushionCountMotion.Hide();
            return legacyShotSerial;
        }

        int scoreMotionSerial = -1;

        public void PublishSentiment(int category,Vector3 shotPosition)
        {
            if(!HasLegacyMatch || historyEnded || !sentimentShotAuthority || category<1 || category>8)return;
            int seed=unchecked(legacyShotSerial*7919+category*104729);
            ApplySentiment(category,seed,shotPosition);
            view.RPC(nameof(SentimentMotionRPC),RpcTarget.Others,LegacyMatchId,legacyShotSerial,category,seed,shotPosition);
        }
        void ApplySentiment(int category,int seed,Vector3 shotPosition)
        {
            int bit=1<<category;
            if((sentimentMask&bit)!=0)return;
            var presenter=Assets.Scripts.Sight.Surface.Pavilion.MultiplayerSentimentController.Instance;
            if(!presenter || !presenter.isActiveAndEnabled)return;
            sentimentMask|=bit;
            presenter.Present(category,seed,shotPosition);
        }
        [PunRPC] async void SentimentMotionRPC(string id,int serial,int category,int seed,Vector3 shotPosition,PhotonMessageInfo info)
        {
            if(!HasLegacyMatch || historyEnded || id!=LegacyMatchId || category<1 || category>8 ||
                float.IsNaN(shotPosition.sqrMagnitude) || float.IsInfinity(shotPosition.sqrMagnitude) ||
                info.Sender.ActorNumber!=(historyPlayer.SeatNo==0 ? historyOffer.actor1 : historyOffer.actor0) ||
                serial<legacyShotSerial || serial>legacyShotSerial+1)return;
            float deadline=Time.realtimeSinceStartup+5;
            while(HasLegacyMatch && LegacyMatchId==id && !historyEnded && legacyShotSerial<serial &&
                Time.realtimeSinceStartup<deadline)await UniTask.Yield();
            if(!HasLegacyMatch || historyEnded || LegacyMatchId!=id || serial!=legacyShotSerial ||
                sentimentShotAuthority)return;
            ApplySentiment(category,seed,shotPosition);
        }

        // Presentation only. Remote scoring still uses the authoritative end-of-shot contacts.
        public void PresentThreeBallScore(int streak)
        {
            if (!HasLegacyMatch || historyEnded || historyOffer.match.MatchBall != 3 || streak < 1 ||
                scoreMotionSerial == legacyShotSerial) return;
            ApplyThreeBallScore(streak);
            if (PoolLogic.controlInNetwork)
                view.RPC(nameof(ThreeBallScoreMotionRPC), RpcTarget.Others, LegacyMatchId, legacyShotSerial, streak);
        }

        void ApplyThreeBallScore(int streak)
        {
            if (scoreMotionSerial == legacyShotSerial) return;
            var panel = Assets.Scripts.Sight.Surface.Pavilion.PnlMatch.Instance;
            if (!panel || !panel.isActiveAndEnabled) return;
            // Capture the displayed cue ball's position now; the label will not follow it.
            panel.ShowScoreFloat(streak);
            panel.ShowPointAvatar();
            scoreMotionSerial = legacyShotSerial;
        }

        [PunRPC] async void ThreeBallScoreMotionRPC(string id, int serial, int streak, PhotonMessageInfo info)
        {
            if (!HasLegacyMatch || historyEnded || id != LegacyMatchId || historyOffer.match.MatchBall != 3 ||
                streak < 1 || streak > 4096 ||
                info.Sender.ActorNumber != (historyPlayer.SeatNo == 0 ? historyOffer.actor1 : historyOffer.actor0) ||
                serial < legacyShotSerial || serial > legacyShotSerial + 1) return;
            float deadline = Time.realtimeSinceStartup + 5;
            while (HasLegacyMatch && LegacyMatchId == id && !historyEnded && legacyShotSerial < serial &&
                Time.realtimeSinceStartup < deadline) await UniTask.Yield();
            if (!HasLegacyMatch || historyEnded || id != LegacyMatchId || serial != legacyShotSerial ||
                !PoolLogic.controlFromNetwork) return;
            // Immediate opponent UI only; settlement still waits for contact replay.
            var scorer = PoolPlayer.otherPlayer;
            int points = Math.Min(streak - scorer.hitInning, Math.Max(0, historyOffer.match.Rules.TargetScore - scorer.hitCnt));
            if (HasScoreCoins && scoreCoins.LastShot < serial && scoreCoins.BustWinner < 0 && points > 0 && points <= 2)
            {
                try {
                    // Reconstruct initial balances from the current net movement.
                    long entry = historyOffer.match.Rules.EntryCoin;
                    previewScoreCoins = ScoreCoinLedger.Replay(scoreCoins.BalanceA - scoreCoins.NetA + entry,
                        scoreCoins.BalanceB + scoreCoins.NetA + entry, entry, historyOffer.match.Rules.TargetScore, scoreCoins.Events);
                    previewScoreCoins.Add(serial, 1 - historyPlayer.SeatNo, scorer.hitInning + 1, points);
                } catch (ArgumentException) { previewScoreCoins = null; }
            }
            ApplyThreeBallScore(streak);
        }

        public void PublishCushionMotion(int count)
        {
            if (!HasLegacyMatch || historyEnded || !PoolLogic.controlInNetwork || cushionMotionClosed) return;
            if (count > 0 && count <= lastCushionMotion) return;
            ApplyCushionMotion(count);
            view.RPC(nameof(CushionMotionRPC), RpcTarget.Others, LegacyMatchId, legacyShotSerial, count);
        }

        void ApplyCushionMotion(int count)
        {
            if (count == 0)
            {
                cushionMotionClosed=true;
                Assets.Scripts.Sight.Surface.Pavilion.CushionCountMotion.Hide();
            }
            else
            {
                lastCushionMotion=count;
                Assets.Scripts.Sight.Surface.Pavilion.CushionCountMotion.Show(count);
            }
        }

        [PunRPC] async void CushionMotionRPC(string id, int serial, int count, PhotonMessageInfo info)
        {
            if (!HasLegacyMatch || historyEnded || id != LegacyMatchId || count < 0 || count > 4096 ||
                info.Sender.ActorNumber != (historyPlayer.SeatNo == 0 ? historyOffer.actor1 : historyOffer.actor0) ||
                serial < legacyShotSerial || serial > legacyShotSerial + 1) return;
            float deadline=Time.realtimeSinceStartup+5;
            while (HasLegacyMatch && LegacyMatchId == id && !historyEnded && legacyShotSerial < serial &&
                Time.realtimeSinceStartup < deadline) await UniTask.Yield();
            if (!HasLegacyMatch || historyEnded || id != LegacyMatchId || serial != legacyShotSerial ||
                !PoolLogic.controlFromNetwork || cushionMotionClosed || (count > 0 && count <= lastCushionMotion)) return;
            ApplyCushionMotion(count);
        }
        public bool SendLegacyShotEnd(float seconds)
        {
            if (!HasLegacyMatch || !PoolLogic.controlInNetwork) return false;
            view.RPC(nameof(LegacyShotEndRPC), RpcTarget.Others, LegacyMatchId, legacyShotSerial,
                seconds, PoolCoach.Instance.ShotContacts);
            Debug.Log($"[ShotContacts] sent match={LegacyMatchId} shot={legacyShotSerial} contacts={PoolCoach.Instance.ShotContacts.Length}");
            return true;
        }

        [PunRPC] async void LegacyShotEndRPC(string id, int serial, float seconds, int[] contacts, PhotonMessageInfo info)
        {
            if (!HasLegacyMatch || historyEnded || id != LegacyMatchId ||
                info.Sender.ActorNumber != (historyPlayer.SeatNo == 0 ? historyOffer.actor1 : historyOffer.actor0) ||
                serial <= receivedShotSerial || serial < legacyShotSerial || serial > legacyShotSerial + 1 ||
                float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0 ||
                contacts == null || contacts.Length > 4096 ||
                contacts.Any(c => !(c >= 0 && c < historyOffer.match.MatchBall) && !(c >= 100 && c <= 103))) return;
            receivedShotSerial = serial;
            // The shot RPC may still be animating the cue. Do not stage contacts before its reset.
            while (HasLegacyMatch && LegacyMatchId == id && !historyEnded && legacyShotSerial < serial)
                await UniTask.Yield();
            if (!HasLegacyMatch || LegacyMatchId != id || historyEnded || legacyShotSerial != serial || !PoolLogic.controlFromNetwork) return;
            PoolCoach.Instance.StageRemoteShotContacts(contacts);
            ApplyCushionMotion(0);
            var physics = UnityEngine.Object.FindFirstObjectByType<Assets.Scripts.Sight.Vital.Pavilion.PhysicsMng>();
            if (physics) physics.WaitAndStopMoveFromNetwork(seconds).Forget();
            Debug.Log($"[ShotContacts] received match={id} shot={serial} contacts={contacts.Length}");
        }
        double matchDeadline;
        bool timeoutPreparing, timeoutReady, peerTimeoutReady, timeoutCommitted;
        public bool HasLegacyMatch => LegacyHistoryEnabled && historyStarted && historyOffer != null;
        public bool BlockLegacyShots => HasLegacyMatch && (timeoutPreparing || historyEnded);
        public string BallSkinMatchId => historyOffer?.match?.MatchId;
        public string LegacyMatchId => HasLegacyMatch ? historyOffer.match.MatchId : null;
        public string ConfirmedResultText { get; set; }
        public double LegacySecondsRemaining => matchDeadline > 0 ? Math.Max(0, matchDeadline - PhotonNetwork.Time) : -1;

        // Only the original host starts the clock, after the opening scatter.
        public void StartLegacyClock(int seconds)
        {
            if (!HasLegacyMatch || historyEnded || matchDeadline > 0 || historyPlayer.SeatNo != 0) return;
            matchDeadline = PhotonNetwork.Time + seconds;
            view.RPC(nameof(HistoryClockRPC), RpcTarget.Others, LegacyMatchId, matchDeadline);
        }

        [PunRPC] void HistoryClockRPC(string id, double deadline, PhotonMessageInfo info)
        {
            if (!HasLegacyMatch || historyEnded || id != LegacyMatchId || info.Sender.ActorNumber != historyOffer.actor0 ||
                matchDeadline > 0 || double.IsNaN(deadline) || double.IsInfinity(deadline)) return;
            matchDeadline = deadline;
        }

        [PunRPC] void HistoryTimeoutPrepareRPC(string id, PhotonMessageInfo info)
        {
            if (!HasLegacyMatch || historyEnded || id != LegacyMatchId || info.Sender.ActorNumber != historyOffer.actor0) return;
            timeoutPreparing = true;
        }

        [PunRPC] void HistoryTimeoutReadyRPC(string id, PhotonMessageInfo info)
        {
            if (!HasLegacyMatch || historyEnded || !timeoutPreparing || id != LegacyMatchId ||
                historyPlayer.SeatNo != 0 || info.Sender.ActorNumber != historyOffer.actor1) return;
            peerTimeoutReady = true;
        }

        [PunRPC] void HistoryTimeoutCommitRPC(string id, PhotonMessageInfo info)
        {
            if (!HasLegacyMatch || historyEnded || !timeoutPreparing || id != LegacyMatchId ||
                info.Sender.ActorNumber != historyOffer.actor0 || timeoutCommitted) return;
            timeoutCommitted = true;
        }

        void TickLegacyTimeout()
        {
            if (!HasLegacyMatch || historyEnded || !HistoryPeerPresent) return;
            var panel = Assets.Scripts.Sight.Surface.Pavilion.PnlMatch.Instance;
            if (!panel || !panel.isActiveAndEnabled) return;
            if (timeoutCommitted)
            {
                PoolCoach.Instance.SetCalculateTimeEnable(false);
                if (!panel.LegacyShotBusy) PoolCoach.Instance.CallMatchTimeout();
                return;
            }
            if (!timeoutPreparing && historyPlayer.SeatNo == 0 && matchDeadline > 0 && PhotonNetwork.Time >= matchDeadline)
            {
                timeoutPreparing = true;
                view.RPC(nameof(HistoryTimeoutPrepareRPC), RpcTarget.Others, LegacyMatchId);
            }
            if (!timeoutPreparing) return;
            PoolCoach.Instance.SetCalculateTimeEnable(false);
            // Ready is sent AFTER any accepted stroke's shot RPC and local scoring.
            // All of these RPCs use the same reliable channel, so commit cannot overtake a shot.
            if (!timeoutReady && !panel.LegacyShotBusy)
            {
                timeoutReady = true;
                if (historyPlayer.SeatNo == 1)
                    view.RPC(nameof(HistoryTimeoutReadyRPC), PhotonNetwork.MasterClient, LegacyMatchId);
            }
            if (historyPlayer.SeatNo == 0 && timeoutReady && peerTimeoutReady && !panel.LegacyShotBusy)
            {
                timeoutCommitted = true;
                view.RPC(nameof(HistoryTimeoutCommitRPC), RpcTarget.Others, LegacyMatchId);
                Debug.Log("[MatchTimeout] coordinated commit match=" + LegacyMatchId);
                PoolCoach.Instance.CallMatchTimeout();
            }
        }
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
                            TargetHit = hall.TargetHit, MatchTotMin = hall.MatchTotMin, MatchCushion = hall.MatchCushion,
                            FinishMission = hall.FinishMission, PrizeCoin = hall.PrizeCoin, MaxCoin = hall.MaxCoin
                        })
                    }
                };
                if (IsFriendly)
                {
                    offer.match.MatchMode = "Friendly";
                    offer.match.Rules.CoinPolicy = 0;
                    offer.match.Rules.EntryCoin = offer.match.Rules.MaxCoin = 0;
                    if (other.uuid != friendlyOpponent) throw new InvalidOperationException("친구 경기 상대가 일치하지 않습니다.");
                }
                bool rematch = IsSameSeriesRematch(offer);
                // Only the master draws; the offer carries the decision to both clients.
                offer.startingSeat = MatchmakingCompatibility.SelectStartingSeat(
                    rematch && seriesCountedMatch == LegacyMatchId, confirmedLoserSeat,
                    UnityEngine.Random.Range(0, 2));
                InstallHistoryOffer(offer, 0);
                if (IsFriendly)
                {
                    PhotonNetwork.CurrentRoom.IsOpen = false;
                    view.RPC(nameof(HistoryOfferRPC), opponentPlayer, JsonUtility.ToJson(offer));
                    return;
                }
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

        bool IsSameSeriesRematch(HistoryStartOffer offer) =>
                historyOffer != null && historyEnded && PoolPlayer.IsReMatch() &&
                historyOffer.match.PlayerAUserId == offer.match.PlayerAUserId &&
                historyOffer.match.PlayerBUserId == offer.match.PlayerBUserId;

        void InstallHistoryOffer(HistoryStartOffer offer, int seat)
        {
            seriesRematch = IsSameSeriesRematch(offer);
            if (!seriesRematch) { seriesWinsA = seriesWinsB = 0; seriesCountedMatch = null; }
            confirmedLoserSeat = -1;
            historyOffer = offer; historyStarted = historyEnded = false;
            PoolCoach.Instance.ApplyBallSkin(offer.match.MatchId);
            scoreCoins = previewScoreCoins = null;
            legacyShotSerial = receivedShotSerial = 0;
            scoreMotionSerial = -1;
            matchDeadline = 0;
            timeoutPreparing = timeoutReady = peerTimeoutReady = timeoutCommitted = false;
            ConfirmedResultText = null;
            friendlyHostWinner = null;
            ResultPrizeCoin = null;
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
                    (offer.startingSeat != 0 && offer.startingSeat != 1) ||
                    !Guid.TryParseExact(offer.match.MatchId, "N", out _) ||
                    offer.actor0 != info.Sender.ActorNumber || offer.actor1 != PhotonNetwork.LocalPlayer.ActorNumber ||
                    offer.match.PlayerBUserId != GameApiClient.UserId || offer.match.PlayerAUserId != NetworkManager.opponentPlayer?.uuid ||
                    offer.match.HallIdx != hall.HallIdx || offer.match.MatchBall != hall.MatchBall ||
                    offer.rulesKey != MatchmakingCompatibility.RulesKey(hall)) return;
                if (IsFriendly && (offer.match.MatchMode != "Friendly" || offer.match.PlayerAUserId != friendlyOpponent ||
                    offer.match.Rules == null || offer.match.Rules.CoinPolicy != 0 || offer.match.Rules.EntryCoin != 0)) return;
                if (historyOffer != null && !historyEnded) return;
                InstallHistoryOffer(offer, 1);
                // Readiness claims come from our own runtime/chart, never the peer's contract.
                if (!IsFriendly) await BackendMatchHistory.Instance.ReadyAsync(offer.match.MatchId, MatchRulesContract.FromHall(hall, MatchRulesContract.Pavilion));
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
                if (!IsFriendly)
                {
                    await BackendMatchHistory.Instance.WaitPlayingAsync(matchId);
                    await LoadScoreCoins(matchId);
                }
                if (historyEnded || historyStarted || historyOffer?.match.MatchId != matchId || !HistoryPeerPresent) return;
                long startedAt = BackendMatchHistory.Now;
                view.RPC(nameof(HistoryCommitRPC), info.Sender, matchId, requestId, startedAt);
                CommitLegacyHistory(startedAt);
            }
            catch (Exception e) { Debug.LogWarning(e.Message); AbortLegacyHistory("ServerStartFailed"); CallNetworkState(NetworkState.JoinRoomFailed); }
        }

        [PunRPC] async void HistoryCommitRPC(string matchId, string requestId, long startedAt, PhotonMessageInfo info)
        {
            if (!LegacyHistoryEnabled || !HistoryPeerPresent || historyEnded || historyStarted || startedAt <= 0 ||
                info.Sender.ActorNumber != historyOffer.actor0 || matchId != historyOffer.match.MatchId ||
                requestId != historyStartRequest || requestId != historyOffer.requestId) return;
            try {
                if (!IsFriendly) await LoadScoreCoins(matchId);
                if (historyEnded || historyStarted || historyOffer?.match.MatchId != matchId || !HistoryPeerPresent) return;
                CommitLegacyHistory(startedAt);
            } catch (Exception e) { Debug.LogWarning(e.Message); AbortLegacyHistory("CoinSetupFailed"); CallNetworkState(NetworkState.JoinRoomFailed); }
        }

        void CommitLegacyHistory(long startedAt)
        {
            historyStarted = true;
            if (!IsFriendly) BackendMatchHistory.Instance.Track(historyOffer.match.MatchId);
            historyOffer.match.StartedAt = startedAt; historyOffer.match.Status = "Playing";
            try {
                if (!IsFriendly)
                {
                    if (historyPlayer.SeatNo == 0) BackendMatchHistory.Instance.RecordMatch(historyOffer.match);
                    BackendMatchHistory.Instance.RecordPlayer(historyPlayer);
                }
            }
            catch (Exception e) { Debug.LogException(e); AbortLegacyHistory("StartSaveFailed"); CallNetworkState(NetworkState.JoinRoomFailed); return; }
            // Seat numbers remain stable for results; local player IDs are self=0/other=1.
            PoolPlayer.SetIdx(historyPlayer.SeatNo == historyOffer.startingSeat ? 0 : 1);
            Debug.Log($"[MatchStart] match={LegacyMatchId} startingSeat={historyOffer.startingSeat} localSeat={historyPlayer.SeatNo} rematch={seriesRematch}");
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
            if (!LegacyHistoryEnabled || historyOffer == null || !historyStarted || matchId != historyOffer.match.MatchId ||
                info.Sender.ActorNumber != (historyPlayer.SeatNo == 0 ? historyOffer.actor1 : historyOffer.actor0) ||
                (winner != "" && winner != historyPlayer.UserId && winner != historyPlayer.OpponentUserId) || score0 < 0 || score1 < 0) return;
            if (IsFriendly && historyPlayer.SeatNo == 1 && friendlyHostWinner == null)
            {
                friendlyHostWinner = winner;
                if (historyEnded) DisplayFriendlyResult(winner);
                return;
            }
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
            if (IsFriendly)
            {
                if (reason.StartsWith("Opponent") || reason == "AuthorityChanged") DisplayFriendlyResult(historyPlayer.UserId);
                else if (historyPlayer.SeatNo == 0) DisplayFriendlyResult(winner);
                else if (friendlyHostWinner != null) DisplayFriendlyResult(friendlyHostWinner);
                return;
            }
            try { if (historyPlayer.SeatNo == 0) BackendMatchHistory.Instance.RecordMatch(match); }
            catch (Exception e) { Debug.LogException(e); }
            try { BackendMatchHistory.Instance.RecordPlayer(historyPlayer); }
            catch (Exception e) { Debug.LogException(e); }
        }

        void DisplayFriendlyResult(string winner)
        {
            if (seriesCountedMatch == historyOffer.match.MatchId) return;
            ConfirmedResultText = winner == "" ? "비겼습니다" : winner == historyPlayer.UserId ? "승리했습니다" : "패배하였습니다";
            if (winner != "")
            {
                confirmedLoserSeat = winner == historyOffer.match.PlayerAUserId ? 1 : 0;
                if (confirmedLoserSeat == 1) seriesWinsA++; else seriesWinsB++;
            }
            seriesCountedMatch = historyOffer.match.MatchId;
        }

        void AbortLegacyHistory(string reason)
        {
            if (historyOffer != null && !historyEnded)
            {
                if (!IsFriendly && reason == "Surrender") BackendMatchHistory.Instance.QueueSurrender(historyOffer.match.MatchId);
                else if (!IsFriendly && (reason.StartsWith("Disconnected:") || reason == "StartTimeout" || reason == "RegistrationFailed"))
                    BackendMatchHistory.Instance.StopHeartbeat(historyOffer.match.MatchId);
                int own = historyStarted ? PoolPlayer.mainPlayer?.hitCnt ?? 0 : 0;
                int other = historyStarted ? PoolPlayer.otherPlayer?.hitCnt ?? 0 : 0;
                EndLegacyHistory("Aborted", reason, "", historyPlayer.SeatNo == 0 ? own : other, historyPlayer.SeatNo == 0 ? other : own);
            }
            historyStartRequest = null;
        }

        void TickLegacyHistory()
        {
            TickLegacyTimeout();
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
            seriesWinsA = seriesWinsB = 0; seriesRematch = false; seriesCountedMatch = null;
            confirmedLoserSeat = -1;
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
