using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Account;
using Maroik.Website.Contracts;
using Maroik.Website.Middlewares;
using Microsoft.AspNetCore.Http;
using Moq;

namespace Maroik.Website.Tests.Middlewares;

/// <summary>
/// Unit tests for <see cref="RoleBasedStaticFileMiddleware"/>, which blocks (403) requests
/// under <c>/admin/</c> for non-Admin sessions and under <c>/user/</c> for non-User sessions,
/// and forwards every other request unchanged. The role is the database's current one, re-validated
/// the way AuthorizationFilter does it, not the snapshot the session took at login.
/// </summary>
public class RoleBasedStaticFileMiddlewareTests
{
    /// <summary>Mock <c>ISessionService</c> injected into the system under test.</summary>
    private readonly Mock<ISessionService> _sessionService = new();
    /// <summary>Mock <c>IAccountService</c>: the database's current copy of the session's account.</summary>
    private readonly Mock<IAccountService> _accountService = new();

    /// <summary>The security stamp the session was signed in with (and, unless a test changes it, the database's).</summary>
    private const string Stamp = "stamp-at-login";

    /// <summary>A request context for <paramref name="path"/>.</summary>
    private static DefaultHttpContext BuildContext(string path) =>
        new() { Request = { Path = path } };

    /// <summary>
    /// Makes the session hold an account with <paramref name="role"/>, or none when it is <see langword="null"/>;
    /// the database holds the same, unchanged account.
    /// </summary>
    private void SetupAccount(string? role)
    {
        _sessionService
            .Setup(s => s.GetAccount())
            .Returns(role == null ? null : new AccountResponse { Email = "test@test.com", Nickname = "Test", Role = role, SecurityStamp = Stamp });
        SetupDatabaseAccount(role == null ? null : new AccountResponse { Email = "test@test.com", Nickname = "Test", Role = role, SecurityStamp = Stamp });
    }

