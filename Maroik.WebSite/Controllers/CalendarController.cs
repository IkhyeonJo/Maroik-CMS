using Ganss.Xss;
using HtmlAgilityPack;
using Maroik.Common.DataAccess.Contracts;
using Maroik.Common.DataAccess.Models;
using Maroik.Common.Miscellaneous.Extensions;
using Maroik.Common.Miscellaneous.Utilities;
using Maroik.WebSite.Contracts;
using Maroik.WebSite.Models.ViewModels.Calendar;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.StaticFiles;
using Newtonsoft.Json;
using System.Text;
using ImageMagick;

namespace Maroik.WebSite.Controllers
{
    public class CalendarController : Controller
    {
        private readonly RSA _rsa;
        private readonly IHtmlLocalizer<CalendarController> _localizer;
        private readonly ILogger<CalendarController> _logger;
        private readonly IIncomeRepository _incomeRepository;
        private readonly IExpenditureRepository _expenditureRepository;
        private readonly IAssetRepository _assetRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly ICalendarRepository _calendarRepository;
        private readonly IOtherCalendarRepository _otherCalendarRepository;
        private readonly ICalendarEventRepository _calendarEventRepository;
        private readonly ICalendarEventAttachedFileRepository _calendarEventAttachedFileRepository;
        private readonly ICalendarEventReminderRepository _calendarReminderRepository;
        private readonly ICalendarSharedRepository _calendarSharedRepository;
        private readonly IHostEnvironment _hostEnvironment;
        private readonly IFileRepository _fileRepository;

        public CalendarController(IHtmlLocalizer<CalendarController> localizer, ILogger<CalendarController> logger, IHostEnvironment hostEnvironment, IIncomeRepository incomeRepository, IExpenditureRepository expenditureRepository, IAssetRepository assetRepository, IAccountRepository accountRepository, ICalendarRepository calendarRepository, IOtherCalendarRepository otherCalendarRepository, ICalendarEventRepository calendarEventRepository, ICalendarEventAttachedFileRepository calendarEventAttachedFileRepository, ICalendarEventReminderRepository calendarReminderRepository, ICalendarSharedRepository calendarSharedRepository, IFileRepository fileRepository)
        {
            _rsa = new RSA(RSAType.RSA2, Encoding.UTF8, RSA.privateKey, RSA.publicKey);
            _localizer = localizer;
            _logger = logger;
            _hostEnvironment = hostEnvironment;
            _incomeRepository = incomeRepository;
            _expenditureRepository = expenditureRepository;
            _assetRepository = assetRepository;
            _accountRepository = accountRepository;
            _calendarRepository = calendarRepository;
            _otherCalendarRepository = otherCalendarRepository;
            _calendarEventRepository = calendarEventRepository;
            _calendarEventAttachedFileRepository = calendarEventAttachedFileRepository;
            _calendarReminderRepository = calendarReminderRepository;
            _calendarSharedRepository = calendarSharedRepository;
            _fileRepository = fileRepository;
        }

        #region Calendar

        #region Create

