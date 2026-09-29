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
    /// Lists every system time zone under its IANA ID â€” the zone's own ID when it already is one, else
    /// its Windows ID converted to IANA â€” skipping zones with no IANA equivalent.
    /// </summary>
    private static List<TimeZoneOptionDto> BuildTimeZoneOptions()
    {
        var options = new List<TimeZoneOptionDto>();

        foreach (var systemTimeZone in TimeZoneInfo.GetSystemTimeZones())
        {
            string systemTimeZoneIanaId = "";
            if (systemTimeZone.HasIanaId)
            {
                systemTimeZoneIanaId = systemTimeZone.Id;
            }
            else if (TimeZoneInfo.TryConvertWindowsIdToIanaId(systemTimeZone.Id, out string? ianaId))
            {
                systemTimeZoneIanaId = ianaId;
            }

            if (string.IsNullOrEmpty(systemTimeZoneIanaId))
            {
                continue;
            }

            options.Add(new TimeZoneOptionDto
            {
                IanaId = systemTimeZoneIanaId,
                DisplayName = systemTimeZone.DisplayName
            });
        }

        return options;
    }
}
