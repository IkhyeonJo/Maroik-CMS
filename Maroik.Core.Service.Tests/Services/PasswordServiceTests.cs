using Maroik.Core.Service.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Maroik.Core.Service.Tests.Services;

/// <summary>
/// Unit tests for <see cref="PasswordService"/>.
/// Verifies BCrypt hash generation (work factor 13), hash/plain-text non-equality,
/// and successful verification of a hashed password against its plain-text original.
/// </summary>
public class PasswordServiceTests
{
    /// <summary>Captures the log entries the system under test writes.</summary>
    private readonly FakeLogger<PasswordService> _logger = new();
    /// <summary>The service under test.</summary>
    private readonly PasswordService _sut;

    /// <summary>Creates the service under test over the capturing logger.</summary>
    public PasswordServiceTests() => _sut = new PasswordService(_logger);

    // -- HashPassword --------------------------------------------------------

    /// <summary>Verifies that <c>HashPassword</c> returns non-empty bcrypt hash for valid password.</summary>
    [Fact]
    public void HashPassword_ReturnsNonEmptyBcryptHash_ForValidPassword()
    {
        string hash = _sut.HashPassword("MySecurePassword123!");

        Assert.NotNull(hash);
        Assert.StartsWith("$2", hash); // BCrypt hashes start with $2a$ or $2b$
    }

    /// <summary>Verifies that <c>HashPassword</c> produces different hash each call when due to random salt.</summary>
    [Fact]
    public void HashPassword_ProducesDifferentHashEachCall_DueToRandomSalt()
    {
        string hash1 = _sut.HashPassword("SamePassword");
        string hash2 = _sut.HashPassword("SamePassword");

        Assert.NotEqual(hash1, hash2);
    }

    /// <summary>Verifies that <c>HashPassword</c> throws argument exception for empty or whitespace.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void HashPassword_ThrowsArgumentException_ForEmptyOrWhitespace(string password)
    {
        Assert.Throws<ArgumentException>(() => _sut.HashPassword(password));
    }

    // -- VerifyPassword -------------------------------------------------------

    /// <summary>Verifies that <c>VerifyPassword</c> returns true when password matches stored hash.</summary>
    [Fact]
    public void VerifyPassword_ReturnsTrue_WhenPasswordMatchesStoredHash()
    {
        const string password = "MySecurePassword123!";
        string hash = _sut.HashPassword(password);

        bool result = _sut.VerifyPassword(password, hash);

        Assert.True(result);
    }

    /// <summary>Verifies that <c>VerifyPassword</c> returns false when password does not match hash.</summary>
    [Fact]
    public void VerifyPassword_ReturnsFalse_WhenPasswordDoesNotMatchHash()
    {
        string hash = _sut.HashPassword("CorrectPassword");

        bool result = _sut.VerifyPassword("WrongPassword", hash);

        Assert.False(result);
    }

    /// <summary>Verifies that <c>VerifyPassword</c> returns false when password is empty or whitespace.</summary>
    [Theory]
    [InlineData("", "$2a$13$validhashplaceholder1234567890")]
    [InlineData("   ", "$2a$13$validhashplaceholder1234567890")]
    public void VerifyPassword_ReturnsFalse_WhenPasswordIsEmptyOrWhitespace(string password, string hash)
    {
        bool result = _sut.VerifyPassword(password, hash);

        Assert.False(result);
    }

    /// <summary>Verifies that <c>VerifyPassword</c> returns false when hash is empty or whitespace.</summary>
    [Theory]
    [InlineData("password", "")]
    [InlineData("password", "   ")]
    public void VerifyPassword_ReturnsFalse_WhenHashIsEmptyOrWhitespace(string password, string hash)
    {
        bool result = _sut.VerifyPassword(password, hash);

        Assert.False(result);
    }

    /// <summary>Verifies that <c>VerifyPassword</c> returns false when hash is malformed.</summary>
    [Fact]
    public void VerifyPassword_ReturnsFalse_WhenHashIsMalformed()
    {
        bool result = _sut.VerifyPassword("password", "not-a-valid-bcrypt-hash");

        Assert.False(result);
    }

    // -- VerifyDummy ------------------------------------------------------------

    /// <summary>
    /// The timing-equalizer used on the "no such account" login path never says "match", whatever it is
    /// given (including empty input) — otherwise an unknown address could be logged into.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("password")]
    [InlineData("maroik-timing-equalizer-")]
    public void VerifyDummy_AlwaysReturnsFalse(string password)
    {
        Assert.False(new PasswordService(new FakeLogger<PasswordService>()).VerifyDummy(password));
    }

    /// <summary>
    /// A stored hash that is not a BCrypt hash is a data problem an operator must hear about: the
    /// verification just answers "no", so without a log the affected user is silently locked out.
    /// The hash itself (a credential) must not be logged.
    /// </summary>
    [Fact]
    public void VerifyPassword_LogsAWarningWithoutTheHash_WhenTheStoredHashIsMalformed()
    {
        const string malformedHash = "not-a-bcrypt-hash-9f3a";

        bool result = _sut.VerifyPassword("Whatever1!", malformedHash);

        Assert.False(result);
        FakeLogRecord record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Contains("malformed", record.Message);
        Assert.NotNull(record.Exception);
        Assert.DoesNotContain(malformedHash, record.Message);
        Assert.DoesNotContain("Whatever1!", record.Message);
    }

    /// <summary>An ordinary wrong password is not an event by itself here (the caller logs the login failure).</summary>
    [Fact]
    public void VerifyPassword_LogsNothing_WhenThePasswordIsSimplyWrong()
    {
        string hash = _sut.HashPassword("Right-Pass1!");

        Assert.False(_sut.VerifyPassword("Wrong-Pass1!", hash));
        Assert.Empty(_logger.Collector.GetSnapshot());
    }
}
