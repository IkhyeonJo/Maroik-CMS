using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Maroik.Website.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Maroik.Website.Tests.Services;

/// <summary>
/// Unit tests for <see cref="CertificateManager"/>. Certificates and keys are real, self-signed
/// PEM files written to a temp directory - <see cref="CertificateManager"/> only ever touches the
/// filesystem and the .NET X509/ECDsa APIs, so no mocking is needed to exercise it end-to-end.
/// </summary>
public class CertificateManagerTests : IDisposable
{
    /// <summary>A fresh temporary folder holding this test's certificate files.</summary>
    private readonly string _tempDir = Directory.CreateTempSubdirectory(nameof(CertificateManagerTests)).FullName;
    /// <summary>Path of the certificate PEM in <see cref="_tempDir"/>.</summary>
    private string CertPath => Path.Combine(_tempDir, "cert.pem");
    /// <summary>Path of the private-key PEM in <see cref="_tempDir"/>.</summary>
    private string KeyPath => Path.Combine(_tempDir, "privkey.pem");

    /// <summary>A self-signed certificate for <paramref name="subject"/> signed by <paramref name="key"/>, as PEM plus its thumbprint.</summary>
    private static (string CertPem, string Thumbprint) GenerateCert(ECDsa key, string subject)
    {
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256);
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
        return (cert.ExportCertificatePem(), cert.Thumbprint);
    }

    /// <summary>Writes the certificate and key PEMs to <see cref="CertPath"/> and <see cref="KeyPath"/>.</summary>
    private void WriteCertFiles(string certPem, string keyPem)
    {
        File.WriteAllText(CertPath, certPem);
        File.WriteAllText(KeyPath, keyPem);
    }

    /// <summary>Verifies the constructor loads the certificate that is on disk at startup.</summary>
    [Fact]
    public void Constructor_LoadsCertificateFromDisk()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var (certPem, thumbprint) = GenerateCert(key, "CN=initial");
        WriteCertFiles(certPem, key.ExportECPrivateKeyPem());

        using var sut = new CertificateManager(CertPath, KeyPath, NullLogger.Instance);

        Assert.Equal(thumbprint, sut.SelectCertificate().Thumbprint);
    }

    // -- the Windows layout --------------------------------------------------------------------------
    // Let's Encrypt's "live" files are symlinks to versioned files under "archive". On Windows (git-for-Windows checkouts)
    // such a link is a small text file holding the link target, so the real file has to be located from that text.

    /// <summary>Creates certbot-style <c>live/</c> and <c>archive/</c> folders for <paramref name="site"/> and returns the file paths within them.</summary>
    private (string LiveCert, string LiveKey, string ArchiveCert, string ArchiveKey) WindowsLayout(string site)
    {
        string live = Directory.CreateDirectory(Path.Combine(_tempDir, "live", site)).FullName;
        string archive = Directory.CreateDirectory(Path.Combine(_tempDir, "archive", site)).FullName;
        return (Path.Combine(live, "cert.pem"), Path.Combine(live, "privkey.pem"), Path.Combine(archive, "cert3.pem"), Path.Combine(archive, "privkey3.pem"));
    }

    /// <summary>In the Windows layout the link files are followed to the versioned files in "archive", and that certificate is served.</summary>
    [Fact]
    public void Constructor_OnTheWindowsLayout_FollowsTheLinkFilesToTheArchive()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var (certPem, thumbprint) = GenerateCert(key, "CN=windows");
        var (liveCert, liveKey, archiveCert, archiveKey) = WindowsLayout("site");
        File.WriteAllText(archiveCert, certPem);
        File.WriteAllText(archiveKey, key.ExportECPrivateKeyPem());
        File.WriteAllText(liveCert, "../../archive/site/cert3.pem");
        File.WriteAllText(liveKey, "../../archive/site/privkey3.pem");

        using var sut = new CertificateManager(liveCert.Replace(Path.DirectorySeparatorChar, '/'), liveKey.Replace(Path.DirectorySeparatorChar, '/'), NullLogger.Instance, isWindows: true);

        Assert.Equal(thumbprint, sut.SelectCertificate().Thumbprint);
    }

    /// <summary>On the Windows layout a renewal (the link now names a newer archive file) is picked up by the next reload.</summary>
    [Fact]
    public void TryReload_OnTheWindowsLayout_PicksUpARenewedCertificate()
    {
        using var oldKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var newKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var (oldPem, _) = GenerateCert(oldKey, "CN=old");
        var (newPem, newThumbprint) = GenerateCert(newKey, "CN=new");
        var (liveCert, liveKey, archiveCert, archiveKey) = WindowsLayout("renew");
        File.WriteAllText(archiveCert, oldPem);
        File.WriteAllText(archiveKey, oldKey.ExportECPrivateKeyPem());
        File.WriteAllText(liveCert, "../../archive/renew/cert3.pem");
        File.WriteAllText(liveKey, "../../archive/renew/privkey3.pem");
        using var sut = new CertificateManager(liveCert.Replace(Path.DirectorySeparatorChar, '/'), liveKey.Replace(Path.DirectorySeparatorChar, '/'), NullLogger.Instance, isWindows: true);

        string renewedCert = Path.Combine(Path.GetDirectoryName(archiveCert)!, "cert4.pem");
        string renewedKey = Path.Combine(Path.GetDirectoryName(archiveKey)!, "privkey4.pem");
        File.WriteAllText(renewedCert, newPem);
        File.WriteAllText(renewedKey, newKey.ExportECPrivateKeyPem());
        File.WriteAllText(liveCert, "../../archive/renew/cert4.pem");
        File.WriteAllText(liveKey, "../../archive/renew/privkey4.pem");
        sut.TryReload();

        Assert.Equal(newThumbprint, sut.SelectCertificate().Thumbprint);
    }

    /// <summary>A link file pointing at an archive file that is not there fails loudly at startup instead of serving nothing.</summary>
    [Fact]
    public void Constructor_OnTheWindowsLayout_ThrowsWhenTheArchiveFileIsMissing()
    {
        var (liveCert, liveKey, _, _) = WindowsLayout("missing");
        File.WriteAllText(liveCert, "../../archive/missing/cert9.pem");
        File.WriteAllText(liveKey, "../../archive/missing/privkey9.pem");

        Assert.ThrowsAny<IOException>(() => new CertificateManager(liveCert.Replace(Path.DirectorySeparatorChar, '/'), liveKey.Replace(Path.DirectorySeparatorChar, '/'), NullLogger.Instance, isWindows: true));
    }

    /// <summary>Verifies that polling with no file changes never swaps the in-memory certificate.</summary>
    [Fact]
    public void TryReload_NoFileChange_KeepsSameCertificateInstance()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var (certPem, _) = GenerateCert(key, "CN=initial");
        WriteCertFiles(certPem, key.ExportECPrivateKeyPem());
        using var sut = new CertificateManager(CertPath, KeyPath, NullLogger.Instance);
        var before = sut.SelectCertificate();

        sut.TryReload();

        Assert.Same(before, sut.SelectCertificate());
    }

    /// <summary>
    /// Regression test: certificate renewal commonly reuses the same private key, so only the cert
    /// file's hash changes while the key file's hash stays identical. Reload must trigger on either
    /// file changing, not only when both change.
    /// </summary>
    [Fact]
    public void TryReload_CertFileChangesButKeyFileDoesNot_StillReloads()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var (initialCertPem, initialThumbprint) = GenerateCert(key, "CN=initial");
        WriteCertFiles(initialCertPem, key.ExportECPrivateKeyPem());
        using var sut = new CertificateManager(CertPath, KeyPath, NullLogger.Instance);

        var (renewedCertPem, renewedThumbprint) = GenerateCert(key, "CN=renewed");
        File.WriteAllText(CertPath, renewedCertPem); // key file is left untouched

        sut.TryReload();

        var current = sut.SelectCertificate();
        Assert.NotEqual(initialThumbprint, current.Thumbprint);
        Assert.Equal(renewedThumbprint, current.Thumbprint);
    }

    /// <summary>Verifies a corrupt certificate file is logged and swallowed, keeping the previous certificate active.</summary>
    [Fact]
    public void TryReload_WhenNewCertificateFailsToLoad_KeepsPreviousCertificateAndDoesNotThrow()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var (certPem, thumbprint) = GenerateCert(key, "CN=initial");
        WriteCertFiles(certPem, key.ExportECPrivateKeyPem());
        using var sut = new CertificateManager(CertPath, KeyPath, NullLogger.Instance);

        File.WriteAllText(CertPath, "not a valid certificate");

        var ex = Record.Exception(sut.TryReload);

        Assert.Null(ex);
        Assert.Equal(thumbprint, sut.SelectCertificate().Thumbprint);
    }

    /// <summary>Dispose.</summary>
    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
        GC.SuppressFinalize(this);
    }
}
