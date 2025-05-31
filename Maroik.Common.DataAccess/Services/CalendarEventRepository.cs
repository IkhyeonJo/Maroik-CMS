using Maroik.Common.DataAccess.Contracts;
using Maroik.Common.DataAccess.Data;
using Maroik.Common.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Maroik.Common.DataAccess.Services
{
    public class CalendarEventRepository : ICalendarEventRepository
    {
        private readonly ApplicationDbContext _context;

        public CalendarEventRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 해당 달력에 해당하는 모든 달력 이벤트를 구합니다.
        /// </summary>
        /// <param name="calendarId"></param>
        /// <returns></returns>
        public async Task<IEnumerable<CalendarEvent>> GetCalendarEventsAsync(long calendarId)
        {
            return await _context.CalendarEvents.Where(x => x.CalendarId == calendarId).ToListAsync();
        }

        /// <summary>
        /// 한 달력 이벤트를 구합니다.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<CalendarEvent> GetCalendarEventAsync(long id)
        {
            return await _context.CalendarEvents.Where(x => x.Id == id).FirstOrDefaultAsync();
        }

        /// <summary>
        /// 달력 이벤트를 생성합니다.
        /// </summary>
        /// <param name="calendarEvent"></param>
        /// <returns></returns>
        public async Task CreateCalendarEventAsync(CalendarEvent calendarEvent)
        {
            _ = await _context.CalendarEvents.AddAsync(calendarEvent);
            _ = await _context.SaveChangesAsync();
        }

        /// <summary>
        /// 달력 이벤트를 업데이트합니다.
        /// </summary>
        /// <param name="calendarEvent"></param>
        /// <returns></returns>
        public async Task UpdateCalendarEventAsync(CalendarEvent calendarEvent)
        {
            _ = _context.CalendarEvents.Update(calendarEvent);
            _ = await _context.SaveChangesAsync();
        }

        /// <summary>
        /// 달력 이벤트를 삭제합니다.
        /// </summary>
        /// <param name="calendarEvent"></param>
        /// <returns></returns>
        public async Task DeleteCalendarEventAsync(CalendarEvent calendarEvent)
        {
            _ = _context.CalendarEvents.Remove(calendarEvent);
            _ = await _context.SaveChangesAsync();
        }
    }
}