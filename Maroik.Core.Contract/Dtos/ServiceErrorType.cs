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
