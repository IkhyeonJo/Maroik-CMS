using Maroik.Core.Domain.Account;
namespace Maroik.Core.Domain.Tests.Account;

/// <summary>
/// Unit tests for <see cref="PasswordPolicy"/>.
/// Rule: at least 8 characters, meeting 3 of 4 criteria — uppercase, lowercase, digit, special character.
/// </summary>
public class PasswordPolicyTests
{
    /// <summary>Verifies that a password of 8+ characters meeting any three (or all four) of the criteria is valid.</summary>
    [Theory]
    [InlineData("Abcdefg1")] // upper + lower + digit
    [InlineData("Abcdefg!")] // upper + lower + special
    [InlineData("ABCDEFG1!")] // upper + digit + special
    [InlineData("abcdefg1!")] // lower + digit + special
    [InlineData("Sup3r$ecurePwd")] // all four criteria
    public void IsValid_ReturnsTrue_WhenPasswordMeetsThreeOfFourCriteria(string password)
    {
        Assert.True(PasswordPolicy.IsValid(password));
    }

    /// <summary>Is valid returns false, when password null or empty.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsValid_ReturnsFalse_WhenPasswordNullOrEmpty(string? password)
    {
        Assert.False(PasswordPolicy.IsValid(password));
    }

    /// <summary>Verifies that a password that is too short or meets fewer than three criteria is invalid.</summary>
    [Theory]
    [InlineData("Abc12")] // meets 3 criteria but under 8 characters
    [InlineData("Abcdefgh")] // only upper + lower (2 criteria)
    [InlineData("abcdefgh1")] // only lower + digit (2 criteria)
    [InlineData("abcdefghijk")] // only lowercase (1 criterion)
    [InlineData("12345678")] // only digits (1 criterion)
    public void IsValid_ReturnsFalse_WhenPasswordFailsComplexityRule(string password)
    {
        Assert.False(PasswordPolicy.IsValid(password));
    }

    /// <summary>Is within max byte length counts UTF-8 bytes, not characters: 24 Hangul fit (72 bytes), 25 do not (75).</summary>
    [Theory]
    [InlineData(0, true)]
    [InlineData(24, true)] // 24 chars x 3 bytes = 72
    [InlineData(25, false)] // 25 chars x 3 bytes = 75
    public void IsWithinMaxByteLength_CountsUtf8Bytes(int hangulCount, bool expected)
    {
        Assert.Equal(expected, PasswordPolicy.IsWithinMaxByteLength(new string('가', hangulCount)));
    }

    /// <summary>Is within max byte length treats null and empty as within the limit.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsWithinMaxByteLength_ReturnsTrue_ForNullOrEmpty(string? password)
    {
        Assert.True(PasswordPolicy.IsWithinMaxByteLength(password));
    }

    /// <summary>Is valid rejects a password over the byte cap even though it is well under any character cap.</summary>
    [Fact]
    public void IsValid_ReturnsFalse_WhenPasswordExceedsMaxBytesButNotMaxChars()
    {
        Assert.False(PasswordPolicy.IsValid("Aa1" + new string('가', 25))); // 78 bytes, 28 chars
        Assert.True(PasswordPolicy.IsValid("Aa1" + new string('가', 23))); // 72 bytes exactly
    }
}
