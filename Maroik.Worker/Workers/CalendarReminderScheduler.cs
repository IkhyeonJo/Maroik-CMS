using Maroik.Core.Contract.Interfaces;

namespace Maroik.Worker.Workers;

/// <summary>
/// Sends the due calendar reminders (<see cref="ICalendarReminderDispatchService"/>) when the worker starts and then once a minute,
/// on the injected <see cref="TimeProvider"/>. Each run gets a scope of its own (the dispatch service uses the scoped DbContext) and
/// handles at most <see cref="MaxPerRun"/> reminders, so a backlog is worked off minute by minute without one run holding the
/// worker. A run that fails is logged and the next one goes ahead; the reminders it did not handle stay due.
/// </summary>
public sealed class CalendarReminderScheduler(IServiceScopeFactory scopes, TimeProvider timeProvider, ILogger<CalendarReminderScheduler> logger)
    : BackgroundService
{
    /// <summary>The most reminders one run handles.</summary>
    public const int MaxPerRun = 100;

    /// <summary>How often the due reminders are looked for.</summary>
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, timeProvider);
        do
        {
            try
            {
                await using AsyncServiceScope scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ICalendarReminderDispatchService>().DispatchDueAsync(MaxPerRun, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Sending the due calendar reminders failed; trying again in a minute");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
