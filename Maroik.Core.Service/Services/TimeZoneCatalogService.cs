using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;

namespace Maroik.Core.Service.Services;

/// <inheritdoc cref="ITimeZoneCatalogService" />
public class TimeZoneCatalogService : ITimeZoneCatalogService
{
    private readonly Lazy<IReadOnlyList<TimeZoneOptionDto>> _timeZoneOptions = new(BuildTimeZoneOptions);

    /// <inheritdoc />
    public IReadOnlyList<TimeZoneOptionDto> GetTimeZoneOptions() => _timeZoneOptions.Value;

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
