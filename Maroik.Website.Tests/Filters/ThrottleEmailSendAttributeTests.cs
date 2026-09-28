using System.Globalization;
using Maroik.Website.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
// ReSharper disable UnusedAutoPropertyAccessor.Local

namespace Maroik.Website.Tests.Filters;

/// <summary>
/// Unit tests for <see cref="ThrottleEmailSendAttribute"/>: the per-address cooldown gate on the
/// account e-mail-sending POST actions. Covers the address-extraction fallback (bare string vs. a
/// bound view model's <c>Email</c> property), the cooldown-hit redirect (which must never reach the
/// action), the fail-open behavior when the distributed cache is unavailable or unregistered, that
/// the cooldown claim is released again when the action never reached a real send attempt, and that
/// the claim is written before the action runs (closing the concurrent-burst race).
/// </summary>
public class ThrottleEmailSendAttributeTests
{
    private static (ActionExecutingContext context, FakeController controller) BuildContext(
        object? boundArgument, IDistributedCache? cache, string action = "ForgotPassword", string controllerName = "Account",
        Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        if (cache != null)
            services.AddSingleton(cache);
        configureServices?.Invoke(services);
        var sp = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = sp };
        var controller = new FakeController
        {
            TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>()),
        };
        var actionDescriptor = new ControllerActionDescriptor
        {
            RouteValues = new Dictionary<string, string?> { ["controller"] = controllerName, ["action"] = action },
        };
        // ThrottleEmailSendAttribute reads context.RouteData.Values (not ActionDescriptor.RouteValues)
        // for the action/controller name, so both must carry the same values here.
        var routeData = new RouteData
        {
            Values =
            {
                ["controller"] = controllerName,
                ["action"] = action
            }
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext,
            RouteData = routeData,
            ActionDescriptor = actionDescriptor,
        };

        var actionContext = new ActionContext(httpContext, routeData, actionDescriptor);
        var actionArguments = new Dictionary<string, object?>();
        if (boundArgument != null)
            actionArguments["model"] = boundArgument;

        var executingContext = new ActionExecutingContext(actionContext, [], actionArguments, controller);
        return (executingContext, controller);
    }

    private static ActionExecutionDelegate NextReturning(Action onNext, Exception? exception = null) => () =>
    {
        onNext();
        return Task.FromResult(new ActionExecutedContext(
            new ActionContext(new DefaultHttpContext(), new RouteData(), new ControllerActionDescriptor()), [], null!)
        {
            ExceptionHandled = exception == null,
            Exception = exception,
        });
    };

    private sealed class ModelWithEmail
    {
        /// <summary>The address the throttle attribute reads from the bound model.</summary>
        public string? Email { get; set; }
    }

    // -- No throttling possible --------------------------------------------------

    /// <summary>No bound argument looks like an e-mail address: the action runs and the cache is never touched.</summary>
    [Fact]
    public async Task OnActionExecutionAsync_NoEmailInArguments_RunsAction_NeverTouchesCache()
    {
        var cache = new Mock<IDistributedCache>();
        var (context, _) = BuildContext(boundArgument: "not-an-email", cache.Object);
        bool nextCalled = false;

        await new ThrottleEmailSendAttribute().OnActionExecutionAsync(context, NextReturning(() => nextCalled = true));

        Assert.True(nextCalled);
        Assert.Null(context.Result);
        cache.Verify(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        cache.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>No <see cref="IDistributedCache"/> registered in DI (e.g. Valkey misconfigured): the action still runs, unthrottled.</summary>
    [Fact]
    public async Task OnActionExecutionAsync_NoCacheRegistered_RunsActionUnthrottled()
    {
        var (context, _) = BuildContext(boundArgument: "user@example.com", cache: null);
        bool nextCalled = false;

        await new ThrottleEmailSendAttribute().OnActionExecutionAsync(context, NextReturning(() => nextCalled = true));

        Assert.True(nextCalled);
        Assert.Null(context.Result);
    }

    // -- Cooldown hit --------------------------------------------------------------

    /// <summary>An address still on cooldown is redirected back to the same action without ever running it.</summary>
    [Fact]
    public async Task OnActionExecutionAsync_AddressOnCooldown_RedirectsWithoutRunningAction()
    {
        var cache = new Mock<IDistributedCache>();
        cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([1]);
        var (context, controller) = BuildContext(boundArgument: "user@example.com", cache.Object);
        bool nextCalled = false;

        await new ThrottleEmailSendAttribute().OnActionExecutionAsync(context, NextReturning(() => nextCalled = true));

        Assert.False(nextCalled);
        var redirect = Assert.IsType<RedirectToActionResult>(context.Result);
        Assert.Equal("ForgotPassword", redirect.ActionName);
        Assert.Equal("Please wait a moment before requesting another email.", controller.TempData["Error"]);
        cache.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>The cooldown notice goes through the request's <c>IStringLocalizer</c> when one is registered.</summary>
    [Fact]
    public async Task OnActionExecutionAsync_AddressOnCooldown_UsesTheRegisteredLocalizer()
    {
        var cache = new Mock<IDistributedCache>();
        cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([1]);
        var localizer = new Mock<IStringLocalizer<ThrottleEmailSendAttribute>>();
        localizer.Setup(l => l[ThrottleEmailSendAttribute.CooldownMessageKey])
            .Returns(new LocalizedString(ThrottleEmailSendAttribute.CooldownMessageKey, "localized notice"));
        var (context, controller) = BuildContext(
            boundArgument: "user@example.com", cache.Object,
            configureServices: services => services.AddSingleton(localizer.Object));

        await new ThrottleEmailSendAttribute().OnActionExecutionAsync(context, NextReturning(() => { }));

        Assert.Equal("localized notice", controller.TempData["Error"]);
    }

    /// <summary>
    /// End to end against the real embedded resx pair (<c>Resources/Attributes/ThrottleEmailSendAttribute.*.resx</c>):
    /// the notice must resolve to the culture's own text, not fall back to the English key.
    /// </summary>
    [Theory]
    [InlineData("en-US", "Please wait a moment before requesting another email.")]
    [InlineData("ko-KR", "잠시 후에 다시 이메일을 요청해 주세요.")]
    public async Task OnActionExecutionAsync_AddressOnCooldown_ResolvesNoticeFromTheRealResx(string culture, string expected)
    {
        var cache = new Mock<IDistributedCache>();
        cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([1]);
        var (context, controller) = BuildContext(
            boundArgument: "user@example.com", cache.Object,
            configureServices: services => services.AddLocalization(o => o.ResourcesPath = "Resources"));

        CultureInfo original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo(culture);
            await new ThrottleEmailSendAttribute().OnActionExecutionAsync(context, NextReturning(() => { }));
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }

        Assert.Equal(expected, controller.TempData["Error"]);
    }

    /// <summary>The cooldown key is namespaced by action, lower-cased, and trimmed -- so casing/whitespace on the submitted address can't dodge or split the cooldown.</summary>
    [Fact]
    public async Task OnActionExecutionAsync_BuildsCooldownKey_LowercasedTrimmed_NamespacedByAction()
    {
        var cache = new Mock<IDistributedCache>();
        string? capturedKey = null;
        cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((key, _) => capturedKey = key)
            .ReturnsAsync((byte[]?)null);
        cache.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var (context, _) = BuildContext(boundArgument: "  User@Example.COM  ", cache.Object, action: "Register");

        await new ThrottleEmailSendAttribute().OnActionExecutionAsync(context, NextReturning(() => { }));

        Assert.Equal("emailsend:Register:user@example.com", capturedKey);
    }

    // -- Address extraction fallback ------------------------------------------------

    /// <summary>The address is also found via reflection on a bound view model's <c>Email</c> property, not just a bare string argument.</summary>
    [Fact]
    public async Task OnActionExecutionAsync_ExtractsEmail_FromViewModelProperty()
    {
        var cache = new Mock<IDistributedCache>();
        cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((byte[]?)null);
        cache.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var (context, _) = BuildContext(boundArgument: new ModelWithEmail { Email = "model@example.com" }, cache.Object);

        await new ThrottleEmailSendAttribute().OnActionExecutionAsync(context, NextReturning(() => { }));

        cache.Verify(c => c.SetAsync(
            It.Is<string>(k => k.Contains("model@example.com", StringComparison.Ordinal)),
            It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // -- Fail-open on cache errors -----------------------------------------------

    /// <summary>A cache read failure (e.g. Valkey blip) fails open: the action still runs, unthrottled.</summary>
    [Fact]
    public async Task OnActionExecutionAsync_CacheReadThrows_FailsOpen_RunsAction()
    {
        var cache = new Mock<IDistributedCache>();
        cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException("cache down"));
        var (context, _) = BuildContext(boundArgument: "user@example.com", cache.Object);
        bool nextCalled = false;

        await new ThrottleEmailSendAttribute().OnActionExecutionAsync(context, NextReturning(() => nextCalled = true));

        Assert.True(nextCalled);
        Assert.Null(context.Result);
        cache.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A cache write failure after a successful send is swallowed -- the response the user already saw is unaffected.</summary>
    [Fact]
    public async Task OnActionExecutionAsync_CacheWriteThrows_IsSwallowed()
    {
        var cache = new Mock<IDistributedCache>();
        cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((byte[]?)null);
        cache.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("cache down"));
        var (context, _) = BuildContext(boundArgument: "user@example.com", cache.Object);
        bool nextCalled = false;

        var exception = await Record.ExceptionAsync(() =>
            new ThrottleEmailSendAttribute().OnActionExecutionAsync(context, NextReturning(() => nextCalled = true)));

        Assert.Null(exception);
        Assert.True(nextCalled);
    }

    // -- Cooldown only armed after a real, successful send attempt ------------------

    /// <summary>After a well-formed address is processed successfully, the cooldown is armed with the configured window.</summary>
    [Fact]
    public async Task OnActionExecutionAsync_SuccessfulSend_ArmsCooldown_WithConfiguredWindow()
    {
        var cache = new Mock<IDistributedCache>();
        cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((byte[]?)null);
        DistributedCacheEntryOptions? capturedOptions = null;
        cache.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>((_, _, opts, _) => capturedOptions = opts)
            .Returns(Task.CompletedTask);
        var (context, _) = BuildContext(boundArgument: "user@example.com", cache.Object);

        var sut = new ThrottleEmailSendAttribute { WindowSeconds = 30 };
        await sut.OnActionExecutionAsync(context, NextReturning(() => { }));

        cache.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(TimeSpan.FromSeconds(30), capturedOptions!.AbsoluteExpirationRelativeToNow);
    }

    /// <summary>
    /// An action that bounced on model validation must not leave the cooldown window armed -- the
    /// user can retry immediately. The window is still claimed up front (closing the burst race,
    /// see <see cref="OnActionExecutionAsync_ClaimsCooldown_BeforeActionRuns"/>) and then released
    /// again once it's clear the action never reached a real send attempt.
    /// </summary>
    [Fact]
    public async Task OnActionExecutionAsync_ModelStateInvalidAfterAction_ReleasesCooldown()
    {
        var cache = new Mock<IDistributedCache>();
        cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((byte[]?)null);
        cache.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        cache.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var (context, _) = BuildContext(boundArgument: "user@example.com", cache.Object);
        context.ModelState.AddModelError("Nickname", "Nickname is required.");

        await new ThrottleEmailSendAttribute().OnActionExecutionAsync(context, NextReturning(() => { }));

        cache.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        cache.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>An action that threw releases the claimed cooldown window too, for the same reason.</summary>
    [Fact]
    public async Task OnActionExecutionAsync_ActionThrew_ReleasesCooldown()
    {
        var cache = new Mock<IDistributedCache>();
        cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((byte[]?)null);
        cache.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        cache.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var (context, _) = BuildContext(boundArgument: "user@example.com", cache.Object);

        await new ThrottleEmailSendAttribute().OnActionExecutionAsync(
            context, NextReturning(() => { }, exception: new InvalidOperationException("boom")));

        cache.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        cache.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A cache that cannot release the claim (unavailable) must not turn the response into an error — the caller only loses this window's retry.</summary>
    [Fact]
    public async Task OnActionExecutionAsync_ReleaseFails_IsSwallowed()
    {
        var cache = new Mock<IDistributedCache>();
        cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((byte[]?)null);
        cache.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        cache.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("cache down"));
        var (context, _) = BuildContext(boundArgument: "user@example.com", cache.Object);

        var ex = await Record.ExceptionAsync(() => new ThrottleEmailSendAttribute().OnActionExecutionAsync(
            context, NextReturning(() => { }, exception: new InvalidOperationException("boom"))));

        Assert.Null(ex);
        cache.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Regression: an action that finished without reaching a mail send — e.g. a registration that the
    /// service rejected ("nickname already taken") after model binding had succeeded — asks for the claim
    /// to be released via <see cref="ThrottleEmailSendAttribute.ReleaseCooldownClaim"/>, so the caller is
    /// not locked out for a mail that was never sent.
    /// </summary>
    [Fact]
    public async Task OnActionExecutionAsync_ActionAsksForRelease_ReleasesCooldown()
    {
        var cache = new Mock<IDistributedCache>();
        cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((byte[]?)null);
        cache.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        cache.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var (context, _) = BuildContext(boundArgument: "user@example.com", cache.Object);

        await new ThrottleEmailSendAttribute().OnActionExecutionAsync(
            context, NextReturning(() => ThrottleEmailSendAttribute.ReleaseCooldownClaim(context.HttpContext)));

        cache.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        cache.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>An action that does not ask for a release keeps its claim (the mail was really sent / attempted).</summary>
    [Fact]
    public async Task OnActionExecutionAsync_ActionDoesNotAskForRelease_KeepsCooldown()
    {
        var cache = new Mock<IDistributedCache>();
        cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((byte[]?)null);
        cache.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var (context, _) = BuildContext(boundArgument: "user@example.com", cache.Object);

        await new ThrottleEmailSendAttribute().OnActionExecutionAsync(context, NextReturning(() => { }));

        cache.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -- Race fix: claim happens before the action runs -----------------------------

    /// <summary>
    /// Regression: the cooldown must be claimed (<c>SetAsync</c>) before the action runs, not after
    /// it completes. Claiming only after the action (and its possibly-slow mail send) let every
    /// request in a concurrent burst for the same address pass the <c>GetAsync</c> check above
    /// before any of them reached the write, defeating the cooldown for the whole burst.
    /// </summary>
    [Fact]
    public async Task OnActionExecutionAsync_ClaimsCooldown_BeforeActionRuns()
    {
        var callOrder = new List<string>();
        var cache = new Mock<IDistributedCache>();
        cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("get"))
            .ReturnsAsync((byte[]?)null);
        cache.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("set"))
            .Returns(Task.CompletedTask);
        var (context, _) = BuildContext(boundArgument: "user@example.com", cache.Object);

        await new ThrottleEmailSendAttribute().OnActionExecutionAsync(
            context, NextReturning(() => callOrder.Add("action")));

        Assert.Equal(["get", "set", "action"], callOrder);
    }

    /// <summary>
    /// The throttle fails open when the cache is down (a genuine reset must not be blocked) — but that
    /// means the send-rate cap is off, so it is logged as a warning, with the exception.
    /// </summary>
    [Fact]
    public async Task OnActionExecutionAsync_LogsAWarning_WhenTheCacheReadFails()
    {
        var cache = new Mock<IDistributedCache>();
        cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException("cache down"));
        var (context, _) = BuildContext(boundArgument: "user@example.com", cache.Object,
            configureServices: services => services.AddFakeLogging());
        var collector = context.HttpContext.RequestServices.GetFakeLogCollector();

        await new ThrottleEmailSendAttribute().OnActionExecutionAsync(context, NextReturning(() => { }));

        FakeLogRecord record = Assert.Single(collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
        Assert.Contains("send-rate throttle is inactive", record.Message);
        Assert.IsType<TimeoutException>(record.Exception);
        Assert.DoesNotContain("user@example.com", record.Message); // the address is not needed to diagnose the cache
    }

    /// <summary>Failing to release the claim after a bounced request is also logged (the caller silently loses one retry window).</summary>
    [Fact]
    public async Task OnActionExecutionAsync_LogsAWarning_WhenReleasingTheClaimFails()
    {
        var cache = new Mock<IDistributedCache>();
        cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((byte[]?)null);
        cache.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        cache.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException("cache down"));
        var (context, _) = BuildContext(boundArgument: "user@example.com", cache.Object,
            configureServices: services => services.AddFakeLogging());
        FakeLogCollector collector = context.HttpContext.RequestServices.GetFakeLogCollector();
        context.ModelState.AddModelError("Email", "invalid"); // bounced on validation -> claim is released

        await new ThrottleEmailSendAttribute().OnActionExecutionAsync(context, NextReturning(() => { }));

        FakeLogRecord record = Assert.Single(collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
        Assert.Contains("could not be released", record.Message);
        Assert.IsType<TimeoutException>(record.Exception);
    }
}
