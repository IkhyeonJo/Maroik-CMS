namespace Maroik.Common.Miscellaneous.Extensions
{
    public static class DateTimeExtension
    {
        public static DateTime ConvertTimeByTimeZoneIanaId(this DateTime value, string timeZoneIanaId)
        {
            try
            {
                return TimeZoneInfo.ConvertTimeBySystemTimeZoneId(value, timeZoneIanaId);
            }
            catch
            {
                return value;
            }
        }
    }
}