        #region Calendar
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.Admin)]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> CreateCalendar([FromBody] CalendarInputViewModel calendarInputViewModel)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    try
                    {
                        _ = HttpContext.Session.TryGetValue(Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous.Utilities.Session.Account), out byte[] resultByte);

                        Calendar tempCalendar = calendarInputViewModel?.Calendars?.FirstOrDefault() ?? new Calendar();
                        tempCalendar.AccountEmail = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email;
                        tempCalendar.Created = DateTime.UtcNow;
                        tempCalendar.Updated = DateTime.UtcNow;

                        Calendar tempCalendarToValidate = (await _calendarRepository.GetCalendarsAsync(tempCalendar.AccountEmail)).Where(x => x.Name == (tempCalendar?.Name ?? "")).FirstOrDefault();

                        if (tempCalendarToValidate is not null)
                        {
                            return Json(new { result = false, error = _localizer["The calendar already exists."].Value });
                        }

                        await _calendarRepository.CreateCalendarAsync(tempCalendar);

                        tempCalendar.AccountEmail = null;
                        tempCalendar.Description = null;
                        tempCalendar.TimeZoneIanaId = null;

                        return Json(new { result = true, message = _localizer["The calendar has been successfully created."].Value, calendar = tempCalendar });
                    }
                    catch
                    {
                        return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                    }
                }
                else
                {
                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                }
            }
            catch
            {
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
            }
        }
        #endregion

        #region Calendar Event
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.Admin)]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> CreateCalendarEvent([FromForm] CalendarEventInputViewModel calendarEventInputViewModel)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    calendarEventInputViewModel.Description ??= ""; // 내용이 아무것도 입력되지 않았다면

                    #region HtmlSanitizer
                    HtmlSanitizer sanitizer = new();
                    _ = sanitizer.AllowedAttributes.Add("class");
                    string sanitized = sanitizer.Sanitize(calendarEventInputViewModel.Description);
                    calendarEventInputViewModel.Description = sanitized;
                    #endregion

                    #region Decrypt img tag file path
                    HtmlDocument htmlDocument = new();
                    htmlDocument.LoadHtml(calendarEventInputViewModel.Description);

                    foreach (HtmlNode imgTag in htmlDocument.DocumentNode.Descendants("img") ?? [])
                    {
                        string encryptedFilePath = imgTag.GetAttributeValue("alt", "");
                        _ = imgTag.SetAttributeValue("alt", _rsa.Decrypt(encryptedFilePath));
                    }

                    calendarEventInputViewModel.Description = htmlDocument.DocumentNode.OuterHtml;
                    #endregion

                    if (calendarEventInputViewModel.Title.Length is not (> 0 and <= 100)) // 제목 길이
                    {
                        return Json(new { result = false, error = _localizer["Name length is must be between 1 and 100 characters."].Value });
                    }

                    if (calendarEventInputViewModel.Description.Length > 16384) // Description Maxlength: 16KB
                    {
                        return Json(new { result = false, error = _localizer["Description length is must be between 0 and 16384 characters."].Value });
                    }

                    if (calendarEventInputViewModel.CalendarEventUploadedFile != null) // 파일 첨부가 되었다면
                    {
                        if (!(Path.GetExtension(calendarEventInputViewModel.CalendarEventUploadedFile.FileName) == ".zip")) // zip 확장자만 가능
                        {
                            return Json(new { result = false, error = _localizer["Only zip extension allowed."].Value });
                        }

                        if (calendarEventInputViewModel.CalendarEventUploadedFile.Length is not (> 0 and <= 10485760)) // 10MB 이하만 가능
                        {
                            return Json(new { result = false, error = _localizer["uploaded file size must be smaller than 10MB."].Value });
                        }
                    }

                    try
                    {
                        bool isAccountSessionExist = HttpContext.Session.TryGetValue(Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous.Utilities.Session.Account), out byte[] resultByte);

                        if (isAccountSessionExist)
                        {
                            Account loginedAccount = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte));

                            Calendar tempCalendarToValidate = (await _calendarRepository.GetCalendarsAsync(loginedAccount.Email)).Where(x => x.Id == calendarEventInputViewModel?.CalendarId).FirstOrDefault();

                            if (tempCalendarToValidate is null)
                            {
                                return Json(new { result = false, error = _localizer["The calendar does not exists."].Value });
                            }

                            if (loginedAccount.Role is not (Role.Admin or Role.User)) // 비 로그인 사용자인 경우
                            {
                                return Json(new { result = false, error = _localizer["Please Login to create calendar event"].Value });
                            }

                            #region CalendarEvent
                            CalendarEvent calendarEvent = new();

                            if (calendarEventInputViewModel.AllDay)
                            {
                                DateTime tempStartDate = new(Convert.ToInt32(calendarEventInputViewModel.StartDate.Split("-")[0]),
                                    Convert.ToInt32(calendarEventInputViewModel.StartDate.Split("-")[1]),
                                    Convert.ToInt32(calendarEventInputViewModel.StartDate.Split("-")[2]), 0, 0, 0, DateTimeKind.Utc);

                                DateTime tempEndDate = new(Convert.ToInt32(calendarEventInputViewModel.EndDate.Split("-")[0]),
                                    Convert.ToInt32(calendarEventInputViewModel.EndDate.Split("-")[1]),
                                    Convert.ToInt32(calendarEventInputViewModel.EndDate.Split("-")[2]), 0, 0, 0, DateTimeKind.Utc);

                                calendarEvent.CalendarId = calendarEventInputViewModel.CalendarId;
                                calendarEvent.Title = calendarEventInputViewModel.Title;
                                calendarEvent.Description = calendarEventInputViewModel.Description;
                                calendarEvent.AllDay = calendarEventInputViewModel.AllDay;
                                calendarEvent.StartDate = tempStartDate;
                                calendarEvent.EndDate = tempEndDate;
                                calendarEvent.StartDateTimeZoneIanaId = null;
                                calendarEvent.EndDateTimeZoneIanaId = null;
                                calendarEvent.Location = calendarEventInputViewModel.Location;
                                calendarEvent.Status = calendarEventInputViewModel.Status;
                                //calendarEvent.RecurrenceId = 0;
                                calendarEvent.Created = DateTime.UtcNow;
                                calendarEvent.Updated = DateTime.UtcNow;

                            }
                            else
                            {
                                string[] temporaryStartDate = calendarEventInputViewModel.StartDate.Split(" ");
                                string[] temporaryEndDate = calendarEventInputViewModel.EndDate.Split(" ");

                                DateTime tempStartDate = new(Convert.ToInt32(temporaryStartDate[0].Split("-")[0]),
                                    Convert.ToInt32(temporaryStartDate[0].Split("-")[1]),
                                    Convert.ToInt32(temporaryStartDate[0].Split("-")[2]),
                                    Convert.ToInt32(temporaryStartDate[1].Split(":")[0]),
                                    Convert.ToInt32(temporaryStartDate[1].Split(":")[1]),
                                    0, DateTimeKind.Unspecified);

                                DateTime tempEndDate = new(Convert.ToInt32(temporaryEndDate[0].Split("-")[0]),
                                    Convert.ToInt32(temporaryEndDate[0].Split("-")[1]),
                                    Convert.ToInt32(temporaryEndDate[0].Split("-")[2]),
                                    Convert.ToInt32(temporaryEndDate[1].Split(":")[0]),
                                    Convert.ToInt32(temporaryEndDate[1].Split(":")[1]),
                                    0, DateTimeKind.Unspecified);

                                foreach (TimeZoneInfo systemTimeZone in TimeZoneInfo.GetSystemTimeZones())
                                {
                                    string systemTimeZoneIanaId = string.Empty;

                                    if (systemTimeZone.HasIanaId)
                                    {
                                        systemTimeZoneIanaId = systemTimeZone.Id;
                                    }
                                    else if (TimeZoneInfo.TryConvertWindowsIdToIanaId(systemTimeZone.Id, out string ianaId))
                                    {
                                        systemTimeZoneIanaId = ianaId;
                                    }

                                    if (string.IsNullOrEmpty(systemTimeZoneIanaId))
                                    {
                                        continue;
                                    }
                                    else if (systemTimeZoneIanaId == calendarEventInputViewModel.StartDateTimeZoneIanaId)
                                    {
                                        tempStartDate = TimeZoneInfo.ConvertTime(tempStartDate, systemTimeZone, TimeZoneInfo.Utc);
                                        break;
                                    }
                                }

                                foreach (TimeZoneInfo systemTimeZone in TimeZoneInfo.GetSystemTimeZones())
                                {
                                    string systemTimeZoneIanaId = string.Empty;

                                    if (systemTimeZone.HasIanaId)
                                    {
                                        systemTimeZoneIanaId = systemTimeZone.Id;
                                    }
                                    else if (TimeZoneInfo.TryConvertWindowsIdToIanaId(systemTimeZone.Id, out string ianaId))
                                    {
                                        systemTimeZoneIanaId = ianaId;
                                    }

                                    if (string.IsNullOrEmpty(systemTimeZoneIanaId))
                                    {
                                        continue;
                                    }
                                    else if (systemTimeZoneIanaId == calendarEventInputViewModel.EndDateTimeZoneIanaId)
                                    {
                                        tempEndDate = TimeZoneInfo.ConvertTime(tempEndDate, systemTimeZone, TimeZoneInfo.Utc);
                                        break;
                                    }
                                }

                                calendarEvent.CalendarId = calendarEventInputViewModel.CalendarId;
                                calendarEvent.Title = calendarEventInputViewModel.Title;
                                calendarEvent.Description = calendarEventInputViewModel.Description;
                                calendarEvent.AllDay = calendarEventInputViewModel.AllDay;
                                calendarEvent.StartDate = tempStartDate;
                                calendarEvent.EndDate = tempEndDate;
                                calendarEvent.StartDateTimeZoneIanaId = calendarEventInputViewModel.StartDateTimeZoneIanaId;
                                calendarEvent.EndDateTimeZoneIanaId = calendarEventInputViewModel.EndDateTimeZoneIanaId;
                                calendarEvent.Location = calendarEventInputViewModel.Location;
                                calendarEvent.Status = calendarEventInputViewModel.Status;
                                //calendarEvent.RecurrenceId = 0;
                                calendarEvent.Created = DateTime.UtcNow;
                                calendarEvent.Updated = DateTime.UtcNow;
                            }

                            await _calendarEventRepository.CreateCalendarEventAsync(calendarEvent);
                            #endregion

                            #region CalendarEventAttachedFile
                            if (calendarEventInputViewModel.CalendarEventUploadedFile != null) // 첨부 파일 존재 시,
                            {
                                if (calendarEventInputViewModel.CalendarEventUploadedFile.Length is > 0 and <= 10485760) // calendarEventInputViewModel.CalendarEventUploadedFile Length : 파일 크기 0MB 이상, 10MB 이하
                                {
                                    var createdCalendarEventId = calendarEvent.Id;

                                    string calendarEventAttachedFilePath = Path.Combine("upload", "Calendar", $"{loginedAccount.Role}Index", "calendarEventAttachedFiles", $"{createdCalendarEventId}");
                                    string guid = Guid.NewGuid().ToString().ToUpper();
                                    string calendarEventAttachedFile = $"{guid}{Path.GetExtension(calendarEventInputViewModel.CalendarEventUploadedFile.FileName)}";
                                    string filePath = Path.Combine(calendarEventAttachedFilePath, calendarEventAttachedFile);

                                    _ = await _fileRepository.UploadAsync(calendarEventInputViewModel.CalendarEventUploadedFile, filePath);

                                    await _calendarEventAttachedFileRepository.SaveCalendarEventAttachedFileAsync(new CalendarEventAttachedFile()
                                    {
                                        CalendarEventId = createdCalendarEventId,
                                        Size = Convert.ToInt32(calendarEventInputViewModel.CalendarEventUploadedFile.Length),
                                        Name = Path.GetFileNameWithoutExtension(calendarEventInputViewModel.CalendarEventUploadedFile.FileName),
                                        Extension = Path.GetExtension(calendarEventInputViewModel.CalendarEventUploadedFile.FileName),
                                        Path = $"upload/Calendar/{loginedAccount.Role}Index/calendarEventAttachedFiles/{createdCalendarEventId}/{calendarEventAttachedFile}"
                                    });
                                }
                                else
                                {
                                    return Ok(new { result = false, errorMessage = _localizer["File Size must be smaller than 10MB."].Value });
                                }

                            }
                            #endregion

                            #region CalendarReminder
                            List<Dictionary<string, object>> calendarReminders = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(calendarEventInputViewModel.SerializedCalendarReminders);

                            foreach (Dictionary<string, object> reminder in calendarReminders)
                            {
                                string method = reminder["Method"].ToString();
                                int? minutesBeforeEvent = reminder["MinutesBeforeEvent"] != null ? Convert.ToInt32(reminder["MinutesBeforeEvent"]) : null;
                                int? hoursBeforeEvent = reminder["HoursBeforeEvent"] != null ? Convert.ToInt32(reminder["HoursBeforeEvent"]) : null;
                                int? daysBeforeEvent = reminder["DaysBeforeEvent"] != null ? Convert.ToInt32(reminder["DaysBeforeEvent"]) : null;
                                int? weeksBeforeEvent = reminder["WeeksBeforeEvent"] != null ? Convert.ToInt32(reminder["WeeksBeforeEvent"]) : null;
                                string tempTimesBeforeEvent = reminder["TimesBeforeEvent"]?.ToString();
                                TimeSpan? timesBeforeEvent = !string.IsNullOrEmpty(tempTimesBeforeEvent)
                                    ? new TimeSpan(Convert.ToInt32(tempTimesBeforeEvent.Split(":")[0]), Convert.ToInt32(tempTimesBeforeEvent.Split(":")[1]), 0)
                                    : null;
                                CalendarEventReminder calendarEventReminder = new()
                                {
                                    CalendarEventId = calendarEvent.Id,
                                    Method = method,
                                    MinutesBeforeEvent = minutesBeforeEvent,
                                    HoursBeforeEvent = hoursBeforeEvent,
                                    DaysBeforeEvent = daysBeforeEvent,
                                    WeeksBeforeEvent = weeksBeforeEvent,
                                    TimesBeforeEvent = timesBeforeEvent != null ? TimeOnly.FromTimeSpan(timesBeforeEvent ?? new TimeSpan()) : null
                                };

                                await _calendarReminderRepository.CreateCalendarEventReminderAsync(calendarEventReminder);
                            }
                            #endregion

                            return Json(new { result = true, message = _localizer["The calendar event has been successfully created."].Value });
                        }
                        else
                        {
                            return Json(new { result = false, error = _localizer["Please Login to create calendar event"].Value });
                        }
                    }
                    catch
                    {
                        return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                    }
                }
                else
                {
                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                }
            }
            catch
            {
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
            }
        }
        #endregion

        #region Summernote Image File Upload
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.Admin)]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> UploadImageFile(IFormFile summernoteImageFile)
        {
            if (summernoteImageFile == null)
            {
                return Ok(new { result = false, errorMessage = _localizer["Please attach a file."].Value });
            }

            if (summernoteImageFile.Length <= 0 || summernoteImageFile.Length > 10 * 1024 * 1024)
            {
                return Ok(new { result = false, errorMessage = _localizer["File Size must be smaller than 10MB."].Value });
            }

            // 확장자 검사
            var ext = Path.GetExtension(summernoteImageFile.FileName).ToLowerInvariant();
            if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
            {
                return Ok(new { result = false, errorMessage = _localizer["Only .jpg, .jpeg or .png file allowed."].Value });
            }

            try
            {
                // 실제 이미지 여부 검사 (Magick.NET)
                using var image = new MagickImage(summernoteImageFile.OpenReadStream());
                // Magick.NET에서 예외가 발생하면 이미지가 아니거나 허용되지 않는 포맷임
            }
            catch (MagickException)
            {
                return Ok(new { result = false, errorMessage = _localizer["Invalid image file."].Value });
            }

            _ = HttpContext.Session.TryGetValue(
                Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous.Utilities.Session.Account),
                out byte[] resultByte);

            var loginedAccount = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte));

            string summernoteImagePath = Path.Combine("upload", "Calendar", $"{loginedAccount.Role}Index", "summernote", "images");
            string imageFile = $"{Guid.NewGuid():N}{ext}";
            string filePath = Path.Combine(summernoteImagePath, imageFile);

            var uploadResult = await _fileRepository.UploadAsync(summernoteImageFile, filePath);

            if (uploadResult)
            {
                var fileStream = await _fileRepository.DownloadAsync(filePath);
                return Ok(new
                {
                    result = true,
                    file = File(fileStream, summernoteImageFile.ContentType, imageFile),
                    filePath = _rsa.Encrypt(filePath)
                });
            }
            else
            {
                return Ok(new { result = false, errorMessage = _localizer["Input is invalid"].Value });
            }
        }
        #endregion

        #endregion

        #region Read

        [HttpGet]
        public async Task<IActionResult> AdminIndex()
        {
            _ = HttpContext.Session.TryGetValue(Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous.Utilities.Session.Account), out byte[] resultByte);

            Account loginedAccount = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte));

            CalendarOutputViewModel calendarOutputViewModel = new();

            #region Load Calendar
            List<Calendar> calendars = await _calendarRepository.GetCalendarsAsync(loginedAccount.Email) ?? [];

            if (calendars.Count == 0)
            {
                calendars.Add(new Calendar
                {
                    AccountEmail = loginedAccount.Email,
                    Name = loginedAccount.Nickname,
                    TimeZoneIanaId = loginedAccount.TimeZoneIanaId,
                    HtmlColorCode = "#fc330e", // Default Html Color Code
                    Created = DateTime.UtcNow,
                    Updated = DateTime.UtcNow
                });
                await _calendarRepository.CreateCalendarAsync(calendars[0]);
            }
            #endregion

            #region Load CalendarEvent
            List<CalendarEventOutputViewModel> calendarEventOutputViewModels = [];

            foreach (Calendar calendar in calendars)
            {
                foreach (CalendarEvent calendarEvent in await _calendarEventRepository.GetCalendarEventsAsync(calendar.Id) ?? [])
                {
                    CalendarEventOutputViewModel item = new()
                    {
                        Id = calendarEvent.Id,
                        CalendarId = calendarEvent.CalendarId,
                        Title = calendarEvent.Title,
                        AllDay = calendarEvent.AllDay
                    };

                    if (calendarEvent.AllDay)
                    {
                        foreach (TimeZoneInfo systemTimeZone in TimeZoneInfo.GetSystemTimeZones())
                        {
                            string systemTimeZoneIanaId = string.Empty;

                            if (systemTimeZone.HasIanaId)
                            {
                                systemTimeZoneIanaId = systemTimeZone.Id;
                            }
                            else if (TimeZoneInfo.TryConvertWindowsIdToIanaId(systemTimeZone.Id, out string ianaId))
                            {
                                systemTimeZoneIanaId = ianaId;
                            }

                            if (string.IsNullOrEmpty(systemTimeZoneIanaId))
                            {
                                continue;
                            }
                            else if (systemTimeZoneIanaId == loginedAccount.TimeZoneIanaId)
                            {
                                calendarEvent.StartDate = TimeZoneInfo.ConvertTime(calendarEvent.StartDate, TimeZoneInfo.Utc, systemTimeZone);
                                item.DisplayStartDateTimeZone = string.Empty;
                                break;
                            }
                        }

                        foreach (TimeZoneInfo systemTimeZone in TimeZoneInfo.GetSystemTimeZones())
                        {
                            string systemTimeZoneIanaId = string.Empty;

                            if (systemTimeZone.HasIanaId)
                            {
                                systemTimeZoneIanaId = systemTimeZone.Id;
                            }
                            else if (TimeZoneInfo.TryConvertWindowsIdToIanaId(systemTimeZone.Id, out string ianaId))
                            {
                                systemTimeZoneIanaId = ianaId;
                            }

                            if (string.IsNullOrEmpty(systemTimeZoneIanaId))
                            {
                                continue;
                            }
                            else if (systemTimeZoneIanaId == loginedAccount.TimeZoneIanaId)
                            {
                                calendarEvent.EndDate = TimeZoneInfo.ConvertTime(calendarEvent.EndDate, TimeZoneInfo.Utc, systemTimeZone);
                                item.DisplayEndDateTimeZone = string.Empty;
                                break;
                            }
                        }

                        item.DisplayStartDate = calendarEvent.StartDate.ToString("yyyy-MM-dd");
                        item.DisplayEndDate = calendarEvent.EndDate.ToString("yyyy-MM-dd");

                        item.StartDate = calendarEvent.StartDate;
                        DateTime tempEndDate = calendarEvent.EndDate.AddDays(1);
                        item.EndDate = new DateTime(tempEndDate.Year, tempEndDate.Month, tempEndDate.Day, 0, 0, 0, tempEndDate.Kind);
                    }
                    else
                    {
                        foreach (TimeZoneInfo systemTimeZone in TimeZoneInfo.GetSystemTimeZones())
                        {
                            string systemTimeZoneIanaId = string.Empty;

                            if (systemTimeZone.HasIanaId)
                            {
                                systemTimeZoneIanaId = systemTimeZone.Id;
                            }
                            else if (TimeZoneInfo.TryConvertWindowsIdToIanaId(systemTimeZone.Id, out string ianaId))
                            {
                                systemTimeZoneIanaId = ianaId;
                            }

                            if (string.IsNullOrEmpty(systemTimeZoneIanaId))
                            {
                                continue;
                            }
                            else if (systemTimeZoneIanaId == calendarEvent.StartDateTimeZoneIanaId)
                            {
                                calendarEvent.StartDate = TimeZoneInfo.ConvertTime(calendarEvent.StartDate, TimeZoneInfo.Utc, systemTimeZone);
                                item.DisplayStartDateTimeZone = systemTimeZone.StandardName;
                                break;
                            }
                        }

                        foreach (TimeZoneInfo systemTimeZone in TimeZoneInfo.GetSystemTimeZones())
                        {
                            string systemTimeZoneIanaId = string.Empty;

                            if (systemTimeZone.HasIanaId)
                            {
                                systemTimeZoneIanaId = systemTimeZone.Id;
                            }
                            else if (TimeZoneInfo.TryConvertWindowsIdToIanaId(systemTimeZone.Id, out string ianaId))
                            {
                                systemTimeZoneIanaId = ianaId;
                            }

                            if (string.IsNullOrEmpty(systemTimeZoneIanaId))
                            {
                                continue;
                            }
                            else if (systemTimeZoneIanaId == calendarEvent.EndDateTimeZoneIanaId)
                            {
                                calendarEvent.EndDate = TimeZoneInfo.ConvertTime(calendarEvent.EndDate, TimeZoneInfo.Utc, systemTimeZone);
                                item.DisplayEndDateTimeZone = systemTimeZone.StandardName;
                                break;
                            }
                        }

                        item.DisplayStartDate = calendarEvent.StartDate.ToString("yyyy-MM-dd HH:mm");
                        item.DisplayEndDate = calendarEvent.EndDate.ToString("yyyy-MM-dd HH:mm");

                        item.StartDate = calendarEvent.StartDate;
                        DateTime tempEndDate = calendarEvent.EndDate.AddDays(1);
                        item.EndDate = new DateTime(tempEndDate.Year, tempEndDate.Month, tempEndDate.Day, 0, 0, 0, tempEndDate.Kind);
                    }

                    item.HtmlColorCode = calendar.HtmlColorCode;

                    calendarEventOutputViewModels.Add(item);
                }
            }
            #endregion

            calendarOutputViewModel.Calendars = calendars;
            calendarOutputViewModel.CalendarEventOutputViewModels = calendarEventOutputViewModels;

            return View(calendarOutputViewModel);
        }

        [HttpGet]
        public async Task<IActionResult> UserIndex()
        {
            _ = HttpContext.Session.TryGetValue(Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous.Utilities.Session.Account), out byte[] resultByte);

            Account loginedAccount = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte));

            CalendarOutputViewModel calendarOutputViewModel = new();

            #region Load Calendar
            List<Calendar> calendars = await _calendarRepository.GetCalendarsAsync(loginedAccount.Email) ?? [];

            if (calendars.Count == 0)
            {
                calendars.Add(new Calendar
                {
                    AccountEmail = loginedAccount.Email,
                    Name = loginedAccount.Nickname,
                    TimeZoneIanaId = loginedAccount.TimeZoneIanaId,
                    HtmlColorCode = "#fc330e", // Default Html Color Code
                    Created = DateTime.UtcNow,
                    Updated = DateTime.UtcNow
                });
                await _calendarRepository.CreateCalendarAsync(calendars[0]);
            }
            #endregion

            #region Load CalendarEvent
            List<CalendarEventOutputViewModel> calendarEventOutputViewModels = [];

            foreach (Calendar calendar in calendars)
            {
                foreach (CalendarEvent calendarEvent in await _calendarEventRepository.GetCalendarEventsAsync(calendar.Id) ?? [])
                {
                    CalendarEventOutputViewModel item = new()
                    {
                        Id = calendarEvent.Id,
                        CalendarId = calendarEvent.CalendarId,
                        Title = calendarEvent.Title,
                        AllDay = calendarEvent.AllDay
                    };

                    if (calendarEvent.AllDay)
                    {
                        foreach (TimeZoneInfo systemTimeZone in TimeZoneInfo.GetSystemTimeZones())
                        {
                            string systemTimeZoneIanaId = string.Empty;

                            if (systemTimeZone.HasIanaId)
                            {
                                systemTimeZoneIanaId = systemTimeZone.Id;
                            }
                            else if (TimeZoneInfo.TryConvertWindowsIdToIanaId(systemTimeZone.Id, out string ianaId))
                            {
                                systemTimeZoneIanaId = ianaId;
                            }

                            if (string.IsNullOrEmpty(systemTimeZoneIanaId))
                            {
                                continue;
                            }
                            else if (systemTimeZoneIanaId == loginedAccount.TimeZoneIanaId)
                            {
                                calendarEvent.StartDate = TimeZoneInfo.ConvertTime(calendarEvent.StartDate, TimeZoneInfo.Utc, systemTimeZone);
                                item.DisplayStartDateTimeZone = string.Empty;
                                break;
                            }
                        }

                        foreach (TimeZoneInfo systemTimeZone in TimeZoneInfo.GetSystemTimeZones())
                        {
                            string systemTimeZoneIanaId = string.Empty;

                            if (systemTimeZone.HasIanaId)
                            {
                                systemTimeZoneIanaId = systemTimeZone.Id;
                            }
                            else if (TimeZoneInfo.TryConvertWindowsIdToIanaId(systemTimeZone.Id, out string ianaId))
                            {
                                systemTimeZoneIanaId = ianaId;
                            }

                            if (string.IsNullOrEmpty(systemTimeZoneIanaId))
                            {
                                continue;
                            }
                            else if (systemTimeZoneIanaId == loginedAccount.TimeZoneIanaId)
                            {
                                calendarEvent.EndDate = TimeZoneInfo.ConvertTime(calendarEvent.EndDate, TimeZoneInfo.Utc, systemTimeZone);
                                item.DisplayEndDateTimeZone = string.Empty;
                                break;
                            }
                        }

                        item.DisplayStartDate = calendarEvent.StartDate.ToString("yyyy-MM-dd");
                        item.DisplayEndDate = calendarEvent.EndDate.ToString("yyyy-MM-dd");

                        item.StartDate = calendarEvent.StartDate;
                        DateTime tempEndDate = calendarEvent.EndDate.AddDays(1);
                        item.EndDate = new DateTime(tempEndDate.Year, tempEndDate.Month, tempEndDate.Day, 0, 0, 0, tempEndDate.Kind);
                    }
                    else
                    {
                        foreach (TimeZoneInfo systemTimeZone in TimeZoneInfo.GetSystemTimeZones())
                        {
                            string systemTimeZoneIanaId = string.Empty;

                            if (systemTimeZone.HasIanaId)
                            {
                                systemTimeZoneIanaId = systemTimeZone.Id;
                            }
                            else if (TimeZoneInfo.TryConvertWindowsIdToIanaId(systemTimeZone.Id, out string ianaId))
                            {
                                systemTimeZoneIanaId = ianaId;
                            }

                            if (string.IsNullOrEmpty(systemTimeZoneIanaId))
                            {
                                continue;
                            }
                            else if (systemTimeZoneIanaId == calendarEvent.StartDateTimeZoneIanaId)
                            {
                                calendarEvent.StartDate = TimeZoneInfo.ConvertTime(calendarEvent.StartDate, TimeZoneInfo.Utc, systemTimeZone);
                                item.DisplayStartDateTimeZone = systemTimeZone.StandardName;
                                break;
                            }
                        }

                        foreach (TimeZoneInfo systemTimeZone in TimeZoneInfo.GetSystemTimeZones())
                        {
                            string systemTimeZoneIanaId = string.Empty;

                            if (systemTimeZone.HasIanaId)
                            {
                                systemTimeZoneIanaId = systemTimeZone.Id;
                            }
                            else if (TimeZoneInfo.TryConvertWindowsIdToIanaId(systemTimeZone.Id, out string ianaId))
                            {
                                systemTimeZoneIanaId = ianaId;
                            }

                            if (string.IsNullOrEmpty(systemTimeZoneIanaId))
                            {
                                continue;
                            }
                            else if (systemTimeZoneIanaId == calendarEvent.EndDateTimeZoneIanaId)
                            {
                                calendarEvent.EndDate = TimeZoneInfo.ConvertTime(calendarEvent.EndDate, TimeZoneInfo.Utc, systemTimeZone);
                                item.DisplayEndDateTimeZone = systemTimeZone.StandardName;
                                break;
                            }
                        }

                        item.DisplayStartDate = calendarEvent.StartDate.ToString("yyyy-MM-dd HH:mm");
                        item.DisplayEndDate = calendarEvent.EndDate.ToString("yyyy-MM-dd HH:mm");

                        item.StartDate = calendarEvent.StartDate;
                        DateTime tempEndDate = calendarEvent.EndDate.AddDays(1);
                        item.EndDate = new DateTime(tempEndDate.Year, tempEndDate.Month, tempEndDate.Day, 0, 0, 0, tempEndDate.Kind);
                    }

                    item.HtmlColorCode = calendar.HtmlColorCode;

                    calendarEventOutputViewModels.Add(item);
                }
            }
            #endregion

            #region Load OtherCalendar
            List<Calendar> otherCalendars = [];

            List<Calendar> allCalendars = await _calendarRepository.GetAllCalendarsAsync() ?? [];

            var tempOtherCalendars = (from calendar in allCalendars
                                      join otherCalendar in await _otherCalendarRepository.GetOtherCalendarsAsync(loginedAccount.Email) ?? []
                                      on calendar.Id equals otherCalendar.CalendarId
                                      select new
                                      {
                                          calendar.Id,
                                          calendar.Name,
                                          calendar.HtmlColorCode
                                      }).OrderBy(x => x.Name).ToList();

            foreach (var tempOtherCalendar in tempOtherCalendars)
            {
                otherCalendars.Add(new Calendar() { Id = tempOtherCalendar.Id, Name = tempOtherCalendar.Name, HtmlColorCode = tempOtherCalendar.HtmlColorCode });
            }
            #endregion

            #region Load OtherCalendarEvent
            List<CalendarEventOutputViewModel> otherCalendarEventOutputViewModels = [];

            foreach (Calendar otherCalendar in otherCalendars)
            {
                foreach (CalendarEvent otherCalendarEvent in await _calendarEventRepository.GetCalendarEventsAsync(otherCalendar.Id) ?? [])
                {
                    CalendarEventOutputViewModel item = new()
                    {
                        Id = otherCalendarEvent.Id,
                        CalendarId = otherCalendarEvent.CalendarId,
                        Title = otherCalendarEvent.Title,
                        AllDay = otherCalendarEvent.AllDay
                    };

                    if (otherCalendarEvent.AllDay)
                    {
                        foreach (TimeZoneInfo systemTimeZone in TimeZoneInfo.GetSystemTimeZones())
                        {
                            string systemTimeZoneIanaId = string.Empty;

                            if (systemTimeZone.HasIanaId)
                            {
                                systemTimeZoneIanaId = systemTimeZone.Id;
                            }
                            else if (TimeZoneInfo.TryConvertWindowsIdToIanaId(systemTimeZone.Id, out string ianaId))
                            {
                                systemTimeZoneIanaId = ianaId;
                            }

                            if (string.IsNullOrEmpty(systemTimeZoneIanaId))
                            {
                                continue;
                            }
                            else if (systemTimeZoneIanaId == loginedAccount.TimeZoneIanaId)
                            {
                                otherCalendarEvent.StartDate = TimeZoneInfo.ConvertTime(otherCalendarEvent.StartDate, TimeZoneInfo.Utc, systemTimeZone);
                                item.DisplayStartDateTimeZone = string.Empty;
                                break;
                            }
                        }

                        foreach (TimeZoneInfo systemTimeZone in TimeZoneInfo.GetSystemTimeZones())
                        {
                            string systemTimeZoneIanaId = string.Empty;

                            if (systemTimeZone.HasIanaId)
                            {
                                systemTimeZoneIanaId = systemTimeZone.Id;
                            }
                            else if (TimeZoneInfo.TryConvertWindowsIdToIanaId(systemTimeZone.Id, out string ianaId))
                            {
                                systemTimeZoneIanaId = ianaId;
                            }

                            if (string.IsNullOrEmpty(systemTimeZoneIanaId))
                            {
                                continue;
                            }
                            else if (systemTimeZoneIanaId == loginedAccount.TimeZoneIanaId)
                            {
                                otherCalendarEvent.EndDate = TimeZoneInfo.ConvertTime(otherCalendarEvent.EndDate, TimeZoneInfo.Utc, systemTimeZone);
                                item.DisplayEndDateTimeZone = string.Empty;
                                break;
                            }
                        }

                        item.DisplayStartDate = otherCalendarEvent.StartDate.ToString("yyyy-MM-dd");
                        item.DisplayEndDate = otherCalendarEvent.EndDate.ToString("yyyy-MM-dd");

                        item.StartDate = otherCalendarEvent.StartDate;
                        DateTime tempEndDate = otherCalendarEvent.EndDate.AddDays(1);
                        item.EndDate = new DateTime(tempEndDate.Year, tempEndDate.Month, tempEndDate.Day, 0, 0, 0, tempEndDate.Kind);
                    }
                    else
                    {
                        foreach (TimeZoneInfo systemTimeZone in TimeZoneInfo.GetSystemTimeZones())
                        {
                            string systemTimeZoneIanaId = string.Empty;

                            if (systemTimeZone.HasIanaId)
                            {
                                systemTimeZoneIanaId = systemTimeZone.Id;
                            }
                            else if (TimeZoneInfo.TryConvertWindowsIdToIanaId(systemTimeZone.Id, out string ianaId))
                            {
                                systemTimeZoneIanaId = ianaId;
                            }

                            if (string.IsNullOrEmpty(systemTimeZoneIanaId))
                            {
                                continue;
                            }
                            else if (systemTimeZoneIanaId == otherCalendarEvent.StartDateTimeZoneIanaId)
                            {
                                otherCalendarEvent.StartDate = TimeZoneInfo.ConvertTime(otherCalendarEvent.StartDate, TimeZoneInfo.Utc, systemTimeZone);
                                item.DisplayStartDateTimeZone = systemTimeZone.StandardName;
                                break;
                            }
                        }

                        foreach (TimeZoneInfo systemTimeZone in TimeZoneInfo.GetSystemTimeZones())
                        {
                            string systemTimeZoneIanaId = string.Empty;

                            if (systemTimeZone.HasIanaId)
                            {
                                systemTimeZoneIanaId = systemTimeZone.Id;
                            }
                            else if (TimeZoneInfo.TryConvertWindowsIdToIanaId(systemTimeZone.Id, out string ianaId))
                            {
                                systemTimeZoneIanaId = ianaId;
                            }

                            if (string.IsNullOrEmpty(systemTimeZoneIanaId))
                            {
                                continue;
                            }
                            else if (systemTimeZoneIanaId == otherCalendarEvent.EndDateTimeZoneIanaId)
                            {
                                otherCalendarEvent.EndDate = TimeZoneInfo.ConvertTime(otherCalendarEvent.EndDate, TimeZoneInfo.Utc, systemTimeZone);
                                item.DisplayEndDateTimeZone = systemTimeZone.StandardName;
                                break;
                            }
                        }

                        item.DisplayStartDate = otherCalendarEvent.StartDate.ToString("yyyy-MM-dd HH:mm");
                        item.DisplayEndDate = otherCalendarEvent.EndDate.ToString("yyyy-MM-dd HH:mm");

                        item.StartDate = otherCalendarEvent.StartDate;
                        DateTime tempEndDate = otherCalendarEvent.EndDate.AddDays(1);
                        item.EndDate = new DateTime(tempEndDate.Year, tempEndDate.Month, tempEndDate.Day, 0, 0, 0, tempEndDate.Kind);
                    }

                    item.HtmlColorCode = otherCalendar.HtmlColorCode;

                    otherCalendarEventOutputViewModels.Add(item);
                }
            }
            #endregion

            calendarOutputViewModel.Calendars = calendars;
            calendarOutputViewModel.CalendarEventOutputViewModels = calendarEventOutputViewModels;
            calendarOutputViewModel.OtherCalendars = otherCalendars;
            calendarOutputViewModel.OtherCalendarEventOutputViewModels = otherCalendarEventOutputViewModels;

            return View(calendarOutputViewModel);
        }

        [HttpGet]
        public async Task<IActionResult> AnonymousIndex()
        {
            Account loginedAccount = new() { Role = Role.Anonymous, TimeZoneIanaId = "UTC" };

            CalendarOutputViewModel calendarOutputViewModel = new();

            #region Load OtherCalendar
            List<Calendar> otherCalendars = [];

            List<Calendar> allCalendars = await _calendarRepository.GetAllCalendarsAsync() ?? [];

            var tempOtherCalendars = (from calendar in allCalendars
                                      join calendarShared in (await _calendarSharedRepository?.GetCalendarSharedAsync())?.Where(x => x.Anonymous)?.ToList() ?? []
                                      on calendar.Id equals calendarShared.CalendarId
                                      select new
                                      {
                                          calendar.Id,
                                          calendar.Name,
                                          calendar.HtmlColorCode
                                      }).OrderBy(x => x.Name).ToList();

            foreach (var tempOtherCalendar in tempOtherCalendars)
            {
                otherCalendars.Add(new Calendar() { Id = tempOtherCalendar.Id, Name = tempOtherCalendar.Name, HtmlColorCode = tempOtherCalendar.HtmlColorCode });
            }
            #endregion

            #region Load OtherCalendarEvent
            List<CalendarEventOutputViewModel> otherCalendarEventOutputViewModels = [];

            foreach (Calendar otherCalendar in otherCalendars)
            {
                foreach (CalendarEvent otherCalendarEvent in await _calendarEventRepository.GetCalendarEventsAsync(otherCalendar.Id) ?? [])
                {
                    CalendarEventOutputViewModel item = new()
                    {
                        Id = otherCalendarEvent.Id,
                        CalendarId = otherCalendarEvent.CalendarId,
                        Title = otherCalendarEvent.Title,
                        AllDay = otherCalendarEvent.AllDay
                    };

                    if (otherCalendarEvent.AllDay)
                    {
                        foreach (TimeZoneInfo systemTimeZone in TimeZoneInfo.GetSystemTimeZones())
                        {
                            string systemTimeZoneIanaId = string.Empty;

                            if (systemTimeZone.HasIanaId)
                            {
                                systemTimeZoneIanaId = systemTimeZone.Id;
                            }
                            else if (TimeZoneInfo.TryConvertWindowsIdToIanaId(systemTimeZone.Id, out string ianaId))
                            {
                                systemTimeZoneIanaId = ianaId;
                            }

                            if (string.IsNullOrEmpty(systemTimeZoneIanaId))
                            {
                                continue;
                            }
                            else if (systemTimeZoneIanaId == loginedAccount.TimeZoneIanaId)
                            {
                                otherCalendarEvent.StartDate = TimeZoneInfo.ConvertTime(otherCalendarEvent.StartDate, TimeZoneInfo.Utc, systemTimeZone);
                                item.DisplayStartDateTimeZone = string.Empty;
                                break;
                            }
                        }

                        foreach (TimeZoneInfo systemTimeZone in TimeZoneInfo.GetSystemTimeZones())
                        {
                            string systemTimeZoneIanaId = string.Empty;

                            if (systemTimeZone.HasIanaId)
                            {
                                systemTimeZoneIanaId = systemTimeZone.Id;
                            }
                            else if (TimeZoneInfo.TryConvertWindowsIdToIanaId(systemTimeZone.Id, out string ianaId))
                            {
                                systemTimeZoneIanaId = ianaId;
                            }

                            if (string.IsNullOrEmpty(systemTimeZoneIanaId))
                            {
                                continue;
                            }
                            else if (systemTimeZoneIanaId == loginedAccount.TimeZoneIanaId)
                            {
                                otherCalendarEvent.EndDate = TimeZoneInfo.ConvertTime(otherCalendarEvent.EndDate, TimeZoneInfo.Utc, systemTimeZone);
                                item.DisplayEndDateTimeZone = string.Empty;
                                break;
                            }
                        }

                        item.DisplayStartDate = otherCalendarEvent.StartDate.ToString("yyyy-MM-dd");
                        item.DisplayEndDate = otherCalendarEvent.EndDate.ToString("yyyy-MM-dd");

                        item.StartDate = otherCalendarEvent.StartDate;
                        DateTime tempEndDate = otherCalendarEvent.EndDate.AddDays(1);
                        item.EndDate = new DateTime(tempEndDate.Year, tempEndDate.Month, tempEndDate.Day, 0, 0, 0, tempEndDate.Kind);
                    }
                    else
                    {
                        foreach (TimeZoneInfo systemTimeZone in TimeZoneInfo.GetSystemTimeZones())
                        {
                            string systemTimeZoneIanaId = string.Empty;

                            if (systemTimeZone.HasIanaId)
                            {
                                systemTimeZoneIanaId = systemTimeZone.Id;
                            }
                            else if (TimeZoneInfo.TryConvertWindowsIdToIanaId(systemTimeZone.Id, out string ianaId))
                            {
                                systemTimeZoneIanaId = ianaId;
                            }

                            if (string.IsNullOrEmpty(systemTimeZoneIanaId))
                            {
                                continue;
                            }
                            else if (systemTimeZoneIanaId == otherCalendarEvent.StartDateTimeZoneIanaId)
                            {
                                otherCalendarEvent.StartDate = TimeZoneInfo.ConvertTime(otherCalendarEvent.StartDate, TimeZoneInfo.Utc, systemTimeZone);
                                item.DisplayStartDateTimeZone = systemTimeZone.StandardName;
                                break;
                            }
                        }

                        foreach (TimeZoneInfo systemTimeZone in TimeZoneInfo.GetSystemTimeZones())
                        {
                            string systemTimeZoneIanaId = string.Empty;

                            if (systemTimeZone.HasIanaId)
                            {
                                systemTimeZoneIanaId = systemTimeZone.Id;
                            }
                            else if (TimeZoneInfo.TryConvertWindowsIdToIanaId(systemTimeZone.Id, out string ianaId))
                            {
                                systemTimeZoneIanaId = ianaId;
                            }

                            if (string.IsNullOrEmpty(systemTimeZoneIanaId))
                            {
                                continue;
                            }
                            else if (systemTimeZoneIanaId == otherCalendarEvent.EndDateTimeZoneIanaId)
                            {
                                otherCalendarEvent.EndDate = TimeZoneInfo.ConvertTime(otherCalendarEvent.EndDate, TimeZoneInfo.Utc, systemTimeZone);
                                item.DisplayEndDateTimeZone = systemTimeZone.StandardName;
                                break;
                            }
                        }

                        item.DisplayStartDate = otherCalendarEvent.StartDate.ToString("yyyy-MM-dd HH:mm");
                        item.DisplayEndDate = otherCalendarEvent.EndDate.ToString("yyyy-MM-dd HH:mm");

                        item.StartDate = otherCalendarEvent.StartDate;
                        DateTime tempEndDate = otherCalendarEvent.EndDate.AddDays(1);
                        item.EndDate = new DateTime(tempEndDate.Year, tempEndDate.Month, tempEndDate.Day, 0, 0, 0, tempEndDate.Kind);
                    }

                    item.HtmlColorCode = otherCalendar.HtmlColorCode;

                    otherCalendarEventOutputViewModels.Add(item);
                }
            }
            #endregion

            calendarOutputViewModel.OtherCalendars = otherCalendars;
            calendarOutputViewModel.OtherCalendarEventOutputViewModels = otherCalendarEventOutputViewModels;

            return View(calendarOutputViewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.Admin)]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> IsCalendarExists(int id)
        {
            try
            {
                _ = HttpContext.Session.TryGetValue(Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous.Utilities.Session.Account), out byte[] resultByte);
                List<Calendar> tempCalendars = await _calendarRepository.GetCalendarsAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email) ?? [];

                if (tempCalendars == null || tempCalendars.Count == 0)
                {
                    return Json(new { result = false, error = _localizer["No calendar exists"].Value });
                }
                else
                {
                    Calendar tempCalendar = tempCalendars.Where(a => a.Id == id).FirstOrDefault();

                    return tempCalendar == null
                        ? Json(new { result = false, error = _localizer["Input is invalid"].Value })
                        : (IActionResult)Json(new { result = true, calendar = tempCalendar });
                }
            }
            catch
            {
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.Admin)]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> GetCalendars()
        {
            try
            {
                _ = HttpContext.Session.TryGetValue(Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous.Utilities.Session.Account), out byte[] resultByte);
                List<Calendar> calendars = (await _calendarRepository.GetCalendarsAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email) ?? [])?.ToList();

                return Json(new { result = true, calendars });
            }
            catch
            {
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.User)]
        [RequiredHttpPostAccess(Role = Role.Anonymous)]
        public async Task<IActionResult> GetOtherCalendars()
        {
            try
            {
                bool isAccountSessionExist = HttpContext.Session.TryGetValue(Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous.Utilities.Session.Account), out byte[] resultByte);

                if (isAccountSessionExist)
                {
                    List<Calendar> calendars = await _calendarRepository.GetAllCalendarsAsync();
                    List<OtherCalendar> otherCalendars = (await _otherCalendarRepository.GetOtherCalendarsAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email) ?? [])?.ToList();

                    List<Calendar> tempOtherCalendars = (from calendar in calendars
                                                         join otherCalendar in otherCalendars
                                                         on calendar.Id equals otherCalendar.CalendarId
                                                         select new Calendar()
                                                         {
                                                             Id = calendar.Id,
                                                             Name = calendar.Name,
                                                             HtmlColorCode = calendar.HtmlColorCode,
                                                         }).OrderBy(x => x.Name).ToList();

                    return Json(new { result = true, tempOtherCalendars });
                }
                else
                {
                    List<Calendar> tempOtherCalendars = [];
                    var temporaryOtherCalendars = (from calendar in await _calendarRepository.GetAllCalendarsAsync() ?? []
                                                   join calendarShared in (await _calendarSharedRepository?.GetCalendarSharedAsync())?.Where(x => x.Anonymous)?.ToList() ?? []
                                                   on calendar.Id equals calendarShared.CalendarId
                                                   select new
                                                   {
                                                       calendar.Id,
                                                       calendar.Name,
                                                       calendar.HtmlColorCode
                                                   }).OrderBy(x => x.Name).ToList();

                    foreach (var tempOtherCalendar in temporaryOtherCalendars)
                    {
                        tempOtherCalendars.Add(new Calendar() { Id = tempOtherCalendar.Id, Name = tempOtherCalendar.Name, HtmlColorCode = tempOtherCalendar.HtmlColorCode });
                    }

                    return Json(new { result = true, tempOtherCalendars });
                }
            }
            catch
            {
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.Admin)]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> IsCalendarEventExists(int id)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    try
                    {
                        _ = HttpContext.Session.TryGetValue(Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous.Utilities.Session.Account), out byte[] resultByte);

                        Account loginedAccount = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte));

                        List<Calendar> calendars = await _calendarRepository.GetCalendarsAsync(loginedAccount.Email) ?? [];

                        CalendarEvent tempCalendarEvent = await _calendarEventRepository.GetCalendarEventAsync(id);
                        Calendar tempCalendar = calendars?.Where(x => x.Id == tempCalendarEvent.CalendarId)?.FirstOrDefault() ?? new Calendar();

                        if (!(tempCalendarEvent.Id > 0))
                        {
                            return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                        }

                        if (!(tempCalendar.Id > 0))
                        {
                            return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                        }

                        CalendarEventOutputViewModel calendarEvent = new()
                        {
                            Id = tempCalendarEvent.Id,
                            CalendarId = tempCalendarEvent.CalendarId,
                            Title = tempCalendarEvent.Title,
                            AllDay = tempCalendarEvent.AllDay
                        };

                        HtmlDocument htmlDocument = new();
                        htmlDocument.LoadHtml(tempCalendarEvent.Description);

                        foreach (HtmlNode imgTag in htmlDocument.DocumentNode.Descendants("img") ?? [])
                        {
                            string filePath = imgTag.GetAttributeValue("alt", "");

                            // 파일을 다운로드하는 메서드
                            byte[] fileData = await _fileRepository.DownloadAsync(filePath);

                            if (fileData != null)
                            {
                                // ContentType을 추출하기 위한 FileExtensionContentTypeProvider 사용
                                FileExtensionContentTypeProvider provider = new();

                                // 기본 contentType은 "application/octet-stream"으로 설정
                                if (!provider.TryGetContentType(filePath, out string contentType))
                                {
                                    contentType = "application/octet-stream";
                                }

                                // 파일 데이터를 Base64로 변환
                                string base64Data = Convert.ToBase64String(fileData);

                                // img 태그에 base64 데이터 저장 (src에 넣는 대신 data-file 속성 사용)
                                _ = imgTag.SetAttributeValue("data-file", base64Data);
                                _ = imgTag.SetAttributeValue("data-contenttype", contentType);
                                _ = imgTag.SetAttributeValue("alt", _rsa.Encrypt(filePath));
                            }
                        }

                        calendarEvent.Description = htmlDocument.DocumentNode.OuterHtml;

                        if (tempCalendarEvent.AllDay)
                        {
                            foreach (TimeZoneInfo systemTimeZone in TimeZoneInfo.GetSystemTimeZones())
                            {
                                string systemTimeZoneIanaId = string.Empty;

                                if (systemTimeZone.HasIanaId)
                                {
                                    systemTimeZoneIanaId = systemTimeZone.Id;
                                }
                                else if (TimeZoneInfo.TryConvertWindowsIdToIanaId(systemTimeZone.Id, out string ianaId))
                                {
                                    systemTimeZoneIanaId = ianaId;
                                }

                                if (string.IsNullOrEmpty(systemTimeZoneIanaId))
                                {
                                    continue;
                                }
                                else if (systemTimeZoneIanaId == loginedAccount.TimeZoneIanaId)
                                {
                                    tempCalendarEvent.StartDate = TimeZoneInfo.ConvertTime(tempCalendarEvent.StartDate, TimeZoneInfo.Utc, systemTimeZone);
                                    calendarEvent.StartDateTimeZoneIanaId = systemTimeZoneIanaId;
                                    calendarEvent.DisplayStartDateTimeZone = string.Empty;
                                    break;
                                }
                            }

                            foreach (TimeZoneInfo systemTimeZone in TimeZoneInfo.GetSystemTimeZones())
                            {
                                string systemTimeZoneIanaId = string.Empty;

                                if (systemTimeZone.HasIanaId)
                                {
                                    systemTimeZoneIanaId = systemTimeZone.Id;
                                }
                                else if (TimeZoneInfo.TryConvertWindowsIdToIanaId(systemTimeZone.Id, out string ianaId))
                                {
                                    systemTimeZoneIanaId = ianaId;
                                }

                                if (string.IsNullOrEmpty(systemTimeZoneIanaId))
                                {
                                    continue;
                                }
                                else if (systemTimeZoneIanaId == loginedAccount.TimeZoneIanaId)
                                {
                                    tempCalendarEvent.EndDate = TimeZoneInfo.ConvertTime(tempCalendarEvent.EndDate, TimeZoneInfo.Utc, systemTimeZone);
                                    calendarEvent.EndDateTimeZoneIanaId = systemTimeZoneIanaId;
                                    calendarEvent.DisplayEndDateTimeZone = string.Empty;
                                    break;
                                }
                            }

                            calendarEvent.DisplayStartDate = tempCalendarEvent.StartDate.ToString("yyyy-MM-dd");
                            calendarEvent.DisplayEndDate = tempCalendarEvent.EndDate.ToString("yyyy-MM-dd");
                        }
                        else
                        {
                            foreach (TimeZoneInfo systemTimeZone in TimeZoneInfo.GetSystemTimeZones())
                            {
                                string systemTimeZoneIanaId = string.Empty;

                                if (systemTimeZone.HasIanaId)
                                {
                                    systemTimeZoneIanaId = systemTimeZone.Id;
                                }
                                else if (TimeZoneInfo.TryConvertWindowsIdToIanaId(systemTimeZone.Id, out string ianaId))
                                {
                                    systemTimeZoneIanaId = ianaId;
                                }

                                if (string.IsNullOrEmpty(systemTimeZoneIanaId))
                                {
                                    continue;
                                }
                                else if (systemTimeZoneIanaId == tempCalendarEvent.StartDateTimeZoneIanaId)
                                {
                                    tempCalendarEvent.StartDate = TimeZoneInfo.ConvertTime(tempCalendarEvent.StartDate, TimeZoneInfo.Utc, systemTimeZone);
                                    calendarEvent.StartDateTimeZoneIanaId = systemTimeZoneIanaId;
                                    calendarEvent.DisplayStartDateTimeZone = systemTimeZone.StandardName;
                                    break;
                                }
                            }

                            foreach (TimeZoneInfo systemTimeZone in TimeZoneInfo.GetSystemTimeZones())
                            {
                                string systemTimeZoneIanaId = string.Empty;

                                if (systemTimeZone.HasIanaId)
                                {
                                    systemTimeZoneIanaId = systemTimeZone.Id;
                                }
                                else if (TimeZoneInfo.TryConvertWindowsIdToIanaId(systemTimeZone.Id, out string ianaId))
                                {
                                    systemTimeZoneIanaId = ianaId;
                                }

                                if (string.IsNullOrEmpty(systemTimeZoneIanaId))
                                {
                                    continue;
                                }
                                else if (systemTimeZoneIanaId == tempCalendarEvent.EndDateTimeZoneIanaId)
                                {
                                    tempCalendarEvent.EndDate = TimeZoneInfo.ConvertTime(tempCalendarEvent.EndDate, TimeZoneInfo.Utc, systemTimeZone);
                                    calendarEvent.EndDateTimeZoneIanaId = systemTimeZoneIanaId;
                                    calendarEvent.DisplayEndDateTimeZone = systemTimeZone.StandardName;
                                    break;
                                }
                            }

                            calendarEvent.DisplayStartDate = tempCalendarEvent.StartDate.ToString("yyyy-MM-dd HH:mm");
                            calendarEvent.DisplayEndDate = tempCalendarEvent.EndDate.ToString("yyyy-MM-dd HH:mm");
                        }

                        calendarEvent.Location = tempCalendarEvent.Location;
                        calendarEvent.Status = tempCalendarEvent.Status;
                        calendarEvent.CalendarEventAttachedFile = await _calendarEventAttachedFileRepository.GetCalendarEventAttachedFileAsync(tempCalendarEvent.Id);

                        if (!string.IsNullOrEmpty(calendarEvent?.CalendarEventAttachedFile?.Path ?? ""))
                        {
                            byte[] fileData = await _fileRepository.DownloadAsync(calendarEvent?.CalendarEventAttachedFile?.Path ?? "");

                            if (fileData != null)
                            {
                                // ContentType을 추출하기 위한 FileExtensionContentTypeProvider 사용
                                FileExtensionContentTypeProvider provider = new();

                                // 기본 contentType은 "application/octet-stream"으로 설정
                                if (!provider.TryGetContentType(calendarEvent?.CalendarEventAttachedFile?.Path ?? "", out string contentType))
                                {
                                    contentType = "application/octet-stream";
                                }

                                // 파일 데이터를 Base64로 변환
                                calendarEvent.CalendarEventAttachedFileBase64Data = Convert.ToBase64String(fileData);
                                calendarEvent.CalendarEventAttachedFileContentType = contentType;
                            }
                        }

                        calendarEvent.Calendars = calendars.Select(x => new Calendar { Id = x.Id, Name = x.Name }).OrderBy(x => x.Name).ToList();
                        calendarEvent.SerializedCalendarReminders = JsonConvert.SerializeObject((await _calendarReminderRepository.GetCalendarEventRemindersAsync(tempCalendarEvent.Id)).Select(x => new CalendarEventReminder { Method = x.Method, MinutesBeforeEvent = x.MinutesBeforeEvent, HoursBeforeEvent = x.HoursBeforeEvent, DaysBeforeEvent = x.DaysBeforeEvent, WeeksBeforeEvent = x.WeeksBeforeEvent, TimesBeforeEvent = x.TimesBeforeEvent }) ?? []);

                        calendarEvent.HtmlColorCode = calendars?.Where(x => x.Id == tempCalendarEvent.CalendarId)?.FirstOrDefault()?.HtmlColorCode;

                        return Json(new { result = true, calendarEvent });
                    }
                    catch
                    {
                        return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                    }
                }
                else
                {
                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                }
            }
            catch
            {
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.Admin)]
        [RequiredHttpPostAccess(Role = Role.User)]
        [RequiredHttpPostAccess(Role = Role.Anonymous)]
        public async Task<IActionResult> IsOtherCalendarEventExists(int id)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    try
                    {
                        bool isAccountSessionExist = HttpContext.Session.TryGetValue(Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous.Utilities.Session.Account), out byte[] resultByte);

                        Account loginedAccount = null;
                        List<Calendar> calendars = null;

                        if (isAccountSessionExist)
                        {
                            loginedAccount = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte));
                            calendars = (from calendar in await _calendarRepository.GetAllCalendarsAsync() ?? []
                                         join otherCalendar in await _otherCalendarRepository.GetOtherCalendarsAsync(loginedAccount.Email) ?? []
                                         on calendar.Id equals otherCalendar.CalendarId
                                         select calendar).ToList();
                        }
                        else
                        {
                            loginedAccount = new Account() { Role = Role.Anonymous, TimeZoneIanaId = "UTC" };
                            calendars = [];
                            var temporaryOtherCalendars = (from calendar in await _calendarRepository.GetAllCalendarsAsync() ?? []
                                                           join calendarShared in (await _calendarSharedRepository?.GetCalendarSharedAsync())?.Where(x => x.Anonymous)?.ToList() ?? []
                                                           on calendar.Id equals calendarShared.CalendarId
                                                           select new
                                                           {
                                                               calendar.Id,
                                                               calendar.Name,
                                                               calendar.HtmlColorCode
                                                           }).OrderBy(x => x.Name).ToList();

                            foreach (var tempOtherCalendar in temporaryOtherCalendars)
                            {
                                calendars.Add(new Calendar() { Id = tempOtherCalendar.Id, Name = tempOtherCalendar.Name, HtmlColorCode = tempOtherCalendar.HtmlColorCode });
                            }
                        }

                        CalendarEvent tempCalendarEvent = await _calendarEventRepository.GetCalendarEventAsync(id);
                        Calendar tempCalendar = calendars?.Where(x => x.Id == tempCalendarEvent.CalendarId)?.FirstOrDefault() ?? new Calendar();

                        if (!(tempCalendarEvent.Id > 0))
                        {
                            return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                        }

                        if (!(tempCalendar.Id > 0))
                        {
                            return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                        }

                        CalendarEventOutputViewModel calendarEvent = new()
                        {
                            Id = tempCalendarEvent.Id,
                            CalendarId = tempCalendarEvent.CalendarId,
                            Title = tempCalendarEvent.Title,
                            AllDay = tempCalendarEvent.AllDay
                        };

                        HtmlDocument htmlDocument = new();
                        htmlDocument.LoadHtml(tempCalendarEvent.Description);

                        foreach (HtmlNode imgTag in htmlDocument.DocumentNode.Descendants("img") ?? [])
                        {
                            string filePath = imgTag.GetAttributeValue("alt", "");

                            // 파일을 다운로드하는 메서드
                            byte[] fileData = await _fileRepository.DownloadAsync(filePath);

                            if (fileData != null)
                            {
                                // ContentType을 추출하기 위한 FileExtensionContentTypeProvider 사용
                                FileExtensionContentTypeProvider provider = new();

                                // 기본 contentType은 "application/octet-stream"으로 설정
                                if (!provider.TryGetContentType(filePath, out string contentType))
                                {
                                    contentType = "application/octet-stream";
                                }

                                // 파일 데이터를 Base64로 변환
                                string base64Data = Convert.ToBase64String(fileData);

                                // img 태그에 base64 데이터 저장 (src에 넣는 대신 data-file 속성 사용)
                                _ = imgTag.SetAttributeValue("data-file", base64Data);
                                _ = imgTag.SetAttributeValue("data-contenttype", contentType);
                                _ = imgTag.SetAttributeValue("alt", _rsa.Encrypt(filePath));
                            }
                        }

                        calendarEvent.Description = htmlDocument.DocumentNode.OuterHtml;

                        if (tempCalendarEvent.AllDay)
                        {
                            foreach (TimeZoneInfo systemTimeZone in TimeZoneInfo.GetSystemTimeZones())
                            {
                                string systemTimeZoneIanaId = string.Empty;

                                if (systemTimeZone.HasIanaId)
                                {
                                    systemTimeZoneIanaId = systemTimeZone.Id;
                                }
                                else if (TimeZoneInfo.TryConvertWindowsIdToIanaId(systemTimeZone.Id, out string ianaId))
                                {
                                    systemTimeZoneIanaId = ianaId;
                                }

                                if (string.IsNullOrEmpty(systemTimeZoneIanaId))
                                {
                                    continue;
                                }
                                else if (systemTimeZoneIanaId == loginedAccount.TimeZoneIanaId)
                                {
                                    tempCalendarEvent.StartDate = TimeZoneInfo.ConvertTime(tempCalendarEvent.StartDate, TimeZoneInfo.Utc, systemTimeZone);
                                    calendarEvent.StartDateTimeZoneIanaId = systemTimeZoneIanaId;
                                    calendarEvent.DisplayStartDateTimeZone = string.Empty;
                                    break;
                                }
                            }

                            foreach (TimeZoneInfo systemTimeZone in TimeZoneInfo.GetSystemTimeZones())
                            {
                                string systemTimeZoneIanaId = string.Empty;

                                if (systemTimeZone.HasIanaId)
                                {
                                    systemTimeZoneIanaId = systemTimeZone.Id;
                                }
                                else if (TimeZoneInfo.TryConvertWindowsIdToIanaId(systemTimeZone.Id, out string ianaId))
                                {
                                    systemTimeZoneIanaId = ianaId;
                                }

                                if (string.IsNullOrEmpty(systemTimeZoneIanaId))
                                {
                                    continue;
                                }
                                else if (systemTimeZoneIanaId == loginedAccount.TimeZoneIanaId)
                                {
                                    tempCalendarEvent.EndDate = TimeZoneInfo.ConvertTime(tempCalendarEvent.EndDate, TimeZoneInfo.Utc, systemTimeZone);
                                    calendarEvent.EndDateTimeZoneIanaId = systemTimeZoneIanaId;
                                    calendarEvent.DisplayEndDateTimeZone = string.Empty;
                                    break;
                                }
                            }

                            calendarEvent.DisplayStartDate = tempCalendarEvent.StartDate.ToString("yyyy-MM-dd");
                            calendarEvent.DisplayEndDate = tempCalendarEvent.EndDate.ToString("yyyy-MM-dd");
                        }
                        else
                        {
                            foreach (TimeZoneInfo systemTimeZone in TimeZoneInfo.GetSystemTimeZones())
                            {
                                string systemTimeZoneIanaId = string.Empty;

                                if (systemTimeZone.HasIanaId)
                                {
                                    systemTimeZoneIanaId = systemTimeZone.Id;
                                }
                                else if (TimeZoneInfo.TryConvertWindowsIdToIanaId(systemTimeZone.Id, out string ianaId))
                                {
                                    systemTimeZoneIanaId = ianaId;
                                }

                                if (string.IsNullOrEmpty(systemTimeZoneIanaId))
                                {
                                    continue;
                                }
                                else if (systemTimeZoneIanaId == tempCalendarEvent.StartDateTimeZoneIanaId)
                                {
                                    tempCalendarEvent.StartDate = TimeZoneInfo.ConvertTime(tempCalendarEvent.StartDate, TimeZoneInfo.Utc, systemTimeZone);
                                    calendarEvent.StartDateTimeZoneIanaId = systemTimeZoneIanaId;
                                    calendarEvent.DisplayStartDateTimeZone = systemTimeZone.StandardName;
                                    break;
                                }
                            }

                            foreach (TimeZoneInfo systemTimeZone in TimeZoneInfo.GetSystemTimeZones())
                            {
                                string systemTimeZoneIanaId = string.Empty;

                                if (systemTimeZone.HasIanaId)
                                {
                                    systemTimeZoneIanaId = systemTimeZone.Id;
                                }
                                else if (TimeZoneInfo.TryConvertWindowsIdToIanaId(systemTimeZone.Id, out string ianaId))
                                {
                                    systemTimeZoneIanaId = ianaId;
                                }

                                if (string.IsNullOrEmpty(systemTimeZoneIanaId))
                                {
                                    continue;
                                }
                                else if (systemTimeZoneIanaId == tempCalendarEvent.EndDateTimeZoneIanaId)
                                {
                                    tempCalendarEvent.EndDate = TimeZoneInfo.ConvertTime(tempCalendarEvent.EndDate, TimeZoneInfo.Utc, systemTimeZone);
                                    calendarEvent.EndDateTimeZoneIanaId = systemTimeZoneIanaId;
                                    calendarEvent.DisplayEndDateTimeZone = systemTimeZone.StandardName;
                                    break;
                                }
                            }

                            calendarEvent.DisplayStartDate = tempCalendarEvent.StartDate.ToString("yyyy-MM-dd HH:mm");
                            calendarEvent.DisplayEndDate = tempCalendarEvent.EndDate.ToString("yyyy-MM-dd HH:mm");
                        }

                        calendarEvent.Location = tempCalendarEvent.Location;
                        calendarEvent.Status = tempCalendarEvent.Status;
                        calendarEvent.CalendarEventAttachedFile = await _calendarEventAttachedFileRepository.GetCalendarEventAttachedFileAsync(tempCalendarEvent.Id);

                        if (!string.IsNullOrEmpty(calendarEvent?.CalendarEventAttachedFile?.Path ?? ""))
                        {
                            byte[] fileData = await _fileRepository.DownloadAsync(calendarEvent?.CalendarEventAttachedFile?.Path ?? "");

                            if (fileData != null)
                            {
                                // ContentType을 추출하기 위한 FileExtensionContentTypeProvider 사용
                                FileExtensionContentTypeProvider provider = new();

                                // 기본 contentType은 "application/octet-stream"으로 설정
                                if (!provider.TryGetContentType(calendarEvent?.CalendarEventAttachedFile?.Path ?? "", out string contentType))
                                {
                                    contentType = "application/octet-stream";
                                }

                                // 파일 데이터를 Base64로 변환
                                calendarEvent.CalendarEventAttachedFileBase64Data = Convert.ToBase64String(fileData);
                                calendarEvent.CalendarEventAttachedFileContentType = contentType;
                            }
                        }

                        calendarEvent.Calendars = calendars.Select(x => new Calendar { Id = x.Id, Name = x.Name }).OrderBy(x => x.Name).ToList();
                        calendarEvent.SerializedCalendarReminders = JsonConvert.SerializeObject((await _calendarReminderRepository.GetCalendarEventRemindersAsync(tempCalendarEvent.Id)).Select(x => new CalendarEventReminder { Method = x.Method, MinutesBeforeEvent = x.MinutesBeforeEvent, HoursBeforeEvent = x.HoursBeforeEvent, DaysBeforeEvent = x.DaysBeforeEvent, WeeksBeforeEvent = x.WeeksBeforeEvent, TimesBeforeEvent = x.TimesBeforeEvent }) ?? []);

                        calendarEvent.HtmlColorCode = calendars?.Where(x => x.Id == tempCalendarEvent.CalendarId)?.FirstOrDefault()?.HtmlColorCode;

                        return Json(new { result = true, calendarEvent });
                    }
                    catch
                    {
                        return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                    }
                }
                else
                {
                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                }
            }
            catch
            {
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.Admin)]
        [RequiredHttpPostAccess(Role = Role.User)]
        [RequiredHttpPostAccess(Role = Role.Anonymous)]
        public async Task<IActionResult> GetCalendarEvents([FromBody] CalendarInputViewModel calendarInputViewModel)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    try
                    {
                        List<CalendarEventOutputViewModel> calendarEvents = [];

                        bool isAccountSessionExist = HttpContext.Session.TryGetValue(Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous.Utilities.Session.Account), out byte[] resultByte);

                        Account loginedAccount = null;

                        List<Calendar> tempCalendars = null;

                        List<Calendar> tempOtherCalendars = null;

                        if (isAccountSessionExist)
                        {
                            loginedAccount = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte));

                            tempCalendars = await _calendarRepository.GetCalendarsAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);

                            tempOtherCalendars = [];

                            var otherCalendars = (from calendar in await _calendarRepository.GetAllCalendarsAsync() ?? []
                                                  join otherCalendar in await _otherCalendarRepository.GetOtherCalendarsAsync(loginedAccount.Email) ?? []
                                                  on calendar.Id equals otherCalendar.CalendarId
                                                  select new
                                                  {
                                                      calendar.Id,
                                                      calendar.Name,
                                                      calendar.HtmlColorCode
                                                  }).OrderBy(x => x.Name).ToList();

                            foreach (var tempOtherCalendar in otherCalendars)
                            {
                                tempOtherCalendars.Add(new Calendar() { Id = tempOtherCalendar.Id, Name = tempOtherCalendar.Name, HtmlColorCode = tempOtherCalendar.HtmlColorCode });
                            }
                        }
                        else
                        {
                            loginedAccount = new Account() { Role = Role.Anonymous, TimeZoneIanaId = "UTC" };

                            tempCalendars = [];

                            tempOtherCalendars = [];

                            var temporaryOtherCalendars = (from calendar in await _calendarRepository.GetAllCalendarsAsync() ?? []
                                                           join calendarShared in (await _calendarSharedRepository?.GetCalendarSharedAsync())?.Where(x => x.Anonymous)?.ToList() ?? []
                                                           on calendar.Id equals calendarShared.CalendarId
                                                           select new
                                                           {
                                                               calendar.Id,
                                                               calendar.Name,
                                                               calendar.HtmlColorCode
                                                           }).OrderBy(x => x.Name).ToList();

                            foreach (var tempOtherCalendar in temporaryOtherCalendars)
                            {
                                tempOtherCalendars.Add(new Calendar() { Id = tempOtherCalendar.Id, Name = tempOtherCalendar.Name, HtmlColorCode = tempOtherCalendar.HtmlColorCode });
                            }
                        }

                        IEnumerable<Calendar> tempInputCalendars = calendarInputViewModel?.Calendars ?? [];

                        foreach (Calendar tempInputCalendar in tempInputCalendars)
                        {
                            if (((tempCalendars?.Where(x => x.Id == tempInputCalendar.Id)?.FirstOrDefault() ?? new Calendar()).Id != 0) ||
                                ((tempOtherCalendars?.Where(x => x.Id == tempInputCalendar.Id)?.FirstOrDefault() ?? new Calendar()).Id != 0))
                            {
                                foreach (CalendarEvent calendarEvent in await _calendarEventRepository.GetCalendarEventsAsync(tempInputCalendar.Id))
                                {
                                    CalendarEventOutputViewModel item = new()
                                    {
                                        Id = calendarEvent.Id,
                                        CalendarId = calendarEvent.CalendarId,
                                        Title = calendarEvent.Title,
                                        AllDay = calendarEvent.AllDay
                                    };

                                    if (calendarEvent.AllDay)
                                    {
                                        foreach (TimeZoneInfo systemTimeZone in TimeZoneInfo.GetSystemTimeZones())
                                        {
                                            string systemTimeZoneIanaId = string.Empty;

                                            if (systemTimeZone.HasIanaId)
                                            {
                                                systemTimeZoneIanaId = systemTimeZone.Id;
                                            }
                                            else if (TimeZoneInfo.TryConvertWindowsIdToIanaId(systemTimeZone.Id, out string ianaId))
                                            {
                                                systemTimeZoneIanaId = ianaId;
                                            }

                                            if (string.IsNullOrEmpty(systemTimeZoneIanaId))
                                            {
                                                continue;
                                            }
                                            else if (systemTimeZoneIanaId == loginedAccount.TimeZoneIanaId)
                                            {
                                                calendarEvent.StartDate = TimeZoneInfo.ConvertTime(calendarEvent.StartDate, TimeZoneInfo.Utc, systemTimeZone);
                                                item.DisplayStartDateTimeZone = string.Empty;
                                                break;
                                            }
                                        }

                                        foreach (TimeZoneInfo systemTimeZone in TimeZoneInfo.GetSystemTimeZones())
                                        {
                                            string systemTimeZoneIanaId = string.Empty;

                                            if (systemTimeZone.HasIanaId)
                                            {
                                                systemTimeZoneIanaId = systemTimeZone.Id;
                                            }
                                            else if (TimeZoneInfo.TryConvertWindowsIdToIanaId(systemTimeZone.Id, out string ianaId))
                                            {
                                                systemTimeZoneIanaId = ianaId;
                                            }

                                            if (string.IsNullOrEmpty(systemTimeZoneIanaId))
                                            {
                                                continue;
                                            }
                                            else if (systemTimeZoneIanaId == loginedAccount.TimeZoneIanaId)
                                            {
                                                calendarEvent.EndDate = TimeZoneInfo.ConvertTime(calendarEvent.EndDate, TimeZoneInfo.Utc, systemTimeZone);
                                                item.DisplayEndDateTimeZone = string.Empty;
                                                break;
                                            }
                                        }

                                        item.DisplayStartDate = calendarEvent.StartDate.ToString("yyyy-MM-dd");
                                        item.DisplayEndDate = calendarEvent.EndDate.ToString("yyyy-MM-dd");

                                        item.StartDate = calendarEvent.StartDate;
                                        DateTime tempEndDate = calendarEvent.EndDate.AddDays(1);
                                        item.EndDate = new DateTime(tempEndDate.Year, tempEndDate.Month, tempEndDate.Day, 0, 0, 0, tempEndDate.Kind);
                                    }
                                    else
                                    {
                                        foreach (TimeZoneInfo systemTimeZone in TimeZoneInfo.GetSystemTimeZones())
                                        {
                                            string systemTimeZoneIanaId = string.Empty;

                                            if (systemTimeZone.HasIanaId)
                                            {
                                                systemTimeZoneIanaId = systemTimeZone.Id;
                                            }
                                            else if (TimeZoneInfo.TryConvertWindowsIdToIanaId(systemTimeZone.Id, out string ianaId))
                                            {
                                                systemTimeZoneIanaId = ianaId;
                                            }

                                            if (string.IsNullOrEmpty(systemTimeZoneIanaId))
                                            {
                                                continue;
                                            }
                                            else if (systemTimeZoneIanaId == calendarEvent.StartDateTimeZoneIanaId)
                                            {
                                                calendarEvent.StartDate = TimeZoneInfo.ConvertTime(calendarEvent.StartDate, TimeZoneInfo.Utc, systemTimeZone);
                                                item.DisplayStartDateTimeZone = systemTimeZone.StandardName;
                                                break;
                                            }
                                        }

                                        foreach (TimeZoneInfo systemTimeZone in TimeZoneInfo.GetSystemTimeZones())
                                        {
                                            string systemTimeZoneIanaId = string.Empty;

                                            if (systemTimeZone.HasIanaId)
                                            {
                                                systemTimeZoneIanaId = systemTimeZone.Id;
                                            }
                                            else if (TimeZoneInfo.TryConvertWindowsIdToIanaId(systemTimeZone.Id, out string ianaId))
                                            {
                                                systemTimeZoneIanaId = ianaId;
                                            }

                                            if (string.IsNullOrEmpty(systemTimeZoneIanaId))
                                            {
                                                continue;
                                            }
                                            else if (systemTimeZoneIanaId == calendarEvent.EndDateTimeZoneIanaId)
                                            {
                                                calendarEvent.EndDate = TimeZoneInfo.ConvertTime(calendarEvent.EndDate, TimeZoneInfo.Utc, systemTimeZone);
                                                item.DisplayEndDateTimeZone = systemTimeZone.StandardName;
                                                break;
                                            }
                                        }

                                        item.DisplayStartDate = calendarEvent.StartDate.ToString("yyyy-MM-dd HH:mm");
                                        item.DisplayEndDate = calendarEvent.EndDate.ToString("yyyy-MM-dd HH:mm");

                                        item.StartDate = calendarEvent.StartDate;
                                        DateTime tempEndDate = calendarEvent.EndDate.AddDays(1);
                                        item.EndDate = new DateTime(tempEndDate.Year, tempEndDate.Month, tempEndDate.Day, 0, 0, 0, tempEndDate.Kind);
                                    }

                                    if ((tempCalendars?.Where(x => x.Id == tempInputCalendar.Id)?.FirstOrDefault() ?? new Calendar()).Id != 0)
                                    {
                                        item.HtmlColorCode = tempCalendars?.Where(x => x.Id == calendarEvent.CalendarId)?.FirstOrDefault()?.HtmlColorCode;
                                        item.CalendarType = EnumHelper.GetDescription(CalendarType.My);
                                    }
                                    else if ((tempOtherCalendars?.Where(x => x.Id == tempInputCalendar.Id)?.FirstOrDefault() ?? new Calendar()).Id != 0)
                                    {
                                        item.HtmlColorCode = tempOtherCalendars?.Where(x => x.Id == calendarEvent.CalendarId)?.FirstOrDefault()?.HtmlColorCode;
                                        item.CalendarType = EnumHelper.GetDescription(CalendarType.Other);
                                    }

                                    calendarEvents.Add(item);
                                }
                            }
                        }

                        return calendarEvents.Count > 0
                            ? Json(new { result = true, calendarEvents = JsonConvert.SerializeObject(calendarEvents) })
                            : (IActionResult)Json(new { result = false, error = _localizer["Input is invalid"].Value });
                    }
                    catch
                    {
                        return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                    }
                }
                else
                {
                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                }
            }
            catch
            {
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.Admin)]
        public async Task<IActionResult> GetCalendarShareds()
        {
            try
            {
                _ = HttpContext.Session.TryGetValue(Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous.Utilities.Session.Account), out byte[] resultByte);
                List<Calendar> calendars = (await _calendarRepository.GetCalendarsAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email) ?? [])?.ToList();
                List<CalendarShared> calendarShareds = (await _calendarSharedRepository.GetCalendarSharedAsync() ?? [])?.ToList();

                foreach (Calendar calendar in calendars)
                {
                    CalendarShared calendarShared = calendarShareds?.Where(x => x.CalendarId == calendar.Id)?.FirstOrDefault() ?? new CalendarShared();

                    if (!(calendarShared.CalendarId > 0))
                    {
                        await _calendarSharedRepository.CreateCalendarSharedAsync(new CalendarShared() { CalendarId = calendar.Id, User = false, Anonymous = false });
                    }
                }

                List<CalendarShared> tempCalendarShareds = [];

                foreach (CalendarShared calendarShared in (await _calendarSharedRepository.GetCalendarSharedAsync() ?? [])?.ToList())
                {
                    Calendar item = calendars.Where(x => x.Id == calendarShared.CalendarId).FirstOrDefault() ?? new Calendar();

                    if (item.Id > 0)
                    {
                        tempCalendarShareds.Add(calendarShared);
                    }
                }

                var setCalendarShareds = (from calendar in calendars
                                          join sharedCalendar in tempCalendarShareds
                                          on calendar.Id equals sharedCalendar.CalendarId
                                          select new
                                          {
                                              Id = sharedCalendar.CalendarId,
                                              calendar.Name,
                                              sharedCalendar.User,
                                              Guest = sharedCalendar.Anonymous
                                          }).OrderBy(x => x.Name).ToList();


                return Json(new { result = true, setCalendarShareds });
            }
            catch
            {
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> GetBrowseCalendarsOfInterest()
        {
            try
            {
                bool isAccountSessionExist = HttpContext.Session.TryGetValue(Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous.Utilities.Session.Account), out byte[] resultByte);

                if (isAccountSessionExist)
                {
                    Account loginedAccount = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte));

                    if (loginedAccount.Role is not (Role.Admin or Role.User)) // 비 로그인 사용자인 경우
                    {
                        return Json(new { result = false, error = _localizer["Login required."].Value });
                    }

                    List<Calendar> allCalendars = await _calendarRepository.GetAllCalendarsAsync() ?? [];
                    List<CalendarShared> calendarShareds = await _calendarSharedRepository.GetCalendarSharedAsync() ?? [];
                    List<OtherCalendar> otherCalendars = await _otherCalendarRepository.GetOtherCalendarsAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);

                    switch (loginedAccount.Role)
                    {
                        case Role.User:
                            calendarShareds = calendarShareds.Where(x => x.User).ToList();
                            break;
                        default:
                            return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                    }

                    var browseCalendarsOfInterests = (from calendar in allCalendars
                                                      join calendarShared in calendarShareds
                                                      on calendar.Id equals calendarShared.CalendarId
                                                      // Left Join otherCalendars based on calendar.Id
                                                      join otherCalendar in otherCalendars
                                                      on calendar.Id equals otherCalendar.CalendarId into otherCalendarGroup
                                                      from otherCalendar in otherCalendarGroup.DefaultIfEmpty() // Left Join
                                                      select new
                                                      {
                                                          calendar.Id,
                                                          calendar.Name,
                                                          Checked = otherCalendar != null // otherCalendar가 존재하면 true, 없으면 false
                                                      }).OrderBy(x => x.Name).ToList();


                    return Json(new { result = true, browseCalendarsOfInterests });
                }
                else
                {
                    return Json(new { result = false, error = _localizer["Please Login to update calendar event"].Value });
                }
            }
            catch
            {
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
            }
        }
        #endregion

        #region Update

        #region Calendar
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.Admin)]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> UpdateCalendar([FromBody] CalendarInputViewModel calendarInputViewModel)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    try
                    {
                        _ = HttpContext.Session.TryGetValue(Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous.Utilities.Session.Account), out byte[] resultByte);

                        Calendar tempCalendar = calendarInputViewModel?.Calendars?.FirstOrDefault() ?? new Calendar();
                        tempCalendar.AccountEmail = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email;
                        tempCalendar.Updated = DateTime.UtcNow;

                        List<Calendar> tempCalendars = await _calendarRepository.GetCalendarsAsync(tempCalendar.AccountEmail);
                        Calendar previousCalendar = tempCalendars.Where(a => a.Id == tempCalendar.Id).FirstOrDefault();
                        tempCalendar.Created = previousCalendar.Created;

                        if (previousCalendar.Id > 0)
                        {
                            await _calendarRepository.UpdateCalendarAsync(tempCalendar);

                            return Json(new { result = true, message = _localizer["The calendar has been successfully updated."].Value, calendar = new Calendar() { Id = tempCalendar.Id, Name = tempCalendar.Name, HtmlColorCode = tempCalendar.HtmlColorCode } });
                        }
                        else
                        {
                            return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                        }
                    }
                    catch
                    {
                        return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                    }
                }
                else
                {
                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                }
            }
            catch
            {
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
            }
        }
        #endregion

        #region Calendar Event
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.Admin)]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> UpdateCalendarEvent([FromForm] CalendarEventInputViewModel calendarEventInputViewModel)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    calendarEventInputViewModel.Description ??= ""; // 내용이 아무것도 입력되지 않았다면

                    #region HtmlSanitizer
                    HtmlSanitizer sanitizer = new();
                    _ = sanitizer.AllowedAttributes.Add("class");
                    string sanitized = sanitizer.Sanitize(calendarEventInputViewModel.Description);
                    calendarEventInputViewModel.Description = sanitized;
                    #endregion

                    #region Decrypt img tag file path
                    HtmlDocument htmlDocument = new();
                    htmlDocument.LoadHtml(calendarEventInputViewModel.Description);

                    foreach (HtmlNode imgTag in htmlDocument.DocumentNode.Descendants("img") ?? [])
                    {
                        string encryptedFilePath = imgTag.GetAttributeValue("alt", "");
                        _ = imgTag.SetAttributeValue("alt", _rsa.Decrypt(encryptedFilePath));
                    }

                    calendarEventInputViewModel.Description = htmlDocument.DocumentNode.OuterHtml;
                    #endregion

                    if (calendarEventInputViewModel.Title.Length is not (> 0 and <= 100)) // 제목 길이
                    {
                        return Json(new { result = false, error = _localizer["Name length is must be between 1 and 100 characters."].Value });
                    }

                    if (calendarEventInputViewModel.Description.Length > 16384) // Description Maxlength: 16KB
                    {
                        return Json(new { result = false, error = _localizer["Description length is must be between 0 and 16384 characters."].Value });
                    }

                    if (calendarEventInputViewModel.CalendarEventUploadedFile != null) // 파일 첨부가 되었다면
                    {
                        if (!(Path.GetExtension(calendarEventInputViewModel.CalendarEventUploadedFile.FileName) == ".zip")) // zip 확장자만 가능
                        {
                            return Json(new { result = false, error = _localizer["Only zip extension allowed."].Value });
                        }

                        if (calendarEventInputViewModel.CalendarEventUploadedFile.Length is not (> 0 and <= 10485760)) // 10MB 이하만 가능
                        {
                            return Json(new { result = false, error = _localizer["uploaded file size must be smaller than 10MB."].Value });
                        }
                    }

                    try
                    {
                        bool isAccountSessionExist = HttpContext.Session.TryGetValue(Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous.Utilities.Session.Account), out byte[] resultByte);

                        if (isAccountSessionExist)
                        {
                            Account loginedAccount = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte));

                            Calendar calendarToValidate = (await _calendarRepository.GetCalendarsAsync(loginedAccount.Email)).Where(x => x.Id == calendarEventInputViewModel?.CalendarId).FirstOrDefault();

                            if (calendarToValidate is null)
                            {
                                return Json(new { result = false, error = _localizer["The calendar does not exists."].Value });
                            }

                            if (loginedAccount.Role is not (Role.Admin or Role.User)) // 비 로그인 사용자인 경우
                            {
                                return Json(new { result = false, error = _localizer["Please Login to update calendar event"].Value });
                            }

                            List<Calendar> calendars = await _calendarRepository.GetCalendarsAsync(loginedAccount.Email) ?? [];

                            CalendarEvent calendarEvent = await _calendarEventRepository.GetCalendarEventAsync(calendarEventInputViewModel.Id);
                            Calendar calendar = calendars?.Where(x => x.Id == calendarEvent.CalendarId)?.FirstOrDefault() ?? new Calendar();

                            if (!(calendarEvent.Id > 0))
                            {
                                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                            }

                            if (!(calendar.Id > 0))
                            {
                                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                            }

                            #region CalendarEvent
                            if (calendarEventInputViewModel.AllDay)
                            {
                                DateTime tempStartDate = new(Convert.ToInt32(calendarEventInputViewModel.StartDate.Split("-")[0]),
                                    Convert.ToInt32(calendarEventInputViewModel.StartDate.Split("-")[1]),
                                    Convert.ToInt32(calendarEventInputViewModel.StartDate.Split("-")[2]), 0, 0, 0, DateTimeKind.Utc);

                                DateTime tempEndDate = new(Convert.ToInt32(calendarEventInputViewModel.EndDate.Split("-")[0]),
                                    Convert.ToInt32(calendarEventInputViewModel.EndDate.Split("-")[1]),
                                    Convert.ToInt32(calendarEventInputViewModel.EndDate.Split("-")[2]), 0, 0, 0, DateTimeKind.Utc);

                                calendarEvent.CalendarId = calendarEventInputViewModel.CalendarId;
                                calendarEvent.Title = calendarEventInputViewModel.Title;
                                calendarEvent.Description = calendarEventInputViewModel.Description;
                                calendarEvent.AllDay = calendarEventInputViewModel.AllDay;
                                calendarEvent.StartDate = tempStartDate;
                                calendarEvent.EndDate = tempEndDate;
                                calendarEvent.StartDateTimeZoneIanaId = null;
                                calendarEvent.EndDateTimeZoneIanaId = null;
                                calendarEvent.Location = calendarEventInputViewModel.Location;
                                calendarEvent.Status = calendarEventInputViewModel.Status;
                                //calendarEvent.RecurrenceId = 0;
                                calendarEvent.Updated = DateTime.UtcNow;

                            }
                            else
                            {
                                string[] temporaryStartDate = calendarEventInputViewModel.StartDate.Split(" ");
                                string[] temporaryEndDate = calendarEventInputViewModel.EndDate.Split(" ");

                                DateTime tempStartDate = new(Convert.ToInt32(temporaryStartDate[0].Split("-")[0]),
                                    Convert.ToInt32(temporaryStartDate[0].Split("-")[1]),
                                    Convert.ToInt32(temporaryStartDate[0].Split("-")[2]),
                                    Convert.ToInt32(temporaryStartDate[1].Split(":")[0]),
                                    Convert.ToInt32(temporaryStartDate[1].Split(":")[1]),
                                    0, DateTimeKind.Unspecified);

                                DateTime tempEndDate = new(Convert.ToInt32(temporaryEndDate[0].Split("-")[0]),
                                    Convert.ToInt32(temporaryEndDate[0].Split("-")[1]),
                                    Convert.ToInt32(temporaryEndDate[0].Split("-")[2]),
                                    Convert.ToInt32(temporaryEndDate[1].Split(":")[0]),
                                    Convert.ToInt32(temporaryEndDate[1].Split(":")[1]),
                                    0, DateTimeKind.Unspecified);

                                foreach (TimeZoneInfo systemTimeZone in TimeZoneInfo.GetSystemTimeZones())
                                {
                                    string systemTimeZoneIanaId = string.Empty;

                                    if (systemTimeZone.HasIanaId)
                                    {
                                        systemTimeZoneIanaId = systemTimeZone.Id;
                                    }
                                    else if (TimeZoneInfo.TryConvertWindowsIdToIanaId(systemTimeZone.Id, out string ianaId))
                                    {
                                        systemTimeZoneIanaId = ianaId;
                                    }

                                    if (string.IsNullOrEmpty(systemTimeZoneIanaId))
                                    {
                                        continue;
                                    }
                                    else if (systemTimeZoneIanaId == calendarEventInputViewModel.StartDateTimeZoneIanaId)
                                    {
                                        tempStartDate = TimeZoneInfo.ConvertTime(tempStartDate, systemTimeZone, TimeZoneInfo.Utc);
                                        break;
                                    }
                                }

                                foreach (TimeZoneInfo systemTimeZone in TimeZoneInfo.GetSystemTimeZones())
                                {
                                    string systemTimeZoneIanaId = string.Empty;

                                    if (systemTimeZone.HasIanaId)
                                    {
                                        systemTimeZoneIanaId = systemTimeZone.Id;
                                    }
                                    else if (TimeZoneInfo.TryConvertWindowsIdToIanaId(systemTimeZone.Id, out string ianaId))
                                    {
                                        systemTimeZoneIanaId = ianaId;
                                    }

                                    if (string.IsNullOrEmpty(systemTimeZoneIanaId))
                                    {
                                        continue;
                                    }
                                    else if (systemTimeZoneIanaId == calendarEventInputViewModel.EndDateTimeZoneIanaId)
                                    {
                                        tempEndDate = TimeZoneInfo.ConvertTime(tempEndDate, systemTimeZone, TimeZoneInfo.Utc);
                                        break;
                                    }
                                }

                                calendarEvent.CalendarId = calendarEventInputViewModel.CalendarId;
                                calendarEvent.Title = calendarEventInputViewModel.Title;
                                calendarEvent.Description = calendarEventInputViewModel.Description;
                                calendarEvent.AllDay = calendarEventInputViewModel.AllDay;
                                calendarEvent.StartDate = tempStartDate;
                                calendarEvent.EndDate = tempEndDate;
                                calendarEvent.StartDateTimeZoneIanaId = calendarEventInputViewModel.StartDateTimeZoneIanaId;
                                calendarEvent.EndDateTimeZoneIanaId = calendarEventInputViewModel.EndDateTimeZoneIanaId;
                                calendarEvent.Location = calendarEventInputViewModel.Location;
                                calendarEvent.Status = calendarEventInputViewModel.Status;
                                //calendarEvent.RecurrenceId = 0;
                                calendarEvent.Updated = DateTime.UtcNow;
                            }

                            await _calendarEventRepository.UpdateCalendarEventAsync(calendarEvent);
                            #endregion

                            #region CalendarEventAttachedFile

                            _ = await _calendarEventAttachedFileRepository.DeleteCalendarEventAttachedFileAsync(calendarEvent.Id);

                            if (calendarEventInputViewModel.CalendarEventUploadedFile != null) // 첨부 파일 존재 시,
                            {
                                if (calendarEventInputViewModel.CalendarEventUploadedFile.Length is > 0 and <= 10485760) // calendarEventInputViewModel.CalendarEventUploadedFile Length : 파일 크기 0MB 이상, 10MB 이하
                                {
                                    var updatedCalendarEventId = calendarEvent.Id;

                                    string calendarEventAttachedFilePath = Path.Combine("upload", "Calendar", $"{loginedAccount.Role}Index", "calendarEventAttachedFiles", $"{updatedCalendarEventId}");
                                    string guid = Guid.NewGuid().ToString().ToUpper();
                                    string calendarEventAttachedFile = $"{guid}{Path.GetExtension(calendarEventInputViewModel.CalendarEventUploadedFile.FileName)}";
                                    string filePath = Path.Combine(calendarEventAttachedFilePath, calendarEventAttachedFile);

                                    _ = await _fileRepository.UploadAsync(calendarEventInputViewModel.CalendarEventUploadedFile, filePath);

                                    await _calendarEventAttachedFileRepository.SaveCalendarEventAttachedFileAsync(new CalendarEventAttachedFile()
                                    {
                                        CalendarEventId = updatedCalendarEventId,
                                        Size = Convert.ToInt32(calendarEventInputViewModel.CalendarEventUploadedFile.Length),
                                        Name = Path.GetFileNameWithoutExtension(calendarEventInputViewModel.CalendarEventUploadedFile.FileName),
                                        Extension = Path.GetExtension(calendarEventInputViewModel.CalendarEventUploadedFile.FileName),
                                        Path = $"upload/Calendar/{loginedAccount.Role}Index/calendarEventAttachedFiles/{updatedCalendarEventId}/{calendarEventAttachedFile}"
                                    });
                                }
                                else
                                {
                                    return Ok(new { result = false, errorMessage = _localizer["File Size must be smaller than 10MB."].Value });
                                }

                            }
                            #endregion

                            #region CalendarReminder

                            _ = await _calendarReminderRepository.DeleteCalendarEventReminderAsync(calendarEvent.Id);

                            List<Dictionary<string, object>> calendarReminders = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(calendarEventInputViewModel.SerializedCalendarReminders);

                            foreach (Dictionary<string, object> reminder in calendarReminders)
                            {
                                string method = reminder["Method"].ToString();
                                int? minutesBeforeEvent = reminder["MinutesBeforeEvent"] != null ? Convert.ToInt32(reminder["MinutesBeforeEvent"]) : null;
                                int? hoursBeforeEvent = reminder["HoursBeforeEvent"] != null ? Convert.ToInt32(reminder["HoursBeforeEvent"]) : null;
                                int? daysBeforeEvent = reminder["DaysBeforeEvent"] != null ? Convert.ToInt32(reminder["DaysBeforeEvent"]) : null;
                                int? weeksBeforeEvent = reminder["WeeksBeforeEvent"] != null ? Convert.ToInt32(reminder["WeeksBeforeEvent"]) : null;
                                string tempTimesBeforeEvent = reminder["TimesBeforeEvent"]?.ToString();
                                TimeSpan? timesBeforeEvent = !string.IsNullOrEmpty(tempTimesBeforeEvent)
                                    ? new TimeSpan(Convert.ToInt32(tempTimesBeforeEvent.Split(":")[0]), Convert.ToInt32(tempTimesBeforeEvent.Split(":")[1]), 0)
                                    : null;
                                CalendarEventReminder calendarEventReminder = new()
                                {
                                    CalendarEventId = calendarEvent.Id,
                                    Method = method,
                                    MinutesBeforeEvent = minutesBeforeEvent,
                                    HoursBeforeEvent = hoursBeforeEvent,
                                    DaysBeforeEvent = daysBeforeEvent,
                                    WeeksBeforeEvent = weeksBeforeEvent,
                                    TimesBeforeEvent = timesBeforeEvent != null ? TimeOnly.FromTimeSpan(timesBeforeEvent ?? new TimeSpan()) : null
                                };

                                await _calendarReminderRepository.CreateCalendarEventReminderAsync(calendarEventReminder);
                            }
                            #endregion

                            return Json(new { result = true, message = _localizer["The calendar event has been successfully updated."].Value });
                        }
                        else
                        {
                            return Json(new { result = false, error = _localizer["Please Login to update calendar event"].Value });
                        }
                    }
                    catch
                    {
                        return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                    }
                }
                else
                {
                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                }
            }
            catch
            {
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
            }
        }
        #endregion

        #region Calendar Shared
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.Admin)]
        public async Task<IActionResult> UpdateCalendarShared([FromBody] IEnumerable<CalendarShared> calendarShareds)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    try
                    {
                        foreach (CalendarShared calendarShared in calendarShareds)
                        {
                            await _calendarSharedRepository.UpdateCalendarSharedAsync(calendarShared);
                        }

                        #region Processing of Other Calendar
                        _ = HttpContext.Session.TryGetValue(Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous.Utilities.Session.Account), out byte[] resultByte);

                        Account loginedAccount = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte));

                        List<OtherCalendar> otherCalendars = await _otherCalendarRepository.GetAllOtherCalendarsAsync() ?? [];

                        foreach (CalendarShared tempCalendarShared in calendarShareds.Where(x => !x.User).ToList())
                        {
                            try
                            {
                                await _otherCalendarRepository.DeleteOtherCalendarAsync(otherCalendars.Where(x => x.CalendarId == tempCalendarShared.CalendarId).FirstOrDefault() ?? new OtherCalendar());
                            }
                            catch
                            {

                            }
                        }
                        #endregion

                        return Json(new { result = true, message = _localizer["The calendar shared has been successfully updated."].Value });
                    }
                    catch
                    {
                        return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                    }
                }
                else
                {
                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                }
            }
            catch
            {
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
            }
        }
        #endregion

        #region Other Calendar
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> UpdateOtherCalendar([FromBody] IEnumerable<OtherCalendar> otherCalendars)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    try
                    {
                        bool isAccountSessionExist = HttpContext.Session.TryGetValue(Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous.Utilities.Session.Account), out byte[] resultByte);

                        if (isAccountSessionExist)
                        {
                            Account loginedAccount = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte));

                            if (loginedAccount.Role is not (Role.Admin or Role.User)) // 비 로그인 사용자인 경우
                            {
                                return Json(new { result = false, error = _localizer["Login required."].Value });
                            }

                            // Delete previous otherCalendars
                            foreach (OtherCalendar previousOtherCalendar in await _otherCalendarRepository.GetOtherCalendarsAsync(loginedAccount.Email) ?? [])
                            {
                                await _otherCalendarRepository.DeleteOtherCalendarAsync(previousOtherCalendar);
                            }

                            // Create new otherCalendars
                            foreach (OtherCalendar otherCalendar in otherCalendars)
                            {
                                otherCalendar.AccountEmail = loginedAccount.Email;
                                await _otherCalendarRepository.CreateOtherCalendarAsync(otherCalendar);
                            }

                            return Json(new { result = true, message = _localizer["The other calendar has been successfully updated."].Value });
                        }
                        else
                        {
                            return Json(new { result = false, error = _localizer["Login required."].Value });
                        }
                    }
                    catch
                    {
                        return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                    }
                }
                else
                {
                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                }
            }
            catch
            {
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
            }
        }
        #endregion

        #endregion

        #region Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.Admin)]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> DeleteCalendar([FromBody] CalendarInputViewModel calendarInputViewModel)
        {
            try
            {
                _ = HttpContext.Session.TryGetValue(Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous.Utilities.Session.Account), out byte[] resultByte);

                Calendar tempCalendar = calendarInputViewModel?.Calendars?.FirstOrDefault() ?? new Calendar();
                tempCalendar.AccountEmail = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email;

                List<Calendar> tempCalendars = await _calendarRepository.GetCalendarsAsync(tempCalendar.AccountEmail);
                if ((tempCalendars.Where(a => a.Id == tempCalendar.Id).FirstOrDefault()?.Id ?? 0) > 0)
                {
                    await _calendarRepository.DeleteCalendarAsync(tempCalendar);

                    return Json(new { result = true, message = _localizer["The calendar has been successfully deleted."].Value, calendar = new Calendar() { Id = tempCalendar.Id } });
                }
                else
                {
                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                }
            }
            catch
            {
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.Admin)]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> DeleteCalendarEvent(int id)
        {
            try
            {
                _ = HttpContext.Session.TryGetValue(Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous.Utilities.Session.Account), out byte[] resultByte);

                List<Calendar> tempCalendars = await _calendarRepository.GetCalendarsAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email) ?? [];

                if (tempCalendars == null || tempCalendars.Count == 0)
                {
                    return Json(new { result = false, error = _localizer["No calendar exists"].Value });
                }
                else
                {

                    CalendarEvent tempCalendarEvent = await _calendarEventRepository.GetCalendarEventAsync(id);

                    if ((tempCalendars?.Where(x => x.Id == tempCalendarEvent.CalendarId)?.FirstOrDefault()?.Id ?? 0) == 0)
                    {
                        return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                    }

                    await _calendarEventRepository.DeleteCalendarEventAsync(tempCalendarEvent);

                    return Json(new { result = true, message = _localizer["The calendar event has been successfully deleted."].Value });
                }
            }
            catch
            {
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
            }
        }

        #endregion

        #endregion
    }
}
