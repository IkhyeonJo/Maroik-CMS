using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;

namespace Maroik.Core.Service.Services;

/// <inheritdoc cref="ITimeZoneCatalogService" />
public class TimeZoneCatalogService : ITimeZoneCatalogService
{
    /// <summary>The option list, built once on first use (the host's zone table does not change at runtime).</summary>
    private readonly Lazy<IReadOnlyList<TimeZoneOptionDto>> _timeZoneOptions = new(BuildTimeZoneOptions);

    /// <inheritdoc />
    public IReadOnlyList<TimeZoneOptionDto> GetTimeZoneOptions() => _timeZoneOptions.Value;

    /// <summary>
    /// Lists every system time zone under its ID. Maroik runs on Linux only (docker-compose on Ubuntu), where every system zone ID is
    /// already an IANA ID, so no Windows-to-IANA conversion is needed.
    /// </summary>
    private static List<TimeZoneOptionDto> BuildTimeZoneOptions() =>
    [
        .. TimeZoneInfo.GetSystemTimeZones().Select(systemTimeZone => new TimeZoneOptionDto
        {
            IanaId = systemTimeZone.Id,
            DisplayName = systemTimeZone.DisplayName
        })
    ];
}
