using Maroik.Core.Contract.Dtos;

namespace Maroik.Core.Contract.Interfaces;

/// <summary>Provides the list of selectable time zones for dropdowns, resolving legacy Windows IDs to IANA IDs.</summary>
public interface ITimeZoneCatalogService
{
    /// <summary>Returns every system time zone that has (or can be mapped to) an IANA identifier.</summary>
    IReadOnlyList<TimeZoneOptionDto> GetTimeZoneOptions();
}
