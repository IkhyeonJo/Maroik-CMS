using Maroik.Common.DataAccess.Contracts;
using Maroik.Common.DataAccess.Data;
using Maroik.Common.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Maroik.Common.DataAccess.Services
{
    public class IncomeRepository(ApplicationDbContext context) : IIncomeRepository
    {
        /// <summary>
        /// 로그인 계정에 해당하는 모든 수입을 구합니다.
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        public async Task<List<Income>> GetIncomesAsync(string email)
        {
            return await context.Incomes.Where(x => x.AccountEmail == email).ToListAsync();
        }
        
        /// <summary>
        /// 로그인 계정에 해당하는 금년 수입을 구합니다.
        /// </summary>
        /// <param name="email"></param>
        /// <param name="year"></param>
        /// <param name="timeZoneIanaId"></param>
        /// <returns></returns>
        public async Task<List<Income>> GetCurrentYearIncomesAsync(string email, string year, string timeZoneIanaId)
        {
            return await context.Incomes.FromSqlInterpolated
            (
                $"""
                 SELECT *
                 FROM "Income"
                 WHERE 
                     TO_CHAR("Created" AT TIME ZONE 'UTC' AT TIME ZONE {timeZoneIanaId}, 'YYYY') = {year}
                     AND "AccountEmail" = {email}
                 """
            ).ToListAsync();
        }

        /// <summary>
        /// 로그인 계정에 해당하는 금년월 수입을 구합니다.
        /// </summary>
        /// <param name="email"></param>
        /// <param name="yearMonth"></param>
        /// <param name="timeZoneIanaId"></param>
        /// <returns></returns>
        public async Task<List<Income>> GetCurrentYearMonthIncomesAsync(string email, string yearMonth, string timeZoneIanaId)
        {
            return await context.Incomes.FromSqlInterpolated
            (
                $"""
                 SELECT *
                 FROM "Income"
                 WHERE 
                     TO_CHAR("Created" AT TIME ZONE 'UTC' AT TIME ZONE {timeZoneIanaId}, 'YYYY-MM') = {yearMonth}
                     AND "AccountEmail" = {email}
                 """
            ).ToListAsync();
        }

        /// <summary>
        /// 수입을 생성합니다.
        /// </summary>
        /// <param name="income"></param>
        /// <returns></returns>
        public async Task CreateIncomeAsync(Income income)
        {
            _ = await context.Incomes.AddAsync(income);
            _ = await context.SaveChangesAsync();
        }

        /// <summary>
        /// 수입을 업데이트합니다.
        /// </summary>
        /// <param name="income"></param>
        /// <returns></returns>
        public async Task UpdateIncomeAsync(Income income)
        {
            _ = context.Incomes.Update(income);
            _ = await context.SaveChangesAsync();
        }

        /// <summary>
        /// 수입을 제거합니다.
        /// </summary>
        /// <param name="income"></param>
        /// <returns></returns>
        public async Task DeleteIncomeAsync(Income income)
        {
            _ = context.Incomes.Remove(income);
            _ = await context.SaveChangesAsync();
        }
    }
}