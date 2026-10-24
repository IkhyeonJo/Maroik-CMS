using Maroik.Core.Domain.Extensions;
using Maroik.Core.Domain.Finances;

namespace Maroik.Core.Domain.Budgets;

/// <summary>
/// The actual side of the budgets: real income and expenditure records summed per kind, line (main class, sub-class, currency) and
/// month. A record's month is its creation time in the owner's time zone (as the dashboard counts it); fixed schedules are not
/// records until one is entered.
/// </summary>
public sealed class BudgetActuals
{
    /// <summary>The sums, keyed by kind, year, line and month.</summary>
    private readonly Dictionary<(BudgetKind Kind, int Year, BudgetLine Line), decimal[]> _sums = [];

    /// <summary>Use <see cref="From"/>.</summary>
    private BudgetActuals() { }

    /// <summary>The actuals of the given records, months taken in <paramref name="timeZoneIanaId"/>.</summary>
    public static BudgetActuals From(IEnumerable<Income> incomes, IEnumerable<Expenditure> expenditures, string timeZoneIanaId)
    {
        var actuals = new BudgetActuals();
        foreach (Income income in incomes)
            actuals.Add(BudgetKind.Income, income.Created.ConvertTimeByTimeZoneIanaId(timeZoneIanaId), income.MainClass, income.SubClass, income.Amount.Currency.Value, income.Amount.Amount);
        foreach (Expenditure expenditure in expenditures)
            actuals.Add(BudgetKind.Expenditure, expenditure.Created.ConvertTimeByTimeZoneIanaId(timeZoneIanaId), expenditure.MainClass, expenditure.SubClass, expenditure.Amount.Currency.Value, expenditure.Amount.Amount);
        return actuals;
    }

    /// <summary>Adds one record.</summary>
    private void Add(BudgetKind kind, DateTime local, string mainClass, string subClass, string currency, decimal amount)
    {
        var key = (kind, local.Year, new BudgetLine(mainClass, subClass, currency));
        if (!_sums.TryGetValue(key, out decimal[]? months))
            _sums[key] = months = new decimal[12];
        months[local.Month - 1] += amount;
    }

    /// <summary>The actual total of a line in a month (0 when there are no records).</summary>
    public decimal Of(BudgetKind kind, int year, int month, BudgetLine line) =>
        _sums.TryGetValue((kind, year, line), out decimal[]? months) ? months[month - 1] : 0m;

    /// <summary>Every line a year has records for, with its twelve monthly totals.</summary>
    public IReadOnlyList<(BudgetLine Line, IReadOnlyList<decimal> Months)> LinesOf(BudgetKind kind, int year) =>
        [.. _sums.Where(p => p.Key.Kind == kind && p.Key.Year == year).Select(p => (p.Key.Line, (IReadOnlyList<decimal>)p.Value))];
}
