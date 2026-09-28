using System.Globalization;

namespace Maroik.Core.Domain.Localization;

/// <summary>
/// Extension methods for <see cref="DateTime"/> to simplify time-zone conversions.
/// </summary>
public static class DateTimeExtensions
{
    extension(DateTime value)
    {
        /// <summary>
        /// Converts a <see cref="DateTime"/> value from UTC to the time zone specified by the IANA ID.
        /// Returns the original value unchanged if the time-zone ID is invalid or the conversion fails.
        /// </summary>
        /// <param name="timeZoneIanaId">A valid IANA time-zone ID (e.g. "Asia/Seoul", "America/New_York").</param>
        /// <returns>The converted local date/time, or <paramref name="value"/> on error.</returns>
        public DateTime ConvertTimeByTimeZoneIanaId(string timeZoneIanaId)
        {
            try
            {
                // The source value is always UTC regardless of its DateTimeKind (e.g. Npgsql
                // returns Unspecified for "timestamp without time zone" columns), so the source
                // must be pinned to UTC explicitly rather than relying on ConvertTimeBySystemTimeZoneId's
                // Kind-based inference, which would otherwise silently fall back to the OS local zone.
                TimeZoneInfo targetZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneIanaId);
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(value, DateTimeKind.Utc), targetZone);
            }
            catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                // Unrecognized / corrupt zone id: fall back to the value unchanged (documented, and
                // relied on by display paths). A null id or any other unexpected failure is no
                // longer silently swallowed — it propagates so the real bug surfaces.
                return value;
            }
        }

        /// <summary>
        /// Formats a UTC timestamp for display. When <paramref name="viewerSeesLocalTime"/> is
        /// <see langword="true"/> (a logged-in viewer with a known time zone), the value is converted
        /// to <paramref name="timeZoneIanaId"/>; otherwise (e.g. an anonymous viewer) the raw UTC value
        /// is returned in ISO-8601 ("yyyy-MM-ddTHH:mm:ssZ") form. Whether a given viewer sees local
        /// time is a caller decision (e.g. an Account bounded-context role check) — this helper stays
        /// role-agnostic so Localization does not depend on the Account bounded context.
        /// </summary>
        public string ToViewerFormattedString(bool viewerSeesLocalTime, string timeZoneIanaId) =>
            viewerSeesLocalTime
                ? value.ConvertTimeByTimeZoneIanaId(timeZoneIanaId).ToString(CultureInfo.InvariantCulture)
                : value.ToString("yyyy-MM-ddTHH:mm:ssZ");

        /// <summary>
        /// Converts a local (<see cref="DateTimeKind.Unspecified"/>) <see cref="DateTime"/> value to UTC,
        /// treating it as wall-clock time in the time zone specified by the IANA ID.
        /// Returns the original value unchanged only if the time-zone ID is unrecognized. A wall-clock
        /// time that falls in the zone's spring-forward DST gap is rolled forward past the gap rather
        /// than persisted verbatim as if it were already UTC.
        /// The inverse of <see cref="ConvertTimeByTimeZoneIanaId"/>.
        /// </summary>
        /// <param name="timeZoneIanaId">A valid IANA time-zone ID (e.g. "Asia/Seoul", "America/New_York").</param>
        public DateTime ConvertToUtcByTimeZoneIanaId(string timeZoneIanaId)
        {
            try
            {
                TimeZoneInfo sourceZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneIanaId);
                DateTime local = DateTime.SpecifyKind(value, DateTimeKind.Unspecified);

                // A wall-clock time inside the DST gap does not exist in this zone;
                // TimeZoneInfo.ConvertTimeToUtc would throw and the old bare catch then persisted
                // the wall-clock value AS IF it were UTC (event shows hours off for every viewer).
                // Advance past the skipped hour instead (DST deltas are 1h in every zone in use).
                if (sourceZone.IsInvalidTime(local))
                    local = local.AddHours(1);

                return TimeZoneInfo.ConvertTimeToUtc(local, sourceZone);
            }
            catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                // Unrecognized / corrupt zone id only. A null id or any other unexpected failure
                // now propagates instead of silently storing wall-clock time as UTC.
                return value;
            }
        }
    }

    extension(string timeZoneIanaId)
    {
        /// <summary>
        /// Returns the <see cref="TimeZoneInfo.StandardName"/> for an IANA time-zone ID
        /// (e.g. "Asia/Seoul" → "Korea Standard Time").
        /// Falls back to the raw IANA ID string if the ID is invalid or unrecognized; a null ID throws
        /// <see cref="ArgumentNullException"/>.
        /// </summary>
        public string ToTimeZoneStandardName()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneIanaId).StandardName;
            }
            catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                // Unrecognized / corrupt zone id only (see ConvertTimeByTimeZoneIanaId): a null id is a
                // caller bug and propagates.
                return timeZoneIanaId;
            }
        }
    }
}
