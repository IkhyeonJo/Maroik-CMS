using Maroik.Common.DataAccess.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Maroik.Common.DataAccess.Contracts
{
    public interface IExpenditureRepository
    {
        /// <summary>
        /// 로그인 계정에 해당하는 모든 지출을 구합니다.
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        public Task<List<Expenditure>> GetExpendituresAsync(string email);

        /// <summary>
        /// 로그인 계정에 해당하는 금년 지출을 구합니다.
        /// </summary>
        /// <param name="email"></param>
        /// <param name="year"></param>
        /// <param name="timeZoneIanaId"></param>
        /// <returns></returns>
        public Task<List<Expenditure>> GetCurrentYearExpendituresAsync(string email, string year, string timeZoneIanaId);
        
        /// <summary>
        /// 로그인 계정에 해당하는 금년월 지출을 구합니다.
        /// </summary>
        /// <param name="email"></param>
        /// <param name="yearMonth"></param>
        /// <param name="timeZoneIanaId"></param>
        /// <returns></returns>
        public Task<List<Expenditure>> GetCurrentYearMonthExpendituresAsync(string email, string yearMonth, string timeZoneIanaId);

        /// <summary>
        /// 지출을 생성합니다.
        /// </summary>
        /// <param name="expenditure"></param>
        /// <returns></returns>
        public Task CreateExpenditureAsync(Expenditure expenditure);

        /// <summary>
        /// 지출을 업데이트합니다.
        /// </summary>
        /// <param name="expenditure"></param>
        /// <returns></returns>
        public Task UpdateExpenditureAsync(Expenditure expenditure);

        /// <summary>
        /// 지출을 제거합니다.
        /// </summary>
        /// <param name="expenditure"></param>
        /// <returns></returns>
        public Task DeleteExpenditureAsync(Expenditure expenditure);

        /// <summary>
        /// 내입금자산명을 업데이트합니다.
        /// </summary>
        /// <param name="accountEmail"></param>
        /// <param name="oldMyDepositAsset"></param>
        /// <param name="newMyDepositAsset"></param>
        /// <returns></returns>
        public Task<int> UpdateMyDepositAssetWithProductNameAsync(string accountEmail, string oldMyDepositAsset, string newMyDepositAsset);
    }
}