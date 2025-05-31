using Maroik.Common.DataAccess.Contracts;
using Maroik.Common.DataAccess.Data;
using Maroik.Common.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Maroik.Common.DataAccess.Services
{
    public class AccountRepository : IAccountRepository
    {
        private readonly ApplicationDbContext _context;

        public AccountRepository(ApplicationDbContext context)
        {
            _context = context;
        }
        /// <summary>
        /// 등록된 모든 계정을 구합니다.
        /// </summary>
        /// <returns></returns>
        public async Task<List<Account>> GetAllAsync()
        {
            return await _context.Accounts.ToListAsync();
        }
        /// <summary>
        /// 한 계정을 이메일로 구합니다.
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        public async Task<Account> GetAccountByEmailAsync(string email)
        {
            return await _context.Accounts.FirstOrDefaultAsync(x => x.Email == email);
        }
        /// <summary>
        /// 계정을 생성합니다.
        /// </summary>
        /// <param name="account"></param>
        /// <returns></returns>
        public async Task CreateAccountAsync(Account account)
        {
            _ = await _context.Accounts.AddAsync(account);
            _ = await _context.SaveChangesAsync();
        }
        /// <summary>
        /// 계정을 업데이트합니다.
        /// </summary>
        /// <param name="account"></param>
        /// <returns></returns>
        public async Task UpdateAccountAsync(Account account)
        {
            _ = _context.Accounts.Update(account);
            _ = await _context.SaveChangesAsync();
        }
    }
}