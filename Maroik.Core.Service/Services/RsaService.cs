using System.Security.Cryptography;
using System.Text;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Misc.Enums;
using Maroik.Core.Contract.Misc.Settings;
using Microsoft.Extensions.Options;

namespace Maroik.Core.Service.Services;

/// <summary>
/// Singleton implementation of <see cref="IRsaService"/> backed by <see cref="RSA"/> from
/// <c>System.Security.Cryptography</c>. Keys are OpenSSL-generated (PKCS#1 private key, X.509
/// SubjectPublicKeyInfo public key, both Base64-encoded DER) and read once at construction from
/// <see cref="ServerSetting"/> (appsettings.json).
/// <para>
/// The imported <see cref="RSA"/> objects are <b>not</b> held as shared fields: a single
/// <see cref="RSA"/> instance is not documented as safe for concurrent use, and every request that
/// confirms an email, resets a password, or renders board images calls in here in parallel. Instead,
/// the raw key bytes are cached and each operation imports them into a short-lived, per-call
/// <see cref="RSA"/> that it owns and disposes. Import cost is negligible next to the RSA operation
/// itself, and the service stays a safe singleton.
/// </para>
/// </summary>
public class RsaService : IRsaService
{
    /// <summary>DER bytes of the configured private key (PKCS#1), or null when none is configured.</summary>
    private readonly byte[]? _privateKeyDer;
    /// <summary>DER bytes of the configured public key (SubjectPublicKeyInfo), or null when none is configured.</summary>
    private readonly byte[]? _publicKeyDer;

    /// <summary>
    /// Hash used for OAEP padding on everything this service encrypts <b>and</b> decrypts. Always
    /// SHA-256: SHA-1 is collision-broken and needlessly weak for OAEP. The legacy SHA-1 fallback
    /// this service used to retry <see cref="Decrypt"/> under (for content produced by the old
    /// <see cref="RsaType.Rsa"/> config) has been retired; SHA-1-produced ciphertext no longer decrypts.
    /// </summary>
    private static readonly HashAlgorithmName _hashAlgorithmName = HashAlgorithmName.SHA256;

    /// <summary>Initializes a new instance of <see cref="RsaService"/> with the supplied dependencies.</summary>
    public RsaService(IOptions<ServerSetting> settings)
    {
        ServerSetting setting = settings.Value;

        _privateKeyDer = string.IsNullOrEmpty(setting.RsaPrivateKey)
            ? null
            : Convert.FromBase64String(setting.RsaPrivateKey);

        _publicKeyDer = string.IsNullOrEmpty(setting.RsaPublicKey)
            ? null
            : Convert.FromBase64String(setting.RsaPublicKey);
    }

    /// <summary>Imports the private key into a fresh, caller-owned <see cref="RSA"/>; null when no private key is configured.</summary>
    private RSA? CreatePrivateKeyRsa()
    {
        if (_privateKeyDer == null) return null;
        var rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(_privateKeyDer, out _);
        return rsa;
    }

    /// <summary>Imports the public key into a fresh, caller-owned <see cref="RSA"/>; null when no public key is configured.</summary>
    private RSA? CreatePublicKeyRsa()
    {
        if (_publicKeyDer == null) return null;
        var rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(_publicKeyDer, out _);
        return rsa;
    }

    /// <inheritdoc />
    public string Decrypt(string cipherText)
    {
        // OAEP, not PKCS#1 v1.5: the latter is vulnerable to Bleichenbacher padding-oracle attacks
        // when a decrypt failure is distinguishable (by response or timing) from a decrypt success,
        // which is the case here since this decrypts attacker-suppliable email-confirmation/
        // password-reset tokens.
        using RSA? rsa = CreatePrivateKeyRsa();
        switch (rsa)
        {
            case null:
                throw new InvalidOperationException("RSA private key is not configured.");
        }
        byte[] cipherBytes;
        try
        {
            cipherBytes = Convert.FromBase64String(cipherText);
        }
        catch (FormatException ex)
        {
            // Surface a malformed (non-Base64) input as the same exception type an undecryptable
            // ciphertext produces, so every caller only has to guard CryptographicException.
            throw new CryptographicException("Ciphertext is not valid Base64.", ex);
        }

        return Encoding.UTF8.GetString(rsa.Decrypt(cipherBytes, RSAEncryptionPadding.CreateOaep(_hashAlgorithmName)));

    }

    /// <inheritdoc />
    public string Encrypt(string text)
    {
        using RSA? rsa = CreatePublicKeyRsa();
        return rsa == null
            ? throw new InvalidOperationException("RSA public key is not configured.")
            : Convert.ToBase64String(rsa.Encrypt(Encoding.UTF8.GetBytes(text), RSAEncryptionPadding.CreateOaep(_hashAlgorithmName)));
    }
}
