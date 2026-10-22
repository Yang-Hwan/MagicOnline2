using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Assets.Scripts.Prack.BackSys.ChartData;
using Assets.Scripts.Prack.BackSys.GameData;
using UnityEngine;
namespace Assets.Scripts.Exert.BackSys
{
    public class BackendChart
    {
        public static List<CommonCodeData> commonCodeData = new List<CommonCodeData>();
        public static List<SkillMatchData> skillMatchData = new List<SkillMatchData>();
        public static List<AttendanceData> attendanceData = new List<AttendanceData>();
        [Serializable] sealed class Rows<T> { public T[] Items; }
        [Serializable] sealed class Protocol { public int Version; }
        static async Task<List<T>> Load<T>(string name)
        {
            var json = await GameApiClient.Request<string>("/api/charts/" + name);
            return new List<T>(JsonUtility.FromJson<Rows<T>>("{\"Items\":" + json + "}").Items);
        }
        public static async Task LoadAllAsync()
        {
            Protocol protocol;
            try { protocol = await GameApiClient.Request<Protocol>("/api/match-protocol"); }
            catch (GameApiException e) when (e.Status == 404) { throw new GameApiException(409, "MatchProtocolMismatch"); }
            if (protocol == null || protocol.Version != MatchRulesContract.CurrentVersion)
                throw new GameApiException(409, "MatchProtocolMismatch");
            var codes = await Load<CommonCodeData>("CommonCode");
            var halls = await Load<SkillMatchData>("SkillMatch");
            var attendance = await Load<AttendanceData>("Attendance");
            if (halls.Count == 0) throw new InvalidOperationException("경기장 설정이 없습니다.");
            commonCodeData = codes; skillMatchData = halls; attendanceData = attendance;
        }
        public static async void LoadAllChart() { try { await LoadAllAsync(); } catch(Exception e) { Debug.LogWarning(e.Message); } }
        public static void LoadCommonCode() => LoadAllChart();
        public static void LoadSkillMatch() => LoadAllChart();
        public static void LoadAttendance() => LoadAllChart();
    }
}