    /// <summary>Makes the database return <paramref name="account"/> for the session's email.</summary>
    private void SetupDatabaseAccount(AccountResponse? account) =>
        _accountService
            .Setup(a => a.GetAccountByEmailAsync("test@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

    /// <summary>Runs the middleware on <paramref name="path"/>; true when it let the request through to the next one.</summary>
    private async Task<(bool NextCalled, int StatusCode)> InvokeAsync(string path)
    {
        var context = BuildContext(path);
        bool nextCalled = false;
        var sut = new RoleBasedStaticFileMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await sut.InvokeAsync(context, _sessionService.Object, _accountService.Object);

        return (nextCalled, context.Response.StatusCode);
    }

    /// <summary>
    /// An admin demoted to User after signing in keeps the session's Admin snapshot; the database's current
    /// role decides, so the admin assets are refused at once.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_AdminPath_SessionSaysAdminButDatabaseSaysUser_Blocks()
    {
        SetupAccount(Role.Admin);
        SetupDatabaseAccount(new AccountResponse { Email = "test@test.com", Role = Role.User, SecurityStamp = Stamp });

        var (nextCalled, status) = await InvokeAsync("/admin/dist/img/logo.png");

        Assert.False(nextCalled);
        Assert.Equal(403, status);
    }

    /// <summary>The reverse: a User promoted to Admin gets the admin assets without signing in again.</summary>
    [Fact]
    public async Task InvokeAsync_AdminPath_SessionSaysUserButDatabaseSaysAdmin_Forwards()
    {
        SetupAccount(Role.User);
        SetupDatabaseAccount(new AccountResponse { Email = "test@test.com", Role = Role.Admin, SecurityStamp = Stamp });

        var (nextCalled, _) = await InvokeAsync("/admin/dist/img/logo.png");

        Assert.True(nextCalled);
    }

    /// <summary>
    /// A session whose account no longer exists, is deleted, or was re-stamped (password changed, admin lock)
    /// is no signed-in account at all — the same rule AuthorizationFilter applies.
    /// </summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("deleted")]
    [InlineData("restamped")]
    public async Task InvokeAsync_AdminPath_SessionNoLongerValid_Blocks(string state)
    {
        SetupAccount(Role.Admin);
        SetupDatabaseAccount(state switch
        {
            "missing" => null,
            "deleted" => new AccountResponse { Email = "test@test.com", Role = Role.Admin, SecurityStamp = Stamp, Deleted = true },
            _ => new AccountResponse { Email = "test@test.com", Role = Role.Admin, SecurityStamp = "stamp-after-password-change" },
        });

        var (nextCalled, status) = await InvokeAsync("/admin/dist/img/logo.png");

        Assert.False(nextCalled);
        Assert.Equal(403, status);
    }

    /// <summary>Only the role-gated folders cost a database read; every other static file is served without one.</summary>
    [Fact]
    public async Task InvokeAsync_UnrestrictedPath_DoesNotReadTheDatabase()
    {
        SetupAccount(Role.Admin);

        var (nextCalled, _) = await InvokeAsync("/anonymous/dist/img/logo.png");

        Assert.True(nextCalled);
        _accountService.Verify(a => a.GetAccountByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Invoke async admin path admin session forwards.</summary>
    [Fact]
    public async Task InvokeAsync_AdminPath_AdminSession_Forwards()
    {
        SetupAccount(Role.Admin);
        var context = BuildContext("/admin/dist/img/logo.png");
        bool nextCalled = false;
        var sut = new RoleBasedStaticFileMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await sut.InvokeAsync(context, _sessionService.Object, _accountService.Object);

        Assert.True(nextCalled);
        Assert.Equal(200, context.Response.StatusCode);
    }

    /// <summary>Invoke async admin path user session blocks.</summary>
    [Fact]
    public async Task InvokeAsync_AdminPath_UserSession_Blocks()
    {
        SetupAccount(Role.User);
        var context = BuildContext("/admin/dist/img/logo.png");
        bool nextCalled = false;
        var sut = new RoleBasedStaticFileMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await sut.InvokeAsync(context, _sessionService.Object, _accountService.Object);

        Assert.False(nextCalled);
        Assert.Equal(403, context.Response.StatusCode);
    }

    /// <summary>Invoke async admin path no session blocks.</summary>
    [Fact]
    public async Task InvokeAsync_AdminPath_NoSession_Blocks()
    {
        SetupAccount(null);
        var context = BuildContext("/admin/dist/img/logo.png");
        bool nextCalled = false;
        var sut = new RoleBasedStaticFileMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await sut.InvokeAsync(context, _sessionService.Object, _accountService.Object);

        Assert.False(nextCalled);
        Assert.Equal(403, context.Response.StatusCode);
    }

    /// <summary>Invoke async user path user session forwards.</summary>
    [Fact]
    public async Task InvokeAsync_UserPath_UserSession_Forwards()
    {
        SetupAccount(Role.User);
        var context = BuildContext("/user/dist/img/avatar.png");
        bool nextCalled = false;
        var sut = new RoleBasedStaticFileMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await sut.InvokeAsync(context, _sessionService.Object, _accountService.Object);

        Assert.True(nextCalled);
        Assert.Equal(200, context.Response.StatusCode);
    }

    /// <summary>Invoke async user path admin session blocks.</summary>
    [Fact]
    public async Task InvokeAsync_UserPath_AdminSession_Blocks()
    {
        SetupAccount(Role.Admin);
        var context = BuildContext("/user/dist/img/avatar.png");
        bool nextCalled = false;
        var sut = new RoleBasedStaticFileMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await sut.InvokeAsync(context, _sessionService.Object, _accountService.Object);

        Assert.False(nextCalled);
        Assert.Equal(403, context.Response.StatusCode);
    }

    /// <summary>Invoke async user path no session blocks.</summary>
    [Fact]
    public async Task InvokeAsync_UserPath_NoSession_Blocks()
    {
        SetupAccount(null);
        var context = BuildContext("/user/dist/img/avatar.png");
        bool nextCalled = false;
        var sut = new RoleBasedStaticFileMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await sut.InvokeAsync(context, _sessionService.Object, _accountService.Object);

        Assert.False(nextCalled);
        Assert.Equal(403, context.Response.StatusCode);
    }

    /// <summary>
    /// The static-file provider trims leading slashes, so "//admin/x" resolves to the same file as
    /// "/admin/x" — the gate must treat any number of leading slashes the same way.
    /// </summary>
    [Theory]
    [InlineData("//admin/dist/img/logo.png")]
    [InlineData("///admin/dist/img/logo.png")]
    [InlineData("//user/dist/img/avatar.png")]
    [InlineData("////user/dist/img/avatar.png")]
    public async Task InvokeAsync_RestrictedPathWithExtraLeadingSlashes_NoSession_Blocks(string path)
    {
        SetupAccount(null);
        var context = BuildContext(path);
        bool nextCalled = false;
        var sut = new RoleBasedStaticFileMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await sut.InvokeAsync(context, _sessionService.Object, _accountService.Object);

        Assert.False(nextCalled);
        Assert.Equal(403, context.Response.StatusCode);
    }

    /// <summary>Extra leading slashes must not turn an admin-owned path into a block for the matching role.</summary>
    [Fact]
    public async Task InvokeAsync_AdminPathWithExtraLeadingSlashes_AdminSession_Forwards()
    {
        SetupAccount(Role.Admin);
        var context = BuildContext("//admin/dist/img/logo.png");
        bool nextCalled = false;
        var sut = new RoleBasedStaticFileMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await sut.InvokeAsync(context, _sessionService.Object, _accountService.Object);

        Assert.True(nextCalled);
    }

    /// <summary>
    /// Regression test: endpoint routing (MapStaticAssets) matches asset routes case-insensitively, so
    /// "/Admin/x" is served the same file as "/admin/x". The gate must not be bypassable by casing.
    /// </summary>
    [Theory]
    [InlineData("/Admin/dist/img/logo.png")]
    [InlineData("/ADMIN/dist/img/logo.png")]
    [InlineData("/aDmIn/custom/Forum/FreeForum/js/site.js")]
    [InlineData("/User/dist/img/avatar.png")]
    [InlineData("/USER/dist/img/avatar.png")]
    [InlineData("//Admin/dist/img/logo.png")]
    public async Task InvokeAsync_RestrictedPathWithDifferentCasing_NoSession_Blocks(string path)
    {
        SetupAccount(null);
        var context = BuildContext(path);
        bool nextCalled = false;
        var sut = new RoleBasedStaticFileMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await sut.InvokeAsync(context, _sessionService.Object, _accountService.Object);

        Assert.False(nextCalled);
        Assert.Equal(403, context.Response.StatusCode);
    }

    /// <summary>The casing variants are gated by role, not blocked outright: the matching role still gets through.</summary>
    [Theory]
    [InlineData("/Admin/dist/img/logo.png", Role.Admin)]
    [InlineData("/USER/dist/img/avatar.png", Role.User)]
    public async Task InvokeAsync_RestrictedPathWithDifferentCasing_MatchingRole_Forwards(string path, string role)
    {
        SetupAccount(role);
        var context = BuildContext(path);
        bool nextCalled = false;
        var sut = new RoleBasedStaticFileMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await sut.InvokeAsync(context, _sessionService.Object, _accountService.Object);

        Assert.True(nextCalled);
    }

    /// <summary>A wrong-role session is blocked on a casing variant too.</summary>
    [Fact]
    public async Task InvokeAsync_AdminPathWithDifferentCasing_UserSession_Blocks()
    {
        SetupAccount(Role.User);
        var context = BuildContext("/Admin/dist/img/logo.png");
        bool nextCalled = false;
        var sut = new RoleBasedStaticFileMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await sut.InvokeAsync(context, _sessionService.Object, _accountService.Object);

        Assert.False(nextCalled);
        Assert.Equal(403, context.Response.StatusCode);
    }

    /// <summary>A path that merely starts with the letters "admin" (no folder boundary) is not gated.</summary>
    [Fact]
    public async Task InvokeAsync_PathSharingOnlyThePrefixLetters_Forwards()
    {
        SetupAccount(null);
        var context = BuildContext("/administrators.css");
        bool nextCalled = false;
        var sut = new RoleBasedStaticFileMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await sut.InvokeAsync(context, _sessionService.Object, _accountService.Object);

        Assert.True(nextCalled);
    }

    /// <summary>Invoke async unrestricted path no session forwards.</summary>
    [Fact]
    public async Task InvokeAsync_UnrestrictedPath_NoSession_Forwards()
    {
        SetupAccount(null);
        var context = BuildContext("/css/site.css");
        bool nextCalled = false;
        var sut = new RoleBasedStaticFileMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await sut.InvokeAsync(context, _sessionService.Object, _accountService.Object);

        Assert.True(nextCalled);
        Assert.Equal(200, context.Response.StatusCode);
    }

    /// <summary>Invoke async empty path forwards.</summary>
    [Fact]
    public async Task InvokeAsync_EmptyPath_Forwards()
    {
        SetupAccount(null);
        var context = BuildContext("");
        bool nextCalled = false;
        var sut = new RoleBasedStaticFileMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await sut.InvokeAsync(context, _sessionService.Object, _accountService.Object);

        Assert.True(nextCalled);
    }
}
