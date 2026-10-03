using Maroik.Core.Domain.Account;
using Maroik.Core.PostgreSQL.Data;
using Maroik.Core.PostgreSQL.Models;
using Maroik.Website.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// Every "now" the web layer shows or decides on comes from the injected <see cref="TimeProvider"/>, the same clock
/// the Core use cases read — not from the machine's clock: the date/time pickers' defaults, the fixed-schedule
/// grids' expiry flags, the Excel export file names, the footer's copyright year and the culture cookie's expiry.
/// One derived host runs on a <see cref="FakeTimeProvider"/> fixed at 2041-12-31 16:00:00 UTC, which is already
/// 2042-01-01 01:00:00 in Asia/Seoul, the time zone the signed-in accounts are moved to.
/// </summary>
[Collection("Website Integration")]
public class InjectedClockTests(MaroikWebApplicationFactory factory)
{
    /// <summary>The instant the fake clock is fixed at.</summary>
    private static readonly DateTimeOffset _now = new(2041, 12, 31, 16, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The one fake-clock host every test of this class shares. Built once, not per test: each derived host is a
    /// full application start-up.
    /// </summary>
    private static WebApplicationFactory<Program>? _sharedHost;

    /// <summary>Guards the one-time creation of <see cref="_sharedHost"/>.</summary>
    private static readonly Lock _sharedHostLock = new();

    /// <summary>The host whose <see cref="TimeProvider"/> is the fake clock.</summary>
    private readonly WebApplicationFactory<Program> _host = SharedHost(factory);

    /// <summary>Returns <see cref="_sharedHost"/>, deriving it from <paramref name="factory"/> on first use.</summary>
    private static WebApplicationFactory<Program> SharedHost(MaroikWebApplicationFactory factory)
    {
        lock (_sharedHostLock)
        {
            return _sharedHost ??= factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
                services.AddSingleton<TimeProvider>(new FakeTimeProvider(_now))));
        }
    }

    /// <summary>A client for <see cref="_host"/>; redirects are not followed and cookies are managed by hand.</summary>
    private HttpClient CreateClient() => _host.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://www.localhost/"), AllowAutoRedirect = false, HandleCookies = false
    });

    /// <summary>Runs <paramref name="seed"/> against a fresh context of <see cref="_host"/> and saves.</summary>
    private void Seed(Action<ApplicationDbContext> seed)
    {
        using var scope = _host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        seed(db);
        db.SaveChanges();
    }

    /// <summary>Signs in a fresh <paramref name="role"/> account on <paramref name="client"/> and moves it to Asia/Seoul.</summary>
    private async Task<(string Email, AuthenticatedSession Session)> LoginInSeoulAsync(HttpClient client, string role)
    {
        string email = $"clock-{Guid.NewGuid():N}@test.com";
        var session = await AuthenticatedSessionHelper.LoginAsync(_host, client, email, "UserPassword1!", role, TestContext.Current.CancellationToken);
        // AuthorizationFilter re-reads the account on every request, so the new zone applies from the next one.
        Seed(db => db.Accounts.Single(a => a.Email == email).TimeZoneIanaId = "Asia/Seoul");
        return (email, session);
    }

    /// <summary>Sends a GET to <paramref name="url"/> with the session, as an AJAX request when <paramref name="ajax"/> is set, and returns the body.</summary>
    private static async Task<string> GetAsync(HttpClient client, string url, AuthenticatedSession session, bool ajax = false)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Cookie", session.CookieHeader);
        if (ajax) request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>The create forms' date pickers default to the clock's date in the viewer's time zone.</summary>
    [Theory]
    [InlineData("/AccountBook/Income", "createIncomeDate", Role.User)]
    [InlineData("/AccountBook/Expenditure", "createExpenditureDate", Role.User)]
    [InlineData("/Calendar/UserIndex", "createCalendarEventAllDayCheckedStartDate", Role.User)]
    [InlineData("/Calendar/AdminIndex", "createCalendarEventAllDayCheckedStartDate", Role.Admin)]
    public async Task CreateForm_DefaultsItsDate_ToTheClocksDateInTheViewersTimeZone(string url, string inputId, string role)
    {
        using HttpClient client = CreateClient();
        var (_, session) = await LoginInSeoulAsync(client, role);

        string html = await GetAsync(client, url, session);

        Assert.Contains($"id=\"{inputId}\" name=\"{inputId}\" value=\"2042-01-01\"", html);
    }

    /// <summary>
    /// The fixed-schedule grids flag a schedule that matured on 2041-12-31 as expired: by the clock it is already
    /// 1 January 2042 in the viewer's time zone (the machine's clock is years before its maturity).
    /// </summary>
    [Theory]
    [InlineData("/Notice/FixedIncome")]
    [InlineData("/Notice/FixedExpenditure")]
    public async Task FixedScheduleGrid_FlagsExpiry_AgainstTheClock(string url)
    {
        using HttpClient client = CreateClient();
        var (email, session) = await LoginInSeoulAsync(client, Role.User);
        long id = 0;
        Seed(db =>
        {
            db.Assets.Add(new Asset
            {
                ProductName = "Clock-Bank", AccountEmail = email, Item = "FreeDepositAndWithdrawal", MonetaryUnit = "KRW",
                Amount = 1000m, Note = "", Deleted = false, Created = DateTime.UtcNow, Updated = DateTime.UtcNow
            });
            db.SaveChanges();
            var maturity = new DateTime(2041, 12, 31);
            if (url.EndsWith("FixedIncome"))
            {
                var row = new FixedIncome
                {
                    AccountEmail = email, DepositMyAssetProductName = "Clock-Bank", MainClass = "RegularIncome", SubClass = "LaborIncome",
                    Content = "clock", Amount = 1m, DepositMonth = 6, DepositDay = 1, MaturityDate = maturity,
                    Note = "", Unpunctuality = false, Created = DateTime.UtcNow, Updated = DateTime.UtcNow
                };
                db.FixedIncomes.Add(row);
                db.SaveChanges();
                id = row.Id;
            }
            else
            {
                var row = new FixedExpenditure
                {
                    AccountEmail = email, PaymentMethod = "Clock-Bank", MainClass = "ConsumerSpending", SubClass = "MealOrEatOutExpenses",
                    Content = "clock", Amount = 1m, DepositMonth = 6, DepositDay = 1, MaturityDate = maturity,
                    Note = "", Unpunctuality = false, Created = DateTime.UtcNow, Updated = DateTime.UtcNow
                };
                db.FixedExpenditures.Add(row);
                db.SaveChanges();
                id = row.Id;
            }
        });

        string grid = await GetAsync(client, url, session, ajax: true);

        Assert.Contains($"class=\"table-danger clsGridRow\" data-id=\"{id}\"", grid);
    }

    /// <summary>Every Excel export is named with the clock's time in the viewer's time zone.</summary>
    [Theory]
    [InlineData("/AccountBook/ExportExcelAsset", Role.User)]
    [InlineData("/AccountBook/ExportExcelIncome", Role.User)]
    [InlineData("/AccountBook/ExportExcelExpenditure", Role.User)]
    [InlineData("/Notice/ExportExcelFixedIncome", Role.User)]
    [InlineData("/Notice/ExportExcelFixedExpenditure", Role.User)]
    [InlineData("/Management/ExportExcelAccount", Role.Admin)]
    [InlineData("/Management/ExportExcelMenu", Role.Admin)]
    public async Task ExcelExport_IsNamedWithTheClocksTimeInTheViewersTimeZone(string endpoint, string role)
    {
        using HttpClient client = CreateClient();
        var (_, session) = await LoginInSeoulAsync(client, role);
        using var request = session.BuildJsonPostRequest($"{endpoint}?fileName=report");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var disposition = response.Content.Headers.ContentDisposition;
        Assert.Equal("report-2042-01-01-01-00-00-000.xlsx", disposition?.FileNameStar ?? disposition?.FileName?.Trim('"'));
    }

    /// <summary>The page footer's copyright year is the clock's year in the viewer's time zone.</summary>
    [Fact]
    public async Task Footer_ShowsTheClocksYearInTheViewersTimeZone()
    {
        using HttpClient client = CreateClient();
        var (_, session) = await LoginInSeoulAsync(client, Role.User);

        string html = await GetAsync(client, "/AccountBook/Income", session);

        Assert.Contains("Copyright &copy; 2021-2042 ", html);
    }

    /// <summary>The culture cookie expires 30 days after the clock's reading.</summary>
    [Fact]
    public async Task CultureCookie_Expires30DaysAfterTheClock()
    {
        using HttpClient client = CreateClient();
        var anonymous = await AuthenticatedSessionHelper.AnonymousAsync(client, TestContext.Current.CancellationToken);
        using var request = anonymous.BuildJsonPostRequest("/Dashboard/CultureManagement", new { Culture = "en-US" });

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        string culture = Assert.Single(cookies, c => c.StartsWith(".AspNetCore.Culture=", StringComparison.Ordinal));
        Assert.Contains("expires=Thu, 30 Jan 2042 16:00:00 GMT", culture);
    }
}
