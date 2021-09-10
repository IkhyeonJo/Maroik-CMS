using System.Collections.Generic;
using System.Threading.Tasks;
using MyLaboratory.Common.DataAccess.Models;

namespace MyLaboratory.Common.DataAccess.Contracts
{
    public interface IFixedIncomeRepository
    {
        /// <summary>
        /// 로그인 계정에 해당하는 모든 고정수입을 구합니다.
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        public Task<List<FixedIncome>> GetFixedIncomesAsync(string email);

        /// <summary>
        /// 고정수입을 생성합니다.
        /// </summary>
        /// <param name="fixedIncome"></param>
        /// <returns></returns>
        public Task CreateFixedIncomeAsync(FixedIncome fixedIncome);

        /// <summary>
        /// 고정수입을 업데이트합니다.
        /// </summary>
        /// <param name="fixedIncome"></param>
        /// <returns></returns>
        public Task UpdateFixedIncomeAsync(FixedIncome fixedIncome);

        /// <summary>
        /// 고정수입을 제거합니다.
        /// </summary>
        /// <param name="fixedIncome"></param>
        /// <returns></returns>
        public Task DeleteFixedIncomeAsync(FixedIncome fixedIncome);
    }
}