using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Accounts;
using Maroik.Core.Service.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Maroik.Core.Service.Tests.Services;

/// <summary>Unit tests for <see cref="SignInRefusalResponder"/>: the answer and the audit log for each refused sign-in.</summary>
public class SignInRefusalResponderTests
{
    /// <summary>Records the audit log.</summary>
    private readonly FakeLogger<AccountService> _logger = new();

    /// <summary>Each refusal answers with its message key and logs a warning naming the account.</summary>
    [Theory]
    [InlineData(SignInRefusal.Deleted, ServiceErrorKeys.AccountDeleted, "Login refused: deleted account user@example.com")]
    [InlineData(SignInRefusal.EmailNotConfirmed, ServiceErrorKeys.EmailNotConfirmed, "Login refused: email not confirmed for user@example.com")]
    [InlineData(SignInRefusal.ServiceTermsNotAgreed, ServiceErrorKeys.ServiceTermsNotAgreed, "Login refused: service terms not accepted for user@example.com")]
    public void Refuse_AnswersAndLogs_EachRefusal(SignInRefusal refusal, string key, string message)
    {
        LoginResult result = SignInRefusalResponder.Refuse(_logger, refusal, "user@example.com");

        Assert.False(result.Success);
        Assert.Equal(key, result.ErrorKey);
        FakeLogRecord record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Equal(message, record.Message);
    }

    /// <summary>Every member of <see cref="SignInRefusal"/> has an answer, so a new refusal fails here until it gets one.</summary>
    [Fact]
    public void Refuse_AnswersEveryRefusal()
    {
        foreach (SignInRefusal refusal in Enum.GetValues<SignInRefusal>())
            Assert.False(SignInRefusalResponder.Refuse(_logger, refusal, "user@example.com").Success);
    }

    /// <summary>A value outside the enum is a programming error and throws rather than guessing an answer.</summary>
    [Fact]
    public void Refuse_Throws_ForAValueOutsideTheEnum()
        => Assert.Throws<ArgumentOutOfRangeException>(() => SignInRefusalResponder.Refuse(_logger, (SignInRefusal)99, "user@example.com"));
}
