using System.Text.Json;
using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Account;
using Maroik.Core.PostgreSQL.Data;
using Maroik.Core.PostgreSQL.Models;
using Maroik.Website.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// The post / comment / private-note write and edit actions and the menu actions answer a refused write with
/// the reason the service gave — localized, like the account book, notice and calendar actions — instead
/// of always "Input is invalid". Each case drives the real host into a representative refusal.
/// </summary>
[Collection("Website Integration")]
public class ServiceFailureReasonContractTests(MaroikWebApplicationFactory factory)
{
    /// <summary>Client for the shared test host; redirects are not followed.</summary>
    private readonly HttpClient _client = factory.CreateTestClient(followRedirects: false);

    /// <summary>Seeds (if missing) an account with <paramref name="role"/> and signs in as it.</summary>
    private Task<AuthenticatedSession> LoginAsync(string email, string role = Role.User) =>
        AuthenticatedSessionHelper.LoginAsync(factory, _client, email, "UserPassword1!", role, TestContext.Current.CancellationToken);

    /// <summary>Sends <paramref name="request"/> (optionally as a <paramref name="culture"/> browser) and returns its <c>error</c>, asserting a 200 refusal.</summary>
    private async Task<string> ErrorOf(HttpRequestMessage request, string? culture = null)
    {
        if (culture != null) request.Headers.AcceptLanguage.ParseAdd(culture);
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.False(doc.RootElement.GetProperty("result").GetBoolean());
        return doc.RootElement.GetProperty("error").GetString()!;
    }

