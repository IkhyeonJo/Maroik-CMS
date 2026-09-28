using System.Security.Cryptography;
using Maroik.Core.Contract.Misc.Enums;
using Maroik.Core.Contract.Misc.Settings;
using Maroik.Core.Service.Services;
using Microsoft.Extensions.Options;

namespace Maroik.Core.Service.Tests.Services;

/// <summary>
/// Unit tests for <see cref="RsaService"/>.
///
/// Keys are generated in-test using <see cref="System.Security.Cryptography.RSA"/> .NET APIs:
/// <list type="bullet">
///   <item><see cref="RSA.ExportRSAPrivateKey"/> → PKCS#1 DER → Base64 (matches <see cref="RSA.ImportRSAPrivateKey"/>)</item>
///   <item><see cref="RSA.ExportSubjectPublicKeyInfo"/> → X.509 SubjectPublicKeyInfo DER → Base64 (matches <see cref="RSA.ImportSubjectPublicKeyInfo"/>)</item>
/// </list>
/// A 2048-bit key pair is generated once per test class (class-level static) to keep individual tests fast.
/// </summary>
public class RsaServiceTests
{
    // -- Shared 2048-bit key pair generated once per test class ---------------

    private static readonly string _privateKeyBase64;
    private static readonly string _publicKeyBase64;

    static RsaServiceTests()
    {
        using var rsa = RSA.Create(2048);
        _privateKeyBase64 = Convert.ToBase64String(rsa.ExportRSAPrivateKey());
        _publicKeyBase64 = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
    }

    // -- Helpers --------------------------------------------------------------

    private static RsaService CreateSut(RsaType algorithm = RsaType.Rsa2) =>
        new(Options.Create(new ServerSetting
        {
            RsaAlgorithm = algorithm,
            RsaPrivateKey = _privateKeyBase64,
            RsaPublicKey = _publicKeyBase64
        }));

    // -- Encrypt / Decrypt ----------------------------------------------------

    /// <summary>Verifies that <c>Encrypt</c> then decrypt when returns original plaintext.</summary>
    [Fact]
    public void Encrypt_ThenDecrypt_ReturnsOriginalPlaintext()
    {
        var sut = CreateSut();
        const string plaintext = "hello-maroik-rsa";

        string cipher = sut.Encrypt(plaintext);
        string recovered = sut.Decrypt(cipher);

        Assert.Equal(plaintext, recovered);
    }

    /// <summary>Verifies that <c>Encrypt</c> then decrypt with sha1 algorithm returns original plaintext.</summary>
    [Fact]
    public void Encrypt_ThenDecrypt_WithSha1Algorithm_ReturnsOriginalPlaintext()
    {
        var sut = CreateSut(RsaType.Rsa);
        const string plaintext = "sha1-roundtrip";

        string cipher = sut.Encrypt(plaintext);
        string recovered = sut.Decrypt(cipher);

        Assert.Equal(plaintext, recovered);
    }

    /// <summary>Verifies that <c>Encrypt</c> produces base64 output.</summary>
    [Fact]
    public void Encrypt_ProducesBase64Output()
    {
        var sut = CreateSut();

        string cipher = sut.Encrypt("test");

        // Must be valid Base64
        var decoded = Convert.FromBase64String(cipher);
        Assert.NotEmpty(decoded);
    }

    /// <summary>Verifies that <c>Encrypt</c> produces different ciphertext each call when due to oaep padding.</summary>
    [Fact]
    public void Encrypt_ProducesDifferentCiphertextEachCall_DueToOaepPadding()
    {
        var sut = CreateSut();
        const string plaintext = "same-plaintext";

        string c1 = sut.Encrypt(plaintext);
        string c2 = sut.Encrypt(plaintext);

        // OAEP includes a random seed → different ciphertext per call
        Assert.NotEqual(c1, c2);
    }

