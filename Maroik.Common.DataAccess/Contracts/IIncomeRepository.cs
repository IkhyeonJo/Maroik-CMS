using Maroik.Common.DataAccess.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Maroik.Common.DataAccess.Contracts
{
    public interface IIncomeRepository
    {
        /// <summary>
        /// 로그인 계정에 해당하는 모든 수입을 구합니다.
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        public Task<List<Income>> GetIncomesAsync(string email);
        
        /// <summary>
        /// 로그인 계정에 해당하는 금년 수입을 구합니다.
        /// </summary>
        /// <param name="email"></param>
        /// <param name="year"></param>
        /// <param name="timeZoneIanaId"></param>
        /// <returns></returns>
        public Task<List<Income>> GetCurrentYearIncomesAsync(string email, string year, string timeZoneIanaId);

        /// <summary>
        /// 로그인 계정에 해당하는 금년월 수입을 구합니다.
        /// </summary>
        /// <param name="email"></param>
        /// <param name="yearMonth"></param>
        /// <param name="timeZoneIanaId"></param>
        /// <returns></returns>
        public Task<List<Income>> GetCurrentYearMonthIncomesAsync(string email, string yearMonth, string timeZoneIanaId);

        /// <summary>
        /// 수입을 생성합니다.
        /// </summary>
        /// <param name="income"></param>
        /// <returns></returns>
        public Task CreateIncomeAsync(Income income);

        /// <summary>
        /// 수입을 업데이트합니다.
        /// </summary>
        /// <param name="income"></param>
        /// <returns></returns>
        public Task UpdateIncomeAsync(Income income);

        /// <summary>
        /// 수입을 제거합니다.
        /// </summary>
        /// <param name="income"></param>
        /// <returns></returns>
        public Task DeleteIncomeAsync(Income income);
    }
}