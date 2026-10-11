using System.Globalization;

namespace Maroik.Website.Extensions;

/// <summary>
/// The one format the fixed-schedule forms exchange a maturity date in (<see cref="Format"/>): parsed from what they submit,
/// and used for what the edit form is filled with.
/// </summary>
public static class MaturityDateExtensions
{
    /// <summary>The maturity date's wire format.</summary>
    public const string Format = "yyyy-MM-dd";

    extension(string? value)
    {
        /// <summary>
        /// Reads a submitted maturity date in exactly <see cref="Format"/> (format-locked, not culture-dependent free-form
        /// parsing). Anything else is the caller's input error — answered "Input is invalid" like any other invalid field —
        /// not an exception for the action's catch block to report as a temporary failure.
        /// </summary>
        public bool TryParseMaturityDate(out DateTime maturityDate) =>
            DateTime.TryParseExact(value, Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out maturityDate);
    }

    extension(DateTime maturityDate)
    {
        /// <summary>The date in <see cref="Format"/>, Gregorian whatever the thread's culture.</summary>
        public string ToMaturityDateString() => maturityDate.ToString(Format, CultureInfo.InvariantCulture);
    }
}
