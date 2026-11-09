using System;
using System.Globalization;
using System.Linq;
using Assets.Scripts.Exert.BackSys;
using Assets.Scripts.Prack.BackSys.ChartData;
using TMPro;
using UnityEngine;

namespace Assets.Scripts.Sight.Surface.Consist
{
    public sealed partial class BottomMenuView
    {
        [Serializable] sealed class AttendanceCalendar
        {
            public int TodayDay;
            public AttendanceData[] Rows;
        }

        async void Attendance()
        {
            CloseModal();
            modal = Node("Attendance", design, 0, 0, Width, Height);
            var opened = modal;
            Box("Shade", opened, 0, 0, Width, Height, new Color(0, 0, 0, .69f)).raycastTarget = true;
            var panel = Node("AttendancePanel", opened, 272, 78, 1126, 764);
            Art("Frame", panel, Resources.Load<Texture2D>("Menu/Attendance/Panel"), 0, 0, 1126, 764);
            Hit("닫기", panel, 1024, 6, 90, 82, CloseModal);
            Hit("우편함 열기", panel, 848, 688, 220, 70, OpenMailbox);
            var status = Text("Status", panel, "출석 현황을 불러오는 중…", 90, 325, 946, 70, 25, Color.white);
            string owner = GameApiClient.UserId;
            if (!Application.isPlaying || string.IsNullOrEmpty(owner))
            {
                status.text = "로그인 후 출석 현황을 확인할 수 있습니다.";
                return;
            }
            try
            {
                var calendar = await GameApiClient.Request<AttendanceCalendar>("/api/me/attendance");
                if (!this || !opened || opened != modal || owner != GameApiClient.UserId) return;
                if (calendar?.Rows == null || calendar.Rows.Length == 0)
                {
                    status.text = "현재 출석 일정이 없습니다.";
                    return;
                }
                status.gameObject.SetActive(false);
                DrawAttendance(panel, calendar.Rows, calendar.TodayDay);
            }
            catch (Exception e)
            {
                if (!this || !opened || opened != modal || owner != GameApiClient.UserId) return;
                Debug.LogWarning("출석 현황 조회 실패: " + e.Message);
                status.text = "출석 현황을 불러오지 못했습니다.";
                SolidButton("다시 시도", panel, 459, 408, 208, 54, Attendance);
            }
        }

        void DrawAttendance(RectTransform panel, AttendanceData[] rows, int todayDay)
        {
            var count = Text("AttendanceCount", panel, "", 600, 45, 373, 54, 34, Color.white);
            count.richText = true;
            count.text = $"누적 출석  <color=#FFE338>{rows.Count(r => r.GiveVal > 0)}일</color> / {rows.Length}일";
            var dayFrame = Resources.Load<Texture2D>("Menu/Attendance/Day");
            var todayFrame = Resources.Load<Texture2D>("Menu/Attendance/Today");
            var missedFrame = Resources.Load<Texture2D>("Menu/Attendance/Missed");
            var check = Resources.Load<Texture2D>("Menu/Attendance/Check");
            var missed = Resources.Load<Texture2D>("Menu/Attendance/Absent");
            var coin = Resources.Load<Texture2D>("Gameplay/level-up-Coin");
            var jem = Resources.Load<Texture2D>("Gameplay/level-up-Jem");
            foreach (var row in rows)
            {
                int slot = row.DayNum - 1;
                if (slot < 0 || slot >= 30) continue;
                bool today = row.DayNum == todayDay;
                bool attended = row.GiveVal > 0;
                bool absent = !attended && row.DayNum < todayDay;
                var cell = Node("Day" + row.DayNum, panel, 27 + slot % 6 * 180, 136 + slot / 6 * 108, 172, 102);
                Art("Tile", cell, today ? todayFrame : absent ? missedFrame : dayFrame, 0, 0, 172, 102);
                var ink = absent ? new Color(.66f, .68f, .72f) : Color.white;
                Text("DayLabel", cell, $"{row.DayNum}일차", 12, 6, 91, 31, 24, ink, TextAlignmentOptions.MidlineLeft);
                if (today)
                {
                    Box("TodayTab", cell, 106, 4, 59, 29, Yellow);
                    Text("TodayLabel", cell, "오늘", 106, 3, 59, 29, 20, Color.black);
                }
                bool marked = attended || absent;
                var icon = Art("Reward", cell, row.RewardType == 1 ? coin : jem, 12, 47, 41, 38);
                icon.color = ink;
                Text("Amount", cell, "×" + row.RewardVal.ToString("N0", CultureInfo.InvariantCulture),
                    53, 41, marked ? 73 : 110, 52, 23, ink, TextAlignmentOptions.MidlineLeft, true);
                if (marked) Art(attended ? "Attended" : "Missed", cell, attended ? check : missed, 128, 46, 36, 36);
            }
        }
    }
}
