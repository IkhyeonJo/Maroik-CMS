using ErrorOr;

namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Broad category of a failed <see cref="ServiceResult"/>. Lets a caller (e.g. a controller)
/// translate an outcome into the right shape — an HTTP 400/404/409/500, a specific toast — instead
/// of treating every failure the same. Mirrors the subset of <see cref="ErrorType"/> the app uses.
/// </summary>
public enum ServiceErrorType
{
    /// <summary>The result is a success; no error.</summary>
    None = 0,

    /// <summary>The caller supplied invalid input or violated a business rule (maps to HTTP 400).</summary>
    Validation,

    /// <summary>A referenced entity does not exist or is not visible to the caller (maps to HTTP 404).</summary>
    NotFound,

    /// <summary>The request conflicts with current state — duplicate, already deleted, … (maps to HTTP 409).</summary>
    Conflict,

    /// <summary>An unexpected/infrastructure failure the caller cannot fix by changing input (maps to HTTP 500).</summary>
    Failure,
}

/// <summary>
/// Generic operation result used by service methods that do not need to return data.
/// Indicates success or failure and, on failure, carries a localization key / message, an optional
/// machine error code, and a <see cref="ServiceErrorType"/> so the caller can react to the
/// <em>kind</em> of failure rather than only "it failed".
/// Use the static factory methods to create instances.
/// </summary>
public class ServiceResult
{
    /// <summary>True when the operation completed successfully.</summary>
    public bool Success { get; private init; }

    /// <summary>
    /// Localization key / message that describes the error (empty string on success). When
    /// <see cref="ErrorArgs"/> is non-empty, this is a composite-format template (e.g.
    /// <c>"'{0}' is not a recognized time-zone ID."</c>) rather than an already-formatted sentence.
    /// </summary>
    public string ErrorKey { get; private init; } = "";

    /// <summary>
    /// Composite-format arguments for <see cref="ErrorKey"/> (empty when the key needs no
    /// formatting). Pass both straight to an <c>IHtmlLocalizer</c> indexer:
    /// <c>_localizer[result.ErrorKey, result.ErrorArgs]</c>.
    /// </summary>
    public object[] ErrorArgs { get; private init; } = [];

    /// <summary>Machine-readable error code (e.g. <c>"Income.AssetDeleted"</c>); empty when not set.</summary>
    public string ErrorCode { get; private init; } = "";

    /// <summary>Category of the failure; <see cref="ServiceErrorType.None"/> on success.</summary>
    public ServiceErrorType ErrorType { get; private init; } = ServiceErrorType.None;

    /// <summary>Creates a successful result.</summary>
    public static ServiceResult Ok() => new() { Success = true };

    /// <summary>
    /// Creates a plain <see cref="ServiceErrorType.Validation"/> failure carrying only a
    /// localization key / message. Use this only when the failure genuinely is "the input is
    /// invalid" and there is no machine code to report. When the category is known, or a domain
    /// <see cref="Error"/> is in hand, prefer <see cref="Validation"/> / <see cref="NotFound"/> /
    /// <see cref="Conflict"/> / <see cref="Failure"/> / <see cref="FromError"/>, all of which also
    /// populate <see cref="ErrorCode"/>.
    /// </summary>
    public static ServiceResult Fail(string errorKey) => Fail(errorKey, ServiceErrorType.Validation);

    /// <summary>Creates a failed result with the given error key and category.</summary>
    private static ServiceResult Fail(string errorKey, ServiceErrorType type) =>
        new() { Success = false, ErrorKey = errorKey, ErrorType = type };

    /// <summary>
    /// Creates a <see cref="ServiceErrorType.Validation"/> failure with a machine code and message.
    /// <paramref name="message"/> may be a composite-format template (e.g. <c>"'{0}' is not valid."</c>)
    /// when <paramref name="args"/> is supplied.
    /// </summary>
    public static ServiceResult Validation(string code, string message, params object[] args) =>
        new() { Success = false, ErrorCode = code, ErrorKey = message, ErrorArgs = args, ErrorType = ServiceErrorType.Validation };

    /// <summary>Creates a <see cref="ServiceErrorType.NotFound"/> failure.</summary>
    public static ServiceResult NotFound(string code, string message, params object[] args) =>
        new() { Success = false, ErrorCode = code, ErrorKey = message, ErrorArgs = args, ErrorType = ServiceErrorType.NotFound };

    /// <summary>Creates a <see cref="ServiceErrorType.Conflict"/> failure.</summary>
    public static ServiceResult Conflict(string code, string message, params object[] args) =>
        new() { Success = false, ErrorCode = code, ErrorKey = message, ErrorArgs = args, ErrorType = ServiceErrorType.Conflict };

    /// <summary>Creates a <see cref="ServiceErrorType.Failure"/> (unexpected/infrastructure) failure.</summary>
    public static ServiceResult Failure(string code, string message, params object[] args) =>
        new() { Success = false, ErrorCode = code, ErrorKey = message, ErrorArgs = args, ErrorType = ServiceErrorType.Failure };

    /// <summary>
    /// Maps a domain <see cref="Error"/> to a <see cref="ServiceResult"/>, preserving its category.
    /// When <paramref name="error"/> was built via <c>LocalizableError</c>, <see cref="ErrorKey"/> /
    /// <see cref="ErrorArgs"/> carry its resource template and arguments instead of the already-formatted
    /// <see cref="Error.Description"/>.
    /// </summary>
    public static ServiceResult FromError(Error error)
    {
        (string key, object[] args) = error.ToLocalizationKeyAndArgs();
        return new ServiceResult
        {
            Success = false,
            ErrorCode = error.Code,
            ErrorKey = key,
            ErrorArgs = args,
            ErrorType = error.Type switch
            {
                ErrorOr.ErrorType.Validation => ServiceErrorType.Validation,
                ErrorOr.ErrorType.NotFound => ServiceErrorType.NotFound,
                ErrorOr.ErrorType.Conflict => ServiceErrorType.Conflict,
                _ => ServiceErrorType.Failure,
            },
        };
    }
}
