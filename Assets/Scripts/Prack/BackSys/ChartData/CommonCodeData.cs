namespace Assets.Scripts.Prack.BackSys.ChartData
{
    [System.Serializable]
    public class CommonCodeData
    {
        public string GrpCd;
        public string ComCd;
        public int ComVal;
        public int ComVal2;
        public string Info;
        public string IsUse;

        public override string ToString()
        {
            string result = string.Empty;
            result += $"GrpCd : {GrpCd}, ";
            result += $"ComCd : {ComCd}, ";
            result += $"ComVal2 : {ComVal2}, ";
            result += $"Info : {Info}, ";
            result += $"IsUse : {IsUse}";

            return result;
        }
    }

}
