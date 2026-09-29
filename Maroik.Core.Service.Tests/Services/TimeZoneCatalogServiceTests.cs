using Maroik.Core.Service.Services;

namespace Maroik.Core.Service.Tests.Services;

/// <summary>
/// Unit tests for <see cref="TimeZoneCatalogService"/> — the server-side replacement
/// for the Windows-to-IANA time zone enumeration previously duplicated across ~11 Razor views.
/// </summary>
public class TimeZoneCatalogServiceTests
{
    /// <summary>The service under test.</summary>
    private static TimeZoneCatalogService CreateSut() => new();

    /// <summary>Get time zone options returns non empty list.</summary>
    [Fact]
    public void GetTimeZoneOptions_ReturnsNonEmptyList()
    {
        var options = CreateSut().GetTimeZoneOptions();

        Assert.NotEmpty(options);
    }

    /// <summary>Get time zone options no entry has empty iana id.</summary>
    [Fact]
    public void GetTimeZoneOptions_NoEntryHasEmptyIanaId()
    {
        var options = CreateSut().GetTimeZoneOptions();

        Assert.All(options, option => Assert.False(string.IsNullOrEmpty(option.IanaId)));
    }

    /// <summary>Get time zone options no entry has empty display name.</summary>
    [Fact]
    public void GetTimeZoneOptions_NoEntryHasEmptyDisplayName()
    {
        var options = CreateSut().GetTimeZoneOptions();

        Assert.All(options, option => Assert.False(string.IsNullOrEmpty(option.DisplayName)));
    }

    /// <summary>Get time zone options contains known iana zone.</summary>
    [Fact]
    public void GetTimeZoneOptions_ContainsKnownIanaZone()
    {
        var options = CreateSut().GetTimeZoneOptions();

        Assert.Contains(options, option => option.IanaId == "Asia/Seoul");
    }

    /// <summary>Get time zone options contains utc.</summary>
    [Fact]
    public void GetTimeZoneOptions_ContainsUtc()
    {
        var options = CreateSut().GetTimeZoneOptions();

        Assert.Contains(options, option => option.IanaId == "UTC");
    }

    /// <summary>Get time zone options returns same cached instance on repeated calls.</summary>
    [Fact]
    public void GetTimeZoneOptions_ReturnsSameCachedInstance_OnRepeatedCalls()
    {
        var sut = CreateSut();

        var first = sut.GetTimeZoneOptions();
        var second = sut.GetTimeZoneOptions();

        Assert.Same(first, second);
    }
}
