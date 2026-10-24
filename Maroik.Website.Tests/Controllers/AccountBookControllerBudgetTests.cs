using System.Net;
using System.Text.Json;
using Maroik.Core.Domain.Accounts;
using Maroik.Core.PostgreSQL.Contexts;
using Maroik.Website.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// Integration tests for the budget actions of <c>AccountBookController</c> against a real PostgreSQL database: the Income Budget and
/// Expenditure Budget pages (menu, User role), the sheet read, the formula preview, cell edits, paste / fill, lines, the carry-over and the
/// Excel download — each scoped to the signed-in account, its messages localized.
/// </summary>
[Collection("Website Integration")]
public class AccountBookControllerBudgetTests(MaroikWebApplicationFactory factory)
{
    /// <summary>Client for the shared test host; redirects are not followed so they can be asserted.</summary>
    private readonly HttpClient _client = factory.CreateTestClient(followRedirects: false);

    /// <summary>The row key of labor income in KRW.</summary>
    private const string LaborKey = "L.RegularIncome.LaborIncome.4B5257";

    /// <summary>Signs in a fresh account (unique per test) that owns one KRW asset, so its new sheets get KRW lines.</summary>
    private async Task<(AuthenticatedSession Session, string Email)> LoginAsync([System.Runtime.CompilerServices.CallerMemberName] string test = "")
    {
        string email = $"budget-{Math.Abs(test.GetHashCode(StringComparison.Ordinal))}-{Guid.NewGuid():N}"[..40].ToLowerInvariant() + "@test.com";
        AuthenticatedSession session = await AuthenticatedSessionHelper.LoginAsync(factory, _client, email, "UserPassword1!", Role.User, TestContext.Current.CancellationToken);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Assets.Add(new Maroik.Core.PostgreSQL.Models.Asset
        {
            ProductName = "Wallet", AccountEmail = email, Item = "FreeDepositAndWithdrawal", Amount = 0, MonetaryUnit = "KRW",
            Created = DateTime.UtcNow, Updated = DateTime.UtcNow, Note = "", Deleted = false,
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (session, email);
    }

    /// <summary>POSTs a JSON body (ko-KR unless told otherwise) and returns the parsed reply.</summary>
    private async Task<JsonElement> PostAsync(AuthenticatedSession session, string url, object body, string culture = "ko-KR")
    {
        using var request = session.BuildJsonPostRequest(url, body);
        request.Headers.AcceptLanguage.ParseAdd(culture);
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return doc.RootElement.Clone();
    }

    /// <summary>The cell with <paramref name="address"/> in a list of cells.</summary>
    private static JsonElement Cell(JsonElement cells, string address) => cells.EnumerateArray().Single(c => c.GetProperty("address").GetString() == address);

    /// <summary>The stored cell inputs of an account's sheet, by "row key|column".</summary>
    private Dictionary<string, string> StoredInputs(string email, string kind, int year)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return db.BudgetSheetCells.AsNoTracking().Where(c => c.Sheet.AccountEmail == email && c.Sheet.Kind == kind && c.Sheet.Year == year)
            .ToDictionary(c => c.RowKey + "|" + c.Column, c => c.Input);
    }

    /// <summary>Both pages render the one budget view, told their kind and the current year; the menu lets only Users in.</summary>
    [Theory]
    [InlineData("/AccountBook/IncomeBudget", "Income")]
    [InlineData("/AccountBook/ExpenditureBudget", "Expenditure")]
    public async Task ThePages_RenderTheBudgetView(string url, string kind)
    {
        var (session, _) = await LoginAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Cookie", session.CookieHeader);
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("id=\"budgetGrid\"", html, StringComparison.Ordinal);
        Assert.Contains($"id=\"budgetKind\" name=\"budgetKind\" type=\"hidden\" value=\"{kind}\"", html, StringComparison.Ordinal);
        Assert.Contains($"value=\"{DateTime.UtcNow.Year}\"", html, StringComparison.Ordinal);
        Assert.Contains("custom/AccountBook/Budget/js/site.js", html, StringComparison.Ordinal);
    }

    /// <summary>An administrator is not let into the User-only pages.</summary>
    [Fact]
    public async Task ThePages_AreNotForAdministrators()
    {
        AuthenticatedSession admin = await AuthenticatedSessionHelper.LoginAsync(factory, _client, "budget-admin@test.com", "UserPassword1!", Role.Admin, TestContext.Current.CancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/AccountBook/IncomeBudget");
        request.Headers.Add("Cookie", admin.CookieHeader);
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Dashboard/AnonymousIndex", response.Headers.Location!.OriginalString);
    }

    /// <summary>A sheet not stored yet reads with its default KRW lines, labels localized.</summary>
    [Fact]
    public async Task GetBudgetSheet_ReadsTheDefaultLines()
    {
        var (session, _) = await LoginAsync();

        JsonElement reply = await PostAsync(session, "/AccountBook/GetBudgetSheet", new { Kind = "Income", Year = 2026 });

        Assert.True(reply.GetProperty("result").GetBoolean());
        JsonElement sheet = reply.GetProperty("sheet");
        Assert.False(sheet.GetProperty("exists").GetBoolean());
        JsonElement line = sheet.GetProperty("rows").EnumerateArray().First(r => r.GetProperty("kind").GetString() == "Line");
        Assert.Equal(("근로수입", "KRW", 2), (line.GetProperty("label").GetString(), line.GetProperty("currency").GetString(), line.GetProperty("number").GetInt32()));
        Assert.Equal("Budget", Cell(sheet.GetProperty("cells"), "B2").GetProperty("role").GetString());
    }

    /// <summary>A bad kind or year is refused.</summary>
    [Fact]
    public async Task GetBudgetSheet_RefusesBadInput()
    {
        var (session, _) = await LoginAsync();

        JsonElement badKind = await PostAsync(session, "/AccountBook/GetBudgetSheet", new { Kind = "Savings", Year = 2026 });
        JsonElement badYear = await PostAsync(session, "/AccountBook/GetBudgetSheet", new { Kind = "Income", Year = 0 }, "en-US");

        Assert.Equal("유효하지 않은 입력값입니다.", badKind.GetProperty("error").GetString());
        Assert.Equal("The year must be between 1 and 9999.", badYear.GetProperty("error").GetString());
    }

    /// <summary>An edit stores the sheet and the input (canonically), and replies the changed cells ready to draw.</summary>
    [Fact]
    public async Task SetBudgetCells_StoresTheInput_AndRepliesTheChangedCells()
    {
        var (session, email) = await LoginAsync();

        JsonElement reply = await PostAsync(session, "/AccountBook/SetBudgetCells", new { Kind = "Income", Year = 2026, Edits = new[] { new { Address = "B2", Input = "3,000,000" }, new { Address = "C2", Input = "=B2*1.1" } } });

        Assert.True(reply.GetProperty("result").GetBoolean());
        JsonElement changed = reply.GetProperty("changed");
        Assert.Equal("3,000,000", Cell(changed, "B2").GetProperty("display").GetString());
        Assert.Equal(("3,300,000", "=B2*1.1"), (Cell(changed, "C2").GetProperty("display").GetString(), Cell(changed, "C2").GetProperty("input").GetString()));
        Assert.Equal("6,300,000", Cell(changed, "N2").GetProperty("display").GetString());
        Dictionary<string, string> stored = StoredInputs(email, "Income", 2026);
        Assert.Equal(("3000000", "=B{" + LaborKey + "}*1.1"), (stored[LaborKey + "|B"], stored[LaborKey + "|C"]));
    }

    /// <summary>An input the cell refuses is answered with the localized reason and nothing is stored.</summary>
    [Fact]
    public async Task SetBudgetCells_RefusesABadInput()
    {
        var (session, email) = await LoginAsync();

        JsonElement reply = await PostAsync(session, "/AccountBook/SetBudgetCells", new { Kind = "Income", Year = 2026, Edits = new[] { new { Address = "B2", Input = "lunch" } } });
        JsonElement formula = await PostAsync(session, "/AccountBook/SetBudgetCells", new { Kind = "Income", Year = 2026, Edits = new[] { new { Address = "B2", Input = "=1+" } } }, "en-US");

        Assert.False(reply.GetProperty("result").GetBoolean());
        Assert.Equal("예산 셀에는 숫자나 수식만 넣을 수 있습니다.", reply.GetProperty("error").GetString());
        Assert.Equal("A value or reference is expected here.", formula.GetProperty("error").GetString());
        Assert.Empty(StoredInputs(email, "Income", 2026));
    }

    /// <summary>The preview colours the input, localizes its problems and shows the value it would have.</summary>
    [Fact]
    public async Task PreviewBudgetCell_ColoursAndCalculates()
    {
        var (session, _) = await LoginAsync();
        await PostAsync(session, "/AccountBook/SetBudgetCells", new { Kind = "Income", Year = 2026, Edits = new[] { new { Address = "B2", Input = "100" } } });

        JsonElement reply = await PostAsync(session, "/AccountBook/PreviewBudgetCell", new { Kind = "Income", Year = 2026, Address = "C2", Input = "=B2*2+FOO(1)" });
        JsonElement ok = await PostAsync(session, "/AccountBook/PreviewBudgetCell", new { Kind = "Income", Year = 2026, Address = "C2", Input = "=B2*2" });

        JsonElement preview = reply.GetProperty("preview");
        Assert.Equal("reference", preview.GetProperty("tokens")[1].GetProperty("class").GetString());
        Assert.Equal(("B2", "B2"), (preview.GetProperty("references")[0].GetProperty("topLeft").GetString(), preview.GetProperty("references")[0].GetProperty("bottomRight").GetString()));
        Assert.Equal("FOO 함수가 없습니다.", preview.GetProperty("problems")[0].GetProperty("message").GetString());
        Assert.Equal("#NAME?", preview.GetProperty("value").GetString());
        Assert.Equal("200", ok.GetProperty("preview").GetProperty("value").GetString());
    }

    /// <summary>Fill right copies a formula across with its references moved, and stores the copies.</summary>
    [Fact]
    public async Task CopyBudgetCells_FillsRight()
    {
        var (session, email) = await LoginAsync();
        await PostAsync(session, "/AccountBook/SetBudgetCells", new { Kind = "Income", Year = 2026, Edits = new[] { new { Address = "B2", Input = "100" }, new { Address = "C2", Input = "=B2+1" } } });

        JsonElement reply = await PostAsync(session, "/AccountBook/CopyBudgetCells", new { Kind = "Income", Year = 2026, Source = "C2", Target = "D2:E2" });

        Assert.Equal("102", Cell(reply.GetProperty("changed"), "D2").GetProperty("display").GetString());
        Assert.Equal("=D{" + LaborKey + "}+1", StoredInputs(email, "Income", 2026)[LaborKey + "|E"]);
    }

    /// <summary>A currency line is added and removed; a line the sheet has is refused.</summary>
    [Fact]
    public async Task Lines_AreAddedAndRemoved()
    {
        var (session, _) = await LoginAsync();

        JsonElement added = await PostAsync(session, "/AccountBook/AddBudgetLine", new { Kind = "Expenditure", Year = 2026, MainClass = "ConsumerSpending", SubClass = "PinMoney", Currency = "USD" });
        JsonElement twice = await PostAsync(session, "/AccountBook/AddBudgetLine", new { Kind = "Expenditure", Year = 2026, MainClass = "ConsumerSpending", SubClass = "PinMoney", Currency = "USD" });
        JsonElement removed = await PostAsync(session, "/AccountBook/RemoveBudgetLine", new { Kind = "Expenditure", Year = 2026, MainClass = "ConsumerSpending", SubClass = "PinMoney", Currency = "USD" });

        Assert.Contains(added.GetProperty("sheet").GetProperty("rows").EnumerateArray(), r => r.GetProperty("currency").GetString() == "USD" && r.GetProperty("kind").GetString() == "Line");
        Assert.Equal("줄을 추가했습니다.", added.GetProperty("message").GetString());
        Assert.Equal("이 시트에 이미 그 줄이 있습니다.", twice.GetProperty("error").GetString());
        Assert.Equal("줄을 삭제했습니다.", removed.GetProperty("message").GetString());
        Assert.DoesNotContain(removed.GetProperty("sheet").GetProperty("rows").EnumerateArray(), r => r.GetProperty("currency").GetString() == "USD");
    }

    /// <summary>Carry-over fills this year from last year and says how many cells.</summary>
    [Fact]
    public async Task CarryOverBudget_FillsFromLastYear()
    {
        var (session, _) = await LoginAsync();
        await PostAsync(session, "/AccountBook/SetBudgetCells", new { Kind = "Income", Year = 2025, Edits = new[] { new { Address = "B2", Input = "7" }, new { Address = "C2", Input = "8" } } });

        JsonElement reply = await PostAsync(session, "/AccountBook/CarryOverBudget", new { Kind = "Income", Year = 2026 });

        Assert.Equal("작년 예산에서 2개 셀을 채웠습니다.", reply.GetProperty("message").GetString());
        Assert.Equal("7", Cell(reply.GetProperty("sheet").GetProperty("cells"), "B2").GetProperty("display").GetString());
    }

    /// <summary>Another account's sheet is never read.</summary>
    [Fact]
    public async Task Sheets_AreOwnerScoped()
    {
        var (other, _) = await LoginAsync("other");
        await PostAsync(other, "/AccountBook/SetBudgetCells", new { Kind = "Income", Year = 2026, Edits = new[] { new { Address = "B2", Input = "99" } } });
        var (session, _) = await LoginAsync();

        JsonElement reply = await PostAsync(session, "/AccountBook/GetBudgetSheet", new { Kind = "Income", Year = 2026 });

        Assert.False(reply.GetProperty("sheet").GetProperty("exists").GetBoolean());
        Assert.Equal("", Cell(reply.GetProperty("sheet").GetProperty("cells"), "B2").GetProperty("display").GetString());
    }

    /// <summary>The Excel download is the sheet as an .xlsx file named after the given name.</summary>
    [Fact]
    public async Task ExportExcelBudget_DownloadsTheSheet()
    {
        var (session, _) = await LoginAsync();
        await PostAsync(session, "/AccountBook/SetBudgetCells", new { Kind = "Income", Year = 2026, Edits = new[] { new { Address = "B2", Input = "5" } } });

        using var request = session.BuildJsonPostRequest("/AccountBook/ExportExcelBudget?kind=Income&year=2026&fileName=budget");
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType!.MediaType);
        Assert.StartsWith("budget-", response.Content.Headers.ContentDisposition!.FileNameStar ?? response.Content.Headers.ContentDisposition.FileName!.Trim('"'), StringComparison.Ordinal);
        byte[] bytes = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        Assert.True(bytes.Length > 4 && bytes[0] == 'P' && bytes[1] == 'K', "an .xlsx file is a zip container starting with 'PK'");
    }

    /// <summary>An Excel download of a bad kind is refused.</summary>
    [Fact]
    public async Task ExportExcelBudget_RefusesABadKind()
    {
        var (session, _) = await LoginAsync();

        using var request = session.BuildJsonPostRequest("/AccountBook/ExportExcelBudget?kind=Savings&year=2026");
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
