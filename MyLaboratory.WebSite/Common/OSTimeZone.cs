using System.Runtime.InteropServices;

namespace MyLaboratory.WebSite.Common
{
    public static class OSTimeZone
    {
        private static string destinationTimeZoneId;
        public static string DestinationTimeZoneId 
        {
            get
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    destinationTimeZoneId = "Korea Standard Time";
                }
                else
                {
                    destinationTimeZoneId = "Asia/Seoul";
                }

                return destinationTimeZoneId;
            } 
        }
    }
}