using Maroik.Common.DataAccess.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Maroik.Common.DataAccess.Contracts
{
    public interface IOtherCalendarRepository
    {
        /// <summary>
        /// 모든 다른 달력을 구합니다.
        /// </summary>
        /// <returns></returns>
        public Task<List<OtherCalendar>> GetAllOtherCalendarsAsync();

        /// <summary>
        /// 로그인 계정에 해당하는 모든 다른 달력을 구합니다.
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        public Task<List<OtherCalendar>> GetOtherCalendarsAsync(string email);

        /// <summary>
        /// 다른 달력을 생성합니다.
        /// </summary>
        /// <param name="otherCalendar"></param>
        /// <returns></returns>
        public Task CreateOtherCalendarAsync(OtherCalendar otherCalendar);

        /// <summary>
        /// 다른 달력을 삭제합니다.
        /// </summary>
        /// <param name="otherCalendar"></param>
        /// <returns></returns>
        public Task DeleteOtherCalendarAsync(OtherCalendar otherCalendar);
    }
}