    /// <summary>Verifies that <c>Encrypt</c> then decrypt when round trips various strings.</summary>
    [Theory]
    [InlineData("short")]
    [InlineData("a longer test string with spaces and punctuation!")]
    [InlineData("unicode: 안녕하세요 🎉")]
    public void Encrypt_ThenDecrypt_RoundTrips_VariousStrings(string plaintext)
    {
        var sut = CreateSut();

        string cipher = sut.Encrypt(plaintext);
        string recovered = sut.Decrypt(cipher);

        Assert.Equal(plaintext, recovered);
    }

    /// <summary>
    /// Regression: the SHA-1 OAEP fallback has been retired (CLAUDE.md TODO — "Retire the RSA SHA-1
    /// fallback"), so ciphertext produced the way the old <see cref="RsaType.Rsa"/> config did no
    /// longer decrypts — <c>Decrypt</c> must now surface it as the same <see cref="CryptographicException"/>
    /// any other undecryptable ciphertext produces, not silently retry under SHA-1.
    /// </summary>
    [Fact]
    public void Decrypt_Throws_ForCiphertextProducedWithLegacySha1Oaep()
    {
        const string plaintext = "legacy-sha1-oaep-blob";

        using var rsa = RSA.Create(2048);
        string privateKey = Convert.ToBase64String(rsa.ExportRSAPrivateKey());
        string publicKey = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());

        // Encrypt exactly the way the old SHA-1 code path did.
        string legacyCipher = Convert.ToBase64String(
            rsa.Encrypt(System.Text.Encoding.UTF8.GetBytes(plaintext), RSAEncryptionPadding.CreateOaep(HashAlgorithmName.SHA1)));

        var sut = new RsaService(Options.Create(new ServerSetting
        {
            RsaAlgorithm = RsaType.Rsa2,
            RsaPrivateKey = privateKey,
            RsaPublicKey = publicKey
        }));

