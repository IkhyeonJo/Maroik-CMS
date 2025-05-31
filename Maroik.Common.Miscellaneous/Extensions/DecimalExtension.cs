namespace Maroik.Common.Miscellaneous.Extensions
{
    public static class DecimalExtension
    {
        public static string TrimTrailingZeros(this decimal value)
        {
            try
            {
                return value % 1 == 0 ? ((int)value).ToString() : value.ToString().TrimEnd('0').TrimEnd('.');
            }
            catch
            {
                return default(decimal).ToString();
            }
        }

        public static string TrimTrailingZeros(this decimal? value)
        {
            try
            {
                return value % 1 == 0 ? ((int)(value ?? 0)).ToString() : (value ?? 0).ToString().TrimEnd('0').TrimEnd('.');
            }
            catch
            {
                return default(decimal).ToString();
            }
        }
    }
}