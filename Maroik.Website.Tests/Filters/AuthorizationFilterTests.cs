using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Account;
using Maroik.Website.Constants;
using Maroik.Website.Contracts;
using Maroik.Website.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;

namespace Maroik.Website.Tests.Filters;

/// <summary>
/// Unit tests for <see cref="AuthorizationFilter"/>, focused purely on the GET/POST allow/deny
/// gating driven by <c>MenuNavigationMatcher.ResolveActiveMenuItem</c>. The filter is an authorization
/// filter (it runs before model binding and before a controller instance exists): it only decides
/// whether the request may proceed — a denial is a <c>Result</c> on the context, an allow leaves it
/// null — and leaves the loaded menu on <c>HttpContext.Items</c> for <c>ViewBagPopulatorFilter</c>. All service dependencies are replaced with Moq mocks; the distributed
/// cache always misses so every test exercises the <see cref="IMenuService"/> fallback path.
/// </summary>
public class AuthorizationFilterTests
{
    private readonly Mock<IMenuService> _menuService = new();
    private readonly Mock<IDistributedCache> _cache = new();
    private readonly Mock<ISessionService> _sessionService = new();
    private readonly Mock<IAccountService> _accountService = new();
    private readonly FakeLogger<AuthorizationFilter> _logger = new();

