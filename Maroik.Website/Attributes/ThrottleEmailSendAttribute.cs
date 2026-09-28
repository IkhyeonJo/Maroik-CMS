using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Localization;

namespace Maroik.Website.Attributes;

/// <summary>
/// Per-target-address cooldown for the account e-mail-sending POST actions (forgot-password and
/// registration / resend-confirmation). Without it, anyone who knows an address can make the app
/// enqueue an unbounded stream of mail to it — and every request also invalidates that address's
/// previous reset / confirmation link.
/// <para>
/// Keyed on the submitted e-mail, <b>not</b> the client IP: the app sits behind Cloudflare with no
/// forwarded-headers handling, so <c>RemoteIpAddress</c> is a Cloudflare edge address shared by all
/// visitors and would throttle everyone at once. Backed by the distributed cache (Valkey); if the
/// cache is unavailable the check fails open, so a real password reset is never blocked by a blip.
/// </para>
/// <para>
/// On a cooldown hit the action does not run; the request is redirected back to the form with a
/// generic "wait a moment" notice. The cooldown key is the address the caller typed, so this
/// discloses nothing about whether that address is registered — the endpoint's e-mail-enumeration
/// resistance is unchanged.
/// </para>
/// <para>
/// The cooldown window is claimed <em>before</em> the action runs (right after the cooldown-hit
/// check passes), not after — a concurrent burst of requests for the same address must not all
/// pass the check before any of them records the claim, or the cooldown never actually bounds the
/// burst. If the action turns out not to have reached a real send attempt (it threw, or bounced on
/// model binding / validation — e.g. a nickname typo on the registration form), the claim is
/// released again afterward so that request doesn't cost the caller their retry window. A
/// well-formed address the action actually processed keeps the claim whether or not that address
/// turned out to be registered, so the enumeration resistance above still holds.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ThrottleEmailSendAttribute : Attribute, IAsyncActionFilter
{
    /// <summary>
    /// Resource key (and English fallback text) of the "wait a moment" notice — localized through
    /// <c>Resources/Attributes/ThrottleEmailSendAttribute.{culture}.resx</c>.
    /// </summary>
    internal const string CooldownMessageKey = "Please wait a moment before requesting another email.";

    /// <summary><c>HttpContext.Items</c> key set by <see cref="ReleaseCooldownClaim"/>.</summary>
    private const string ReleaseClaimItemKey = "ThrottleEmailSend.ReleaseClaim";

    /// <summary>
    /// Called by an action that finished <em>without</em> reaching a mail send — a service-level
    /// rejection such as "nickname already taken", or a form check that bounced the request after
    /// model binding succeeded. The attribute then releases the cooldown claim it took up front, so
    /// the caller can correct the form and retry at once instead of being told to "wait a moment
    /// before requesting another email" for a mail that was never sent.
    /// </summary>
    public static void ReleaseCooldownClaim(HttpContext httpContext) =>
        httpContext.Items[ReleaseClaimItemKey] = true;

    /// <summary>Length of the cooldown window, in seconds. Defaults to 60.</summary>
    public int WindowSeconds { get; init; } = 60;

    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        string? email = ExtractEmail(context.ActionArguments.Values);
        var cache = context.HttpContext.RequestServices.GetService<IDistributedCache>();
        var logger = context.HttpContext.RequestServices.GetService<ILogger<ThrottleEmailSendAttribute>>();
        string action = context.RouteData.Values["action"]?.ToString() ?? "";
        CancellationToken ct = context.HttpContext.RequestAborted;

        string? key = cache != null && !string.IsNullOrWhiteSpace(email)
            ? $"emailsend:{action}:{email.Trim().ToLowerInvariant()}"
            : null;

        if (key != null)
        {
            try
            {
                if (await cache!.GetAsync(key, ct) is not null)
                {
                    if (context.Controller is Controller controller)
                    {
                        // An attribute has no constructor injection, so resolve the localizer from the
                        // request's services; without one registered, fall back to the English key.
                        var localizer = context.HttpContext.RequestServices
                            .GetService<IStringLocalizer<ThrottleEmailSendAttribute>>();
                        controller.TempData["Error"] = localizer?[CooldownMessageKey].Value ?? CooldownMessageKey;
                    }

                    context.Result = new RedirectToActionResult(
                        action,
                        context.RouteData.Values["controller"]?.ToString() ?? "Account",
                        routeValues: null);
                    return;
                }

                // Claim the cooldown window immediately, before the action (and its possibly-slow
                // mail send) runs — not after, as this used to do. Deferring the write let every
                // request in a concurrent burst for the same address pass the GetAsync check above
                // before any of them reached the write, defeating the cooldown entirely for the
                // whole action+mail-send duration. This still leaves a check-then-act gap between
                // GetAsync and SetAsync (IDistributedCache has no atomic "set if absent"), but that
                // gap is now a single cache round trip instead of an entire request's processing
                // time. If the action turns out not to have actually reached a send attempt, the
                // claim is released again below.
                await cache.SetAsync(
                    key,
                    [1],
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(WindowSeconds)
                    },
                    ct);
            }
            catch (Exception e)
            {
                // Cache unavailable — fail open. Blocking a genuine password reset because Valkey
                // hiccuped is worse than briefly losing the send-rate cap. Also skip the
                // post-action release below (nothing was claimed anyway).
                key = null;
                logger?.LogWarning(e, "Distributed cache unavailable: the send-rate throttle is inactive for this request");
            }
        }

        ActionExecutedContext executed = await next();

        // Release the claim when the action did not actually reach a send attempt: a request that
        // threw, or that bounced on model binding / validation (the action returns its view without
        // ever calling the mail service), must not cost the caller their retry window. A well-formed
        // address the action processed keeps the claim regardless of whether it was registered,
        // preserving e-mail-enumeration resistance.
        bool actionAskedForRelease = context.HttpContext.Items.ContainsKey(ReleaseClaimItemKey);
        if (key != null && (executed.Exception != null || !context.ModelState.IsValid || actionAskedForRelease))
        {
            try { await cache!.RemoveAsync(key, ct); }
            catch (Exception e)
            {
                // Cache unavailable — fail open (see above): worst case the caller loses this
                // window's retry, same as before the claim-first change.
                logger?.LogWarning(e, "The send-rate throttle claim could not be released; the caller loses this retry window");
            }
        }
    }

    /// <summary>
    /// Finds the target e-mail among the bound action arguments: a bare <see cref="string"/> that
    /// looks like an address, or the <c>Email</c> property of a bound view model.
    /// </summary>
    private static string? ExtractEmail(IEnumerable<object?> actionArguments)
    {
        foreach (object? argument in actionArguments)
        {
            switch (argument)
            {
                case null:
                    continue;
                case string s when s.Contains('@', StringComparison.Ordinal):
                    return s;
                default:
                    if (argument.GetType().GetProperty("Email")?.GetValue(argument) is string email
                        && email.Contains('@', StringComparison.Ordinal))
                    {
                        return email;
                    }
                    break;
            }
        }

        return null;
    }
}
