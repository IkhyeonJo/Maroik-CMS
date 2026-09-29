using Maroik.Core.Domain.Account;
using Maroik.Core.PostgreSQL.Data;
using Maroik.Core.PostgreSQL.Models;
using Maroik.Website.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NonFactors.Mvc.Grid;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// Razor views whose optional parts only render for particular data or modes: the dashboard's settings card (needs assets in
/// more than one currency and a span of years), the top bar's notification entries (need due fixed schedules), the
/// access-denied page (the authorization filter always redirects away from it, so it is rendered with that filter removed) and
/// the MVC Grid table template in the Excel filter mode no page of the site uses (its header mode is not rendered here: the library's template throws for a header-mode column that has no applied filter, and no site grid uses that mode).
/// </summary>
[Collection("Website Integration")]
public class ViewRenderingTests(MaroikWebApplicationFactory factory)
{
    /// <summary>Client for the shared test host; redirects are not followed so they can be asserted.</summary>
    private readonly HttpClient _client = factory.CreateTestClient(followRedirects: false);

    /// <summary>Runs <paramref name="seed"/> against a fresh context and saves.</summary>
    private void Seed(Action<ApplicationDbContext> seed)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        seed(db);
        db.SaveChanges();
    }

    /// <summary>Inserts a 1000-unit asset of <paramref name="email"/> in <paramref name="unit"/>.</summary>
    private void SeedAsset(string email, string productName, string unit) => Seed(db => db.Assets.Add(new Asset
    {
        ProductName = productName, AccountEmail = email, Item = "FreeDepositAndWithdrawal", MonetaryUnit = unit,
        Amount = 1000m, Note = "", Deleted = false, Created = DateTime.UtcNow, Updated = DateTime.UtcNow,
    }));

    /// <summary>GETs <paramref name="url"/> with the session cookie, asserts 200, and returns the HTML.</summary>
    private async Task<string> GetHtmlAsync(string url, AuthenticatedSession session)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Cookie", session.CookieHeader);
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// A user with assets in two currencies and older income gets the dashboard's settings card: a year list reaching back to the
    /// oldest record (this year selected), the twelve months, and the currencies — the account's default one selected.
    /// </summary>
    [Fact]
    public async Task Dashboard_WithAssetsInTwoCurrencies_ShowsTheSettingsCard()
    {
        const string email = "view-dash@test.com";
        var session = await AuthenticatedSessionHelper.LoginAsync(factory, _client, email, "UserPassword1!", Role.User, TestContext.Current.CancellationToken);
        SeedAsset(email, "View-KRW-Asset", "KRW");
        SeedAsset(email, "View-USD-Asset", "USD");
        Seed(db => db.Incomes.Add(new Income
        {
            AccountEmail = email, DepositMyAssetProductName = "View-KRW-Asset", MainClass = "RegularIncome", SubClass = "LaborIncome", Content = "old",
            Amount = 10m, Note = "", Created = DateTime.UtcNow.AddYears(-2), Updated = DateTime.UtcNow.AddYears(-2),
        }));

        string html = await GetHtmlAsync("/Dashboard/UserIndex", session);

        Assert.Contains("id=\"year\"", html);
        Assert.Contains($"<option value=\"{DateTime.UtcNow.Year}\" selected=\"selected\">", html);
        Assert.Contains($"<option value=\"{DateTime.UtcNow.Year - 2}\">", html);   // an older year, not selected
        Assert.Contains($"<option value=\"{DateTime.UtcNow.Month}\" selected=\"selected\">", html);
        Assert.Contains("id=\"monetaryUnit\"", html);
        Assert.Contains("<option value=\"KRW\"", html);
        Assert.Contains("<option value=\"USD\"", html);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "<option value=\"(KRW|USD)\" selected=\"selected\">")); // exactly the default
    }

    /// <summary>A user with due fixed incomes and fixed expenditures sees an entry for each kind in the top bar's notification list.</summary>
    [Fact]
    public async Task TopBar_ListsDueFixedIncomesAndExpenditures()
    {
        const string email = "view-topbar@test.com";
        var session = await AuthenticatedSessionHelper.LoginAsync(factory, _client, email, "UserPassword1!", Role.User, TestContext.Current.CancellationToken);
        SeedAsset(email, "View-Top-Asset", "KRW");
        Seed(db =>
        {
            db.FixedIncomes.Add(new FixedIncome
            {
                AccountEmail = email, DepositMyAssetProductName = "View-Top-Asset", MainClass = "RegularIncome", SubClass = "LaborIncome", Content = "due income",
                Amount = 3000m, DepositMonth = 1, DepositDay = 25, MaturityDate = DateTime.UtcNow.AddYears(1), Note = "", Unpunctuality = true,
                Created = DateTime.UtcNow, Updated = DateTime.UtcNow,
            });
            db.FixedExpenditures.Add(new FixedExpenditure
            {
                AccountEmail = email, PaymentMethod = "View-Top-Asset", MyDepositAsset = null, MainClass = "ConsumerSpending", SubClass = "Tax", Content = "due expense",
                Amount = 70m, DepositMonth = 1, DepositDay = 25, MaturityDate = DateTime.UtcNow.AddYears(1), Note = "", Unpunctuality = true,
                Created = DateTime.UtcNow, Updated = DateTime.UtcNow,
            });
        });

        string html = await GetHtmlAsync("/Dashboard/UserIndex", session);

        Assert.Contains("href=\"/Notice/FixedIncome\" class=\"dropdown-item\"", html);
        Assert.Contains("href=\"/Notice/FixedExpenditure\" class=\"dropdown-item\"", html);
    }

    /// <summary>The access-denied page renders (for a signed-in user) once the authorization filter — which otherwise always redirects away from it — is out of the way.</summary>
    [Fact]
    public async Task AccessDenied_RendersItsView_WhenNotRedirectedAway()
    {
        var session = await AuthenticatedSessionHelper.LoginAsync(factory, _client, "view-denied@test.com", "UserPassword1!", Role.User, TestContext.Current.CancellationToken);
        await using var host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
            services.Configure<MvcOptions>(o =>
            {
                foreach (var filter in o.Filters.Where(f => f is TypeFilterAttribute { ImplementationType.Name: "AuthorizationFilter" } or ServiceFilterAttribute { ServiceType.Name: "AuthorizationFilter" }).ToList())
                    o.Filters.Remove(filter);
            })));
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://www.localhost/"), AllowAutoRedirect = false, HandleCookies = false });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/Exception/AccessDenied");
        request.Headers.Add("Cookie", session.CookieHeader);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("AccessDenied", html);
    }

    // -- the MVC Grid table template in the filter modes the site's own grids do not use ---------------------------

    /// <summary>A grid row with two text columns.</summary>
    private sealed record Row(string Name, string Kind);

    /// <summary>Renders the shared MvcGrid partial for a two-row grid (text and multi-select filters enabled) in filter mode <paramref name="mode"/>.</summary>
    private async Task<string> RenderGridAsync(GridFilterMode mode)
    {
        using var scope = factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { RequestServices = sp };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        IRazorViewEngine engine = sp.GetRequiredService<IRazorViewEngine>();
        ViewEngineResult found = engine.GetView(null, "~/Views/Shared/MvcGrid/_Grid.cshtml", isMainPage: false);
        Assert.True(found.Success, string.Join(",", found.SearchedLocations));

        var grid = new Grid<Row>([new Row("alpha", "x"), new Row("beta", "y")])
        {
            FilterMode = mode,
        };
        var name = grid.Columns.Add(r => r.Name);
        var kind = grid.Columns.Add(r => r.Kind);
        var choice = grid.Columns.Add(r => r.Kind);
        name.Title = "Name"; kind.Title = "Kind"; choice.Title = "Choice";
        foreach (IGridColumn column in grid.Columns)
        {
            column.Filter.IsEnabled = true;
            column.Sort.IsEnabled = true;
            column.Filter.Name = column.Name;
        }
        choice.Filter.Options = [new SelectListItem("X", "x"), new SelectListItem("Y", "y")];
        choice.Filter.Type = GridFilterType.Multi;

        var viewData = new ViewDataDictionary<IGrid>(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = grid };
        await using var writer = new StringWriter();
        var viewContext = new ViewContext(actionContext, found.View, viewData,
            new TempDataDictionary(httpContext, sp.GetRequiredService<ITempDataProvider>()), writer, new HtmlHelperOptions());
        await found.View.RenderAsync(viewContext);
        return writer.ToString();
    }

    /// <summary>In Excel mode the header shows the plain title with the filter popup's options next to it.</summary>
    [Fact]
    public async Task GridTemplate_ExcelFilterMode_ShowsTitlesWithFilterOptions()
    {
        string html = await RenderGridAsync(GridFilterMode.Excel);

        Assert.Contains("mvc-grid-excel-mode", html);
        Assert.Contains("class=\"mvc-grid-title\"", html);
        Assert.Contains("class=\"mvc-grid-options\"", html);
    }

    /// <summary>In row mode a choice column gets its own read-only value box and option list in the filter row.</summary>
    [Fact]
    public async Task GridTemplate_RowFilterMode_RendersAFilterRowWithChoiceOptions()
    {
        string html = await RenderGridAsync(GridFilterMode.Row);

        Assert.Contains("mvc-grid-row-mode", html);
        Assert.Contains("class=\"mvc-grid-options\"", html);
        Assert.Contains("<option value=\"y\">Y</option>", html);
    }
}
