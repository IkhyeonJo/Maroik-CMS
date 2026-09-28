using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Finance;
using Maroik.Website.Mappings;

namespace Maroik.Website.Tests.Mappings;

/// <summary>
/// Unit tests for <see cref="FixedSchedulePolicy"/> (schedule status rules)
/// and <see cref="NoticeViewModelMapper"/> (presentation mapping).
/// </summary>
public class NoticeViewModelMapperTests
{
    // ── FixedSchedulePolicy.IsNoticed ─────────────────────────────────────────

    /// <summary>Is noticed unpunctuality always true.</summary>
    [Fact]
    public void IsNoticed_Unpunctuality_AlwaysTrue()
    {
        var result = FixedSchedulePolicy.IsNoticed(1, 1, DateTime.UtcNow, 7, unpunctuality: true);
        Assert.True(result);
    }

    /// <summary>Is noticed deposit today within window true.</summary>
    [Fact]
    public void IsNoticed_DepositTodayWithinWindow_True()
    {
        var today = new DateTime(2025, 6, 15);
        var result = FixedSchedulePolicy.IsNoticed(6, 15, today, 7, unpunctuality: false);
        Assert.True(result);
    }

    /// <summary>Is noticed deposit beyond window false.</summary>
    [Fact]
    public void IsNoticed_DepositBeyondWindow_False()
    {
        var today = new DateTime(2025, 6, 1);
        var result = FixedSchedulePolicy.IsNoticed(6, 15, today, 7, unpunctuality: false);
        Assert.False(result);
    }

    /// <summary>Is noticed deposit already past false.</summary>
    [Fact]
    public void IsNoticed_DepositAlreadyPast_False()
    {
        var today = new DateTime(2025, 6, 20);
        var result = FixedSchedulePolicy.IsNoticed(6, 15, today, 7, unpunctuality: false);
        Assert.False(result);
    }

    /// <summary>Is noticed feb29 non leap year false.</summary>
    [Fact]
    public void IsNoticed_Feb29NonLeapYear_False()
    {
        var today = new DateTime(2025, 2, 27);
        var result = FixedSchedulePolicy.IsNoticed(2, 29, today, 7, unpunctuality: false);
        Assert.False(result);
    }

    // ── FixedSchedulePolicy.IsExpired ─────────────────────────────────────────

    /// <summary>Is expired maturity in past true.</summary>
    [Fact]
    public void IsExpired_MaturityInPast_True()
    {
        var today = new DateTime(2025, 6, 15);
        var result = FixedSchedulePolicy.IsExpired(new DateTime(2025, 6, 14), today);
        Assert.True(result);
    }

    /// <summary>Is expired maturity today false.</summary>
    [Fact]
    public void IsExpired_MaturityToday_False()
    {
        var today = new DateTime(2025, 6, 15);
        var result = FixedSchedulePolicy.IsExpired(today, today);
        Assert.False(result);
    }

    /// <summary>Is expired maturity in future false.</summary>
    [Fact]
    public void IsExpired_MaturityInFuture_False()
    {
        var today = new DateTime(2025, 6, 15);
        var result = FixedSchedulePolicy.IsExpired(new DateTime(2025, 12, 31), today);
        Assert.False(result);
    }

    // ── NoticeViewModelMapper.ToRowCssClass (presentation mapping of FixedSchedulePolicy.GetRowStatus) ──

    /// <summary>Expired status takes priority and maps to the danger row class.</summary>
    [Fact]
    public void ToRowCssClass_Expired_ReturnsDangerClass()
    {
        var result = FixedScheduleRowStatus.Expired.ToRowCssClass();
        Assert.Equal("table-danger clsGridRow", result);
    }

    /// <summary>Noticed (not expired) maps to the info row class.</summary>
    [Fact]
    public void ToRowCssClass_Noticed_ReturnsInfoClass()
    {
        var result = FixedScheduleRowStatus.Noticed.ToRowCssClass();
        Assert.Equal("table-info clsGridRow", result);
    }

    /// <summary>Normal (neither expired nor noticed) maps to the inactive row class.</summary>
    [Fact]
    public void ToRowCssClass_Normal_ReturnsInactiveClass()
    {
        var result = FixedScheduleRowStatus.Normal.ToRowCssClass();
        Assert.Equal("table-inactive clsGridRow", result);
    }

    /// <summary>End-to-end: a mapped FixedIncomeOutputViewModel's RowCssClass reflects Expired/Noticed.</summary>
    [Fact]
    public void FixedIncomesToDisplayViewModels_SetsRowCssClass_ForExpiredItem()
    {
        var items = new[] { new FixedIncomeResponse { MaturityDate = DateTime.UtcNow.AddDays(-1) } };
        var vms = items.ToDisplayViewModels([], k => k, "UTC", 7);
        Assert.Equal("table-danger clsGridRow", vms[0].RowCssClass);
    }

    // ── NoticeViewModelMapper.ToDisplayViewModels ────────────────────────────

    /// <summary>Fixed incomes to display view models localizes class fields.</summary>
    [Fact]
    public void FixedIncomesToDisplayViewModels_LocalizesClassFields()
    {
        var items = new[] { new FixedIncomeResponse { MainClass = "RegularIncome", SubClass = "LaborIncome", MaturityDate = DateTime.UtcNow.AddYears(1) } };
        var vms = items.ToDisplayViewModels([], key => $"L:{key}", "UTC", 7);
        Assert.Equal("L:RegularIncome", vms[0].MainClass);
        Assert.Equal("L:LaborIncome", vms[0].SubClass);
    }

    /// <summary>Fixed incomes to display view models populates monetary unit.</summary>
    [Fact]
    public void FixedIncomesToDisplayViewModels_PopulatesMonetaryUnit()
    {
        var assets = new[] { new AssetResponse { ProductName = "Bank", MonetaryUnit = "KRW" } };
        var items = new[] { new FixedIncomeResponse { DepositMyAssetProductName = "Bank", MaturityDate = DateTime.UtcNow.AddYears(1) } };
        var vms = items.ToDisplayViewModels(assets, k => k, "UTC", 7);
        Assert.Equal("KRW", vms[0].MonetaryUnit);
    }
}
