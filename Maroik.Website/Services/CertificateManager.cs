using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Maroik.Website.Contracts;

namespace Maroik.Website.Services;

/// <summary>
/// Loads a TLS certificate/key pair from disk and keeps it hot-swappable: <see cref="TryReload"/>
/// re-reads both files, and if either has changed (compared by SHA-256 hash) reloads the certificate
/// under a lock so concurrent <see cref="SelectCertificate"/> calls from Kestrel never observe a
/// half-swapped state. Intended to be polled on a timer by the caller.
/// </summary>
public sealed class CertificateManager(string certPath, string keyPath, ILogger logger, bool? isWindows = null) : ICertificateManager
{
    /// <summary>Guards every read and swap of <see cref="_currentCert"/> / <see cref="_retiredCert"/> and the stored hashes.</summary>
    private readonly Lock _certLock = new();

    // Where the certificate files are read from differs per OS (see LoadCertificate). Production leaves this to be
    // detected; the optional constructor argument lets tests exercise the Windows layout on any OS.
    private readonly bool _windows = DetectWindows(isWindows);

    /// <summary>The certificate (with private key) Kestrel is currently handed by <see cref="SelectCertificate"/>.</summary>
    private X509Certificate2 _currentCert = LoadCertificate(certPath, keyPath, logger, DetectWindows(isWindows));
    // The cert displaced by the most recent reload. It is not disposed at swap time because Kestrel's
    // ServerCertificateSelector may still be mid-handshake with the reference it just took from
    // SelectCertificate(); it is disposed one reload later, by which point no handshake can still
    // hold it. Disposing the just-displaced cert immediately would surface as ObjectDisposedException
    // on any TLS handshake in flight at the exact moment of a cert renewal.
    private X509Certificate2? _retiredCert;
    /// <summary>SHA-256 of the certificate file as last loaded; <see cref="TryReload"/> reloads when it changes.</summary>
    private string _previousCertHash = ComputeFileHash(certPath);
    /// <summary>SHA-256 of the private-key file as last loaded; <see cref="TryReload"/> reloads when it changes.</summary>
    private string _previousCertKeyHash = ComputeFileHash(keyPath);
    /// <summary>True while a <see cref="TryReload"/> is running, so an overlapping timer tick returns at once.</summary>
    private volatile bool _isReloading;

    /// <inheritdoc />
    public X509Certificate2 SelectCertificate()
    {
        lock (_certLock)
        {
            return _currentCert;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// A reload already in progress makes subsequent calls no-ops (guards against a slow reload overlapping
    /// with the next timer tick). Exceptions are logged and swallowed so a bad file never takes down the timer.
    /// </remarks>
    public void TryReload()
    {
        if (_isReloading) return;
        _isReloading = true;

        try
        {
            var currentCertHash = ComputeFileHash(certPath);
            var currentCertKeyHash = ComputeFileHash(keyPath);

            // Either file changing is sufficient - certs are sometimes renewed while reusing the same key.
            if (currentCertHash == _previousCertHash && currentCertKeyHash == _previousCertKeyHash)
            {
                return;
            }
            logger.LogInformation("Certificate file changed, reloading...");

            var newCert = LoadCertificate(certPath, keyPath, logger, _windows);

            lock (_certLock)
            {
                // Dispose the cert retired two reloads ago (safe now), then retire the current one.
                _retiredCert?.Dispose();
                _retiredCert = _currentCert;
                _currentCert = newCert;
                _previousCertHash = currentCertHash;
                _previousCertKeyHash = currentCertKeyHash;
            }

            logger.LogInformation("Certificate reloaded successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reload certificate");
        }
        finally
        {
            _isReloading = false;
        }
    }

    /// <summary>Hex SHA-256 of <paramref name="filePath"/>'s contents, read with sharing so a writer (the renewal job) is not blocked.</summary>
    private static string ComputeFileHash(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            bufferSize: 8192,
            FileOptions.SequentialScan);

        var hashBytes = sha256.ComputeHash(stream);
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>The caller's forced value when given (tests), otherwise whether the process runs on Windows.</summary>
    private static bool DetectWindows(bool? forced) => forced ?? RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <summary>
    /// Reads the PEM certificate and ECDSA private key and combines them into one certificate. On Linux /
    /// macOS the given paths are read directly. On Windows the Let's Encrypt <c>live/</c> files are
    /// read as plain text holding the target file name (a symlink copied without link support), and the
    /// real PEM files are then read from the matching <c>archive/</c> directory. Failures are logged and rethrown.
    /// </summary>
    private static X509Certificate2 LoadCertificate(string certPath, string keyPath, ILogger logger, bool windows)
    {
        try
        {
            var realCertPem = "";
            var realKeyPem = "";

            // Read certificate and key as string (pem format)
            if (windows)
            {
                var linkCertPem = File.ReadAllText(certPath);
                var linkKeyPem = File.ReadAllText(keyPath);

                var realCertName = Path.GetFileName(linkCertPem);
                var realKeyName = Path.GetFileName(linkKeyPem);

                var realCertPath = certPath.Replace("/live/", "/archive/").Replace("cert.pem", realCertName);
                var realKeyPath = keyPath.Replace("/live/", "/archive/").Replace("privkey.pem", realKeyName);

                realCertPem = File.ReadAllText(realCertPath);
                realKeyPem = File.ReadAllText(realKeyPath);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                     RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                realCertPem = File.ReadAllText(certPath);
                realKeyPem = File.ReadAllText(keyPath);
            }

            // Create the certificate using X509Certificate2.CreateFromPem
            var cert = X509Certificate2.CreateFromPem(realCertPem);

            // Load private key from PEM manually
            using var ecdsaPrivateKey = ECDsa.Create();
            ecdsaPrivateKey.ImportFromPem(realKeyPem);

            // Combine certificate plus private key
            var certWithKey = cert.CopyWithPrivateKey(ecdsaPrivateKey);

            return certWithKey;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load certificate from {CertPath} and {KeyPath}", certPath, keyPath);
            throw;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_certLock)
        {
            _currentCert.Dispose();
            _retiredCert?.Dispose();
        }
    }
}
