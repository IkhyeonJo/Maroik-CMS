namespace Maroik.Core.Domain.Finance;

/// <summary>
/// Computes per-subClass totals and percentage-of-total breakdowns for a set of income or
/// expenditure records already filtered to one main class (e.g. "RegularIncome",
/// "ConsumerSpending"). The dashboard's multi-property summary (labor/business/pension income,
/// meal/housing/education spending, ...) is built entirely from this, so the presentation layer
/// only copies pre-computed numbers instead of re-deriving the same sums and percentages.
/// </summary>
public static class FinanceBreakdownPolicy
{
    /// <summary>
    /// <paramref name="Total"/> is the sum of every item's amount, whatever its subClass — so a
    /// subClass not otherwise recognized is still counted in the total (and therefore in every
    /// other subClass's percentage share), it just has no entry of its own in the two dictionaries.
    /// <paramref name="AmountBySubClass"/> sums amounts per subClass actually present in the input.
    /// <paramref name="PercentageBySubClass"/> is each subClass's share of <paramref name="Total"/>
    /// as 0-100 (empty when <paramref name="Total"/> is zero, avoiding a division by zero).
    /// </summary>
    public sealed record SubClassBreakdown(
        decimal Total,
        IReadOnlyDictionary<string, decimal> AmountBySubClass,
        IReadOnlyDictionary<string, double> PercentageBySubClass);

    /// <summary>
    /// Groups <paramref name="items"/> (already filtered to one main class) by subClass and
    /// computes each one's amount and percentage-of-total share.
    /// </summary>
    public static SubClassBreakdown Compute(IEnumerable<(string? SubClass, decimal Amount)> items)
    {
        var amountBySubClass = new Dictionary<string, decimal>(StringComparer.Ordinal);
        decimal total = 0m;

        foreach ((string? subClass, decimal amount) in items)
        {
            total += amount;
            string key = subClass ?? "";
            amountBySubClass[key] = amountBySubClass.GetValueOrDefault(key) + amount;
        }

        IReadOnlyDictionary<string, double> percentageBySubClass = total == 0m
            ? new Dictionary<string, double>(StringComparer.Ordinal)
            : amountBySubClass.ToDictionary(kv => kv.Key, kv => 100.0 * (double)kv.Value / (double)total, StringComparer.Ordinal);

        return new SubClassBreakdown(total, amountBySubClass, percentageBySubClass);
    }
}
