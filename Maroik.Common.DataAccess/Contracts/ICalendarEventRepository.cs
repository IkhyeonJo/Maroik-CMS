using Maroik.Common.DataAccess.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Maroik.Common.DataAccess.Contracts
{
    public interface ICalendarEventRepository
    {
        /// <summary>
        /// 해당 달력에 해당하는 모든 달력 이벤트를 구합니다.
        /// </summary>
        /// <param name="calendarId"></param>
        /// <returns></returns>
        public Task<IEnumerable<CalendarEvent>> GetCalendarEventsAsync(long calendarId);

        /// <summary>
        /// 한 달력 이벤트를 구합니다.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public Task<CalendarEvent> GetCalendarEventAsync(long id);

        /// <summary>
        /// 달력 이벤트를 생성합니다.
        /// </summary>
        /// <param name="calendarEvent"></param>
        /// <returns></returns>
        public Task CreateCalendarEventAsync(CalendarEvent calendarEvent);

        /// <summary>
        /// 달력 이벤트를 업데이트합니다.
        /// </summary>
        /// <param name="calendarEvent"></param>
        /// <returns></returns>
        public Task UpdateCalendarEventAsync(CalendarEvent calendarEvent);

        /// <summary>
        /// 달력 이벤트를 삭제합니다.
        /// </summary>
        /// <param name="calendarEvent"></param>
        /// <returns></returns>
        public Task DeleteCalendarEventAsync(CalendarEvent calendarEvent);
    }
}