using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Misc.Settings;
using Maroik.Core.Domain.Account;
using Maroik.Core.Domain.Localization;
using Maroik.Website.Constants;
using Maroik.Website.Extensions;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace Maroik.Website.Filters;

/// <summary>
/// Global action filter that populates shared ViewBag entries before every action executes:
/// domain name, file-size limit, the active navigation menu item (for sidebar highlighting/breadcrumbs),
/// notification badge counts, current culture, and the return URI. It also resolves the logged-in
/// account, but publishes it on <c>HttpContext.Items</c> (read via the typed
/// <see cref="Extensions.HttpContextAccountExtensions"/>) rather than the dynamic ViewBag.
/// Runs after <see cref="AuthorizationFilter"/>, which has already decided allow/deny for this request and left the navigation menu on <c>HttpContext.Items</c>; this filter publishes it as
/// ViewBag.{Role}Categories/{Role}SubCategories.
/// </summary>
public class ViewBagPopulatorFilter(
    IDashboardService dashboardService,
    ITimeZoneCatalogService timeZoneCatalogService,
    IOptions<ServerSetting> serverSettings,
    TimeProvider timeProvider) : IAsyncActionFilter
{
    /// <summary>Populates shared ViewBag entries before each action executes.</summary>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var logger = context.HttpContext.RequestServices
            .GetRequiredService<ILoggerFactory>().CreateLogger<ViewBagPopulatorFilter>();

        if (context.Controller is not Controller controller)
        {
            await next();
            return;
        }

        // AuthorizationFilter runs before the controller exists, so it can't write to the ViewBag; it
        // leaves the menu it loaded on HttpContext.Items and this is the first point that can publish it.
        if (context.HttpContext.Items.TryGetValue(HttpContextItemKeys.NavigationMenus, out var menusItem)
            && menusItem is NavigationMenus menus)
        {
            controller.ViewBag.AdminCategories = menus.AdminCategories;
            controller.ViewBag.AdminSubCategories = menus.AdminSubCategories;
            controller.ViewBag.UserCategories = menus.UserCategories;
            controller.ViewBag.UserSubCategories = menus.UserSubCategories;
            controller.ViewBag.AnonymousCategories = menus.AnonymousCategories;
            controller.ViewBag.AnonymousSubCategories = menus.AnonymousSubCategories;
        }

        controller.ViewBag.DomainName = serverSettings.Value.DomainName;
        controller.ViewBag.MaxAttachedFileSizeBytes = serverSettings.Value.MaxAttachedFileSizeBytes;

        // AuthorizationFilter (which always runs first) re-loads and re-validates a signed-in session's
        // account from the database and stashes it here — or redirects the request to the login page,
        // so the action never runs. No stash therefore means no signed-in account: the anonymous
        // placeholder below.
        AccountResponse loggedInAccount =
            context.HttpContext.Items.TryGetValue(HttpContextItemKeys.LoggedInAccount, out var stashed)
            && stashed is AccountResponse signedInAccount
                ? signedInAccount
                : new AccountResponse
                {
                    Nickname = "Login",
                    Role = Role.Anonymous,
                    TimeZoneIanaId = "UTC",
                    AvatarImagePath = "/anonymous/images/bg1.jpg"
                };

        // Single source of truth for controllers and views alike, read back via the typed
        // HttpContext.GetLoggedInAccount() — never null from here on (anonymous placeholder at worst).
        context.HttpContext.Items[HttpContextItemKeys.LoggedInAccount] = loggedInAccount;
        controller.ViewBag.TimeZoneOptions = timeZoneCatalogService.GetTimeZoneOptions();
        controller.ViewBag.CopyrightYear = timeProvider.GetUtcNow().UtcDateTime.ConvertTimeByTimeZoneIanaId(loggedInAccount.TimeZoneIanaId ?? "UTC").Year;

        (IEnumerable<CategoryResponse>? roleCategories, IEnumerable<SubCategoryResponse>? roleSubCategories) = loggedInAccount.Role switch
        {
            Role.Admin => ((IEnumerable<CategoryResponse>?)controller.ViewBag.AdminCategories, (IEnumerable<SubCategoryResponse>?)controller.ViewBag.AdminSubCategories),
            Role.User => ((IEnumerable<CategoryResponse>?)controller.ViewBag.UserCategories, (IEnumerable<SubCategoryResponse>?)controller.ViewBag.UserSubCategories),
            _ => ((IEnumerable<CategoryResponse>?)controller.ViewBag.AnonymousCategories, (IEnumerable<SubCategoryResponse>?)controller.ViewBag.AnonymousSubCategories)
        };

        if (roleCategories != null && roleSubCategories != null)
        {
            context.ActionDescriptor.RouteValues.TryGetValue("controller", out var currentControllerName);
            context.ActionDescriptor.RouteValues.TryGetValue("action", out var currentActionName);
            var activeMenuMatch = roleCategories.ResolveActiveMenuItem(roleSubCategories, currentControllerName ?? "", currentActionName ?? "");
            if (activeMenuMatch.IsMatch)
            {
                controller.ViewBag.ActiveCategory = activeMenuMatch.Category;
                controller.ViewBag.ActiveSubCategory = activeMenuMatch.SubCategory;
            }
        }

        int fixedIncomesNoticedCount = 0, fixedExpenditureNoticedCount = 0;
        int fixedIncomesExpiredCount = 0, fixedExpenditureExpiredCount = 0;

        if (loggedInAccount.Role == Role.User)
        {
            try
            {
                var counts = await dashboardService.GetNoticeCountsAsync(
                    loggedInAccount.Email ?? "",
                    serverSettings.Value.NoticeMaturityDateDay,
                    loggedInAccount.TimeZoneIanaId ?? "UTC",
                    context.HttpContext.RequestAborted);

                fixedIncomesNoticedCount = counts.FixedIncomesNoticed;
                fixedExpenditureNoticedCount = counts.FixedExpendituresNoticed;
                fixedIncomesExpiredCount = counts.FixedIncomesExpired;
                fixedExpenditureExpiredCount = counts.FixedExpendituresExpired;
            }
            catch (Exception ex) { logger.LogWarning(ex, "Failed to load notice counts for {Email}", loggedInAccount.Email); }
        }

        controller.ViewBag.FixedIncomesNoticedCount = fixedIncomesNoticedCount;
        controller.ViewBag.FixedExpenditureNoticedCount = fixedExpenditureNoticedCount;
        controller.ViewBag.FixedIncomesExpiredCount = fixedIncomesExpiredCount;
        controller.ViewBag.FixedExpenditureExpiredCount = fixedExpenditureExpiredCount;
        controller.ViewBag.FixedIncomesTotalNoticeCount = fixedIncomesNoticedCount + fixedIncomesExpiredCount;
        controller.ViewBag.FixedExpenditureTotalNoticeCount = fixedExpenditureNoticedCount + fixedExpenditureExpiredCount;
        controller.ViewBag.TotalNoticeCount = fixedIncomesNoticedCount + fixedExpenditureNoticedCount +
                                               fixedIncomesExpiredCount + fixedExpenditureExpiredCount;

        var requestCultureFeature = context.HttpContext.Features.Get<IRequestCultureFeature>();
        controller.ViewBag.CurrentCulture = requestCultureFeature?.RequestCulture.Culture.Name ?? "en-US";

        string requestPath = context.HttpContext.Request.Path.Value ?? "";
        controller.ViewBag.ReturnUri = string.IsNullOrWhiteSpace(requestPath) ? "/" : $"{requestPath}{context.HttpContext.Request.QueryString}";

        await next();
    }
}
