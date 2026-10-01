using Maroik.Core.Domain.Account;
namespace Maroik.Core.Domain.Tests.Account;

/// <summary>
/// Unit tests for <see cref="GuidToken"/>.
/// Verifies token generation uniqueness, Base64 validity, the 24-hour expiry window at its exact
/// edges (the current time is passed in, so the boundaries are deterministic), and rejection of
/// future-dated, expired or malformed tokens.
/// </summary>
public class GuidTokenTests
{
    /// <summary>The fixed "current time" the tests check tokens against.</summary>
    private static readonly DateTime Now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Verifies that <c>Generate</c> returns non-empty base64 string.</summary>
    [Fact]
    public void Generate_ReturnsNonEmptyBase64String()
    {
        string token = GuidToken.Generate(Now);

        Assert.NotNull(token);
        Assert.NotEmpty(token);
        // Must be valid Base64
        byte[] bytes = Convert.FromBase64String(token);
        Assert.NotEmpty(bytes);
    }

    /// <summary>Verifies that <c>Generate</c> produces unique tokens on each call, even at the same instant.</summary>
    [Fact]
    public void Generate_ProducesUniqueTokensOnEachCall()
    {
        string token1 = GuidToken.Generate(Now);
        string token2 = GuidToken.Generate(Now);

        Assert.NotEqual(token1, token2);
    }

    /// <summary>Verifies that <c>Generate</c> embeds the given time, not the machine clock.</summary>
    [Fact]
    public void Generate_EmbedsTheGivenTime()
    {
        string token = GuidToken.Generate(Now);

        Assert.Equal(Now, DateTime.FromBinary(BitConverter.ToInt64(Convert.FromBase64String(token), 0)));
    }

    /// <summary>A fresh token is alive.</summary>
    [Fact]
    public void IsTokenAlive_ReturnsTrue_ForFreshToken()
        => Assert.True(GuidToken.IsTokenAlive(GuidToken.Generate(Now), Now));

    /// <summary>The 24-hour window at its edges: just before, exactly at, and just after 24 hours.</summary>
    [Theory]
    [InlineData(-1, true)]  // one tick short of 24 h
    [InlineData(0, true)]   // exactly 24 h: still alive (the window is inclusive)
    [InlineData(1, false)]  // one tick past 24 h
    public void IsTokenAlive_AtTheTwentyFourHourEdge(long ticksPastTheWindow, bool expected)
    {
        string token = GuidToken.Generate(Now);
        DateTime checkedAt = Now.AddHours(24).AddTicks(ticksPastTheWindow);

        Assert.Equal(expected, GuidToken.IsTokenAlive(token, checkedAt));
    }

    /// <summary>A token dated up to 5 minutes in the future (clock skew) is accepted; beyond that it is refused.</summary>
    [Theory]
    [InlineData(5 * 60, true)]
    [InlineData(5 * 60 + 1, false)]
    public void IsTokenAlive_AllowsFiveMinutesOfClockSkew(int secondsInTheFuture, bool expected)
    {
        string token = GuidToken.Generate(Now.AddSeconds(secondsInTheFuture));

        Assert.Equal(expected, GuidToken.IsTokenAlive(token, Now));
    }

    /// <summary>Verifies that <c>IsTokenAlive</c> returns false, not an exception, for non-Base64 input.</summary>
    [Fact]
    public void IsTokenAlive_ReturnsFalse_ForNonBase64Token()
    {
        Assert.False(GuidToken.IsTokenAlive("not-valid-base64!!", Now));
    }

    /// <summary>Verifies that <c>IsTokenAlive</c> returns false, not an exception, for a too-short token.</summary>
    [Fact]
    public void IsTokenAlive_ReturnsFalse_ForTooShortToken()
    {
        string tooShort = Convert.ToBase64String([1, 2, 3]);

        Assert.False(GuidToken.IsTokenAlive(tooShort, Now));
    }
}
