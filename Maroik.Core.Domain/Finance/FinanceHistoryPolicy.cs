namespace Maroik.Core.Domain.Finance;

/// <summary>How far back an account's income / expenditure history goes (the dashboard's first selectable year).</summary>
public static class FinanceHistoryPolicy
{
    /// <summary>
    /// When the history starts: the earlier of the first income's and the first expenditure's
    /// <c>Created</c> (UTC). A missing record, or one stamped <see cref="DateTime.MinValue"/>, does not count;
    /// with nothing recorded the history starts at <paramref name="utcNow"/>.
    /// </summary>
    public static DateTime StartsAt(DateTime? firstIncomeCreated, DateTime? firstExpenditureCreated, DateTime utcNow)
    {
        DateTime[] recorded = [.. new[] { firstIncomeCreated, firstExpenditureCreated }
            .Where(created => created is { } value && value != DateTime.MinValue)
            .Select(created => created.GetValueOrDefault())];
        return recorded.Length == 0 ? utcNow : recorded.Min();
    }
}
