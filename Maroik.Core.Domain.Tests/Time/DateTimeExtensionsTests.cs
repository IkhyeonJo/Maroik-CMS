using System.Globalization;
using Maroik.Core.Domain.Time;
namespace Maroik.Core.Domain.Tests.Time;

/// <summary>
/// Unit tests for <see cref="DateTimeExtensions"/>.
/// Verifies UTC-to-local conversion using IANA time-zone IDs and graceful handling
/// of unknown or null zone identifiers.
/// </summary>
public class DateTimeExtensionsTests
{
    // -- ConvertTimeByTimeZoneIanaId ------------------------------------------

    /// <summary>Verifies that <c>ConvertTimeByTimeZoneIanaId</c> converts utc to local time for valid iana id.</summary>
    [Fact]
    public void ConvertTimeByTimeZoneIanaId_ConvertsUtcToLocalTime_ForValidIanaId()
    {
        // UTC noon should be 9 PM in Asia/Seoul (UTC+9)
        var utcNoon = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        DateTime result = utcNoon.ConvertTimeByTimeZoneIanaId("Asia/Seoul");

        // Asia/Seoul is UTC+9; noon UTC = 21:00 KST
        Assert.Equal(21, result.Hour);
        Assert.Equal(0, result.Minute);
    }

    /// <summary>Verifies that <c>ConvertTimeByTimeZoneIanaId</c> returns original value for invalid iana id.</summary>
    [Fact]
    public void ConvertTimeByTimeZoneIanaId_ReturnsOriginalValue_ForInvalidIanaId()
    {
        var original = new DateTime(2025, 1, 1, 10, 0, 0);

        DateTime result = original.ConvertTimeByTimeZoneIanaId("Invalid/Timezone/ID");

        Assert.Equal(original, result);
    }

    /// <summary>Verifies that <c>ConvertTimeByTimeZoneIanaId</c> returns original value for empty iana id.</summary>
    [Fact]
    public void ConvertTimeByTimeZoneIanaId_ReturnsOriginalValue_ForEmptyIanaId()
    {
        var original = new DateTime(2025, 3, 15, 8, 30, 0);

        DateTime result = original.ConvertTimeByTimeZoneIanaId("");

        Assert.Equal(original, result);
    }

    /// <summary>Verifies that <c>ConvertTimeByTimeZoneIanaId</c> returns utc time for utc iana id.</summary>
    [Fact]
    public void ConvertTimeByTimeZoneIanaId_ReturnsUtcTime_ForUtcIanaId()
    {
        var utcTime = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        DateTime result = utcTime.ConvertTimeByTimeZoneIanaId("UTC");

        Assert.Equal(12, result.Hour);
        Assert.Equal(0, result.Minute);
    }

    /// <summary>Verifies that <c>ConvertTimeByTimeZoneIanaId</c> handles negative offsets.</summary>
    [Fact]
    public void ConvertTimeByTimeZoneIanaId_HandlesNegativeOffsets()
    {
        // UTC noon in America/New_York (UTC-5 in winter) should be 7 AM
        var utcNoon = new DateTime(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc);

        DateTime result = utcNoon.ConvertTimeByTimeZoneIanaId("America/New_York");

        Assert.Equal(7, result.Hour);
    }

    // -- ToViewerFormattedString ------------------------------------------------

    /// <summary>Verifies that a viewer who sees local time gets the value converted to their time zone.</summary>
    [Fact]
    public void ToViewerFormattedString_ViewerSeesLocalTime_ReturnsLocalTimeConvertedValue()
    {
        var utcNoon = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        string result = utcNoon.ToViewerFormattedString(viewerSeesLocalTime: true, "Asia/Seoul");

        Assert.Equal(utcNoon.ConvertTimeByTimeZoneIanaId("Asia/Seoul").ToString(CultureInfo.InvariantCulture), result);
    }

    /// <summary>Verifies that a viewer who does not see local time gets the raw, unconverted UTC value in ISO-8601 form.</summary>
    [Fact]
    public void ToViewerFormattedString_ViewerDoesNotSeeLocalTime_ReturnsRawUtcIso8601()
    {
        var utcNoon = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        string result = utcNoon.ToViewerFormattedString(viewerSeesLocalTime: false, "Asia/Seoul");

        Assert.Equal("2025-06-15T12:00:00Z", result);
    }

