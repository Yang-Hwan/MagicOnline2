using System;

namespace Assets.Scripts.Often
{
    public class DateHandle
    {
        public static long GetDateToStamp(DateTime dt)
        {
            return ((DateTimeOffset)dt).ToUnixTimeSeconds();
        }

        public static DateTime GetStampToDate(long val)
        {
            DateTime dt = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dt = dt.AddSeconds(val).ToLocalTime();
            return dt;
        }


        public static long GetCurDtToStamp()
        {
            DateTime dt = DateTime.Now;
            return ((DateTimeOffset)dt).ToUnixTimeSeconds();
        }

    }
}
