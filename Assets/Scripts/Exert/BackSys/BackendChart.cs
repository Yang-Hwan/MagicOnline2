using System.Collections.Generic;
using UnityEngine;
using BackEnd;
using BackEnd.Content;
using Assets.Scripts.Prack.BackSys.ChartData;
using Assets.Scripts.Often;

namespace Assets.Scripts.Exert.BackSys
{
    public class BackendChart
    {

        public static List<CommonCodeData> commonCodeData;
        public static List<SkillMatchData> skillMatchData;
        public static List<AttendanceData> attendanceData;

        static BackendChart()
        {
            commonCodeData = new List<CommonCodeData>();
            skillMatchData = new List<SkillMatchData>();
            attendanceData = new List<AttendanceData>();
        }

        /// <summary>
        /// 공통, 시합, 출석 
        /// </summary>
        public static void LoadAllChart()
        {
            LoadCommonCode();   // 동기
            LoadSkillMatch();   // 동기 
            LoadAttendance();   // 동기
        }

        private static LitJson.JsonData LoadChartRows(string configuredId)
        {
            var tableResponse = Backend.CDN.Content.Table.Get();
            if (!tableResponse.IsSuccess())
            {
                Debug.LogError($"차트 목록 조회 실패 (설정 ID: {configuredId}): {tableResponse}");
                return null;
            }

            var tables = tableResponse.GetContentTableItemList();
            var table = tables.Find(item => item.chartId == configuredId);
            // 이전 설정이 차트 파일 ID인 경우 현재 선택된 파일과 대조한다.
            if (table == null)
                table = tables.Find(item => item.selectedChartFileId == configuredId);

            if (table == null)
            {
                Debug.LogError($"차트를 찾을 수 없습니다 (설정 ID: {configuredId}). 뒤끝 콘솔의 차트 ID와 선택된 파일을 확인하세요.");
                return null;
            }

            var contentResponse = Backend.CDN.Content.Get(new List<ContentTableItem> { table });
            if (!contentResponse.IsSuccess())
            {
                Debug.LogError($"차트 내용 조회 실패 ({table.chartName}, ID: {table.chartId}): {contentResponse}");
                return null;
            }

            var content = contentResponse.GetContentList().Find(item => item.chartId == table.chartId);
            if (content == null || !string.IsNullOrEmpty(content.errorString))
            {
                Debug.LogError($"차트 파일 다운로드 실패 (ID: {table.chartId}): {content?.errorString}");
                return null;
            }

            try
            {
                var rows = LitJson.JsonMapper.ToObject(content.contentString);
                if (rows == null || !rows.IsArray)
                {
                    Debug.LogError($"차트 내용이 JSON 배열이 아닙니다 (ID: {table.chartId}).");
                    return null;
                }
                return rows;
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"차트 JSON 해석 실패 (ID: {table.chartId}): {exception}");
                return null;
            }
        }

        public static void LoadCommonCode()
        {
            var jsonData = LoadChartRows(Constants.CommonCode_Id);

            if (jsonData != null)
            {
                try
                {

                    // 받아온 데이터 개수가 0이면 데이터가 없는 것
                    if (jsonData.Count <= 0)
                    {
                        Debug.LogWarning("데이터가 존재하지 않습니다.");
                    }
                    else
                    {
                        Debug.Log($"CommonCode cnt : {jsonData.Count}");
                        for (int i = 0; i < jsonData.Count; i++)
                        {
                            CommonCodeData newCommoncode = new CommonCodeData();
                            newCommoncode.GrpCd = jsonData[i]["GrpCd"].ToString();
                            newCommoncode.ComCd = jsonData[i]["ComCd"].ToString();
                            newCommoncode.ComVal = int.Parse(jsonData[i]["ComVal"].ToString());
                            newCommoncode.ComVal2 = int.Parse(jsonData[i]["ComVal2"].ToString());
                            newCommoncode.Info = jsonData[i]["Info"].ToString();
                            newCommoncode.IsUse = jsonData[i]["IsUse"].ToString();

                            commonCodeData.Add(newCommoncode);
                        }

                        //Debug.Log($"CHART.commonCodeData len : {commonCodeData.Count}");
                    }

                }
                // JSON 데이터 파싱 실패
                catch (System.Exception e)
                {
                    // try-catch 에러 출력
                    Debug.LogError(e);
                }
            }

        }

        public static void LoadSkillMatch()
        {
            var jsonData = LoadChartRows(Constants.SkillMatch_Id);
            if (jsonData != null)
            {
                try
                {

                    Debug.Log($"SkillMatch cnt : {jsonData.Count}");
                    // 받아온 데이터 개수가 0이면 데이터가 없는 것
                    if (jsonData.Count <= 0)
                    {
                        Debug.LogWarning("데이터가 존재하지 않습니다.");
                    }
                    else
                    {
                        for (int i = 0; i < jsonData.Count; i++)
                        {
                            SkillMatchData newSkillMatch = new SkillMatchData();
                            newSkillMatch.HallIdx = int.Parse(jsonData[i]["HallIdx"].ToString());
                            newSkillMatch.MatchBall = int.Parse(jsonData[i]["MatchBall"].ToString());
                            newSkillMatch.TargetHit = int.Parse(jsonData[i]["TargetHit"].ToString());
                            newSkillMatch.PrizeCoin = long.Parse(jsonData[i]["PrizeCoin"].ToString());
                            newSkillMatch.MaxCoin = long.Parse(jsonData[i]["MaxCoin"].ToString());
                            newSkillMatch.MatchTotMin = int.Parse(jsonData[i]["MatchTotMin"].ToString());
                            newSkillMatch.HallName = jsonData[i]["HallName"].ToString();
                            newSkillMatch.MatchCushion = int.Parse(jsonData[i]["MatchCushion"].ToString());
                            newSkillMatch.FinishMission = int.Parse(jsonData[i]["FinishMission"].ToString());

                            skillMatchData.Add(newSkillMatch);
                        }
                        //Debug.Log($"CHART.skillMatchData len : {skillMatchData.Count}");

                    }
                }
                // JSON 데이터 파싱 실패
                catch (System.Exception e)
                {
                    // try-catch 에러 출력
                    Debug.LogError(e);
                }
            }

        }

        public static void LoadAttendance()
        {
            var jsonData = LoadChartRows(Constants.Attendance_Id);
            if (jsonData == null) return;

            try
            {
                if (jsonData.Count <= 0)
                {
                    Debug.LogWarning("데이터가 존재하지 않습니다.");
                    return;
                }

                for (int i = 0; i < jsonData.Count; i++)
                {
                    AttendanceData newAttendance = new AttendanceData();
                    newAttendance.DayNum = int.Parse(jsonData[i]["DayNum"].ToString());
                    newAttendance.RewardType = int.Parse(jsonData[i]["RewardType"].ToString());
                    newAttendance.RewardVal = int.Parse(jsonData[i]["RewardVal"].ToString());
                    newAttendance.GiveVal = 0;

                    attendanceData.Add(newAttendance);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError(e);
            }
        }
    }
}