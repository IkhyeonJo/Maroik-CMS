namespace Maroik.Website.Extensions;

/// <summary>
/// Presentation-formatting extension methods for <see cref="string"/> used by the account-book and
/// notice views. Lives in the Website layer because only the Website consumes it — it produces a
/// display label, not a domain value.
/// </summary>
public static class StringExtensions
{
    extension(string? value)
    {
        /// <summary>
        /// Formats a numeric amount string as a column header label that includes the currency unit,
        /// e.g. "Amount (KRW)". Null values default to an empty string.
        /// </summary>
        /// <param name="monetaryUnit">The currency unit to append (e.g. "KRW", "USD").</param>
        /// <returns>A formatted label string such as "Amount (KRW)".</returns>
        public string GetAmountLabel(string? monetaryUnit)
        {
            return $"{value ?? ""} ({monetaryUnit ?? ""})";
        }
    }
}
