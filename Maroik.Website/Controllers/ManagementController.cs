using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Misc.Extensions;
using Maroik.Core.Contract.Misc.Helpers;
using Maroik.Core.Contract.Misc.Settings;
using Maroik.Core.Domain.Account;
using Maroik.Core.Domain.Board;
using Maroik.Core.Domain.Localization;
using Maroik.Core.Domain.Media;
using Maroik.Website.Attributes;
using Maroik.Website.Contracts;
using Maroik.Website.Extensions;
using Maroik.Website.Mappings;
// ReSharper disable ConvertToConstant.Local
using Maroik.Website.Models;
using Maroik.Website.Models.ViewModels.Management;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace Maroik.Website.Controllers;

/// <summary>
/// The Management area: every signed-in account's own profile (avatar, time zone, password) and private
/// notes, plus the admin-only account management and navigation-menu (categories and sub-categories) pages.
/// </summary>
public class ManagementController : Controller
{
    /// <summary>Encrypts the storage path returned for an uploaded Summernote image.</summary>
    private readonly IRsaService _rsa;
    /// <summary>Localizer for this controller's user-facing messages.</summary>
    private readonly IHtmlLocalizer<ManagementController> _localizer;
    /// <summary>Logger for unexpected failures in the Management actions.</summary>
    private readonly ILogger<ManagementController> _logger;
    /// <summary>Self-service profile use cases (avatar, time zone, password).</summary>
    private readonly IProfileService _profileService;
    /// <summary>Account reads (writer nicknames of the private-note pages).</summary>
    private readonly IAccountService _accountService;
    /// <summary>Admin account-management use cases.</summary>
    private readonly IManagementAccountService _managementAccountService;
    /// <summary>Admin navigation-menu use cases.</summary>
    private readonly IMenuService _menuService;
    /// <summary>Private-note (PrivateNote board) use cases.</summary>
    private readonly IBoardService _boardService;
    /// <summary>Reads / refreshes / clears the signed-in account stored in the session.</summary>
    private readonly ISessionService _sessionService;
    /// <summary>Builds the account / menu Excel exports.</summary>
    private readonly IExcelExportService _excelExportService;
    /// <summary>Distributed cache whose navigation-menu entries are invalidated after a menu change.</summary>
    private readonly IDistributedCache _cache;
    /// <summary>Server settings (upload size cap).</summary>
    private readonly IOptions<ServerSetting> _serverSettings;
    /// <summary>Resource-key → localized text delegate handed to the mappers and the Excel export.</summary>
    private readonly Func<string, string> _localize;

    /// <summary>Initializes a new instance of <see cref="ManagementController"/> with the supplied dependencies.</summary>
    public ManagementController(
        IHtmlLocalizer<ManagementController> localizer,
        ILogger<ManagementController> logger,
        IProfileService profileService,
        IAccountService accountService,
        IManagementAccountService managementAccountService,
        IMenuService menuService,
        IBoardService boardService,
        IRsaService rsa,
        IOptions<ServerSetting> serverSettings,
        ISessionService sessionService,
        IExcelExportService excelExportService,
        IDistributedCache cache)
    {
        _rsa = rsa;
        _sessionService = sessionService;
        _localizer = localizer;
        _logger = logger;
        _profileService = profileService;
        _accountService = accountService;
        _managementAccountService = managementAccountService;
        _menuService = menuService;
        _boardService = boardService;
        _excelExportService = excelExportService;
        _cache = cache;
        _serverSettings = serverSettings;
        _localize = key => _localizer[key].Value;
    }

    /// <summary>
    /// E-mail of the signed-in administrator, passed to the admin write use cases so their audit log
    /// records who made the change (ViewBag.LoggedInAccount is set by ViewBagPopulatorFilter).
    /// </summary>
    private string AdminEmail => ((AccountResponse)ViewBag.LoggedInAccount).Email!;

    #region Profile

    #region Read

    /// <summary>Displays the current user's profile page.</summary>
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        AccountResponse loggedInAccount = ViewBag.LoggedInAccount;
        string? loggedInAccountTimeZoneIanaId = loggedInAccount.TimeZoneIanaId;
        AccountResponse? tempAccount = await _profileService.GetProfileAsync(loggedInAccount.Email!, HttpContext.RequestAborted);

