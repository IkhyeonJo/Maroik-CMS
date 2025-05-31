using Maroik.Common.DataAccess.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Maroik.Common.DataAccess.Contracts
{
    public interface ICalendarEventAttachedFileRepository
    {
        /// <summary>
        /// 등록된 모든 파일을 구합니다.
        /// </summary>
        /// <returns></returns>
        public Task<List<CalendarEventAttachedFile>> GetAllAsync();

        /// <summary>
        /// 한 첨부파일을 구합니다.
        /// </summary>
        /// <param name="calendarEventId"></param>
        /// <returns></returns>
        public Task<CalendarEventAttachedFile> GetCalendarEventAttachedFileAsync(long calendarEventId);

        /// <summary>
        /// 첨부파일을 저장합니다.
        /// </summary>
        /// <param name="calendarEventAttachedFile"></param>
        /// <returns></returns>
        public Task SaveCalendarEventAttachedFileAsync(CalendarEventAttachedFile calendarEventAttachedFile);

        /// <summary>
        /// 첨부파일을 업데이트합니다.
        /// </summary>
        /// <param name="calendarEventAttachedFile"></param>
        /// <returns></returns>
        public Task UpdateCalendarEventAttachedFileAsync(CalendarEventAttachedFile calendarEventAttachedFile);

        /// <summary>
        /// 첨부파일들을 삭제합니다.
        /// </summary>
        /// <param name="calendarEventId"></param>
        /// <returns></returns>
        public Task<int> DeleteCalendarEventAttachedFileAsync(long calendarEventId);
    }
}