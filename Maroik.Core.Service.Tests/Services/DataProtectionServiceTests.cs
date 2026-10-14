using System.Security.Cryptography;
using Maroik.Core.Service.Services;
using Microsoft.AspNetCore.DataProtection;

namespace Maroik.Core.Service.Tests.Services;

/// <summary>
/// Unit tests for <see cref="DataProtectionService"/>, run against a real (in-memory) Data Protection key ring
/// (<see cref="EphemeralDataProtectionProvider"/>) rather than a mock, so the round trip, the tamper detection and
/// the purpose isolation are the framework's actual behaviour.
/// </summary>
public class DataProtectionServiceTests
{
    /// <summary>The key ring shared by the tests that need two parties on the same keys.</summary>
    private readonly EphemeralDataProtectionProvider _provider = new();

    /// <summary>The service under test on <see cref="_provider"/>.</summary>
    private DataProtectionService CreateSut() => new(_provider);

    /// <summary>A protected value unprotects back to the original text.</summary>
    [Theory]
    [InlineData("short")]
    [InlineData("upload/Forum/FreeForum/summernote/images/0f8fad5bd9cb469fa16570867728950e.png")]
    [InlineData("unicode: 안녕하세요 🎉")]
    public void Protect_ThenUnprotect_RoundTrips(string plaintext)
    {
        var sut = CreateSut();

        string protectedValue = sut.Protect(plaintext);

        Assert.Equal(plaintext, sut.Unprotect(protectedValue));
    }

    /// <summary>
    /// The value is opaque and URL-safe: it does not carry the plaintext, and it is base64url only, so it survives
    /// a mailed link's query string and an <c>alt</c> attribute unchanged.
    /// </summary>
    [Fact]
    public void Protect_ReturnsAnOpaqueUrlSafeValue()
    {
        const string plaintext = "upload/Forum/FreeForum/summernote/images/secret-path.png";

        string protectedValue = CreateSut().Protect(plaintext);

        Assert.DoesNotContain("upload", protectedValue);
        Assert.DoesNotContain("secret-path", protectedValue);
        Assert.Matches("^[A-Za-z0-9_-]+$", protectedValue);
    }

    /// <summary>Protecting the same text twice gives two different values, so equal paths or tokens are not linkable.</summary>
    [Fact]
    public void Protect_ProducesADifferentValueEachCall()
    {
        var sut = CreateSut();

        Assert.NotEqual(sut.Protect("same-plaintext"), sut.Protect("same-plaintext"));
    }

    /// <summary>The keys live in the key ring, not in the instance: another instance on the same ring reads the value.</summary>
    [Fact]
    public void Unprotect_ReadsAValueAnotherInstanceOnTheSameKeyRingProtected()
    {
        string protectedValue = new DataProtectionService(_provider).Protect("token");

        Assert.Equal("token", new DataProtectionService(_provider).Unprotect(protectedValue));
    }

    /// <summary>A value with one character changed is rejected (authenticated encryption), not decrypted to garbage.</summary>
    [Fact]
    public void Unprotect_Throws_WhenTheValueWasTamperedWith()
    {
        var sut = CreateSut();
        string protectedValue = sut.Protect("token");
        int middle = protectedValue.Length / 2;
        char flipped = protectedValue[middle] == 'A' ? 'B' : 'A';
        string tampered = string.Concat(protectedValue.AsSpan(0, middle), flipped.ToString(), protectedValue.AsSpan(middle + 1));

        Assert.ThrowsAny<CryptographicException>(() => sut.Unprotect(tampered));
    }

    /// <summary>
    /// A malformed (non-base64url) value — e.g. a plaintext storage path posted straight to a write endpoint —
    /// surfaces as the same exception type as any other unreadable value, so callers guard one type.
    /// </summary>
    [Fact]
    public void Unprotect_Throws_CryptographicException_WhenTheValueIsNotBase64Url()
    {
        Assert.ThrowsAny<CryptographicException>(() => CreateSut().Unprotect("upload/Forum/not-a-token.png"));
    }

    /// <summary>
    /// The service uses its own purpose: a payload the same key ring protected for another purpose (the session
    /// and antiforgery cookies share this key ring) is not accepted as a token or an image path.
    /// </summary>
    [Fact]
    public void Unprotect_Throws_ForAValueProtectedUnderAnotherPurpose()
    {
        string foreign = _provider.CreateProtector("Microsoft.AspNetCore.Session").Protect("token");

        Assert.ThrowsAny<CryptographicException>(() => CreateSut().Unprotect(foreign));
    }

    /// <summary>Only the server's key ring can produce an accepted value: one from another key ring is rejected.</summary>
    [Fact]
    public void Unprotect_Throws_ForAValueFromAnotherKeyRing()
    {
        string foreign = new DataProtectionService(new EphemeralDataProtectionProvider()).Protect("token");

        Assert.ThrowsAny<CryptographicException>(() => CreateSut().Unprotect(foreign));
    }
}
