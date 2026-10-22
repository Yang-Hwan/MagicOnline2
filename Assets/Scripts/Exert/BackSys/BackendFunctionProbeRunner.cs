#if BACKND_LEGACY_DIAGNOSTICS
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using BackEnd;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Prack.BackSys
{
    // Add explicitly to a diagnostic GameObject. Never called by ordinary match flow.
    public sealed class BackendFunctionProbeRunner : MonoBehaviour
    {
        public string FunctionName = "MatchSettlementProbe";
        public string RunId;
        public bool UsePrivateTable;
        public string OtherUserInDate;
        public string OtherPrimaryRowId;
        public long ExpectedVersion;
        [Range(2, 8)] public int ConcurrentRequests = 4;
        readonly ConcurrentQueue<string> output = new ConcurrentQueue<string>();
        int pending;
        public bool ShowWindow;
        public string LastResult = "No request sent. Log in first, then click Health.";
        Vector2 resultScroll;
        readonly List<GraphicRaycaster> blockedRaycasters = new List<GraphicRaycaster>();
        string blockedScene;
        GUISkin probeSkin;

        void Update()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            bool shouldBlock = ShowWindow && scene.name != "Come" && scene.name != "Arise";
            if (!shouldBlock || blockedScene != scene.name) RestoreGameInput();
            if (shouldBlock && blockedScene != scene.name)
            {
                blockedScene = scene.name;
                foreach (var raycaster in UnityEngine.Object.FindObjectsByType<GraphicRaycaster>(FindObjectsSortMode.None))
                    if (raycaster.enabled) { blockedRaycasters.Add(raycaster); raycaster.enabled = false; }
            }
            while (output.TryDequeue(out var line)) { LastResult = line; UnityEngine.Debug.Log(line); }
        }
        void RestoreGameInput()
        {
            foreach (var raycaster in blockedRaycasters) if (raycaster != null) raycaster.enabled = true;
            blockedRaycasters.Clear(); blockedScene = null;
        }
        void OnDisable() { RestoreGameInput(); }
        void OnDestroy() { RestoreGameInput(); if (probeSkin != null) Destroy(probeSkin); }

        void OnGUI()
        {
            if (!ShowWindow) return;
            var oldSkin = GUI.skin;
            if (probeSkin == null)
            {
                probeSkin = Instantiate(GUI.skin);
                probeSkin.label.fontSize = probeSkin.button.fontSize = probeSkin.textField.fontSize = probeSkin.textArea.fontSize = probeSkin.toggle.fontSize = 16;
            }
            GUI.skin = probeSkin;
            float scale = Mathf.Min(1f, Screen.width / 760f, Screen.height / 660f);
            var old = GUI.matrix; GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            var oldColor = GUI.color; GUI.color = new Color(0.05f, 0.05f, 0.05f, 1f);
            GUI.DrawTexture(new Rect(10, 10, 740, 640), Texture2D.whiteTexture); GUI.color = oldColor;
            GUILayout.BeginArea(new Rect(10, 10, 740, 640), GUI.skin.box);
            GUILayout.Label("BACKND FUNCTION PROBE - test tables only (no game coins)");
            GUILayout.Label("Scene: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name +
                " | initialized=" + Backend.IsInitialized + " | loggedIn=" + !string.IsNullOrWhiteSpace(Backend.UserInDate) + " | pending=" + pending);
            GUILayout.Label("Function name"); FunctionName = GUILayout.TextField(FunctionName ?? "");
            GUILayout.Label("Run ID (same value on both test accounts)"); RunId = GUILayout.TextField(RunId ?? "");
            UsePrivateTable = GUILayout.Toggle(UsePrivateTable, "Use Private test table (unchecked = Public)");
            GUILayout.Label("Other test account UserInDate / PrimaryRowId");
            OtherUserInDate = GUILayout.TextField(OtherUserInDate ?? ""); OtherPrimaryRowId = GUILayout.TextField(OtherPrimaryRowId ?? "");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("1 Generate Run ID", GUILayout.Height(32))) GenerateRunId();
            if (GUILayout.Button("2 Health", GUILayout.Height(32))) Health();
            GUILayout.EndHorizontal();
            GUI.enabled = pending == 0;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("3 Seed", GUILayout.Height(32))) Seed();
            if (GUILayout.Button("4 Read self", GUILayout.Height(32))) ReadSelf();
            if (GUILayout.Button("5 Write self", GUILayout.Height(32))) WriteSelf();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("6 Read peer", GUILayout.Height(32))) ReadPeer();
            if (GUILayout.Button("7 Write pair", GUILayout.Height(32))) WritePair();
            if (GUILayout.Button("8 Rollback", GUILayout.Height(32))) Rollback();
            GUILayout.EndHorizontal();
            GUILayout.Label("Expected version: " + ExpectedVersion + " | Concurrent requests: " + ConcurrentRequests);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("9 Concurrent requests", GUILayout.Height(32))) ConcurrentConditionalWrites();
            if (GUILayout.Button("10 Retry old version", GUILayout.Height(32))) RetryOldVersion();
            GUILayout.EndHorizontal();
            if (GUILayout.Button("11 Atomic pair + match state suite", GUILayout.Height(32))) Send("ProbeAtomicPairSuite", true);
            GUI.enabled = true;
            GUILayout.Label("Latest result (full request results are in Unity Console)");
            resultScroll = GUILayout.BeginScrollView(resultScroll, GUILayout.Height(130));
            GUILayout.TextArea(LastResult ?? ""); GUILayout.EndScrollView();
            if (GUILayout.Button("Hide probe window")) ShowWindow = false;
            GUILayout.EndArea(); GUI.matrix = old; GUI.skin = oldSkin;
        }

        [ContextMenu("Probe/1 Generate Run ID (share with other account)")]
        public void GenerateRunId() { RunId = Guid.NewGuid().ToString("N"); LastResult = "Generated RunId=" + RunId + ". No server data written."; UnityEngine.Debug.Log("[FunctionProbe] RunId=" + RunId); }
        [ContextMenu("Probe/2 Health (measure first and later calls)")]
        public void Health() { Invoke(new Param { { "Action", "Health" } }); }
        [ContextMenu("Probe/3 Seed own test rows")]
        public void Seed() { Send("ProbeSeed"); }
        [ContextMenu("Probe/4 Read own test row")]
        public void ReadSelf() { Send("ProbeReadSelf"); }
        [ContextMenu("Probe/5 Write own test row")]
        public void WriteSelf() { Send("ProbeWriteSelf"); }
        [ContextMenu("Probe/6 Read peer (Public then Private)")]
        public void ReadPeer() { Send("ProbeReadPeer", true); }
        [ContextMenu("Probe/7 Update pair (read both afterwards)")]
        public void WritePair() { Send("ProbeWritePair", true); }
        [ContextMenu("Probe/8 Rollback with invalid third row")]
        public void Rollback() { Send("ProbeRollback"); }
        [ContextMenu("Probe/9 Concurrent identical conditional requests")]
        public void ConcurrentConditionalWrites()
        {
            if (!ValidateRun() || Volatile.Read(ref pending) != 0) return;
            string requestId = Guid.NewGuid().ToString("N");
            int count = Mathf.Clamp(ConcurrentRequests, 2, 8);
            UnityEngine.Debug.Log("[FunctionProbe] Sending " + count + " requests with the SAME version and request ID. ReadSelf afterwards; transport success alone is not a pass.");
            for (int i = 0; i < count; i++)
            {
                var param = Request("ProbeConditionalWrite");
                param.Add("ExpectedVersion", ExpectedVersion); param.Add("RequestId", requestId);
                Invoke(param);
            }
        }
        [ContextMenu("Probe/10 Retry old version (must not apply)")]
        public void RetryOldVersion()
        {
            var param = Request("ProbeConditionalWrite");
            param.Add("ExpectedVersion", ExpectedVersion); param.Add("RequestId", Guid.NewGuid().ToString("N"));
            if (ValidateRun()) Invoke(param);
        }

        bool ValidateRun()
        {
            if (Guid.TryParseExact(RunId, "N", out _)) return true;
            LastResult = "Generate/share a RunId first. No request sent.";
            UnityEngine.Debug.LogWarning("[FunctionProbe] " + LastResult); return false;
        }
        Param Request(string action) => new Param {
            { "Action", action }, { "RunId", RunId ?? "" }, { "Visibility", UsePrivateTable ? "Private" : "Public" }
        };
        void Send(string action, bool peer = false)
        {
            if (!ValidateRun() || Volatile.Read(ref pending) != 0) return;
            var param = Request(action);
            if (peer) { param.Add("OtherUserId", OtherUserInDate ?? ""); param.Add("OtherRowId", OtherPrimaryRowId ?? ""); }
            Invoke(param);
        }
        void Invoke(Param param)
        {
            if (!Backend.IsInitialized || string.IsNullOrWhiteSpace(Backend.UserInDate) || string.IsNullOrWhiteSpace(FunctionName))
            { LastResult = "Blocked: backend initialization/login or function name missing. No request sent; no data changed."; UnityEngine.Debug.LogWarning("[FunctionProbe] " + LastResult); return; }
            if (Volatile.Read(ref pending) >= 8) return;
            string owner = Backend.UserInDate;
            var timer = Stopwatch.StartNew(); Interlocked.Increment(ref pending);
            LastResult = "Waiting for backend response...";
            try
            {
                // Direct async calls intentionally avoid SendQueue serialization in the race test.
                Backend.BFunc.InvokeFunction(FunctionName, param, response => {
                    try {
                        string body = response.IsSuccess() ? response.GetReturnValue() : "(transport failed; inspect status)";
                        if (body != null && body.Length > 2000) body = body.Substring(0, 2000) + "...";
                        output.Enqueue("[FunctionProbe] elapsedMs=" + timer.ElapsedMilliseconds + " status=" + response.GetStatusCode() + " errorCode=" + response.GetErrorCode() +
                            " accountChanged=" + (Backend.UserInDate != owner) + " reply=" + body);
                    } finally { Interlocked.Decrement(ref pending); }
                });
            }
            catch { Interlocked.Decrement(ref pending); output.Enqueue("[FunctionProbe] invoke failed before callback; no automatic retry."); }
        }
    }
}
#endif

#endif