    /// <summary>A write/edit-post form carrying an attachment named <paramref name="fileName"/>.</summary>
    private static MultipartFormDataContent BoardFormWithFile(string fileName, long id = 0)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(id.ToString()), "Id" },
            { new StringContent($"title-{Guid.NewGuid():N}"[..30]), "Title" },
            { new StringContent("Body"), "Content" },
            { new StringContent("false"), "Locked" },
            { new StringContent("false"), "Noticed" },
            { new ByteArrayContent([1, 2, 3]), "UploadedFile", fileName }
        };
        return form;
    }

    /// <summary>Inserts a post of <paramref name="type"/> written by <paramref name="writer"/> and returns its id.</summary>
    private async Task<long> SeedBoardAsync(string type, string writer, bool locked = false)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var board = new Board
        {
            Type = type, Title = $"seed-{Guid.NewGuid():N}"[..30], Content = "Body", Writer = writer,
            Created = DateTime.UtcNow, Updated = DateTime.UtcNow, View = 0, Deleted = false, Locked = locked
        };
        TestAccounts.EnsureNickname(db, board.Writer);
        db.Boards.Add(board);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return board.Id;
    }

    // -- Forum ---------------------------------------------------------------------------------------

    /// <summary>A post with a non-zip attachment is refused with the attachment rule, in English and in Korean.</summary>
    [Fact]
    public async Task WriteFreeBoard_ReportsTheAttachmentRule()
    {
        var session = await LoginAsync("reason-forum-write@test.com");

        Assert.Equal("Only zip extension allowed.",
            await ErrorOf(session.BuildFormPostRequest("/Forum/WriteFreeBoard", BoardFormWithFile("notes.txt"))));
        Assert.Equal("zip 확장자만 가능합니다.",
            await ErrorOf(session.BuildFormPostRequest("/Forum/WriteFreeBoard", BoardFormWithFile("notes.txt")), "ko-KR"));
    }

    /// <summary>Editing one's own post with a non-zip attachment is refused with the attachment rule.</summary>
    [Fact]
    public async Task EditFreeBoard_ReportsTheAttachmentRule()
    {
        var session = await LoginAsync("reason-forum-edit@test.com");
        long boardId = await SeedBoardAsync("FreeForum", session.Nickname);

        Assert.Equal("Only zip extension allowed.",
            await ErrorOf(session.BuildFormPostRequest("/Forum/EditFreeBoard", BoardFormWithFile("report.pdf", boardId))));
    }

    /// <summary>A comment on someone else's locked post is refused because the post is locked.</summary>
    [Fact]
    public async Task WriteFreeComment_ReportsThatThePostIsLocked()
    {
        long boardId = await SeedBoardAsync("FreeForum", "ReasonLockedWriter", locked: true);
        var session = await LoginAsync("reason-forum-comment@test.com");

        Assert.Equal("Cannot add a comment to a locked post.", await ErrorOf(session.BuildJsonPostRequest(
            "/Forum/WriteFreeComment", new { BoardId = boardId, Content = "hello", DetailCurrentPage = 1 })));
    }

    // -- Private note ------------------------------------------------------------------------------

    /// <summary>A private note with a non-zip attachment is refused with the attachment rule.</summary>
    [Fact]
    public async Task WritePrivateNoteBoard_ReportsTheAttachmentRule()
    {
        var session = await LoginAsync("reason-note-write@test.com");

        Assert.Equal("Only zip extension allowed.",
            await ErrorOf(session.BuildFormPostRequest("/Management/WritePrivateNoteBoard", BoardFormWithFile("setup.exe"))));
    }

    /// <summary>Editing one's own private note with a non-zip attachment is refused with the attachment rule.</summary>
    [Fact]
    public async Task EditPrivateNoteBoard_ReportsTheAttachmentRule()
    {
        var session = await LoginAsync("reason-note-edit@test.com");
        long boardId = await SeedBoardAsync("PrivateNote", session.Nickname);

        Assert.Equal("Only zip extension allowed.",
            await ErrorOf(session.BuildFormPostRequest("/Management/EditPrivateNoteBoard", BoardFormWithFile("notes.txt", boardId))));
    }

    /// <summary>A comment on someone else's private note is refused for lack of permission.</summary>
    [Fact]
    public async Task WritePrivateNoteComment_ReportsTheMissingPermission()
    {
        long boardId = await SeedBoardAsync("PrivateNote", "ReasonNoteOwner");
        var session = await LoginAsync("reason-note-comment@test.com");

        Assert.Equal("You do not have permission to write a comment.", await ErrorOf(session.BuildJsonPostRequest(
            "/Management/WritePrivateNoteComment", new { BoardId = boardId, Content = "hello", DetailCurrentPage = 1 })));
    }

    // -- Menu --------------------------------------------------------------------------------------

    /// <summary>A menu body for <paramref name="role"/> (and <paramref name="order"/>) under a unique name.</summary>
    private static object MenuBody(string role, int order = 500, int id = 0, long categoryId = 0) => new
    {
        Id = id, CategoryId = categoryId, Name = $"reason-{Guid.NewGuid():N}", DisplayName = "Reason",
        IconPath = "/icons/test.png", Controller = "Notice", Action = "Index", Role = role, Order = order
    };

    /// <summary>Inserts a category (and a sub-category under it) for the update cases; returns both ids.</summary>
    private async Task<(int CategoryId, int SubCategoryId)> SeedMenuAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var category = new Category
        {
            Name = $"reason-{Guid.NewGuid():N}", DisplayName = "Reason", IconPath = "/icons/test.png",
            Controller = "Notice", Action = "", Role = Role.User, Order = 900
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var sub = new SubCategory
        {
            CategoryId = category.Id, Name = $"reason-{Guid.NewGuid():N}", DisplayName = "Reason",
            IconPath = "/icons/test.png", Action = "Index", Role = Role.User, Order = 900
        };
        db.SubCategories.Add(sub);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return ((int)category.Id, (int)sub.Id);
    }

    /// <summary>Creating or updating a category or sub-category with an unknown role names the allowed roles, in English and Korean.</summary>
    [Fact]
    public async Task MenuWrites_ReportTheRoleRule()
    {
        var admin = await LoginAsync("reason-menu-admin@test.com", Role.Admin);
        var (categoryId, subCategoryId) = await SeedMenuAsync();
        const string expected = "Role must be Admin, User or Anonymous.";

        Assert.Equal(expected, await ErrorOf(admin.BuildJsonPostRequest("/Management/CreateCategory", MenuBody("Superuser"))));
        Assert.Equal(expected, await ErrorOf(admin.BuildJsonPostRequest("/Management/CreateSubCategory", MenuBody("Superuser", categoryId: categoryId))));
        Assert.Equal(expected, await ErrorOf(admin.BuildJsonPostRequest("/Management/UpdateCategory", MenuBody("Superuser", id: categoryId))));
        Assert.Equal(expected, await ErrorOf(admin.BuildJsonPostRequest("/Management/UpdateSubCategory", MenuBody("Superuser", id: subCategoryId, categoryId: categoryId))));
        Assert.Equal("역할은 Admin, User, Anonymous 중 하나여야 합니다.",
            await ErrorOf(admin.BuildJsonPostRequest("/Management/CreateCategory", MenuBody("Superuser")), "ko-KR"));
    }

    /// <summary>A negative order is refused with the order rule (English and Korean).</summary>
    [Fact]
    public async Task CreateCategory_ReportsTheOrderRule()
    {
        var admin = await LoginAsync("reason-menu-admin@test.com", Role.Admin);

        Assert.Equal("Order cannot be negative.",
            await ErrorOf(admin.BuildJsonPostRequest("/Management/CreateCategory", MenuBody(Role.User, order: -1))));
        Assert.Equal("순서는 음수일 수 없습니다.",
            await ErrorOf(admin.BuildJsonPostRequest("/Management/CreateCategory", MenuBody(Role.User, order: -1)), "ko-KR"));
    }

    /// <summary>A menu field over the length limit is refused naming the field and the limit (English and Korean).</summary>
    [Fact]
    public async Task UpdateCategory_ReportsTheFieldLengthRule()
    {
        var admin = await LoginAsync("reason-menu-admin@test.com", Role.Admin);
        var (categoryId, _) = await SeedMenuAsync();
        object body = new
        {
            Id = categoryId, Name = "reason-long", DisplayName = new string('d', 256), IconPath = "/icons/test.png",
            Controller = "Notice", Action = "", Role = Role.User, Order = 1
        };

        Assert.Equal("'DisplayName' must be 255 characters or fewer.",
            await ErrorOf(admin.BuildJsonPostRequest("/Management/UpdateCategory", body)));
        Assert.Equal("'DisplayName'은(는) 255자 이하여야 합니다.",
            await ErrorOf(admin.BuildJsonPostRequest("/Management/UpdateCategory", body), "ko-KR"));
    }

    /// <summary>
    /// A refused menu delete reports the service's reason too. The real service only refuses a delete when the
    /// database throws, so a stub service stands in to give the action a reason to pass on.
    /// </summary>
    [Fact]
    public async Task MenuDeletes_ReportTheServiceReason()
    {
        using WebApplicationFactory<Program> host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
        {
            services.RemoveAll<IMenuService>();
            services.AddScoped<Maroik.Core.Service.Services.MenuService>();
            services.AddScoped<IMenuService>(sp => new DeleteRefusingMenuService(sp.GetRequiredService<Maroik.Core.Service.Services.MenuService>()));
        }));
        using HttpClient client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://www.localhost/"), AllowAutoRedirect = false, HandleCookies = false
        });
        var admin = await AuthenticatedSessionHelper.LoginAsync(host, client, "reason-menu-delete@test.com", "UserPassword1!", Role.Admin, TestContext.Current.CancellationToken);

        async Task<string> Error(string url)
        {
            var response = await client.SendAsync(admin.BuildJsonPostRequest(url, MenuBody(Role.User, id: 1, categoryId: 1)), TestContext.Current.CancellationToken);
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            return doc.RootElement.GetProperty("error").GetString()!;
        }

        Assert.Equal("Category name cannot be empty.", await Error("/Management/DeleteCategory"));
        Assert.Equal("Sub-category name cannot be empty.", await Error("/Management/DeleteSubCategory"));
    }

    /// <summary>The real menu service, except that every delete is refused with a recognizable reason.</summary>
    private sealed class DeleteRefusingMenuService(IMenuService inner) : IMenuService
    {
        public Task<IEnumerable<CategoryResponse>> GetAllCategoriesAsync(CancellationToken ct = default) => inner.GetAllCategoriesAsync(ct);
        public Task<IEnumerable<SubCategoryResponse>> GetAllSubCategoriesAsync(CancellationToken ct = default) => inner.GetAllSubCategoriesAsync(ct);
        public Task<IEnumerable<CategoryResponse>> SearchCategoriesAsync(string? search, CancellationToken ct = default) => inner.SearchCategoriesAsync(search, ct);
        public Task<IEnumerable<SubCategoryResponse>> SearchSubCategoriesAsync(string? search, CancellationToken ct = default) => inner.SearchSubCategoriesAsync(search, ct);
        public Task<CategoryResponse?> GetCategoryByIdAsync(int id, CancellationToken ct = default) => inner.GetCategoryByIdAsync(id, ct);
        public Task<SubCategoryResponse?> GetSubCategoryByIdAsync(int id, CancellationToken ct = default) => inner.GetSubCategoryByIdAsync(id, ct);
        public Task<ServiceResult> CreateCategoryAsync(CategoryRequest request, string actorEmail, CancellationToken ct = default) => inner.CreateCategoryAsync(request, actorEmail, ct);
        public Task<ServiceResult> UpdateCategoryAsync(CategoryRequest request, string actorEmail, CancellationToken ct = default) => inner.UpdateCategoryAsync(request, actorEmail, ct);
        public Task<ServiceResult> CreateSubCategoryAsync(SubCategoryRequest request, string actorEmail, CancellationToken ct = default) => inner.CreateSubCategoryAsync(request, actorEmail, ct);
        public Task<ServiceResult> UpdateSubCategoryAsync(SubCategoryRequest request, string actorEmail, CancellationToken ct = default) => inner.UpdateSubCategoryAsync(request, actorEmail, ct);

        public Task<ServiceResult> DeleteCategoryAsync(CategoryRequest request, string actorEmail, CancellationToken ct = default) =>
            Task.FromResult(ServiceResult.Conflict("Category.Refused", "Category name cannot be empty."));
        public Task<ServiceResult> DeleteSubCategoryAsync(SubCategoryRequest request, string actorEmail, CancellationToken ct = default) =>
            Task.FromResult(ServiceResult.Conflict("SubCategory.Refused", "Sub-category name cannot be empty."));
    }
}
