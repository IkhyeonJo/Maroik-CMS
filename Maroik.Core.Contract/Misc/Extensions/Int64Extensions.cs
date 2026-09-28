namespace Maroik.Core.Contract.Misc.Extensions;

/// <summary>Extension methods for <see cref="long"/> to format byte counts for display.</summary>
public static class Int64Extensions
{
    extension(long bytes)
    {
        /// <summary>Formats a byte count as megabytes with up to one decimal place (e.g. "9.5").</summary>
        public string ToMegabytesLabel() => (bytes / 1048576.0).ToString("0.#");

        /// <summary>Formats a byte count as a thousands-grouped, truncated kilobyte value (e.g. "1,024").</summary>
        public string ToKilobytesLabel() => ((int)(bytes / 1024)).ToString("#,###0.#");
    }
}
