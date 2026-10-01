using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Account;
using Maroik.Website.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// Every admin write on the Management page (account create / update / delete, menu category and
/// sub-category create / update / delete) hands the signed-in administrator's e-mail to the service as
/// the actor, so the audit log records who made the change. The services are replaced by recording mocks
/// in one derived host; the admin session is real.
/// </summary>
[Collection("Website Integration")]
public class ManagementControllerAuditActorTests(MaroikWebApplicationFactory factory)
{
    /// <summary>The admin who performs every write; the services must receive this e-mail as the actor.</summary>
    private const string AdminEmail = "audit-actor-admin@test.com";

    /// <summary>A valid admin create/update-account JSON body.</summary>
    private static object AccountPayload() => new
    {
        Email = "audit-actor-target@test.com",
        Password = "NewAccountPw1!",
        Nickname = "AuditActorTarget",
        AvatarImagePath = "",
        Role = Role.User,
        TimeZoneIanaId = "UTC",
        Locked = false,
        LoginAttempt = 0,
        EmailConfirmed = true,
        AgreedServiceTerms = true,
        RegistrationToken = "",
        ResetPasswordToken = "",
        Message = "",
        Deleted = false
    };

    /// <summary>A valid menu JSON body, usable for both category and sub-category writes.</summary>
    private static object MenuPayload() => new
    {
        Id = 1,
        CategoryId = 1,
        Name = "AuditActorMenu",
        DisplayName = "AuditActorMenu",
        IconPath = "/icons/test.png",
        Controller = "Notice",
        Action = "Index",
        Role = Role.User,
        Order = 500
    };

    /// <summary>Each admin write endpoint passes the signed-in admin's e-mail to its service as the actor.</summary>
    [Fact]
    public async Task AdminWrites_PassTheSignedInAdminAsTheActor()
    {
        var accounts = new Mock<IManagementAccountService>();
        var menus = new Mock<IMenuService>();
        ServiceResult ok = ServiceResult.Ok();
        accounts.Setup(s => s.CreateAccountAsync(It.IsAny<AdminCreateAccountRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(ok);
        accounts.Setup(s => s.UpdateAccountAsync(It.IsAny<AdminUpdateAccountRequest>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(ok);
        accounts.Setup(s => s.DeleteAccountAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(ok);
        menus.Setup(s => s.GetAllCategoriesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new CategoryResponse { Id = 1, Name = "Management", DisplayName = "Management", Controller = "Management", Action = "Account", Role = Role.Admin }
        ]);
        menus.Setup(s => s.GetAllSubCategoriesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        menus.Setup(s => s.CreateCategoryAsync(It.IsAny<CategoryRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(ok);
        menus.Setup(s => s.UpdateCategoryAsync(It.IsAny<CategoryRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(ok);
        menus.Setup(s => s.DeleteCategoryAsync(It.IsAny<CategoryRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(ok);
        menus.Setup(s => s.CreateSubCategoryAsync(It.IsAny<SubCategoryRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(ok);
        menus.Setup(s => s.UpdateSubCategoryAsync(It.IsAny<SubCategoryRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(ok);
        menus.Setup(s => s.DeleteSubCategoryAsync(It.IsAny<SubCategoryRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(ok);

        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IManagementAccountService>();
            services.RemoveAll<IMenuService>();
            services.AddSingleton(accounts.Object);
            services.AddSingleton(menus.Object);
        }));
        var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://www.localhost/"), AllowAutoRedirect = false, HandleCookies = false
        });
        var admin = await AuthenticatedSessionHelper.LoginAsync(host, client, AdminEmail, "AdminPassword1!", Role.Admin, TestContext.Current.CancellationToken);

        (string Url, object Payload)[] writes =
        [
            ("/Management/CreateAccount", AccountPayload()),
            ("/Management/UpdateAccount", AccountPayload()),
            ("/Management/DeleteAccount", AccountPayload()),
            ("/Management/CreateCategory", MenuPayload()),
            ("/Management/UpdateCategory", MenuPayload()),
            ("/Management/DeleteCategory", MenuPayload()),
            ("/Management/CreateSubCategory", MenuPayload()),
            ("/Management/UpdateSubCategory", MenuPayload()),
            ("/Management/DeleteSubCategory", MenuPayload()),
        ];
        foreach (var (url, payload) in writes)
        {
            using var request = admin.BuildJsonPostRequest(url, payload);
            var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.True(json.Contains("\"result\":true"), $"{url}: {(int)response.StatusCode} {json}");
        }

        accounts.Verify(s => s.CreateAccountAsync(It.IsAny<AdminCreateAccountRequest>(), AdminEmail, It.IsAny<CancellationToken>()), Times.Once);
        accounts.Verify(s => s.UpdateAccountAsync(It.IsAny<AdminUpdateAccountRequest>(), It.IsAny<string?>(), AdminEmail, It.IsAny<CancellationToken>()), Times.Once);
        accounts.Verify(s => s.DeleteAccountAsync(It.IsAny<string>(), AdminEmail, It.IsAny<CancellationToken>()), Times.Once);
        menus.Verify(s => s.CreateCategoryAsync(It.IsAny<CategoryRequest>(), AdminEmail, It.IsAny<CancellationToken>()), Times.Once);
        menus.Verify(s => s.UpdateCategoryAsync(It.IsAny<CategoryRequest>(), AdminEmail, It.IsAny<CancellationToken>()), Times.Once);
        menus.Verify(s => s.DeleteCategoryAsync(It.IsAny<CategoryRequest>(), AdminEmail, It.IsAny<CancellationToken>()), Times.Once);
        menus.Verify(s => s.CreateSubCategoryAsync(It.IsAny<SubCategoryRequest>(), AdminEmail, It.IsAny<CancellationToken>()), Times.Once);
        menus.Verify(s => s.UpdateSubCategoryAsync(It.IsAny<SubCategoryRequest>(), AdminEmail, It.IsAny<CancellationToken>()), Times.Once);
        menus.Verify(s => s.DeleteSubCategoryAsync(It.IsAny<SubCategoryRequest>(), AdminEmail, It.IsAny<CancellationToken>()), Times.Once);
    }
}
