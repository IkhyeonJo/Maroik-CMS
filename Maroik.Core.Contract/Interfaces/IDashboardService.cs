using Maroik.Core.Contract.Dtos;

namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Read service for the user dashboard, aggregating income, expenditure, and asset data
/// and computing notification badge counts. Its one write is a self-correction: when the account's stored
/// <c>DefaultMonetaryUnit</c> differs from the one resolved from its assets, the summary persists the resolved
/// value (never for the demo account), so other screens do not show a stale currency.
/// </summary>
public interface IDashboardService
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
