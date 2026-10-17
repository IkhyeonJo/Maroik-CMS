using Maroik.Core.Contract.Misc.Enums;

namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Protects values that leave the server and come back (the registration / password-reset tokens embedded in
/// mailed links, and the storage path of each inline Summernote image carried in its <c>alt</c> attribute), so
/// the client only ever holds an opaque value that it can neither read nor forge. Backed by ASP.NET Core Data
/// Protection with the host's key ring (Valkey, or the file fallback): authenticated encryption, no key for the
/// application to manage.
/// </summary>
public interface IDataProtectionService
{
    /// <summary>Protects <paramref name="plaintext"/> as a <paramref name="kind"/> and returns it as a URL-safe (base64url) string.</summary>
    string Protect(ProtectedValue kind, string plaintext);

    /// <summary>
    /// Recovers the plaintext of a value <see cref="Protect"/> produced for the same <paramref name="kind"/>. Throws a
    /// <see cref="System.Security.Cryptography.CryptographicException"/> for anything else — a malformed, tampered
    /// or foreign value, or one protected as the other kind.
    /// </summary>
    string Unprotect(ProtectedValue kind, string protectedValue);
}
