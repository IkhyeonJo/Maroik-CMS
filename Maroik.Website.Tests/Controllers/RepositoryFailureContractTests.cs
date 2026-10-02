using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Account;
using Maroik.Core.PostgreSQL.Data;
using Maroik.Website.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// The service layer turns a repository (database) failure in a few places into a failure *result* rather than an
/// exception — confirming an e-mail address, resetting a password, deleting a menu entry. Each repository here is the real
/// one wrapped in a decorator that throws from exactly one named method, in a derived host sharing the same database, so
/// the controller's handling of that result is exercised end to end.
/// </summary>
[Collection("Website Integration")]
public class RepositoryFailureContractTests(MaroikWebApplicationFactory factory)
{
    /// <summary>Message of every exception the failing repository methods throw; it must never reach the response.</summary>
    private const string DatabaseSecret = "db-secret-do-not-leak";

    /// <summary>Forwards every call to the real repository, except the named methods, which throw.</summary>
    public class FailingRepositoryProxy : DispatchProxy
    {
        /// <summary>The real repository.</summary>
        public object Target { get; set; } = null!;

        /// <summary>The names of the methods that throw.</summary>
        public HashSet<string> FailingMethods { get; set; } = [];

        /// <inheritdoc />
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (FailingMethods.Contains(targetMethod!.Name)) throw new InvalidOperationException(DatabaseSecret);
            try { return targetMethod.Invoke(Target, args); }
            catch (TargetInvocationException e) { throw e.InnerException ?? e; }
        }
    }

    /// <summary>A host whose <typeparamref name="TRepository"/> forwards to the real repository except for <paramref name="failingMethods"/>, which throw.</summary>
    private WebApplicationFactory<Program> HostWithFailing<TRepository>(params string[] failingMethods) where TRepository : class =>
        factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
        {
            ServiceDescriptor real = services.Single(d => d.ServiceType == typeof(TRepository));
            services.Remove(real);
            services.AddScoped(sp =>
            {
                object target = real.ImplementationFactory != null
                    ? real.ImplementationFactory(sp)
                    : ActivatorUtilities.CreateInstance(sp, real.ImplementationType!);
                var proxy = DispatchProxy.Create<TRepository, FailingRepositoryProxy>();
                var typed = (FailingRepositoryProxy)(object)proxy;
                typed.Target = target;
                typed.FailingMethods = [.. failingMethods];
                return proxy;
            });
        }));

    /// <summary>GETs <paramref name="url"/> and returns its antiforgery cookie (<c>name=value</c>) and form token.</summary>
    private static async Task<(string Cookie, string Token)> AntiForgeryAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url, TestContext.Current.CancellationToken);
        string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
 #pragma warning disable SYSLIB1045
        Match token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
 #pragma warning restore SYSLIB1045
        Assert.True(token.Success);
        string cookie = response.Headers.GetValues("Set-Cookie")
            .Select(h => h.Split(';', 2)[0]).First(c => c.StartsWith("__Secure-.AspNetCore.Antiforgery.", StringComparison.Ordinal));
        return (cookie, token.Groups[1].Value);
    }

    /// <summary>Inserts a User account for <paramref name="email"/> (password "OldPassword1!") directly into the database with the given tokens and confirmation state.</summary>
    private async Task SeedAccountAsync(string email, string? registrationToken = null, string? resetPasswordToken = null, bool emailConfirmed = true)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Accounts.Add(new Maroik.Core.PostgreSQL.Models.Account
        {
            Email = email, HashedPassword = scope.ServiceProvider.GetRequiredService<IPasswordService>().HashPassword("OldPassword1!"),
            Nickname = "Fail_" + Guid.NewGuid().ToString("N")[..10], AvatarImagePath = "/upload/Management/Profile/default-avatar.jpg",
            Role = Role.User, TimeZoneIanaId = "UTC", Locked = false, LoginAttempt = 0, EmailConfirmed = emailConfirmed, AgreedServiceTerms = true,
            RegistrationToken = registrationToken, ResetPasswordToken = resetPasswordToken, Deleted = false, Created = DateTime.UtcNow, Updated = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>A confirmation whose write fails shows the service's error on the page and leaves the account unconfirmed.</summary>
    [Fact]
    public async Task ConfirmEmail_WhenTheConfirmationWriteFails_ShowsTheErrorAndKeepsTheAccountUnconfirmed()
    {
        string email = $"confirm-fail-{Guid.NewGuid():N}@test.com";
        string rawToken = GuidToken.Generate(DateTime.UtcNow);
        await SeedAccountAsync(email, registrationToken: rawToken, emailConfirmed: false);
        await using var host = HostWithFailing<IAccountRepository>(nameof(IAccountRepository.UpdateEmailConfirmationAsync));
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://www.localhost/"), AllowAutoRedirect = false, HandleCookies = false });
        string encrypted = host.Services.CreateScope().ServiceProvider.GetRequiredService<IRsaService>().Encrypt(rawToken);

        var (cookie, token) = await AntiForgeryAsync(client, $"/Account/ConfirmEmail?registrationToken={Uri.EscapeDataString(encrypted)}");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/Account/ConfirmEmail");
        request.Headers.Add("Cookie", cookie);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["RegistrationToken"] = encrypted, ["Password"] = "OldPassword1!", ["__RequestVerificationToken"] = token,
        });
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Error occurred while processing about confirm email account status", html);
        Assert.DoesNotContain(DatabaseSecret, html);
        using var scope = factory.Services.CreateScope();
        Assert.False(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Accounts.Single(a => a.Email == email).EmailConfirmed);
    }

    /// <summary>A password reset whose locked re-read fails keeps the form and the token (the token itself was fine) and leaks nothing.</summary>
    [Fact]
    public async Task ResetPassword_WhenTheLockedReadFails_ReShowsTheFormAndKeepsTheToken()
    {
        string email = $"reset-fail-{Guid.NewGuid():N}@test.com";
        string rawToken = GuidToken.Generate(DateTime.UtcNow);
        await SeedAccountAsync(email, resetPasswordToken: rawToken);
        await using var host = HostWithFailing<IAccountRepository>(nameof(IAccountRepository.FindByEmailForUpdateAsync));
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://www.localhost/"), AllowAutoRedirect = false, HandleCookies = false });
        string encrypted = host.Services.CreateScope().ServiceProvider.GetRequiredService<IRsaService>().Encrypt(rawToken);
        var (cookie, token) = await AntiForgeryAsync(client, "/Account/ResetPassword");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/Account/ResetPassword");
        request.Headers.Add("Cookie", cookie);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ResetPasswordToken"] = encrypted, ["Password"] = "BrandNewPassword1!", ["__RequestVerificationToken"] = token,
        });
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("resetPasswordForm", html);
        Assert.DoesNotContain("The authentication token is invalid.", html);
        Assert.DoesNotContain(DatabaseSecret, html);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(rawToken, scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Accounts.Single(a => a.Email == email).ResetPasswordToken);
    }

    /// <summary>A category JSON body with id <paramref name="id"/>.</summary>
    private static object Category(string name, long id) => new { Id = id, Name = name, DisplayName = name, IconPath = "/i.png", Controller = "Notice", Action = "", Role = Role.User, Order = 500 };
    /// <summary>A sub-category JSON body with id <paramref name="id"/> under <paramref name="categoryId"/>.</summary>
    private static object SubCategory(string name, long categoryId, long id) => new { Id = id, CategoryId = categoryId, Name = name, DisplayName = name, IconPath = "/i.png", Controller = "", Action = "Index", Role = Role.User, Order = 500 };

    /// <summary>A menu delete whose database write fails answers with the generic message and leaks nothing.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MenuDelete_WhenTheDatabaseWriteFails_AnswersWithTheGenericMessage(bool category)
    {
        await using var host = category
            ? HostWithFailing<ICategoryRepository>(nameof(ICategoryRepository.DeleteByIdAsync))
            : HostWithFailing<ISubCategoryRepository>(nameof(ISubCategoryRepository.DeleteByIdAsync));
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://www.localhost/"), AllowAutoRedirect = false, HandleCookies = false });
        var admin = await AuthenticatedSessionHelper.LoginAsync(host, client, $"menu-delete-fail-{(category ? "category" : "sub")}@test.com", "AdminPassword1!", Role.Admin, TestContext.Current.CancellationToken);

        using var request = category
            ? admin.BuildJsonPostRequest("/Management/DeleteCategory", Category("x", 987654321))
            : admin.BuildJsonPostRequest("/Management/DeleteSubCategory", SubCategory("x", 1, 987654321));
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.False(JsonDocument.Parse(json).RootElement.GetProperty("result").GetBoolean());
        Assert.Contains("A temporary error occurred. Please try again later.", json);
        Assert.DoesNotContain(DatabaseSecret, json);
    }
}
