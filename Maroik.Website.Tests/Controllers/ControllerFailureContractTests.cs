using System.Reflection;
using System.Text.Json;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Account;
using Maroik.Website.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// The failure contract of the JSON endpoints: when the service behind an action blows up, the caller gets
/// HTTP 200 with <c>{ "result": false, ... }</c> and the generic message — never an error page, and never the
/// exception's own text (which can carry SQL, host names or file paths). The services are replaced by proxies
/// that throw on every call, in a derived host that shares the same PostgreSQL database.
/// </summary>
[Collection("Website Integration")]
public class ControllerFailureContractTests(MaroikWebApplicationFactory factory)
{
    /// <summary>Message of every exception the broken services throw; it must never reach the response.</summary>
    private const string Secret = "boom-secret-detail-do-not-leak";

    /// <summary>The services replaced by always-throwing proxies in <see cref="CreateBrokenHost"/>.</summary>
    private static readonly Type[] _brokenServices =
    [
        typeof(ICalendarService), typeof(IBoardService), typeof(IAssetService), typeof(IIncomeService),
        typeof(IExpenditureService), typeof(IFixedIncomeService), typeof(IFixedExpenditureService), typeof(IManagementAccountService),
        typeof(IProfileService),
    ];

    /// <summary>Throws <see cref="InvalidOperationException"/> carrying <see cref="Secret"/> from every method it is asked to run.</summary>
    public class ThrowingProxy : DispatchProxy
    {
        /// <inheritdoc />
        protected override object Invoke(MethodInfo? targetMethod, object?[]? args) => throw new InvalidOperationException(Secret);
    }

