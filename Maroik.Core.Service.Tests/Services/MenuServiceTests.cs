using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Account;
using Maroik.Core.Domain.Board;
using Maroik.Core.Domain.Menu;
using Maroik.Core.Service.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Maroik.Core.Service.Tests.Services;

/// <summary>
/// Unit tests for <see cref="MenuService"/>.
/// All repository dependencies are replaced with Moq mocks.
/// Covers navigation category and sub-category CRUD operations.
/// </summary>
public class MenuServiceTests
{
    /// <summary>The signed-in administrator performing the change (recorded in the audit log).</summary>
    private const string Actor = "admin@example.com";

    private readonly Mock<ICategoryRepository> _categoryRepo = new();
    private readonly Mock<ISubCategoryRepository> _subCategoryRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private MenuService CreateSut() => new(
        _categoryRepo.Object,
        _subCategoryRepo.Object,
        _unitOfWork.Object,
        NullLogger<MenuService>.Instance);

    // -- Helpers --------------------------------------------------------------

    private static Category MakeCategory(long id = 1, string name = "Forum") =>
        Category.Reconstitute(id, name, name, "/icons/forum.png", "Board", "Index", Role.User, 0L);

    private static SubCategory MakeSubCategory(long id = 1, string name = BoardTypes.FreeForum, long categoryId = 1) =>
        SubCategory.Reconstitute(id, categoryId, name, name, "/icons/forum.png", "Index", Role.User, 0L);

    // -- GetAllCategoriesAsync ------------------------------------------------

    /// <summary>Verifies that <c>GetAllCategoriesAsync</c> delegates to repository.</summary>
    [Fact]
    public async Task GetAllCategoriesAsync_DelegatesToRepository()
    {
        _categoryRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([MakeCategory()]);
        var sut = CreateSut();

        IEnumerable<CategoryResponse> result = await sut.GetAllCategoriesAsync(TestContext.Current.CancellationToken);

        Assert.Single(result);
    }

    // -- GetAllSubCategoriesAsync ---------------------------------------------

    /// <summary>Verifies that <c>GetAllSubCategoriesAsync</c> delegates to repository.</summary>
    [Fact]
    public async Task GetAllSubCategoriesAsync_DelegatesToRepository()
    {
        _subCategoryRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([MakeSubCategory()]);
        var sut = CreateSut();

        IEnumerable<SubCategoryResponse> result = await sut.GetAllSubCategoriesAsync(TestContext.Current.CancellationToken);

        Assert.Single(result);
    }

    // -- SearchCategoriesAsync -------------------------------------------------

