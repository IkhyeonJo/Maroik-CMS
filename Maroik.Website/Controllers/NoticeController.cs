using System.Globalization;
using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Misc.Settings;
using Maroik.Core.Domain.Account;
using Maroik.Website.Attributes;
using Maroik.Website.Contracts;
using Maroik.Website.Extensions;
using Maroik.Website.Mappings;
using Maroik.Website.Models.ViewModels.Notice;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Options;

namespace Maroik.Website.Controllers;

/// <summary>Manages fixed-income and fixed-expenditure notice records (create, read, update, delete, export).</summary>
/// <param name="localizer">Localizer for this controller's user-facing messages.</param>
/// <param name="logger">Logger for unexpected failures in the Notice actions.</param>
/// <param name="assetService">Asset reads (asset dropdowns, currency labels).</param>
/// <param name="fixedIncomeService">Fixed-income use cases.</param>
/// <param name="fixedExpenditureService">Fixed-expenditure use cases.</param>
/// <param name="settings">Server settings (notice window in days).</param>
/// <param name="excelExportService">Builds the fixed-income / fixed-expenditure Excel exports.</param>
/// <param name="timeProvider">The clock the grids' notice / expiry flags and the export file name's timestamp are read from.</param>
public class NoticeController(
    IHtmlLocalizer<NoticeController> localizer,
    ILogger<NoticeController> logger,
    IAssetService assetService,
    IFixedIncomeService fixedIncomeService,
    IFixedExpenditureService fixedExpenditureService,
    IOptions<ServerSetting> settings,
    IExcelExportService excelExportService,
    TimeProvider timeProvider) : Controller
{
    #region FixedIncome

    #region Create
    /// <summary>Creates a new fixed-income record.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> CreateFixedIncome(
        [FromBody] FixedIncomeInputViewModel fixedIncomeInputViewModel, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return Json(new { result = false, error = localizer["Input is invalid"].Value });

        try
        {
            string email = ((AccountResponse)ViewBag.LoggedInAccount).Email!;

            var request = new FixedIncomeRequest
            {
                Id = fixedIncomeInputViewModel.Id,
                MainClass = fixedIncomeInputViewModel.MainClass,
                SubClass = fixedIncomeInputViewModel.SubClass,
                Content = fixedIncomeInputViewModel.Content ?? "",
                Amount = fixedIncomeInputViewModel.Amount,
                DepositMyAssetProductName = fixedIncomeInputViewModel.DepositMyAssetProductName,
                DepositMonth = fixedIncomeInputViewModel.DepositMonth,
                DepositDay = fixedIncomeInputViewModel.DepositDay,
                // ParseExact (not Parse) so only a strict yyyy-MM-dd string is accepted, matching
                // main's format-locked validation instead of culture-dependent free-form parsing.
                MaturityDate = DateTime.ParseExact(fixedIncomeInputViewModel.MaturityDate!, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                Note = fixedIncomeInputViewModel.Note ?? "",
                Unpunctuality = fixedIncomeInputViewModel.Unpunctuality
            };

            var result = await fixedIncomeService.CreateAsync(email, request, ct);
            return result.Success
                ? Json(new { result = true, message = localizer["The fixedIncome has been successfully created."].Value })
                : Json(new { result = false, error = localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create fixed-income record");
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }
    #endregion

    #region Read

    /// <summary>Returns the fixed-income grid (AJAX) or the full page.</summary>
    [HttpGet]
    public async Task<IActionResult> FixedIncome(string wholeSearch, CancellationToken ct)
    {
#pragma warning disable ASP0015
        if (HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
#pragma warning restore ASP0015
        {
            AccountResponse account = ViewBag.LoggedInAccount;
            var assets = await assetService.GetAssetsAsync(account.Email!, ct);
            var incomes = string.IsNullOrEmpty(wholeSearch)
                ? await fixedIncomeService.GetFixedIncomesAsync(account.Email!, ct)
                : await fixedIncomeService.SearchFixedIncomesAsync(account.Email!, wholeSearch, ct);
            var viewModels = incomes
                .ToDisplayViewModels(assets, key => localizer[key].Value, account.TimeZoneIanaId!, settings.Value.NoticeMaturityDateDay, timeProvider.GetUtcNow().UtcDateTime)
                .OrderByDescending(a => a.Expired).ThenByDescending(m => m.Noticed)
                .ThenByDescending(m => m.Unpunctuality).ThenByDescending(a => a.Created)
                .ThenByDescending(a => a.Updated)
                .AsQueryable();
            return PartialView("_FixedIncomeGrid", viewModels);
        }

        AccountResponse loggedInAccount = ViewBag.LoggedInAccount;
        var fixedIncomeAssets = (await assetService.GetAssetsAsync(loggedInAccount.Email!, ct))
            .Where(x => !x.Deleted).OrderBy(x => x.ProductName).ToList();
        ViewBag.Assets = fixedIncomeAssets;
        ViewBag.DefaultAssetProductName = fixedIncomeAssets.FirstOrDefault()?.ProductName;
        return View();
    }

    /// <summary>Checks whether a fixed-income record with the given ID exists.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> IsFixedIncomeExists(int id, CancellationToken ct)
    {
        try
        {
            string email = ((AccountResponse)ViewBag.LoggedInAccount).Email!;
            FixedIncomeResponse? item = await fixedIncomeService.GetByIdAsync(email, id, ct);
            if (item == null)
                return Json(new { result = false, error = localizer["Input is invalid"].Value });

            var vm = new FixedIncomeOutputViewModel
            {
                Id = item.Id,
                MainClass = item.MainClass,
                SubClass = item.SubClass,
                Content = item.Content,
                Amount = item.Amount,
                DepositMonth = item.DepositMonth,
                DepositDay = item.DepositDay,
                MaturityDate = item.MaturityDate.ToString("yyyy-MM-dd"),
                Note = item.Note,
                DepositMyAssetProductName = item.DepositMyAssetProductName,
                Unpunctuality = item.Unpunctuality
            };
            return Json(new { result = true, fixedIncome = vm });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check fixed-income existence for id {FixedIncomeId}", id);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    /// <summary>Returns a formatted amount label for the given fixed-income product.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> GetFixedIncomeAmountLabel(string productName, CancellationToken ct)
    {
        try
        {
            string baseLabel = localizer["Amount"].Value;
            AssetResponse? asset = await assetService.GetAssetAsync(
                ((AccountResponse)ViewBag.LoggedInAccount).Email!, productName, ct);

            string label = !string.IsNullOrEmpty(asset?.MonetaryUnit)
                ? baseLabel.GetAmountLabel(asset.MonetaryUnit)
                : baseLabel;

            return Json(new { result = true, label });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get fixed-income amount label for product {ProductName}", productName);
            return Json(new { result = false, label = localizer["Amount"].Value });
        }
    }

    #endregion

    #region Update
    /// <summary>Updates an existing fixed-income record.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> UpdateFixedIncome(
        [FromBody] FixedIncomeInputViewModel fixedIncomeInputViewModel, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return Json(new { result = false, error = localizer["Input is invalid"].Value });

        try
        {
            string email = ((AccountResponse)ViewBag.LoggedInAccount).Email!;

            var request = new FixedIncomeRequest
            {
                Id = fixedIncomeInputViewModel.Id,
                MainClass = fixedIncomeInputViewModel.MainClass,
                SubClass = fixedIncomeInputViewModel.SubClass,
                Content = fixedIncomeInputViewModel.Content ?? "",
                Amount = fixedIncomeInputViewModel.Amount,
                DepositMyAssetProductName = fixedIncomeInputViewModel.DepositMyAssetProductName,
                DepositMonth = fixedIncomeInputViewModel.DepositMonth,
                DepositDay = fixedIncomeInputViewModel.DepositDay,
                // ParseExact (not Parse) so only a strict yyyy-MM-dd string is accepted, matching
                // main's format-locked validation instead of culture-dependent free-form parsing.
                MaturityDate = DateTime.ParseExact(fixedIncomeInputViewModel.MaturityDate!, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                Note = fixedIncomeInputViewModel.Note ?? "",
                Unpunctuality = fixedIncomeInputViewModel.Unpunctuality
            };

            var result = await fixedIncomeService.UpdateAsync(email, request, ct);
            return result.Success
                ? Json(new { result = true, message = localizer["The fixedIncome has been successfully updated."].Value })
                : Json(new { result = false, error = localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update fixed-income record {FixedIncomeId}", fixedIncomeInputViewModel.Id);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }
    #endregion

    #region Delete
    /// <summary>Deletes an existing fixed-income record.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> DeleteFixedIncome(
        [FromBody] FixedIncomeInputViewModel fixedIncomeInputViewModel, CancellationToken ct)
    {
        try
        {
            string email = ((AccountResponse)ViewBag.LoggedInAccount).Email!;
            var result = await fixedIncomeService.DeleteAsync(email, fixedIncomeInputViewModel.Id, ct);
            return result.Success
                ? Json(new { result = true, message = localizer["The fixedIncome has been successfully deleted."].Value })
                : Json(new { result = false, error = localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete fixed-income record {FixedIncomeId}", fixedIncomeInputViewModel.Id);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }
    #endregion

    #region Excel
    /// <summary>Exports fixed-income records to an Excel file.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> ExportExcelFixedIncome(string fileName = "", CancellationToken ct = default)
    {
        AccountResponse account = ViewBag.LoggedInAccount;
        var assets = await assetService.GetAssetsAsync(account.Email!, ct);
        var incomes = await fixedIncomeService.GetFixedIncomesAsync(account.Email!, ct);
        var stream = excelExportService.CreateFixedIncomeExcel(incomes, assets, key => localizer[key].Value, account.TimeZoneIanaId!, settings.Value.NoticeMaturityDateDay);
        string name = fileName.ToExcelFileName(account.TimeZoneIanaId!, timeProvider.GetUtcNow().UtcDateTime);
        return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
    }
    #endregion

    #endregion

    #region FixedExpenditure

    #region Create
    /// <summary>Creates a new fixed-expenditure record.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> CreateFixedExpenditure(
        [FromBody] FixedExpenditureInputViewModel fixedExpenditureInputViewModel, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return Json(new { result = false, error = localizer["Input is invalid"].Value });

        try
        {
            string email = ((AccountResponse)ViewBag.LoggedInAccount).Email!;

            var request = new FixedExpenditureRequest
            {
                Id = fixedExpenditureInputViewModel.Id,
                MainClass = fixedExpenditureInputViewModel.MainClass,
                SubClass = fixedExpenditureInputViewModel.SubClass,
                Content = fixedExpenditureInputViewModel.Content ?? "",
                Amount = fixedExpenditureInputViewModel.Amount,
                PaymentMethod = fixedExpenditureInputViewModel.PaymentMethod,
                MyDepositAsset = fixedExpenditureInputViewModel.MyDepositAsset,
                DepositMonth = fixedExpenditureInputViewModel.DepositMonth,
                DepositDay = fixedExpenditureInputViewModel.DepositDay,
                // ParseExact (not Parse) so only a strict yyyy-MM-dd string is accepted, matching
                // main's format-locked validation instead of culture-dependent free-form parsing.
                MaturityDate = DateTime.ParseExact(fixedExpenditureInputViewModel.MaturityDate!, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                Note = fixedExpenditureInputViewModel.Note ?? "",
                Unpunctuality = fixedExpenditureInputViewModel.Unpunctuality
            };

            var result = await fixedExpenditureService.CreateAsync(email, request, ct);
            return result.Success
                ? Json(new { result = true, message = localizer["The fixedExpenditure has been successfully created."].Value })
                : Json(new { result = false, error = localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create fixed-expenditure record");
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }
    #endregion

    #region Read

    /// <summary>Returns the fixed-expenditure grid (AJAX) or the full page.</summary>
    [HttpGet]
    public async Task<IActionResult> FixedExpenditure(string wholeSearch, CancellationToken ct)
    {
#pragma warning disable ASP0015
        if (HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
#pragma warning restore ASP0015
        {
            AccountResponse account = ViewBag.LoggedInAccount;
            var assets = await assetService.GetAssetsAsync(account.Email!, ct);
            var expenditures = string.IsNullOrEmpty(wholeSearch)
                ? await fixedExpenditureService.GetFixedExpendituresAsync(account.Email!, ct)
                : await fixedExpenditureService.SearchFixedExpendituresAsync(account.Email!, wholeSearch, ct);
            var viewModels = expenditures
                .ToDisplayViewModels(assets, key => localizer[key].Value, account.TimeZoneIanaId!, settings.Value.NoticeMaturityDateDay, timeProvider.GetUtcNow().UtcDateTime)
                .OrderByDescending(a => a.Expired).ThenByDescending(m => m.Noticed)
                .ThenByDescending(m => m.Unpunctuality).ThenByDescending(a => a.Created)
                .ThenByDescending(a => a.Updated)
                .AsQueryable();
            return PartialView("_FixedExpenditureGrid", viewModels);
        }

        AccountResponse loggedInAccount = ViewBag.LoggedInAccount;
        var fixedExpenditureAssets = (await assetService.GetAssetsAsync(loggedInAccount.Email!, ct))
            .Where(x => !x.Deleted).OrderBy(x => x.ProductName).ToList();
        ViewBag.Assets = fixedExpenditureAssets;
        ViewBag.DefaultAssetProductName = fixedExpenditureAssets.FirstOrDefault()?.ProductName;
        return View();
    }

    /// <summary>Checks whether a fixed-expenditure record with the given ID exists.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> IsFixedExpenditureExists(int id, CancellationToken ct)
    {
        try
        {
            string email = ((AccountResponse)ViewBag.LoggedInAccount).Email!;
            FixedExpenditureResponse? item = await fixedExpenditureService.GetByIdAsync(email, id, ct);
            if (item == null)
                return Json(new { result = false, error = localizer["Input is invalid"].Value });

            var vm = new FixedExpenditureOutputViewModel
            {
                Id = item.Id,
                MainClass = item.MainClass,
                SubClass = item.SubClass,
                Content = item.Content,
                Amount = item.Amount,
                DepositMonth = item.DepositMonth,
                DepositDay = item.DepositDay,
                MaturityDate = item.MaturityDate.ToString("yyyy-MM-dd"),
                Note = item.Note,
                PaymentMethod = item.PaymentMethod,
                MyDepositAsset = item.MyDepositAsset ?? "",
                Unpunctuality = item.Unpunctuality
            };
            return Json(new { result = true, fixedExpenditure = vm });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check fixed-expenditure existence for id {FixedExpenditureId}", id);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    /// <summary>Returns a formatted amount label for the given fixed-expenditure product.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> GetFixedExpenditureAmountLabel(string productName, CancellationToken ct)
    {
        try
        {
            string baseLabel = localizer["Amount"].Value;
            AssetResponse? asset = await assetService.GetAssetAsync(
                ((AccountResponse)ViewBag.LoggedInAccount).Email!, productName, ct);

            string label = !string.IsNullOrEmpty(asset?.MonetaryUnit)
                ? baseLabel.GetAmountLabel(asset.MonetaryUnit)
                : baseLabel;

            return Json(new { result = true, label });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get fixed-expenditure amount label for product {ProductName}", productName);
            return Json(new { result = false, label = localizer["Amount"].Value });
        }
    }

    #endregion

    #region Update
    /// <summary>Updates an existing fixed-expenditure record.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> UpdateFixedExpenditure(
        [FromBody] FixedExpenditureInputViewModel fixedExpenditureInputViewModel, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return Json(new { result = false, error = localizer["Input is invalid"].Value });

        try
        {
            string email = ((AccountResponse)ViewBag.LoggedInAccount).Email!;

            var request = new FixedExpenditureRequest
            {
                Id = fixedExpenditureInputViewModel.Id,
                MainClass = fixedExpenditureInputViewModel.MainClass,
                SubClass = fixedExpenditureInputViewModel.SubClass,
                Content = fixedExpenditureInputViewModel.Content ?? "",
                Amount = fixedExpenditureInputViewModel.Amount,
                PaymentMethod = fixedExpenditureInputViewModel.PaymentMethod,
                MyDepositAsset = fixedExpenditureInputViewModel.MyDepositAsset,
                DepositMonth = fixedExpenditureInputViewModel.DepositMonth,
                DepositDay = fixedExpenditureInputViewModel.DepositDay,
                // ParseExact (not Parse) so only a strict yyyy-MM-dd string is accepted, matching
                // main's format-locked validation instead of culture-dependent free-form parsing.
                MaturityDate = DateTime.ParseExact(fixedExpenditureInputViewModel.MaturityDate!, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                Note = fixedExpenditureInputViewModel.Note ?? "",
                Unpunctuality = fixedExpenditureInputViewModel.Unpunctuality
            };

            var result = await fixedExpenditureService.UpdateAsync(email, request, ct);
            return result.Success
                ? Json(new { result = true, message = localizer["The fixedExpenditure has been successfully updated."].Value })
                : Json(new { result = false, error = localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update fixed-expenditure record {FixedExpenditureId}", fixedExpenditureInputViewModel.Id);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }
    #endregion

    #region Delete
    /// <summary>Deletes an existing fixed-expenditure record.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> DeleteFixedExpenditure(
        [FromBody] FixedExpenditureInputViewModel fixedExpenditureInputViewModel, CancellationToken ct)
    {
        try
        {
            string email = ((AccountResponse)ViewBag.LoggedInAccount).Email!;
            var result = await fixedExpenditureService.DeleteAsync(email, fixedExpenditureInputViewModel.Id, ct);
            return result.Success
                ? Json(new { result = true, message = localizer["The fixedExpenditure has been successfully deleted."].Value })
                : Json(new { result = false, error = localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete fixed-expenditure record {FixedExpenditureId}", fixedExpenditureInputViewModel.Id);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }
    #endregion

    #region Excel
    /// <summary>Exports fixed-expenditure records to an Excel file.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> ExportExcelFixedExpenditure(string fileName = "", CancellationToken ct = default)
    {
        AccountResponse account = ViewBag.LoggedInAccount;
        var assets = await assetService.GetAssetsAsync(account.Email!, ct);
        var expenditures = await fixedExpenditureService.GetFixedExpendituresAsync(account.Email!, ct);
        var stream = excelExportService.CreateFixedExpenditureExcel(expenditures, assets, key => localizer[key].Value, account.TimeZoneIanaId!, settings.Value.NoticeMaturityDateDay);
        string name = fileName.ToExcelFileName(account.TimeZoneIanaId!, timeProvider.GetUtcNow().UtcDateTime);
        return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
    }
    #endregion

    #endregion
}
