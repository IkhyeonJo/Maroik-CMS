using System.Text.Encodings.Web;
using System.Text.Json;

namespace Maroik.Website.Extensions;

/// <summary>
/// Central System.Text.Json settings for the web layer's ad-hoc serialization — the
/// <c>@Html.Hidden(...)</c> policy / view-model payloads embedded in views and the calendar-events
/// JSON string returned by <see cref="Maroik.Website.Controllers.CalendarController"/>.
/// <para>
/// Verbatim (PascalCase) property names, no indentation, and relaxed escaping so the emitted JSON
/// both matches what the client scripts <c>JSON.parse</c> and keeps non-ASCII text (e.g. Korean
/// event titles) readable. Every value produced here is rendered through Razor's HTML-attribute
/// encoding or an MVC <c>Json(...)</c> string field, so the relaxed encoder cannot introduce markup.
/// MVC own request/response serialization is configured separately and is unaffected.
/// </para>
/// </summary>
public static class JsonSerialization
{
    /// <summary>Shared options backing <see cref="ToClientJson"/>.</summary>
    private static readonly JsonSerializerOptions _clientPayload = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Serializes <paramref name="value"/> for embedding in a page (hidden field) or an AJAX payload
    /// that a client script will <c>JSON.parse</c>.
    /// </summary>
    public static string ToClientJson(object? value) => JsonSerializer.Serialize(value, _clientPayload);
}