    /// <summary>Initializes a new instance of AuthorizationFilterTests.</summary>
    public AuthorizationFilterTests()
    {
        _cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);
    }

    private AuthorizationFilter CreateSut() =>
        new(_menuService.Object, _cache.Object, _sessionService.Object, _accountService.Object, _logger);

    private static CategoryResponse MakeCategory(long id, string role, string controller, string? action, long order = 0) => new()
    {
        Id = id,
        DisplayName = $"Category{id}",
        Role = role,
        Controller = controller,
        Action = action,
        Order = order
    };

    private static SubCategoryResponse MakeSubCategory(long id, long categoryId, string role, string action, long order = 0) => new()
    {
        Id = id,
        CategoryId = categoryId,
        DisplayName = $"SubCategory{id}",
        Role = role,
        Action = action,
        Order = order
    };

    private void SetupMenu(IEnumerable<CategoryResponse> categories, IEnumerable<SubCategoryResponse> subCategories)
    {
        _menuService.Setup(m => m.GetAllCategoriesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(categories);
        _menuService.Setup(m => m.GetAllSubCategoriesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(subCategories);
    }

    private static AuthorizationFilterContext BuildContext(
        string controllerName, string actionName, string httpMethod = "GET")
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = sp,
            Request =
            {
                Method = httpMethod
            }
        };

        var actionDescriptor = new ControllerActionDescriptor
        {
            RouteValues = new Dictionary<string, string?>
            {
                ["controller"] = controllerName,
                ["action"] = actionName
            }
        };

        // An authorization filter runs before the controller instance exists, so the context carries
        // only the request and the matched action — no controller, no ViewBag.
        return new AuthorizationFilterContext(
            new ActionContext(httpContext, new RouteData(), actionDescriptor),
            []);
    }

    // -- GET, not logged in -------------------------------------------------------

    /// <summary>On action execution async get not logged in bypass route always allowed.</summary>
    [Fact]
    public async Task OnAuthorizationAsync_Get_NotLoggedIn_BypassRoute_AlwaysAllowed()
    {
        _sessionService.Setup(s => s.GetAccount()).Returns((AccountResponse?)null);
        SetupMenu([], []);
        var context = BuildContext("Account", "Login");

        await CreateSut().OnAuthorizationAsync(context);

        Assert.Null(context.Result);
        Assert.Null(context.Result);
    }

    /// <summary>On action execution async get not logged in single category match allows.</summary>
    [Fact]
    public async Task OnAuthorizationAsync_Get_NotLoggedIn_SingleCategoryMatch_Allows()
    {
        _sessionService.Setup(s => s.GetAccount()).Returns((AccountResponse?)null);
        var category = MakeCategory(1, Role.Anonymous, "Dashboard", "AnonymousIndex");
        SetupMenu([category], []);
        var context = BuildContext("Dashboard", "AnonymousIndex");

        await CreateSut().OnAuthorizationAsync(context);

        Assert.Null(context.Result);
        Assert.Null(context.Result);
    }

    /// <summary>On action execution async get not logged in sub category match allows.</summary>
    [Fact]
    public async Task OnAuthorizationAsync_Get_NotLoggedIn_SubCategoryMatch_Allows()
    {
        _sessionService.Setup(s => s.GetAccount()).Returns((AccountResponse?)null);
        var category = MakeCategory(1, Role.Anonymous, "Notice", null); // parent category (no direct Action = has children)
        var subCategory = MakeSubCategory(10, categoryId: 1, role: Role.Anonymous, action: "FixedIncome");
        SetupMenu([category], [subCategory]);
        var context = BuildContext("Notice", "FixedIncome");

        await CreateSut().OnAuthorizationAsync(context);

        Assert.Null(context.Result);
    }

    /// <summary>On action execution async get not logged in no match redirects.</summary>
    [Fact]
    public async Task OnAuthorizationAsync_Get_NotLoggedIn_NoMatch_Redirects()
    {
        _sessionService.Setup(s => s.GetAccount()).Returns((AccountResponse?)null);
        var category = MakeCategory(1, Role.Anonymous, "Dashboard", "AnonymousIndex");
        SetupMenu([category], []);
        var context = BuildContext("Forum", "FreeForum");

        await CreateSut().OnAuthorizationAsync(context);

        Assert.NotNull(context.Result);
        var redirect = Assert.IsType<RedirectResult>(context.Result);
        Assert.Equal("/Dashboard/AnonymousIndex", redirect.Url);
    }

    // -- GET, Admin logged in ------------------------------------------------------

    /// <summary>On action execution async get admin logged in single category match allows.</summary>
    [Fact]
    public async Task OnAuthorizationAsync_Get_AdminLoggedIn_SingleCategoryMatch_Allows()
    {
        var admin = new AccountResponse { Email = "admin@test.com", Nickname = "Admin", Role = Role.Admin };
        _sessionService.Setup(s => s.GetAccount()).Returns(admin);
        _accountService.Setup(a => a.GetAccountByEmailAsync("admin@test.com", It.IsAny<CancellationToken>())).ReturnsAsync(admin);
        var category = MakeCategory(1, Role.Admin, "Management", "Account");
        SetupMenu([category], []);
        var context = BuildContext("Management", "Account");

        await CreateSut().OnAuthorizationAsync(context);

        Assert.Null(context.Result);
    }

    /// <summary>On action execution async get admin logged in sub category match allows.</summary>
    [Fact]
    public async Task OnAuthorizationAsync_Get_AdminLoggedIn_SubCategoryMatch_Allows()
    {
        var admin = new AccountResponse { Email = "admin@test.com", Nickname = "Admin", Role = Role.Admin };
        _sessionService.Setup(s => s.GetAccount()).Returns(admin);
        _accountService.Setup(a => a.GetAccountByEmailAsync("admin@test.com", It.IsAny<CancellationToken>())).ReturnsAsync(admin);
        var category = MakeCategory(1, Role.Admin, "Notice", null); // parent category (no direct Action = has children)
        var subCategory = MakeSubCategory(10, categoryId: 1, role: Role.Admin, action: "FixedIncome");
        SetupMenu([category], [subCategory]);
        var context = BuildContext("Notice", "FixedIncome");

        await CreateSut().OnAuthorizationAsync(context);

        Assert.Null(context.Result);
    }

    /// <summary>On action execution async get admin logged in no match redirects.</summary>
    [Fact]
    public async Task OnAuthorizationAsync_Get_AdminLoggedIn_NoMatch_Redirects()
    {
        var admin = new AccountResponse { Email = "admin@test.com", Nickname = "Admin", Role = Role.Admin };
        _sessionService.Setup(s => s.GetAccount()).Returns(admin);
        _accountService.Setup(a => a.GetAccountByEmailAsync("admin@test.com", It.IsAny<CancellationToken>())).ReturnsAsync(admin);
        var category = MakeCategory(1, Role.Admin, "Management", "Account");
        SetupMenu([category], []);
        var context = BuildContext("Unregistered", "Action");

        await CreateSut().OnAuthorizationAsync(context);

        Assert.NotNull(context.Result);
        var redirect = Assert.IsType<RedirectResult>(context.Result);
        Assert.Equal("/Dashboard/AnonymousIndex", redirect.Url);
    }

    // -- GET, User logged in --------------------------------------------------------

    /// <summary>On action execution async get user logged in single category match allows.</summary>
    [Fact]
    public async Task OnAuthorizationAsync_Get_UserLoggedIn_SingleCategoryMatch_Allows()
    {
        var user = new AccountResponse { Email = "user@test.com", Nickname = "User", Role = Role.User };
        _sessionService.Setup(s => s.GetAccount()).Returns(user);
        _accountService.Setup(a => a.GetAccountByEmailAsync("user@test.com", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var category = MakeCategory(1, Role.User, "AccountBook", "Income");
        SetupMenu([category], []);
        var context = BuildContext("AccountBook", "Income");

        await CreateSut().OnAuthorizationAsync(context);

        Assert.Null(context.Result);
    }

    /// <summary>On action execution async get user logged in no match redirects.</summary>
    [Fact]
    public async Task OnAuthorizationAsync_Get_UserLoggedIn_NoMatch_Redirects()
    {
        var user = new AccountResponse { Email = "user@test.com", Nickname = "User", Role = Role.User };
        _sessionService.Setup(s => s.GetAccount()).Returns(user);
        _accountService.Setup(a => a.GetAccountByEmailAsync("user@test.com", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var category = MakeCategory(1, Role.User, "AccountBook", "Income");
        SetupMenu([category], []);
        var context = BuildContext("Management", "Account"); // Admin-only route

        await CreateSut().OnAuthorizationAsync(context);

        var redirect = Assert.IsType<RedirectResult>(context.Result);
        Assert.Equal("/Dashboard/AnonymousIndex", redirect.Url);
    }

    /// <summary>
    /// Regression test: Role is a plain string, not an enum, so an account persisted with neither
    /// Admin nor User must still produce an explicit result. Before the role switch had a default
    /// case, this fell through with next() never called and context.Result never set, silently
    /// short-circuiting the pipeline with an empty response instead of redirecting.
    /// </summary>
    [Fact]
    public async Task OnAuthorizationAsync_Get_UnrecognizedRole_Redirects()
    {
        var account = new AccountResponse { Email = "weird@test.com", Nickname = "Weird", Role = "SuperAdmin" };
        _sessionService.Setup(s => s.GetAccount()).Returns(account);
        _accountService.Setup(a => a.GetAccountByEmailAsync("weird@test.com", It.IsAny<CancellationToken>())).ReturnsAsync(account);
        SetupMenu([], []);
        var context = BuildContext("AccountBook", "Income");

        await CreateSut().OnAuthorizationAsync(context);

        var redirect = Assert.IsType<RedirectResult>(context.Result);
        Assert.Equal("/Dashboard/AnonymousIndex", redirect.Url);
    }

    // -- Exception/Error, reached through UseExceptionHandler with the original method ------

    /// <summary>
    /// Regression test: UseExceptionHandler re-executes /Exception/Error with the original request's
    /// method, so a failed POST arrives as a POST. An anonymous visitor must still get the error page
    /// rather than being redirected away by the POST branch.
    /// </summary>
    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task OnAuthorizationAsync_ExceptionError_NonGetMethod_NotLoggedIn_Allows(string method)
    {
        _sessionService.Setup(s => s.GetAccount()).Returns((AccountResponse?)null);
        SetupMenu([], []);
        var context = BuildContext("Exception", "Error", method);

        await CreateSut().OnAuthorizationAsync(context);

        Assert.Null(context.Result);
    }

    /// <summary>A logged-in user (any role) whose POST threw also reaches the error page.</summary>
    [Theory]
    [InlineData(Role.User)]
    [InlineData(Role.Admin)]
    public async Task OnAuthorizationAsync_ExceptionError_Post_LoggedIn_Allows(string role)
    {
        var account = new AccountResponse { Email = "who@test.com", Nickname = "Who", Role = role };
        _sessionService.Setup(s => s.GetAccount()).Returns(account);
        _accountService.Setup(a => a.GetAccountByEmailAsync("who@test.com", It.IsAny<CancellationToken>())).ReturnsAsync(account);
        SetupMenu([], []);
        var context = BuildContext("Exception", "Error", "POST");

        await CreateSut().OnAuthorizationAsync(context);

        Assert.Null(context.Result);
    }

    /// <summary>The allowance is for Exception/Error only: another Exception action (AccessDenied) is still gated on POST.</summary>
    [Fact]
    public async Task OnAuthorizationAsync_ExceptionAccessDenied_Post_NotLoggedIn_StillRedirects()
    {
        _sessionService.Setup(s => s.GetAccount()).Returns((AccountResponse?)null);
        SetupMenu([], []);
        var context = BuildContext("Exception", "AccessDenied", "POST");

        await CreateSut().OnAuthorizationAsync(context);

        var redirect = Assert.IsType<RedirectResult>(context.Result);
        Assert.Equal("/Dashboard/AnonymousIndex", redirect.Url);
    }

    /// <summary>A revoked / deleted session is still sent to log in even on the error page (session validity comes first).</summary>
    [Fact]
    public async Task OnAuthorizationAsync_ExceptionError_Post_DeletedAccount_StillRedirectsToLogin()
    {
        var sessionAccount = new AccountResponse { Email = "gone@test.com", Nickname = "Gone", Role = Role.User };
        var dbAccount = new AccountResponse { Email = "gone@test.com", Nickname = "Gone", Role = Role.User, Deleted = true };
        _sessionService.Setup(s => s.GetAccount()).Returns(sessionAccount);
        _accountService.Setup(a => a.GetAccountByEmailAsync("gone@test.com", It.IsAny<CancellationToken>())).ReturnsAsync(dbAccount);
        SetupMenu([], []);
        var context = BuildContext("Exception", "Error", "POST");

        await CreateSut().OnAuthorizationAsync(context);

        var redirect = Assert.IsType<RedirectResult>(context.Result);
        Assert.Equal("/Account/Login", redirect.Url);
    }

    // -- Session validity (unrelated to menu resolution, still gated by this filter) --

    /// <summary>On action execution async logged in account deleted in db redirects to log in and clears session.</summary>
    [Fact]
    public async Task OnAuthorizationAsync_LoggedInAccountDeletedInDb_RedirectsToLoginAndClearsSession()
    {
        var sessionAccount = new AccountResponse { Email = "gone@test.com", Nickname = "Gone", Role = Role.User };
        var dbAccount = new AccountResponse { Email = "gone@test.com", Nickname = "Gone", Role = Role.User, Deleted = true };
        _sessionService.Setup(s => s.GetAccount()).Returns(sessionAccount);
        _accountService.Setup(a => a.GetAccountByEmailAsync("gone@test.com", It.IsAny<CancellationToken>())).ReturnsAsync(dbAccount);
        SetupMenu([], []);
        var context = BuildContext("Dashboard", "UserIndex");

        await CreateSut().OnAuthorizationAsync(context);

        Assert.NotNull(context.Result);
        var redirect = Assert.IsType<RedirectResult>(context.Result);
        Assert.Equal("/Account/Login", redirect.Url);
        _sessionService.Verify(s => s.RemoveAccount(), Times.Once);
    }

    /// <summary>A stale security stamp (password changed since this session was minted) redirects to log in and clears the session.</summary>
    [Fact]
    public async Task OnAuthorizationAsync_SecurityStampMismatch_RedirectsToLoginAndClearsSession()
    {
        var sessionAccount = new AccountResponse { Email = "user@test.com", Nickname = "User", Role = Role.User, SecurityStamp = "old-stamp" };
        var dbAccount = new AccountResponse { Email = "user@test.com", Nickname = "User", Role = Role.User, SecurityStamp = "new-stamp" };
        _sessionService.Setup(s => s.GetAccount()).Returns(sessionAccount);
        _accountService.Setup(a => a.GetAccountByEmailAsync("user@test.com", It.IsAny<CancellationToken>())).ReturnsAsync(dbAccount);
        SetupMenu([], []);
        var context = BuildContext("Dashboard", "UserIndex");

        await CreateSut().OnAuthorizationAsync(context);

        Assert.NotNull(context.Result);
        var redirect = Assert.IsType<RedirectResult>(context.Result);
        Assert.Equal("/Account/Login", redirect.Url);
        _sessionService.Verify(s => s.RemoveAccount(), Times.Once);
    }

    // -- MustChangePassword forced flow --------------------------------------------

    /// <summary>A pending forced password change redirects any other GET page to the profile page.</summary>
    [Fact]
    public async Task OnAuthorizationAsync_MustChangePassword_OtherGet_RedirectsToProfile()
    {
        var account = new AccountResponse { Email = "user@test.com", Nickname = "User", Role = Role.User, MustChangePassword = true };
        _sessionService.Setup(s => s.GetAccount()).Returns(account);
        _accountService.Setup(a => a.GetAccountByEmailAsync("user@test.com", It.IsAny<CancellationToken>())).ReturnsAsync(account);
        var category = MakeCategory(1, Role.User, "AccountBook", "Income");
        SetupMenu([category], []);
        var context = BuildContext("AccountBook", "Income");

        await CreateSut().OnAuthorizationAsync(context);

        Assert.NotNull(context.Result);
        var redirect = Assert.IsType<RedirectResult>(context.Result);
        Assert.Equal("/Management/Profile", redirect.Url);
    }

    /// <summary>A pending forced password change blocks any other POST (redirected, not logged out).</summary>
    [Fact]
    public async Task OnAuthorizationAsync_MustChangePassword_OtherPost_Blocks()
    {
        var account = new AccountResponse { Email = "user@test.com", Nickname = "User", Role = Role.User, MustChangePassword = true };
        _sessionService.Setup(s => s.GetAccount()).Returns(account);
        _accountService.Setup(a => a.GetAccountByEmailAsync("user@test.com", It.IsAny<CancellationToken>())).ReturnsAsync(account);
        SetupMenu([], []);
        var context = BuildContext("AccountBook", "CreateIncome", "POST");

        await CreateSut().OnAuthorizationAsync(context);

        Assert.NotNull(context.Result);
        var redirect = Assert.IsType<RedirectResult>(context.Result);
        Assert.Equal("/Dashboard/AnonymousIndex", redirect.Url);
    }

    /// <summary>A pending forced password change still allows the profile page itself.</summary>
    [Fact]
    public async Task OnAuthorizationAsync_MustChangePassword_ProfileGet_Allows()
    {
        var account = new AccountResponse { Email = "user@test.com", Nickname = "User", Role = Role.User, MustChangePassword = true };
        _sessionService.Setup(s => s.GetAccount()).Returns(account);
        _accountService.Setup(a => a.GetAccountByEmailAsync("user@test.com", It.IsAny<CancellationToken>())).ReturnsAsync(account);
        SetupMenu([], []);
        var context = BuildContext("Management", "Profile");

        await CreateSut().OnAuthorizationAsync(context);

        Assert.Null(context.Result);
        Assert.Null(context.Result);
    }

    /// <summary>A pending forced password change still allows submitting the change-password form.</summary>
    [Fact]
    public async Task OnAuthorizationAsync_MustChangePassword_UpdateProfilePasswordPost_Allows()
    {
        var account = new AccountResponse { Email = "user@test.com", Nickname = "User", Role = Role.User, MustChangePassword = true };
        _sessionService.Setup(s => s.GetAccount()).Returns(account);
        _accountService.Setup(a => a.GetAccountByEmailAsync("user@test.com", It.IsAny<CancellationToken>())).ReturnsAsync(account);
        SetupMenu([], []);
        var context = BuildContext("Management", "UpdateProfilePassword", "POST");

        await CreateSut().OnAuthorizationAsync(context);

        Assert.Null(context.Result);
        Assert.Null(context.Result);
    }

    /// <summary>A pending forced password change still allows logging out.</summary>
    [Fact]
    public async Task OnAuthorizationAsync_MustChangePassword_LogoutGet_Allows()
    {
        var account = new AccountResponse { Email = "user@test.com", Nickname = "User", Role = Role.User, MustChangePassword = true };
        _sessionService.Setup(s => s.GetAccount()).Returns(account);
        _accountService.Setup(a => a.GetAccountByEmailAsync("user@test.com", It.IsAny<CancellationToken>())).ReturnsAsync(account);
        SetupMenu([], []);
        var context = BuildContext("Account", "Logout");

        await CreateSut().OnAuthorizationAsync(context);

        Assert.Null(context.Result);
        Assert.Null(context.Result);
    }
    // -- Filter kind / hand-off to ViewBagPopulatorFilter -------------------------

    /// <summary>
    /// Regression test: the gate must be an authorization filter, not an action filter — action
    /// filters run after model binding, so a denied request would still have had its form / file
    /// upload bound first.
    /// </summary>
    [Fact]
    public void AuthorizationFilter_IsAnAuthorizationFilter_NotAnActionFilter()
    {
        Assert.True(typeof(IAsyncAuthorizationFilter).IsAssignableFrom(typeof(AuthorizationFilter)));
        Assert.False(typeof(IActionFilter).IsAssignableFrom(typeof(AuthorizationFilter)));
        Assert.False(typeof(IAsyncActionFilter).IsAssignableFrom(typeof(AuthorizationFilter)));
    }

    /// <summary>The loaded menu, split by role, is left on HttpContext.Items for ViewBagPopulatorFilter to publish.</summary>
    [Fact]
    public async Task OnAuthorizationAsync_StashesLoadedMenuOnHttpContextItems_SplitByRole()
    {
        _sessionService.Setup(s => s.GetAccount()).Returns((AccountResponse?)null);
        var anonymous = MakeCategory(1, Role.Anonymous, "Dashboard", "AnonymousIndex");
        var user = MakeCategory(2, Role.User, "AccountBook", "Income");
        var admin = MakeCategory(3, Role.Admin, "Management", "Account");
        var userSub = MakeSubCategory(20, categoryId: 2, role: Role.User, action: "Expenditure");
        SetupMenu([anonymous, user, admin], [userSub]);
        var context = BuildContext("Dashboard", "AnonymousIndex");

        await CreateSut().OnAuthorizationAsync(context);

        var menus = Assert.IsType<NavigationMenus>(context.HttpContext.Items[HttpContextItemKeys.NavigationMenus]);
        Assert.Equal([1L], menus.AnonymousCategories.Select(c => c.Id));
        Assert.Equal([2L], menus.UserCategories.Select(c => c.Id));
        Assert.Equal([3L], menus.AdminCategories.Select(c => c.Id));
        Assert.Equal([20L], menus.UserSubCategories.Select(c => c.Id));
        Assert.Empty(menus.AdminSubCategories);
    }

    /// <summary>A denied request still leaves the menu on Items (the decision needed it), and carries a Result.</summary>
    [Fact]
    public async Task OnAuthorizationAsync_DeniedRequest_SetsResult_AndStillStashesMenu()
    {
        _sessionService.Setup(s => s.GetAccount()).Returns((AccountResponse?)null);
        SetupMenu([MakeCategory(1, Role.Anonymous, "Dashboard", "AnonymousIndex")], []);
        var context = BuildContext("Management", "Account");

        await CreateSut().OnAuthorizationAsync(context);

        Assert.IsType<RedirectResult>(context.Result);
        Assert.IsType<NavigationMenus>(context.HttpContext.Items[HttpContextItemKeys.NavigationMenus]);
    }

    private void SignIn(string role, string email = "gate@test.com")
    {
        var account = new AccountResponse { Email = email, Nickname = "Gate", Role = role };
        _sessionService.Setup(s => s.GetAccount()).Returns(account);
        _accountService.Setup(a => a.GetAccountByEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(account);
    }

    /// <summary>Logout is a POST on the (menu-less) Account controller; any signed-in account may use it whatever its role.</summary>
    [Theory]
    [InlineData(Role.User)]
    [InlineData(Role.Admin)]
    public async Task OnAuthorizationAsync_AccountLogoutPost_LoggedIn_Allows(string role)
    {
        SignIn(role);
        SetupMenu([], []);
        var context = BuildContext("Account", "Logout", "POST");

        await CreateSut().OnAuthorizationAsync(context);

        Assert.Null(context.Result);
    }

    /// <summary>
    /// Activating an account is an anonymous POST: the confirmation form (mailed link + registration
    /// password) must reach <c>AccountController.ConfirmEmail</c> without a session.
    /// </summary>
    [Fact]
    public async Task OnAuthorizationAsync_AccountConfirmEmailPost_NotLoggedIn_Allows()
    {
        _sessionService.Setup(s => s.GetAccount()).Returns((AccountResponse?)null);
        SetupMenu([], []);
        var context = BuildContext("Account", "ConfirmEmail", "POST");

        await CreateSut().OnAuthorizationAsync(context);

        Assert.Null(context.Result);
    }

    /// <summary>A signed-in account has nothing to confirm; the confirmation POST stays anonymous-only, like the GET link.</summary>
    [Fact]
    public async Task OnAuthorizationAsync_AccountConfirmEmailPost_LoggedIn_Denies()
    {
        SignIn(Role.User);
        SetupMenu([], []);
        var context = BuildContext("Account", "ConfirmEmail", "POST");

        await CreateSut().OnAuthorizationAsync(context);

        var redirect = Assert.IsType<RedirectResult>(context.Result);
        Assert.Equal("/Dashboard/AnonymousIndex", redirect.Url);
    }

    /// <summary>An account persisted with neither role (a stale or hand-edited row) gets an explicit redirect on POST, not an empty response.</summary>
    [Fact]
    public async Task OnAuthorizationAsync_Post_LoggedInWithAnUnknownRole_RedirectsToTheAnonymousDashboard()
    {
        SignIn("Ghost");
        SetupMenu([], []);
        var context = BuildContext("AccountBook", "CreateIncome", "POST");

        await CreateSut().OnAuthorizationAsync(context);

        Assert.Equal("/Dashboard/AnonymousIndex", Assert.IsType<RedirectResult>(context.Result).Url);
    }

    /// <summary>Any method other than GET / POST (PATCH, DELETE, ...) is rejected outright.</summary>
    [Theory]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    [InlineData("PUT")]
    public async Task OnAuthorizationAsync_OtherHttpMethods_AreRedirected(string method)
    {
        SignIn(Role.Admin);
        SetupMenu([], []);
        var context = BuildContext("Dashboard", "AdminIndex", method);

        await CreateSut().OnAuthorizationAsync(context);

        Assert.Equal("/Dashboard/AnonymousIndex", Assert.IsType<RedirectResult>(context.Result).Url);
    }

    /// <summary>A poisoned cache entry does not become a 500: the menu is rebuilt from the database and the request is decided as usual.</summary>
    [Fact]
    public async Task OnAuthorizationAsync_APoisonedMenuCacheEntry_FallsBackToTheDatabase()
    {
        _cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([.. "this is not json"u8]);
        _sessionService.Setup(s => s.GetAccount()).Returns((AccountResponse?)null);
        SetupMenu([], []);
        var context = BuildContext("Account", "Login");

        await CreateSut().OnAuthorizationAsync(context);

        Assert.Null(context.Result);
        _menuService.Verify(m => m.GetAllCategoriesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // -- Logging ------------------------------------------------------------------

    private FakeLogRecord OnlyWarning(string containing) =>
        Assert.Single(_logger.Collector.GetSnapshot(),
            r => r.Level == LogLevel.Warning && r.Message.Contains(containing, StringComparison.Ordinal));

    /// <summary>Verifies that an unreadable navigation-menu cache entry logs a Warning with the exception attached.</summary>
    [Fact]
    public async Task OnAuthorizationAsync_LogsAWarningWithTheException_WhenTheMenuCacheEntryIsPoisoned()
    {
        _cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([.. "this is not json"u8]);
        _sessionService.Setup(s => s.GetAccount()).Returns((AccountResponse?)null);
        SetupMenu([], []);

        await CreateSut().OnAuthorizationAsync(BuildContext("Account", "Login"));

        FakeLogRecord record = OnlyWarning("Navigation menu cache entry could not be read");
        Assert.NotNull(record.Exception);
    }

    /// <summary>Verifies that a session whose account was deleted is invalidated with a Warning naming the email and the reason.</summary>
    [Fact]
    public async Task OnAuthorizationAsync_LogsTheSessionInvalidation_WhenTheAccountWasDeleted()
    {
        var sessionAccount = new AccountResponse { Email = "gone@test.com", Nickname = "Gone", Role = Role.User };
        _sessionService.Setup(s => s.GetAccount()).Returns(sessionAccount);
        _accountService.Setup(a => a.GetAccountByEmailAsync("gone@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountResponse { Email = "gone@test.com", Nickname = "Gone", Role = Role.User, Deleted = true });
        SetupMenu([], []);

        await CreateSut().OnAuthorizationAsync(BuildContext("Dashboard", "UserIndex"));

        Assert.Contains("gone@test.com", OnlyWarning("Session invalidated").Message);
        Assert.Contains("account is deleted", OnlyWarning("Session invalidated").Message);
    }

    /// <summary>Verifies that a session whose security stamp changed is invalidated with a Warning that names the email but not the stamp.</summary>
    [Fact]
    public async Task OnAuthorizationAsync_LogsTheSessionInvalidation_WhenTheSecurityStampChanged()
    {
        _sessionService.Setup(s => s.GetAccount()).Returns(new AccountResponse { Email = "user@test.com", Nickname = "User", Role = Role.User, SecurityStamp = "old" });
        _accountService.Setup(a => a.GetAccountByEmailAsync("user@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountResponse { Email = "user@test.com", Nickname = "User", Role = Role.User, SecurityStamp = "new" });
        SetupMenu([], []);

        await CreateSut().OnAuthorizationAsync(BuildContext("Dashboard", "UserIndex"));

        FakeLogRecord record = OnlyWarning("Session invalidated");
        Assert.Contains("user@test.com", record.Message);
        Assert.Contains("security stamp changed", record.Message);
        Assert.DoesNotContain("old", record.Message.Replace("Session invalidated", ""));
    }

    /// <summary>Verifies that a signed-in account requesting a page outside its menu logs an access-denied Warning with the method, route and email.</summary>
    [Fact]
    public async Task OnAuthorizationAsync_LogsAnAccessDeniedWarning_WhenALoggedInAccountRequestsAPageOutsideItsMenu()
    {
        var admin = new AccountResponse { Email = "admin@test.com", Nickname = "Admin", Role = Role.Admin };
        _sessionService.Setup(s => s.GetAccount()).Returns(admin);
        _accountService.Setup(a => a.GetAccountByEmailAsync("admin@test.com", It.IsAny<CancellationToken>())).ReturnsAsync(admin);
        SetupMenu([MakeCategory(1, Role.Admin, "Management", "Account")], []);

        await CreateSut().OnAuthorizationAsync(BuildContext("Unregistered", "Action"));

        FakeLogRecord record = OnlyWarning("Access denied");
        Assert.Contains("GET Unregistered/Action", record.Message);
        Assert.Contains("admin@test.com", record.Message);
    }

    /// <summary>Verifies that a signed-in account posting to a disallowed action logs an access-denied Warning with the method, route and email.</summary>
    [Fact]
    public async Task OnAuthorizationAsync_LogsAnAccessDeniedWarning_WhenALoggedInPostIsNotPermitted()
    {
        var user = new AccountResponse { Email = "user@test.com", Nickname = "User", Role = Role.User };
        _sessionService.Setup(s => s.GetAccount()).Returns(user);
        _accountService.Setup(a => a.GetAccountByEmailAsync("user@test.com", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        SetupMenu([], []);

        await CreateSut().OnAuthorizationAsync(BuildContext("Management", "DeleteAccount", "POST"));

        FakeLogRecord record = OnlyWarning("Access denied");
        Assert.Contains("POST Management/DeleteAccount", record.Message);
        Assert.Contains("user@test.com", record.Message);
    }

    /// <summary>Verifies that an anonymous post to a disallowed action logs an access-denied Warning marked as anonymous.</summary>
    [Fact]
    public async Task OnAuthorizationAsync_LogsAnAccessDeniedWarning_WhenAnAnonymousPostIsNotPermitted()
    {
        _sessionService.Setup(s => s.GetAccount()).Returns((AccountResponse?)null);
        SetupMenu([], []);

        await CreateSut().OnAuthorizationAsync(BuildContext("Management", "DeleteAccount", "POST"));

        FakeLogRecord record = OnlyWarning("Access denied");
        Assert.Contains("POST Management/DeleteAccount", record.Message);
        Assert.Contains("anonymous", record.Message);
    }

    /// <summary>Verifies that an anonymous visitor requesting an unlisted page is not logged as a Warning or above.</summary>
    [Fact]
    public async Task OnAuthorizationAsync_LogsNothing_WhenAnAnonymousVisitorIsSentFromAnUnlistedPage()
    {
        _sessionService.Setup(s => s.GetAccount()).Returns((AccountResponse?)null);
        SetupMenu([], []);

        await CreateSut().OnAuthorizationAsync(BuildContext("Unregistered", "Action"));

        Assert.DoesNotContain(_logger.Collector.GetSnapshot(), r => r.Level >= LogLevel.Warning);
    }
}
