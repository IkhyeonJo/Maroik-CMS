using Maroik.Core.Contract.Extensions;
using Maroik.Core.Domain.Accounts;

namespace Maroik.Core.Contract.Tests.Extensions;

/// <summary>
/// Unit tests for <see cref="Maroik.Core.Contract.Extensions.EnumExtensions"/>.
/// Verifies that <c>GetDescription</c> returns the value of the
/// <see cref="System.ComponentModel.DescriptionAttribute"/> on each enum member,
/// and falls back to the member name when no attribute is present.
/// </summary>
public class EnumExtensionsTests
{
    /// <summary>Verifies that <c>GetDescription</c> returns description attribute for account message.</summary>
    [Theory]
    [InlineData(AccountMessage.Success, "Success")]
    [InlineData(AccountMessage.FailToMailSent, "Fail to mail sent")]
    [InlineData(AccountMessage.UserAlreadyCreated, "User already created, please login")]
    [InlineData(AccountMessage.VerifyEmail, "User already created, please verify your given mail Id")]
    [InlineData(AccountMessage.SuccessToResetPassword, "Success to reset password")]
    [InlineData(AccountMessage.ResetPasswordMail, "Email has been sent to reset password")]
    [InlineData(AccountMessage.UserCreatedVerifyEmail, "User created, Check email, click link and verify")]
    public void GetDescription_ReturnsDescriptionAttribute_ForAccountMessage(AccountMessage value, string expected)
    {
        string result = value.GetDescription();

        Assert.Equal(expected, result);
    }

    /// <summary>A value that is not a defined member of its enum has no member info, so its number is returned.</summary>
    [Fact]
    public void GetDescription_FallsBackToTheValueText_ForAnUndefinedEnumValue()
    {
        Assert.Equal("99", ((DayOfWeek)99).GetDescription());
    }
}
