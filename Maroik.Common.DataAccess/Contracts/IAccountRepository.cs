using Maroik.Common.DataAccess.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Maroik.Common.DataAccess.Contracts
{
    public interface IAccountRepository
    {
        /// <summary>
        /// 등록된 모든 계정을 구합니다.
        /// </summary>
        /// <returns></returns>
        public Task<List<Account>> GetAllAsync();
        /// <summary>
        /// 한 계정을 이메일로 구합니다.
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        public Task<Account> GetAccountByEmailAsync(string email);
        /// <summary>
        /// 계정을 생성합니다.
        /// </summary>
        /// <param name="account"></param>
        /// <returns></returns>
        public Task CreateAccountAsync(Account account);
        /// <summary>
        /// 계정을 업데이트합니다.
        /// </summary>
        /// <param name="account"></param>
        /// <returns></returns>
        public Task UpdateAccountAsync(Account account);
    }
}