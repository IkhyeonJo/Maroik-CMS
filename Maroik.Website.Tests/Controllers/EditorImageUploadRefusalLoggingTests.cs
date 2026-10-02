using System.Net.Http.Headers;
using Maroik.Core.Contract.Misc.Settings;
using Maroik.Core.Domain.Account;
using Maroik.Core.Service.Services;
using Maroik.Website.Controllers;
using Maroik.Website.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// A refused editor (Summernote) image upload is a security event: each of the three editor endpoints logs
/// one Warning naming the reason and the signed-in uploader, whether the controller refuses the file (size,
/// extension) or the attachment service does (content type, SVG, undecodable bytes). The loggers are
/// replaced by <see cref="FakeLogger{T}"/>s in one derived host; the session is real.
/// </summary>
[Collection("Website Integration")]
public class EditorImageUploadRefusalLoggingTests(MaroikWebApplicationFactory factory)
{
    /// <summary>Captures what <see cref="ForumController"/> logs.</summary>
    private readonly FakeLogger<ForumController> _forumLogger = new();
    /// <summary>Captures what <see cref="ManagementController"/> logs.</summary>
    private readonly FakeLogger<ManagementController> _managementLogger = new();
    /// <summary>Captures what <see cref="CalendarController"/> logs.</summary>
    private readonly FakeLogger<CalendarController> _calendarLogger = new();
    /// <summary>Captures what <see cref="AttachmentContentService"/> logs.</summary>
    private readonly FakeLogger<AttachmentContentService> _attachmentLogger = new();

    /// <summary>A host whose controller and attachment-service loggers are the fakes above.</summary>
    private WebApplicationFactory<Program> CreateHost() =>
        factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
        {
            services.AddSingleton<ILogger<ForumController>>(_forumLogger);
            services.AddSingleton<ILogger<ManagementController>>(_managementLogger);
            services.AddSingleton<ILogger<CalendarController>>(_calendarLogger);
            services.AddSingleton<ILogger<AttachmentContentService>>(_attachmentLogger);
        }));

    /// <summary>Every Warning-or-above entry the four fake loggers hold.</summary>
    private IEnumerable<FakeLogRecord> Warnings() =>
        _forumLogger.Collector.GetSnapshot()
            .Concat(_managementLogger.Collector.GetSnapshot())
            .Concat(_calendarLogger.Collector.GetSnapshot())
            .Concat(_attachmentLogger.Collector.GetSnapshot())
            .Where(r => r.Level >= LogLevel.Warning);

    /// <summary>
    /// Signs in as a fresh user on <paramref name="host"/>, posts <paramref name="bytes"/> as
    /// <paramref name="fileName"/> to <paramref name="url"/>, asserts the upload was refused, and returns the
    /// uploader's e-mail.
    /// </summary>
    private static async Task<string> PostRefusedImageAsync(
        WebApplicationFactory<Program> host, string url, string fileName, byte[] bytes, string contentType)
    {
        using HttpClient client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://www.localhost/"), AllowAutoRedirect = false, HandleCookies = false
        });
        string email = $"editor-refusal-{Guid.NewGuid():N}@test.com";
        var session = await AuthenticatedSessionHelper.LoginAsync(host, client, email, "UserPassword1!", Role.User, TestContext.Current.CancellationToken);

        var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(part, "summernoteImageFile", fileName);
        using var request = session.BuildFormPostRequest(url, form);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"result\":false", json);
        return email;
    }

    /// <summary>Asserts exactly one Warning was logged for the refusal, naming <paramref name="reason"/> and <paramref name="email"/>.</summary>
    private void AssertRefusalLoggedOnce(string reason, string email)
    {
        FakeLogRecord record = Assert.Single(Warnings());
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Contains("Editor image upload refused", record.Message);
        Assert.Contains(reason, record.Message);
        Assert.Contains(email, record.Message);
    }

    /// <summary>A file with an extension outside jpg / jpeg / png is refused by the controller, and logged.</summary>
    [Theory]
    [InlineData("/Forum/UploadImageFile")]
    [InlineData("/Management/UploadImageFile")]
    [InlineData("/Calendar/UploadImageFile")]
    public async Task UploadImageFile_LogsARefusedExtension(string url)
    {
        await using var host = CreateHost();

        string email = await PostRefusedImageAsync(host, url, "notes.txt", [1, 2, 3, 4], "text/plain");

        AssertRefusalLoggedOnce(".txt", email);
    }

    /// <summary>A file over the configured size limit is refused by the controller, and logged with its size.</summary>
    [Theory]
    [InlineData("/Forum/UploadImageFile")]
    [InlineData("/Management/UploadImageFile")]
    [InlineData("/Calendar/UploadImageFile")]
    public async Task UploadImageFile_LogsARefusedOversizedFile(string url)
    {
        await using var host = CreateHost();
        long maxBytes = host.Services.GetRequiredService<IOptions<ServerSetting>>().Value.MaxAttachedFileSizeBytes;

        string email = await PostRefusedImageAsync(host, url, "huge.png", new byte[maxBytes + 1], "image/png");

        AssertRefusalLoggedOnce($"{maxBytes + 1}", email);
    }

    /// <summary>
    /// A .png that is not an image passes the controller's checks and is refused by the attachment service,
    /// which logs it with the uploader the controller handed over.
    /// </summary>
    [Theory]
    [InlineData("/Forum/UploadImageFile")]
    [InlineData("/Management/UploadImageFile")]
    [InlineData("/Calendar/UploadImageFile")]
    public async Task UploadImageFile_LogsAnImageTheServiceRefuses_WithTheUploader(string url)
    {
        await using var host = CreateHost();

        string email = await PostRefusedImageAsync(host, url, "fake.png", [1, 2, 3, 4], "image/png");

        AssertRefusalLoggedOnce("not a valid image", email);
    }
}
