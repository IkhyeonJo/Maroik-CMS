using ErrorOr;

namespace Maroik.Core.Domain.Localization;

/// <summary>
/// Builds an <see cref="Error"/> whose <see cref="Error.Description"/> may embed a runtime value (a
/// bad time-zone ID, email address, hex color, class name, ...) while still carrying the
/// composite-format resource template and its arguments separately in <see cref="Error.Metadata"/>.
/// A helper in an outer layer (<c>ErrorMetadataExtensions.ToLocalizationKeyAndArgs</c>) reads that
/// metadata back out so the UI gets a static, resx-localizable template + args instead of an
/// already-baked English sentence it can never match against a resource key.
/// <see cref="Error.Description"/> itself is unaffected — it stays the fully-formatted message, for
/// logs and any caller that does not localize.
/// </summary>
/// <remarks>
/// This is the <em>only</em> place in <c>Maroik.Core.Domain</c> allowed to call the raw
/// <c>ErrorOr.Error.Validation</c>/<c>Conflict</c>/<c>Failure</c> factories — enforced by
/// <c>DomainArchitectureTests.Domain_ErrorMessages_MustGoThrough_LocalizableError</c>. Every other
/// type builds its errors via <see cref="Validation"/>/<see cref="Conflict"/>/<see cref="Failure"/>
/// below, even when the message has no runtime value to interpolate (pass zero <c>args</c>) — a
/// consistent path means a message that starts static and later gains an interpolated value can
/// never silently regress to bypassing localization.
/// </remarks>
public static class LocalizableError
{
    /// <summary><see cref="Error.Metadata"/> key holding the composite-format template (e.g. <c>"'{0}' is not valid."</c>).</summary>
    public const string ResourceKeyMetadataKey = "ResourceKey";

    /// <summary><see cref="Error.Metadata"/> key holding the <c>object[]</c> arguments for <see cref="ResourceKeyMetadataKey"/>.</summary>
    public const string ResourceArgsMetadataKey = "ResourceArgs";

    /// <summary>
    /// Creates a <see cref="ErrorType.Validation"/> error from a composite-format
    /// <paramref name="resourceTemplate"/> (e.g. <c>"'{0}' is not a recognized time-zone ID."</c>)
    /// and its <paramref name="args"/>. <see cref="Error.Description"/> is the template with
    /// <paramref name="args"/> already substituted in.
    /// </summary>
    public static Error Validation(string code, string resourceTemplate, params object[] args) =>
        Error.Validation(code, string.Format(resourceTemplate, args), Metadata(resourceTemplate, args));

    /// <summary>
    /// Creates a <see cref="ErrorType.Conflict"/> error from a composite-format
    /// <paramref name="resourceTemplate"/> and its <paramref name="args"/> (same pattern as
    /// <see cref="Validation"/>) — for a request that conflicts with current state (already
    /// deleted/confirmed/locked, ...) rather than invalid input.
    /// </summary>
    public static Error Conflict(string code, string resourceTemplate, params object[] args) =>
        Error.Conflict(code, string.Format(resourceTemplate, args), Metadata(resourceTemplate, args));

    /// <summary>
    /// Creates a <see cref="ErrorType.Failure"/> error from a composite-format
    /// <paramref name="resourceTemplate"/> and its <paramref name="args"/> (same pattern as
    /// <see cref="Validation"/>) — for a precondition the caller cannot fix by changing the input
    /// (e.g. an operation that requires state only an earlier step can set up).
    /// </summary>
    public static Error Failure(string code, string resourceTemplate, params object[] args) =>
        Error.Failure(code, string.Format(resourceTemplate, args), Metadata(resourceTemplate, args));

    /// <summary>Builds the <see cref="Error.Metadata"/> dictionary carrying the unformatted template and its arguments.</summary>
    private static Dictionary<string, object> Metadata(string resourceTemplate, object[] args) => new()
    {
        [ResourceKeyMetadataKey] = resourceTemplate,
        [ResourceArgsMetadataKey] = args,
    };
}
