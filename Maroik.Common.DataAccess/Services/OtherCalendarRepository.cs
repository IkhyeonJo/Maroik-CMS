using Maroik.Common.DataAccess.Contracts;
using Maroik.Common.DataAccess.Data;
using Maroik.Common.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Maroik.Common.DataAccess.Services
{
    public class OtherCalendarRepository : IOtherCalendarRepository
    {
        private readonly ApplicationDbContext _context;

        public OtherCalendarRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 모든 다른 달력을 구합니다.
        /// </summary>
        /// <returns></returns>
        public async Task<List<OtherCalendar>> GetAllOtherCalendarsAsync()
        {
            return await _context.OtherCalendars.OrderBy(x => x.CalendarId).ToListAsync();
        }

        /// <summary>
        /// 로그인 계정에 해당하는 모든 다른 달력을 구합니다.
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        public async Task<List<OtherCalendar>> GetOtherCalendarsAsync(string email)
        {
            return await _context.OtherCalendars.Where(x => x.AccountEmail == email).OrderBy(x => x.CalendarId).ToListAsync();
        }

        /// <summary>
        /// 다른 달력을 생성합니다.
        /// </summary>
        /// <param name="otherCalendar"></param>
        /// <returns></returns>
        public async Task CreateOtherCalendarAsync(OtherCalendar otherCalendar)
        {
            _ = await _context.OtherCalendars.AddAsync(otherCalendar);
            _ = await _context.SaveChangesAsync();
        }

        /// <summary>
        /// 다른 달력을 삭제합니다.
        /// </summary>
        /// <param name="otherCalendar"></param>
        /// <returns></returns>
        public async Task DeleteOtherCalendarAsync(OtherCalendar otherCalendar)
        {
            _ = _context.OtherCalendars.Remove(otherCalendar);
            _ = await _context.SaveChangesAsync();
        }
    }
}