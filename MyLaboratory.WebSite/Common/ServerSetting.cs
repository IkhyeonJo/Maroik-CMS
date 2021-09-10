namespace MyLaboratory.WebSite.Common
{
    public static class ServerSetting
    {
        public static string DomainName { get; set; }
        public static string SmtpUserName { get; set; }
        public static string SmtpPassword { get; set; }
        public static string SmtpHost { get; set; }
        public static int SmtpPort { get; set; }
        public static bool SmtpSSL { get; set; }
        public static string FromEmail { get; set; }
        public static string FromFullName { get; set; }
        public static bool IsDefault { get; set; }
        public static int MaxLoginAttempt { get; set; }
        public static int SessionExpireMinutes { get; set; }
        public static int NoticeMaturityDateDay { get; set; }
    }
}