using UnityEngine;
using BackEnd;
using System;

using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Prack.BackSys.ChartData;
using Assets.Scripts.Prack.BackSys.GameData;
using Assets.Scripts.Often;

namespace Assets.Scripts.Exert.BackSys
{
    public class BackendAttendance
    {
        static BackendAttendance instance = null;

        public static BackendAttendance Instance
        {
            get
            {
                if(instance == null)
                {
                    instance = new BackendAttendance();
                }
                return instance;
            }
        }

        public List<AttendanceData> AttendanceGiveDatas = new List<AttendanceData>();


        /// <summary>
        /// 회원의 출석체크
        /// </summary>
        /// <param name="StTs">시작일자</param>
        /// <param name="EnTs">종료일자</param>
        /// <returns> 순번, 보상타입, 보상값, 받은시간, 받은일자</returns>
        public List<UserAttendanceData> GetUserAttendance(long StTs, long EnTs)
        {
            List<UserAttendanceData> atts = new List<UserAttendanceData>();
            Where where = new Where();
            where.LessOrEqual("StTs", StTs);
            where.GreaterOrEqual("EnTs", EnTs);
            var bro = Backend.GameData.GetMyData("UserAttendance", where);
            if (bro.IsSuccess())
            {
                LitJson.JsonData jsonData = bro.FlattenRows();
                if (jsonData.Count > 0)
                {
                    for (int i = 0; i < jsonData.Count; i++)
                    {
                        UserAttendanceData data = new UserAttendanceData();
                        data.DayNum = int.Parse(jsonData[i]["DayNum"].ToString());
                        data.RewardType = int.Parse(jsonData[i]["RewardType"].ToString());
                        data.RewardVal = int.Parse(jsonData[i]["RewardVal"].ToString());
                        data.GiveTs = long.Parse(jsonData[i]["GiveTs"].ToString());
                        data.GiveDt = jsonData[i]["GiveDt"].ToString();
                        atts.Add(data);
                    }
                }
            }

            return atts;

        }



        /// <summary>
        /// 출석부 없다면 새로추가
        /// </summary>
        /// <param name="data"></param>
        public void SetUserAttendanceExistsInsert(UserAttendanceData data)
        {
            Where where = new Where();
            where.Equal("GiveDt", data.GiveDt);
            var bro = Backend.GameData.GetMyData("UserAttendance", where);
            if (bro.IsSuccess())
            {
                if (bro.Rows().Count == 0)
                {
                    //   Debug.Log("출석부 새로 추가.");
                    SetUserAttendance(data);
                }
                else
                {
                    //    Debug.Log("출석부 이미 적용되어 있음.");
                }
            }

        }


        /// <summary>
        /// 출석부 추가 
        /// </summary>
        /// <param name="data"></param>
        public void SetUserAttendance(UserAttendanceData data)
        {
            Param param = new Param()
            {
                {"GiveDt", data.GiveDt },
                {"GiveTs", data.GiveTs },
                {"DayNum", data.DayNum },
                {"RewardType", data.RewardType },
                {"RewardVal", data.RewardVal },
            };
            var bro = Backend.GameData.Insert("UserAttendance", param);

            if (bro.IsSuccess())
            {
                Debug.Log($"SAVE UserAttendance data.DayNum : {data.DayNum}, data.GiveDt : {data.GiveDt}  ");
            }
            // 실패했을 때
            else
            {
                Debug.LogError($"게임 정보 데이터 삽입에 실패했습니다. : {bro}");
            }
        }


        /// <summary>
        /// 출석정보 조회 한번만 실행 필요.
        /// </summary>
        public void GetAttendanceGive()
        {
            // 출석부기간 조회 (없으면 기간 생성)
            UserAttendanceTermData term = GetAttendanceTerm();

            // 출석 체크 (DayNum확인)
            SetTodayAttendanceSave(term);

            // 출석한 데이터 조회
            List<UserAttendanceData> give = GetAttendance(term.StTs, term.EnTs);

            // 출석부에서 출석체크 정리
            List<AttendanceData> att_data = BackendChart.attendanceData
                .GroupJoin(
                    give,
                    a => a.DayNum,
                    g => g.DayNum,
                    (a, g) => new { a, g })
                .SelectMany(
                    item => item.g.DefaultIfEmpty(),
                    (att, giv) => new AttendanceData()
                    {
                        DayNum = att.a.DayNum,
                        RewardType = att.a.RewardType,
                        RewardVal = att.a.RewardVal,
                        GiveVal = att.a.GiveVal != -1 ? (giv?.DayNum ?? 0) : -1,
                    })
                .ToList();
            AttendanceGiveDatas = att_data;
        }


