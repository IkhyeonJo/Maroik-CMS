using Maroik.Website.Controllers;
using Maroik.Website.Extensions;
using Microsoft.AspNetCore.Mvc.Localization;
using Moq;

namespace Maroik.Website.Tests.Extensions;

/// <summary>
/// Unit tests for <see cref="AccountEmailTemplateExtensions"/>. The localizer mock simply echoes
/// each lookup key back as the resolved value, so a passing test confirms each
/// <see cref="Maroik.Core.Contract.Dtos.EmailTemplate"/> field is wired to the *correct* resource
/// key -- a swapped Subject/Title or reused key would show up as a mismatch here even though every
/// field still gets *some* localized text.
/// </summary>
public class AccountEmailTemplateExtensionsTests
{
    private static IHtmlLocalizer<AccountController> MakeEchoLocalizer()
    {
        var mock = new Mock<IHtmlLocalizer<AccountController>>();
        mock.Setup(l => l[It.IsAny<string>()]).Returns((string name) => new LocalizedHtmlString(name, name));
        return mock.Object;
    }

    /// <summary>Each field of the confirmation-email template is sourced from its own, distinct resource key.</summary>
    [Fact]
    public void ToConfirmationEmailTemplate_MapsEachFieldToItsOwnResourceKey()
    {
        var localizer = MakeEchoLocalizer();

        var template = localizer.ToConfirmationEmailTemplate();

        Assert.Equal("Maroik Email Confirmation", template.Subject);
        Assert.Equal("Welcome to Maroik", template.Title);
        Assert.Equal("Click the link below, then enter the password you chose when signing up to verify your Email", template.Content0);
        Assert.Equal("If this link does not work, please copy & paste this link to your Internet URL", template.Content1);
    }

    /// <summary>Each field of the reset-password-email template is sourced from its own, distinct resource key.</summary>
    [Fact]
    public void ToResetPasswordEmailTemplate_MapsEachFieldToItsOwnResourceKey()
    {
        var localizer = MakeEchoLocalizer();

        var template = localizer.ToResetPasswordEmailTemplate();

        Assert.Equal("Maroik Reset Password", template.Subject);
        Assert.Equal("Welcome to Maroik", template.Title);
        Assert.Equal("Click the link below to reset your password", template.Content0);
        Assert.Equal("If this link does not work, please copy & paste this link to your Internet URL", template.Content1);
    }

    /// <summary>The two templates share the same Title and fallback-link Content1 text, but differ in Subject/Content0.</summary>
    [Fact]
    public void BothTemplates_ShareTitleAndFallbackLine_ButDifferInSubjectAndFirstLine()
    {
        var localizer = MakeEchoLocalizer();

        var confirmation = localizer.ToConfirmationEmailTemplate();
        var resetPassword = localizer.ToResetPasswordEmailTemplate();

        Assert.Equal(confirmation.Title, resetPassword.Title);
        Assert.Equal(confirmation.Content1, resetPassword.Content1);
        Assert.NotEqual(confirmation.Subject, resetPassword.Subject);
        Assert.NotEqual(confirmation.Content0, resetPassword.Content0);
    }
}
