using System.Reflection;
using System.Text.Json;
using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Account;
using Maroik.Website.Attributes;
using Maroik.Website.Constants;
using Maroik.Website.Contracts;
using Maroik.Website.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Distributed;
// ReSharper disable InvalidXmlDocComment

namespace Maroik.Website.Filters;

/// <summary>
/// Global <b>authorization</b> filter: decides, before model binding and before the action runs,
/// whether the current request may proceed, given the visitor's session role and the seeded
/// navigation menu (a GET must resolve to a menu item for the role; a POST must carry a matching
/// <see cref="RequiredHttpPostAccessAttribute"/> and target a controller present in the role's menu).
/// Denied requests are redirected instead of executing. Being an authorization filter (not an action
/// filter) means a denied request never reaches form / file model binding.
/// <para>
/// It also loads the category / sub-category menu for all three roles (Admin, User, Anonymous) —
/// cached in the distributed cache with a 10-minute sliding expiration — because that data is the
/// input to the decision. The loaded menu is stashed on <c>HttpContext.Items</c> as
/// <see cref="NavigationMenus"/>; <see cref="ViewBagPopulatorFilter"/> publishes it to the ViewBag
/// (there is no controller instance yet at this stage, so it cannot be done here).
/// </para>
/// <para>
/// Ordering: runs at the default filter order (0), i.e. before the global
/// <c>AutoValidateAntiforgeryTokenAttribute</c> (order 1000), so an unauthorized request is rejected
/// before its form body is parsed for an antiforgery token.
/// </para>
/// The controller/action → role map used for POST authorization is derived once from the compiled
/// controllers (see <see cref="_postActionRoles"/>) rather than re-reflected on every request.
/// </summary>
public class AuthorizationFilter(IMenuService menuService, IDistributedCache cache,
    ISessionService sessionService, IAccountService accountService,
    ILogger<AuthorizationFilter> logger) : IAsyncAuthorizationFilter
{
    private const string AdminCategoriesCacheKey = NavigationCacheKeys.AdminCategories;
    private const string AdminSubCategoriesCacheKey = NavigationCacheKeys.AdminSubCategories;
    private const string UserCategoriesCacheKey = NavigationCacheKeys.UserCategories;
    private const string UserSubCategoriesCacheKey = NavigationCacheKeys.UserSubCategories;
    private const string AnonymousCategoriesCacheKey = NavigationCacheKeys.AnonymousCategories;
    private const string AnonymousSubCategoriesCacheKey = NavigationCacheKeys.AnonymousSubCategories;

    private static readonly IReadOnlyList<Type> _cachedControllerTypes =
    [
        .. Assembly.GetExecutingAssembly().GetTypes()
            .Where(type => typeof(Controller).IsAssignableFrom(type))
    ];

    /// <summary>
    /// (controller, action) POST endpoint → the set of role strings its
    /// <see cref="RequiredHttpPostAccessAttribute"/> attributes grant. Only actions that also carry
    /// <c>[HttpPost]</c> and <c>[ValidateAntiForgeryToken]</c> are included. The compiled set of
    /// actions and their attributes never changes at runtime, so this is built once instead of
    /// re-scanning every controller's methods and custom attributes on every POST request.
    /// </summary>
    private static readonly IReadOnlyDictionary<(string Controller, string Action), IReadOnlySet<string>> _postActionRoles =
        BuildPostActionRoleMap();

    /// <summary>
    /// The fixed set of <c>AccountController</c> POST endpoints an unauthenticated visitor may POST
    /// to without a navigation-menu match — login, logout, registration / resend, email
    /// confirmation (link + registration password), and the forgot / reset-password flow. Spelled out explicitly (not reflected off the controller) so
    /// this anonymous surface mirrors the hardcoded GET allow-list in <see cref="OnAuthorizationAsync"/>:
    /// adding a new POST action to <c>AccountController</c> does NOT silently widen anonymous access
    /// — it stays denied (redirect to Dashboard) until it is added here on purpose. Every entry must
    /// still carry <c>[HttpPost]</c> + <c>[ValidateAntiForgeryToken]</c>, enforced by
    /// <c>WebsiteArchitectureTests.PostActions_MustCarry_AntiforgeryAndRoleAttributes</c>.
    /// </summary>
 #pragma warning disable CA1859
    private static readonly IReadOnlySet<(string Controller, string Action)> _anonymousAccountPostActions =
 #pragma warning restore CA1859
        new HashSet<(string Controller, string Action)>
        {
            ("Account", nameof(Controllers.AccountController.Login)),
            ("Account", nameof(Controllers.AccountController.Logout)),
            ("Account", nameof(Controllers.AccountController.Register)),
            ("Account", nameof(Controllers.AccountController.ConfirmEmail)),
            ("Account", nameof(Controllers.AccountController.ForgotPassword)),
            ("Account", nameof(Controllers.AccountController.ResetPassword)),
        };

    private static string ControllerName(Type controllerType) =>
        controllerType.Name.EndsWith("Controller", StringComparison.Ordinal)
            ? controllerType.Name[..^"Controller".Length]
            : controllerType.Name;

    private static bool HasPostGuards(MethodInfo method) =>
        method.IsPublic
        && !method.IsDefined(typeof(NonActionAttribute))
        && method.IsDefined(typeof(HttpPostAttribute))
        && method.IsDefined(typeof(ValidateAntiForgeryTokenAttribute));

 #pragma warning disable CA1859
    private static IReadOnlyDictionary<(string, string), IReadOnlySet<string>> BuildPostActionRoleMap()
 #pragma warning restore CA1859
    {
        var map = new Dictionary<(string, string), IReadOnlySet<string>>();
        foreach (Type controller in _cachedControllerTypes)
        {
            foreach (MethodInfo method in controller.GetMethods())
            {
                if (!HasPostGuards(method) || !method.IsDefined(typeof(RequiredHttpPostAccessAttribute)))
                    continue;

 #pragma warning disable CA1859
                IReadOnlySet<string> roles = method.GetCustomAttributes<RequiredHttpPostAccessAttribute>()
 #pragma warning restore CA1859
                    .Select(attr => attr.Role)
                    .Where(role => !string.IsNullOrEmpty(role))
                    .ToHashSet(StringComparer.Ordinal);

                if (roles.Count > 0)
                    map[(ControllerName(controller), method.Name)] = roles;
            }
        }
        return map;
    }

    /// <summary>Loads (or restores from cache) the navigation menu data, then allows or denies the request before model binding.</summary>
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var results = await Task.WhenAll(
            cache.GetAsync(AdminCategoriesCacheKey),
            cache.GetAsync(AdminSubCategoriesCacheKey),
            cache.GetAsync(UserCategoriesCacheKey),
            cache.GetAsync(UserSubCategoriesCacheKey),
            cache.GetAsync(AnonymousCategoriesCacheKey),
            cache.GetAsync(AnonymousSubCategoriesCacheKey));

        List<CategoryResponse> adminCategories = [];
        List<SubCategoryResponse> adminSubCategories = [];
        List<CategoryResponse> userCategories = [];
        List<SubCategoryResponse> userSubCategories = [];
        List<CategoryResponse> anonymousCategories = [];
        List<SubCategoryResponse> anonymousSubCategories = [];

        bool menusLoaded = false;
        if (results[0] != null && results[1] != null && results[2] != null &&
            results[3] != null && results[4] != null && results[5] != null)
        {
            try
            {
                adminCategories = JsonSerializer.Deserialize<List<CategoryResponse>>(results[0]!)!;
                adminSubCategories = JsonSerializer.Deserialize<List<SubCategoryResponse>>(results[1]!)!;
                userCategories = JsonSerializer.Deserialize<List<CategoryResponse>>(results[2]!)!;
                userSubCategories = JsonSerializer.Deserialize<List<SubCategoryResponse>>(results[3]!)!;
                anonymousCategories = JsonSerializer.Deserialize<List<CategoryResponse>>(results[4]!)!;
                anonymousSubCategories = JsonSerializer.Deserialize<List<SubCategoryResponse>>(results[5]!)!;
                menusLoaded = true;
            }
            catch (Exception e)
            {
                // A poisoned or shape-changed cache entry must not turn into a 500 on every request
                // for the whole 10-minute TTL — leave menusLoaded false so the block below rebuilds
                // from the database, which also overwrites the bad entry. (SessionService.GetAccount
                // guards its own deserialization the same way.)
                logger.LogWarning(e, "Navigation menu cache entry could not be read; rebuilding it from the database");
            }
        }

        if (!menusLoaded)
        {
            var categories = (await menuService.GetAllCategoriesAsync()).ToList();
            var subCategories = (await menuService.GetAllSubCategoriesAsync()).ToList();

            var entryOptions = new DistributedCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(10));

            adminCategories = [.. categories.Where(c => c.Role == Role.Admin)];
            adminSubCategories = [.. subCategories.Where(s => s.Role == Role.Admin)];
            userCategories = [.. categories.Where(c => c.Role == Role.User)];
            userSubCategories = [.. subCategories.Where(s => s.Role == Role.User)];
            anonymousCategories = [.. categories.Where(c => c.Role == Role.Anonymous)];
            anonymousSubCategories = [.. subCategories.Where(s => s.Role == Role.Anonymous)];

            await Task.WhenAll(
                cache.SetAsync(AdminCategoriesCacheKey, JsonSerializer.SerializeToUtf8Bytes(adminCategories),
                    entryOptions),
                cache.SetAsync(AdminSubCategoriesCacheKey, JsonSerializer.SerializeToUtf8Bytes(adminSubCategories),
                    entryOptions),
                cache.SetAsync(UserCategoriesCacheKey, JsonSerializer.SerializeToUtf8Bytes(userCategories),
                    entryOptions),
                cache.SetAsync(UserSubCategoriesCacheKey, JsonSerializer.SerializeToUtf8Bytes(userSubCategories),
                    entryOptions),
                cache.SetAsync(AnonymousCategoriesCacheKey, JsonSerializer.SerializeToUtf8Bytes(anonymousCategories),
                    entryOptions),
                cache.SetAsync(AnonymousSubCategoriesCacheKey,
                    JsonSerializer.SerializeToUtf8Bytes(anonymousSubCategories),
                    entryOptions));
        }

        // Hand the menu to ViewBagPopulatorFilter: an authorization filter runs before the controller
        // instance exists, so there is no ViewBag to write to here.
        context.HttpContext.Items[HttpContextItemKeys.NavigationMenus] = new NavigationMenus(
            adminCategories, adminSubCategories,
            userCategories, userSubCategories,
            anonymousCategories, anonymousSubCategories);

        var currentControllerName = context.ActionDescriptor.RouteValues["controller"] ?? ""; // Controller name before execution
        var currentActionName = context.ActionDescriptor.RouteValues["action"] ?? ""; // Action name before execution
        var currentRequestMethod = context.HttpContext.Request.Method; // GET HTTP METHOD
        var sessionAccount = sessionService.GetAccount(); // Check if account session exists after login

        AccountResponse? loggedInAccount = null;
        if (sessionAccount != null)
        {
            loggedInAccount = await accountService.GetAccountByEmailAsync(sessionAccount.Email ?? "", context.HttpContext.RequestAborted); // Re-check account state from DB so that Locked/Deleted/Role changes take effect on the next request, not only at login

            // SecurityStamp mismatch means the password changed (self-service, forgot-password
            // reset, or admin override) after this session was minted — the session was captured
            // by value at login, so it never sees the new stamp on its own.
            if (loggedInAccount == null || loggedInAccount.Deleted || loggedInAccount.Locked ||
                loggedInAccount.SecurityStamp != sessionAccount.SecurityStamp)
            {
                string reason = loggedInAccount == null ? "account no longer exists"
                    : loggedInAccount.Deleted ? "account is deleted"
                    : loggedInAccount.Locked ? "account is locked"
                    : "security stamp changed";
                logger.LogWarning("Session invalidated for {Email}: {Reason}", sessionAccount.Email, reason);
                sessionService.RemoveAccount(); // Invalidate this session immediately
                context.Result = new RedirectResult("/Account/Login");
                return;
            }

            // Hand the freshly re-validated account to ViewBagPopulatorFilter (runs next in the
            // pipeline) so it doesn't re-query the same row on every authenticated request.
            context.HttpContext.Items[HttpContextItemKeys.LoggedInAccount] = loggedInAccount;
        }

        var isAccountSessionExist = loggedInAccount != null;

        // An admin-forced password change locks the account to the profile page (where the
        // change-password form lives) and logout, until UpdateProfilePassword clears the flag.
        if (isAccountSessionExist && loggedInAccount!.MustChangePassword)
        {
            bool isAllowedWhileForced =
                (currentControllerName == "Management" && currentActionName is "Profile" or "UpdateProfilePassword") ||
                (currentControllerName == "Account" && currentActionName == "Logout") ||
                (currentControllerName == "Exception" && currentActionName == "Error");

            if (!isAllowedWhileForced)
            {
                context.Result = string.Equals(
                    currentRequestMethod,
                    "GET",
                    StringComparison.OrdinalIgnoreCase)
                    ? new RedirectResult("/Management/Profile")
                    : new RedirectResult("/Dashboard/AnonymousIndex");

            }

            return; // allowed: no Result set
        }

        // UseExceptionHandler re-executes /Exception/Error with the ORIGINAL request's method, so a
        // failed POST/PUT/DELETE arrives here as that method. The error page is inert (it changes no
        // state and shows only a request id), so it is reachable for every method and every role;
        // without this, the POST branches below would redirect the user away from the error page.
        if (currentControllerName == "Exception" && currentActionName == "Error")
            return; // allowed: no Result set

        switch (currentRequestMethod.ToUpperInvariant())
        {
            // GET method (block page access only!!)
            // If not logged in
            case "GET" when !isAccountSessionExist:
                {
                    switch (currentControllerName)
                    {
                        // Allow exception resource access (Error)
                        case "Exception" when currentActionName == "Error":
                        // If login page
                        case "Account" when currentActionName == "Login":
                        // If registration page
                        case "Account" when currentActionName == "Register":
                        // If email confirmation page
                        case "Account" when currentActionName == "ConfirmEmail":
                        // If consent form page
                        case "Account" when currentActionName == "ConsentForm":
                        // If forgot password page
                        case "Account" when currentActionName == "ForgotPassword":
                        // If password reset page
                        case "Account" when currentActionName == "ResetPassword":
                            return; // allowed: no Result set
                    }

                    var anonymousMatch = anonymousCategories.ResolveActiveMenuItem(
                        anonymousSubCategories, currentControllerName, currentActionName);

                    if (anonymousMatch.IsMatch)
                    {
                        return; // allowed: no Result set
                    }

                    context.Result = new RedirectResult("/Dashboard/AnonymousIndex");
                    return;
                }
            case "GET" when isAccountSessionExist:
                {
                    switch (currentControllerName)
                    {
                        // Allow exception resource access (Error)
                        case "Exception" when currentActionName == "Error":
                        // Initial screen
                        case "Dashboard" when currentActionName == "AnonymousIndex":
                            return; // allowed: no Result set
                    }

                    switch (loggedInAccount!.Role)
                    {
                        // Admin and User share identical resolution logic — only which
                        // category/sub-category list is searched differs by role.
                        case Role.Admin:
                        case Role.User:
                            {
                                bool isAdmin = loggedInAccount.Role == Role.Admin;
                                var match = (isAdmin ? adminCategories : userCategories).ResolveActiveMenuItem(
                                    isAdmin ? adminSubCategories : userSubCategories, currentControllerName, currentActionName);

                                if (match.IsMatch)
                                {
                                    return; // allowed: no Result set
                                }

                                Deny();
                                return;
                            }
                        // Role is a plain string, not an enum — an account somehow persisted with
                        // neither Admin nor User must still get an explicit result instead of
                        // falling through with next() never called and context.Result never set,
                        // which would silently short-circuit the pipeline with an empty response.
                        default:
                            Deny();
                            return;
                    }
                }
            // POST method — not logged in
            case "POST" when !isAccountSessionExist:
                {
                    (string, string) endpoint = (currentControllerName, currentActionName);

                    // AccountController's own POST actions (login, register, forgot/reset password …)
                    // are always reachable to an anonymous visitor.
                    if (_anonymousAccountPostActions.Contains(endpoint))
                    {
                        return; // allowed: no Result set
                    }

                    // Otherwise the action must be explicitly marked for the Anonymous role AND live
                    // on a controller that appears in the anonymous navigation menu.
                    if (_postActionRoles.TryGetValue(endpoint, out IReadOnlySet<string>? anonRoles)
                        && anonRoles.Contains(Role.Anonymous)
                        && anonymousCategories.Any(category => category.Controller == currentControllerName))
                    {
                        return; // allowed: no Result set
                    }

                    Deny();
                    return;
                }
            // POST method — logged in
            case "POST" when isAccountSessionExist:
                {
                    // Logout is a POST (antiforgery-protected) endpoint on AccountController, which is
                    // not part of any role's navigation menu — allow it explicitly for any signed-in
                    // account regardless of role.
                    if (currentControllerName == "Account" && currentActionName == "Logout")
                    {
                        return; // allowed: no Result set
                    }

                    switch (loggedInAccount!.Role)
                    {
                        // Admin and User share identical resolution logic — only which category
                        // list is searched for a controller with a matching RequiredHttpPostAccess
                        // role differs.
                        case Role.Admin:
                        case Role.User:
                            {
                                var categories = loggedInAccount.Role == Role.Admin ? adminCategories : userCategories;

                                // The action must carry [RequiredHttpPostAccess(Role = <this role>)]
                                // AND its controller must appear in this role's navigation menu.
                                if (_postActionRoles.TryGetValue((currentControllerName, currentActionName), out IReadOnlySet<string>? allowedRoles)
                                    && allowedRoles.Contains(loggedInAccount.Role)
                                    && categories.Any(category => category.Controller == currentControllerName))
                                {
                                    return; // allowed: no Result set
                                }

                                Deny();
                                return;
                            }
                        // Role is a plain string, not an enum — an account somehow persisted with
                        // neither Admin nor User must still get an explicit result instead of
                        // falling through with next() never called and context.Result never set,
                        // which would silently short-circuit the pipeline with an empty response.
                        default:
                            Deny();
                            return;
                    }
                }
            // (Reject all methods like PATCH / DELETE etc.)
            default:
                Deny();
                return;
        }

        // A refused request is a security event: record who asked for what, then send them to the start page.
        void Deny()
        {
            logger.LogWarning("Access denied: {Method} {Controller}/{Action} for {Account}",
                currentRequestMethod, currentControllerName, currentActionName, loggedInAccount?.Email ?? "anonymous");
            context.Result = new RedirectResult("/Dashboard/AnonymousIndex");
        }
    }
}
