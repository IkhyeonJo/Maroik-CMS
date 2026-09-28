using Maroik.Core.Contract.Interfaces;
using Maroik.Website.Middlewares;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Maroik.Website.Tests.Middlewares;

/// <summary>
/// Unit tests for <see cref="AvatarCacheMiddleware"/>, which lazily copies an uploaded avatar from
/// file storage into this replica's local <c>wwwroot</c> the first time it is requested, then always
/// forwards to the next middleware (the static-file middleware that actually serves the response).
/// Each test gets its own throwaway <see cref="IWebHostEnvironment.WebRootPath"/> directory so the
/// filesystem writes under test never touch the real project tree.
/// </summary>
public sealed class AvatarCacheMiddlewareTests : IDisposable
{
    private const string AvatarUrl = "/upload/Management/Profile/Avatar/avatar.png";
    private readonly string _webRoot = Directory.CreateTempSubdirectory("MaroikAvatarCacheTests_").FullName;
    private readonly Mock<IProfileService> _profileService = new();

    private string LocalAvatarDir => Path.Combine(_webRoot, "upload", "Management", "Profile", "Avatar");

    private static IWebHostEnvironment MakeEnv(string? webRootPath)
    {
        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(e => e.WebRootPath).Returns(webRootPath!);
        return env.Object;
    }

    private static DefaultHttpContext BuildContext(string path, string method = "GET") => new()
    {
        Request = { Path = path, Method = method },
    };

