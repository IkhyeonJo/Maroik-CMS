using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Account;
using Maroik.Core.Domain.Localization;
using Maroik.Website.Attributes;
using Maroik.Website.Contracts;
using Maroik.Website.Extensions;
using Maroik.Website.Mappings;
using Maroik.Website.Models.ViewModels.AccountBook;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;

namespace Maroik.Website.Controllers;

/// <summary>
/// Manages account-book records: assets, income transactions, and expenditure transactions
/// (create, read, update, delete, export).
/// </summary>
/// <param name="localizer">Localizer for this controller's user-facing messages.</param>
/// <param name="logger">Logger for unexpected failures in the account-book actions.</param>
/// <param name="assetService">Asset use cases.</param>
/// <param name="incomeService">Income use cases.</param>
/// <param name="expenditureService">Expenditure use cases.</param>
/// <param name="excelExportService">Builds the asset / income / expenditure Excel exports.</param>
/// <param name="timeProvider">The clock read for the pickers' current date/time and the export file name's timestamp.</param>
public class AccountBookController(
    IHtmlLocalizer<AccountBookController> localizer,
    ILogger<AccountBookController> logger,
    IAssetService assetService,
    IIncomeService incomeService,
    IExpenditureService expenditureService,
    IExcelExportService excelExportService,
    TimeProvider timeProvider) : Controller
{
    #region Asset

    #region Create

    /// <summary>Creates a new asset record.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> CreateAsset([FromBody] AssetInputViewModel assetInputViewModel, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return Json(new { result = false, error = localizer["Input is invalid"].Value });

        string email = HttpContext.GetLoggedInAccount().Email!;

        var request = new AssetRequest
        {
            ProductName = assetInputViewModel.ProductName,
            Item = assetInputViewModel.Item,
            Amount = assetInputViewModel.Amount,
            MonetaryUnit = assetInputViewModel.MonetaryUnit,
            Note = assetInputViewModel.Note ?? "",
            Deleted = false
        };

        var result = await assetService.CreateAsync(email, request, ct);
        return result.Success
            ? Json(new { result = true, message = localizer["The asset has been successfully created."].Value })
            : Json(new { result = false, error = localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
    }

    #endregion

    #region Read

    /// <summary>Returns the asset grid (AJAX) or the full asset page.</summary>
    [HttpGet]
    public async Task<IActionResult> Asset(string wholeSearch, CancellationToken ct)
    {
#pragma warning disable ASP0015
        if (HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
#pragma warning restore ASP0015
        {
            AccountResponse account = HttpContext.GetLoggedInAccount();
            var assets = string.IsNullOrEmpty(wholeSearch)
                ? await assetService.GetAssetsAsync(account.Email!, ct)
                : await assetService.SearchAssetsAsync(account.Email!, wholeSearch, ct);
            var viewModels = assets
                .ToDisplayViewModels(key => localizer[key].Value, account.TimeZoneIanaId!)
                .OrderBy(m => m.ProductName)
                .AsQueryable();
            return PartialView("_AssetGrid", viewModels);
        }

        AccountResponse pageAccount = HttpContext.GetLoggedInAccount();
        string tz = pageAccount.TimeZoneIanaId ?? "UTC";
        var vm = new AccountBookPageViewModel
        {
            Assets =
            [
                .. (await assetService.GetAssetsAsync(pageAccount.Email!, ct))
                .Where(x => !x.Deleted).OrderBy(x => x.ProductName)
            ]
        };
        vm.PopulateTimeData(tz, timeProvider.GetUtcNow().UtcDateTime);
        return View(vm);
    }

    /// <summary>Checks whether an asset with the given product name exists.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> IsAssetExists(string productName, CancellationToken ct)
    {
        try
        {
            AssetResponse? asset = await assetService.GetAssetAsync(
                HttpContext.GetLoggedInAccount().Email!, productName, ct);

            return asset == null
                ? Json(new { result = false, error = localizer["Fail to find the asset by given product name"].Value })
                : Json(new { result = true, asset });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check asset existence for product {ProductName}", productName);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    #endregion

    #region Update

    /// <summary>Updates an existing asset record.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> UpdateAsset([FromBody] AssetInputViewModel assetInputViewModel, CancellationToken ct)
    {
        _ = ModelState.Remove(nameof(assetInputViewModel.MonetaryUnit));
        if (!ModelState.IsValid)
            return Json(new { result = false, error = localizer["Input is invalid"].Value });

        string email = HttpContext.GetLoggedInAccount().Email!;

        var request = new AssetRequest
        {
            ProductName = assetInputViewModel.ProductName,
            Item = assetInputViewModel.Item,
            Amount = assetInputViewModel.Amount,
            MonetaryUnit = assetInputViewModel.MonetaryUnit,
            Note = assetInputViewModel.Note,
            Deleted = assetInputViewModel.Deleted
        };

        var result = await assetService.UpdateAsync(email, request, assetInputViewModel.OriginalProductName!, ct);
        return result.Success
            ? Json(new { result = true, message = localizer["The asset has been successfully updated."].Value })
            : Json(new { result = false, error = localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
    }

    #endregion

    #region Delete

    /// <summary>Soft-deletes an asset (the row and its history are kept; it disappears from the asset dropdowns).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> DeleteAsset([FromBody] AssetInputViewModel assetInputViewModel, CancellationToken ct)
    {
        string email = HttpContext.GetLoggedInAccount().Email!;
        var result = await assetService.DeleteAsync(email, assetInputViewModel.ProductName!, ct);
        return result.Success
            ? Json(new { result = true, message = localizer["The asset has been successfully deleted."].Value })
            : Json(new { result = false, error = localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
    }

    #endregion

    #region Excel

    /// <summary>Exports asset records to an Excel file.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> ExportExcelAsset(string fileName = "", CancellationToken ct = default)
    {
        AccountResponse account = HttpContext.GetLoggedInAccount();
        var assets = await assetService.GetAssetsAsync(account.Email!, ct);
        var stream = excelExportService.CreateAssetExcel(assets, key => localizer[key].Value, account.TimeZoneIanaId!);
        string name = fileName.ToExcelFileName(account.TimeZoneIanaId!, timeProvider.GetUtcNow().UtcDateTime);
        return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
    }

    #endregion

    #endregion

    #region Income

    #region Create

    /// <summary>Creates a new income transaction.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> CreateIncome([FromBody] IncomeInputViewModel incomeInputViewModel, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return Json(new { result = false, error = localizer["Input is invalid"].Value });

        AccountResponse account = HttpContext.GetLoggedInAccount();

        var request = new IncomeRequest
        {
            MainClass = incomeInputViewModel.MainClass,
            SubClass = incomeInputViewModel.SubClass,
            Content = incomeInputViewModel.Content ?? "",
            Amount = incomeInputViewModel.Amount,
            DepositMyAssetProductName = incomeInputViewModel.DepositMyAssetProductName,
            Created = incomeInputViewModel.ToCreatedUtc(account.TimeZoneIanaId ?? "UTC"),
            Note = incomeInputViewModel.Note ?? ""
        };

        var result = await incomeService.CreateAsync(account.Email!, request, ct);
        return result.Success
            ? Json(new { result = true, message = localizer["The income has been successfully created."].Value })
            : Json(new { result = false, error = localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
    }

    #endregion

    #region Read

    /// <summary>Returns the income grid (AJAX) or the full income page.</summary>
    [HttpGet]
    public async Task<IActionResult> Income(string wholeSearch, CancellationToken ct)
    {
#pragma warning disable ASP0015
        if (HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
#pragma warning restore ASP0015
        {
            AccountResponse account = HttpContext.GetLoggedInAccount();
            var assets = await assetService.GetAssetsAsync(account.Email!, ct);
            var incomes = string.IsNullOrEmpty(wholeSearch)
                ? await incomeService.GetIncomesAsync(account.Email!, ct)
                : await incomeService.SearchIncomesAsync(account.Email!, wholeSearch, ct);
            var viewModels = incomes
                .ToDisplayViewModels(assets, key => localizer[key].Value, account.TimeZoneIanaId!)
                .OrderByDescending(m => m.Created)
                .ThenByDescending(m => m.Updated)
                .AsQueryable();
            return PartialView("_IncomeGrid", viewModels);
        }

        AccountResponse pageAccount = HttpContext.GetLoggedInAccount();
        string tz = pageAccount.TimeZoneIanaId ?? "UTC";
        var vm = new AccountBookPageViewModel
        {
            Assets =
            [
                .. (await assetService.GetAssetsAsync(pageAccount.Email!, ct))
                .Where(x => !x.Deleted).OrderBy(x => x.ProductName)
            ]
        };
        vm.PopulateTimeData(tz, timeProvider.GetUtcNow().UtcDateTime);
        return View(vm);
    }

    /// <summary>Checks whether an income record with the given ID exists.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> IsIncomeExists(int id, CancellationToken ct)
    {
        try
        {
            string email = HttpContext.GetLoggedInAccount().Email!;
            string tz = HttpContext.GetLoggedInAccount().TimeZoneIanaId!;

            IncomeResponse? income = await incomeService.GetByIdAsync(email, id, ct);
            if (income == null)
                return Json(new { result = false, error = localizer["Input is invalid"].Value });

            income.Created = income.Created.ConvertTimeByTimeZoneIanaId(tz);
            return Json(new { result = true, income });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check income existence for id {IncomeId}", id);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    /// <summary>Returns a formatted amount label for the given income product.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> GetIncomeAmountLabel(string productName, CancellationToken ct)
    {
        try
        {
            string baseLabel = localizer["Amount"].Value;
            AssetResponse? asset = await assetService.GetAssetAsync(
                HttpContext.GetLoggedInAccount().Email!, productName, ct);

            string label = !string.IsNullOrEmpty(asset?.MonetaryUnit)
                ? baseLabel.GetAmountLabel(asset.MonetaryUnit)
                : baseLabel;

            return Json(new { result = true, label });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get income amount label for product {ProductName}", productName);
            return Json(new { result = false, label = localizer["Amount"].Value });
        }
    }

    #endregion

    #region Update

    /// <summary>Updates an existing income record.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> UpdateIncome([FromBody] IncomeInputViewModel incomeInputViewModel, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return Json(new { result = false, error = localizer["Input is invalid"].Value });

        AccountResponse account = HttpContext.GetLoggedInAccount();

        var request = new IncomeRequest
        {
            Id = incomeInputViewModel.Id,
            MainClass = incomeInputViewModel.MainClass,
            SubClass = incomeInputViewModel.SubClass,
            Content = incomeInputViewModel.Content,
            Amount = incomeInputViewModel.Amount,
            DepositMyAssetProductName = incomeInputViewModel.DepositMyAssetProductName,
            Created = incomeInputViewModel.ToCreatedUtc(account.TimeZoneIanaId ?? "UTC"),
            Note = incomeInputViewModel.Note
        };

        var result = await incomeService.UpdateAsync(account.Email!, request, ct);
        return result.Success
            ? Json(new { result = true, message = localizer["The income has been successfully updated."].Value })
            : Json(new { result = false, error = localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
    }

    #endregion

    #region Delete

    /// <summary>Deletes an income record, reversing its deposit on the linked asset's balance.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> DeleteIncome([FromBody] IncomeInputViewModel incomeInputViewModel, CancellationToken ct)
    {
        string email = HttpContext.GetLoggedInAccount().Email!;
        var result = await incomeService.DeleteAsync(email, incomeInputViewModel.Id, ct);
        return result.Success
            ? Json(new { result = true, message = localizer["The income has been successfully deleted."].Value })
            : Json(new { result = false, error = localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
    }

    #endregion

    #region Excel

    /// <summary>Exports income records to an Excel file.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> ExportExcelIncome(string fileName = "", CancellationToken ct = default)
    {
        AccountResponse account = HttpContext.GetLoggedInAccount();
        var assets = await assetService.GetAssetsAsync(account.Email!, ct);
        var incomes = await incomeService.GetIncomesAsync(account.Email!, ct);
        var stream = excelExportService.CreateIncomeExcel(incomes, assets, key => localizer[key].Value, account.TimeZoneIanaId!);
        string name = fileName.ToExcelFileName(account.TimeZoneIanaId!, timeProvider.GetUtcNow().UtcDateTime);
        return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
    }

    #endregion

    #endregion

    #region Expenditure

    #region Create

    /// <summary>Creates a new expenditure transaction.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> CreateExpenditure(
        [FromBody] ExpenditureInputViewModel expenditureInputViewModel, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return Json(new { result = false, error = localizer["Input is invalid"].Value });

        AccountResponse account = HttpContext.GetLoggedInAccount();

        var request = new ExpenditureRequest
        {
            MainClass = expenditureInputViewModel.MainClass,
            SubClass = expenditureInputViewModel.SubClass,
            Content = expenditureInputViewModel.Content ?? "",
            Amount = expenditureInputViewModel.Amount,
            PaymentMethod = expenditureInputViewModel.PaymentMethod,
            MyDepositAsset = expenditureInputViewModel.MyDepositAsset,
            Created = expenditureInputViewModel.ToCreatedUtc(account.TimeZoneIanaId ?? "UTC"),
            Note = expenditureInputViewModel.Note ?? ""
        };

        var result = await expenditureService.CreateAsync(account.Email!, request, ct);
        return result.Success
            ? Json(new { result = true, message = localizer["The expenditure has been successfully created."].Value })
            : Json(new { result = false, error = localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
    }

    #endregion

    #region Read

    /// <summary>Returns the expenditure grid (AJAX) or the full expenditure page.</summary>
    [HttpGet]
    public async Task<IActionResult> Expenditure(string wholeSearch, CancellationToken ct)
    {
#pragma warning disable ASP0015
        if (HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
#pragma warning restore ASP0015
        {
            AccountResponse account = HttpContext.GetLoggedInAccount();
            var assets = await assetService.GetAssetsAsync(account.Email!, ct);
            var expenditures = string.IsNullOrEmpty(wholeSearch)
                ? await expenditureService.GetExpendituresAsync(account.Email!, ct)
                : await expenditureService.SearchExpendituresAsync(account.Email!, wholeSearch, ct);
            var viewModels = expenditures
                .ToDisplayViewModels(assets, key => localizer[key].Value, account.TimeZoneIanaId!)
                .OrderByDescending(m => m.Created)
                .ThenByDescending(m => m.Updated)
                .AsQueryable();
            return PartialView("_ExpenditureGrid", viewModels);
        }

        AccountResponse pageAccount = HttpContext.GetLoggedInAccount();
        string tz = pageAccount.TimeZoneIanaId ?? "UTC";
        var vm = new AccountBookPageViewModel
        {
            Assets =
            [
                .. (await assetService.GetAssetsAsync(pageAccount.Email!, ct))
                .Where(x => !x.Deleted).OrderBy(x => x.ProductName)
            ]
        };
        vm.PopulateTimeData(tz, timeProvider.GetUtcNow().UtcDateTime);
        return View(vm);
    }

    /// <summary>Checks whether an expenditure record with the given ID exists.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> IsExpenditureExists(int id, CancellationToken ct)
    {
        try
        {
            string email = HttpContext.GetLoggedInAccount().Email!;
            string tz = HttpContext.GetLoggedInAccount().TimeZoneIanaId!;

            ExpenditureResponse? expenditure = await expenditureService.GetByIdAsync(email, id, ct);
            if (expenditure == null)
                return Json(new { result = false, error = localizer["Input is invalid"].Value });

            expenditure.Created = expenditure.Created.ConvertTimeByTimeZoneIanaId(tz);
            return Json(new { result = true, expenditure });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check expenditure existence for id {ExpenditureId}", id);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    /// <summary>Returns a formatted amount label for the given expenditure product.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> GetExpenditureAmountLabel(string productName, CancellationToken ct)
    {
        try
        {
            string baseLabel = localizer["Amount"].Value;
            AssetResponse? asset = await assetService.GetAssetAsync(
                HttpContext.GetLoggedInAccount().Email!, productName, ct);

            string label = !string.IsNullOrEmpty(asset?.MonetaryUnit)
                ? baseLabel.GetAmountLabel(asset.MonetaryUnit)
                : baseLabel;

            return Json(new { result = true, label });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get expenditure amount label for product {ProductName}", productName);
            return Json(new { result = false, label = localizer["Amount"].Value });
        }
    }

    #endregion

    #region Update

    /// <summary>Updates an existing expenditure record.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> UpdateExpenditure(
        [FromBody] ExpenditureInputViewModel expenditureInputViewModel, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return Json(new { result = false, error = localizer["Input is invalid"].Value });

        AccountResponse account = HttpContext.GetLoggedInAccount();

        var request = new ExpenditureRequest
        {
            Id = expenditureInputViewModel.Id,
            MainClass = expenditureInputViewModel.MainClass,
            SubClass = expenditureInputViewModel.SubClass,
            Content = expenditureInputViewModel.Content ?? "",
            Amount = expenditureInputViewModel.Amount,
            PaymentMethod = expenditureInputViewModel.PaymentMethod,
            MyDepositAsset = expenditureInputViewModel.MyDepositAsset,
            Created = expenditureInputViewModel.ToCreatedUtc(account.TimeZoneIanaId ?? "UTC"),
            Note = expenditureInputViewModel.Note ?? ""
        };

        var result = await expenditureService.UpdateAsync(account.Email!, request, ct);
        return result.Success
            ? Json(new { result = true, message = localizer["The expenditure has been successfully updated."].Value })
            : Json(new { result = false, error = localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
    }

    #endregion

    #region Delete

    /// <summary>Deletes an expenditure record, reversing its effect on the linked asset balance(s).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> DeleteExpenditure(
        [FromBody] ExpenditureInputViewModel expenditureInputViewModel, CancellationToken ct)
    {
        string email = HttpContext.GetLoggedInAccount().Email!;
        var result = await expenditureService.DeleteAsync(email, expenditureInputViewModel.Id, ct);
        return result.Success
            ? Json(new { result = true, message = localizer["The expenditure has been successfully deleted."].Value })
            : Json(new { result = false, error = localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
    }

    #endregion

    #region Excel

    /// <summary>Exports expenditure records to an Excel file.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> ExportExcelExpenditure(string fileName = "", CancellationToken ct = default)
    {
        AccountResponse account = HttpContext.GetLoggedInAccount();
        var assets = await assetService.GetAssetsAsync(account.Email!, ct);
        var expenditures = await expenditureService.GetExpendituresAsync(account.Email!, ct);
        var stream = excelExportService.CreateExpenditureExcel(expenditures, assets, key => localizer[key].Value, account.TimeZoneIanaId!);
        string name = fileName.ToExcelFileName(account.TimeZoneIanaId!, timeProvider.GetUtcNow().UtcDateTime);
        return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
    }

    #endregion

    #endregion

}
