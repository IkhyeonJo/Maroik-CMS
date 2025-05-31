using Maroik.Common.DataAccess.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Maroik.Common.DataAccess.Contracts
{
    public interface ICalendarSharedRepository
    {
        /// <summary>
        /// 모든 공유 달력을 구합니다.
        /// </summary>
        /// <returns></returns>
        public Task<List<CalendarShared>> GetCalendarSharedAsync();

        /// <summary>
        /// 공유 달력을 생성합니다.
        /// </summary>
        /// <param name="calendarShared"></param>
        /// <returns></returns>
        public Task CreateCalendarSharedAsync(CalendarShared calendarShared);

        /// <summary>
        /// 공유 달력을 업데이트합니다.
        /// </summary>
        /// <param name="calendarShared"></param>
        /// <returns></returns>
        public Task UpdateCalendarSharedAsync(CalendarShared calendarShared);
    }
}