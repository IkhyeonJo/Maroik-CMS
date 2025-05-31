using Maroik.Common.DataAccess.Contracts;
using Maroik.Common.DataAccess.Data;
using Maroik.Common.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Maroik.Common.DataAccess.Services
{
    public class ExpenditureRepository : IExpenditureRepository
    {
        private readonly ApplicationDbContext _context;

        public ExpenditureRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 로그인 계정에 해당하는 모든 지출을 구합니다.
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        public async Task<List<Expenditure>> GetExpendituresAsync(string email)
        {
            return await _context.Expenditures.Where(x => x.AccountEmail == email).ToListAsync();
        }

        /// <summary>
        /// 로그인 계정에 해당하는 금년 지출을 구합니다.
        /// </summary>
        /// <param name="email"></param>
        /// <param name="year"></param>
        /// <param name="timeZoneIanaId"></param>
        /// <returns></returns>
        public async Task<List<Expenditure>> GetCurrentYearExpendituresAsync(string email, string year, string timeZoneIanaId)
        {
            return await _context.Expenditures.FromSqlInterpolated
            (
                $"""
                 SELECT *
                 FROM "Expenditure"
                 WHERE 
                     TO_CHAR("Created" AT TIME ZONE 'UTC' AT TIME ZONE {timeZoneIanaId}, 'YYYY') = {year}
                     AND "AccountEmail" = {email}
                 """
            ).ToListAsync();
        }

        /// <summary>
        /// 로그인 계정에 해당하는 금년월 지출을 구합니다.
        /// </summary>
        /// <param name="email"></param>
        /// <param name="yearMonth"></param>
        /// <param name="timeZoneIanaId"></param>
        /// <returns></returns>
        public async Task<List<Expenditure>> GetCurrentYearMonthExpendituresAsync(string email, string yearMonth, string timeZoneIanaId)
        {
            return await _context.Expenditures.FromSqlInterpolated
            (
                $"""
                 SELECT *
                 FROM "Expenditure"
                 WHERE 
                     TO_CHAR("Created" AT TIME ZONE 'UTC' AT TIME ZONE {timeZoneIanaId}, 'YYYY-MM') = {yearMonth}
                     AND "AccountEmail" = {email}
                 """
            ).ToListAsync();
        }

        /// <summary>
        /// 지출을 생성합니다.
        /// </summary>
        /// <param name="expenditure"></param>
        /// <returns></returns>
        public async Task CreateExpenditureAsync(Expenditure expenditure)
        {
            _ = await _context.Expenditures.AddAsync(expenditure);
            _ = await _context.SaveChangesAsync();
        }

        /// <summary>
        /// 지출을 업데이트합니다.
        /// </summary>
        /// <param name="expenditure"></param>
        /// <returns></returns>
        public async Task UpdateExpenditureAsync(Expenditure expenditure)
        {
            _ = _context.Expenditures.Update(expenditure);
            _ = await _context.SaveChangesAsync();
        }

        /// <summary>
        /// 지출을 제거합니다.
        /// </summary>
        /// <param name="expenditure"></param>
        /// <returns></returns>
        public async Task DeleteExpenditureAsync(Expenditure expenditure)
        {
            _ = _context.Expenditures.Remove(expenditure);
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
                  UPDATE "Expenditure"
                  SET "MyDepositAsset" = {newMyDepositAsset}
                  WHERE "MyDepositAsset" = {oldMyDepositAsset} AND "AccountEmail" = {accountEmail}
                  """
            );
        }
    }
}