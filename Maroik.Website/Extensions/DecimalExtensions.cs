using System.Globalization;

namespace Maroik.Website.Extensions;

/// <summary>
/// Presentation-formatting extension methods for <see cref="decimal"/> that produce clean display
/// strings without unnecessary trailing zeros. Lives in the Website layer because only the Website
/// (views, Excel export, grid search) consumes it.
/// </summary>
public static class DecimalExtensions
{
    extension(decimal value)
    {
        /// <summary>
        /// Returns a string representation of <paramref name="value"/> with trailing decimal zeros removed.
        /// Whole numbers are formatted without a decimal point (e.g. 1000.00 → "1000").
        /// </summary>
        public string TrimTrailingZeros() =>
            value.ToString("0.############", CultureInfo.InvariantCulture);
    }

    extension(decimal? value)
    {
        /// <summary>
        /// Nullable overload of <see cref="TrimTrailingZeros(decimal)"/>.
        /// Treats null as zero.
        /// </summary>
        public string TrimTrailingZeros()
            => (value ?? 0m).TrimTrailingZeros();
    }
}
