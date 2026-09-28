using Maroik.Core.Domain.Account;
namespace Maroik.Core.Domain.Tests.Account;

/// <summary>
/// Unit tests for <see cref="GuidToken"/>.
/// Verifies token generation uniqueness, Base64 validity, 24-hour expiry window,
/// and rejection of expired or tampered tokens.
/// </summary>
public class GuidTokenTests
{
    /// <summary>Verifies that <c>Generate</c> returns non-empty base64 string.</summary>
    [Fact]
    public void Generate_ReturnsNonEmptyBase64String()
    {
        string token = GuidToken.Generate();

        Assert.NotNull(token);
        Assert.NotEmpty(token);
        // Must be valid Base64
        byte[] bytes = Convert.FromBase64String(token);
        Assert.NotEmpty(bytes);
    }

    /// <summary>Verifies that <c>Generate</c> produces unique tokens on each call.</summary>
    [Fact]
    public void Generate_ProducesUniqueTokensOnEachCall()
    {
        string token1 = GuidToken.Generate();
        string token2 = GuidToken.Generate();

        Assert.NotEqual(token1, token2);
    }

    /// <summary>Verifies that <c>IsTokenAlive</c> returns true for fresh token.</summary>
    [Fact]
    public void IsTokenAlive_ReturnsTrue_ForFreshToken()
    {
        string token = GuidToken.Generate();

        Assert.True(GuidToken.IsTokenAlive(token));
    }

    /// <summary>Verifies that <c>IsTokenAlive</c> returns true for token created23 hours ago.</summary>
    [Fact]
    public void IsTokenAlive_ReturnsTrue_ForTokenCreated23HoursAgo()
    {
        string token = BuildTokenWithAge(hours: -23);

        Assert.True(GuidToken.IsTokenAlive(token));
    }

    /// <summary>Verifies that <c>IsTokenAlive</c> returns false for token created25 hours ago.</summary>
    [Fact]
    public void IsTokenAlive_ReturnsFalse_ForTokenCreated25HoursAgo()
    {
        string token = BuildTokenWithAge(hours: -25);

        Assert.False(GuidToken.IsTokenAlive(token));
    }

    /// <summary>Verifies that <c>IsTokenAlive</c> returns false for token created exactly24 hours ago.</summary>
    [Fact]
    public void IsTokenAlive_ReturnsFalse_ForTokenCreatedExactly24HoursAgo()
    {
        // Exactly on the boundary (24 h ago) — the check is strict less-than, so expired.
        string token = BuildTokenWithAge(hours: -24);

        Assert.False(GuidToken.IsTokenAlive(token));
    }

    /// <summary>Verifies that <c>IsTokenAlive</c> returns false, not an exception, for non-Base64 input.</summary>
    [Fact]
    public void IsTokenAlive_ReturnsFalse_ForNonBase64Token()
    {
        Assert.False(GuidToken.IsTokenAlive("not-valid-base64!!"));
    }

    /// <summary>Verifies that <c>IsTokenAlive</c> returns false, not an exception, for a too-short token.</summary>
    [Fact]
    public void IsTokenAlive_ReturnsFalse_ForTooShortToken()
    {
        string tooShort = Convert.ToBase64String([1, 2, 3]);

        Assert.False(GuidToken.IsTokenAlive(tooShort));
    }

    /// <summary>
    /// Builds a token using the same encoding as <see cref="GuidToken.Generate"/>
    /// but with a timestamp offset by the given number of hours from UtcNow.
    /// </summary>
    private static string BuildTokenWithAge(int hours)
    {
        byte[] time = BitConverter.GetBytes(DateTime.UtcNow.AddHours(hours).ToBinary());
        byte[] key = Guid.NewGuid().ToByteArray();
        return Convert.ToBase64String(time.Concat(key).ToArray());
    }
}
