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
public class AccountBookController : Controller
{
    /// <summary>Localizer for this controller's user-facing messages.</summary>
    private readonly IHtmlLocalizer<AccountBookController> _localizer;
    /// <summary>Logger for unexpected failures in the account-book actions.</summary>
    private readonly ILogger<AccountBookController> _logger;
    /// <summary>Asset use cases.</summary>
    private readonly IAssetService _assetService;
    /// <summary>Income use cases.</summary>
    private readonly IIncomeService _incomeService;
    /// <summary>Expenditure use cases.</summary>
    private readonly IExpenditureService _expenditureService;
    /// <summary>Builds the asset / income / expenditure Excel exports.</summary>
    private readonly IExcelExportService _excelExportService;
    /// <summary>Resource-key → localized text delegate handed to the mappers and the Excel export.</summary>
    private readonly Func<string, string> _localize;
    /// <summary>The clock read for the pickers' current date/time and the export file name's timestamp.</summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of <see cref="AccountBookController"/> with the supplied dependencies.</summary>
    public AccountBookController(
        IHtmlLocalizer<AccountBookController> localizer,
        ILogger<AccountBookController> logger,
        IAssetService assetService,
        IIncomeService incomeService,
        IExpenditureService expenditureService,
        IExcelExportService excelExportService,
        TimeProvider timeProvider)
    {
        _localizer = localizer;
        _logger = logger;
        _assetService = assetService;
        _incomeService = incomeService;
        _expenditureService = expenditureService;
        _excelExportService = excelExportService;
        _localize = key => _localizer[key].Value;
        _timeProvider = timeProvider;
    }

    #region Asset

    #region Create

    /// <summary>Creates a new asset record.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> CreateAsset([FromBody] AssetInputViewModel assetInputViewModel, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return Json(new { result = false, error = _localizer["Input is invalid"].Value });

        string email = ((AccountResponse)ViewBag.LoggedInAccount).Email!;

        var request = new AssetRequest
        {
            ProductName = assetInputViewModel.ProductName,
            Item = assetInputViewModel.Item,
            Amount = assetInputViewModel.Amount,
            MonetaryUnit = assetInputViewModel.MonetaryUnit,
            Note = assetInputViewModel.Note ?? "",
            Deleted = false
        };

