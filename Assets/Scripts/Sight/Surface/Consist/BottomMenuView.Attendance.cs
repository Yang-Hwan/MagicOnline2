using System;
using System.Globalization;
using System.Linq;
using Assets.Scripts.Exert.BackSys;
using Assets.Scripts.Prack.BackSys.ChartData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
            Art("Frame", panel, Resources.Load<Texture2D>("Menu/Attendance/CalendarPanel"), 0, 0, 1126, 764);
            Text("Title", panel, "출석부", 225, 65, 220, 70, 56, Color.white);
            Text("Subtitle", panel, "매일 쌓이는 즐거운 보상", 458, 84, 238, 40, 20, new Color(.85f, .93f, .85f));
            Text("CloseLabel", panel, "×", 1046, 45, 54, 54, 40, Color.white);
            Hit("닫기", panel, 1035, 37, 70, 70, CloseModal);
            Text("MailHint", panel, "출석 보상은 우편함에서 받을 수 있어요.", 65, 681, 735, 48, 24, AttendanceInk, TextAlignmentOptions.MidlineLeft);
            AttendancePaper("MailButton", panel, 848, 678, 225, 52, new Color(.07f, .38f, .31f), new Color(.18f, .49f, .38f), 26);
            Text("MailLabel", panel, "우편함 열기", 848, 678, 225, 52, 25, Color.white);
            Hit("우편함 열기", panel, 848, 678, 225, 52, OpenMailbox);
            var status = Text("Status", panel, "출석 현황을 불러오는 중…", 90, 325, 946, 70, 25, AttendanceInk);
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
            AttendancePaper("CountBadge", panel, 721, 81, 310, 46, new Color(1, .96f, .84f), new Color(.77f, .67f, .43f), 12);
            var count = Text("AttendanceCount", panel, "", 727, 81, 298, 46, 23, AttendanceInk);
            count.richText = true;
            count.text = $"누적 출석  <color=#B77A24>{rows.Count(r => r.GiveVal > 0)}일</color> / {rows.Length}일";
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
                var cell = Node("Day" + row.DayNum, panel, 29 + slot % 6 * 179, 184 + slot / 6 * 93, 172, 86);
                var fill = today ? new Color(1, .94f, .69f) : attended ? new Color(.85f, .94f, .85f) : absent ? new Color(.91f, .91f, .86f) : new Color(1, .98f, .92f);
                AttendancePaper("Tile", cell, 0, 0, 172, 86, fill, today ? new Color(.18f, .52f, .39f) : new Color(.81f, .77f, .64f), 10, today ? 3 : 1.2f);
                var ink = absent ? new Color(.48f, .54f, .48f) : AttendanceInk;
                Text("DayLabel", cell, $"{row.DayNum}일차", 12, 5, 112, 42, 32, ink, TextAlignmentOptions.MidlineLeft);
                if (today)
                {
                    AttendancePaper("TodayTab", cell, 125, 10, 39, 24, new Color(.17f, .48f, .35f), new Color(.17f, .48f, .35f), 7);
                    Text("TodayLabel", cell, "오늘", 125, 10, 39, 24, 16, Color.white);
                }
                bool marked = attended || absent;
                var icon = Art("Reward", cell, row.RewardType == 1 ? coin : jem, 12, 51, 27, 25);
                icon.color = absent ? new Color(1, 1, 1, .5f) : Color.white;
                Text("Amount", cell, "×" + row.RewardVal.ToString("N0", CultureInfo.InvariantCulture),
                    44, 48, marked ? 87 : 116, 31, 18, ink, TextAlignmentOptions.MidlineLeft, true);
                if (marked) Art(attended ? "Attended" : "Missed", cell, attended ? check : missed, 139, 52, 24, 24);
            }
        }

        static readonly Color AttendanceInk = new Color(.12f, .29f, .24f);

        static void AttendancePaper(string name, Transform parent, float x, float y, float w, float h,
            Color fill, Color border, float radius, float stroke = 1)
        {
            var graphic = Node(name, parent, x, y, w, h).gameObject.AddComponent<AttendancePaperGraphic>();
            graphic.color = fill;
            graphic.Border = border;
            graphic.Radius = radius;
            graphic.Stroke = stroke;
            graphic.raycastTarget = false;
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class AttendancePaperGraphic : MaskableGraphic
    {
        public Color Border;
        public float Radius = 10, Stroke = 1;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = rectTransform.rect;
            AddRounded(vh, rect, Radius, Border);
            rect.xMin += Stroke; rect.xMax -= Stroke;
            rect.yMin += Stroke; rect.yMax -= Stroke;
            AddRounded(vh, rect, Mathf.Max(0, Radius - Stroke), color);
        }

        static void AddRounded(VertexHelper vh, Rect rect, float radius, Color tint)
        {
            radius = Mathf.Min(radius, Mathf.Min(rect.width, rect.height) / 2);
            int start = vh.currentVertCount;
            vh.AddVert(rect.center, tint, Vector2.zero);
            const int steps = 8;
            for (int corner = 0; corner < 4; corner++)
            {
                var center = new Vector2(corner < 2 ? rect.xMax - radius : rect.xMin + radius,
                    corner == 0 || corner == 3 ? rect.yMax - radius : rect.yMin + radius);
                for (int step = 0; step <= steps; step++)
                {
                    float angle = (90 - corner * 90 - step * 90f / steps) * Mathf.Deg2Rad;
                    vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, tint, Vector2.zero);
                }
            }
            int count = 4 * (steps + 1);
            for (int i = 0; i < count; i++) vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % count);
        }
    }
}
