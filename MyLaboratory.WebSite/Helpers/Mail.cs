using MyLaboratory.WebSite.Common;
using System.Collections.Generic;

namespace MyLaboratory.WebSite.Helpers
{
    public class Mail
    {
        public Mail()
        {
            this.SmtpUserName = ServerSetting.SmtpUserName;
            this.SmtpPassword = ServerSetting.SmtpPassword;
            this.SmtpHost = ServerSetting.SmtpHost;
            this.SmtpPort = ServerSetting.SmtpPort;
            this.SmtpSSL = ServerSetting.SmtpSSL;
            this.FromEmail = ServerSetting.FromEmail;
            this.FromFullName = ServerSetting.FromFullName;
            this.IsDefault = ServerSetting.IsDefault;
        }
        public string SmtpUserName { get; set; }
        public string SmtpPassword { get; set; }
        public string SmtpHost { get; set; }
        public int SmtpPort { get; set; }
        public bool SmtpSSL { get; set; }
        public string FromEmail { get; set; }
        public string FromFullName { get; set; }
        public bool IsDefault { get; set; }
        public List<string> ToMailIds { get; set; } = new List<string>();
        public string Subject { get; set; } = "";
        public string Body { get; set; } = "";
        public bool IsBodyHTML { get; set; } = true;
        public List<string> Attachments { get; set; } = new List<string>();
    }
}