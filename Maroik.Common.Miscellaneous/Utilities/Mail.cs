namespace Maroik.Common.Miscellaneous.Utilities
{
    public class Mail
    {
        public Mail()
        {
            SmtpUserName = ServerSetting.SmtpUserName ?? string.Empty;
            SmtpPassword = ServerSetting.SmtpPassword ?? string.Empty;
            SmtpHost = ServerSetting.SmtpHost ?? string.Empty;
            SmtpPort = ServerSetting.SmtpPort;
            SmtpSSL = ServerSetting.SmtpSSL;
            FromEmail = ServerSetting.FromEmail ?? string.Empty;
            FromFullName = ServerSetting.FromFullName ?? string.Empty;
            IsDefault = ServerSetting.IsDefault;
        }
        public string SmtpUserName { get; set; }
        public string SmtpPassword { get; set; }
        public string SmtpHost { get; set; }
        public int SmtpPort { get; set; }
        public bool SmtpSSL { get; set; }
        public string FromEmail { get; set; }
        public string FromFullName { get; set; }
        public bool IsDefault { get; set; }
        public List<string> ToMailIds { get; set; } = [];
        public string Subject { get; set; } = "";
        public string Body { get; set; } = "";
        public bool IsBodyHTML { get; set; } = true;
        public List<string> Attachments { get; set; } = [];
    }
}