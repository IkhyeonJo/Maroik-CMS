using Maroik.Core.Domain.Localization;

namespace Maroik.Website.Extensions;

/// <summary>
/// Cross-cutting helpers for Excel export operations.
/// </summary>
public static class ExportExcelExtensions
{
    private static readonly char[] _invalidFileNameChars = Path.GetInvalidFileNameChars();

    extension(string prefix)
    {
        /// <summary>
        /// Builds a timestamped Excel filename from a user-supplied prefix. Strips characters
        /// invalid in a file name (the prefix is caller-supplied, e.g. from a query string) so a
        /// crafted value can't throw when the framework builds the response's Content-Disposition
        /// header, instead of just quietly dropping from the name.
        /// </summary>
        public string ToExcelFileName(string timeZoneIanaId)
        {
            string safePrefix = new(prefix.Where(c => Array.IndexOf(_invalidFileNameChars, c) < 0).ToArray());
            return $"{safePrefix}-{DateTime.UtcNow.ConvertTimeByTimeZoneIanaId(timeZoneIanaId):yyyy-MM-dd-HH-mm-ss-fff}.xlsx";
        }
    }
}
