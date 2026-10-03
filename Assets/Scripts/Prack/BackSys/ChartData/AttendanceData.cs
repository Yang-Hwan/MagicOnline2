namespace Assets.Scripts.Prack.BackSys.ChartData
{
    [System.Serializable]
    public class AttendanceData
    {
        public int DayNum;
        public int RewardType;
        public int RewardVal;
        public int GiveVal;

        public override string ToString()
        {
            string result = string.Empty;
            result += $"DayNum : {DayNum}, ";
            result += $"RewardType : {RewardType}, ";
            result += $"RewardVal : {RewardVal}, ";
            result += $"GiveVal : {GiveVal} ";
            return result;
        }
    }
}
