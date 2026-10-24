using Maroik.Core.Contract.Dtos;

namespace Maroik.Website.Interfaces;

/// <summary>
/// Builds in-memory Excel workbooks for the grids' Excel export (account book, notices, accounts, menu).
/// Accepts raw service response objects and a localization delegate so that
/// column headers and enumeration values are rendered in the user's language.
/// </summary>
public interface IExcelExportService
{
    /// <summary>Creates an Excel workbook for the given asset records.</summary>
    MemoryStream CreateAssetExcel(
        IEnumerable<AssetResponse> items,
        Func<string, string> localize,
        string timeZoneIanaId);

    /// <summary>Creates an Excel workbook for the given income records.</summary>
    MemoryStream CreateIncomeExcel(
        IEnumerable<IncomeResponse> items,
        IEnumerable<AssetResponse> assets,
        Func<string, string> localize,
        string timeZoneIanaId);

    /// <summary>Creates an Excel workbook for the given expenditure records.</summary>
    MemoryStream CreateExpenditureExcel(
        IEnumerable<ExpenditureResponse> items,
        IEnumerable<AssetResponse> assets,
        Func<string, string> localize,
        string timeZoneIanaId);

    /// <summary>Creates an Excel workbook for the given fixed-income records, including Noticed/Expired flags.</summary>
    MemoryStream CreateFixedIncomeExcel(
        IEnumerable<FixedIncomeResponse> items,
        IEnumerable<AssetResponse> assets,
        Func<string, string> localize,
        string timeZoneIanaId,
        int noticeWindowDays);

    /// <summary>Creates an Excel workbook for the given fixed-expenditure records, including Noticed/Expired flags.</summary>
    MemoryStream CreateFixedExpenditureExcel(
        IEnumerable<FixedExpenditureResponse> items,
        IEnumerable<AssetResponse> assets,
        Func<string, string> localize,
        string timeZoneIanaId,
        int noticeWindowDays);

    /// <summary>Creates an Excel workbook for the given account records.</summary>
    MemoryStream CreateAccountExcel(
        IEnumerable<AccountResponse> items,
        Func<string, string> localize,
        string timeZoneIanaId);

    /// <summary>Creates an Excel workbook combining navigation categories and sub-categories.</summary>
    MemoryStream CreateMenuExcel(
        IEnumerable<CategoryResponse> categories,
        IEnumerable<SubCategoryResponse> subCategories,
        Func<string, string> localize);

    /// <summary>
    /// Creates an Excel workbook of a budget sheet: its rows' localized labels and currencies, then each cell's calculated value
    /// (months, year total, free columns); empty free rows are left out.
    /// </summary>
    MemoryStream CreateBudgetExcel(BudgetSheetResponse sheet, Func<string, string> localize);
}
