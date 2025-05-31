using Maroik.Common.DataAccess.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Maroik.Common.DataAccess.Contracts
{
    public interface IFixedExpenditureRepository
    {
        /// <summary>
        /// 로그인 계정에 해당하는 모든 고정지출을 구합니다.
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        public Task<List<FixedExpenditure>> GetFixedExpendituresAsync(string email);

        /// <summary>
        /// 고정지출을 생성합니다.
        /// </summary>
        /// <param name="fixedExpenditure"></param>
        /// <returns></returns>
        public Task CreateFixedExpenditureAsync(FixedExpenditure fixedExpenditure);

        /// <summary>
        /// 고정지출을 업데이트합니다.
        /// </summary>
        /// <param name="fixedExpenditure"></param>
        /// <returns></returns>
        public Task UpdateFixedExpenditureAsync(FixedExpenditure fixedExpenditure);

        /// <summary>
        /// 고정지출을 제거합니다.
        /// </summary>
        /// <param name="fixedExpenditure"></param>
        /// <returns></returns>
        public Task DeleteFixedExpenditureAsync(FixedExpenditure fixedExpenditure);

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