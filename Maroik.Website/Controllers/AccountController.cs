using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Misc.Helpers;
using Maroik.Core.Domain.Account;
using Maroik.Core.Domain.Localization;
using Maroik.Website.Attributes;
using Maroik.Website.Contracts;
using Maroik.Website.Extensions;
using Maroik.Website.Models.ViewModels.Account;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;

namespace Maroik.Website.Controllers;

/// <summary>
/// Handles all self-service account flows: consent, login, logout, registration,
/// email confirmation, forgot-password, and password reset.
/// </summary>
public class AccountController(
    IHtmlLocalizer<AccountController> localizer,
    IAccountService accountService,
    ISessionService sessionService) : Controller
{
    #region ConsentForm
    /// <summary>Displays the service consent form.</summary>
    [HttpGet]
    public IActionResult ConsentForm() => View();
    #endregion

    #region Login
    /// <summary>Displays the login form (GET) or processes login credentials (POST).</summary>
    [HttpGet]
    public IActionResult Login() => View();

    /// <summary>Displays the login form (GET) or processes login credentials (POST).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginInputViewModel loginInputViewModel, CancellationToken ct)
    {
        string userAgent = Request.Headers.UserAgent.ToString();
        if (userAgent.Contains("MSIE") || userAgent.Contains("Trident"))
        {
            TempData["Error"] = localizer["BlockInternetExplorer"].Value;
            return View();
        }

        _ = ModelState.Remove(nameof(loginInputViewModel.Password));
        _ = ModelState.Remove(nameof(loginInputViewModel.Nickname));

        if (!ModelState.IsValid) return View();

        var result = await accountService.LoginAsync(loginInputViewModel.Email!, loginInputViewModel.Password ?? "", ct);

        if (!result.Success)
        {
            TempData["Error"] = localizer[result.ErrorKey].Value;
            return View();
        }

        // Session fixation mitigation: mint a fresh, unpredictable session id before attaching the
        // account to it, so a session id an attacker fixated before login never becomes authenticated.
        HttpContext.RegenerateSession();
        sessionService.SetAccount(result.Account!);
        // The new session created by RegenerateSession() above isn't the one SessionMiddleware
        // auto-commits at the end of the request, so it must be saved explicitly here.
        await HttpContext.Session.CommitAsync(ct);

        // AuthorizationFilter is the actual enforcement (it re-checks MustChangePassword from the
        // DB on every request); this redirect is just so a forced account lands there directly
        // instead of bouncing through the normal dashboard first.
        return result.Account!.MustChangePassword
            ? RedirectToAction("Profile", "Management")
            : RedirectToAction("AnonymousIndex", "Dashboard");
    }
    #endregion

    #region Logout
    /// <summary>
    /// Clears the session and redirects to the anonymous dashboard. POST + antiforgery so a
    /// cross-site page cannot force-log-out a signed-in user with a bare link / image tag.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        sessionService.RemoveAccount();
        return RedirectToAction("AnonymousIndex", "Dashboard");
    }
    #endregion

    #region Register
    /// <summary>Displays the registration form (GET) or processes new-account / resend-email requests (POST).</summary>
    [HttpGet]
    public IActionResult Register()
    {
        // Pre-select the time zone implied by the request culture (e.g. ko-KR -> Asia/Seoul);
        // the rule lives in CulturePolicy so the client no longer does it.
        var culture = HttpContext.Features
            .Get<Microsoft.AspNetCore.Localization.IRequestCultureFeature>()?
            .RequestCulture.Culture.Name;
        return View(new LoginOutputViewModel
        {
            TimeZoneIanaId = CulturePolicy.DefaultTimeZoneIanaId(culture)
        });
    }

    /// <summary>Displays the registration form (GET) or processes new-account / resend-email requests (POST).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ThrottleEmailSend]
    public async Task<IActionResult> Register(LoginInputViewModel loginInputViewModel, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(loginInputViewModel.Password)) // Registration path
        {
            // Re-render the form (keeps ModelState so field errors and typed-in values survive)
            // instead of redirecting to a fresh GET, which discards both.
            if (!ModelState.IsValid) return View();

            if (!loginInputViewModel.AgreedServiceTerms)
            {
                ThrottleEmailSendAttribute.ReleaseCooldownClaim(HttpContext); // no mail was sent
                TempData["Error"] = localizer["Please check the consent form"].Value;
                return View();
            }

            if (string.IsNullOrEmpty(loginInputViewModel.TimeZoneIanaId))
            {
                ThrottleEmailSendAttribute.ReleaseCooldownClaim(HttpContext); // no mail was sent
                TempData["Error"] = localizer["Please select time zone"].Value;
                return View();
            }

            var emailTemplate = localizer.ToConfirmationEmailTemplate();

            var newAccount = new AccountRequest
            {
                Email = loginInputViewModel.Email,
                PlainPassword = loginInputViewModel.Password, // service validates + hashes
                Nickname = loginInputViewModel.Nickname,
                AvatarImagePath = Account.DefaultAvatarImagePath,
                Role = Role.User,
                TimeZoneIanaId = loginInputViewModel.TimeZoneIanaId,
                Locked = false,
                EmailConfirmed = false,
                AgreedServiceTerms = true,
                RegistrationToken = GuidToken.Generate(),
                Message = EnumHelper.GetDescription(AccountMessage.UserCreatedVerifyEmail),
                Deleted = false
            };

            var result = await accountService.RegisterAsync(newAccount, emailTemplate, ct);

            if (!result.Success)
            {
                // Rejected before / instead of a mail send (nickname taken, weak password, account
                // already confirmed, ...): the caller may fix the form and retry immediately.
                ThrottleEmailSendAttribute.ReleaseCooldownClaim(HttpContext);
                TempData["Error"] = localizer.ToRawText(result.ErrorKey, result.ErrorArgs);
                ViewBag.ResendEmail = result.ShowResendEmail;
                return View();
            }

            ViewBag.ResendEmail = result.ShowResendEmail;
            ViewBag.ResentEmailAddress = result.EmailAddress;
        }
        else // Email resend path
        {
            _ = ModelState.Remove(nameof(loginInputViewModel.Password));
            _ = ModelState.Remove(nameof(loginInputViewModel.Nickname));

            if (!ModelState.IsValid) return View();

            var emailTemplate = localizer.ToConfirmationEmailTemplate();

            var result = await accountService.ResendConfirmationEmailAsync(loginInputViewModel.Email!, emailTemplate, ct);

            if (!result.Success)
            {
                // Rejected before / instead of a mail send (nickname taken, weak password, account
                // already confirmed, ...): the caller may fix the form and retry immediately.
                ThrottleEmailSendAttribute.ReleaseCooldownClaim(HttpContext);
                TempData["Error"] = localizer.ToRawText(result.ErrorKey, result.ErrorArgs);
                ViewBag.ResendEmail = result.ShowResendEmail;
                return View();
            }

            ViewBag.ResendEmail = result.ShowResendEmail;
            ViewBag.ResentEmailAddress = result.EmailAddress;
            ViewBag.RepeatEmailSend = result.RepeatEmailSend;
        }

        return View();
    }
    #endregion

    #region ConfirmEmail
    /// <summary>Validates the email-confirmation token from the registration link and activates the account.</summary>
    [HttpGet]
    public async Task<IActionResult> ConfirmEmail(string registrationToken, CancellationToken ct)
    {
        var result = await accountService.ConfirmEmailAsync(registrationToken, ct);

        if (!string.IsNullOrEmpty(result.ErrorKey))
            TempData["Error"] = localizer[result.ErrorKey].Value;

        ViewBag.InvalidToken = result.InvalidToken;
        ViewBag.AccountCreated = result.AccountCreated;
        return View();
    }
    #endregion

    #region ForgotPassword
    /// <summary>Displays the forgot-password form (GET) or sends a reset-password email (POST).</summary>
    [HttpGet]
    public IActionResult ForgotPassword() => View();

    /// <summary>Displays the forgot-password form (GET) or sends a reset-password email (POST).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ThrottleEmailSend]
    public async Task<IActionResult> ForgotPassword(LoginInputViewModel loginInputViewModel, CancellationToken ct)
    {
        _ = ModelState.Remove(nameof(loginInputViewModel.Nickname));
        _ = ModelState.Remove(nameof(loginInputViewModel.Password));

        if (!ModelState.IsValid) return View();

        var emailTemplate = localizer.ToResetPasswordEmailTemplate();

        await accountService.ForgotPasswordAsync(loginInputViewModel.Email!, emailTemplate, ct);

        ViewBag.MailSent = true;
        return View();
    }

    /// <summary>Validates the reset token (GET) or applies the new password (POST).</summary>
    [HttpGet]
    public async Task<IActionResult> ResetPassword(string resetPasswordToken, CancellationToken ct)
    {
        var result = await accountService.ValidateResetPasswordTokenAsync(resetPasswordToken, ct);
        ViewBag.FailToResetPassword = result.FailToReset;
        ViewBag.ResetPasswordToken = result.ResetPasswordToken;
        return View();
    }

    /// <summary>Validates the reset token (GET) or applies the new password (POST).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(LoginInputViewModel loginInputViewModel, CancellationToken ct)
    {
        _ = ModelState.Remove(nameof(loginInputViewModel.Email));
        _ = ModelState.Remove(nameof(loginInputViewModel.Nickname));

        if (!ModelState.IsValid)
        {
            // Re-render with the token so the form stays submittable; ModelState carries the field errors.
            ViewBag.ResetPasswordToken = loginInputViewModel.ResetPasswordToken;
            return View();
        }

        var result = await accountService.ResetPasswordAsync(
            loginInputViewModel.ResetPasswordToken ?? "",
            loginInputViewModel.Password ?? "",
            ct);

        if (!result.Success)
        {
            if (result.ErrorKey == "reset-password-invalid")
            {
                ViewBag.FailToResetPassword = true;
                return View();
            }
            // Any other refusal (the password rule, ...) says nothing against the token itself — keep
            // showing the form, with the token, so the user can pick another password.
            TempData["Error"] = localizer.ToRawText(result.ErrorKey, result.ErrorArgs);
            ViewBag.ResetPasswordToken = loginInputViewModel.ResetPasswordToken;
            return View();
        }

        ViewBag.ResetPasswordComplete = true;
        return View();
    }
    #endregion
}
