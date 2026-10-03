namespace Assets.Scripts.Prack.BackSys.GameData
{
    [System.Serializable]
    public class UserAttendanceTermData
    {
        public long StTs;
        public long EnTs;
        public string DtInfo;

        public override string ToString()
        {
            string result = string.Empty;
            result += $"StTs : {StTs}, ";
            result += $"EnTs : {EnTs}, ";
            result += $"DtInfo : {DtInfo}, ";
            return result;
        }

    }


}