        Assert.ThrowsAny<CryptographicException>(() => sut.Decrypt(legacyCipher));
    }

    /// <summary>
    /// Regression: the SHA-1 fallback has been retired, so a signature made with the legacy SHA-1
    /// hash must no longer verify.
    /// </summary>
    [Fact]
    public void Verify_RejectsSignature_MadeWithLegacySha1()
    {
        const string data = "legacy-sha1-signed";

        using var rsa = RSA.Create(2048);
        string privateKey = Convert.ToBase64String(rsa.ExportRSAPrivateKey());
        string publicKey = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());

        string legacySignature = Convert.ToBase64String(
            rsa.SignData(System.Text.Encoding.UTF8.GetBytes(data), HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1));

        var sut = new RsaService(Options.Create(new ServerSetting
        {
            RsaAlgorithm = RsaType.Rsa2,
            RsaPrivateKey = privateKey,
            RsaPublicKey = publicKey
        }));

        Assert.False(sut.Verify(data, legacySignature));
    }

    /// <summary>Verifies that <c>Decrypt</c> throws when ciphertext is invalid.</summary>
    [Fact]
    public void Decrypt_Throws_WhenCiphertextIsInvalid()
    {
        var sut = CreateSut();
        string badCipher = Convert.ToBase64String(new byte[256]); // zeros ?? not a valid RSA ciphertext

        Assert.ThrowsAny<CryptographicException>(() => sut.Decrypt(badCipher));
    }

    /// <summary>Verifies that a non-Base64 input surfaces as CryptographicException, not FormatException,
    /// so callers only ever have to guard one exception type.</summary>
    [Fact]
    public void Decrypt_Throws_CryptographicException_WhenInputIsNotBase64()
    {
        var sut = CreateSut();

        Assert.ThrowsAny<CryptographicException>(() => sut.Decrypt("upload/Forum/not-a-token.png"));
    }

    // -- Sign / Verify --------------------------------------------------------

    /// <summary>Verifies that <c>Sign</c> then verify when returns true.</summary>
    [Fact]
    public void Sign_ThenVerify_ReturnsTrue()
    {
        var sut = CreateSut();
        const string data = "data-to-sign";

        string signature = sut.Sign(data);
        bool valid = sut.Verify(data, signature);

        Assert.True(valid);
    }

    /// <summary>Verifies that <c>Sign</c> then verify with sha1 algorithm returns true.</summary>
    [Fact]
    public void Sign_ThenVerify_WithSha1Algorithm_ReturnsTrue()
    {
        var sut = CreateSut(RsaType.Rsa);
        const string data = "sha1-sign-verify";

        string signature = sut.Sign(data);
        bool valid = sut.Verify(data, signature);

        Assert.True(valid);
    }

    /// <summary>Verifies that <c>Verify</c> returns false when data is tampered.</summary>
    [Fact]
    public void Verify_ReturnsFalse_WhenDataIsTampered()
    {
        var sut = CreateSut();
        string signature = sut.Sign("original");

        bool valid = sut.Verify("tampered", signature);

        Assert.False(valid);
    }

    /// <summary>Verifies that <c>Verify</c> returns false when signature is corrupted.</summary>
    [Fact]
    public void Verify_ReturnsFalse_WhenSignatureIsCorrupted()
    {
        var sut = CreateSut();
        string signature = sut.Sign("data");

        // Flip the last byte of the Base64-decoded signature
        byte[] sigBytes = Convert.FromBase64String(signature);
        sigBytes[^1] ^= 0xFF;
        string corruptedSignature = Convert.ToBase64String(sigBytes);

        bool valid = sut.Verify("data", corruptedSignature);

        Assert.False(valid);
    }

    /// <summary>Verifies that <c>Verify</c> returns false (does not throw) for a non-Base64 signature.</summary>
    [Fact]
    public void Verify_ReturnsFalse_WhenSignatureIsNotBase64()
    {
        var sut = CreateSut();

        bool valid = sut.Verify("data", "not-valid-base64-!!!");

        Assert.False(valid);
    }

    /// <summary>Verifies that <c>Sign</c> produces non-empty base64 string.</summary>
    [Fact]
    public void Sign_ProducesNonEmptyBase64String()
    {
        var sut = CreateSut();

        string signature = sut.Sign("some data");

        Assert.False(string.IsNullOrEmpty(signature));
        var bytes = Convert.FromBase64String(signature); // must be valid Base64
        Assert.NotEmpty(bytes);
    }

    // -- No-key edge cases ----------------------------------------------------

    /// <summary>Verifies that <c>Sign</c> returns empty when private key is not configured.</summary>
    [Fact]
    public void Sign_ReturnsEmpty_WhenPrivateKeyIsNotConfigured()
    {
        // RsaService with no private key → _rsa has null private key provider
        var sut = new RsaService(Options.Create(new ServerSetting
        {
            RsaAlgorithm = RsaType.Rsa2,
            RsaPrivateKey = "",
            RsaPublicKey = _publicKeyBase64
        }));

        string result = sut.Sign("data");

        Assert.Equal("", result);
    }

    /// <summary>Verifies that <c>Verify</c> returns false when public key is not configured.</summary>
    [Fact]
    public void Verify_ReturnsFalse_WhenPublicKeyIsNotConfigured()
    {
        var sut = new RsaService(Options.Create(new ServerSetting
        {
            RsaAlgorithm = RsaType.Rsa2,
            RsaPrivateKey = _privateKeyBase64,
            RsaPublicKey = null
        }));

        // No public key → Verify returns false (provider is null)
        bool result = sut.Verify("data", Convert.ToBase64String(new byte[256]));

        Assert.False(result);
    }

    /// <summary>Decrypting without a configured private key fails loudly instead of returning garbage.</summary>
    [Fact]
    public void Decrypt_Throws_WhenNoPrivateKeyIsConfigured()
    {
        var sut = new RsaService(Options.Create(new ServerSetting { RsaAlgorithm = RsaType.Rsa2, RsaPrivateKey = "", RsaPublicKey = _publicKeyBase64 }));

        var ex = Assert.Throws<InvalidOperationException>(() => sut.Decrypt("anything"));

        Assert.Contains("private key", ex.Message);
    }
}