        return View(new ProfileOutputViewModel()
        {
            Email = tempAccount!.Email,
            AvatarImagePath = tempAccount.AvatarImagePath,
            Nickname = tempAccount.Nickname,
            Created = tempAccount.Created.ConvertTimeByTimeZoneIanaId(loggedInAccountTimeZoneIanaId!),
            TimeZoneIanaId = tempAccount.TimeZoneIanaId,
            MustChangePassword = tempAccount.MustChangePassword
        });
    }

    #endregion

    #region Update

    #region ProfileAvatar

    /// <summary>Updates the current user's avatar image.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> UpdateProfileAvatar(ProfileInputViewModel? profileInputViewModel)
    {
        // A request with no form fields at all (an empty multipart body) is not bound to a model — treat it as "no file".
        if (profileInputViewModel?.ProfileAvatarFiles == null || profileInputViewModel.ProfileAvatarFiles.Count == 0)
        {
            return Ok(new { result = false, errorMessage = _localizer["Please attach a file"].Value });
        }

        IFormFile file = profileInputViewModel.ProfileAvatarFiles[0];

        if (file.Length <= 0 || file.Length > _serverSettings.Value.MaxAttachedFileSizeBytes)
            return Ok(new { result = false, errorMessage = _localizer["File Size must be smaller than {0}MB.", _serverSettings.Value.MaxAttachedFileSizeBytes / (1024 * 1024)].ToPlainString() });

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!ImageUploadPolicy.IsAllowedExtension(extension))
            return Ok(new { result = false, errorMessage = _localizer["Only .jpg or jpeg or .png file allowed"].Value });

        byte[] imageBytes;
        using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms);
            imageBytes = ms.ToArray();
        }

        string? email = ((AccountResponse)ViewBag.LoggedInAccount).Email;

        ServiceResult avatarResult = await _profileService.UploadAndUpdateAvatarAsync(email!, imageBytes, extension, HttpContext.RequestAborted);

        if (!avatarResult.Success)
        {
            return avatarResult.ErrorKey switch
            {
                "virus-detected" => Ok(new { result = false, errorMessage = _localizer["File may be infected with a virus."].Value }),
                "scan-unavailable" => Ok(new { result = false, errorMessage = _localizer["The file could not be scanned for viruses. Please try again later."].Value }),
                "svg-not-allowed" => Ok(new { result = false, errorMessage = _localizer["SVG format is not allowed"].Value }),
                "invalid-image" => Ok(new { result = false, errorMessage = _localizer["Invalid image file"].Value }),
                ServiceResult.TemporaryErrorKey => Ok(new { result = false, errorMessage = _localizer[ServiceResult.TemporaryErrorKey].Value }),
                _ => Ok(new { result = false, errorMessage = _localizer["Input is invalid"].Value })
            };
        }

        return Ok(new { result = true });
    }

    #endregion

    #region ProfileTimeZone

    /// <summary>Updates the current user's time-zone setting.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> UpdateProfileTimeZone(ProfileInputViewModel profileInputViewModel)
    {
        _ = ModelState.Remove(nameof(profileInputViewModel.Nickname));
        _ = ModelState.Remove(nameof(profileInputViewModel.Password));
        _ = ModelState.Remove(nameof(profileInputViewModel.NewPassword));

        if (!ModelState.IsValid)
        {
            return RedirectToAction("Profile", "Management");
        }

        string? email = ((AccountResponse)ViewBag.LoggedInAccount).Email;
        try
        {
            await _profileService.UpdateTimeZoneAsync(email!, profileInputViewModel.TimeZoneIanaId!, HttpContext.RequestAborted);

            // Refresh session with updated timezone. GetProfileAsync returns a plain AccountResponse,
            // which no longer carries a password hash at all (see AdminAccountResponse).
            AccountResponse? updatedAccount = await _profileService.GetProfileAsync(email!, HttpContext.RequestAborted);
            _sessionService.RemoveAccount();
            _sessionService.SetAccount(updatedAccount!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update time zone for account {AccountEmail}", email);
        }

        return RedirectToAction("Profile", "Management");
    }

    #endregion

    #region ProfilePassword

    /// <summary>Changes the current user's password.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> UpdateProfilePassword([FromBody] ProfileInputViewModel profileInputViewModel)
    {
        _ = ModelState.Remove(nameof(profileInputViewModel.Nickname));
        _ = ModelState.Remove(nameof(profileInputViewModel.TimeZoneIanaId));

        if (!ModelState.IsValid)
            return Json(new { result = false, error = _localizer["Input is invalid"].Value });

        string? email = ((AccountResponse)ViewBag.LoggedInAccount).Email;
        try
        {
            ServiceResult result = await _profileService.UpdatePasswordAsync(
                email!,
                profileInputViewModel.Password ?? "",
                profileInputViewModel.NewPassword ?? "",
                HttpContext.RequestAborted);

            if (result.Success)
            {
                // ChangePassword regenerated the account's SecurityStamp, so every session of this
                // account is now invalid — this one included. Drop it here explicitly and let the
                // client send the user to the sign-in page (they sign in again with the new password).
                _sessionService.RemoveAccount();
            }

            return result.Success
                ? Json(new { result = true, message = _localizer["Password changed. Please sign in again."].Value })
                : Json(new { result = false, error = _localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update password for account {AccountEmail}", email);
            return Json(new { result = false, error = _localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    #endregion

    #endregion

    #endregion

    #region Account

    #region Create

    /// <summary>Creates a new user account (admin action).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    public async Task<IActionResult> CreateAccount([FromBody] AccountInputViewModel accountInputViewModel)
    {
        try
        {
            _ = ModelState.Remove(nameof(accountInputViewModel.AvatarImagePath));
            _ = ModelState.Remove(nameof(accountInputViewModel.RegistrationToken));
            _ = ModelState.Remove(nameof(accountInputViewModel.ResetPasswordToken));
            _ = ModelState.Remove(nameof(accountInputViewModel.Message));
            // TimeZoneIanaId's [Required] would otherwise reject an empty value before the
            // friendlier "Please select time zone" check further down ever runs.
            _ = ModelState.Remove(nameof(accountInputViewModel.TimeZoneIanaId));

            if (!ModelState.IsValid)
            {
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
            }

            #region Create account

            // Email format, password complexity, nickname length, role and time-zone are all
            // validated by ManagementAccountService / Account.Create / PasswordPolicy in the Core
            // layers — the controller only binds the request and renders the result.
            ServiceResult createResult = await _managementAccountService.CreateAccountAsync(new AdminCreateAccountRequest
            {
                Email = accountInputViewModel.Email,
                PlainPassword = accountInputViewModel.Password, // service validates + hashes
                Nickname = accountInputViewModel.Nickname,
                Role = accountInputViewModel.Role,
                TimeZoneIanaId = accountInputViewModel.TimeZoneIanaId,
                EmailConfirmed = true,
                AgreedServiceTerms = true,
                Message = EnumHelper.GetDescription(AccountMessage.Success)
            }, AdminEmail, HttpContext.RequestAborted);

            return createResult.Success
                ? Json(new { result = true, message = _localizer["Account create successfully done."].Value })
                : Json(new { result = false, error = _localizer[createResult.ErrorKey, createResult.ErrorArgs].ToPlainString() });

            #endregion

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create account {AccountEmail}", accountInputViewModel.Email);
            return Json(new { result = false, error = _localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    #endregion

    #region Read

    /// <summary>Returns the account management grid (AJAX) or the full page.</summary>
    [HttpGet]
    public async Task<IActionResult> Account(string wholeSearch)
    {
#pragma warning disable ASP0015
        if (HttpContext.Request.Headers["X-Requested-With"] != "XMLHttpRequest")
#pragma warning restore ASP0015
        {
            return View();
        }

        string tz = ((AccountResponse)ViewBag.LoggedInAccount).TimeZoneIanaId!;
        var accounts = string.IsNullOrEmpty(wholeSearch)
            ? await _managementAccountService.GetAllAccountsAsync(HttpContext.RequestAborted)
            : await _managementAccountService.SearchAccountsAsync(wholeSearch, HttpContext.RequestAborted);
        var viewModels = accounts
            .ToDisplayViewModels(tz)
            .OrderBy(m => m.Email) // the e-mail address is the account's unique key, so no tie-breaker is needed
            .AsQueryable();
        return PartialView("_AccountGrid", viewModels);

    }

    /// <summary>Checks whether an account with the given email exists.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    public async Task<IActionResult> IsAccountExists(string email)
    {
        try
        {
            AccountResponse? tempAccount = await _managementAccountService.GetAccountByEmailAsync(email, HttpContext.RequestAborted);

            return tempAccount == null
                ? Json(new
                {
                    result = false,
                    error = _localizer["Fail to find the account by given email address"].Value
                })
                : (IActionResult)Json(new { result = true, account = tempAccount });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check account existence for email {AccountEmail}", email);
            return Json(new { result = false, error = _localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    #endregion

    #region Update

    /// <summary>Updates an existing account's fields.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    public async Task<IActionResult> UpdateAccount([FromBody] AccountInputViewModel accountInputViewModel)
    {
        try
        {
            _ = ModelState.Remove(nameof(accountInputViewModel.Nickname));
            _ = ModelState.Remove(nameof(accountInputViewModel.AvatarImagePath));
            _ = ModelState.Remove(nameof(accountInputViewModel.RegistrationToken));
            _ = ModelState.Remove(nameof(accountInputViewModel.ResetPasswordToken));
            // Password/Message are optional on update — an empty Password means "leave the
            // current password unchanged" (the service only calls AdminResetPassword when non-empty),
            // and Message has no [Required]-worthy business meaning here.
            _ = ModelState.Remove(nameof(accountInputViewModel.Password));
            _ = ModelState.Remove(nameof(accountInputViewModel.Message));

            if (!ModelState.IsValid)
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });

            // Role validity is enforced by Account.ChangeRole in the Domain layer, which rejects
            // the request with a validation error below instead of the controller silently
            // coercing an invalid value.
            ServiceResult updateResult = await _managementAccountService.UpdateAccountAsync(
                new AdminUpdateAccountRequest
                {
                    Email = accountInputViewModel.Email,
                    Role = accountInputViewModel.Role,
                    TimeZoneIanaId = accountInputViewModel.TimeZoneIanaId,
                    Locked = accountInputViewModel.Locked,
                    EmailConfirmed = accountInputViewModel.EmailConfirmed,
                    AgreedServiceTerms = accountInputViewModel.AgreedServiceTerms,
                    Message = accountInputViewModel.Message,
                    Deleted = accountInputViewModel.Deleted
                },
                accountInputViewModel.Password, // new password (plain); service hashes it
                AdminEmail, HttpContext.RequestAborted);

            return updateResult.Success
                ? Json(new { result = true, message = _localizer["Successfully updated the account"].Value })
                : Json(new { result = false, error = _localizer[updateResult.ErrorKey, updateResult.ErrorArgs].ToPlainString() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update account {AccountEmail}", accountInputViewModel.Email);
            return Json(new { result = false, error = _localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    #endregion

    #region Delete

    /// <summary>Soft-deletes an existing account.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    public async Task<IActionResult> DeleteAccount([FromBody] AccountInputViewModel accountInputViewModel)
    {
        try
        {
            ServiceResult deleteResult = await _managementAccountService.DeleteAccountAsync(accountInputViewModel.Email!, AdminEmail, HttpContext.RequestAborted);
            return deleteResult.Success
                ? Json(new { result = true, message = _localizer["Successfully deleted the account"].Value })
                : Json(new { result = false, error = _localizer[deleteResult.ErrorKey, deleteResult.ErrorArgs].ToPlainString() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete account {AccountEmail}", accountInputViewModel.Email);
            return Json(new { result = false, error = _localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    #endregion

    #region Excel

    /// <summary>Exports the account list to an Excel file.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    public async Task<IActionResult> ExportExcelAccount(string fileName = "")
    {
        AccountResponse account = ViewBag.LoggedInAccount;
        var accounts = await _managementAccountService.GetAllAccountsAsync(HttpContext.RequestAborted);
        var stream = _excelExportService.CreateAccountExcel(accounts, _localize, account.TimeZoneIanaId!);
        string name = fileName.ToExcelFileName(account.TimeZoneIanaId!);
        return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
    }

    #endregion

    #endregion

    #region Menu

    #region Create

    /// <summary>Creates a new navigation category.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    public async Task<IActionResult> CreateCategory([FromBody] MenuInputViewModel menuInputViewModel)
    {
        try
        {
            _ = ModelState.Remove(nameof(menuInputViewModel
                .Action)); // remove ModelState check in Action (in Request parameters, Action can be null)

            if (!ModelState.IsValid)
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });

            ServiceResult createCatResult = await _menuService.CreateCategoryAsync(new CategoryRequest
            {
                Name = menuInputViewModel.Name,
                DisplayName = menuInputViewModel.DisplayName,
                IconPath = menuInputViewModel.IconPath,
                Controller = menuInputViewModel.Controller,
                Action = menuInputViewModel.Action,
                Role = menuInputViewModel.Role,
                Order = menuInputViewModel.Order
            }, AdminEmail, HttpContext.RequestAborted);
            if (!createCatResult.Success)
                return Json(new { result = false, error = _localizer[createCatResult.ErrorKey, createCatResult.ErrorArgs].ToPlainString() });
            await _cache.InvalidateNavigationMenuCacheAsync();
            return Json(new { result = true, message = _localizer["Successfully created the category"].Value });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create menu category {CategoryName}", menuInputViewModel.Name);
            return Json(new { result = false, error = _localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    /// <summary>Creates a new navigation sub-category.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    public async Task<IActionResult> CreateSubCategory([FromBody] MenuInputViewModel menuInputViewModel)
    {
        try
        {
            _ = ModelState.Remove(nameof(menuInputViewModel
                .Controller)); // remove ModelState check in Controller (in Request parameters, Controller can be null)

            if (!ModelState.IsValid)
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });

            ServiceResult createSubCatResult = await _menuService.CreateSubCategoryAsync(new SubCategoryRequest
            {
                CategoryId = menuInputViewModel.CategoryId,
                Name = menuInputViewModel.Name,
                DisplayName = menuInputViewModel.DisplayName,
                IconPath = menuInputViewModel.IconPath,
                Action = menuInputViewModel.Action,
                Role = menuInputViewModel.Role,
                Order = menuInputViewModel.Order
            }, AdminEmail, HttpContext.RequestAborted);
            if (!createSubCatResult.Success)
                return Json(new { result = false, error = _localizer[createSubCatResult.ErrorKey, createSubCatResult.ErrorArgs].ToPlainString() });
            await _cache.InvalidateNavigationMenuCacheAsync();
            return Json(new { result = true, message = _localizer["Successfully created the subCategory"].Value });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create menu sub-category {SubCategoryName}", menuInputViewModel.Name);
            return Json(new { result = false, error = _localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    #endregion

    #region Read

    /// <summary>Returns the menu management grid (AJAX) or the full page.</summary>
    [HttpGet]
    public async Task<IActionResult> Menu(string wholeSearch)
    {
#pragma warning disable ASP0015
        if (HttpContext.Request.Headers["X-Requested-With"] != "XMLHttpRequest") // ajax
#pragma warning restore ASP0015
        {
            return View();
        }

        // Whole-search filtering is a Service-layer concern (IMenuService.SearchCategoriesAsync /
        // SearchSubCategoriesAsync); the controller only reshapes the returned DTOs into the grid's
        // view model.
        List<MenuOutputViewModel> menuOutputViewModels =
        [
            .. (await _menuService.SearchCategoriesAsync(wholeSearch, HttpContext.RequestAborted))
            .Select(item => new MenuOutputViewModel
            {
                Id = item.Id,
                CategoryId = null,
                Name = item.Name,
                DisplayName = item.DisplayName,
                IconPath = item.IconPath,
                Controller = item.Controller,
                Action = item.Action,
                Role = item.Role,
                Order = item.Order
            }),

            .. (await _menuService.SearchSubCategoriesAsync(wholeSearch, HttpContext.RequestAborted))
            .Select(item => new MenuOutputViewModel
            {
                Id = item.Id,
                CategoryId = item.CategoryId,
                Name = item.Name,
                DisplayName = item.DisplayName,
                IconPath = item.IconPath,
                Controller = null,
                Action = item.Action,
                Role = item.Role,
                Order = item.Order
            })
        ];

        IQueryable<MenuOutputViewModel> result = menuOutputViewModels.OrderByDescending(m => m.Id)
            .ThenByDescending(m => m.CategoryId).AsQueryable();
        return PartialView("_MenuGrid", result);

    }

    /// <summary>Checks whether a category with the given ID exists.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    public async Task<IActionResult> IsCategoryExists(int id)
    {
        try
        {
            CategoryResponse? category = await _menuService.GetCategoryByIdAsync(id, HttpContext.RequestAborted);
            if (category == null)
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });

            MenuOutputViewModel vm = new()
            {
                Id = category.Id,
                CategoryId = null,
                Name = category.Name,
                DisplayName = category.DisplayName,
                IconPath = category.IconPath,
                Controller = category.Controller,
                Action = category.Action,
                Role = category.Role,
                Order = category.Order
            };
            return Json(new { result = true, category = vm });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check category existence for id {CategoryId}", id);
            return Json(new { result = false, error = _localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    /// <summary>Checks whether a sub-category with the given ID exists.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    public async Task<IActionResult> IsSubCategoryExists(int id)
    {
        try
        {
            SubCategoryResponse? subCategory = await _menuService.GetSubCategoryByIdAsync(id, HttpContext.RequestAborted);
            if (subCategory == null)
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });

            MenuOutputViewModel vm = new()
            {
                Id = subCategory.Id,
                CategoryId = subCategory.CategoryId,
                Name = subCategory.Name,
                DisplayName = subCategory.DisplayName,
                IconPath = subCategory.IconPath,
                Controller = null,
                Action = subCategory.Action,
                Role = subCategory.Role,
                Order = subCategory.Order
            };
            return Json(new { result = true, subCategory = vm });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check sub-category existence for id {SubCategoryId}", id);
            return Json(new { result = false, error = _localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    #endregion

    #region Update

    /// <summary>Updates an existing navigation category.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    public async Task<IActionResult> UpdateCategory([FromBody] MenuInputViewModel menuInputViewModel)
    {
        try
        {
            _ = ModelState.Remove(nameof(menuInputViewModel
                .Action)); // remove ModelState check in Action (in Request parameters, Action can be null)

            if (!ModelState.IsValid)
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });

            ServiceResult updateCatResult = await _menuService.UpdateCategoryAsync(new CategoryRequest
            {
                Id = menuInputViewModel.Id,
                Name = menuInputViewModel.Name,
                DisplayName = menuInputViewModel.DisplayName,
                IconPath = menuInputViewModel.IconPath,
                Controller = menuInputViewModel.Controller,
                Action = menuInputViewModel.Action,
                Role = menuInputViewModel.Role,
                Order = menuInputViewModel.Order
            }, AdminEmail, HttpContext.RequestAborted);
            if (!updateCatResult.Success)
                return Json(new { result = false, error = _localizer[updateCatResult.ErrorKey, updateCatResult.ErrorArgs].ToPlainString() });
            await _cache.InvalidateNavigationMenuCacheAsync();
            return Json(new { result = true, message = _localizer["Successfully updated the category"].Value });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update menu category {CategoryId}", menuInputViewModel.Id);
            return Json(new { result = false, error = _localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    /// <summary>Updates an existing navigation sub-category.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    public async Task<IActionResult> UpdateSubCategory([FromBody] MenuInputViewModel menuInputViewModel)
    {
        try
        {
            _ = ModelState.Remove(nameof(menuInputViewModel
                .Controller)); // remove ModelState check in Controller (in Request parameters, Controller can be null)

            if (!ModelState.IsValid)
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });

            ServiceResult updateSubCatResult = await _menuService.UpdateSubCategoryAsync(new SubCategoryRequest
            {
                Id = menuInputViewModel.Id,
                CategoryId = menuInputViewModel.CategoryId,
                Name = menuInputViewModel.Name,
                DisplayName = menuInputViewModel.DisplayName,
                IconPath = menuInputViewModel.IconPath,
                Action = menuInputViewModel.Action,
                Role = menuInputViewModel.Role,
                Order = menuInputViewModel.Order
            }, AdminEmail, HttpContext.RequestAborted);
            if (!updateSubCatResult.Success)
                return Json(new { result = false, error = _localizer[updateSubCatResult.ErrorKey, updateSubCatResult.ErrorArgs].ToPlainString() });
            await _cache.InvalidateNavigationMenuCacheAsync();
            return Json(new { result = true, message = _localizer["Successfully updated the subCategory"].Value });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update menu sub-category {SubCategoryId}", menuInputViewModel.Id);
            return Json(new { result = false, error = _localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    #endregion

    #region Delete

    /// <summary>Deletes a navigation category.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    public async Task<IActionResult> DeleteCategory([FromBody] MenuInputViewModel menuInputViewModel)
    {
        try
        {
            _ = ModelState.Remove(nameof(menuInputViewModel
                .Action)); // remove ModelState check in Action (in Request parameters, Action can be null)

            if (!ModelState.IsValid)
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });

            ServiceResult deleteCatResult = await _menuService.DeleteCategoryAsync(new CategoryRequest
            {
                Id = menuInputViewModel.Id,
                Name = menuInputViewModel.Name,
                DisplayName = menuInputViewModel.DisplayName,
                IconPath = menuInputViewModel.IconPath,
                Controller = menuInputViewModel.Controller,
                Action = menuInputViewModel.Action,
                Role = menuInputViewModel.Role,
                Order = menuInputViewModel.Order
            }, AdminEmail, HttpContext.RequestAborted);
            if (!deleteCatResult.Success)
                return Json(new { result = false, error = _localizer[deleteCatResult.ErrorKey, deleteCatResult.ErrorArgs].ToPlainString() });
            await _cache.InvalidateNavigationMenuCacheAsync();
            return Json(new { result = true, message = _localizer["Successfully deleted the category"].Value });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete menu category {CategoryId}", menuInputViewModel.Id);
            return Json(new { result = false, error = _localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    /// <summary>Deletes a navigation sub-category.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    public async Task<IActionResult> DeleteSubCategory([FromBody] MenuInputViewModel menuInputViewModel)
    {
        try
        {
            _ = ModelState.Remove(nameof(menuInputViewModel
                .Controller)); // remove ModelState check in Controller (in Request parameters, Controller can be null)

            if (!ModelState.IsValid)
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });

            ServiceResult deleteSubCatResult = await _menuService.DeleteSubCategoryAsync(new SubCategoryRequest
            {
                Id = menuInputViewModel.Id,
                CategoryId = menuInputViewModel.CategoryId,
                Name = menuInputViewModel.Name,
                DisplayName = menuInputViewModel.DisplayName,
                IconPath = menuInputViewModel.IconPath,
                Action = menuInputViewModel.Action,
                Role = menuInputViewModel.Role,
                Order = menuInputViewModel.Order
            }, AdminEmail, HttpContext.RequestAborted);

            if (!deleteSubCatResult.Success)
                return Json(new { result = false, error = _localizer[deleteSubCatResult.ErrorKey, deleteSubCatResult.ErrorArgs].ToPlainString() });
            await _cache.InvalidateNavigationMenuCacheAsync();
            return Json(new { result = true, message = _localizer["Successfully deleted the subCategory"].Value });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete menu sub-category {SubCategoryId}", menuInputViewModel.Id);
            return Json(new { result = false, error = _localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    #endregion

    #region Excel

    /// <summary>Exports the navigation menu (categories and sub-categories) to an Excel file.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    public async Task<IActionResult> ExportExcelMenu(string fileName = "")
    {
        AccountResponse account = ViewBag.LoggedInAccount;
        var categories = await _menuService.GetAllCategoriesAsync(HttpContext.RequestAborted);
        var subCats = await _menuService.GetAllSubCategoriesAsync(HttpContext.RequestAborted);
        var stream = _excelExportService.CreateMenuExcel(categories, subCats, _localize);
        string name = fileName.ToExcelFileName(account.TimeZoneIanaId!);
        return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
    }

    #endregion

    #endregion

    #region PrivateNote

    #region Create

    #region Write

    #region PrivateNoteBoard

    /// <summary>Creates a new private note (with optional file attachment) owned by the caller.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> WritePrivateNoteBoard(BoardInputViewModel boardInputViewModel)
    {
        try
        {
            if (!ModelState.IsValid)
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });

            boardInputViewModel.Content ??= "";

            // The DB-fresh, re-validated account ViewBagPopulatorFilter already loaded for this
            // request (via HttpContext.Items) — reuse it instead of re-reading the login-time
            // session snapshot, so a mid-session role change takes effect here immediately.
            AccountResponse loggedInAccount = ViewBag.LoggedInAccount;

            if (loggedInAccount.Role is not (Role.Admin or Role.User))
                return Json(new { result = false, error = _localizer["Please Login to write board"].Value });

            BoardRequest boardRequest = new()
            {
                Type = BoardTypes.PrivateNote,
                Title = boardInputViewModel.Title,
                Content = boardInputViewModel.Content,
                Writer = loggedInAccount.Nickname!
            };

            AttachedFileDto? attachedFile = await boardInputViewModel.UploadedFile.ToAttachedFileInfoAsync(_serverSettings.Value.MaxAttachedFileSizeBytes, HttpContext.RequestAborted);

            ServiceResult result = await _boardService.WriteBoardAsync(boardRequest, loggedInAccount.Role == Role.Admin, attachedFile, HttpContext.RequestAborted);
            return result.Success
                ? Json(new { result = true, message = _localizer["The board has been successfully created."].Value })
                : Json(new { result = false, error = _localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write private note board {Title}", boardInputViewModel.Title);
            return Json(new { result = false, error = _localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    #endregion

    #region PrivateNoteComment

    /// <summary>Submits a comment on one of the caller's own private notes.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> WritePrivateNoteComment(
        [FromBody] BoardCommentInputViewModel boardCommentInputViewModel)
    {
        try
        {
            if (!ModelState.IsValid)
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });

            if (string.IsNullOrEmpty(boardCommentInputViewModel.Content))
                return Json(new { result = false, error = _localizer["Please enter a comment."].Value });

            // DB-fresh account already loaded by ViewBagPopulatorFilter for this request (see WritePrivateNoteBoard).
            AccountResponse loggedInAccount = ViewBag.LoggedInAccount;
            if (loggedInAccount.Role is not (Role.Admin or Role.User))
                return Json(new { result = false, error = _localizer["Please Login to write comment."].Value });

            BoardCommentRequest commentRequest = new()
            {
                BoardId = boardCommentInputViewModel.BoardId,
                AvatarImagePath = loggedInAccount.AvatarImagePath!,
                Writer = loggedInAccount.Nickname!,
                Content = boardCommentInputViewModel.Content
            };

            ServiceResult result = await _boardService.WriteCommentAsync(commentRequest, loggedInAccount.Nickname!, requiredType: BoardTypes.PrivateNote, ct: HttpContext.RequestAborted);
            return result.Success
                ? Json(new { result = true, boardId = boardCommentInputViewModel.BoardId, page = boardCommentInputViewModel.DetailCurrentPage })
                : Json(new { result = false, error = _localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write private note comment on board {BoardId}", boardCommentInputViewModel.BoardId);
            return Json(new { result = false, error = _localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    #endregion

    #endregion

    #region Summernote Image File Upload
    /// <summary>
    /// Validates and stores a Summernote inline image for a private note, returning its bytes (base64) and
    /// content type for the editor preview plus its RSA-encrypted storage path (kept in the image's <c>alt</c>).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> UploadImageFile(IFormFile? summernoteImageFile)
    {
        if (summernoteImageFile == null)
            return Ok(new { result = false, errorMessage = _localizer["Please attach a file."].Value });

        // A refused upload is a security event: record who sent what (the attachment service logs its own refusals).
        string uploaderEmail = ((AccountResponse)ViewBag.LoggedInAccount).Email!;
        if (summernoteImageFile.Length <= 0 || summernoteImageFile.Length > _serverSettings.Value.MaxAttachedFileSizeBytes)
        {
            _logger.LogWarning("Editor image upload refused: size {Size} bytes is outside the {MaxBytes}-byte limit for {Email}",
                summernoteImageFile.Length, _serverSettings.Value.MaxAttachedFileSizeBytes, uploaderEmail);
            return Ok(new { result = false, errorMessage = _localizer["File Size must be smaller than {0}MB.", _serverSettings.Value.MaxAttachedFileSizeBytes / (1024 * 1024)].ToPlainString() });
        }

        var ext = Path.GetExtension(summernoteImageFile.FileName).ToLowerInvariant();
        if (!ImageUploadPolicy.IsAllowedExtension(ext))
        {
            _logger.LogWarning("Editor image upload refused: extension not allowed ({Extension}) for {Email}", ext, uploaderEmail);
            return Ok(new { result = false, errorMessage = _localizer["Only .jpg or jpeg or .png file allowed."].Value });
        }

        AttachedFileDto file = (await summernoteImageFile.ToAttachedFileInfoAsync(_serverSettings.Value.MaxAttachedFileSizeBytes, HttpContext.RequestAborted))!;

        SummernoteUploadResult uploadResult = await _boardService.UploadSummernoteImageAsync(file, "Management", BoardTypes.PrivateNote, uploaderEmail, HttpContext.RequestAborted);

        if (!uploadResult.Success)
            return Ok(new { result = false, errorMessage = _localizer[uploadResult.ErrorKey ?? "Input is invalid"].Value });

        // The Summernote editor only needs the raw bytes (base64) + content type to build an object
        // URL for the inserted <img>; return those explicitly rather than serializing a whole
        // FileContentResult and relying on its property names.
        return Ok(new
        {
            result = true,
            file = new
            {
                fileContents = Convert.ToBase64String(uploadResult.FileBytes!),
                contentType = uploadResult.ContentType!
            },
            filePath = _rsa.Encrypt(uploadResult.FilePath!)
        });
    }
    #endregion

    #endregion

    #region Read

    #region Edit, List, Detail, Write

    /// <summary>Displays the caller's private notes in the mode named by <paramref name="method"/>: "list" (default), "write", "detail" or "edit".</summary>
    public async Task<IActionResult> PrivateNote(string method = "list", int? boardId = null, int page = 1,
        string searchType = "", string searchText = "")
    {
        switch (method)
        {
            // Write
            case "write":
            {
                PrivateNoteOutputViewModel privateNoteOutputViewModel = new() { Method = method };
                bool isAccountSessionExist = ((AccountResponse)ViewBag.LoggedInAccount).Role != Role.Anonymous;
                return isAccountSessionExist ? View(privateNoteOutputViewModel) : RedirectToAction("Login", "Account");
            }
            // Detail view
            case "detail":
            {
                // ViewBag.LoggedInAccount is re-fetched from the database by ViewBagPopulatorFilter
                // on every request (not the session-cached snapshot), so a mid-session Role change
                // (e.g. an admin demotion) is reflected immediately in the CanView check below.
                AccountResponse loggedInAccount = ViewBag.LoggedInAccount;

                if (boardId == null)
                {
                    return RedirectToAction(BoardTypes.PrivateNote, "Management");
                }

                PrivateNoteOutputViewModel privateNoteOutputViewModel = new()
                {
                    Method = method,
                    DetailCurrentPage = page,
                    DetailBoardId = (int)boardId
                };

                try
                {
                    BoardResponse? privateNoteBoard = await _boardService.GetBoardByIdAsync((long)boardId, BoardTypes.PrivateNote, HttpContext.RequestAborted);
                    BoardAttachedFileDto? attachedFile = await _boardService.GetAttachedFileByBoardIdAsync((long)boardId, HttpContext.RequestAborted);

                    if (privateNoteBoard == null || privateNoteBoard.Deleted || !_boardService.CanView(privateNoteBoard, loggedInAccount))
                        return RedirectToAction(BoardTypes.PrivateNote, "Management");

                    await _boardService.IncrementBoardViewAsync((long)boardId, HttpContext.RequestAborted);

                    (privateNoteBoard.Content, bool isImgTagIncluded) = await _boardService.PrepareHtmlForDisplayAsync(privateNoteBoard.Content ?? "", HttpContext.RequestAborted);

                    privateNoteOutputViewModel.BoardOutputViewModel =
                        new BoardOutputViewModel
                        {
                            Title = privateNoteBoard.Title,
                            Writer = privateNoteBoard.Writer,
                            Views = privateNoteBoard.View,
                            Content = privateNoteBoard.Content,
                            Updated = privateNoteBoard.Updated,
                            BoardAttachedFileName = attachedFile?.Name ?? "",
                            BoardAttachedFileExtension = attachedFile?.Extension ?? "",
                            BoardAttachedFileSize = attachedFile?.Size ?? 0,
                            BoardAttachedFilePath = attachedFile?.Path ?? "",
                            IsImgTagIncluded = isImgTagIncluded,
                            CanModify = loggedInAccount.IsOwner(privateNoteBoard.Writer),
                            // Private notes are single-owner: unlike the free forum, an admin may not
                            // delete another account's private note (server enforces this in DeleteBoard).
                            CanDelete = loggedInAccount.IsOwner(privateNoteBoard.Writer)
                        };

                    // Preload comments and accounts for detail view
                    privateNoteOutputViewModel.DetailBoardComments =
                    [
                        .. await _boardService.GetCommentsByBoardIdAsync((long)boardId, HttpContext.RequestAborted)
                    ];
                    // Only the accounts shown on this page (the note's author and its commenters) are
                    // needed, to highlight admin-authored entries — not every account in the system.
                    var detailNicknames = privateNoteOutputViewModel.DetailBoardComments.Select(c => c.Writer)
                        .Append(privateNoteBoard.Writer)
                        .Where(w => !string.IsNullOrEmpty(w))
                        .Select(w => w!)
                        .Distinct();
                    privateNoteOutputViewModel.AllAccounts = await _accountService.GetAccountsByNicknamesAsync(detailNicknames, HttpContext.RequestAborted);
                    privateNoteOutputViewModel.AdminNicknames = privateNoteOutputViewModel.AllAccounts.ToAdminNicknameSet();
                    privateNoteOutputViewModel.LoggedInAccount = loggedInAccount;
                    privateNoteOutputViewModel.LoggedInAccountTimeZoneIanaId = loggedInAccount.TimeZoneIanaId ?? "";

                    return View(privateNoteOutputViewModel);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load private note board detail for board {BoardId}", boardId);
                    return RedirectToAction(BoardTypes.PrivateNote, "Management");
                }
            }
            // Edit
            case "edit" when boardId == null:
                return RedirectToAction(BoardTypes.PrivateNote, "Management");
            case "edit":
            {
                PrivateNoteOutputViewModel privateNoteOutputViewModel = new()
                {
                    Method = method,
                    EditCurrentPage = page,
                    EditBoardId = (int)boardId
                };

                // ViewBag.LoggedInAccount is re-fetched from the database by ViewBagPopulatorFilter
                // on every request (not the session-cached snapshot) — see the "detail" case above.
                AccountResponse loggedInAccount = ViewBag.LoggedInAccount;

                try
                {
                    BoardResponse? privateNoteBoard = await _boardService.GetBoardByIdAsync((long)boardId, BoardTypes.PrivateNote, HttpContext.RequestAborted);
                    BoardAttachedFileDto? attachedFile = await _boardService.GetAttachedFileByBoardIdAsync((long)boardId, HttpContext.RequestAborted);

                    if (privateNoteBoard == null || privateNoteBoard.Deleted || !loggedInAccount.IsOwner(privateNoteBoard.Writer))
                    {
                        return RedirectToAction(BoardTypes.PrivateNote, "Management");
                    }

                    (privateNoteBoard.Content, bool isImgTagIncluded) = await _boardService.PrepareHtmlForDisplayAsync(privateNoteBoard.Content ?? "", HttpContext.RequestAborted);

                    privateNoteOutputViewModel.BoardOutputViewModel =
                        new BoardOutputViewModel
                        {
                            Title = privateNoteBoard.Title,
                            Writer = privateNoteBoard.Writer,
                            Views = privateNoteBoard.View,
                            Content = privateNoteBoard.Content,
                            Updated = privateNoteBoard.Updated,
                            BoardAttachedFileName = attachedFile?.Name ?? "",
                            BoardAttachedFileExtension = attachedFile?.Extension ?? "",
                            BoardAttachedFileSize = attachedFile?.Size ?? 0,
                            BoardAttachedFilePath = attachedFile?.Path ?? "",
                            IsImgTagIncluded = isImgTagIncluded
                        };

                    return View(privateNoteOutputViewModel);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load private note board edit form for board {BoardId}", boardId);
                    return RedirectToAction(BoardTypes.PrivateNote, "Management");
                }
            }
            // List
            default:
            {
                // ViewBag.LoggedInAccount is re-fetched from the database by ViewBagPopulatorFilter
                // on every request (not the session-cached snapshot) — see the "detail" case above.
                AccountResponse loggedInAccount = ViewBag.LoggedInAccount;

                #region Board default paging logic

                const int pageSize = 5;
                if (page < 1)
                {
                    page = 1;
                }

                #endregion

                #region Logic to get all related board data

                // Ownership scope, search, ordering, and paging are all pushed down into one SQL
                // query (see BoardRepository.QueryPageAsync) instead of materializing every
                // PrivateNote row into memory first. OwnerNickname being set means the repository
                // applies no further role/lock visibility split — every candidate row already
                // belongs to the caller (same "already scoped to the caller's own notes" behavior
                // this replaced).
                (IEnumerable<BoardResponse> pageItems, int totalCount) = await _boardService.GetBoardPageAsync(new BoardPageQuery(
                    Type: BoardTypes.PrivateNote,
                    OwnerNickname: loggedInAccount.Nickname,
                    SearchType: searchType,
                    SearchText: searchText,
                    IsLoggedIn: true,
                    ViewerRole: loggedInAccount.Role,
                    ViewerNickname: loggedInAccount.Nickname,
                    Page: page,
                    PageSize: pageSize), HttpContext.RequestAborted);

                #endregion

                PrivateNoteOutputViewModel privateNoteOutputViewModel = new();

                #region Board list

                privateNoteOutputViewModel.Method = "list";

                #endregion

                #region Board paging logic

                privateNoteOutputViewModel.Pager = new Pager(totalCount, page, pageSize);

                #endregion

                #region Board data logic

                privateNoteOutputViewModel.Boards = [.. pageItems];

                #endregion

                #region Preload comment counts and attached files for view
                privateNoteOutputViewModel.LoggedInAccount = loggedInAccount;
                privateNoteOutputViewModel.LoggedInAccountTimeZoneIanaId = loggedInAccount.TimeZoneIanaId ?? "";
                var boardResponses = privateNoteOutputViewModel.Boards.ToList();
                // Only the writers on this page are needed (to highlight admin-authored notes).
                var listNicknames = boardResponses.Select(b => b.Writer)
                    .Where(w => !string.IsNullOrEmpty(w))
                    .Select(w => w!)
                    .Distinct();
                privateNoteOutputViewModel.AllAccounts = await _accountService.GetAccountsByNicknamesAsync(listNicknames, HttpContext.RequestAborted);
                privateNoteOutputViewModel.AdminNicknames = privateNoteOutputViewModel.AllAccounts.ToAdminNicknameSet();
                List<long> boardIds = [.. boardResponses.Select(b => b.Id)];
                Dictionary<long, int> commentCounts = await _boardService.GetCommentCountsForBoardsAsync(boardIds, HttpContext.RequestAborted);
                Dictionary<long, BoardAttachedFileDto?> attachedFiles = await _boardService.GetAttachedFilesForBoardsAsync(boardIds, HttpContext.RequestAborted);

                foreach (var board in boardResponses)
                {
                    privateNoteOutputViewModel.CommentCountByBoardId[board.Id] = commentCounts.GetValueOrDefault(board.Id, 0);
                    BoardAttachedFileDto? af = attachedFiles.GetValueOrDefault(board.Id);
                    privateNoteOutputViewModel.AttachedFilesByBoardId[board.Id] = af != null && !string.IsNullOrEmpty(af.Name) ? [af] : [];
                }
                #endregion

                privateNoteOutputViewModel.SelectedSearchType = searchType;
                privateNoteOutputViewModel.TypedSearchText = searchText;
                return View(privateNoteOutputViewModel);
            }
        }
    }

    #endregion

    #region DownloadAttachedFile

    /// <summary>
    /// Streams the attachment of private note <paramref name="boardId"/>. The note's visibility (its
    /// owner only) is checked again for the caller, and the file is streamed from file storage rather
    /// than embedded in the page.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> DownloadPrivateNoteAttachedFile(long boardId)
    {
        try
        {
            // Re-fetched from the database by ViewBagPopulatorFilter on every request.
            AccountResponse loggedInAccount = ViewBag.LoggedInAccount;
            (ServiceResult result, AttachmentDownload? file) = await _boardService.OpenAttachedFileAsync(
                boardId, BoardTypes.PrivateNote, loggedInAccount, HttpContext.RequestAborted);
            if (!result.Success)
                return Json(new { result = false, error = _localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });

            return File(file!.Content, "application/octet-stream", file.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download the attachment of private note {BoardId}", boardId);
            return Json(new { result = false, error = _localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    #endregion

    #region IsBoardExists

    /// <summary>
    /// Checks that a private note with the given ID exists and belongs to the caller, returning its id
    /// when so — never its content, which holds decrypted plain storage paths.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> IsBoardExists(int id)
    {
        try
        {
            string? nickname = ((AccountResponse)ViewBag.LoggedInAccount).Nickname;
            BoardResponse? board = await _boardService.GetBoardByIdAsync(id, BoardTypes.PrivateNote, HttpContext.RequestAborted);

            return board == null || board.Deleted || board.Writer != nickname
                ? Json(new { result = false, error = _localizer["Input is invalid"].Value })
                : Json(new { result = true, PrivateNoteBoard = new { id = board.Id } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check private note board existence for id {BoardId}", id);
            return Json(new { result = false, error = _localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    #endregion

    #endregion

    #region Update

    #region Edit

    /// <summary>Saves edits to one of the caller's own private notes.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> EditPrivateNoteBoard(BoardInputViewModel boardInputViewModel)
    {
        try
        {
            if (!ModelState.IsValid)
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });

            boardInputViewModel.Content ??= "";

            // DB-fresh account already loaded by ViewBagPopulatorFilter for this request (see WritePrivateNoteBoard).
            AccountResponse loggedInAccount = ViewBag.LoggedInAccount;
            if (loggedInAccount.Role is not (Role.Admin or Role.User))
                return Json(new { result = false, error = _localizer["Please Login to edit board"].Value });

            BoardRequest boardRequest = new()
            {
                Id = boardInputViewModel.Id,
                Type = BoardTypes.PrivateNote,
                Title = boardInputViewModel.Title,
                Content = boardInputViewModel.Content
            };

            AttachedFileDto? attachedFile = await boardInputViewModel.UploadedFile.ToAttachedFileInfoAsync(_serverSettings.Value.MaxAttachedFileSizeBytes, HttpContext.RequestAborted);

            ServiceResult result = await _boardService.EditBoardAsync(boardRequest, loggedInAccount.Nickname!, loggedInAccount.Role == Role.Admin, attachedFile, HttpContext.RequestAborted);
            return result.Success
                ? Json(new { result = true, message = _localizer["The board has been successfully updated."].Value })
                : Json(new { result = false, error = _localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to edit private note board {BoardId}", boardInputViewModel.Id);
            return Json(new { result = false, error = _localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    #endregion

    #endregion

    #region Delete

    #region Board

    /// <summary>Soft-deletes one of the caller's own private notes (no admin bypass).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> DeleteBoard([FromBody] BoardInputViewModel boardInputViewModel)
    {
        try
        {
            // ViewBag.LoggedInAccount is re-fetched from the database by ViewBagPopulatorFilter
            // on every request (not the session-cached snapshot) — see the "detail" case above.
            AccountResponse loggedInAccount = ViewBag.LoggedInAccount;

            // Private notes are single-owner: unlike the free forum, an admin may not delete
            // another account's private note board.
            ServiceResult result = await _boardService.DeleteBoardAsync(
                boardInputViewModel.Id,
                BoardTypes.PrivateNote,
                loggedInAccount.Nickname!,
                false,
                HttpContext.RequestAborted);

            return result.Success
                ? Json(new { result = true, message = _localizer["The board has been successfully deleted."].Value })
                : Json(new { result = false, error = _localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete private note board {BoardId}", boardInputViewModel.Id);
            return Json(new { result = false, error = _localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    #endregion

    #region BoardComment

    /// <summary>Soft-deletes a comment on a private note; only the comment's author may (no admin bypass for private notes).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.Admin)]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> DeleteComment(int id)
    {
        try
        {
            // ViewBag.LoggedInAccount is re-fetched from the database by ViewBagPopulatorFilter
            // on every request (not the session-cached snapshot) — see the "detail" case above.
            AccountResponse loggedInAccount = ViewBag.LoggedInAccount;

            ServiceResult result = await _boardService.DeleteCommentAsync(
                id,
                loggedInAccount.Nickname!,
                loggedInAccount.Role == Role.Admin,
                BoardTypes.PrivateNote,
                HttpContext.RequestAborted);

            return result.Success
                ? Json(new { result = true })
                : Json(new { result = false, error = _localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete private note comment {CommentId}", id);
            return Json(new { result = false, error = _localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }

    #endregion

    #endregion

    #endregion
}
