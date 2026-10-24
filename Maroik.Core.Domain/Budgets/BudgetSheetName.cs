using System.Globalization;

namespace Maroik.Core.Domain.Budgets;

/// <summary>
/// The names formulas use for sheets: the kind and the year (Income2026, Expenditure2025), or the kind alone for the sheet of the
/// same year as the formula's own sheet (Income!, Expenditure!). Any case.
/// </summary>
public static class BudgetSheetName
{
    /// <summary>The name of a sheet.</summary>
    public static string Format(BudgetKind kind, int year) => kind + year.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Reads a sheet name; a name without a year means <paramref name="homeYear"/>. False for any other word, and for a year outside
    /// 1..9999 or written with a sign or a leading zero.
    /// </summary>
    public static bool TryParse(string text, int homeYear, out BudgetKind kind, out int year)
    {
        foreach (BudgetKind candidate in Enum.GetValues<BudgetKind>())
        {
            string name = candidate.ToString();
            if (!text.StartsWith(name, StringComparison.OrdinalIgnoreCase)) continue;
            string digits = text[name.Length..];
            kind = candidate;
            if (digits.Length == 0)
            {
                year = homeYear;
                return true;
            }

            if (digits.Length <= 4 && digits[0] != '0' && digits.All(char.IsAsciiDigit))
            {
                year = int.Parse(digits, CultureInfo.InvariantCulture);
                return true;
            }
        }

        kind = default;
        year = 0;
        return false;
    }
}
