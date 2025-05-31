using Maroik.Common.Miscellaneous.Extensions;
using Maroik.Common.Miscellaneous.Utilities;
using Maroik.WebSite.Contracts;
using Maroik.WebSite.Models.ViewModels.Account;
using System.Net.Mail;

namespace Maroik.WebSite.Services
{
    public class MailRepository : IMailRepository
    {
        /// <summary>
        /// 계정 비밀번호 초기화 메일의 HttpBody 부분을 구합니다.
        /// </summary>
        /// <param name="loginInputViewModel"></param>
        /// <param name="title"></param>
        /// <param name="content0"></param>
        /// <param name="content1"></param>
        /// <returns></returns>
        public string GetMailConfirmationBody(LoginInputViewModel loginInputViewModel, string title, string content0, string content1)
        {
            string url = $"{ServerSetting.DomainName}Account/ConfirmEmail?registrationToken={Uri.EscapeDataString(loginInputViewModel.RegistrationToken)}";
            return @$"<div style='text-align:centre;'>
                        <h1>{title}<h1>
                        <h3>{content0}<h3>
                        <h3>{content1}<h3>
                        <a href='{url}' target='_blank' rel='noopener noreferrer'>{url}</a>
                      </div>";
        }
        /// <summary>
        /// 계정 확인 메일의 HttpBody 부분을 구합니다.
        /// </summary>
        /// <param name="loginInputViewModel"></param>
        /// <param name="title"></param>
        /// <param name="content0"></param>
        /// <param name="content1"></param>
        /// <returns></returns>
        public string GetMailResetPasswordBody(LoginInputViewModel loginInputViewModel, string title, string content0, string content1)
        {
            string url = $"{ServerSetting.DomainName}Account/ResetPassword?resetPasswordToken={Uri.EscapeDataString(loginInputViewModel.ResetPasswordToken)}";
            return @$"<div style='text-align:centre;'>
                        <h1>{title}<h1>
                        <h3>{content0}<h3>
                        <h3>{content1}<h3>
                        <a href='{url}' target='_blank' rel='noopener noreferrer'>{url}</a>
                      </div>";
        }
        /// <summary>
        /// 메일 전송
        /// </summary>
        /// <param name="mail"></param>
        /// <returns></returns>
        public async Task<string> SendMailAsync(Mail mail)
        {
            try
            {
                using MailMessage mailMessage = new();
                mailMessage.From = new MailAddress(mail.FromEmail, mail.FromFullName);
                mail.ToMailIds.ForEach(mailMessage.To.Add);
                mailMessage.Subject = mail.Subject;
                mailMessage.Body = mail.Body;
                mailMessage.IsBodyHtml = mail.IsBodyHTML;
                mail.Attachments.ForEach(x =>
                {
                    mailMessage.Attachments.Add(new Attachment(x));
                });

                using SmtpClient smtp = new(mail.SmtpHost, mail.SmtpPort);
                smtp.Credentials = new System.Net.NetworkCredential(mail.SmtpUserName, mail.SmtpPassword);
                smtp.EnableSsl = mail.SmtpSSL;
                await smtp.SendMailAsync(mailMessage);
                return EnumHelper.GetDescription(AccountMessage.MailSent);
            }
            catch (Exception ex)
            {

                return ex.Message;
            }
        }
    }
}