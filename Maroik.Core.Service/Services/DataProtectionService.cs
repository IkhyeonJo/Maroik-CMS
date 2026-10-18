using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Enums;
using Microsoft.AspNetCore.DataProtection;

namespace Maroik.Core.Service.Services;

/// <summary>
/// Singleton implementation of <see cref="IDataProtectionService"/> over the host's ASP.NET Core Data Protection
/// key ring — the same one the session and antiforgery cookies use (persisted to Valkey, or to the file fallback;
/// see <c>Program.cs</c>). Both ends of every round trip are this server, and the key ring gives authenticated
/// symmetric encryption (AES-256-CBC + HMAC-SHA256 by default) with key generation, rotation and storage handled
/// by the framework — there is no key for the application to configure.
/// <para>
/// Each <see cref="ProtectedValue"/> kind has its own protector, under this service's purpose plus the kind as a
/// sub-purpose: a payload the same key ring protected for another purpose (a session or antiforgery cookie) is
/// rejected here, a value from this service is useless there, and a value handed out as one kind (a mailed token) is
/// never accepted as the other (an editor image path). An <see cref="IDataProtector"/> is thread-safe, so one instance
/// per kind serves every request.
/// </para>
/// </summary>
public class DataProtectionService(IDataProtectionProvider provider) : IDataProtectionService
{
    /// <summary>
    /// The purpose that isolates this service's payloads within the shared key ring. Changing it (or a kind's name)
    /// makes every value already handed out (a mailed link not yet clicked, an editor page still open) unreadable.
    /// </summary>
    private const string Purpose = "Maroik.Core.Service.DataProtectionService";

    /// <summary>The protector for <see cref="ProtectedValue.MailToken"/>.</summary>
    private readonly IDataProtector _mailTokens = provider.CreateProtector(Purpose, nameof(ProtectedValue.MailToken));

    /// <summary>The protector for <see cref="ProtectedValue.StoredFilePath"/>.</summary>
    private readonly IDataProtector _storedFilePaths = provider.CreateProtector(Purpose, nameof(ProtectedValue.StoredFilePath));

    /// <inheritdoc />
    public string Protect(ProtectedValue kind, string plaintext) => ProtectorFor(kind).Protect(plaintext);

    /// <inheritdoc />
    public string Unprotect(ProtectedValue kind, string protectedValue) => ProtectorFor(kind).Unprotect(protectedValue);

    /// <summary>The protector of <paramref name="kind"/>; a value outside <see cref="ProtectedValue"/> throws.</summary>
    private IDataProtector ProtectorFor(ProtectedValue kind) => kind switch
    {
        ProtectedValue.MailToken => _mailTokens,
        ProtectedValue.StoredFilePath => _storedFilePaths,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a ProtectedValue member."),
    };
}
