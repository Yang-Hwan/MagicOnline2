using System;
using Assets.Scripts.Often;
using Assets.Scripts.Prack.BackSys.ChartData;
using Assets.Scripts.Prack.BackSys.GameData;
using UnityEngine;

namespace Assets.Scripts.Exert.BackSys
{
    // Photon results are client reports. Only a server may apply wallet/stat changes.
    public sealed class MatchHistoryReporter : IDisposable
    {
        readonly MatchStartSession session;
        readonly MatchResultSession results;
        readonly MatchHistoryData match;
        readonly UserMatchHistoryData player;
        readonly Action registered;
        bool started, ended;
        public bool Registered { get; private set; }

        public MatchHistoryReporter(MatchStartSession session, MatchResultSession results,
            SkillMatchData hall, string user0, string user1, Action registered)
        {
            this.session = session; this.results = results; this.registered = registered;
            var config = session.Configuration;
            var rules = MatchRulesContract.FromHall(hall, MatchRulesContract.Practice);
            rules.Ball = config.BallCount; rules.HallId = config.HallId; rules.TargetScore = config.TargetScore;
            rules.Cushions = config.CushionCount; rules.MatchSeconds = config.MatchSeconds; rules.TurnSeconds = config.TurnSeconds;
            match = new MatchHistoryData {
                MatchId = session.MatchId, MatchBall = config.BallCount, HallIdx = config.HallId,
                Rules = rules,
                PlayerAUserId = user0, PlayerBUserId = user1,
                RulesSnapshot = JsonUtility.ToJson(new MatchRulesSnapshot {
                    HallIdx = hall.HallIdx, HallName = hall.HallName, MatchBall = config.BallCount,
                    TargetHit = config.TargetScore, MatchTotMin = config.MatchSeconds / 60,
                    MatchCushion = config.CushionCount, FinishMission = hall.FinishMission,
                    PrizeCoin = hall.PrizeCoin, MaxCoin = hall.MaxCoin, ReconnectGraceSec = 30
                })
            };
            bool seat0 = config.LocalSeat == 0;
            player = new UserMatchHistoryData {
                MatchId = session.MatchId, UserId = seat0 ? user0 : user1,
                OpponentUserId = seat0 ? user1 : user0, SeatNo = config.LocalSeat,
                MatchBall = config.BallCount, HallIdx = config.HallId,
                NicknameSnapshot = seat0 ? config.Seat0Name : config.Seat1Name,
                OpponentNicknameSnapshot = seat0 ? config.Seat1Name : config.Seat0Name
            };
            session.Changed += OnChanged;
            results.Applied += OnResult;
        }

        public async void Prepare()
        {
            try
            {
                if (session.LocalActor == session.AuthorityActor)
                    await BackendMatchHistory.Instance.RegisterAsync(match);
                else
                {
                    for (int i = 0; ; i++)
                    {
                        try { await BackendMatchHistory.Instance.ReadyAsync(match.MatchId, match.Rules); break; }
                        catch (GameApiException e) when (e.Status == 404 && i < 24) { await System.Threading.Tasks.Task.Delay(400); }
                    }
                }
                await BackendMatchHistory.Instance.WaitPlayingAsync(match.MatchId);
                Registered = true;
                registered?.Invoke();
            }
            catch (Exception e) { var message = e is GameApiException api ? api.UserMessage : "서버에서 경기 시작을 승인하지 못했습니다."; Debug.LogWarning(message); session.Abort(message); }
        }

        void OnChanged()
        {
            try
            {
                if (session.Phase == MatchStartPhase.Started && !ended) BackendMatchHistory.Instance.Track(match.MatchId);
                if (session.Phase == MatchStartPhase.Started && !started)
                {
                    started = true; match.StartedAt = BackendMatchHistory.Now; match.Status = "Playing";
                    BackendMatchHistory.Instance.Track(match.MatchId);
                    SaveShared(); BackendMatchHistory.Instance.RecordPlayer(player);
                }
                else if (session.Phase == MatchStartPhase.Aborted && !ended)
                    End(false);
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        void OnResult(MatchResultMessage message)
        {
            if (!results.Finished || ended) return;
            try { End(true, message.kind == MatchResultKind.MatchTimeout ? "MatchTimeout" : "TargetScore"); }
            catch (Exception e) { Debug.LogException(e); }
        }

        void End(bool completed, string reason = null)
        {
            // A disk failure during the first end report must not turn an already
            // finished match into an abort when the scene subsequently closes.
            if (results.Finished && !completed)
            {
                completed = true;
                reason = string.IsNullOrEmpty(match.EndReason) ? "Finished" : match.EndReason;
            }
            match.EndedAt = BackendMatchHistory.Now;
            match.DurationSec = started ? (int)Math.Max(0, (match.EndedAt - match.StartedAt) / 1000) : 0;
            match.HistoryExpiresAt = BackendMatchHistory.Expires(match.EndedAt);
            match.Status = completed ? "Completed" : "Aborted";
            match.EndReason = reason ?? session.AbortReason ?? "Cancelled";
            match.WinnerUserId = completed && results.Winner >= 0 ?
                (results.Winner == 0 ? match.PlayerAUserId : match.PlayerBUserId) : "";
            player.EndedAt = match.EndedAt; player.HistoryExpiresAt = match.HistoryExpiresAt;
            player.Score = player.SeatNo == 0 ? results.Score0 : results.Score1;
            player.OpponentScore = player.SeatNo == 0 ? results.Score1 : results.Score0;
            // Disconnections need server validation; do not invent a final forfeit result.
            player.Result = completed ? (results.Winner == -2 ? "Draw" : results.Winner == player.SeatNo ? "Win" : "Lose") : "";
            SaveShared();
            if (started) BackendMatchHistory.Instance.RecordPlayer(player);
            ended = true;
        }

        void SaveShared()
        {
            if (session.LocalActor == session.AuthorityActor)
                BackendMatchHistory.Instance.RecordMatch(match);
        }

        public void Dispose()
        {
            session.Changed -= OnChanged;
            results.Applied -= OnResult;
        }
    }
}
