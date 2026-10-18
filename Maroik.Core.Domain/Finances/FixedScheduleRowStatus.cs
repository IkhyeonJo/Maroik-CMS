
namespace Maroik.Core.Domain.Finances;

/// <summary>Notice-grid row status for a fixed schedule, as decided by <see cref="FixedSchedulePolicy.GetRowStatus"/>.</summary>
public enum FixedScheduleRowStatus
{
    /// <summary>No further recurrences are expected; the maturity date has passed.</summary>
    Expired,

    /// <summary>The next recurrence is approaching within the notification window.</summary>
    Noticed,

    /// <summary>Neither expired nor noticed.</summary>
    Normal
}