    /// <summary>A path outside the avatar prefix is forwarded untouched, with no file-storage call.</summary>
    [Fact]
    public async Task InvokeAsync_UnrelatedPath_ForwardsWithoutDownloading()
    {
        bool nextCalled = false;
        var sut = new AvatarCacheMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = BuildContext("/css/site.css");

        await sut.InvokeAsync(context, MakeEnv(_webRoot), _profileService.Object, NullLogger<AvatarCacheMiddleware>.Instance);

        Assert.True(nextCalled);
        _profileService.Verify(p => p.DownloadAvatarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A non-GET request to the avatar path is forwarded without triggering a download.</summary>
    [Fact]
    public async Task InvokeAsync_NonGetMethod_ForwardsWithoutDownloading()
    {
        bool nextCalled = false;
        var sut = new AvatarCacheMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = BuildContext(AvatarUrl, method: "POST");

        await sut.InvokeAsync(context, MakeEnv(_webRoot), _profileService.Object, NullLogger<AvatarCacheMiddleware>.Instance);

        Assert.True(nextCalled);
        _profileService.Verify(p => p.DownloadAvatarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A null/empty WebRootPath (env not fully configured) skips the caching logic entirely.</summary>
    [Fact]
    public async Task InvokeAsync_NoWebRootPath_ForwardsWithoutDownloading()
    {
        bool nextCalled = false;
        var sut = new AvatarCacheMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = BuildContext(AvatarUrl);

        await sut.InvokeAsync(context, MakeEnv(null), _profileService.Object, NullLogger<AvatarCacheMiddleware>.Instance);

        Assert.True(nextCalled);
        _profileService.Verify(p => p.DownloadAvatarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>When the file already exists locally, it is served as-is -- no re-download.</summary>
    [Fact]
    public async Task InvokeAsync_FileAlreadyCached_ForwardsWithoutDownloading()
    {
        Directory.CreateDirectory(LocalAvatarDir);
        await File.WriteAllBytesAsync(Path.Combine(LocalAvatarDir, "avatar.png"), [1, 2, 3], TestContext.Current.CancellationToken);
        bool nextCalled = false;
        var sut = new AvatarCacheMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = BuildContext(AvatarUrl);

        await sut.InvokeAsync(context, MakeEnv(_webRoot), _profileService.Object, NullLogger<AvatarCacheMiddleware>.Instance);

        Assert.True(nextCalled);
        _profileService.Verify(p => p.DownloadAvatarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A missing local file is fetched from file storage and written into the local cache dir.</summary>
    [Fact]
    public async Task InvokeAsync_FileMissing_DownloadsAndWritesLocalCopy()
    {
        byte[] avatarBytes = [10, 20, 30, 40];
        _profileService.Setup(p => p.DownloadAvatarAsync("avatar.png", It.IsAny<CancellationToken>())).ReturnsAsync(avatarBytes);
        bool nextCalled = false;
        var sut = new AvatarCacheMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = BuildContext(AvatarUrl);

        await sut.InvokeAsync(context, MakeEnv(_webRoot), _profileService.Object, NullLogger<AvatarCacheMiddleware>.Instance);

        Assert.True(nextCalled);
        string localPath = Path.Combine(LocalAvatarDir, "avatar.png");
        Assert.True(File.Exists(localPath));
        Assert.Equal(avatarBytes, await File.ReadAllBytesAsync(localPath, TestContext.Current.CancellationToken));
        // The temp file used for the atomic write/move must not be left behind.
        Assert.DoesNotContain(Directory.GetFiles(LocalAvatarDir), f => f.EndsWith(".tmp", StringComparison.Ordinal));
    }

    /// <summary>An empty download result (file storage has nothing for this name) writes no local file, but still forwards.</summary>
    [Fact]
    public async Task InvokeAsync_DownloadReturnsEmpty_WritesNoFile_StillForwards()
    {
        _profileService.Setup(p => p.DownloadAvatarAsync("avatar.png", It.IsAny<CancellationToken>())).ReturnsAsync([]);
        bool nextCalled = false;
        var sut = new AvatarCacheMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = BuildContext(AvatarUrl);

        await sut.InvokeAsync(context, MakeEnv(_webRoot), _profileService.Object, NullLogger<AvatarCacheMiddleware>.Instance);

        Assert.True(nextCalled);
        Assert.False(File.Exists(Path.Combine(LocalAvatarDir, "avatar.png")));
    }

    /// <summary>A null download result (avatar not found) writes no local file, but still forwards to the 404 fallback.</summary>
    [Fact]
    public async Task InvokeAsync_DownloadReturnsNull_WritesNoFile_StillForwards()
    {
        _profileService.Setup(p => p.DownloadAvatarAsync("avatar.png", It.IsAny<CancellationToken>())).ReturnsAsync((byte[]?)null);
        bool nextCalled = false;
        var sut = new AvatarCacheMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = BuildContext(AvatarUrl);

        await sut.InvokeAsync(context, MakeEnv(_webRoot), _profileService.Object, NullLogger<AvatarCacheMiddleware>.Instance);

        Assert.True(nextCalled);
        Assert.False(File.Exists(Path.Combine(LocalAvatarDir, "avatar.png")));
    }

    /// <summary>A file-storage failure is swallowed (logged) rather than propagated, and the request still forwards.</summary>
    [Fact]
    public async Task InvokeAsync_DownloadThrows_SwallowsException_StillForwards()
    {
        _profileService.Setup(p => p.DownloadAvatarAsync("avatar.png", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("file storage unavailable"));
        bool nextCalled = false;
        var sut = new AvatarCacheMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = BuildContext(AvatarUrl);

        await sut.InvokeAsync(context, MakeEnv(_webRoot), _profileService.Object, NullLogger<AvatarCacheMiddleware>.Instance);

        Assert.True(nextCalled);
        Assert.False(File.Exists(Path.Combine(LocalAvatarDir, "avatar.png")));
    }

    /// <summary>
    /// A path-traversal attempt in the file name is neutralized by <c>Path.GetFileName</c> stripping
    /// any directory component, so the cached file is still written safely under the avatar dir.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_PathTraversalAttempt_StripsDirectoryComponent()
    {
        byte[] avatarBytes =
        [
            .. "\t\t\t"u8
        ];
        _profileService.Setup(p => p.DownloadAvatarAsync("passwd", It.IsAny<CancellationToken>())).ReturnsAsync(avatarBytes);
        bool nextCalled = false;
        var sut = new AvatarCacheMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = BuildContext("/upload/Management/Profile/Avatar/../../../../etc/passwd");

        await sut.InvokeAsync(context, MakeEnv(_webRoot), _profileService.Object, NullLogger<AvatarCacheMiddleware>.Instance);

        Assert.True(nextCalled);
        Assert.True(File.Exists(Path.Combine(LocalAvatarDir, "passwd")));
        Assert.False(File.Exists(Path.Combine(_webRoot, "etc", "passwd")));
    }

    /// <summary>Releases the throwaway WebRootPath directory created for this test instance.</summary>
    public void Dispose()
    {
        try { Directory.Delete(_webRoot, recursive: true); } catch { /* best-effort cleanup */ }
    }
}
