using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Primitives;

namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Pure query service for the user dashboard, aggregating income, expenditure, and asset data
/// and computing notification badge counts. Performs no state mutations.
/// </summary>
public interface IDashboardService : IQueryService
{
    /// <summary>
    /// Builds the full dashboard summary for the given account, year, and month.
    /// All date comparisons are performed in the user's <paramref name="timeZoneId"/>.
    /// </summary>
    Task<DashboardDto> GetSummaryAsync(string accountEmail, string year, string month, string timeZoneId, CancellationToken ct = default);

    /// <summary>
    /// Counts fixed income and expenditure items that are approaching their deposit day
    /// within <paramref name="noticeMaturityDateDay"/> days, or whose maturity date has passed.
    /// "Today" is evaluated in the user's <paramref name="timeZoneId"/>.
    /// Used to populate the navigation header badges.
    /// </summary>
    Task<NotificationDto> GetNoticeCountsAsync(string email, int noticeMaturityDateDay, string timeZoneId, CancellationToken ct = default);

    /// <summary>
    /// Reads the host and Docker resource log files and returns the parsed metrics
    /// used by the admin dashboard.
    /// </summary>
    ServerResourceDto GetServerResourceSummary(string hostResourceFilePath, string dockerResourceFilePath);
}
