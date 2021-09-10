using System.Collections.Generic;
using System.Threading.Tasks;
using MyLaboratory.Common.DataAccess.Models;

namespace MyLaboratory.Common.DataAccess.Contracts
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
    }
}