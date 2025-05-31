namespace Maroik.WebSite.Models.ViewModels.Calendar
{
    public class CalendarEventInputViewModel
    {
        public int Id { get; set; }
        public int CalendarId { get; set; }
        public string Title { get; set; }
        public bool AllDay { get; set; }
        public string StartDate { get; set; }
        public string EndDate { get; set; }
        public string StartDateTimeZoneIanaId { get; set; }
        public string EndDateTimeZoneIanaId { get; set; }
        public string Location { get; set; }
        public string Description { get; set; }
        public IFormFile CalendarEventUploadedFile { get; set; }
        public string Status { get; set; }
        public string SerializedCalendarReminders { get; set; }
    }
}