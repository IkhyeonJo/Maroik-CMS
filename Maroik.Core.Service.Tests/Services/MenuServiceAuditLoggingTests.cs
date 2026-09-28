using System.Data;
using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Account;
using Maroik.Core.Domain.Board;
using Maroik.Core.Domain.Menu;
using Maroik.Core.Service.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;

namespace Maroik.Core.Service.Tests.Services;

/// <summary>
/// The navigation menu decides which pages each role may open (see AuthorizationFilter), so every change to
/// it — create, update, delete of a category or sub-category — is an audited administrative event.
/// </summary>
public class MenuServiceAuditLoggingTests
{
    /// <summary>The signed-in administrator performing the change (recorded in the audit log).</summary>
    private const string Actor = "admin@example.com";

    private readonly Mock<ICategoryRepository> _categoryRepo = new();
    private readonly Mock<ISubCategoryRepository> _subCategoryRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeLogger<MenuService> _logger = new();

    public MenuServiceAuditLoggingTests()
    {
        _unitOfWork.Setup(u => u.BeginAsync(It.IsAny<CancellationToken>(), It.IsAny<IsolationLevel?>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    }

    private MenuService CreateSut() => new(_categoryRepo.Object, _subCategoryRepo.Object, _unitOfWork.Object, _logger);

    private static Category MakeCategory() => Category.Reconstitute(3, "Forum", "Forum", "/icons/forum.png", "Board", "Index", Role.User, 0L);

    private static SubCategory MakeSubCategory() => SubCategory.Reconstitute(4, 3, BoardTypes.FreeForum, BoardTypes.FreeForum, "/icons/forum.png", "Index", Role.User, 0L);

    private void AssertOnlyInformation(string containing)
    {
        FakeLogRecord record = Assert.Single(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Information);
        Assert.Contains(containing, record.Message);
        Assert.Contains($"by admin {Actor}", record.Message);
    }

    /// <summary>Verifies that creating a menu category logs its name at Information.</summary>
    [Fact]
    public async Task CreateCategory_LogsInformation()
    {
        _categoryRepo.Setup(r => r.CreateAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().CreateCategoryAsync(new CategoryRequest { Name = "Forum", DisplayName = "Forum", IconPath = "/i.png", Controller = "Forum", Action = "Index", Role = Role.User }, Actor, TestContext.Current.CancellationToken);

        AssertOnlyInformation("Menu category Forum created");
    }

    /// <summary>Verifies that updating a menu category logs its ID and name at Information.</summary>
    [Fact]
    public async Task UpdateCategory_LogsInformationWithTheId()
    {
        _categoryRepo.Setup(r => r.FindByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeCategory());
        _categoryRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().UpdateCategoryAsync(new CategoryRequest { Id = 3, Name = "Forum", DisplayName = "Forum", IconPath = "/i.png", Controller = "Forum", Action = "Index", Role = Role.Admin }, Actor, TestContext.Current.CancellationToken);

        AssertOnlyInformation("Menu category 3 (Forum) updated");
    }

    /// <summary>Verifies that deleting a menu category logs its ID and name at Information.</summary>
    [Fact]
    public async Task DeleteCategory_LogsInformation()
    {
        _categoryRepo.Setup(r => r.DeleteByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().DeleteCategoryAsync(new CategoryRequest { Id = 3, Name = "Forum" }, Actor, TestContext.Current.CancellationToken);

        AssertOnlyInformation("Menu category 3 (Forum) deleted");
    }

    /// <summary>Verifies that creating a sub-category logs its name at Information.</summary>
    [Fact]
    public async Task CreateSubCategory_LogsInformation()
    {
        _subCategoryRepo.Setup(r => r.CreateAsync(It.IsAny<SubCategory>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().CreateSubCategoryAsync(new SubCategoryRequest { CategoryId = 3, Name = "FreeForum", DisplayName = "Free", IconPath = "/i.png", Action = "Index", Role = Role.User }, Actor, TestContext.Current.CancellationToken);

        AssertOnlyInformation("Menu sub-category FreeForum created");
    }

    /// <summary>Verifies that updating a sub-category logs its ID and name at Information.</summary>
    [Fact]
    public async Task UpdateSubCategory_LogsInformationWithTheId()
    {
        _subCategoryRepo.Setup(r => r.FindByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeSubCategory());
        _subCategoryRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<SubCategory>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().UpdateSubCategoryAsync(new SubCategoryRequest { Id = 4, CategoryId = 3, Name = "FreeForum", DisplayName = "Free", IconPath = "/i.png", Action = "Index", Role = Role.Admin }, Actor, TestContext.Current.CancellationToken);

        AssertOnlyInformation("Menu sub-category 4 (FreeForum) updated");
    }

    /// <summary>Verifies that deleting a sub-category logs its ID and name at Information.</summary>
    [Fact]
    public async Task DeleteSubCategory_LogsInformation()
    {
        _subCategoryRepo.Setup(r => r.DeleteByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().DeleteSubCategoryAsync(new SubCategoryRequest { Id = 4, Name = "FreeForum" }, Actor, TestContext.Current.CancellationToken);

        AssertOnlyInformation("Menu sub-category 4 (FreeForum) deleted");
    }
}
