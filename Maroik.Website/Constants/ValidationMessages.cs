using Maroik.Core.Domain.Account;

namespace Maroik.Website.Constants;

/// <summary>
/// Validation messages shared by several view models. The text doubles as the resx lookup key, so it is kept in one place —
/// a change here and in the resx pair is then a single edit rather than one per view model.
/// </summary>
public static class ValidationMessages
{
    /// <summary>
    /// The password rule's message — the same text the services return
    /// (<see cref="PasswordPolicy.ViolationMessage"/>), so the client-side and server-side rejections
    /// read identically and share one resx entry per page.
    /// </summary>
    public const string PasswordComplexity = PasswordPolicy.ViolationMessage;
}
