using System.Security.Cryptography.X509Certificates;

namespace Maroik.Website.Contracts;

/// <summary>
/// Holds the TLS certificate Kestrel serves and keeps it hot-swappable without a restart.
/// </summary>
public interface ICertificateManager : IDisposable
{
    /// <summary>Returns the currently active certificate. Safe to call concurrently with <see cref="TryReload"/>.</summary>
    X509Certificate2 SelectCertificate();

    /// <summary>Re-checks the certificate/key files on disk and reloads if either has changed.</summary>
    void TryReload();
}
