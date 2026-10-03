using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Prack.BackSys.GameData
{
    [System.Serializable]
    public class UserAttendanceData
    {
        public string GiveDt;     // 2023.09.01
        public long GiveTs;
        public int DayNum;
        public int RewardType;
        public int RewardVal;

        public override string ToString()
        {
            string result = string.Empty;
            result += $"GiveDt : {GiveDt}, ";
            result += $"GiveTs : {GiveTs}, ";
            result += $"DayNum : {DayNum}, ";
            result += $"RewardType : {RewardType}, ";
            result += $"RewardVal : {RewardVal} ";
            return result;
        }
    }
}