    /// <summary>A host in which every service in <see cref="_brokenServices"/> throws on any call.</summary>
    private WebApplicationFactory<Program> CreateBrokenHost() =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            foreach (Type service in _brokenServices)
            {
                services.RemoveAll(service);
                services.AddScoped(service, _ => DispatchProxy.Create(service, typeof(ThrowingProxy)));
            }
        }));

    /// <summary>A JSON body listing one placeholder calendar.</summary>
    private static object CalendarsPayload() => new { Calendars = new[] { new { Id = 1, Name = "x", Description = "", TimeZoneIanaId = "UTC", HtmlColorCode = "#3788d8" } } };

    /// <summary>
    /// Each row: the role that may call the endpoint, the endpoint, and whether it takes a body. All of these
    /// reach their service inside a try/catch, so a throwing service must surface as the generic failure.
    /// </summary>
    private static readonly (string Role, string Url, bool WithBody)[] _endpoints =
    [
        (Role.User, "/Calendar/IsCalendarExists?id=1", false),
        (Role.User, "/Calendar/GetCalendars", false),
        (Role.User, "/Calendar/GetOtherCalendars", false),
        (Role.User, "/Calendar/IsCalendarEventExists?id=1", false),
        (Role.User, "/Calendar/IsOtherCalendarEventExists?id=1", false),
        (Role.User, "/Calendar/GetBrowseCalendarsOfInterest", false),
        (Role.User, "/Calendar/DeleteCalendarEvent?id=1", false),
        (Role.User, "/Calendar/GetCalendarEvents", true),
        (Role.User, "/Calendar/CreateCalendar", true),
        (Role.User, "/Calendar/UpdateCalendar", true),
        (Role.User, "/Calendar/DeleteCalendar", true),
        (Role.Admin, "/Calendar/GetCalendarShareds", false),
        (Role.User, "/Forum/IsBoardExists?id=1", false),
        (Role.User, "/Forum/DeleteComment?id=1", false),
        (Role.User, "/Management/IsBoardExists?id=1", false),
        (Role.User, "/Management/DeleteComment?id=1", false),
        (Role.Admin, "/Management/IsAccountExists?email=someone@example.com", false),
        (Role.User, "/Notice/IsFixedIncomeExists?id=1", false),
        (Role.User, "/Notice/IsFixedExpenditureExists?id=1", false),
        (Role.User, "/AccountBook/IsAssetExists?productName=x", false),
        (Role.User, "/AccountBook/IsIncomeExists?id=1", false),
        (Role.User, "/AccountBook/IsExpenditureExists?id=1", false),
    ];

    /// <summary>The endpoints that return an amount label (currency) for the selected asset.</summary>
    private static readonly string[] _amountLabelEndpoints =
    [
        "/AccountBook/GetIncomeAmountLabel", "/AccountBook/GetExpenditureAmountLabel",
        "/Notice/GetFixedIncomeAmountLabel", "/Notice/GetFixedExpenditureAmountLabel",
    ];

    /// <summary>
    /// A service failure inside a JSON action becomes the generic failure result (HTTP 200, <c>result:false</c>,
    /// "Input is invalid"), without leaking the exception text. One derived host serves every endpoint (each
    /// extra host costs seconds of start-up), so failures are collected and reported together.
    /// </summary>
    [Fact]
    public async Task AServiceFailure_BecomesTheGenericFailureResult_WithoutLeakingTheException()
    {
        await using var host = CreateBrokenHost();
        var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://www.localhost/"), AllowAutoRedirect = false, HandleCookies = false
        });
        var sessions = new Dictionary<string, AuthenticatedSession>();
        foreach (string role in new[] { Role.User, Role.Admin })
            sessions[role] = await AuthenticatedSessionHelper.LoginAsync(host, client, $"failure-contract-{role.ToLowerInvariant()}@test.com", "UserPassword1!", role, TestContext.Current.CancellationToken);

        List<string> failures = [];
        foreach (var (role, url, withBody) in _endpoints)
        {
            using var request = sessions[role].BuildJsonPostRequest(url, withBody ? CalendarsPayload() : null);
            var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            if (response.StatusCode != System.Net.HttpStatusCode.OK) { failures.Add($"{url}: HTTP {(int)response.StatusCode}"); continue; }
            using JsonDocument doc = JsonDocument.Parse(json);
            if (doc.RootElement.GetProperty("result").GetBoolean()) failures.Add($"{url}: reported success");
            if (!json.Contains("Input is invalid")) failures.Add($"{url}: not the generic message ({json})");
            if (json.Contains(Secret)) failures.Add($"{url}: leaked the exception text");
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>The amount-label lookups degrade to the plain "Amount" label instead of failing the whole form.</summary>
    [Fact]
    public async Task AmountLabel_FallsBackToThePlainLabel_WhenTheAssetLookupFails()
    {
        await using var host = CreateBrokenHost();
        var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://www.localhost/"), AllowAutoRedirect = false, HandleCookies = false
        });
        var session = await AuthenticatedSessionHelper.LoginAsync(host, client, "failure-label@test.com", "UserPassword1!", Role.User, TestContext.Current.CancellationToken);

        List<string> failures = [];
        foreach (string url in _amountLabelEndpoints)
        {
            using var request = session.BuildJsonPostRequest($"{url}?productName=x");
            var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            if (response.StatusCode != System.Net.HttpStatusCode.OK) { failures.Add($"{url}: HTTP {(int)response.StatusCode}"); continue; }
            using JsonDocument doc = JsonDocument.Parse(json);
            if (doc.RootElement.GetProperty("result").GetBoolean()) failures.Add($"{url}: reported success");
            if (doc.RootElement.GetProperty("label").GetString() != "Amount") failures.Add($"{url}: label is not the plain 'Amount' ({json})");
            if (json.Contains(Secret)) failures.Add($"{url}: leaked the exception text");
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    // -- endpoints with a request body, and page actions that must degrade to a redirect ----------------------

    /// <summary>A multipart form with one text part per field.</summary>
    private static MultipartFormDataContent Form(params (string Name, string Value)[] fields)
    {
        var form = new MultipartFormDataContent();
        foreach (var (name, value) in fields) form.Add(new StringContent(value), name);
        return form;
    }

    /// <summary>A valid fixed-income JSON body with id <paramref name="id"/>.</summary>
    private static object FixedIncome(long id) => new
    {
        Id = id, MainClass = "RegularIncome", SubClass = "LaborIncome", Content = "Salary", Amount = 100, DepositMonth = 6, DepositDay = 25,
        MaturityDate = "2030-12-31", Note = "", DepositMyAssetProductName = "Bank", Unpunctuality = false,
    };

    /// <summary>A valid fixed-expenditure JSON body with id <paramref name="id"/>.</summary>
    private static object FixedExpenditure(long id) => new
    {
        Id = id, MainClass = "ConsumerSpending", SubClass = "MealOrEatOutExpenses", Content = "Rent", Amount = 100, DepositMonth = 6, DepositDay = 1,
        MaturityDate = "2030-12-31", Note = "", PaymentMethod = "Bank", MyDepositAsset = "N/A", Unpunctuality = false,
    };

    /// <summary>A valid timed-event form with id <paramref name="id"/> in calendar 1.</summary>
    private static MultipartFormDataContent EventForm(long id) => Form(
        ("Id", id.ToString()), ("CalendarId", "1"), ("Title", "Lunch"), ("AllDay", "false"), ("StartDate", "2024-05-01 10:00"),
        ("EndDate", "2024-05-01 11:00"), ("StartDateTimeZoneIanaId", "UTC"), ("EndDateTimeZoneIanaId", "UTC"),
        ("Status", "Busy"), ("SerializedCalendarReminders", "[]"));

    /// <summary>Each body-carrying JSON endpoint whose action wraps its service call in a try/catch.</summary>
    internal static readonly (string Role, string Url, Func<AuthenticatedSession, HttpRequestMessage> Build)[] BodyEndpoints =
    [
        (Role.User, "/Forum/WriteFreeBoard", s => s.BuildFormPostRequest("/Forum/WriteFreeBoard", Form(("Title", "t"), ("Content", "c"), ("Locked", "false"), ("Noticed", "false")))),
        (Role.User, "/Forum/EditFreeBoard", s => s.BuildFormPostRequest("/Forum/EditFreeBoard", Form(("Id", "1"), ("Title", "t"), ("Content", "c"), ("Locked", "false")))),
        (Role.User, "/Forum/DeleteBoard", s => s.BuildJsonPostRequest("/Forum/DeleteBoard", new { Id = 1 })),
        (Role.User, "/Forum/WriteFreeComment", s => s.BuildJsonPostRequest("/Forum/WriteFreeComment", new { BoardId = 1, Content = "hi", DetailCurrentPage = 1 })),
        (Role.User, "/Management/WritePrivateNoteBoard", s => s.BuildFormPostRequest("/Management/WritePrivateNoteBoard", Form(("Title", "t"), ("Content", "c")))),
        (Role.User, "/Management/EditPrivateNoteBoard", s => s.BuildFormPostRequest("/Management/EditPrivateNoteBoard", Form(("Id", "1"), ("Title", "t"), ("Content", "c")))),
        (Role.User, "/Management/DeleteBoard", s => s.BuildJsonPostRequest("/Management/DeleteBoard", new { Id = 1 })),
        (Role.User, "/Management/WritePrivateNoteComment", s => s.BuildJsonPostRequest("/Management/WritePrivateNoteComment", new { BoardId = 1, Content = "hi", DetailCurrentPage = 1 })),
        (Role.User, "/Management/UpdateProfilePassword", s => s.BuildJsonPostRequest("/Management/UpdateProfilePassword", new { Password = "OldPassw0rd!x", NewPassword = "NewPassw0rd!x1" })),
        (Role.Admin, "/Management/UpdateAccount", s => s.BuildJsonPostRequest("/Management/UpdateAccount", new { Email = "someone@example.com", Password = "", Role = Role.User, TimeZoneIanaId = "UTC", Locked = false, EmailConfirmed = true, AgreedServiceTerms = true, Message = "", Deleted = false })),
        (Role.Admin, "/Management/DeleteAccount", s => s.BuildJsonPostRequest("/Management/DeleteAccount", new { Email = "someone@example.com" })),
        (Role.User, "/Notice/CreateFixedIncome", s => s.BuildJsonPostRequest("/Notice/CreateFixedIncome", FixedIncome(0))),
        (Role.User, "/Notice/UpdateFixedIncome", s => s.BuildJsonPostRequest("/Notice/UpdateFixedIncome", FixedIncome(1))),
        (Role.User, "/Notice/DeleteFixedIncome", s => s.BuildJsonPostRequest("/Notice/DeleteFixedIncome", FixedIncome(1))),
        (Role.User, "/Notice/CreateFixedExpenditure", s => s.BuildJsonPostRequest("/Notice/CreateFixedExpenditure", FixedExpenditure(0))),
        (Role.User, "/Notice/UpdateFixedExpenditure", s => s.BuildJsonPostRequest("/Notice/UpdateFixedExpenditure", FixedExpenditure(1))),
        (Role.User, "/Notice/DeleteFixedExpenditure", s => s.BuildJsonPostRequest("/Notice/DeleteFixedExpenditure", FixedExpenditure(1))),
        (Role.User, "/Calendar/CreateCalendarEvent", s => s.BuildFormPostRequest("/Calendar/CreateCalendarEvent", EventForm(0))),
        (Role.User, "/Calendar/UpdateCalendarEvent", s => s.BuildFormPostRequest("/Calendar/UpdateCalendarEvent", EventForm(1))),
        (Role.Admin, "/Calendar/UpdateCalendarShared", s => s.BuildJsonPostRequest("/Calendar/UpdateCalendarShared", new[] { new { CalendarId = 1, User = true, Anonymous = false } })),
        (Role.User, "/Calendar/UpdateOtherCalendar", s => s.BuildJsonPostRequest("/Calendar/UpdateOtherCalendar", new[] { new { CalendarId = 1 } })),
    ];

    /// <summary>The same guarantee for the write endpoints: a failing service never yields an error page or leaks the exception.</summary>
    [Fact]
    public async Task AServiceFailure_InAWriteEndpoint_BecomesTheGenericFailureResult_WithoutLeakingTheException()
    {
        await using var host = CreateBrokenHost();
        var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://www.localhost/"), AllowAutoRedirect = false, HandleCookies = false
        });
        var sessions = new Dictionary<string, AuthenticatedSession>();
        foreach (string role in new[] { Role.User, Role.Admin })
            sessions[role] = await AuthenticatedSessionHelper.LoginAsync(host, client, $"failure-body-{role.ToLowerInvariant()}@test.com", "UserPassword1!", role, TestContext.Current.CancellationToken);

        List<string> failures = [];
        foreach (var (role, url, build) in BodyEndpoints)
        {
            using var request = build(sessions[role]);
            var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            if (response.StatusCode != System.Net.HttpStatusCode.OK) { failures.Add($"{url}: HTTP {(int)response.StatusCode} {json[..Math.Min(json.Length, 80)]}"); continue; }
            using JsonDocument doc = JsonDocument.Parse(json);
            if (doc.RootElement.GetProperty("result").GetBoolean()) failures.Add($"{url}: reported success");
            if (json.Contains(Secret)) failures.Add($"{url}: leaked the exception text");
            if (!json.Contains("Input is invalid") && !json.Contains("could not be completed")) failures.Add($"{url}: unexpected message ({json})");
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// Two endpoints answer a failing service in their own way (not the shared generic message): account creation says
    /// the input is invalid, the default-currency change reports a bare <c>result:false</c>. Neither leaks the exception.
    /// </summary>
    [Fact]
    public async Task AServiceFailure_InCreateAccountAndDefaultMonetary_ReportsFailure_WithoutLeakingTheException()
    {
        await using var host = CreateBrokenHost();
        var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://www.localhost/"), AllowAutoRedirect = false, HandleCookies = false
        });
        var admin = await AuthenticatedSessionHelper.LoginAsync(host, client, "failure-create-admin@test.com", "UserPassword1!", Role.Admin, TestContext.Current.CancellationToken);
        var user = await AuthenticatedSessionHelper.LoginAsync(host, client, "failure-monetary-user@test.com", "UserPassword1!", Role.User, TestContext.Current.CancellationToken);

        using var create = admin.BuildJsonPostRequest("/Management/CreateAccount", new
        {
            Email = "brand-new@example.com", Password = "NewPassw0rd!x1", Nickname = "BrandNew", Role = Role.User, TimeZoneIanaId = "UTC",
            Locked = false, EmailConfirmed = true, AgreedServiceTerms = true, Message = "", Deleted = false,
        });
        using var monetary = user.BuildJsonPostRequest("/Dashboard/UserUpdateDefaultMonetary", new { DefaultMonetaryUnit = "USD" });

        foreach (var (request, expectedMessage) in new[] { (create, "Input is invalid"), (monetary, "") })
        {
            var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
            using JsonDocument doc = JsonDocument.Parse(json);
            Assert.False(doc.RootElement.GetProperty("result").GetBoolean(), json);
            Assert.DoesNotContain(Secret, json);
            Assert.Contains(expectedMessage, json);
        }
    }

    /// <summary>A post / private-note page whose service call fails sends the reader back to the list instead of an error page.</summary>
    [Fact]
    public async Task ADetailOrEditPage_WhoseServiceFails_RedirectsToTheList()
    {
        await using var host = CreateBrokenHost();
        var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://www.localhost/"), AllowAutoRedirect = false, HandleCookies = false
        });
        var session = await AuthenticatedSessionHelper.LoginAsync(host, client, "failure-pages@test.com", "UserPassword1!", Role.User, TestContext.Current.CancellationToken);

        List<string> failures = [];
        foreach (string url in new[]
        {
            "/Forum/FreeForum?method=detail&boardId=1", "/Forum/FreeForum?method=edit&boardId=1",
            "/Management/PrivateNote?method=detail&boardId=1", "/Management/PrivateNote?method=edit&boardId=1",
        })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Cookie", session.CookieHeader);
            var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            if (response.StatusCode != System.Net.HttpStatusCode.Redirect) failures.Add($"{url}: HTTP {(int)response.StatusCode}");
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>The time-zone form is a plain (non-AJAX) post: a failing service is logged and the user is sent back to the profile page, unchanged.</summary>
    [Fact]
    public async Task ATimeZoneUpdate_WhoseServiceFails_ReturnsToTheProfilePage()
    {
        await using var host = CreateBrokenHost();
        var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://www.localhost/"), AllowAutoRedirect = false, HandleCookies = false
        });
        var session = await AuthenticatedSessionHelper.LoginAsync(host, client, "failure-timezone@test.com", "UserPassword1!", Role.User, TestContext.Current.CancellationToken);
        using var request = session.BuildFormPostRequest("/Management/UpdateProfileTimeZone", Form(("TimeZoneIanaId", "UTC")));

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Management/Profile", response.Headers.Location!.ToString());
    }

    /// <summary>
    /// Passes the read calls the navigation filters make (<c>GetAll*</c>) through to the real menu service and throws on
    /// everything else, so the menu-management actions can fail without breaking every authenticated request.
    /// </summary>
    public class MenuWritesThrowProxy : DispatchProxy
    {
        /// <summary>The real service the read calls are forwarded to.</summary>
        public IMenuService Target { get; set; } = null!;

        /// <inheritdoc />
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod!.Name.StartsWith("GetAll", StringComparison.Ordinal) ? targetMethod.Invoke(Target, args) : throw new InvalidOperationException(Secret);
        }
    }

    /// <summary>The menu-management endpoints degrade to the generic failure when the menu service fails.</summary>
    [Fact]
    public async Task AMenuServiceFailure_BecomesTheGenericFailureResult_WithoutLeakingTheException()
    {
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IMenuService>();
            services.AddScoped<IMenuService>(sp =>
            {
                // ReSharper disable once SuspiciousTypeConversion.Global
                var proxy = (MenuWritesThrowProxy)DispatchProxy.Create<IMenuService, MenuWritesThrowProxy>();
                proxy.Target = ActivatorUtilities.CreateInstance<Maroik.Core.Service.Services.MenuService>(sp);
                // ReSharper disable once SuspiciousTypeConversion.Global
                return (IMenuService)proxy;
            });
        }));
        var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://www.localhost/"), AllowAutoRedirect = false, HandleCookies = false
        });
        var session = await AuthenticatedSessionHelper.LoginAsync(host, client, "failure-menu-admin@test.com", "UserPassword1!", Role.Admin, TestContext.Current.CancellationToken);
        var category = new { Id = 1, Name = "N", DisplayName = "D", IconPath = "i", Controller = "C", Action = "A", Role = "Admin", Order = 1 };
        var sub = new { Id = 1, CategoryId = 1, Name = "N", DisplayName = "D", IconPath = "i", Action = "A", Role = "Admin", Order = 1 };
        (string Url, object? Body)[] calls =
        [
            ("/Management/IsCategoryExists?id=1", null), ("/Management/IsSubCategoryExists?id=1", null),
            ("/Management/CreateCategory", category), ("/Management/CreateSubCategory", sub),
            ("/Management/UpdateCategory", category), ("/Management/UpdateSubCategory", sub),
            ("/Management/DeleteCategory", category), ("/Management/DeleteSubCategory", sub),
        ];

        List<string> failures = [];
        foreach (var (url, body) in calls)
        {
            using var request = session.BuildJsonPostRequest(url, body);
            var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            if (response.StatusCode != System.Net.HttpStatusCode.OK) { failures.Add($"{url}: HTTP {(int)response.StatusCode}"); continue; }
            using JsonDocument doc = JsonDocument.Parse(json);
            if (doc.RootElement.GetProperty("result").GetBoolean()) failures.Add($"{url}: reported success");
            if (!json.Contains("Input is invalid")) failures.Add($"{url}: unexpected message ({json})");
            if (json.Contains(Secret)) failures.Add($"{url}: leaked the exception text");
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }
}
