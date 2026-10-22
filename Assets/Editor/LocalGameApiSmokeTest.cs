#if UNITY_EDITOR
using System;
using System.IO;
using System.Threading.Tasks;
using Assets.Scripts.Exert.BackSys;
using Assets.Scripts.Prack.BackSys;
using UnityEditor;
using UnityEngine;

// Exercises UnityWebRequest + JsonUtility against the real local API, without storing credentials.
public static class LocalGameApiSmokeTest
{
    [MenuItem("Tools/Local Game API/Run HTTP smoke test")]
    public static async void Run()
    {
        const string result = "UserSettings/LocalGameApiSmokeTest.json";
        if (!new Uri(GameApiClient.BaseUrl).IsLoopback) throw new InvalidOperationException("Smoke test requires local API.");
        try
        {
            var login = "unity_" + Guid.NewGuid().ToString("N").Substring(0, 12);
            await GameApiClient.Login(login, "Test-" + Guid.NewGuid().ToString("N"), true);
            await BackendChart.LoadAllAsync();
            await BackendAttendance.Instance.LoadAsync();
            await BackendGame.Instance.LoadAsync();
            if (BackendChart.skillMatchData.Count < 2 || BackendGame.Instance.UserMainData.Coin != 30 || string.IsNullOrEmpty(GameApiClient.UserId))
                throw new InvalidOperationException("Unexpected chart/profile/attendance response");
            await GameApiClient.Logout();
            Directory.CreateDirectory("UserSettings");
            File.WriteAllText(result, "{\"Ok\":true,\"Checks\":\"Unity registration, charts, attendance, profile, logout\"}");
            Debug.Log("[LocalGameApiSmokeTest] PASS: Unity HTTP, signup, charts, attendance, profile, logout.");
        }
        catch (Exception e)
        {
            GameApiClient.Clear();
            Directory.CreateDirectory("UserSettings");
            File.WriteAllText(result, "{\"Ok\":false}");
            Debug.LogError("[LocalGameApiSmokeTest] FAIL: " + e.Message);
        }
    }
}
#endif
