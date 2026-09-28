// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>One selectable time zone entry for a time zone dropdown.</summary>
public class TimeZoneOptionDto
{
    /// <summary>IANA time zone identifier (e.g. "Asia/Seoul"), suitable for <c>DateTimeExtensions.ConvertTimeByTimeZoneIanaId</c>.</summary>
    public string IanaId { get; init; } = "";

    /// <summary>Human-readable label shown in the dropdown (e.g. "(UTC+09:00) Seoul").</summary>
    public string DisplayName { get; init; } = "";
}
