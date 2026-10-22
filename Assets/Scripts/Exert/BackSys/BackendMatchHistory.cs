using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Assets.Scripts.Prack.BackSys;
using Assets.Scripts.Prack.BackSys.GameData;
using UnityEngine;

namespace Assets.Scripts.Exert.BackSys
{
    // Scene-compatible name; persistence is exclusively through the authenticated local API.
    public sealed class BackendMatchHistory : MonoBehaviour
    {
        [Serializable] sealed class Registration { public string RequestId, OpponentUserId; public int HallId; public MatchRulesContract Rules; }
        [Serializable] sealed class ReadyBody { public MatchRulesContract Rules; }
        [Serializable] public sealed class ResultBody { public int ScoreA, ScoreB, WinnerSeat; }
        [Serializable] public sealed class Receipt { public string MatchId, Reason, WinnerUserId; public long RewardA, RewardB; }
        [Serializable] public sealed class State { public string MatchId, Phase; public bool Disputed, ReadyA, ReadyB; public MatchRulesContract Rules; public Receipt Receipt; }
        [Serializable] sealed class ReadyReply { public bool Playing; }
        [Serializable] public sealed class PendingWrite { public string MatchId; public ResultBody Report; public bool Surrender; }
        [Serializable] public sealed class Journal
        {
            public int Version = 1;
            public long Generation;
            public string Checksum;
            public List<PendingWrite> Writes = new List<PendingWrite>();
        }
        static BackendMatchHistory instance;
        public static BackendMatchHistory Instance
        {
            get { if (!instance) { var go = new GameObject("GameApiMatchHistory"); DontDestroyOnLoad(go); instance = go.AddComponent<BackendMatchHistory>(); } return instance; }
        }
        public static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        public static long Expires(long endedAt) => DateTimeOffset.FromUnixTimeMilliseconds(endedAt).AddMonths(6).ToUnixTimeMilliseconds();
        public string LastStatus { get; private set; } = "";
        public string LastMatchId { get; private set; } = "";
        public string LastStartError { get; private set; } = "";
        public void ClearStartError() => LastStartError = "";
        Journal journal = new Journal();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Local integration tests can terminate after durable enqueue, before any send.
        public bool PauseWritesForDevelopmentTest { get; set; }
        public int PendingWritesForDevelopmentTest => LoadOwner() ? journal.Writes.Count : -1;
        public string JournalPathForDevelopmentTest => LoadOwner() ? path : null;
#endif
        readonly HashSet<string> active = new HashSet<string>();
        readonly HashSet<string> applied = new HashSet<string>();
        public bool IsSettled(string id) => owner == GameApiClient.UserId && !string.IsNullOrEmpty(id) && applied.Contains(id);
        string owner, path; bool busy; float nextTick;
        bool LoadOwner()
        {
            var current = GameApiClient.UserId;
            if (string.IsNullOrEmpty(current)) return false;
            if (current == owner) return true;
            if (busy) return false;
            byte[] hash;
            using (var sha = SHA256.Create()) hash = sha.ComputeHash(Encoding.UTF8.GetBytes(GameApiClient.BaseUrl + "|" + current));
            var newPath = Path.Combine(Application.persistentDataPath, "ApiResults-" + BitConverter.ToString(hash).Replace("-", "") + ".json");
            var loaded = MatchResultJournalStore.Load(newPath, out var recovery);
            owner = current; path = newPath; journal = loaded;
            active.Clear(); applied.Clear(); LastStatus = recovery; LastMatchId = "";
            if (!string.IsNullOrEmpty(recovery)) Debug.LogWarning(recovery);
            return true;
        }
        void Persist()
        {
            MatchResultJournalStore.Save(path, journal);
        }
        public async Task RegisterAsync(MatchHistoryData match)
        {
            if (!LoadOwner() || match.PlayerAUserId != owner) throw new InvalidOperationException("Invalid match owner");
            LastStartError = ""; LastMatchId = match.MatchId;
            try
            {
                await GameApiClient.Request<object>("/api/matches", "POST", new Registration { RequestId = match.MatchId, OpponentUserId = match.PlayerBUserId, HallId = match.HallIdx, Rules = match.Rules });
                await ReadyAsync(match.MatchId, match.Rules);
            }
            catch (GameApiException e) { LastStartError = e.UserMessage; throw; }
        }
        public async Task<bool> ReadyAsync(string id, MatchRulesContract rules)
        {
            LastStartError = ""; LastMatchId = id;
            try
            {
                var reply = await GameApiClient.Request<ReadyReply>("/api/matches/" + id + "/ready", "POST", new ReadyBody { Rules = rules });
                return reply.Playing;
            }
            catch (GameApiException e) { LastStartError = e.UserMessage; throw; }
        }
        public async Task WaitPlayingAsync(string id)
        {
            for (int i=0; i<25; i++)
            {
                try { if ((await GetState(id)).Phase == "Playing") { Track(id); return; } }
                catch (GameApiException e) when(e.Status == 404) { }
                await Task.Delay(400);
            }
            throw new InvalidOperationException("서버의 경기 시작 승인이 지연되었습니다.");
        }
        public async Task<State> GetState(string id) => await GameApiClient.Request<State>("/api/matches/" + id);
        public void Track(string id) { LoadOwner(); active.Add(id); LastMatchId = id; LastStatus = "경기 진행 중"; }
        public void StopHeartbeat(string id) { active.Remove(id); }
        public void QueueSurrender(string id)
        {
            if (!LoadOwner()) return;
            active.Remove(id);
            if (journal.Writes.Find(w=>w.MatchId == id) == null)
                journal.Writes.Add(new PendingWrite { MatchId = id, Surrender = true });
            Persist(); nextTick = 0;
        }
        public async Task SurrenderAsync(string id)
        {
            await GameApiClient.Request<object>("/api/matches/"+id+"/surrender", "POST"); active.Remove(id);
            await BackendGame.Instance.LoadAsync();
        }
        public async void RecordMatch(MatchHistoryData match, Action onSaved = null, Action<Exception> onFailed = null)
        {
            try
            {
                if (match.Status == "Preparing") await RegisterAsync(match);
                else if (match.Status == "Playing") Track(match.MatchId);
                onSaved?.Invoke();
            }
            catch(Exception e) { LastStatus = "서버 저장 실패: " + (e is GameApiException api ? api.UserMessage : e.Message); Debug.LogWarning(LastStatus); onFailed?.Invoke(e); }
        }
        public void RecordPlayer(UserMatchHistoryData player)
        {
            if (!LoadOwner() || player.UserId != owner) throw new InvalidOperationException("Invalid report owner");
            LastMatchId = player.MatchId;
            if (string.IsNullOrEmpty(player.Result)) return;
            var report = new ResultBody { ScoreA = player.SeatNo == 0 ? player.Score : player.OpponentScore,
                ScoreB = player.SeatNo == 0 ? player.OpponentScore : player.Score,
                WinnerSeat = player.Result == "Draw" ? -1 : player.Result == "Win" ? player.SeatNo : 1 - player.SeatNo };
            var existing = journal.Writes.Find(w=>w.MatchId == player.MatchId);
            if(existing == null) journal.Writes.Add(new PendingWrite { MatchId = player.MatchId, Report = report });
            Persist(); LastStatus = "정산 대기 중"; nextTick = 0;
        }
        async void Update()
        {
            if (busy || Time.realtimeSinceStartup < nextTick || string.IsNullOrEmpty(GameApiClient.UserId)) return;
            busy = true;
            try
            {
                // Account changes are applied only between requests; never send an old journal as another user.
                busy = false; if (!LoadOwner()) return; busy = true;
                string current = owner;
                foreach(var id in new List<string>(active))
                {
                    // A locally disconnected Photon participant must not keep itself alive via HTTP.
                    if (!Photon.Pun.PhotonNetwork.IsConnectedAndReady) { active.Remove(id); continue; }
                    try
                    {
                        await GameApiClient.Request<object>("/api/matches/"+id+"/heartbeat", "POST");
                        if(current != GameApiClient.UserId) return;
                        await Observe(id);
                    }
                    catch (GameApiException e) when (e.Status == 403 || e.Status == 404)
                    {
                        active.Remove(id);
                        LastStatus = "경기 상태 확인 필요: " + e.Message;
                    }
                }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (PauseWritesForDevelopmentTest) return;
#endif
                foreach(var write in new List<PendingWrite>(journal.Writes))
                {
                    if(current != GameApiClient.UserId) return;
                    try
                    {
                        // Reconnection may find a timeout receipt already applied by the server.
                        var state = await Observe(write.MatchId);
                        if(current != GameApiClient.UserId) return;
                        if(state.Phase != "Applied" && !state.Disputed)
                        {
                            await GameApiClient.Request<object>("/api/matches/"+write.MatchId+(write.Surrender ? "/surrender" : "/result"), "POST",write.Report);
                            if(current != GameApiClient.UserId) return;
                            state = await Observe(write.MatchId);
                        }
                        if(current != GameApiClient.UserId) return;
                        if(state.Phase == "Applied") { journal.Writes.Remove(write); Persist(); }
                    }
                    catch (GameApiException e) when (e.Status == 400 || e.Status == 403 || e.Status == 404 || e.Status == 409)
                    {
                        // Preserve the request for review without blocking unrelated valid results.
                        LastStatus = "결과 확인 필요 (" + write.MatchId + "): " + e.Message;
                    }
                }
            }
            catch(Exception e) { LastStatus = "저장 재시도 대기: " + e.Message; }
            finally { busy = false; nextTick = Time.realtimeSinceStartup + 4; }
        }
        async Task<State> Observe(string id)
        {
            var state = await GetState(id);
            if(state.Phase == "Applied") { active.Remove(id); applied.Add(id); LastStatus = "정산 완료"; await BackendGame.Instance.LoadAsync(); }
            else if(state.Disputed) LastStatus = "결과 불일치: 확인 필요";
            return state;
        }
    }
}
