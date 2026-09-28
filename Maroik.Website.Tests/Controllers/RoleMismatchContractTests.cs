using System.Text.Json;
using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Account;
using Maroik.Website.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// The write actions re-check the role of the (DB-fresh) account the request runs as, and refuse one that is neither
/// Admin nor User. The authorization filter and the database's own CHECK constraint make such an account impossible
/// over plain HTTP, so a test-only action filter — placed after the site's own filters, and only for requests that
/// carry <c>X-Test-Ghost</c> — swaps the account on the controller's ViewBag for one with an unknown role.
/// </summary>
[Collection("Website Integration")]
public class RoleMismatchContractTests(MaroikWebApplicationFactory factory)
{
    private sealed class GhostRoleFilter : IAsyncActionFilter
    {
        /// <summary>Replaces the logged-in account on the ViewBag with an unknown-role copy when the request carries <c>X-Test-Ghost</c>; otherwise just continues.</summary>
        public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (!context.HttpContext.Request.Headers.ContainsKey("X-Test-Ghost") || context.Controller is not Controller controller)
            {
                return next();
            }
            object? current = controller.ViewBag.LoggedInAccount;
            if (current is AccountResponse account)
                controller.ViewBag.LoggedInAccount = new AccountResponse
                {
                    Email = account.Email, Nickname = account.Nickname, TimeZoneIanaId = account.TimeZoneIanaId, Role = "Ghost",
                };
            return next();
        }
    }

    private static readonly string[] _writeEndpoints =
    [
        "/Forum/WriteFreeBoard", "/Forum/EditFreeBoard", "/Forum/WriteFreeComment",
        "/Management/WritePrivateNoteBoard", "/Management/EditPrivateNoteBoard", "/Management/WritePrivateNoteComment",
        "/Calendar/CreateCalendarEvent", "/Calendar/UpdateCalendarEvent", "/Calendar/UpdateOtherCalendar",
    ];

    /// <summary>An account with an unknown role is told to log in on every write endpoint that re-checks it, and nothing is written.</summary>
    [Fact]
    public async Task AWriteByAnAccountWithAnUnknownRole_IsRefusedAsNotLoggedIn()
    {
        await using var host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
            services.Configure<MvcOptions>(o => o.Filters.Add(new GhostRoleFilter()))));
        var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://www.localhost/"), AllowAutoRedirect = false, HandleCookies = false
        });
        var sessions = new Dictionary<string, AuthenticatedSession>();
        foreach (string role in new[] { Role.User, Role.Admin })
            sessions[role] = await AuthenticatedSessionHelper.LoginAsync(host, client, $"role-mismatch-{role.ToLowerInvariant()}@test.com", "UserPassword1!", role, TestContext.Current.CancellationToken);

        List<string> problems = [];
        var endpoints = ControllerFailureContractTests.BodyEndpoints.Where(e => _writeEndpoints.Contains(e.Url)).ToList();
        Assert.Equal(_writeEndpoints.Length, endpoints.Count);
        foreach (var (role, url, build) in endpoints)
        {
            using var request = build(sessions[role]);
            request.Headers.Add("X-Test-Ghost", "1");
            var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            if (response.StatusCode != System.Net.HttpStatusCode.OK) { problems.Add($"{url}: HTTP {(int)response.StatusCode}"); continue; }
            using JsonDocument doc = JsonDocument.Parse(json);
            if (doc.RootElement.GetProperty("result").GetBoolean() || !json.Contains("Login", StringComparison.OrdinalIgnoreCase)) problems.Add($"{url}: {json}");
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }
}
