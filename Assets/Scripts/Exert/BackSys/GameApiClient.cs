using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Assets.Scripts.Exert.BackSys
{
    public sealed class GameApiException : Exception
    {
        public readonly long Status;
        public GameApiException(long status, string code) : base(code) { Status = status; }
        public string UserMessage => Message == "MatchProtocolMismatch" ? "게임과 서버 버전이 다릅니다. 게임을 업데이트한 후 다시 로그인해 주세요." :
            Message == "MatchRulesMismatch" ? "경기장 규칙이 변경됐습니다. 다시 로그인해 경기장 정보를 갱신해 주세요." :
            Message == "UnsupportedMatchRuntime" || Message == "UnsupportedMatchRules" ? "현재 게임에서 지원하지 않는 경기 규칙입니다." : Message;
    }
    public static class GameApiClient
    {
        [Serializable] public sealed class Config { public string BaseUrl = "http://127.0.0.1:5080"; }
        [Serializable] public sealed class Credentials { public string LoginId, Password; }
        [Serializable] public sealed class Tokens { public string UserId, AccessToken, RefreshToken, AccessExpiresAt; }
        [Serializable] sealed class TokenInput { public string RefreshToken; }
        [Serializable] sealed class Error { public string Code; }
        static readonly SemaphoreSlim refreshing = new SemaphoreSlim(1, 1);
        static Tokens tokens;
        static int generation;
        static string baseUrl;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Local integration fault injection; never compiled into release players.
        public static bool OfflineForDevelopmentTest { get; set; }
#endif
        public static string UserId => tokens?.UserId ?? "";
        public static string LoginId { get; private set; }
        public static string BaseUrl
        {
            get
            {
                if (baseUrl == null)
                {
                    var file = Resources.Load<TextAsset>("GameApiConfig");
                    var config = file ? JsonUtility.FromJson<Config>(file.text) : new Config();
                    var uri = new Uri(config.BaseUrl);
                    if (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback))
                        throw new InvalidOperationException("API에는 HTTPS 주소를 사용하세요. 로컬 루프백만 HTTP를 허용합니다.");
                    baseUrl = config.BaseUrl.TrimEnd('/');
                }
                return baseUrl;
            }
        }
        public static async Task Login(string login, string password, bool register = false)
        {
            Clear();
            int started = generation;
            var reply = await Request<Tokens>("/api/auth/" + (register ? "register" : "login"), "POST", new Credentials { LoginId = login, Password = password }, false);
            if (started != generation) throw new InvalidOperationException("AccountChanged");
            tokens = reply; LoginId = login;
        }
        public static void Clear() { generation++; tokens = null; LoginId = ""; }
        public static async Task Logout()
        {
            try { if (tokens != null) await Request<object>("/api/auth/logout", "POST"); }
            finally { Clear(); }
        }
        public static async Task<T> Request<T>(string path, string method = "GET", object body = null, bool authenticated = true)
        {
            var owner = UserId; var access = tokens?.AccessToken;
            int started = generation;
            try
            {
                var reply = await Send<T>(path, method, body, authenticated ? access : null);
                if (authenticated && started != generation) throw new InvalidOperationException("AccountChanged");
                return reply;
            }
            catch (GameApiException e) when (authenticated && e.Status == 401 && tokens != null)
            {
                await refreshing.WaitAsync();
                try
                {
                    if (started != generation || owner != UserId) throw new InvalidOperationException("AccountChanged");
                    if (tokens.AccessToken == access)
                    {
                        var rotated = await Send<Tokens>("/api/auth/refresh", "POST", new TokenInput { RefreshToken = tokens.RefreshToken }, null);
                        if (started != generation) throw new InvalidOperationException("AccountChanged");
                        tokens = rotated;
                    }
                }
                finally { refreshing.Release(); }
                if (started != generation || owner != UserId) throw new InvalidOperationException("AccountChanged");
                var retried = await Send<T>(path, method, body, tokens.AccessToken);
                if (started != generation || owner != UserId) throw new InvalidOperationException("AccountChanged");
                return retried;
            }
        }
        static async Task<T> Send<T>(string path, string method, object body, string access)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (OfflineForDevelopmentTest) throw new GameApiException(0, "DevelopmentNetworkUnavailable");
#endif
            using (var request = new UnityWebRequest(BaseUrl + path, method))
            {
                request.downloadHandler = new DownloadHandlerBuffer(); request.timeout = 12;
                if (body != null) { request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body))); request.SetRequestHeader("Content-Type", "application/json"); }
                if (!string.IsNullOrEmpty(access)) request.SetRequestHeader("Authorization", "Bearer " + access);
                var completion = new TaskCompletionSource<bool>();
                var operation = request.SendWebRequest(); operation.completed += _ => completion.TrySetResult(true);
                if (operation.isDone) completion.TrySetResult(true);
                await completion.Task;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    string code = "서버에 연결할 수 없습니다.";
                    try { code = JsonUtility.FromJson<Error>(request.downloadHandler.text)?.Code ?? code; } catch (ArgumentException) { }
                    throw new GameApiException(request.responseCode, code);
                }
                var text = request.downloadHandler.text;
                if (typeof(T) == typeof(string)) return (T)(object)text;
                if (typeof(T) == typeof(object)) return default;
                return string.IsNullOrEmpty(text) || text == "null" ? default : JsonUtility.FromJson<T>(text);
            }
        }
    }
}
