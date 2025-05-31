namespace Maroik.Common.Miscellaneous.Utilities
{
    public static class ServerSetting
    {
        public static string? DomainName { get; set; }
        public static string? SmtpUserName { get; set; }
        public static string? SmtpPassword { get; set; }
        public static string? SmtpHost { get; set; }
        public static int SmtpPort { get; set; }
        public static bool SmtpSSL { get; set; }
        public static string? FromEmail { get; set; }
        public static string? FromFullName { get; set; }
        public static bool IsDefault { get; set; }
        public static byte MaxLoginAttempt { get; set; }
        public static int SessionExpireMinutes { get; set; }
        public static int NoticeMaturityDateDay { get; set; }
        public static string? DockerCertPath { get; set; }
        public static string? DockerKeyPath { get; set; }
    }
}