        var result = await _assetService.CreateAsync(email, request, ct);
        return result.Success
            ? Json(new { result = true, message = _localizer["The asset has been successfully created."].Value })
            : Json(new { result = false, error = _localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
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
            AccountResponse account = ViewBag.LoggedInAccount;
            var assets = string.IsNullOrEmpty(wholeSearch)
                ? await _assetService.GetAssetsAsync(account.Email!, ct)
                : await _assetService.SearchAssetsAsync(account.Email!, wholeSearch, ct);
            var viewModels = assets
                .ToDisplayViewModels(_localize, account.TimeZoneIanaId!)
                .OrderBy(m => m.ProductName)
                .AsQueryable();
            return PartialView("_AssetGrid", viewModels);
        }

        AccountResponse pageAccount = ViewBag.LoggedInAccount;
        string tz = pageAccount.TimeZoneIanaId ?? "UTC";
        var vm = new AccountBookPageViewModel
        {
            Assets =
            [
                .. (await _assetService.GetAssetsAsync(pageAccount.Email!, ct))
                .Where(x => !x.Deleted).OrderBy(x => x.ProductName)
            ]
        };
        vm.PopulateTimeData(tz, _timeProvider.GetUtcNow().UtcDateTime);
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
            AssetResponse? asset = await _assetService.GetAssetAsync(
                ((AccountResponse)ViewBag.LoggedInAccount).Email!, productName, ct);

            return asset == null
                ? Json(new { result = false, error = _localizer["Fail to find the asset by given product name"].Value })
                : Json(new { result = true, asset });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check asset existence for product {ProductName}", productName);
            return Json(new { result = false, error = _localizer[ServiceResult.TemporaryErrorKey].Value });
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
            return Json(new { result = false, error = _localizer["Input is invalid"].Value });

        string email = ((AccountResponse)ViewBag.LoggedInAccount).Email!;

        var request = new AssetRequest
        {
            ProductName = assetInputViewModel.ProductName,
            Item = assetInputViewModel.Item,
            Amount = assetInputViewModel.Amount,
            MonetaryUnit = assetInputViewModel.MonetaryUnit,
            Note = assetInputViewModel.Note,
            Deleted = assetInputViewModel.Deleted
        };

        var result = await _assetService.UpdateAsync(email, request, assetInputViewModel.OriginalProductName!, ct);
        return result.Success
            ? Json(new { result = true, message = _localizer["The asset has been successfully updated."].Value })
            : Json(new { result = false, error = _localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
    }

    #endregion

    #region Delete

    /// <summary>Soft-deletes an asset (the row and its history are kept; it disappears from the asset dropdowns).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> DeleteAsset([FromBody] AssetInputViewModel assetInputViewModel, CancellationToken ct)
    {
        string email = ((AccountResponse)ViewBag.LoggedInAccount).Email!;
        var result = await _assetService.DeleteAsync(email, assetInputViewModel.ProductName!, ct);
        return result.Success
            ? Json(new { result = true, message = _localizer["The asset has been successfully deleted."].Value })
            : Json(new { result = false, error = _localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
    }

    #endregion

    #region Excel

    /// <summary>Exports asset records to an Excel file.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> ExportExcelAsset(string fileName = "", CancellationToken ct = default)
    {
        AccountResponse account = ViewBag.LoggedInAccount;
        var assets = await _assetService.GetAssetsAsync(account.Email!, ct);
        var stream = _excelExportService.CreateAssetExcel(assets, _localize, account.TimeZoneIanaId!);
        string name = fileName.ToExcelFileName(account.TimeZoneIanaId!, _timeProvider.GetUtcNow().UtcDateTime);
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
            return Json(new { result = false, error = _localizer["Input is invalid"].Value });

        AccountResponse account = ViewBag.LoggedInAccount;

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

        var result = await _incomeService.CreateAsync(account.Email!, request, ct);
        return result.Success
            ? Json(new { result = true, message = _localizer["The income has been successfully created."].Value })
            : Json(new { result = false, error = _localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
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
            AccountResponse account = ViewBag.LoggedInAccount;
            var assets = await _assetService.GetAssetsAsync(account.Email!, ct);
            var incomes = string.IsNullOrEmpty(wholeSearch)
                ? await _incomeService.GetIncomesAsync(account.Email!, ct)
                : await _incomeService.SearchIncomesAsync(account.Email!, wholeSearch, ct);
            var viewModels = incomes
                .ToDisplayViewModels(assets, _localize, account.TimeZoneIanaId!)
                .OrderByDescending(m => m.Created)
                .ThenByDescending(m => m.Updated)
                .AsQueryable();
            return PartialView("_IncomeGrid", viewModels);
        }

        AccountResponse pageAccount = ViewBag.LoggedInAccount;
        string tz = pageAccount.TimeZoneIanaId ?? "UTC";
        var vm = new AccountBookPageViewModel
        {
            Assets =
            [
                .. (await _assetService.GetAssetsAsync(pageAccount.Email!, ct))
                .Where(x => !x.Deleted).OrderBy(x => x.ProductName)
            ]
        };
        vm.PopulateTimeData(tz, _timeProvider.GetUtcNow().UtcDateTime);
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
            string email = ((AccountResponse)ViewBag.LoggedInAccount).Email!;
            string tz = ((AccountResponse)ViewBag.LoggedInAccount).TimeZoneIanaId!;

            IncomeResponse? income = await _incomeService.GetByIdAsync(email, id, ct);
            if (income == null)
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });

            income.Created = income.Created.ConvertTimeByTimeZoneIanaId(tz);
            return Json(new { result = true, income });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check income existence for id {IncomeId}", id);
            return Json(new { result = false, error = _localizer[ServiceResult.TemporaryErrorKey].Value });
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
            string baseLabel = _localizer["Amount"].Value;
            AssetResponse? asset = await _assetService.GetAssetAsync(
                ((AccountResponse)ViewBag.LoggedInAccount).Email!, productName, ct);

            string label = !string.IsNullOrEmpty(asset?.MonetaryUnit)
                ? baseLabel.GetAmountLabel(asset.MonetaryUnit)
                : baseLabel;

            return Json(new { result = true, label });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get income amount label for product {ProductName}", productName);
            return Json(new { result = false, label = _localizer["Amount"].Value });
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
            return Json(new { result = false, error = _localizer["Input is invalid"].Value });

        AccountResponse account = ViewBag.LoggedInAccount;

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

        var result = await _incomeService.UpdateAsync(account.Email!, request, ct);
        return result.Success
            ? Json(new { result = true, message = _localizer["The income has been successfully updated."].Value })
            : Json(new { result = false, error = _localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
    }

    #endregion

    #region Delete

    /// <summary>Deletes an income record, reversing its deposit on the linked asset's balance.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> DeleteIncome([FromBody] IncomeInputViewModel incomeInputViewModel, CancellationToken ct)
    {
        string email = ((AccountResponse)ViewBag.LoggedInAccount).Email!;
        var result = await _incomeService.DeleteAsync(email, incomeInputViewModel.Id, ct);
        return result.Success
            ? Json(new { result = true, message = _localizer["The income has been successfully deleted."].Value })
            : Json(new { result = false, error = _localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
    }

    #endregion

    #region Excel

    /// <summary>Exports income records to an Excel file.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> ExportExcelIncome(string fileName = "", CancellationToken ct = default)
    {
        AccountResponse account = ViewBag.LoggedInAccount;
        var assets = await _assetService.GetAssetsAsync(account.Email!, ct);
        var incomes = await _incomeService.GetIncomesAsync(account.Email!, ct);
        var stream = _excelExportService.CreateIncomeExcel(incomes, assets, _localize, account.TimeZoneIanaId!);
        string name = fileName.ToExcelFileName(account.TimeZoneIanaId!, _timeProvider.GetUtcNow().UtcDateTime);
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
            return Json(new { result = false, error = _localizer["Input is invalid"].Value });

        AccountResponse account = ViewBag.LoggedInAccount;

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

        var result = await _expenditureService.CreateAsync(account.Email!, request, ct);
        return result.Success
            ? Json(new { result = true, message = _localizer["The expenditure has been successfully created."].Value })
            : Json(new { result = false, error = _localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
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
            AccountResponse account = ViewBag.LoggedInAccount;
            var assets = await _assetService.GetAssetsAsync(account.Email!, ct);
            var expenditures = string.IsNullOrEmpty(wholeSearch)
                ? await _expenditureService.GetExpendituresAsync(account.Email!, ct)
                : await _expenditureService.SearchExpendituresAsync(account.Email!, wholeSearch, ct);
            var viewModels = expenditures
                .ToDisplayViewModels(assets, _localize, account.TimeZoneIanaId!)
                .OrderByDescending(m => m.Created)
                .ThenByDescending(m => m.Updated)
                .AsQueryable();
            return PartialView("_ExpenditureGrid", viewModels);
        }

        AccountResponse pageAccount = ViewBag.LoggedInAccount;
        string tz = pageAccount.TimeZoneIanaId ?? "UTC";
        var vm = new AccountBookPageViewModel
        {
            Assets =
            [
                .. (await _assetService.GetAssetsAsync(pageAccount.Email!, ct))
                .Where(x => !x.Deleted).OrderBy(x => x.ProductName)
            ]
        };
        vm.PopulateTimeData(tz, _timeProvider.GetUtcNow().UtcDateTime);
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
            string email = ((AccountResponse)ViewBag.LoggedInAccount).Email!;
            string tz = ((AccountResponse)ViewBag.LoggedInAccount).TimeZoneIanaId!;

            ExpenditureResponse? expenditure = await _expenditureService.GetByIdAsync(email, id, ct);
            if (expenditure == null)
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });

            expenditure.Created = expenditure.Created.ConvertTimeByTimeZoneIanaId(tz);
            return Json(new { result = true, expenditure });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check expenditure existence for id {ExpenditureId}", id);
            return Json(new { result = false, error = _localizer[ServiceResult.TemporaryErrorKey].Value });
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
            string baseLabel = _localizer["Amount"].Value;
            AssetResponse? asset = await _assetService.GetAssetAsync(
                ((AccountResponse)ViewBag.LoggedInAccount).Email!, productName, ct);

            string label = !string.IsNullOrEmpty(asset?.MonetaryUnit)
                ? baseLabel.GetAmountLabel(asset.MonetaryUnit)
                : baseLabel;

            return Json(new { result = true, label });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get expenditure amount label for product {ProductName}", productName);
            return Json(new { result = false, label = _localizer["Amount"].Value });
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
            return Json(new { result = false, error = _localizer["Input is invalid"].Value });

        AccountResponse account = ViewBag.LoggedInAccount;

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

        var result = await _expenditureService.UpdateAsync(account.Email!, request, ct);
        return result.Success
            ? Json(new { result = true, message = _localizer["The expenditure has been successfully updated."].Value })
            : Json(new { result = false, error = _localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
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
        string email = ((AccountResponse)ViewBag.LoggedInAccount).Email!;
        var result = await _expenditureService.DeleteAsync(email, expenditureInputViewModel.Id, ct);
        return result.Success
            ? Json(new { result = true, message = _localizer["The expenditure has been successfully deleted."].Value })
            : Json(new { result = false, error = _localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
    }

    #endregion

    #region Excel

    /// <summary>Exports expenditure records to an Excel file.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> ExportExcelExpenditure(string fileName = "", CancellationToken ct = default)
    {
        AccountResponse account = ViewBag.LoggedInAccount;
        var assets = await _assetService.GetAssetsAsync(account.Email!, ct);
        var expenditures = await _expenditureService.GetExpendituresAsync(account.Email!, ct);
        var stream = _excelExportService.CreateExpenditureExcel(expenditures, assets, _localize, account.TimeZoneIanaId!);
        string name = fileName.ToExcelFileName(account.TimeZoneIanaId!, _timeProvider.GetUtcNow().UtcDateTime);
        return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
    }

    #endregion

    #endregion

}
