using Maroik.Core.Domain.Accounts;
namespace Maroik.Core.Domain.Tests.Accounts;

/// <summary>
/// Unit tests for the account states that decide a sign-in and a password reset:
/// <see cref="Domain.Accounts.Account.CheckSignInOnceAuthenticated"/>, <see cref="Domain.Accounts.Account.CanRequestPasswordReset"/>
/// and <see cref="Domain.Accounts.Account.AcceptsResetToken"/>.
/// </summary>
public class AccountSignInTests
{
    /// <summary>The fixed "current time" every domain call in this class receives.</summary>
    private static readonly DateTime Now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>A stored account in the given state; by default confirmed, terms accepted, not deleted, not locked.</summary>
    private static Domain.Accounts.Account AccountIn(
        bool deleted = false, bool emailConfirmed = true, bool agreedServiceTerms = true, bool locked = false, string? resetToken = null)
        => Domain.Accounts.Account.Reconstitute("user@example.com", "hashed", "Nick", null, "User", "UTC", "KRW",
            locked, 0, emailConfirmed, agreedServiceTerms, null, resetToken, Now, Now, null, deleted, "stamp", false);

    // -- CheckSignInOnceAuthenticated -------------------------------------------

    /// <summary>A deleted account is refused first, whatever else is wrong with it.</summary>
    [Fact]
    public void CheckSignIn_RefusesADeletedAccount_First()
        => Assert.Equal(SignInRefusal.Deleted,
            AccountIn(deleted: true, emailConfirmed: false, agreedServiceTerms: false).CheckSignInOnceAuthenticated());

    /// <summary>An unconfirmed account is refused before the service terms are looked at.</summary>
    [Fact]
    public void CheckSignIn_RefusesAnUnconfirmedAccount_BeforeTheTerms()
        => Assert.Equal(SignInRefusal.EmailNotConfirmed,
            AccountIn(emailConfirmed: false, agreedServiceTerms: false).CheckSignInOnceAuthenticated());

    /// <summary>An account that has not accepted the service terms is refused.</summary>
    [Fact]
    public void CheckSignIn_RefusesAnAccountWithoutTheTerms()
        => Assert.Equal(SignInRefusal.ServiceTermsNotAgreed, AccountIn(agreedServiceTerms: false).CheckSignInOnceAuthenticated());

    /// <summary>An active, confirmed account with the terms accepted is signed in; a lock is not this check's concern.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CheckSignIn_AcceptsAnActiveConfirmedAccount(bool locked)
        => Assert.Null(AccountIn(locked: locked).CheckSignInOnceAuthenticated());

    // -- CanRequestPasswordReset ------------------------------------------------

    /// <summary>A confirmed account that is not deleted gets a reset mail, even while locked.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CanRequestPasswordReset_IsTrue_ForAConfirmedActiveAccount(bool locked)
        => Assert.True(AccountIn(locked: locked).CanRequestPasswordReset);

    /// <summary>A deleted or unconfirmed account gets no reset mail.</summary>
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void CanRequestPasswordReset_IsFalse_ForADeletedOrUnconfirmedAccount(bool deleted, bool emailConfirmed)
        => Assert.False(AccountIn(deleted: deleted, emailConfirmed: emailConfirmed).CanRequestPasswordReset);

    // -- AcceptsResetToken ------------------------------------------------------

    /// <summary>A live token of a qualifying account is accepted.</summary>
    [Fact]
    public void AcceptsResetToken_IsTrue_ForALiveTokenOfAQualifyingAccount()
    {
        string token = GuidToken.Generate(Now);

        Assert.True(AccountIn(resetToken: token).AcceptsResetToken(token, Now.AddHours(24)));
    }

    /// <summary>An expired token is refused.</summary>
    [Fact]
    public void AcceptsResetToken_IsFalse_ForAnExpiredToken()
    {
        string token = GuidToken.Generate(Now);

        Assert.False(AccountIn(resetToken: token).AcceptsResetToken(token, Now.AddHours(24).AddTicks(1)));
    }

    /// <summary>A live token of a deleted or unconfirmed account is refused.</summary>
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void AcceptsResetToken_IsFalse_ForADeletedOrUnconfirmedAccount(bool deleted, bool emailConfirmed)
    {
        string token = GuidToken.Generate(Now);

        Assert.False(AccountIn(deleted: deleted, emailConfirmed: emailConfirmed, resetToken: token).AcceptsResetToken(token, Now));
    }
}