        /// <summary>
        /// 유저출석부 : 한달간 출석한 데이터 조회 
        /// </summary>
        /// <param name="stTs"></param>
        /// <param name="enTs"></param>
        /// <returns></returns>
        public List<UserAttendanceData> GetAttendance(long stTs, long enTs)
        {
            List<UserAttendanceData> lst = new List<UserAttendanceData>();
            Where where = new Where();
            where.Between("GiveTs", stTs, enTs);
            //where.GreaterOrEqual("GiveTs", stTs);
            //where.LessOrEqual("GiveTs", enTs);
            var bro = Backend.GameData.GetMyData("UserAttendance", where);
            if (bro.IsSuccess())
            {
                LitJson.JsonData jsonData = bro.FlattenRows();
                for (int i = 0; i < jsonData.Count; i++)
                {
                    UserAttendanceData data = new UserAttendanceData();
                    data.GiveTs = long.Parse(jsonData[i]["GiveTs"].ToString());
                    data.GiveDt = jsonData[i]["GiveDt"].ToString();
                    data.DayNum = int.Parse(jsonData[i]["DayNum"].ToString());
                    data.RewardType = int.Parse(jsonData[i]["RewardType"].ToString());
                    data.RewardVal = int.Parse(jsonData[i]["RewardVal"].ToString());
                    lst.Add(data);
                }
            }
            return lst.OrderBy(r => r.DayNum).ToList();
        }

        /// <summary>
        /// 금일에 맞는 출석부기간을 조회 
        /// </summary>
        /// <returns></returns>
        public UserAttendanceTermData GetAttendanceTerm()
        {
            int term_len = BackendChart.attendanceData.Count;
            UserAttendanceTermData data = new UserAttendanceTermData();
            DateTime now = DateTime.Now.Date;
            long ts =  DateHandle.GetDateToStamp(now);

            Where where = new Where();
            where.LessOrEqual("StTs", ts);   //  
            where.GreaterOrEqual("EnTs", ts);  //     
            var bro = Backend.GameData.GetMyData("UserAttendanceTerm", where);
            if (bro.IsSuccess())
            {
                LitJson.JsonData jsonData = bro.FlattenRows();
                if (jsonData.Count > 0)
                {
                    //Debug.Log($"UserAttendanceTerm cnt : {jsonData.Count}");
                    data.StTs = long.Parse(jsonData[0]["StTs"].ToString());
                    data.EnTs = long.Parse(jsonData[0]["EnTs"].ToString());
                    data.DtInfo = jsonData[0]["DtInfo"].ToString();
                }
                else
                {
                    DateTime now_end = now.AddDays(term_len - 1).Date;
                    long ts_end = DateHandle.GetDateToStamp(now_end);
                    string dtInfo = $"{now.ToString("MM.dd")} - {now_end.Date.ToString("MM.dd")}";
                    Param param = new Param()
                    {
                        {"StTs", ts },
                        {"EnTs", ts_end },
                        {"DtInfo", dtInfo },
                    };

                    var bro1 = Backend.GameData.Insert("UserAttendanceTerm", param);
                    if (bro1.IsSuccess())
                    {
                        data.StTs = ts;
                        data.EnTs = ts_end;
                        data.DtInfo = dtInfo;
                        string indate = bro1.GetInDate();
                        //Debug.Log($"게임 정보 데이터 삽입에 성공했습니다.  ");
                    }
                    // 실패했을 때
                    else
                    {
                        Debug.LogError($"게임 정보 데이터 삽입에 실패했습니다. : {bro1}");
                    }
                }
            }
            // 실패했을 때
            else
            {
                Debug.LogError($"게임 정보 데이터 삽입에 실패했습니다. : {bro}");
            }

            return data;
        }



        /// <summary>
        /// 금일자 출석체크 저장 
        /// </summary>
        /// <param name="term"></param>
        public void SetTodayAttendanceSave(UserAttendanceTermData term)
        {
            DateTime stDt = DateHandle.GetStampToDate(term.StTs);
            DateTime now = DateTime.Now.Date;
            long nowTs = DateHandle.GetDateToStamp(DateTime.Now.Date);
            int gap = (now - stDt).Days;
            int dayNum = gap + 1;

            AttendanceData attData = BackendChart.attendanceData.Where(r => r.DayNum.Equals(dayNum)).FirstOrDefault();
            //if(attData == null)
            //{
            //    int cnt = BackendChartData.attendanceData.Count;
            //    Debug.Log("SetTodayAttendanceSave attData is null , cnt : " + cnt);
            //}
            //Debug.Log($"SetTodayAttendanceSave stDt : {stDt.ToString("MM.dd")}, now : {now.ToString("MM.dd")}, gap : {gap}");
            //Debug.Log($"SetTodayAttendanceSave num : {dayNum} ...  {attData.DayNum}, {attData.RewardVal}");

            UserAttendanceData uatt_data = new UserAttendanceData();
            uatt_data.DayNum = dayNum;
            uatt_data.GiveDt = now.ToString("yyyy.MM.dd");
            uatt_data.GiveTs = nowTs;
            uatt_data.RewardVal = attData.RewardVal;
            uatt_data.RewardType = attData.RewardType;
            SetUserAttendanceExistsInsert(uatt_data);

            BackendChart.attendanceData.Where(r => r.DayNum > dayNum).ToList().ForEach(r => r.GiveVal = -1);

            //BackendChartData.attendanceData.ForEach(r => Debug.Log(r.ToString()));
        }


    }
}
