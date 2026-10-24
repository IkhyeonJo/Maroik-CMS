using System.Globalization;
using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Messages;
using Maroik.Core.Contract.Settings;
using Maroik.Core.Domain.Accounts;
using Maroik.Core.Domain.Calendars;
using Maroik.Core.Domain.Extensions;
using Calendar = Maroik.Core.Domain.Calendars.Calendar;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Maroik.Core.Service.Services;

/// <summary>
/// Sends the calendar reminders that are due, by e-mail through the mail queue (<see cref="IEmailPublisher"/> → the worker's
/// e-mail consumer → SMTP, with its retry and dead-letter path). Each reminder is one transaction: it is locked (a second worker
/// skips it), e-mailed to its calendar's owner or skipped per <see cref="ReminderDeliveryPolicy"/>, and marked handled. A queue
/// failure rolls that reminder back, so it is tried again on the next run; nothing holds a row lock across more than one publish.
/// </summary>
public sealed class CalendarReminderDispatchService(
    ICalendarEventReminderRepository reminderRepository,
    ICalendarEventRepository eventRepository,
    ICalendarRepository calendarRepository,
    IAccountRepository accountRepository,
    IEmailPublisher emailPublisher,
    IMailClient mailClient,
    IUnitOfWork unitOfWork,
    IOptions<ServerSetting> settings,
    ILogger<CalendarReminderDispatchService> logger,
    TimeProvider timeProvider) : ICalendarReminderDispatchService
{
    /// <summary>Shown for an all-day event, in both languages (an account stores no language).</summary>
    private const string AllDayLabel = "종일 / All day";

    /// <inheritdoc />
    public async Task<int> DispatchDueAsync(int max, CancellationToken ct = default)
    {
        int handled = 0;
        while (handled < max)
        {
            DateTime now = timeProvider.GetUtcNow().UtcDateTime;
            await unitOfWork.BeginAsync(ct: ct);
            try
            {
                CalendarEventReminder? reminder = (await reminderRepository.LockDueAsync(now, 1, ct)).FirstOrDefault();
                if (reminder is null)
                {
                    await unitOfWork.RollbackAsync(ct);
                    return handled;
                }

                CalendarEvent? calendarEvent = await eventRepository.FindByIdAsync(reminder.CalendarEventId, ct);
                Calendar? calendar = calendarEvent is null ? null : await calendarRepository.FindByIdAsync(calendarEvent.CalendarId, ct);
                Account? owner = calendar is null ? null : await accountRepository.FindByEmailAsync(calendar.AccountEmail.Value, ct);
                string? skipReason = ReminderDeliveryPolicy.SkipReason(
                    owner is null ? null : new ReminderRecipient(owner.Email.Value, owner.Deleted, owner.Locked, owner.EmailConfirmed),
                    settings.Value.DemoAccountEmail, reminder.FireAtUtc, now);

                if (skipReason is null && owner is not null && calendarEvent is not null && calendar is not null)
                {
                    try
                    {
                        await emailPublisher.PublishAsync(BuildMessage(reminder, calendarEvent, calendar, owner), ct);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogWarning(ex, "Could not queue calendar reminder {ReminderId} for {Email}; it is tried again on the next run",
                            reminder.Id, owner.Email.Value);
                        await unitOfWork.RollbackAsync(ct);
                        return handled;
                    }

                    logger.LogInformation("Sent calendar reminder {ReminderId} of event {CalendarEventId} to {Email}",
                        reminder.Id, reminder.CalendarEventId, owner.Email.Value);
                }
                else
                {
                    logger.LogInformation("Skipped calendar reminder {ReminderId} of event {CalendarEventId}: {Reason}",
                        reminder.Id, reminder.CalendarEventId, skipReason);
                }

                reminder.MarkHandled(now);
                await reminderRepository.UpdateEntityAsync(reminder, ct);
                await unitOfWork.CommitAsync(ct);
                handled++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to handle the due calendar reminders");
                await unitOfWork.RollbackAsync(ct);
                return handled;
            }
        }

        return handled;
    }

    /// <summary>
    /// The reminder e-mail: to the owner, the subject the event's title and start, the body (<see cref="IMailClient.GetCalendarReminderBody"/>)
    /// its time, place and calendar with a link to the owner's calendar page. A timed event's time is shown in the owner's time zone;
    /// an all-day event's dates as they are.
    /// </summary>
    private SendEmailMessage BuildMessage(CalendarEventReminder reminder, CalendarEvent calendarEvent, Calendar calendar, Account owner)
    {
        string start, when;
        if (calendarEvent.AllDay)
        {
            string first = calendarEvent.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            string last = calendarEvent.EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            start = $"{first} ({AllDayLabel})";
            when = first == last ? start : $"{first} – {last} ({AllDayLabel})";
        }
        else
        {
            string zone = owner.TimeZone.Value;
            DateTime from = calendarEvent.StartDate.ConvertTimeByTimeZoneIanaId(zone);
            DateTime to = calendarEvent.EndDate.ConvertTimeByTimeZoneIanaId(zone);
            string fromText = from.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            string toText = to.ToString(from.Date == to.Date ? "HH:mm" : "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            start = $"{fromText} ({zone})";
            when = $"{fromText} – {toText} ({zone})";
        }

        string body = mailClient.GetCalendarReminderBody(
            new CalendarReminderMail(calendarEvent.Title, when, calendarEvent.Location, calendar.Name,
                owner.Role.IsAdmin ? "Calendar/AdminIndex" : "Calendar/UserIndex"),
            settings.Value.DomainName ?? "");
        return new SendEmailMessage(owner.Email.Value, $"[Maroik] {calendarEvent.Title} — {start}", body,
            $"calendar-reminder-{reminder.Id.ToString(CultureInfo.InvariantCulture)}");
    }
}
