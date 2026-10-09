using ErrorOr;

namespace Maroik.Core.Domain.Errors;

/// <summary>
/// Builds every <see cref="Error"/> the domain returns. The domain speaks one language — English —
/// and knows nothing about cultures or translation: it only states <em>what</em> went wrong, as an
/// English composite-format message template plus the runtime values that fill it (a bad time-zone
/// ID, email address, hex color, class name, ...).
/// <see cref="Error.Description"/> is the template with those values already substituted in (for
/// logs and any caller that just wants a readable sentence), and the unformatted template and its
/// values are also kept separately in <see cref="Error.Metadata"/>.
/// Keeping them separate is what lets an outer layer translate the message: the template is a
/// stable lookup key, whereas a sentence with a value already baked into it can never match one.
/// Translation itself happens outside the domain (<c>ErrorMetadataExtensions.ToLocalizationKeyAndArgs</c>
/// in Contract, then the Website's resx).
/// </summary>
/// <remarks>
/// This is the <em>only</em> place in <c>Maroik.Core.Domain</c> allowed to call the raw
/// <c>ErrorOr.Error.Validation</c>/<c>Conflict</c>/<c>Failure</c> factories — enforced by
/// <c>DomainArchitectureTests.Domain_ErrorMessages_MustGoThrough_DomainError</c>. Every other
/// type builds its errors via <see cref="Validation"/>/<see cref="Conflict"/>/<see cref="Failure"/>
/// below, even when the message has no runtime value to interpolate (pass zero <c>args</c>) — a
/// consistent path means a message that starts static and later gains an interpolated value can
/// never silently lose its separate template.
/// </remarks>
public static class DomainError
{
    /// <summary><see cref="Error.Metadata"/> key holding the composite-format message template (e.g. <c>"'{0}' is not valid."</c>).</summary>
    public const string MessageTemplateMetadataKey = "MessageTemplate";

    /// <summary><see cref="Error.Metadata"/> key holding the <c>object[]</c> arguments for <see cref="MessageTemplateMetadataKey"/>.</summary>
    public const string MessageArgsMetadataKey = "MessageArgs";

    /// <summary>
    /// Creates a <see cref="ErrorType.Validation"/> error from a composite-format
    /// <paramref name="messageTemplate"/> (e.g. <c>"'{0}' is not a recognized time-zone ID."</c>)
    /// and its <paramref name="args"/>. <see cref="Error.Description"/> is the template with
    /// <paramref name="args"/> already substituted in.
    /// </summary>
    public static Error Validation(string code, string messageTemplate, params object[] args) =>
        Error.Validation(code, string.Format(messageTemplate, args), Metadata(messageTemplate, args));

    /// <summary>
    /// Creates a <see cref="ErrorType.Conflict"/> error from a composite-format
    /// <paramref name="messageTemplate"/> and its <paramref name="args"/> (same pattern as
    /// <see cref="Validation"/>) — for a request that conflicts with current state (already
    /// deleted/confirmed/locked, ...) rather than invalid input.
    /// </summary>
    public static Error Conflict(string code, string messageTemplate, params object[] args) =>
        Error.Conflict(code, string.Format(messageTemplate, args), Metadata(messageTemplate, args));

    /// <summary>
    /// Creates a <see cref="ErrorType.Failure"/> error from a composite-format
    /// <paramref name="messageTemplate"/> and its <paramref name="args"/> (same pattern as
    /// <see cref="Validation"/>) — for a precondition the caller cannot fix by changing the input
    /// (e.g. an operation that requires state only an earlier step can set up).
    /// </summary>
    public static Error Failure(string code, string messageTemplate, params object[] args) =>
        Error.Failure(code, string.Format(messageTemplate, args), Metadata(messageTemplate, args));

    /// <summary>Builds the <see cref="Error.Metadata"/> dictionary carrying the unformatted template and its arguments.</summary>
    private static Dictionary<string, object> Metadata(string messageTemplate, object[] args) => new()
    {
        [MessageTemplateMetadataKey] = messageTemplate,
        [MessageArgsMetadataKey] = args,
    };
}
