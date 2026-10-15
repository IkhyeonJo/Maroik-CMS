using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Maroik.Core.Domain.Account;
using Maroik.Core.Domain.Finance;
using Maroik.Website.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// The main-class drop-downs of the income / expenditure forms (one-off and fixed, create and edit) offer exactly
/// <see cref="IncomeClassPolicy.MainClasses"/> / <see cref="ExpenditureClassPolicy.MainClasses"/>, in that order, each
/// labelled through the page's resx, with the first one pre-selected — the class list lives in the Domain policy, not
/// in the view.
/// </summary>
[Collection("Website Integration")]
public class FinanceMainClassOptionsTests(MaroikWebApplicationFactory factory)
{
    /// <summary>The Korean label of every main class (the four pages' ko-KR resx agree).</summary>
    private static readonly Dictionary<string, string> _koreanLabels = new()
    {
        ["RegularIncome"] = "정기수입", ["IrregularIncome"] = "비정기수입",
        ["RegularSavings"] = "정기저축", ["NonConsumerSpending"] = "비소비지출", ["ConsumerSpending"] = "소비지출",
    };

    /// <summary>Each row: the page, its create / edit main-class select, and whether it is an income page.</summary>
    public static TheoryData<string, string, bool> Selects => new()
    {
        { "/AccountBook/Income", "createIncomeMainClass", true },
        { "/AccountBook/Income", "editIncomeMainClass", true },
        { "/AccountBook/Expenditure", "createExpenditureMainClass", false },
        { "/AccountBook/Expenditure", "editExpenditureMainClass", false },
        { "/Notice/FixedIncome", "createFixedIncomeMainClass", true },
        { "/Notice/FixedIncome", "editFixedIncomeMainClass", true },
        { "/Notice/FixedExpenditure", "createFixedExpenditureMainClass", false },
        { "/Notice/FixedExpenditure", "editFixedExpenditureMainClass", false },
    };

    /// <summary>The select offers the policy's main classes in order, labelled in Korean, the first selected.</summary>
    [Theory]
    [MemberData(nameof(Selects))]
    public async Task MainClassSelect_OffersThePolicysMainClasses(string url, string selectId, bool income)
    {
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://www.localhost/"), AllowAutoRedirect = false, HandleCookies = false
        });
        var session = await AuthenticatedSessionHelper.LoginAsync(factory, client, "main-class-options@test.com", "UserPassword1!", Role.User, TestContext.Current.CancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Cookie", session.CookieHeader);
        request.Headers.AcceptLanguage.ParseAdd("ko-KR");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var page = await new HtmlParser().ParseDocumentAsync(html, TestContext.Current.CancellationToken);

        var select = Assert.IsType<IHtmlSelectElement>(page.GetElementById(selectId), exactMatch: false);
        IReadOnlyList<string> expected = income ? IncomeClassPolicy.MainClasses : ExpenditureClassPolicy.MainClasses;
        Assert.Equal(expected, select.Options.Select(o => o.Value));
        Assert.Equal(expected.Select(m => _koreanLabels[m]), select.Options.Select(o => o.Text.Trim()));
        Assert.Equal(0, select.SelectedIndex);
    }
}
