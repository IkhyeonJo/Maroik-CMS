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
    /// <summary>Protects <paramref name="plaintext"/> and returns it as a URL-safe (base64url) string.</summary>
    string Protect(string plaintext);

    /// <summary>
    /// Recovers the plaintext of a value <see cref="Protect"/> produced. Throws a
    /// <see cref="System.Security.Cryptography.CryptographicException"/> for anything else — a malformed, tampered
    /// or foreign value.
    /// </summary>
    string Unprotect(string protectedValue);
}
