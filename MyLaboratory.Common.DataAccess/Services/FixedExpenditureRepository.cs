using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyLaboratory.Common.DataAccess.Data;
using MyLaboratory.Common.DataAccess.Models;
using MyLaboratory.Common.DataAccess.Contracts;

namespace MyLaboratory.Common.DataAccess.Services
{
    public class FixedExpenditureRepository : IFixedExpenditureRepository
    {
        private readonly ApplicationDbContext context;

        public FixedExpenditureRepository(ApplicationDbContext context)
        {
            this.context = context;
        }

        /// <summary>
        /// 로그인 계정에 해당하는 모든 고정지출을 구합니다.
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        public async Task<List<FixedExpenditure>> GetFixedExpendituresAsync(string email)
        {
            return await context.FixedExpenditures.Where(x => x.AccountEmail == email).ToListAsync();
        }

        /// <summary>
        /// 고정지출을 생성합니다.
        /// </summary>
        /// <param name="fixedExpenditure"></param>
        /// <returns></returns>
        public async Task CreateFixedExpenditureAsync(FixedExpenditure fixedExpenditure)
        {
            await context.FixedExpenditures.AddAsync(fixedExpenditure);
            await context.SaveChangesAsync();
        }

        /// <summary>
        /// 고정지출을 업데이트합니다.
        /// </summary>
        /// <param name="fixedExpenditure"></param>
        /// <returns></returns>
        public async Task UpdateFixedExpenditureAsync(FixedExpenditure fixedExpenditure)
        {
            context.FixedExpenditures.Update(fixedExpenditure);
            await context.SaveChangesAsync();
        }

        /// <summary>
        /// 고정지출을 제거합니다.
        /// </summary>
        /// <param name="fixedExpenditure"></param>
        /// <returns></returns>
        public async Task DeleteFixedExpenditureAsync(FixedExpenditure fixedExpenditure)
        {
            context.FixedExpenditures.Remove(fixedExpenditure);
            await context.SaveChangesAsync();
        }
    }
}