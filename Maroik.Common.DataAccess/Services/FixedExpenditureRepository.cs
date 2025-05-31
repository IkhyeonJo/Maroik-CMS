using Maroik.Common.DataAccess.Contracts;
using Maroik.Common.DataAccess.Data;
using Maroik.Common.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Maroik.Common.DataAccess.Services
{
    public class FixedExpenditureRepository : IFixedExpenditureRepository
    {
        private readonly ApplicationDbContext _context;

        public FixedExpenditureRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 로그인 계정에 해당하는 모든 고정지출을 구합니다.
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        public async Task<List<FixedExpenditure>> GetFixedExpendituresAsync(string email)
        {
            return await _context.FixedExpenditures.Where(x => x.AccountEmail == email).ToListAsync();
        }

        /// <summary>
        /// 고정지출을 생성합니다.
        /// </summary>
        /// <param name="fixedExpenditure"></param>
        /// <returns></returns>
        public async Task CreateFixedExpenditureAsync(FixedExpenditure fixedExpenditure)
        {
            _ = await _context.FixedExpenditures.AddAsync(fixedExpenditure);
            _ = await _context.SaveChangesAsync();
        }

        /// <summary>
        /// 고정지출을 업데이트합니다.
        /// </summary>
        /// <param name="fixedExpenditure"></param>
        /// <returns></returns>
        public async Task UpdateFixedExpenditureAsync(FixedExpenditure fixedExpenditure)
        {
            _ = _context.FixedExpenditures.Update(fixedExpenditure);
            _ = await _context.SaveChangesAsync();
        }

        /// <summary>
        /// 고정지출을 제거합니다.
        /// </summary>
        /// <param name="fixedExpenditure"></param>
        /// <returns></returns>
        public async Task DeleteFixedExpenditureAsync(FixedExpenditure fixedExpenditure)
        {
            _ = _context.FixedExpenditures.Remove(fixedExpenditure);
            _ = await _context.SaveChangesAsync();
        }

        /// <summary>
        /// 내입금자산명을 업데이트합니다.
        /// </summary>
        /// <param name="accountEmail"></param>
        /// <param name="oldMyDepositAsset"></param>
        /// <param name="newMyDepositAsset"></param>
        /// <returns></returns>
        public async Task<int> UpdateMyDepositAssetWithProductNameAsync(string accountEmail, string oldMyDepositAsset, string newMyDepositAsset)
        {
            return await _context.Database.ExecuteSqlInterpolatedAsync
            (
                $"""
                 UPDATE "FixedExpenditure"
                 SET "MyDepositAsset" = {newMyDepositAsset}
                 WHERE "MyDepositAsset" = {oldMyDepositAsset} AND "AccountEmail" = {accountEmail}
                 """
            );
        }
    }
}