using System.Globalization;

namespace Maroik.Website.Extensions;

/// <summary>Extension methods for <see cref="long"/> to format byte counts for display.</summary>
public static class Int64Extensions
{
    extension(long bytes)
    {
        /// <summary>Formats a byte count as megabytes with up to one decimal place (e.g. "9.5").</summary>
        public string ToMegabytesLabel() => (bytes / 1048576.0).ToString("0.#", CultureInfo.CurrentCulture);

        /// <summary>Formats a byte count as a thousands-grouped, truncated kilobyte value (e.g. "1,024").</summary>
        public string ToKilobytesLabel() => ((int)(bytes / 1024)).ToString("#,###0.#", CultureInfo.CurrentCulture);

        /// <summary>
        /// Formats a byte count in the largest 1024-based unit (B, KB, MB, GB, TB) that keeps it at least 1, with up to one decimal
        /// place (e.g. "12.3 MB", "30 GB") — the File Manager's sizes and storage usage.
        /// </summary>
        public string ToSizeLabel()
        {
            string[] units = ["B", "KB", "MB", "GB", "TB"];
            double value = bytes;
            int unit = 0;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }
            return $"{value.ToString("0.#", CultureInfo.CurrentCulture)} {units[unit]}";
        }
    }
}
