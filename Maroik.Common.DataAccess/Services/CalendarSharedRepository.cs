using Maroik.Common.DataAccess.Contracts;
using Maroik.Common.DataAccess.Data;
using Maroik.Common.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Maroik.Common.DataAccess.Services
{
    public class CalendarSharedRepository : ICalendarSharedRepository
    {
        private readonly ApplicationDbContext _context;

        public CalendarSharedRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 모든 공유 달력을 구합니다.
        /// </summary>
        /// <returns></returns>
        public async Task<List<CalendarShared>> GetCalendarSharedAsync()
        {
            return await _context.CalendarShareds.OrderBy(x => x.CalendarId).ToListAsync();
        }

        /// <summary>
        /// 공유 달력을 생성합니다.
        /// </summary>
        /// <param name="calendarShared"></param>
        /// <returns></returns>
        public async Task CreateCalendarSharedAsync(CalendarShared calendarShared)
        {
            _ = await _context.CalendarShareds.AddAsync(calendarShared);
            _ = await _context.SaveChangesAsync();
        }

        /// <summary>
        /// 공유 달력을 업데이트합니다.
        /// </summary>
        /// <param name="calendarShared"></param>
        /// <returns></returns>
        public async Task UpdateCalendarSharedAsync(CalendarShared calendarShared)
        {
            _ = _context.CalendarShareds.Update(calendarShared);
            _ = await _context.SaveChangesAsync();
        }
    }
}