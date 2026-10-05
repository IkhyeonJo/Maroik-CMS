using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
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
    ISessionService sessionService,
    ILogger<AccountController> logger) : Controller
{
    #region ConsentForm
    /// <summary>Displays the service consent form.</summary>
    [HttpGet]
    public IActionResult ConsentForm() => View();
    #endregion

    #region Login
    /// <summary>Displays the login form.</summary>
    [HttpGet]
    public IActionResult Login() => View();

    /// <summary>
    /// Processes login credentials: on success regenerates the session id, stores the account in it and
    /// redirects (to the profile page when a password change is forced); otherwise re-renders the form with the error.
    /// </summary>
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
    /// <summary>Displays the registration form, pre-selecting the time zone implied by the request culture.</summary>
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

    /// <summary>
    /// Processes a registration (when a password is posted) or a resend-confirmation-email request (when
    /// only the email is posted). Throttled per target address by <see cref="ThrottleEmailSendAttribute"/>.
    /// </summary>
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

            // Role, confirmation state and the registration token are fixed by the service.
            var newAccount = new RegisterAccountRequest
            {
                Email = loginInputViewModel.Email,
                PlainPassword = loginInputViewModel.Password, // service validates + hashes
                Nickname = loginInputViewModel.Nickname,
                TimeZoneIanaId = loginInputViewModel.TimeZoneIanaId,
                AgreedServiceTerms = true
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
                // Rejected before / instead of a mail send (unknown address, account already
                // confirmed, mail could not be queued, ...): the caller may retry immediately.
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
    /// <summary>
    /// Opens the link mailed after registration. A live link shows a form asking for the password
    /// chosen at registration; opening the link alone activates nothing.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ConfirmEmail(string registrationToken, CancellationToken ct)
    {
        var result = await accountService.ValidateRegistrationTokenAsync(registrationToken, ct);

        ViewBag.InvalidToken = result.InvalidToken;
        ViewBag.AccountCreated = result.AccountCreated;
        ViewBag.RegistrationToken = result.RegistrationToken;
        return View();
    }

    /// <summary>
    /// Activates the account when the mailed link and the registration password are presented
    /// together, so an address someone else pre-registered is never activated with their password.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmEmail(LoginInputViewModel loginInputViewModel, CancellationToken ct)
    {
        _ = ModelState.Remove(nameof(loginInputViewModel.Email));
        _ = ModelState.Remove(nameof(loginInputViewModel.Nickname));

        if (!ModelState.IsValid)
        {
            // Re-render the form with the token; ModelState carries the field errors.
            ViewBag.RegistrationToken = loginInputViewModel.RegistrationToken;
            return View();
        }

        var result = await accountService.ConfirmEmailAsync(
            loginInputViewModel.RegistrationToken ?? "", loginInputViewModel.Password ?? "", ct);

        if (result.WrongPassword)
            TempData["Error"] = localizer["The password does not match the one you registered with."].Value;
        else if (!string.IsNullOrEmpty(result.ErrorKey))
            TempData["Error"] = localizer[result.ErrorKey].Value;

        ViewBag.InvalidToken = result.InvalidToken;
        ViewBag.AccountCreated = result.AccountCreated;
        ViewBag.RegistrationToken = result.RegistrationToken;
        return View();
    }
    #endregion

    #region ForgotPassword
    /// <summary>Displays the forgot-password form.</summary>
    [HttpGet]
    public IActionResult ForgotPassword() => View();

    /// <summary>
    /// Queues a reset-password email when the address belongs to a confirmed account, and always shows the
    /// same "mail sent" page (no email enumeration). Throttled per target address by <see cref="ThrottleEmailSendAttribute"/>.
    /// </summary>
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

    /// <summary>Opens the mailed reset link: validates the token and shows the new-password form, or the invalid-link message.</summary>
    [HttpGet]
    public async Task<IActionResult> ResetPassword(string resetPasswordToken, CancellationToken ct)
    {
        var result = await accountService.ValidateResetPasswordTokenAsync(resetPasswordToken, ct);
        ViewBag.FailToResetPassword = result.FailToReset;
        ViewBag.ResetPasswordToken = result.ResetPasswordToken;
        return View();
    }

    /// <summary>Applies the new password for the posted reset token; an invalid token shows the invalid-link message.</summary>
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

        var (result, signIn) = await accountService.ResetPasswordAsync(
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

        if (signIn == null)
        {
            // Password changed, but the account may not sign in yet (service terms not accepted).
            ViewBag.ResetPasswordComplete = true;
            return View();
        }

        // Sign the user in with the new password straight away, exactly as Login does: a fresh session id (a
        // session id planted in the browser before the reset never becomes signed in), the account with its new
        // security stamp, committed explicitly because RegenerateSession's session is not the auto-committed one.
        HttpContext.RegenerateSession();
        sessionService.SetAccount(signIn);
        await HttpContext.Session.CommitAsync(ct);
 #pragma warning disable CA1873
        logger.LogInformation("Signed in after password reset: {Email}", signIn.Email);
 #pragma warning restore CA1873

        return RedirectToAction("AnonymousIndex", "Dashboard");
    }
    #endregion
}
