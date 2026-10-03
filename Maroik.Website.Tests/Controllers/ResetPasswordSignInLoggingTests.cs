using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Misc.Helpers;
using Maroik.Core.Domain.Account;
using Maroik.Core.PostgreSQL.Data;
using Maroik.Website.Controllers;
using Maroik.Website.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// Signing a user in after a successful password reset is a security event: <see cref="AccountController"/> logs
/// it once at Information with the account's e-mail, and never the new password or the reset token. The
/// controller's logger is a <see cref="FakeLogger{T}"/> in one derived host shared by the class.
/// </summary>
[Collection("Website Integration")]
public class ResetPasswordSignInLoggingTests(MaroikWebApplicationFactory factory)
{
    /// <summary>Captures what <see cref="AccountController"/> logs; shared with <see cref="_sharedHost"/>.</summary>
    private static readonly FakeLogger<AccountController> _logger = new();

    /// <summary>The one derived host the tests of this class share (each derived host is a full start-up).</summary>
    private static WebApplicationFactory<Program>? _sharedHost;

    /// <summary>Guards the one-time creation of <see cref="_sharedHost"/>.</summary>
    private static readonly Lock _sharedHostLock = new();

    /// <summary>The host whose <see cref="AccountController"/> logs to <see cref="_logger"/>.</summary>
    private readonly WebApplicationFactory<Program> _host = SharedHost(factory);

    /// <summary>Returns <see cref="_sharedHost"/>, deriving it from <paramref name="factory"/> on first use.</summary>
    private static WebApplicationFactory<Program> SharedHost(MaroikWebApplicationFactory factory)
    {
        lock (_sharedHostLock)
        {
            return _sharedHost ??= factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
                services.AddSingleton<ILogger<AccountController>>(_logger)));
        }
    }

    /// <summary>A successful reset that signs the user in logs it once, naming the account and nothing secret.</summary>
    [Fact]
    public async Task ResetPassword_SigningTheUserIn_IsLoggedOnce_WithoutThePasswordOrToken()
    {
        string email = $"reset-signin-{Guid.NewGuid():N}@test.com";
        string rawToken = GuidToken.Generate(DateTime.UtcNow);
        string encryptedToken;
        using (var scope = _host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Accounts.Add(new Maroik.Core.PostgreSQL.Models.Account
            {
                Email = email,
                HashedPassword = scope.ServiceProvider.GetRequiredService<IPasswordService>().HashPassword("OldPassword1!"),
                Nickname = "ResetSignIn_" + Guid.NewGuid().ToString("N")[..12],
                AvatarImagePath = "/upload/Management/Profile/default-avatar.jpg",
                Role = Role.User,
                TimeZoneIanaId = "UTC",
                EmailConfirmed = true,
                AgreedServiceTerms = true,
                ResetPasswordToken = rawToken,
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            encryptedToken = scope.ServiceProvider.GetRequiredService<IRsaService>().Encrypt(rawToken);
        }
        using HttpClient client = _host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://www.localhost/"), AllowAutoRedirect = false, HandleCookies = false
        });
        var anonymous = await AuthenticatedSessionHelper.AnonymousAsync(client, TestContext.Current.CancellationToken);
        var form = new MultipartFormDataContent
        {
            { new StringContent(encryptedToken), "ResetPasswordToken" },
            { new StringContent("BrandNewPassword1!"), "Password" }
        };
        using var request = anonymous.BuildFormPostRequest("/Account/ResetPassword", form);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        FakeLogRecord record = Assert.Single(_logger.Collector.GetSnapshot(),
            r => r.Message.Contains("after password reset", StringComparison.Ordinal) && r.Message.Contains(email, StringComparison.Ordinal));
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.All(_logger.Collector.GetSnapshot(), r =>
        {
            Assert.DoesNotContain("BrandNewPassword1!", r.Message);
            Assert.DoesNotContain(rawToken, r.Message);
            Assert.DoesNotContain(encryptedToken, r.Message);
        });
    }
}
