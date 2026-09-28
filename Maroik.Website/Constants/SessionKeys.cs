namespace Maroik.Website.Constants;

/// <summary>
/// Keys used to store data in the ASP.NET Core server-side session, read/written by
/// <see cref="Maroik.Website.Services.SessionService"/>.
/// </summary>
internal static class SessionKeys
{
    /// <summary>Session key holding the serialised <c>AccountResponse</c> for the logged-in user.</summary>
    internal const string Account = "Account";
}
