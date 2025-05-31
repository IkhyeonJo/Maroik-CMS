using Maroik.Common.DataAccess.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Maroik.Common.DataAccess.Contracts
{
    public interface ICalendarEventReminderRepository
    {
        /// <summary>
        /// 해당 달력 이벤트에 해당하는 모든 달력 이벤트 알림을 구합니다.
        /// </summary>
        /// <param name="calendarEventId"></param>
        /// <returns></returns>
        public Task<IEnumerable<CalendarEventReminder>> GetCalendarEventRemindersAsync(long calendarEventId);

        /// <summary>
        /// 달력 이벤트 알림을 생성합니다.
        /// </summary>
        /// <param name="calendarEventReminder"></param>
        /// <returns></returns>
        public Task CreateCalendarEventReminderAsync(CalendarEventReminder calendarEventReminder);

        /// <summary>
        /// 달력 이벤트 알림들을 삭제합니다.
        /// </summary>
        /// <param name="calendarEventId"></param>
        /// <returns></returns>
        public Task<int> DeleteCalendarEventReminderAsync(long calendarEventId);
    }
}