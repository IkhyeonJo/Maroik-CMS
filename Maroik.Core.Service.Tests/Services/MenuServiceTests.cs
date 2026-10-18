using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Accounts;
using Maroik.Core.Domain.Boards;
using Maroik.Core.Domain.Menus;
using Maroik.Core.Service.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
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

    /// <summary>Mock <c>ICategoryRepository</c> injected into the system under test.</summary>
    private readonly Mock<ICategoryRepository> _categoryRepo = new();
    /// <summary>Mock <c>ISubCategoryRepository</c> injected into the system under test.</summary>
    private readonly Mock<ISubCategoryRepository> _subCategoryRepo = new();
    /// <summary>Mock <c>IUnitOfWork</c> injected into the system under test.</summary>
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    /// <summary>Captures the log entries the system under test writes.</summary>
    private readonly FakeLogger<MenuService> _logger = new();

    /// <summary>The service under test over the mocked dependencies.</summary>
    private MenuService CreateSut() => new(
        _categoryRepo.Object,
        _subCategoryRepo.Object,
        _unitOfWork.Object,
        _logger);

    /// <summary>
    /// Every menu write locks the whole menu first. By default it is empty — no route to menu management, so the menu
    /// safeguard lets every change through — unless a test seeds one with <see cref="SeedMenuManagementRoute"/>.
    /// </summary>
    public MenuServiceTests()
    {
        _categoryRepo.Setup(r => r.GetAllForUpdateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => []);
        _subCategoryRepo.Setup(r => r.GetAllForUpdateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => []);
    }

    // -- Helpers --------------------------------------------------------------

    /// <summary>A persisted category named <paramref name="name"/>.</summary>
    private static Category MakeCategory(long id = 1, string name = "Forum") =>
        Category.Reconstitute(id, name, name, "/icons/forum.png", "Board", "Index", Role.User, 0L);

    /// <summary>A persisted sub-category named <paramref name="name"/> under <paramref name="categoryId"/>.</summary>
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
        Assert.Equal(ServiceResult.TemporaryErrorKey, result.ErrorKey);
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
        Assert.Equal(ServiceResult.TemporaryErrorKey, result.ErrorKey);
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
        Assert.Equal(ServiceResult.TemporaryErrorKey, result.ErrorKey);
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
        Assert.Equal(ServiceResult.TemporaryErrorKey, result.ErrorKey);
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
        Assert.Equal(ServiceResult.TemporaryErrorKey, result.ErrorKey);
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
        Assert.Equal(ServiceResult.TemporaryErrorKey, result.ErrorKey);
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

    // -- Whole-search fields, transaction boundaries, exact results and failure logging -----

    /// <summary>A category whose every searchable field holds a distinct value.</summary>
    private static Category DistinctCategory() =>
        Category.Reconstitute(3, "nm", "disp", "/ic.png", "Ctrl", "Act", Role.Admin, 7L);

    /// <summary>A category that shares none of <see cref="DistinctCategory"/>'s values.</summary>
    private static Category OtherCategory() =>
        Category.Reconstitute(4, "zz", "zz", "/zz.png", "Zz", "Zz", Role.User, 8L);

    /// <summary>The category search matches on each field on its own: id, name, display name, icon, controller, action, role and order.</summary>
    [Theory]
    [InlineData("3")]
    [InlineData("nm")]
    [InlineData("disp")]
    [InlineData("ic.png")]
    [InlineData("Ctrl")]
    [InlineData("Act")]
    [InlineData("Admin")]
    [InlineData("7")]
    public async Task SearchCategoriesAsync_MatchesEachSearchableField(string search)
    {
        _categoryRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([DistinctCategory(), OtherCategory()]);

        IEnumerable<CategoryResponse> result = await CreateSut().SearchCategoriesAsync(search, TestContext.Current.CancellationToken);

        Assert.Equal(3, Assert.Single(result).Id);
    }

    /// <summary>The sub-category search matches on each field on its own: id, category id, name, display name, icon, action, role and order.</summary>
    [Theory]
    [InlineData("3")]
    [InlineData("55")]
    [InlineData("nm")]
    [InlineData("disp")]
    [InlineData("ic.png")]
    [InlineData("Act")]
    [InlineData("Admin")]
    [InlineData("7")]
    public async Task SearchSubCategoriesAsync_MatchesEachSearchableField(string search)
    {
        _subCategoryRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([
            SubCategory.Reconstitute(3, 55, "nm", "disp", "/ic.png", "Act", Role.Admin, 7L),
            SubCategory.Reconstitute(4, 66, "zz", "zz", "/zz.png", "Zz", Role.User, 8L)]);

        IEnumerable<SubCategoryResponse> result = await CreateSut().SearchSubCategoriesAsync(search, TestContext.Current.CancellationToken);

        Assert.Equal(3, Assert.Single(result).Id);
    }

    /// <summary>Asserts the one Error entry carries <paramref name="thrown"/> and reads <paramref name="message"/>.</summary>
    private void AssertLoggedError(string message, Exception thrown)
    {
        FakeLogRecord record = Assert.Single(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Error);
        Assert.Same(thrown, record.Exception);
        Assert.Equal(message, record.Message);
    }

    /// <summary>A category create that throws is logged with its name and reported as the create failure.</summary>
    [Fact]
    public async Task CreateCategoryAsync_LogsAndReportsCreateFailed_WhenTheRepositoryThrows()
    {
        var thrown = new InvalidOperationException("boom");
        _categoryRepo.Setup(r => r.CreateAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>())).ThrowsAsync(thrown);

        ServiceResult result = await CreateSut().CreateCategoryAsync(
            new CategoryRequest { Name = "Forum", DisplayName = "Forum", Controller = "Board", Action = "Index", Role = Role.User }, Actor, TestContext.Current.CancellationToken);

        Assert.Equal("Menu.CreateCategoryFailed", result.ErrorCode);
        AssertLoggedError("Failed to create category Forum", thrown);
    }

    /// <summary>A successful category update opens the transaction and commits it.</summary>
    [Fact]
    public async Task UpdateCategoryAsync_BeginsAndCommitsTheTransaction_OnSuccess()
    {
        _categoryRepo.Setup(r => r.FindByIdForUpdateAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeCategory(id: 1));

        ServiceResult result = await CreateSut().UpdateCategoryAsync(new CategoryRequest { Id = 1, Name = "Updated" }, Actor, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _unitOfWork.Verify(u => u.BeginAsync(null, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>An unknown category id is a NotFound ("Input is invalid") and the transaction is rolled back.</summary>
    [Fact]
    public async Task UpdateCategoryAsync_ReturnsCategoryNotFound_AndRollsBack_WhenTheIdIsUnknown()
    {
        ServiceResult result = await CreateSut().UpdateCategoryAsync(new CategoryRequest { Id = 999, Name = "Updated" }, Actor, TestContext.Current.CancellationToken);

        Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
        Assert.Equal("Category.NotFound", result.ErrorCode);
        Assert.Equal("Input is invalid", result.ErrorKey);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A category update that throws is logged with its name.</summary>
    [Fact]
    public async Task UpdateCategoryAsync_LogsTheFailure_WhenTheRepositoryThrows()
    {
        _categoryRepo.Setup(r => r.FindByIdForUpdateAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeCategory(id: 1));
        var thrown = new InvalidOperationException("boom");
        _categoryRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>())).ThrowsAsync(thrown);

        await CreateSut().UpdateCategoryAsync(new CategoryRequest { Id = 1, Name = "Updated" }, Actor, TestContext.Current.CancellationToken);

        AssertLoggedError("Failed to update category Updated", thrown);
    }

    /// <summary>A category delete that throws is logged with its name and reported as the delete failure.</summary>
    [Fact]
    public async Task DeleteCategoryAsync_LogsAndReportsDeleteFailed_WhenTheRepositoryThrows()
    {
        var thrown = new InvalidOperationException("boom");
        _categoryRepo.Setup(r => r.DeleteByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ThrowsAsync(thrown);

        ServiceResult result = await CreateSut().DeleteCategoryAsync(new CategoryRequest { Id = 1, Name = "Forum" }, Actor, TestContext.Current.CancellationToken);

        Assert.Equal("Menu.DeleteCategoryFailed", result.ErrorCode);
        AssertLoggedError("Failed to delete category Forum", thrown);
    }

    /// <summary>A sub-category create that throws is logged with its name and reported as the create failure.</summary>
    [Fact]
    public async Task CreateSubCategoryAsync_LogsAndReportsCreateFailed_WhenTheRepositoryThrows()
    {
        var thrown = new InvalidOperationException("boom");
        _subCategoryRepo.Setup(r => r.CreateAsync(It.IsAny<SubCategory>(), It.IsAny<CancellationToken>())).ThrowsAsync(thrown);

        ServiceResult result = await CreateSut().CreateSubCategoryAsync(
            new SubCategoryRequest { CategoryId = 1, Name = "Notice", DisplayName = "Notice", Action = "Notice", Role = Role.User }, Actor, TestContext.Current.CancellationToken);

        Assert.Equal("Menu.CreateSubCategoryFailed", result.ErrorCode);
        AssertLoggedError("Failed to create sub-category Notice", thrown);
    }

    /// <summary>A successful sub-category update opens the transaction and commits it.</summary>
    [Fact]
    public async Task UpdateSubCategoryAsync_BeginsAndCommitsTheTransaction_OnSuccess()
    {
        _subCategoryRepo.Setup(r => r.FindByIdForUpdateAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeSubCategory(id: 1));

        ServiceResult result = await CreateSut().UpdateSubCategoryAsync(new SubCategoryRequest { Id = 1, CategoryId = 1, Name = "Updated" }, Actor, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _unitOfWork.Verify(u => u.BeginAsync(null, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>An unknown sub-category id is a NotFound ("Input is invalid") and the transaction is rolled back.</summary>
    [Fact]
    public async Task UpdateSubCategoryAsync_ReturnsSubCategoryNotFound_AndRollsBack_WhenTheIdIsUnknown()
    {
        ServiceResult result = await CreateSut().UpdateSubCategoryAsync(new SubCategoryRequest { Id = 999, CategoryId = 1, Name = "Updated" }, Actor, TestContext.Current.CancellationToken);

        Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
        Assert.Equal("SubCategory.NotFound", result.ErrorCode);
        Assert.Equal("Input is invalid", result.ErrorKey);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A sub-category update that throws is logged with its name.</summary>
    [Fact]
    public async Task UpdateSubCategoryAsync_LogsTheFailure_WhenTheRepositoryThrows()
    {
        _subCategoryRepo.Setup(r => r.FindByIdForUpdateAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeSubCategory(id: 1));
        var thrown = new InvalidOperationException("boom");
        _subCategoryRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<SubCategory>(), It.IsAny<CancellationToken>())).ThrowsAsync(thrown);

        await CreateSut().UpdateSubCategoryAsync(new SubCategoryRequest { Id = 1, CategoryId = 1, Name = "Updated" }, Actor, TestContext.Current.CancellationToken);

        AssertLoggedError("Failed to update sub-category Updated", thrown);
    }

    /// <summary>A sub-category delete that throws is logged with its name and reported as the delete failure.</summary>
    [Fact]
    public async Task DeleteSubCategoryAsync_LogsAndReportsDeleteFailed_WhenTheRepositoryThrows()
    {
        var thrown = new InvalidOperationException("boom");
        _subCategoryRepo.Setup(r => r.DeleteByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ThrowsAsync(thrown);

        ServiceResult result = await CreateSut().DeleteSubCategoryAsync(new SubCategoryRequest { Id = 1, Name = "Notice" }, Actor, TestContext.Current.CancellationToken);

        Assert.Equal("Menu.DeleteSubCategoryFailed", result.ErrorCode);
        AssertLoggedError("Failed to delete sub-category Notice", thrown);
    }

    // -- Menu safeguard: the administrators' last route to menu management stays ---------

    /// <summary>The seeded Admin "Management" category (a dropdown: no action of its own).</summary>
    private static Category AdminManagement() =>
        Category.Reconstitute(2, "Management", "Management", "nav-icon fas fa-cog", "Management", "", Role.Admin, 4);

    /// <summary>The seeded Admin "Menu" item under <see cref="AdminManagement"/>.</summary>
    private static SubCategory AdminMenu() =>
        SubCategory.Reconstitute(3, 2, "Menu", "Menu", "far fa-circle nav-icon", "Menu", Role.Admin, 3);

    /// <summary>The locked menu holds exactly one route to Management/Menu: category 2 with its item 3 (fresh rows per call).</summary>
    private void SeedMenuManagementRoute()
    {
        _categoryRepo.Setup(r => r.GetAllForUpdateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => [AdminManagement()]);
        _subCategoryRepo.Setup(r => r.GetAllForUpdateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => [AdminMenu()]);
    }

    /// <summary>
    /// Asserts the safeguard's refusal: the domain conflict (its English template is the resx key), a rollback with no
    /// commit, and one Warning naming what was refused, for whom and why.
    /// </summary>
    private void AssertRefusedAndRolledBack(ServiceResult result, string warning)
    {
        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Conflict, result.ErrorType);
        Assert.Equal("Menu.LastMenuManagementRoute", result.ErrorCode);
        Assert.Equal(
            "The menu entry that lets administrators open menu management cannot be removed, moved or given to another role.",
            result.ErrorKey);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        FakeLogRecord record = Assert.Single(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
        Assert.Equal(warning, record.Message);
        Assert.Null(record.Exception);
    }

    /// <summary>Handing the last route's category to another role is refused and nothing is written.</summary>
    [Fact]
    public async Task UpdateCategoryAsync_RefusesTakingTheLastMenuManagementRouteAway()
    {
        SeedMenuManagementRoute();
        _categoryRepo.Setup(r => r.FindByIdForUpdateAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(AdminManagement());

        ServiceResult result = await CreateSut().UpdateCategoryAsync(new CategoryRequest
        {
            Id = 2, Name = "Management", DisplayName = "Management", Controller = "Management", Action = "", Role = Role.User, Order = 4
        }, Actor, TestContext.Current.CancellationToken);

        AssertRefusedAndRolledBack(result, "Menu category 2 update refused for admin admin@example.com: Menu.LastMenuManagementRoute");
        _categoryRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Refused too when the row being edited is the very instance the locked menu holds: the "before" verdict is taken
    /// before the in-place update, so the change is not judged against itself.
    /// </summary>
    [Fact]
    public async Task UpdateCategoryAsync_RefusesTheChange_WhenTheEditedRowIsTheLockedInstance()
    {
        Category management = AdminManagement();
        _categoryRepo.Setup(r => r.GetAllForUpdateAsync(It.IsAny<CancellationToken>())).ReturnsAsync([management]);
        _subCategoryRepo.Setup(r => r.GetAllForUpdateAsync(It.IsAny<CancellationToken>())).ReturnsAsync([AdminMenu()]);
        _categoryRepo.Setup(r => r.FindByIdForUpdateAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(management);

        ServiceResult result = await CreateSut().UpdateCategoryAsync(new CategoryRequest
        {
            Id = 2, Name = "Management", DisplayName = "Management", Controller = "Dashboard", Action = "", Role = Role.Admin, Order = 4
        }, Actor, TestContext.Current.CancellationToken);

        AssertRefusedAndRolledBack(result, "Menu category 2 update refused for admin admin@example.com: Menu.LastMenuManagementRoute");
    }

    /// <summary>Pointing the last route's item at another action is refused and nothing is written.</summary>
    [Fact]
    public async Task UpdateSubCategoryAsync_RefusesTakingTheLastMenuManagementRouteAway()
    {
        SeedMenuManagementRoute();
        _subCategoryRepo.Setup(r => r.FindByIdForUpdateAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(AdminMenu());

        ServiceResult result = await CreateSut().UpdateSubCategoryAsync(new SubCategoryRequest
        {
            Id = 3, CategoryId = 2, Name = "Menu", DisplayName = "Menu", Action = "Account", Role = Role.Admin, Order = 3
        }, Actor, TestContext.Current.CancellationToken);

        AssertRefusedAndRolledBack(result, "Menu sub-category 3 update refused for admin admin@example.com: Menu.LastMenuManagementRoute");
        _subCategoryRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<SubCategory>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A change that keeps the route (here a new label and order) still goes through.</summary>
    [Fact]
    public async Task UpdateSubCategoryAsync_CommitsAChangeThatKeepsTheRoute()
    {
        SeedMenuManagementRoute();
        _subCategoryRepo.Setup(r => r.FindByIdForUpdateAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(AdminMenu());

        ServiceResult result = await CreateSut().UpdateSubCategoryAsync(new SubCategoryRequest
        {
            Id = 3, CategoryId = 2, Name = "Menu", DisplayName = "Menu settings", Action = "Menu", Role = Role.Admin, Order = 9
        }, Actor, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _subCategoryRepo.Verify(r => r.UpdateEntityAsync(It.Is<SubCategory>(s => s.Id == 3 && s.Order == 9), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Deleting the last route's category (which would take its items with it) is refused.</summary>
    [Fact]
    public async Task DeleteCategoryAsync_RefusesTakingTheLastMenuManagementRouteAway()
    {
        SeedMenuManagementRoute();

        ServiceResult result = await CreateSut().DeleteCategoryAsync(new CategoryRequest { Id = 2, Name = "Management" }, Actor, TestContext.Current.CancellationToken);

        AssertRefusedAndRolledBack(result, "Menu category 2 delete refused for admin admin@example.com: Menu.LastMenuManagementRoute");
        _categoryRepo.Verify(r => r.DeleteByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Deleting the last route's item is refused.</summary>
    [Fact]
    public async Task DeleteSubCategoryAsync_RefusesTakingTheLastMenuManagementRouteAway()
    {
        SeedMenuManagementRoute();

        ServiceResult result = await CreateSut().DeleteSubCategoryAsync(new SubCategoryRequest { Id = 3, Name = "Menu" }, Actor, TestContext.Current.CancellationToken);

        AssertRefusedAndRolledBack(result, "Menu sub-category 3 delete refused for admin admin@example.com: Menu.LastMenuManagementRoute");
        _subCategoryRepo.Verify(r => r.DeleteByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A category delete now runs in a transaction (the menu stays locked from the check to the delete) and commits.</summary>
    [Fact]
    public async Task DeleteCategoryAsync_BeginsAndCommitsTheTransaction_OnSuccess()
    {
        ServiceResult result = await CreateSut().DeleteCategoryAsync(new CategoryRequest { Id = 1, Name = "Forum" }, Actor, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _categoryRepo.Verify(r => r.DeleteByIdAsync(1, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.BeginAsync(null, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A sub-category delete now runs in a transaction and commits.</summary>
    [Fact]
    public async Task DeleteSubCategoryAsync_BeginsAndCommitsTheTransaction_OnSuccess()
    {
        ServiceResult result = await CreateSut().DeleteSubCategoryAsync(new SubCategoryRequest { Id = 1, Name = "Notice" }, Actor, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _subCategoryRepo.Verify(r => r.DeleteByIdAsync(1, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.BeginAsync(null, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A delete that throws rolls the transaction back.</summary>
    [Fact]
    public async Task DeleteCategoryAsync_RollsBack_WhenTheRepositoryThrows()
    {
        _categoryRepo.Setup(r => r.DeleteByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));

        await CreateSut().DeleteCategoryAsync(new CategoryRequest { Id = 1, Name = "Forum" }, Actor, TestContext.Current.CancellationToken);

        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A sub-category delete that throws rolls the transaction back.</summary>
    [Fact]
    public async Task DeleteSubCategoryAsync_RollsBack_WhenTheRepositoryThrows()
    {
        _subCategoryRepo.Setup(r => r.DeleteByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));

        await CreateSut().DeleteSubCategoryAsync(new SubCategoryRequest { Id = 1, Name = "Notice" }, Actor, TestContext.Current.CancellationToken);

        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Every menu write takes its locks in one order — all categories, then all sub-categories, then the edited row —
    /// so two concurrent menu writes cannot deadlock, and the safeguard judges a menu nobody else can change meanwhile.
    /// </summary>
    [Fact]
    public async Task MenuWrites_LockCategoriesThenSubCategoriesThenTheEditedRow()
    {
        List<string> calls = [];
        _categoryRepo.Setup(r => r.GetAllForUpdateAsync(It.IsAny<CancellationToken>())).Callback(() => calls.Add("categories")).ReturnsAsync(() => []);
        _subCategoryRepo.Setup(r => r.GetAllForUpdateAsync(It.IsAny<CancellationToken>())).Callback(() => calls.Add("subCategories")).ReturnsAsync(() => []);
        _categoryRepo.Setup(r => r.FindByIdForUpdateAsync(1, It.IsAny<CancellationToken>())).Callback(() => calls.Add("category row")).ReturnsAsync(MakeCategory(id: 1));
        _subCategoryRepo.Setup(r => r.FindByIdForUpdateAsync(1, It.IsAny<CancellationToken>())).Callback(() => calls.Add("sub-category row")).ReturnsAsync(MakeSubCategory(id: 1));
        _categoryRepo.Setup(r => r.DeleteByIdAsync(1, It.IsAny<CancellationToken>())).Callback(() => calls.Add("category delete")).Returns(Task.CompletedTask);
        _subCategoryRepo.Setup(r => r.DeleteByIdAsync(1, It.IsAny<CancellationToken>())).Callback(() => calls.Add("sub-category delete")).Returns(Task.CompletedTask);
        MenuService sut = CreateSut();

        _ = await sut.UpdateCategoryAsync(new CategoryRequest { Id = 1, Name = "Forum" }, Actor, TestContext.Current.CancellationToken);
        _ = await sut.UpdateSubCategoryAsync(new SubCategoryRequest { Id = 1, CategoryId = 1, Name = "Notice" }, Actor, TestContext.Current.CancellationToken);
        _ = await sut.DeleteCategoryAsync(new CategoryRequest { Id = 1, Name = "Forum" }, Actor, TestContext.Current.CancellationToken);
        _ = await sut.DeleteSubCategoryAsync(new SubCategoryRequest { Id = 1, Name = "Notice" }, Actor, TestContext.Current.CancellationToken);

        Assert.Equal(
        [
            "categories", "subCategories", "category row",
            "categories", "subCategories", "sub-category row",
            "categories", "subCategories", "category delete",
            "categories", "subCategories", "sub-category delete",
        ], calls);
    }
}
