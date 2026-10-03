using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Misc.Settings;
using Maroik.Core.Domain.Account;
using Maroik.Website.Contracts;
using Maroik.Website.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Maroik.Website.Tests.Filters;

/// <summary>
/// Unit tests for <see cref="ViewBagPopulatorFilter"/>.
/// All service dependencies are replaced with Moq mocks.
/// </summary>
public class ViewBagPopulatorFilterTests
{
    /// <summary>Mock <c>IAccountService</c> injected into the system under test.</summary>
    private readonly Mock<IAccountService> _accountService = new();
    /// <summary>Mock <c>IDashboardService</c> injected into the system under test.</summary>
    private readonly Mock<IDashboardService> _dashboardService = new();
    /// <summary>Mock <c>ITimeZoneCatalogService</c> injected into the system under test.</summary>
    private readonly Mock<ITimeZoneCatalogService> _timeZoneCatalogService = new();
    /// <summary>Mock <c>ISessionService</c> injected into the system under test.</summary>
    private readonly Mock<ISessionService> _sessionService = new();
    /// <summary>Settings whose values the filter copies to the ViewBag.</summary>
    private readonly IOptions<ServerSetting> _serverSettings = Options.Create(new ServerSetting
    {
        DomainName = "https://test.maroik.com",
        MaxAttachedFileSizeBytes = 10_485_760,
        NoticeMaturityDateDay = 7
    });

