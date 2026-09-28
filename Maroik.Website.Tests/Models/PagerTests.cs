using Maroik.Website.Models;

namespace Maroik.Website.Tests.Models;

/// <summary>
/// Unit tests for <see cref="Pager"/>. Covers the basic total-pages calculation, the ±2 page-link
/// window (and its clamping at both boundaries), and the input guards added to fix a page-size-0
/// divide-by-zero and an out-of-range <c>?page=</c> query value.
/// </summary>
public class PagerTests
{
    /// <summary>Total pages is the ceiling of items / pageSize.</summary>
    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(1, 10, 1)]
    [InlineData(10, 10, 1)]
    [InlineData(11, 10, 2)]
    [InlineData(100, 10, 10)]
    [InlineData(101, 10, 11)]
    public void Constructor_ComputesTotalPages_AsCeilingOfItemsOverPageSize(int totalItems, int pageSize, int expectedTotalPages)
    {
        var pager = new Pager(totalItems, page: 1, pageSize);

        Assert.Equal(expectedTotalPages, pager.TotalPages);
    }

    /// <summary>A requested page within range is used as-is.</summary>
    [Fact]
    public void Constructor_KeepsCurrentPage_WhenWithinRange()
    {
        var pager = new Pager(totalItems: 100, page: 5, pageSize: 10);

        Assert.Equal(5, pager.CurrentPage);
    }

    /// <summary>A requested page of 0 or negative is clamped up to page 1.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_ClampsCurrentPage_ToOne_WhenPageIsZeroOrNegative(int page)
    {
        var pager = new Pager(totalItems: 100, page, pageSize: 10);

        Assert.Equal(1, pager.CurrentPage);
    }

    /// <summary>A requested page far past the last page is clamped down to the last page.</summary>
    [Fact]
    public void Constructor_ClampsCurrentPage_ToLastPage_WhenPageIsTooHigh()
    {
        var pager = new Pager(totalItems: 100, page: 999, pageSize: 10); // 10 total pages

        Assert.Equal(10, pager.CurrentPage);
    }

    /// <summary>With zero items (zero total pages), the current page still clamps to 1 rather than 0.</summary>
    [Fact]
    public void Constructor_ClampsCurrentPage_ToOne_WhenThereAreNoItems()
    {
        var pager = new Pager(totalItems: 0, page: 5, pageSize: 10);

        Assert.Equal(0, pager.TotalPages);
        Assert.Equal(1, pager.CurrentPage);
    }

    /// <summary>A page size of zero or negative would divide-by-zero in the ceiling calculation -- guarded up to 1.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Constructor_GuardsPageSize_ToAtLeastOne_WhenZeroOrNegative(int pageSize)
    {
        var exception = Record.Exception(() => new Pager(totalItems: 10, page: 1, pageSize));

        Assert.Null(exception);
        var pager = new Pager(totalItems: 10, page: 1, pageSize);
        Assert.Equal(1, pager.PageSize);
        Assert.Equal(10, pager.TotalPages);
    }

    /// <summary>A negative total item count is guarded to zero rather than propagating a nonsense negative.</summary>
    [Fact]
    public void Constructor_GuardsTotalItems_ToZero_WhenNegative()
    {
        var pager = new Pager(totalItems: -5, page: 1, pageSize: 10);

        Assert.Equal(0, pager.TotalItems);
        Assert.Equal(0, pager.TotalPages);
    }

    /// <summary>The page-link window is centred ±2 pages around the current page when there is room on both sides.</summary>
    [Fact]
    public void Constructor_CentersWindow_AroundCurrentPage_WhenAwayFromBothEdges()
    {
        var pager = new Pager(totalItems: 200, page: 10, pageSize: 10); // 20 total pages

        Assert.Equal(8, pager.StartPage);
        Assert.Equal(12, pager.EndPage);
    }

    /// <summary>Near the first page, the window is clamped to start at page 1 (and widened on the right to stay 5 wide).</summary>
    [Fact]
    public void Constructor_ClampsWindow_ToStartAtPageOne_WhenCurrentPageIsNearTheStart()
    {
        var pager = new Pager(totalItems: 200, page: 1, pageSize: 10); // 20 total pages

        Assert.Equal(1, pager.StartPage);
        Assert.Equal(5, pager.EndPage);
    }

    /// <summary>Near the last page, the window is clamped to end at the last page (and widened on the left to stay 5 wide).</summary>
    [Fact]
    public void Constructor_ClampsWindow_ToEndAtLastPage_WhenCurrentPageIsNearTheEnd()
    {
        var pager = new Pager(totalItems: 200, page: 20, pageSize: 10); // 20 total pages, last page = 20

        Assert.Equal(16, pager.StartPage);
        Assert.Equal(20, pager.TotalPages);
        Assert.Equal(20, pager.EndPage);
    }

    /// <summary>When there are 5 or fewer pages total, the window simply covers every page.</summary>
    [Fact]
    public void Constructor_WindowCoversAllPages_WhenFivePagesOrFewer()
    {
        var pager = new Pager(totalItems: 30, page: 2, pageSize: 10); // 3 total pages

        Assert.Equal(1, pager.StartPage);
        Assert.Equal(3, pager.EndPage);
    }

    /// <summary>The single-page case (0 or 1 total pages) produces a window of exactly that one page.</summary>
    [Fact]
    public void Constructor_WindowIsJustPageOne_WhenThereIsOnlyOnePage()
    {
        var pager = new Pager(totalItems: 5, page: 1, pageSize: 10); // 1 total page

        Assert.Equal(1, pager.StartPage);
        Assert.Equal(1, pager.EndPage);
    }

    /// <summary>
    /// When totalPages is small (&lt;= 5) but currentPage sits near the top, the window must still
    /// re-widen to start at page 1 -- regression test for a bug where the right-boundary clamp only
    /// re-widened when the clamped endPage exceeded 5, leaving page 1 unreachable (e.g. 4 total
    /// pages at page 4 produced a (2,4) window instead of (1,4)).
    /// </summary>
    [Theory]
    [InlineData(40, 4, 4)] // 4 total pages, currentPage = 4
    [InlineData(50, 5, 5)] // 5 total pages, currentPage = 5
    public void Constructor_WindowStartsAtPageOne_WhenTotalPagesIsSmall_AndCurrentPageIsNearTheEnd(int totalItems, int page, int expectedTotalPages)
    {
        var pager = new Pager(totalItems, page, pageSize: 10);

        Assert.Equal(expectedTotalPages, pager.TotalPages);
        Assert.Equal(1, pager.StartPage);
        Assert.Equal(expectedTotalPages, pager.EndPage);
    }

    /// <summary>Default page size is 10 when not specified.</summary>
    [Fact]
    public void Constructor_DefaultsPageSize_ToTen()
    {
        var pager = new Pager(totalItems: 25, page: 1);

        Assert.Equal(10, pager.PageSize);
        Assert.Equal(3, pager.TotalPages);
    }

    /// <summary>The parameterless constructor (required for model binding) leaves every property at its default.</summary>
    [Fact]
    public void ParameterlessConstructor_LeavesPropertiesAtDefault()
    {
        var pager = new Pager();

        Assert.Equal(0, pager.TotalItems);
        Assert.Equal(0, pager.CurrentPage);
        Assert.Equal(0, pager.PageSize);
        Assert.Equal(0, pager.TotalPages);
        Assert.Equal(0, pager.StartPage);
        Assert.Equal(0, pager.EndPage);
    }
}
