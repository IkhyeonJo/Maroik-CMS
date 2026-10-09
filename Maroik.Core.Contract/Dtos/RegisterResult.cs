using ErrorOr;

// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable MemberCanBePrivate.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Result returned by the registration operation.
/// Includes flags that tell the UI whether to show a "resend email" prompt.
/// </summary>
public class RegisterResult
{
    /// <summary>True when registration succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>
    /// Localization key describing the error (empty string on success). When
    /// <see cref="ErrorArgs"/> is non-empty, this is a composite-format template rather than an
    /// already-formatted sentence.
    /// </summary>
    public string ErrorKey { get; init; } = "";

    /// <summary>
    /// Composite-format arguments for <see cref="ErrorKey"/> (empty when the key needs no
    /// formatting).
    /// </summary>
    public object[] ErrorArgs { get; init; } = [];

    /// <summary>True when the UI should display the "resend confirmation email" button.</summary>
    public bool ShowResendEmail { get; init; }

    /// <summary>Email address that the confirmation was sent to (used in the UI message).</summary>
    public string? EmailAddress { get; init; }

    /// <summary>True when the confirmation email was re-sent (the resend flow) rather than sent for the first time.</summary>
    public bool RepeatEmailSend { get; init; }

    /// <summary>Creates a successful registration result.</summary>
    public static RegisterResult Ok(bool showResendEmail = false, string? email = null, bool repeat = false)
        => new() { Success = true, ShowResendEmail = showResendEmail, EmailAddress = email, RepeatEmailSend = repeat };

    /// <summary>
    /// Creates a failed registration result with the given error key. <paramref name="errorArgs"/>
    /// carries composite-format arguments when <paramref name="errorKey"/> is a template.
    /// </summary>
    public static RegisterResult Fail(string errorKey, bool showResendEmail = false, string? email = null, object[]? errorArgs = null)
        => new() { Success = false, ErrorKey = errorKey, ErrorArgs = errorArgs ?? [], ShowResendEmail = showResendEmail, EmailAddress = email };

    /// <summary>
    /// Maps a domain <see cref="Error"/> to a failed <see cref="RegisterResult"/>. When
    /// <paramref name="error"/> was built via <c>DomainError</c>, <see cref="ErrorKey"/> /
    /// <see cref="ErrorArgs"/> carry its resource template and arguments instead of the already-formatted
    /// <see cref="Error.Description"/>.
    /// </summary>
    public static RegisterResult FromError(Error error)
    {
        (string key, object[] args) = error.ToLocalizationKeyAndArgs();
        return new RegisterResult { Success = false, ErrorKey = key, ErrorArgs = args };
    }
}