    // -- ConvertToUtcByTimeZoneIanaId --------------------------------------------

    /// <summary>Verifies that <c>ConvertToUtcByTimeZoneIanaId</c> converts local time to UTC for a valid IANA id.</summary>
    [Fact]
    public void ConvertToUtcByTimeZoneIanaId_ConvertsLocalToUtc_ForValidIanaId()
    {
        // 21:00 in Asia/Seoul (UTC+9) should be noon UTC
        var localSeoul = new DateTime(2025, 6, 15, 21, 0, 0, DateTimeKind.Unspecified);

        DateTime result = localSeoul.ConvertToUtcByTimeZoneIanaId("Asia/Seoul");

        Assert.Equal(12, result.Hour);
        Assert.Equal(0, result.Minute);
    }

    /// <summary>Verifies that <c>ConvertToUtcByTimeZoneIanaId</c> is the inverse of <c>ConvertTimeByTimeZoneIanaId</c>.</summary>
    [Fact]
    public void ConvertToUtcByTimeZoneIanaId_RoundTrips_WithConvertTimeByTimeZoneIanaId()
    {
        var utcNoon = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        DateTime local = utcNoon.ConvertTimeByTimeZoneIanaId("America/New_York");
        DateTime roundTripped = local.ConvertToUtcByTimeZoneIanaId("America/New_York");

        Assert.Equal(utcNoon.Hour, roundTripped.Hour);
        Assert.Equal(utcNoon.Minute, roundTripped.Minute);
    }

    /// <summary>Verifies that <c>ConvertToUtcByTimeZoneIanaId</c> returns original value for invalid iana id.</summary>
    [Fact]
    public void ConvertToUtcByTimeZoneIanaId_ReturnsOriginalValue_ForInvalidIanaId()
    {
        var original = new DateTime(2025, 1, 1, 10, 0, 0);

        DateTime result = original.ConvertToUtcByTimeZoneIanaId("Invalid/Timezone/ID");

        Assert.Equal(original, result);
    }

    /// <summary>
    /// A wall-clock time inside the spring-forward DST gap does not exist; it must be rolled
    /// forward past the skipped hour, not persisted verbatim as if it were already UTC. In
    /// America/New_York on 2026-03-08 the clocks jump 02:00 → 03:00, so 02:30 local is really
    /// 03:30 EDT = 07:30 UTC.
    /// </summary>
    [Fact]
    public void ConvertToUtcByTimeZoneIanaId_RollsForward_ForDstGapLocalTime()
    {
        var gap = new DateTime(2026, 3, 8, 2, 30, 0, DateTimeKind.Unspecified);

        DateTime result = gap.ConvertToUtcByTimeZoneIanaId("America/New_York");

        Assert.Equal(new DateTime(2026, 3, 8, 7, 30, 0), result);
        Assert.NotEqual(gap, result); // not the silently-passed-through wall-clock value
    }

    // -- ToTimeZoneStandardName ---------------------------------------------------

    /// <summary>Verifies that <c>ToTimeZoneStandardName</c> returns the standard display name for a valid IANA id.</summary>
    [Fact]
    public void ToTimeZoneStandardName_ReturnsStandardName_ForValidIanaId()
    {
        string result = "Asia/Seoul".ToTimeZoneStandardName();

        Assert.Equal(TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul").StandardName, result);
    }

    /// <summary>Verifies that <c>ToTimeZoneStandardName</c> falls back to the raw id for an invalid IANA id.</summary>
    [Fact]
    public void ToTimeZoneStandardName_ReturnsRawId_ForInvalidIanaId()
    {
        string result = "Invalid/Timezone/ID".ToTimeZoneStandardName();

        Assert.Equal("Invalid/Timezone/ID", result);
    }

    /// <summary>
    /// Verifies that only an unrecognized / corrupt zone id falls back to the raw id: a null id is a caller bug
    /// and propagates (same contract as <c>ConvertTimeByTimeZoneIanaId</c>) instead of being swallowed.
    /// </summary>
    [Fact]
    public void ToTimeZoneStandardName_Throws_ForNullId()
    {
        string? id = null;

        Assert.Throws<ArgumentNullException>(() => id!.ToTimeZoneStandardName());
    }
}
