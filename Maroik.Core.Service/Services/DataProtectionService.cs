using Maroik.Core.Contract.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace Maroik.Core.Service.Services;

/// <summary>
/// Singleton implementation of <see cref="IDataProtectionService"/> over the host's ASP.NET Core Data Protection
/// key ring — the same one the session and antiforgery cookies use (persisted to Valkey, or to the file fallback;
/// see <c>Program.cs</c>). It replaced the earlier RSA service: both ends of every round trip are this server, so
/// a public key bought nothing, while the key ring gives authenticated symmetric encryption (AES-256-CBC +
/// HMAC-SHA256 by default) with key generation, rotation and storage handled by the framework — there is no key
/// for the application to configure.
/// <para>
/// The protector is created under its own purpose string, so a payload the same key ring protected for another
/// purpose (a session or antiforgery cookie) is rejected here, and a value from this service is useless there.
/// An <see cref="IDataProtector"/> is thread-safe, so one instance serves every request.
/// </para>
/// </summary>
public class DataProtectionService(IDataProtectionProvider provider) : IDataProtectionService
{
    /// <summary>
    /// The purpose that isolates this service's payloads within the shared key ring. Changing it makes every value
    /// already handed out (a mailed link not yet clicked, an editor page still open) unreadable.
    /// </summary>
    private const string Purpose = "Maroik.Core.Service.DataProtectionService";

    /// <summary>The protector for <see cref="Purpose"/>.</summary>
    private readonly IDataProtector _protector = provider.CreateProtector(Purpose);

    /// <inheritdoc />
    public string Protect(string plaintext) => _protector.Protect(plaintext);

    /// <inheritdoc />
    public string Unprotect(string protectedValue) => _protector.Unprotect(protectedValue);
}