    /// <summary>Verifies that <c>SearchCategoriesAsync</c> returns every category when the search text is empty.</summary>
    [Fact]
    public async Task SearchCategoriesAsync_ReturnsAll_WhenSearchIsEmpty()
    {
        _categoryRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeCategory(), MakeCategory(2, "Calendar")]);
        var sut = CreateSut();

        IEnumerable<CategoryResponse> result = await sut.SearchCategoriesAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count());
    }

    /// <summary>Verifies that <c>SearchCategoriesAsync</c> filters by a substring match on Name.</summary>
    [Fact]
    public async Task SearchCategoriesAsync_FiltersByNameSubstring()
    {
        _categoryRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeCategory(), MakeCategory(2, "Calendar")]);
        var sut = CreateSut();

        IEnumerable<CategoryResponse> result = await sut.SearchCategoriesAsync("Cal", TestContext.Current.CancellationToken);

        CategoryResponse only = Assert.Single(result);
        Assert.Equal("Calendar", only.Name);
    }

    /// <summary>Verifies that <c>SearchCategoriesAsync</c> also matches a substring of the numeric ID.</summary>
    [Fact]
    public async Task SearchCategoriesAsync_FiltersByIdSubstring()
    {
        _categoryRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeCategory(), MakeCategory(42, "Calendar")]);
        var sut = CreateSut();

        IEnumerable<CategoryResponse> result = await sut.SearchCategoriesAsync("42", TestContext.Current.CancellationToken);

        CategoryResponse only = Assert.Single(result);
        Assert.Equal(42, only.Id);
    }

    // -- SearchSubCategoriesAsync -----------------------------------------------

    /// <summary>Verifies that <c>SearchSubCategoriesAsync</c> returns every sub-category when the search text is empty.</summary>
    [Fact]
    public async Task SearchSubCategoriesAsync_ReturnsAll_WhenSearchIsEmpty()
    {
        _subCategoryRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeSubCategory(), MakeSubCategory(2, BoardTypes.PrivateNote)]);
        var sut = CreateSut();

        IEnumerable<SubCategoryResponse> result = await sut.SearchSubCategoriesAsync("", TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count());
    }

    /// <summary>Verifies that <c>SearchSubCategoriesAsync</c> filters by a substring match on Name.</summary>
    [Fact]
    public async Task SearchSubCategoriesAsync_FiltersByNameSubstring()
    {
        _subCategoryRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeSubCategory(), MakeSubCategory(2, BoardTypes.PrivateNote)]);
        var sut = CreateSut();

        IEnumerable<SubCategoryResponse> result = await sut.SearchSubCategoriesAsync(BoardTypes.PrivateNote, TestContext.Current.CancellationToken);

        SubCategoryResponse only = Assert.Single(result);
        Assert.Equal(BoardTypes.PrivateNote, only.Name);
    }

    // -- GetCategoryByIdAsync -------------------------------------------------

    /// <summary>Verifies that <c>GetCategoryByIdAsync</c> returns category when found.</summary>
    [Fact]
    public async Task GetCategoryByIdAsync_ReturnsCategory_WhenFound()
    {
        _categoryRepo.Setup(r => r.FindByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeCategory(id: 5, name: "Board"));
        var sut = CreateSut();

        CategoryResponse? result = await sut.GetCategoryByIdAsync(5, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(5, result.Id);
        Assert.Equal("Board", result.Name);
    }

    /// <summary>Verifies that <c>GetCategoryByIdAsync</c> returns null when not found.</summary>
    [Fact]
    public async Task GetCategoryByIdAsync_ReturnsNull_WhenNotFound()
    {
        _categoryRepo.Setup(r => r.FindByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync((Category?)null);
        var sut = CreateSut();

        CategoryResponse? result = await sut.GetCategoryByIdAsync(999, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    // -- GetSubCategoryByIdAsync ----------------------------------------------

    /// <summary>Verifies that <c>GetSubCategoryByIdAsync</c> returns sub category when found.</summary>
    [Fact]
    public async Task GetSubCategoryByIdAsync_ReturnsSubCategory_WhenFound()
    {
        _subCategoryRepo.Setup(r => r.FindByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeSubCategory(id: 3, name: BoardTypes.PrivateNote));
        var sut = CreateSut();

        SubCategoryResponse? result = await sut.GetSubCategoryByIdAsync(3, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(3, result.Id);
    }

    /// <summary>Verifies that <c>GetSubCategoryByIdAsync</c> returns null when not found.</summary>
    [Fact]
    public async Task GetSubCategoryByIdAsync_ReturnsNull_WhenNotFound()
    {
        _subCategoryRepo.Setup(r => r.FindByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync((SubCategory?)null);
        var sut = CreateSut();

        SubCategoryResponse? result = await sut.GetSubCategoryByIdAsync(999, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    // -- CreateCategoryAsync --------------------------------------------------

    /// <summary>Verifies that <c>CreateCategoryAsync</c> returns success when repository succeeds.</summary>
    [Fact]
    public async Task CreateCategoryAsync_ReturnsSuccess_WhenRepositorySucceeds()
    {
        _categoryRepo.Setup(r => r.CreateAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.CreateCategoryAsync(new CategoryRequest { Name = "NewCategory" }, Actor, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
    }

    /// <summary>Verifies that <c>CreateCategoryAsync</c> returns fail when repository throws.</summary>
    [Fact]
    public async Task CreateCategoryAsync_ReturnsFail_WhenRepositoryThrows()
    {
        _categoryRepo.Setup(r => r.CreateAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("DB error"));
        var sut = CreateSut();

        ServiceResult result = await sut.CreateCategoryAsync(new CategoryRequest { Name = "Bad" }, Actor, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }

    // -- UpdateCategoryAsync --------------------------------------------------

    /// <summary>Verifies that <c>UpdateCategoryAsync</c> returns success when repository succeeds.</summary>
    [Fact]
    public async Task UpdateCategoryAsync_ReturnsSuccess_WhenRepositorySucceeds()
    {
        _categoryRepo.Setup(r => r.FindByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeCategory(id: 1));
        _categoryRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.UpdateCategoryAsync(new CategoryRequest { Id = 1, Name = "Updated" }, Actor, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
    }

    /// <summary>Verifies that <c>UpdateCategoryAsync</c> returns fail when the ID doesn't match any existing category.</summary>
    [Fact]
    public async Task UpdateCategoryAsync_ReturnsFail_WhenIdNotFound()
    {
        _categoryRepo.Setup(r => r.FindByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync((Category?)null);
        var sut = CreateSut();

        ServiceResult result = await sut.UpdateCategoryAsync(new CategoryRequest { Id = 999 }, Actor, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _categoryRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Verifies that <c>UpdateCategoryAsync</c> returns fail when repository throws.</summary>
    [Fact]
    public async Task UpdateCategoryAsync_ReturnsFail_WhenRepositoryThrows()
    {
        _categoryRepo.Setup(r => r.FindByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeCategory(id: 1));
        _categoryRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("DB error"));
        var sut = CreateSut();

        // (a valid name: an empty one is refused by the domain before the repository is ever asked to write)
        ServiceResult result = await sut.UpdateCategoryAsync(new CategoryRequest { Id = 1, Name = "Updated" }, Actor, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("Menu.UpdateCategoryFailed", result.ErrorCode);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Regression test: an update request with an empty name must be rejected, not silently
    /// persisted. Update used to rebuild the entity via Category.Reconstitute (which skips
    /// validation and is meant only for trusted repository data), so an empty name from the
    /// request would previously pass straight through.
    /// </summary>
    [Fact]
    public async Task UpdateCategoryAsync_ReturnsFail_WhenNameEmpty()
    {
        _categoryRepo.Setup(r => r.FindByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeCategory(id: 1));
        var sut = CreateSut();

        ServiceResult result = await sut.UpdateCategoryAsync(new CategoryRequest { Id = 1, Name = "" }, Actor, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _categoryRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -- DeleteCategoryAsync --------------------------------------------------

    /// <summary>Verifies that <c>DeleteCategoryAsync</c> returns success when repository succeeds.</summary>
    [Fact]
    public async Task DeleteCategoryAsync_ReturnsSuccess_WhenRepositorySucceeds()
    {
        _categoryRepo.Setup(r => r.DeleteByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.DeleteCategoryAsync(new CategoryRequest { Id = 1 }, Actor, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
    }

    /// <summary>Verifies that <c>DeleteCategoryAsync</c> returns fail when repository throws.</summary>
    [Fact]
    public async Task DeleteCategoryAsync_ReturnsFail_WhenRepositoryThrows()
    {
        _categoryRepo.Setup(r => r.DeleteByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("DB error"));
        var sut = CreateSut();

        ServiceResult result = await sut.DeleteCategoryAsync(new CategoryRequest { Id = 1 }, Actor, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }

    // -- CreateSubCategoryAsync -----------------------------------------------

    /// <summary>Verifies that <c>CreateSubCategoryAsync</c> returns success when repository succeeds.</summary>
    [Fact]
    public async Task CreateSubCategoryAsync_ReturnsSuccess_WhenRepositorySucceeds()
    {
        _subCategoryRepo.Setup(r => r.CreateAsync(It.IsAny<SubCategory>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.CreateSubCategoryAsync(new SubCategoryRequest { Name = "NewSub" }, Actor, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
    }

    /// <summary>Verifies that <c>CreateSubCategoryAsync</c> returns fail when repository throws.</summary>
    [Fact]
    public async Task CreateSubCategoryAsync_ReturnsFail_WhenRepositoryThrows()
    {
        _subCategoryRepo.Setup(r => r.CreateAsync(It.IsAny<SubCategory>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("DB error"));
        var sut = CreateSut();

        ServiceResult result = await sut.CreateSubCategoryAsync(new SubCategoryRequest { Name = "Bad" }, Actor, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }

    // -- UpdateSubCategoryAsync -----------------------------------------------

    /// <summary>Verifies that <c>UpdateSubCategoryAsync</c> returns success when repository succeeds.</summary>
    [Fact]
    public async Task UpdateSubCategoryAsync_ReturnsSuccess_WhenRepositorySucceeds()
    {
        _subCategoryRepo.Setup(r => r.FindByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeSubCategory(id: 1));
        _subCategoryRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<SubCategory>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.UpdateSubCategoryAsync(new SubCategoryRequest { Id = 1, Name = "Updated" }, Actor, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
    }

    /// <summary>Verifies that <c>UpdateSubCategoryAsync</c> returns fail when the ID doesn't match any existing sub-category.</summary>
    [Fact]
    public async Task UpdateSubCategoryAsync_ReturnsFail_WhenIdNotFound()
    {
        _subCategoryRepo.Setup(r => r.FindByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync((SubCategory?)null);
        var sut = CreateSut();

        ServiceResult result = await sut.UpdateSubCategoryAsync(new SubCategoryRequest { Id = 999 }, Actor, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _subCategoryRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<SubCategory>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Verifies that <c>UpdateSubCategoryAsync</c> returns fail when repository throws.</summary>
    [Fact]
    public async Task UpdateSubCategoryAsync_ReturnsFail_WhenRepositoryThrows()
    {
        _subCategoryRepo.Setup(r => r.FindByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeSubCategory(id: 1));
        _subCategoryRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<SubCategory>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("DB error"));
        var sut = CreateSut();

        // (a valid name: an empty one is refused by the domain before the repository is ever asked to write)
        ServiceResult result = await sut.UpdateSubCategoryAsync(new SubCategoryRequest { Id = 1, Name = "Updated" }, Actor, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("Menu.UpdateSubCategoryFailed", result.ErrorCode);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Regression test: an update request with an empty name must be rejected, not silently
    /// persisted (see the matching Category regression test for why).
    /// </summary>
    [Fact]
    public async Task UpdateSubCategoryAsync_ReturnsFail_WhenNameEmpty()
    {
        _subCategoryRepo.Setup(r => r.FindByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeSubCategory(id: 1));
        var sut = CreateSut();

        ServiceResult result = await sut.UpdateSubCategoryAsync(new SubCategoryRequest { Id = 1, Name = "" }, Actor, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _subCategoryRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<SubCategory>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -- DeleteSubCategoryAsync -----------------------------------------------

    /// <summary>Verifies that <c>DeleteSubCategoryAsync</c> returns success when repository succeeds.</summary>
    [Fact]
    public async Task DeleteSubCategoryAsync_ReturnsSuccess_WhenRepositorySucceeds()
    {
        _subCategoryRepo.Setup(r => r.DeleteByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.DeleteSubCategoryAsync(new SubCategoryRequest { Id = 1 }, Actor, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
    }

    /// <summary>Verifies that <c>DeleteSubCategoryAsync</c> returns fail when repository throws.</summary>
    [Fact]
    public async Task DeleteSubCategoryAsync_ReturnsFail_WhenRepositoryThrows()
    {
        _subCategoryRepo.Setup(r => r.DeleteByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("DB error"));
        var sut = CreateSut();

        ServiceResult result = await sut.DeleteSubCategoryAsync(new SubCategoryRequest { Id = 1 }, Actor, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }

    // -- Field policy is enforced at the service boundary ---------------------------------

    /// <summary>A role outside Admin/User/Anonymous is refused with the domain's error rather than reaching the DB CHECK constraint.</summary>
    [Fact]
    public async Task CreateCategoryAsync_ReturnsTheDomainValidationError_ForAnUnknownRole()
    {
        ServiceResult result = await CreateSut().CreateCategoryAsync(
            new CategoryRequest { Name = "Forum", DisplayName = "Forum", IconPath = "/i.png", Controller = "Board", Action = "Index", Role = "Root", Order = 0 },
            Actor, TestContext.Current.CancellationToken);

        Assert.Equal("Menu.RoleInvalid", result.ErrorCode);
        _categoryRepo.Verify(r => r.CreateAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A negative order is refused for a sub-category as well.</summary>
    [Fact]
    public async Task CreateSubCategoryAsync_ReturnsTheDomainValidationError_ForANegativeOrder()
    {
        ServiceResult result = await CreateSut().CreateSubCategoryAsync(
            new SubCategoryRequest { CategoryId = 1, Name = "Free", DisplayName = "Free", IconPath = "/i.png", Action = "Index", Role = Role.User, Order = -1 },
            Actor, TestContext.Current.CancellationToken);

        Assert.Equal("Menu.OrderNegative", result.ErrorCode);
        _subCategoryRepo.Verify(r => r.CreateAsync(It.IsAny<SubCategory>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>An over-long text field is refused on update, the row is not written and the lock is released.</summary>
    [Fact]
    public async Task UpdateCategoryAsync_ReturnsTheDomainValidationError_ForAnOverlongField()
    {
        _categoryRepo.Setup(r => r.FindByIdForUpdateAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeCategory());

        ServiceResult result = await CreateSut().UpdateCategoryAsync(
            new CategoryRequest { Id = 1, Name = new string('n', 300), DisplayName = "d", IconPath = "/i.png", Controller = "Board", Action = "Index", Role = Role.User, Order = 0 },
            Actor, TestContext.Current.CancellationToken);

        Assert.Equal("Menu.FieldTooLong", result.ErrorCode);
        _categoryRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
