using MyLaboratory.WebSite.Helpers;
using MyLaboratory.WebSite.Models.ViewModels.Account;
using System.Threading.Tasks;

namespace MyLaboratory.WebSite.Contracts
{
    public interface IMailRepository
    {
        /// <summary>
        /// 계정 비밀번호 초기화 메일의 HttpBody 부분을 구합니다.
        /// </summary>
        /// <param name="loginInputViewModel"></param>
        /// <param name="title"></param>
        /// <param name="content0"></param>
        /// <param name="content1"></param>
        /// <returns></returns>
        public string GetMailResetPasswordBody(LoginInputViewModel loginInputViewModel, string title, string content0, string content1);
        /// <summary>
        /// 계정 확인 메일의 HttpBody 부분을 구합니다.
        /// </summary>
        /// <param name="loginInputViewModel"></param>
        /// <param name="title"></param>
        /// <param name="content0"></param>
        /// <param name="content1"></param>
        /// <returns></returns>
        public string GetMailConfirmationBody(LoginInputViewModel loginInputViewModel, string title, string content0, string content1);
        /// <summary>
        /// 메일 전송
        /// </summary>
        /// <param name="mail"></param>
        /// <returns></returns>
        public Task<string> SendMailAsync(Mail mail);
    }
}