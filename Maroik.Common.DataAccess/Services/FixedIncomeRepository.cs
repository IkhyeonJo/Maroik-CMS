using Maroik.Common.DataAccess.Contracts;
using Maroik.Common.DataAccess.Data;
using Maroik.Common.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Maroik.Common.DataAccess.Services
{
    public class FixedIncomeRepository : IFixedIncomeRepository
    {
        private readonly ApplicationDbContext _context;

        public FixedIncomeRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 로그인 계정에 해당하는 모든 고정수입을 구합니다.
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        public async Task<List<FixedIncome>> GetFixedIncomesAsync(string email)
        {
            return await _context.FixedIncomes.Where(x => x.AccountEmail == email).ToListAsync();
        }

        /// <summary>
        /// 고정수입을 생성합니다.
        /// </summary>
        /// <param name="fixedIncome"></param>
        /// <returns></returns>
        public async Task CreateFixedIncomeAsync(FixedIncome fixedIncome)
        {
            _ = await _context.FixedIncomes.AddAsync(fixedIncome);
            _ = await _context.SaveChangesAsync();
        }

        /// <summary>
        /// 고정수입을 업데이트합니다.
        /// </summary>
        /// <param name="fixedIncome"></param>
        /// <returns></returns>
        public async Task UpdateFixedIncomeAsync(FixedIncome fixedIncome)
        {
            _ = _context.FixedIncomes.Update(fixedIncome);
            _ = await _context.SaveChangesAsync();
        }

        /// <summary>
        /// 고정수입을 제거합니다.
        /// </summary>
        /// <param name="fixedIncome"></param>
        /// <returns></returns>
        public async Task DeleteFixedIncomeAsync(FixedIncome fixedIncome)
        {
            _ = _context.FixedIncomes.Remove(fixedIncome);
            _ = await _context.SaveChangesAsync();
        }
    }
}