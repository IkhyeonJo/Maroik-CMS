using Maroik.Core.Contract.Dtos;

namespace Maroik.Website.Contracts;

/// <summary>
/// Builds in-memory Excel workbooks for account-book data export.
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
        IEnumerable<AdminAccountResponse> items,
        Func<string, string> localize,
        string timeZoneIanaId);

    /// <summary>Creates an Excel workbook combining navigation categories and sub-categories.</summary>
    MemoryStream CreateMenuExcel(
        IEnumerable<CategoryResponse> categories,
        IEnumerable<SubCategoryResponse> subCategories,
        Func<string, string> localize);
}
