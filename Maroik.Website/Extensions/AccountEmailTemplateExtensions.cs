using Maroik.Core.Contract.Dtos;
using Maroik.Website.Controllers;
using Microsoft.AspNetCore.Mvc.Localization;

namespace Maroik.Website.Extensions;

/// <summary>
/// Builds the localized <see cref="EmailTemplate"/> content for the transactional emails
/// <see cref="AccountController"/> asks <see cref="Maroik.Core.Contract.Interfaces.IAccountService"/> to send.
/// </summary>
public static class AccountEmailTemplateExtensions
{
    extension(IHtmlLocalizer<AccountController> localizer)
    {
        /// <summary>Builds the template shared by new-registration and resend-confirmation emails.</summary>
        public EmailTemplate ToConfirmationEmailTemplate() => new()
        {
            Subject = localizer["Maroik Email Confirmation"].Value,
            Title = localizer["Welcome to Maroik"].Value,
            Content0 = localizer["Click the link below, then enter the password you chose when signing up to verify your Email"].Value,
            Content1 = localizer["If this link does not work, please copy & paste this link to your Internet URL"].Value
        };

        /// <summary>Builds the template used for the forgot-password email.</summary>
        public EmailTemplate ToResetPasswordEmailTemplate() => new()
        {
            Subject = localizer["Maroik Reset Password"].Value,
            Title = localizer["Welcome to Maroik"].Value,
            Content0 = localizer["Click the link below to reset your password"].Value,
            Content1 = localizer["If this link does not work, please copy & paste this link to your Internet URL"].Value
        };

        /// <summary>Builds the alert mailed to an account's owner when failed sign-ins started its first login wait.</summary>
        public EmailTemplate ToLoginAlertEmailTemplate() => new()
        {
            Subject = localizer["Maroik sign-in alert"].Value,
            Title = localizer["Several sign-ins to your account failed"].Value,
            Content0 = localizer["New sign-ins to your account are paused for a while. If this was not you, reset your password from the link below."].Value,
            Content1 = localizer["If this link does not work, please copy & paste this link to your Internet URL"].Value
        };
    }
}
