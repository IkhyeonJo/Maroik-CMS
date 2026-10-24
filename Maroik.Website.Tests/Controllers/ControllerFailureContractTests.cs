using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Accounts;
using Maroik.Website.Controllers;
using Maroik.Website.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

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

    /// <summary>The message a server-side fault is answered with: not "Input is invalid", which would send the user to fix their input.</summary>
    private const string Temporary = "A temporary error occurred. Please try again later.";

    /// <summary>The services replaced by always-throwing proxies in <see cref="CreateBrokenHost"/>.</summary>
    private static readonly Type[] _brokenServices =
    [
        typeof(ICalendarService), typeof(IBoardService), typeof(IAssetService), typeof(IIncomeService),
        typeof(IExpenditureService), typeof(IFixedIncomeService), typeof(IFixedExpenditureService), typeof(IManagementAccountService),
        typeof(IProfileService), typeof(IKanbanService), typeof(IFileManagerService), typeof(IBudgetService),
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
        (Role.User, "/Calendar/DownloadCalendarEventAttachedFile?calendarEventId=1", false),
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
        (Role.User, "/Forum/DownloadFreeBoardAttachedFile?boardId=1", false),
        (Role.User, "/Management/DownloadPrivateNoteAttachedFile?boardId=1", false),
        (Role.User, "/Kanban/DeleteKanbanCard?id=1", false),
        (Role.User, "/FileManager/GetDrive", false),
        (Role.User, "/FileManager/Download?id=1", false),
        (Role.User, "/FileManager/Preview?id=1", false),
    ];

    /// <summary>The endpoints that return an amount label (currency) for the selected asset.</summary>
    private static readonly string[] _amountLabelEndpoints =
    [
        "/AccountBook/GetIncomeAmountLabel", "/AccountBook/GetExpenditureAmountLabel",
        "/Notice/GetFixedIncomeAmountLabel", "/Notice/GetFixedExpenditureAmountLabel",
    ];

    /// <summary>
    /// A service failure inside a JSON action becomes the generic failure result (HTTP 200, <c>result:false</c>,
    /// "A temporary error occurred. Please try again later."), without leaking the exception text. One derived host serves every endpoint (each
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
            if (!json.Contains(Temporary, StringComparison.Ordinal)) failures.Add($"{url}: not the generic message ({json})");
            if (json.Contains(Secret, StringComparison.Ordinal)) failures.Add($"{url}: leaked the exception text");
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>Each controller's resx pair carries the temporary-error message in Korean too.</summary>
    [Fact]
    public async Task AServiceFailure_IsReportedInKorean_ToAKoreanBrowser()
    {
        await using var host = CreateBrokenHost();
        var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://www.localhost/"), AllowAutoRedirect = false, HandleCookies = false
        });
        var session = await AuthenticatedSessionHelper.LoginAsync(host, client, "failure-contract-korean@test.com", "UserPassword1!", Role.User, TestContext.Current.CancellationToken);

        List<string> failures = [];
        foreach (string url in new[]
        {
            "/Calendar/IsCalendarExists?id=1", "/Forum/IsBoardExists?id=1", "/Management/IsBoardExists?id=1",
            "/Notice/IsFixedIncomeExists?id=1", "/AccountBook/IsIncomeExists?id=1", "/Kanban/DeleteKanbanCard?id=1",
        })
        {
            using var request = session.BuildJsonPostRequest(url);
            request.Headers.AcceptLanguage.ParseAdd("ko-KR");
            var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            string? error = doc.RootElement.GetProperty("error").GetString();
            if (error != "일시적인 오류가 발생했습니다. 잠시 후 다시 시도해 주세요.") failures.Add($"{url}: {error}");
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
            if (json.Contains(Secret, StringComparison.Ordinal)) failures.Add($"{url}: leaked the exception text");
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    // -- endpoints with a request body, and page actions that must degrade to a redirect ----------------------

    /// <summary>A multipart form with one text part per field.</summary>
    private static MultipartFormDataContent Form(params (string Name, string Value)[] fields)
    {
        var form = new MultipartFormDataContent();
        foreach (var (name, value) in fields) MultipartForm.AddText(form, name, value);
        return form;
    }

    /// <summary>A valid fixed-income JSON body with id <paramref name="id"/>.</summary>
    private static object FixedIncome(long id) => new
    {
        Id = id, MainClass = "RegularIncome", SubClass = "LaborIncome", Content = "Salary", Amount = "100", DepositMonth = 6, DepositDay = 25,
        MaturityDate = "2030-12-31", Note = "", DepositMyAssetProductName = "Bank", Unpunctuality = false,
    };

    /// <summary>A valid fixed-expenditure JSON body with id <paramref name="id"/>.</summary>
    private static object FixedExpenditure(long id) => new
    {
        Id = id, MainClass = "ConsumerSpending", SubClass = "MealOrEatOutExpenses", Content = "Rent", Amount = "100", DepositMonth = 6, DepositDay = 1,
        MaturityDate = "2030-12-31", Note = "", PaymentMethod = "Bank", MyDepositAsset = "N/A", Unpunctuality = false,
    };

    /// <summary>A valid asset JSON body.</summary>
    private static object Asset() => new
    {
        ProductName = "Bank", Item = "FreeDepositAndWithdrawal", Amount = "100", MonetaryUnit = "KRW", Note = "", Deleted = false, OriginalProductName = "Bank",
    };

    /// <summary>A valid income JSON body with id <paramref name="id"/>.</summary>
    private static object Income(long id) => new
    {
        Id = id, MainClass = "RegularIncome", SubClass = "LaborIncome", Content = "Salary", Amount = "100", Created = "2026-01-15 10:30:00",
        DepositMyAssetProductName = "Bank", Note = "",
    };

    /// <summary>A valid expenditure JSON body with id <paramref name="id"/>.</summary>
    private static object Expenditure(long id) => new
    {
        Id = id, MainClass = "ConsumerSpending", SubClass = "MealOrEatOutExpenses", Content = "Lunch", Amount = "100", Created = "2026-01-15 10:30:00",
        PaymentMethod = "Bank", MyDepositAsset = "", Note = "",
    };

    /// <summary>A valid timed-event form with id <paramref name="id"/> in calendar 1.</summary>
    private static MultipartFormDataContent EventForm(long id) => Form(
        ("Id", id.ToString(CultureInfo.InvariantCulture)), ("CalendarId", "1"), ("Title", "Lunch"), ("AllDay", "false"), ("StartDate", "2024-05-01 10:00"),
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
        (Role.User, "/AccountBook/CreateAsset", s => s.BuildJsonPostRequest("/AccountBook/CreateAsset", Asset())),
        (Role.User, "/AccountBook/UpdateAsset", s => s.BuildJsonPostRequest("/AccountBook/UpdateAsset", Asset())),
        (Role.User, "/AccountBook/DeleteAsset", s => s.BuildJsonPostRequest("/AccountBook/DeleteAsset", Asset())),
        (Role.User, "/AccountBook/CreateIncome", s => s.BuildJsonPostRequest("/AccountBook/CreateIncome", Income(0))),
        (Role.User, "/AccountBook/UpdateIncome", s => s.BuildJsonPostRequest("/AccountBook/UpdateIncome", Income(1))),
        (Role.User, "/AccountBook/DeleteIncome", s => s.BuildJsonPostRequest("/AccountBook/DeleteIncome", Income(1))),
        (Role.User, "/AccountBook/CreateExpenditure", s => s.BuildJsonPostRequest("/AccountBook/CreateExpenditure", Expenditure(0))),
        (Role.User, "/AccountBook/UpdateExpenditure", s => s.BuildJsonPostRequest("/AccountBook/UpdateExpenditure", Expenditure(1))),
        (Role.User, "/AccountBook/DeleteExpenditure", s => s.BuildJsonPostRequest("/AccountBook/DeleteExpenditure", Expenditure(1))),
        (Role.User, "/Calendar/CreateCalendarEvent", s => s.BuildFormPostRequest("/Calendar/CreateCalendarEvent", EventForm(0))),
        (Role.User, "/Calendar/UpdateCalendarEvent", s => s.BuildFormPostRequest("/Calendar/UpdateCalendarEvent", EventForm(1))),
        (Role.Admin, "/Calendar/UpdateCalendarShared", s => s.BuildJsonPostRequest("/Calendar/UpdateCalendarShared", new[] { new { CalendarId = 1, User = true, Anonymous = false } })),
        (Role.User, "/Calendar/UpdateOtherCalendar", s => s.BuildJsonPostRequest("/Calendar/UpdateOtherCalendar", new[] { new { CalendarId = 1 } })),
        (Role.User, "/Kanban/CreateKanbanCard", s => s.BuildJsonPostRequest("/Kanban/CreateKanbanCard", new { Lane = "ToDo", Title = "t", Description = "", DueDate = "2026-05-28" })),
        (Role.User, "/Kanban/UpdateKanbanCard", s => s.BuildJsonPostRequest("/Kanban/UpdateKanbanCard", new { Id = 1, Title = "t", Description = "", DueDate = (string?)null })),
        (Role.User, "/AccountBook/GetBudgetSheet", s => s.BuildJsonPostRequest("/AccountBook/GetBudgetSheet", new { Kind = "Income", Year = 2026 })),
        (Role.User, "/AccountBook/PreviewBudgetCell", s => s.BuildJsonPostRequest("/AccountBook/PreviewBudgetCell", new { Kind = "Income", Year = 2026, Address = "B2", Input = "=1" })),
        (Role.User, "/AccountBook/SetBudgetCells", s => s.BuildJsonPostRequest("/AccountBook/SetBudgetCells", new { Kind = "Income", Year = 2026, Edits = new[] { new { Address = "B2", Input = "1" } } })),
        (Role.User, "/AccountBook/CopyBudgetCells", s => s.BuildJsonPostRequest("/AccountBook/CopyBudgetCells", new { Kind = "Income", Year = 2026, Source = "B2", Target = "C2" })),
        (Role.User, "/AccountBook/AddBudgetLine", s => s.BuildJsonPostRequest("/AccountBook/AddBudgetLine", new { Kind = "Income", Year = 2026, MainClass = "RegularIncome", SubClass = "LaborIncome", Currency = "USD" })),
        (Role.User, "/AccountBook/RemoveBudgetLine", s => s.BuildJsonPostRequest("/AccountBook/RemoveBudgetLine", new { Kind = "Income", Year = 2026, MainClass = "RegularIncome", SubClass = "LaborIncome", Currency = "USD" })),
        (Role.User, "/AccountBook/CarryOverBudget", s => s.BuildJsonPostRequest("/AccountBook/CarryOverBudget", new { Kind = "Income", Year = 2026 })),
        (Role.User, "/FileManager/GetFolder", s => s.BuildJsonPostRequest("/FileManager/GetFolder", new { FolderId = (long?)null })),
        (Role.User, "/FileManager/Search", s => s.BuildJsonPostRequest("/FileManager/Search", new { Query = "a" })),
        (Role.User, "/FileManager/ListDownloadFiles", s => s.BuildJsonPostRequest("/FileManager/ListDownloadFiles", new { Ids = new[] { 1 } })),
        (Role.User, "/FileManager/CreateFolder", s => s.BuildJsonPostRequest("/FileManager/CreateFolder", new { ParentId = (long?)null, Name = "f", Merge = false })),
        (Role.User, "/FileManager/Rename", s => s.BuildJsonPostRequest("/FileManager/Rename", new { Id = 1, Name = "f" })),
        (Role.User, "/FileManager/Move", s => s.BuildJsonPostRequest("/FileManager/Move", new { Ids = new[] { 1 }, TargetFolderId = (long?)null })),
        (Role.User, "/FileManager/Delete", s => s.BuildJsonPostRequest("/FileManager/Delete", new { Ids = new[] { 1 } })),
        (Role.User, "/FileManager/CheckQuota", s => s.BuildJsonPostRequest("/FileManager/CheckQuota", new { TotalBytes = 1 })),
        (Role.User, "/FileManager/BeginUpload", s => s.BuildJsonPostRequest("/FileManager/BeginUpload", new { Name = "a.txt", Size = 1, Conflict = "Ask" })),
        (Role.User, "/FileManager/UploadChunk", s => s.BuildFormPostRequest("/FileManager/UploadChunk", ChunkForm())),
        (Role.User, "/FileManager/CompleteUpload", s => s.BuildJsonPostRequest("/FileManager/CompleteUpload", new { UploadId = Guid.NewGuid(), Overwrite = false })),
        (Role.User, "/FileManager/AbortUpload", s => s.BuildJsonPostRequest("/FileManager/AbortUpload", new { UploadId = Guid.NewGuid() })),
    ];

    /// <summary>A File Manager chunk form (an upload id, an offset and one byte).</summary>
    private static MultipartFormDataContent ChunkForm()
    {
        MultipartFormDataContent? form = Form(("UploadId", Guid.NewGuid().ToString()), ("Offset", "0"));
        try
        {
            MultipartForm.AddFile(form, "Chunk", "blob", [1], "application/octet-stream");
            MultipartFormDataContent built = form;
            form = null;
            return built;
        }
        finally
        {
            form?.Dispose();
        }
    }

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
            if (json.Contains(Secret, StringComparison.Ordinal)) failures.Add($"{url}: leaked the exception text");
            if (!json.Contains(Temporary, StringComparison.Ordinal)) failures.Add($"{url}: unexpected message ({json})");
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// The account-book write endpoints log the service failure once, at Error, with the exception attached and the
    /// signed-in owner named, so an operator sees the fault the user was only told was "temporary".
    /// </summary>
    [Fact]
    public async Task AServiceFailure_InAnAccountBookWriteEndpoint_IsLoggedAsAnErrorWithTheException()
    {
        var logger = new FakeLogger<AccountBookController>();
        await using var host = CreateBrokenHost().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<ILogger<AccountBookController>>(logger)));
        var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://www.localhost/"), AllowAutoRedirect = false, HandleCookies = false
        });
        const string email = "failure-accountbook-log@test.com";
        var session = await AuthenticatedSessionHelper.LoginAsync(host, client, email, "UserPassword1!", Role.User, TestContext.Current.CancellationToken);

        List<string> failures = [];
        foreach (var (_, url, build) in BodyEndpoints.Where(e => e.Url.StartsWith("/AccountBook/", StringComparison.Ordinal)))
        {
            logger.Collector.Clear();
            using var request = build(session);
            _ = await client.SendAsync(request, TestContext.Current.CancellationToken);

            var errors = logger.Collector.GetSnapshot().Where(r => r.Level == LogLevel.Error).ToList();
            if (errors.Count != 1) { failures.Add($"{url}: {errors.Count} Error entries"); continue; }
            if (errors[0].Exception?.Message != Secret) failures.Add($"{url}: the exception is not attached");
            if (!errors[0].Message.Contains(email, StringComparison.Ordinal)) failures.Add($"{url}: the owner is not named ({errors[0].Message})");
        }

        Assert.Equal(16, BodyEndpoints.Count(e => e.Url.StartsWith("/AccountBook/", StringComparison.Ordinal)));
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// Every Kanban write logs a service failure once, at Error, with the exception attached and the signed-in owner named, so an
    /// operator sees the fault the user was only told was "temporary".
    /// </summary>
    [Fact]
    public async Task AServiceFailure_InAKanbanWrite_IsLoggedAsAnErrorWithTheExceptionAndTheOwner()
    {
        var logger = new FakeLogger<KanbanController>();
        await using var host = CreateBrokenHost().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<ILogger<KanbanController>>(logger)));
        var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://www.localhost/"), AllowAutoRedirect = false, HandleCookies = false
        });
        const string email = "failure-kanban-log@test.com";
        var session = await AuthenticatedSessionHelper.LoginAsync(host, client, email, "UserPassword1!", Role.User, TestContext.Current.CancellationToken);

        List<(string Url, Func<HttpRequestMessage> Build)> requests =
        [
            .. BodyEndpoints.Where(e => e.Url.StartsWith("/Kanban/", StringComparison.Ordinal))
                .Select(e => (e.Url, (Func<HttpRequestMessage>)(() => e.Build(session)))),
            .. _endpoints.Where(e => e.Url.StartsWith("/Kanban/", StringComparison.Ordinal))
                .Select(e => (e.Url, (Func<HttpRequestMessage>)(() => session.BuildJsonPostRequest(e.Url, null)))),
        ];

        List<string> failures = [];
        foreach (var (url, build) in requests)
        {
            logger.Collector.Clear();
            using var request = build();
            _ = await client.SendAsync(request, TestContext.Current.CancellationToken);

            var errors = logger.Collector.GetSnapshot().Where(r => r.Level == LogLevel.Error).ToList();
            if (errors.Count != 1) { failures.Add($"{url}: {errors.Count} Error entries"); continue; }
            if (errors[0].Exception?.Message != Secret) failures.Add($"{url}: the exception is not attached");
            if (!errors[0].Message.Contains(email, StringComparison.Ordinal)) failures.Add($"{url}: the owner is not named ({errors[0].Message})");
        }

        Assert.Equal(3, requests.Count);
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// Every forum action logs a service failure once, at Error, with the exception attached and the signed-in account
    /// named — not a context-free "unhandled exception" line an operator cannot tie to anyone.
    /// </summary>
    [Fact]
    public async Task AServiceFailure_InAForumAction_IsLoggedWithTheExceptionAndTheAccount()
    {
        var logger = new FakeLogger<ForumController>();
        await using var host = CreateBrokenHost().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<ILogger<ForumController>>(logger)));
        var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://www.localhost/"), AllowAutoRedirect = false, HandleCookies = false
        });
        const string email = "failure-forum-log@test.com";
        var session = await AuthenticatedSessionHelper.LoginAsync(host, client, email, "UserPassword1!", Role.User, TestContext.Current.CancellationToken);

        List<(string Url, Func<HttpRequestMessage> Build)> requests =
        [
            .. BodyEndpoints.Where(e => e.Url.StartsWith("/Forum/", StringComparison.Ordinal))
                .Select(e => (e.Url, (Func<HttpRequestMessage>)(() => e.Build(session)))),
            .. _endpoints.Where(e => e.Url.StartsWith("/Forum/", StringComparison.Ordinal) && !e.Url.Contains("Download", StringComparison.Ordinal))
                .Select(e => (e.Url, (Func<HttpRequestMessage>)(() => session.BuildJsonPostRequest(e.Url, null)))),
            .. new[] { "/Forum/FreeForum?method=detail&boardId=1", "/Forum/FreeForum?method=edit&boardId=1" }
                .Select(url => (url, (Func<HttpRequestMessage>)(() =>
                {
                    var get = new HttpRequestMessage(HttpMethod.Get, url);
                    get.Headers.Add("Cookie", session.CookieHeader);
                    return get;
                }))),
        ];

        List<string> failures = [];
        foreach (var (url, build) in requests)
        {
            logger.Collector.Clear();
            using var request = build();
            _ = await client.SendAsync(request, TestContext.Current.CancellationToken);

            var errors = logger.Collector.GetSnapshot().Where(r => r.Level == LogLevel.Error).ToList();
            if (errors.Count != 1) { failures.Add($"{url}: {errors.Count} Error entries"); continue; }
            if (errors[0].Exception?.Message != Secret) failures.Add($"{url}: the exception is not attached");
            if (!errors[0].Message.Contains(email, StringComparison.Ordinal)) failures.Add($"{url}: the account is not named ({errors[0].Message})");
        }

        Assert.Equal(8, requests.Count);
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// Account creation answers a failing service with the temporary-error message, the default-currency change with a bare
    /// <c>result:false</c>. Neither leaks the exception.
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

        foreach (var (request, expectedMessage) in new[] { (create, Temporary), (monetary, "") })
        {
            var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
            using JsonDocument doc = JsonDocument.Parse(json);
            Assert.False(doc.RootElement.GetProperty("result").GetBoolean(), json);
            Assert.DoesNotContain(Secret, json, StringComparison.Ordinal);
            Assert.Contains(expectedMessage, json, StringComparison.Ordinal);
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
        Assert.Contains("/Management/Profile", response.Headers.Location!.ToString(), StringComparison.Ordinal);
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
            if (!json.Contains(Temporary, StringComparison.Ordinal)) failures.Add($"{url}: unexpected message ({json})");
            if (json.Contains(Secret, StringComparison.Ordinal)) failures.Add($"{url}: leaked the exception text");
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>A multipart form carrying a small .png under <paramref name="field"/>, which passes the controllers' own size / extension checks.</summary>
    private static MultipartFormDataContent PngForm(string field)
    {
        var form = new MultipartFormDataContent();
        MultipartForm.AddFile(form, field, "picture.png", [1, 2, 3, 4], "image/png");
        return form;
    }

    /// <summary>The image-upload endpoints: the editor (Summernote) uploads of the three editors, and the profile avatar.</summary>
    internal static readonly string[] UploadEndpoints =
    [
        "/Forum/UploadImageFile", "/Management/UploadImageFile", "/Calendar/UploadImageFile", "/Management/UpdateProfileAvatar",
    ];

    /// <summary>
    /// The image-upload endpoints answer a failing service with <c>{ result: false, errorMessage: &lt;temporary error&gt; }</c> (the
    /// field their pages read), never an error page or the exception text, and log the failure once at Error with the exception
    /// and the uploader's email.
    /// </summary>
    [Fact]
    public async Task AServiceFailure_InAnImageUpload_BecomesTheGenericFailure_AndIsLogged()
    {
        var forumLogger = new FakeLogger<ForumController>();
        var managementLogger = new FakeLogger<ManagementController>();
        var calendarLogger = new FakeLogger<CalendarController>();
        await using var host = CreateBrokenHost().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<ILogger<ForumController>>(forumLogger);
            services.AddSingleton<ILogger<ManagementController>>(managementLogger);
            services.AddSingleton<ILogger<CalendarController>>(calendarLogger);
        }));
        var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://www.localhost/"), AllowAutoRedirect = false, HandleCookies = false
        });
        const string email = "failure-upload@test.com";
        var session = await AuthenticatedSessionHelper.LoginAsync(host, client, email, "UserPassword1!", Role.User, TestContext.Current.CancellationToken);

        List<string> failures = [];
        foreach (string url in UploadEndpoints)
        {
            forumLogger.Collector.Clear(); managementLogger.Collector.Clear(); calendarLogger.Collector.Clear();
            using var request = session.BuildFormPostRequest(url, PngForm(url.EndsWith("Avatar", StringComparison.Ordinal) ? "ProfileAvatarFiles" : "summernoteImageFile"));
            var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            if (response.StatusCode != System.Net.HttpStatusCode.OK) { failures.Add($"{url}: HTTP {(int)response.StatusCode}"); continue; }
            using JsonDocument doc = JsonDocument.Parse(json);
            if (doc.RootElement.GetProperty("result").GetBoolean()) failures.Add($"{url}: reported success");
            if (doc.RootElement.GetProperty("errorMessage").GetString() != Temporary) failures.Add($"{url}: unexpected message ({json})");
            if (json.Contains(Secret, StringComparison.Ordinal)) failures.Add($"{url}: leaked the exception text");

            var errors = forumLogger.Collector.GetSnapshot().Concat(managementLogger.Collector.GetSnapshot()).Concat(calendarLogger.Collector.GetSnapshot())
                .Where(r => r.Level == LogLevel.Error).ToList();
            if (errors.Count != 1) { failures.Add($"{url}: {errors.Count} Error entries"); continue; }
            if (errors[0].Exception?.Message != Secret) failures.Add($"{url}: the exception is not attached");
            if (!errors[0].Message.Contains(email, StringComparison.Ordinal)) failures.Add($"{url}: the uploader is not named ({errors[0].Message})");
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    // -- every POST action is accounted for -----------------------------------------------------------------

    /// <summary>POST actions whose failure contract a dedicated test of this class or another one exercises, with that test.</summary>
    private static readonly Dictionary<string, string> _coveredByDedicatedTests = new()
    {
        ["/Management/CreateAccount"] = nameof(AServiceFailure_InCreateAccountAndDefaultMonetary_ReportsFailure_WithoutLeakingTheException),
        ["/Dashboard/UserUpdateDefaultMonetary"] = nameof(AServiceFailure_InCreateAccountAndDefaultMonetary_ReportsFailure_WithoutLeakingTheException),
        ["/Management/UpdateProfileTimeZone"] = nameof(ATimeZoneUpdate_WhoseServiceFails_ReturnsToTheProfilePage),
        ["/Management/IsCategoryExists"] = nameof(AMenuServiceFailure_BecomesTheGenericFailureResult_WithoutLeakingTheException),
        ["/Management/IsSubCategoryExists"] = nameof(AMenuServiceFailure_BecomesTheGenericFailureResult_WithoutLeakingTheException),
        ["/Management/CreateCategory"] = nameof(AMenuServiceFailure_BecomesTheGenericFailureResult_WithoutLeakingTheException),
        ["/Management/CreateSubCategory"] = nameof(AMenuServiceFailure_BecomesTheGenericFailureResult_WithoutLeakingTheException),
        ["/Management/UpdateCategory"] = nameof(AMenuServiceFailure_BecomesTheGenericFailureResult_WithoutLeakingTheException),
        ["/Management/UpdateSubCategory"] = nameof(AMenuServiceFailure_BecomesTheGenericFailureResult_WithoutLeakingTheException),
        ["/Management/DeleteCategory"] = nameof(AMenuServiceFailure_BecomesTheGenericFailureResult_WithoutLeakingTheException),
        ["/Management/DeleteSubCategory"] = nameof(AMenuServiceFailure_BecomesTheGenericFailureResult_WithoutLeakingTheException),
        ["/Account/ConfirmEmail"] = "RepositoryFailureContractTests",
        ["/Account/ResetPassword"] = "RepositoryFailureContractTests",
    };

    /// <summary>POST actions outside the <c>{ result, error }</c> failure contract, each with the reason.</summary>
    private static readonly Dictionary<string, string> _exemptPosts = new()
    {
        ["/Account/Login"] = "non-AJAX form post that renders a page: a failure goes to the global exception page, like a page GET",
        ["/Account/Logout"] = "non-AJAX form post that renders a page: a failure goes to the global exception page, like a page GET",
        ["/Account/Register"] = "non-AJAX form post that renders a page: a failure goes to the global exception page, like a page GET",
        ["/Account/ForgotPassword"] = "non-AJAX form post that renders a page: a failure goes to the global exception page, like a page GET",
        ["/AccountBook/ExportExcelAsset"] = "plain form download (the page navigates to the file): a failure goes to the global exception page",
        ["/AccountBook/ExportExcelIncome"] = "plain form download (the page navigates to the file): a failure goes to the global exception page",
        ["/AccountBook/ExportExcelExpenditure"] = "plain form download (the page navigates to the file): a failure goes to the global exception page",
        ["/AccountBook/ExportExcelBudget"] = "plain form download (the page navigates to the file): a failure goes to the global exception page",
        ["/Notice/ExportExcelFixedIncome"] = "plain form download (the page navigates to the file): a failure goes to the global exception page",
        ["/Notice/ExportExcelFixedExpenditure"] = "plain form download (the page navigates to the file): a failure goes to the global exception page",
        ["/Management/ExportExcelAccount"] = "plain form download (the page navigates to the file): a failure goes to the global exception page",
        ["/Management/ExportExcelMenu"] = "plain form download (the page navigates to the file): a failure goes to the global exception page",
        ["/Dashboard/CultureManagement"] = "only sets the culture cookie; calls no service",
    };

    /// <summary>The path of <paramref name="url"/> without its query string.</summary>
    private static string PathOf(string url) => url.Split('?')[0];

    /// <summary>
    /// Every <c>[HttpPost]</c> action of the Website is either exercised by a failure-contract test (a throwing service must
    /// become HTTP 200 with the generic message, never an error page) or listed in <see cref="_exemptPosts"/> with its reason.
    /// A new POST action fails this test until it is given one of the two, so the per-action catch cannot be forgotten
    /// silently (CLAUDE.md "Error handling"). A listed path that no longer exists fails it too.
    /// </summary>
    [Fact]
    public void EveryPostAction_IsCoveredByAFailureContractTest_OrExemptWithAReason()
    {
        var actions = typeof(Program).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(Microsoft.AspNetCore.Mvc.Controller)) && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttribute<Microsoft.AspNetCore.Mvc.HttpPostAttribute>() != null)
                .Select(m => $"/{t.Name[..^"Controller".Length]}/{m.Name}"))
            .ToHashSet();

        var covered = _endpoints.Select(e => PathOf(e.Url))
            .Concat(BodyEndpoints.Select(e => e.Url))
            .Concat(_amountLabelEndpoints)
            .Concat(UploadEndpoints)
            .Concat(_coveredByDedicatedTests.Keys)
            .ToHashSet();

        var unaccounted = actions.Except(covered).Except(_exemptPosts.Keys).Order().ToList();
        var stale = covered.Concat(_exemptPosts.Keys).Except(actions).Order().ToList();
        var both = covered.Intersect(_exemptPosts.Keys).Order().ToList();

        Assert.True(unaccounted.Count == 0, "POST actions with no failure-contract test and no exemption:" + Environment.NewLine + string.Join(Environment.NewLine, unaccounted));
        Assert.True(stale.Count == 0, "Listed paths that are not POST actions:" + Environment.NewLine + string.Join(Environment.NewLine, stale));
        Assert.True(both.Count == 0, "Paths both covered and exempt:" + Environment.NewLine + string.Join(Environment.NewLine, both));
    }
}
