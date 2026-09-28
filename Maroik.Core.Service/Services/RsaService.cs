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
    private readonly byte[]? _privateKeyDer;
    private readonly byte[]? _publicKeyDer;

    /// <summary>
    /// Hash used for OAEP padding and PKCS#1 signatures on everything this service produces
    /// <b>and</b> accepts. Always SHA-256: SHA-1 is collision-broken (fatal for signatures) and
    /// needlessly weak for OAEP. The legacy SHA-1 fallback this service used to retry
    /// <see cref="Decrypt"/>/<see cref="Verify"/> under (for content produced by the old
    /// <see cref="RsaType.Rsa"/> config) has been retired; SHA-1-produced ciphertext/signatures no
    /// longer decrypt/verify.
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

    private RSA? CreatePrivateKeyRsa()
    {
        if (_privateKeyDer == null) return null;
        var rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(_privateKeyDer, out _);
        return rsa;
    }

    private RSA? CreatePublicKeyRsa()
    {
        if (_publicKeyDer == null) return null;
        var rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(_publicKeyDer, out _);
        return rsa;
    }

    /// <inheritdoc cref="data" />
    public string Sign(string data)
    {
        using RSA? rsa = CreatePrivateKeyRsa();
        byte[]? signatureBytes = rsa?.SignData(Encoding.UTF8.GetBytes(data), _hashAlgorithmName, RSASignaturePadding.Pkcs1);
        return signatureBytes != null ? Convert.ToBase64String(signatureBytes) : "";
    }

    /// <inheritdoc cref="data" />
    public bool Verify(string data, string sign)
    {
        using RSA? rsa = CreatePublicKeyRsa();
        if (rsa == null)
            return false;

        byte[] signBytes;
        try
        {
            signBytes = Convert.FromBase64String(sign);
        }
        catch (FormatException)
        {
            // A malformed (non-Base64) signature is just an invalid signature, not an exception
            // the caller must guard against — same guarded-parse shape Decrypt / GuidToken use.
            return false;
        }

        byte[] dataBytes = Encoding.UTF8.GetBytes(data);

        return rsa.VerifyData(dataBytes, signBytes, _hashAlgorithmName, RSASignaturePadding.Pkcs1);
    }

    /// <inheritdoc />
    public string Decrypt(string cipherText)
    {
        // OAEP, not PKCS#1 v1.5: the latter is vulnerable to Bleichenbacher padding-oracle attacks
        // when a decrypt failure is distinguishable (by response or timing) from a decrypt success,
        // which is the case here since this decrypts attacker-suppliable email-confirmation/
        // password-reset tokens.
        using RSA? rsa = CreatePrivateKeyRsa();
        if (rsa == null)
            throw new InvalidOperationException("RSA private key is not configured.");

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
