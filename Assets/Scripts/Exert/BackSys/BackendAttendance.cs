using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Assets.Scripts.Prack.BackSys.ChartData;
using Assets.Scripts.Prack.BackSys.GameData;
namespace Assets.Scripts.Exert.BackSys
{
    public class BackendAttendance
    {
        static readonly BackendAttendance instance = new BackendAttendance();
        public static BackendAttendance Instance => instance;
        public List<AttendanceData> AttendanceGiveDatas = new List<AttendanceData>();
        [Serializable] sealed class Reply { public long StTs, EnTs; public string DtInfo; public AttendanceData[] Rows; }
        UserAttendanceTermData term = new UserAttendanceTermData();
        public async Task LoadAsync()
        {
            var reply = await GameApiClient.Request<Reply>("/api/me/attendance", "POST");
            term = new UserAttendanceTermData { StTs = reply.StTs, EnTs = reply.EnTs, DtInfo = reply.DtInfo };
            AttendanceGiveDatas = new List<AttendanceData>(reply.Rows);
        }
        public async void GetAttendanceGive() { try { await LoadAsync(); } catch(Exception e) { UnityEngine.Debug.LogWarning(e.Message); } }
        public UserAttendanceTermData GetAttendanceTerm() => term;
    }
}
