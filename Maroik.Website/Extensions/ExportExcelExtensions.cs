using Maroik.Core.Domain.Localization;

namespace Maroik.Website.Extensions;

/// <summary>
/// Cross-cutting helpers for Excel export operations.
/// </summary>
public static class ExportExcelExtensions
{
    /// <summary>Characters the host OS forbids in a file name, read once.</summary>
    private static readonly char[] _invalidFileNameChars = Path.GetInvalidFileNameChars();

    extension(string prefix)
    {
        /// <summary>
        /// Builds a timestamped Excel filename from a user-supplied prefix. Strips characters
        /// invalid in a file name (the prefix is caller-supplied, e.g. from a query string) so a
        /// crafted value can't make the framework throw while it builds the response's
        /// Content-Disposition header — such characters are simply dropped from the name. The timestamp
        /// suffix is <paramref name="utcNow"/> (the request's clock reading) in <paramref name="timeZoneIanaId"/>.
        /// </summary>
        public string ToExcelFileName(string timeZoneIanaId, DateTime utcNow)
        {
            string safePrefix = new(prefix.Where(c => Array.IndexOf(_invalidFileNameChars, c) < 0).ToArray());
            return $"{safePrefix}-{utcNow.ConvertTimeByTimeZoneIanaId(timeZoneIanaId):yyyy-MM-dd-HH-mm-ss-fff}.xlsx";
        }
    }
}