    /// <summary>The clock the filter reads; set to a fixed instant far from the real one.</summary>
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2041, 12, 31, 16, 0, 0, TimeSpan.Zero));

    /// <summary>The filter under test over the mocked dependencies.</summary>
    private ViewBagPopulatorFilter CreateSut() =>
        new(_accountService.Object, _dashboardService.Object, _timeZoneCatalogService.Object, _serverSettings, _sessionService.Object, _timeProvider);

    /// <summary>An action context for a request to <paramref name="path"/>, optionally with a culture and route names.</summary>
    private static (ActionExecutingContext context, Controller controller) BuildContext(
        string path = "/Test/Index",
        string? cultureName = null,
        string? controllerName = null,
        string? actionName = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = sp,
            Request =
            {
                Path = path
            }
        };

        if (cultureName != null)
        {
            var cultureFeature = new Mock<IRequestCultureFeature>();
            cultureFeature.Setup(f => f.RequestCulture)
                .Returns(new RequestCulture(cultureName));
            httpContext.Features.Set(cultureFeature.Object);
        }

        var controller = new FakeController();
        var actionDescriptor = new ControllerActionDescriptor
        {
            RouteValues = new Dictionary<string, string?>
            {
                ["controller"] = controllerName,
                ["action"] = actionName
            }
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext,
            RouteData = new RouteData(),
            ActionDescriptor = actionDescriptor
        };

        var actionContext = new ActionContext(httpContext, new RouteData(), actionDescriptor);
        var executingContext = new ActionExecutingContext(
            actionContext,
            [],
            new Dictionary<string, object?>(),
            controller);

        return (executingContext, controller);
    }

    /// <summary>A next-delegate that does nothing.</summary>
    private static ActionExecutionDelegate EmptyNext() =>
        () => Task.FromResult(new ActionExecutedContext(
            new ActionContext(new DefaultHttpContext(), new RouteData(), new ControllerActionDescriptor()),
            [],
            null!));

    // -- ServerSettings -------------------------------------------------------

    /// <summary>An action on something that is not an MVC <c>Controller</c> (so it has no ViewBag) is passed straight through untouched.</summary>
    [Fact]
    public async Task OnActionExecutionAsync_NonControllerTarget_RunsTheActionAndPopulatesNothing()
    {
        var (template, _) = BuildContext();
        var context = new ActionExecutingContext(template, [], new Dictionary<string, object?>(), new object());
        bool ran = false;

        await CreateSut().OnActionExecutionAsync(context, () =>
        {
            ran = true;
            return EmptyNext()();
        });

        Assert.True(ran);
        _sessionService.Verify(s => s.GetAccount(), Times.Never);
    }

    /// <summary>Verifies that <c>ViewBag.DomainName</c> is populated from server settings.</summary>
    [Fact]
    public async Task OnActionExecutionAsync_SetsDomainName_FromServerSettings()
    {
        _sessionService.Setup(s => s.GetAccount()).Returns((AccountResponse?)null);
        var (context, controller) = BuildContext();

        await CreateSut().OnActionExecutionAsync(context, EmptyNext());

        Assert.Equal("https://test.maroik.com", controller.ViewBag.DomainName);
    }

    /// <summary>Verifies that <c>ViewBag.MaxAttachedFileSizeBytes</c> is populated from server settings.</summary>
    [Fact]
    public async Task OnActionExecutionAsync_SetsMaxAttachedFileSizeBytes_FromServerSettings()
    {
        _sessionService.Setup(s => s.GetAccount()).Returns((AccountResponse?)null);
        var (context, controller) = BuildContext();

        await CreateSut().OnActionExecutionAsync(context, EmptyNext());

        Assert.Equal(10_485_760L, controller.ViewBag.MaxAttachedFileSizeBytes);
    }

    // -- Anonymous session (no session account) --------------------------------

    /// <summary>Verifies that <c>ViewBag.LoggedInAccount</c> defaults to anonymous when no session exists.</summary>
    [Fact]
    public async Task OnActionExecutionAsync_SetsAnonymousAccount_WhenNoSession()
    {
        _sessionService.Setup(s => s.GetAccount()).Returns((AccountResponse?)null);
        var (context, controller) = BuildContext();

        await CreateSut().OnActionExecutionAsync(context, EmptyNext());

        AccountResponse account = controller.ViewBag.LoggedInAccount;
        Assert.Equal(Role.Anonymous, account.Role);
    }

    /// <summary>Verifies that notice counts are zero when no session exists.</summary>
    [Fact]
    public async Task OnActionExecutionAsync_SetsZeroNoticeCounts_WhenNoSession()
    {
        _sessionService.Setup(s => s.GetAccount()).Returns((AccountResponse?)null);
        var (context, controller) = BuildContext();

        await CreateSut().OnActionExecutionAsync(context, EmptyNext());

        Assert.Equal(0, controller.ViewBag.FixedIncomesNoticedCount);
        Assert.Equal(0, controller.ViewBag.FixedExpenditureNoticedCount);
        Assert.Equal(0, controller.ViewBag.FixedIncomesExpiredCount);
        Assert.Equal(0, controller.ViewBag.FixedExpenditureExpiredCount);
    }

    // -- Logged-in User session ------------------------------------------------

    /// <summary>Verifies that <c>ViewBag.LoggedInAccount</c> is populated from the account service when session exists.</summary>
    [Fact]
    public async Task OnActionExecutionAsync_SetsLoggedInAccount_WhenSessionExists()
    {
        var sessionAccount = new AccountResponse { Email = "user@test.com", Role = Role.User };
        var dbAccount = new AccountResponse { Email = "user@test.com", Nickname = "TestUser", Role = Role.User };

        _sessionService.Setup(s => s.GetAccount()).Returns(sessionAccount);
        _accountService
            .Setup(a => a.GetAccountByEmailAsync("user@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(dbAccount);
        _dashboardService
            .Setup(d => d.GetNoticeCountsAsync("user@test.com", 7, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationDto { FixedIncomesNoticed = 2, FixedExpendituresNoticed = 3, FixedIncomesExpired = 1, FixedExpendituresExpired = 4 });

        var (context, controller) = BuildContext();

        await CreateSut().OnActionExecutionAsync(context, EmptyNext());

        AccountResponse account = controller.ViewBag.LoggedInAccount;
        Assert.Equal("TestUser", account.Nickname);
        Assert.Equal(Role.User, account.Role);
    }

    /// <summary>
    /// The footer's copyright year is the injected clock's year in the viewer's time zone: 2041-12-31 16:00 UTC
    /// is already 2042 in Asia/Seoul (UTC+9).
    /// </summary>
    [Fact]
    public async Task OnActionExecutionAsync_SetsCopyrightYear_FromTheClockInTheViewersTimeZone()
    {
        var account = new AccountResponse { Email = "user@test.com", Role = Role.User, TimeZoneIanaId = "Asia/Seoul" };
        _sessionService.Setup(s => s.GetAccount()).Returns(account);
        _dashboardService
            .Setup(d => d.GetNoticeCountsAsync("user@test.com", 7, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationDto());
        var (context, controller) = BuildContext();
        context.HttpContext.Items[Constants.HttpContextItemKeys.LoggedInAccount] = account;

        await CreateSut().OnActionExecutionAsync(context, EmptyNext());

        Assert.Equal(2042, (int)controller.ViewBag.CopyrightYear);
    }

    /// <summary>
    /// When AuthorizationFilter has already stashed the re-validated account on HttpContext.Items,
    /// ViewBagPopulatorFilter must reuse it and NOT issue a second GetAccountByEmailAsync query.
    /// </summary>
    [Fact]
    public async Task OnActionExecutionAsync_ReusesStashedAccount_WithoutQueryingAgain()
    {
        var sessionAccount = new AccountResponse { Email = "user@test.com", Role = Role.User };
        var stashedAccount = new AccountResponse { Email = "user@test.com", Nickname = "FromAuthFilter", Role = Role.User };

        _sessionService.Setup(s => s.GetAccount()).Returns(sessionAccount);
        _dashboardService
            .Setup(d => d.GetNoticeCountsAsync("user@test.com", 7, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationDto());

        var (context, controller) = BuildContext();
        context.HttpContext.Items[Constants.HttpContextItemKeys.LoggedInAccount] = stashedAccount;

        await CreateSut().OnActionExecutionAsync(context, EmptyNext());

        AccountResponse account = controller.ViewBag.LoggedInAccount;
        Assert.Equal("FromAuthFilter", account.Nickname);
        _accountService.Verify(
            a => a.GetAccountByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Verifies that notice counts are populated from the dashboard service for a logged-in user.</summary>
    [Fact]
    public async Task OnActionExecutionAsync_SetsNoticeCounts_WhenUserIsLoggedIn()
    {
        var sessionAccount = new AccountResponse { Email = "user@test.com", Role = Role.User };
        var dbAccount = new AccountResponse { Email = "user@test.com", Role = Role.User };

        _sessionService.Setup(s => s.GetAccount()).Returns(sessionAccount);
        _accountService
            .Setup(a => a.GetAccountByEmailAsync("user@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(dbAccount);
        _dashboardService
            .Setup(d => d.GetNoticeCountsAsync("user@test.com", 7, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationDto { FixedIncomesNoticed = 2, FixedExpendituresNoticed = 3, FixedIncomesExpired = 1, FixedExpendituresExpired = 4 });

        var (context, controller) = BuildContext();

        await CreateSut().OnActionExecutionAsync(context, EmptyNext());

        Assert.Equal(2, controller.ViewBag.FixedIncomesNoticedCount);
        Assert.Equal(3, controller.ViewBag.FixedExpenditureNoticedCount);
        Assert.Equal(1, controller.ViewBag.FixedIncomesExpiredCount);
        Assert.Equal(4, controller.ViewBag.FixedExpenditureExpiredCount);
    }

    // -- Culture & ReturnUri --------------------------------------------------

    /// <summary>Verifies that <c>ViewBag.CurrentCulture</c> falls back to "en-US" when no culture feature is set.</summary>
    [Fact]
    public async Task OnActionExecutionAsync_DefaultsCultureToEnUs_WhenNoCultureFeature()
    {
        _sessionService.Setup(s => s.GetAccount()).Returns((AccountResponse?)null);
        var (context, controller) = BuildContext();

        await CreateSut().OnActionExecutionAsync(context, EmptyNext());

        Assert.Equal("en-US", controller.ViewBag.CurrentCulture);
    }

    /// <summary>Verifies that <c>ViewBag.CurrentCulture</c> reflects the request culture feature.</summary>
    [Fact]
    public async Task OnActionExecutionAsync_SetsCulture_FromRequestCultureFeature()
    {
        _sessionService.Setup(s => s.GetAccount()).Returns((AccountResponse?)null);
        var (context, controller) = BuildContext(cultureName: "ko-KR");

        await CreateSut().OnActionExecutionAsync(context, EmptyNext());

        Assert.Equal("ko-KR", controller.ViewBag.CurrentCulture);
    }

    /// <summary>Verifies that <c>ViewBag.ReturnUri</c> is populated from the request path.</summary>
    [Fact]
    public async Task OnActionExecutionAsync_SetsReturnUri_FromRequestPath()
    {
        _sessionService.Setup(s => s.GetAccount()).Returns((AccountResponse?)null);
        var (context, controller) = BuildContext(path: "/Forum/FreeForum");

        await CreateSut().OnActionExecutionAsync(context, EmptyNext());

        Assert.Equal("/Forum/FreeForum", controller.ViewBag.ReturnUri);
    }

    // -- TimeZone options & notice total ---------------------------------------

    /// <summary>Verifies that <c>ViewBag.TimeZoneOptions</c> is populated from the time zone catalog service.</summary>
    [Fact]
    public async Task OnActionExecutionAsync_SetsTimeZoneOptions_FromCatalogService()
    {
        var options = new List<TimeZoneOptionDto> { new() { IanaId = "Asia/Seoul", DisplayName = "Seoul" } };
        _timeZoneCatalogService.Setup(t => t.GetTimeZoneOptions()).Returns(options);
        _sessionService.Setup(s => s.GetAccount()).Returns((AccountResponse?)null);
        var (context, controller) = BuildContext();

        await CreateSut().OnActionExecutionAsync(context, EmptyNext());

        Assert.Same(options, controller.ViewBag.TimeZoneOptions);
    }

    /// <summary>Verifies that <c>ViewBag.TotalNoticeCount</c> is the sum of all four individual notice counts.</summary>
    [Fact]
    public async Task OnActionExecutionAsync_SetsTotalNoticeCount_AsSumOfIndividualCounts()
    {
        var sessionAccount = new AccountResponse { Email = "user@test.com", Role = Role.User };
        var dbAccount = new AccountResponse { Email = "user@test.com", Role = Role.User };

        _sessionService.Setup(s => s.GetAccount()).Returns(sessionAccount);
        _accountService
            .Setup(a => a.GetAccountByEmailAsync("user@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(dbAccount);
        _dashboardService
            .Setup(d => d.GetNoticeCountsAsync("user@test.com", 7, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationDto { FixedIncomesNoticed = 2, FixedExpendituresNoticed = 3, FixedIncomesExpired = 1, FixedExpendituresExpired = 4 });

        var (context, controller) = BuildContext();

        await CreateSut().OnActionExecutionAsync(context, EmptyNext());

        Assert.Equal(10, controller.ViewBag.TotalNoticeCount);
    }

    /// <summary>Verifies that <c>ViewBag.FixedIncomesTotalNoticeCount</c>/<c>FixedExpenditureTotalNoticeCount</c>
    /// are each the sum of that type's noticed and expired counts, precomputed for the navbar badge so the view
    /// doesn't need to add the two counts together itself.</summary>
    [Fact]
    public async Task OnActionExecutionAsync_SetsPerTypeTotalNoticeCounts_AsSumOfNoticedAndExpired()
    {
        var sessionAccount = new AccountResponse { Email = "user@test.com", Role = Role.User };
        var dbAccount = new AccountResponse { Email = "user@test.com", Role = Role.User };

        _sessionService.Setup(s => s.GetAccount()).Returns(sessionAccount);
        _accountService
            .Setup(a => a.GetAccountByEmailAsync("user@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(dbAccount);
        _dashboardService
            .Setup(d => d.GetNoticeCountsAsync("user@test.com", 7, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationDto { FixedIncomesNoticed = 2, FixedExpendituresNoticed = 3, FixedIncomesExpired = 1, FixedExpendituresExpired = 4 });

        var (context, controller) = BuildContext();

        await CreateSut().OnActionExecutionAsync(context, EmptyNext());

        Assert.Equal(3, controller.ViewBag.FixedIncomesTotalNoticeCount);
        Assert.Equal(7, controller.ViewBag.FixedExpenditureTotalNoticeCount);
    }

    // -- Active menu resolution -------------------------------------------------

    /// <summary>
    /// Verifies the hand-off from AuthorizationFilter: the menu it left on HttpContext.Items are published
    /// as ViewBag.{Role}Categories/{Role}SubCategories (an authorization filter has no controller to
    /// write to), and the active menu item is then resolved from it.
    /// </summary>
    [Fact]
    public async Task OnActionExecutionAsync_PublishesNavigationMenusFromHttpContextItems_ToViewBag()
    {
        var sessionAccount = new AccountResponse { Email = "user@test.com", Role = Role.User };
        var dbAccount = new AccountResponse { Email = "user@test.com", Role = Role.User };
        _sessionService.Setup(s => s.GetAccount()).Returns(sessionAccount);
        _accountService.Setup(a => a.GetAccountByEmailAsync("user@test.com", It.IsAny<CancellationToken>())).ReturnsAsync(dbAccount);

        var (context, controller) = BuildContext(controllerName: "AccountBook", actionName: "Income");
        var userCategories = new List<CategoryResponse>
        {
            new() { Id = 1, DisplayName = "AccountBook", Controller = "AccountBook", Action = "Income" }
        };
        var adminCategories = new List<CategoryResponse> { new() { Id = 2, DisplayName = "Management", Controller = "Management", Action = "Account" } };
        context.HttpContext.Items[Constants.HttpContextItemKeys.NavigationMenus] = new NavigationMenus(
            adminCategories, [], userCategories, [], [], []);

        await CreateSut().OnActionExecutionAsync(context, EmptyNext());

        Assert.Same(userCategories, controller.ViewBag.UserCategories);
        Assert.Same(adminCategories, controller.ViewBag.AdminCategories);
        Assert.NotNull(controller.ViewBag.AnonymousCategories);
        CategoryResponse activeCategory = controller.ViewBag.ActiveCategory;
        Assert.Equal(1, activeCategory.Id);
    }

    /// <summary>Verifies that ViewBag.ActiveCategory/ActiveSubCategory are resolved from the role-appropriate
    /// categories that AuthorizationFilter (running earlier in the pipeline) already published to ViewBag.</summary>
    [Fact]
    public async Task OnActionExecutionAsync_SetsActiveCategory_FromRolePublishedCategories()
    {
        var sessionAccount = new AccountResponse { Email = "user@test.com", Role = Role.User };
        var dbAccount = new AccountResponse { Email = "user@test.com", Role = Role.User };
        _sessionService.Setup(s => s.GetAccount()).Returns(sessionAccount);
        _accountService.Setup(a => a.GetAccountByEmailAsync("user@test.com", It.IsAny<CancellationToken>())).ReturnsAsync(dbAccount);

        var (context, controller) = BuildContext(controllerName: "AccountBook", actionName: "Income");
        // Simulate AuthorizationFilter, which runs earlier in the pipeline, having already published these.
        controller.ViewBag.UserCategories = new List<CategoryResponse>
        {
            new() { Id = 1, DisplayName = "AccountBook", Controller = "AccountBook", Action = "Income" }
        };
        controller.ViewBag.UserSubCategories = new List<SubCategoryResponse>();

        await CreateSut().OnActionExecutionAsync(context, EmptyNext());

        CategoryResponse activeCategory = controller.ViewBag.ActiveCategory;
        Assert.Equal(1, activeCategory.Id);
        Assert.Null(controller.ViewBag.ActiveSubCategory);
    }

    /// <summary>Verifies that ViewBag.ActiveCategory is left unset when the current route matches no published category.</summary>
    [Fact]
    public async Task OnActionExecutionAsync_LeavesActiveCategoryUnset_WhenNoRouteMatch()
    {
        var sessionAccount = new AccountResponse { Email = "user@test.com", Role = Role.User };
        var dbAccount = new AccountResponse { Email = "user@test.com", Role = Role.User };
        _sessionService.Setup(s => s.GetAccount()).Returns(sessionAccount);
        _accountService.Setup(a => a.GetAccountByEmailAsync("user@test.com", It.IsAny<CancellationToken>())).ReturnsAsync(dbAccount);

        var (context, controller) = BuildContext(controllerName: "Forum", actionName: "FreeForum");
        controller.ViewBag.UserCategories = new List<CategoryResponse>
        {
            new() { Id = 1, DisplayName = "AccountBook", Controller = "AccountBook", Action = "Income" }
        };
        controller.ViewBag.UserSubCategories = new List<SubCategoryResponse>();

        await CreateSut().OnActionExecutionAsync(context, EmptyNext());

        Assert.Null(controller.ViewBag.ActiveCategory);
    }

    /// <summary>Verifies that a missing role-category ViewBag entry (e.g. AuthorizationFilter did not run) does not throw.</summary>
    [Fact]
    public async Task OnActionExecutionAsync_DoesNotThrow_WhenRoleCategoriesNotPublished()
    {
        _sessionService.Setup(s => s.GetAccount()).Returns((AccountResponse?)null);
        var (context, controller) = BuildContext(controllerName: "Dashboard", actionName: "AnonymousIndex");
        // Deliberately do not set controller.ViewBag.AnonymousCategories/AnonymousSubCategories.

        var ex = await Record.ExceptionAsync(() => CreateSut().OnActionExecutionAsync(context, EmptyNext()));

        Assert.Null(ex);
        Assert.Null(controller.ViewBag.ActiveCategory);
    }

    // -- Resilience -----------------------------------------------------------

    /// <summary>Verifies that a faulting account service does not throw — falls back to anonymous account.</summary>
    [Fact]
    public async Task OnActionExecutionAsync_FallsBackToAnonymous_WhenAccountServiceThrows()
    {
        var sessionAccount = new AccountResponse { Email = "user@test.com", Role = Role.User };
        _sessionService.Setup(s => s.GetAccount()).Returns(sessionAccount);
        _accountService
            .Setup(a => a.GetAccountByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        var (context, controller) = BuildContext();

        var ex = await Record.ExceptionAsync(() => CreateSut().OnActionExecutionAsync(context, EmptyNext()));

        Assert.Null(ex);
        AccountResponse account = controller.ViewBag.LoggedInAccount;
        Assert.Equal(Role.Anonymous, account.Role);
    }

    /// <summary>Verifies that a faulting dashboard service does not throw — notice counts stay zero.</summary>
    [Fact]
    public async Task OnActionExecutionAsync_KeepsZeroNoticeCounts_WhenDashboardServiceThrows()
    {
        var sessionAccount = new AccountResponse { Email = "user@test.com", Role = Role.User };
        var dbAccount = new AccountResponse { Email = "user@test.com", Role = Role.User };

        _sessionService.Setup(s => s.GetAccount()).Returns(sessionAccount);
        _accountService
            .Setup(a => a.GetAccountByEmailAsync("user@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(dbAccount);
        _dashboardService
            .Setup(d => d.GetNoticeCountsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        var (context, controller) = BuildContext();

        var ex = await Record.ExceptionAsync(() => CreateSut().OnActionExecutionAsync(context, EmptyNext()));

        Assert.Null(ex);
        Assert.Equal(0, controller.ViewBag.FixedIncomesNoticedCount);
    }
}

/// <summary>Minimal <see cref="Controller"/> subclass used as the action target in filter tests.</summary>
internal class FakeController : Controller
{
    /// <summary>No-op action used only as a target for the filter pipeline under test.</summary>
    public IActionResult Index() => Ok();
}
