using Ganss.Xss;
using HtmlAgilityPack;
using Maroik.Common.DataAccess.Contracts;
using Maroik.Common.DataAccess.Models;
using Maroik.Common.Miscellaneous.Extensions;
using Maroik.Common.Miscellaneous.Utilities;
using Maroik.WebSite.Contracts;
using Maroik.WebSite.Models;
using Maroik.WebSite.Models.ViewModels.Management;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.StaticFiles;
using Newtonsoft.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using ImageMagick;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace Maroik.WebSite.Controllers
{
    public class ManagementController : Controller
    {
        private readonly RSA _rsa;
        private readonly IHtmlLocalizer<ManagementController> _localizer;
        private readonly ILogger<ManagementController> _logger;
        private readonly IAccountRepository _accountRepository;
        private readonly IHostEnvironment _hostEnvironment;
        private readonly ICategoryRepository _categoryRepository;
        private readonly ISubCategoryRepository _subCategoryRepository;
        private readonly IBoardRepository _boardRepository;
        private readonly IBoardAttachedFileRepository _boardAttachedFileRepository;
        private readonly IBoardCommentRepository _boardCommentRepository;
        private readonly IFileRepository _fileRepository;
        private readonly IClamavRepository _clamavRepository;

        public ManagementController(IHtmlLocalizer<ManagementController> localizer,
            ILogger<ManagementController> logger, IBoardRepository boardRepository,
            IBoardAttachedFileRepository boardAttachedFileRepository, IBoardCommentRepository boardCommentRepository,
            IAccountRepository accountRepository, IHostEnvironment hostEnvironment,
            ICategoryRepository categoryRepository, ISubCategoryRepository subCategoryRepository,
            IFileRepository fileRepository, IClamavRepository clamavRepository)
        {
            _rsa = new RSA(RSAType.RSA2, Encoding.UTF8, RSA.privateKey, RSA.publicKey);
            _localizer = localizer;
            _logger = logger;
            _boardRepository = boardRepository;
            _boardAttachedFileRepository = boardAttachedFileRepository;
            _accountRepository = accountRepository;
            _hostEnvironment = hostEnvironment;
            _categoryRepository = categoryRepository;
            _subCategoryRepository = subCategoryRepository;
            _boardCommentRepository = boardCommentRepository;
            _fileRepository = fileRepository;
            _clamavRepository = clamavRepository;
        }

        #region Profile

        #region Read

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            _ = HttpContext.Session.TryGetValue(
                Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous.Utilities
                    .Session.Account), out byte[] resultByte);
            string loginedAccountTimeZoneIanaId = JsonConvert
                .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).TimeZoneIanaId;
            Account tempAccount = await _accountRepository.GetAccountByEmailAsync(JsonConvert
                .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);

            return View(new ProfileOutputViewModel()
            {
                Email = tempAccount.Email, AvatarImagePath = tempAccount.AvatarImagePath,
                Nickname = tempAccount.Nickname,
                Created = tempAccount.Created.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId),
                TimeZoneIanaId = tempAccount.TimeZoneIanaId
            });
        }

        #endregion

        #region Update

        #region ProfileAvatar

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.Admin)]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> UpdateProfileAvatar(ProfileInputViewModel profileInputViewModel)
        {
            if (profileInputViewModel.ProfileAvatarFiles == null || profileInputViewModel.ProfileAvatarFiles.Count == 0)
            {
                return Ok(new { result = false, errorMessage = _localizer["Please attach a file"].Value });
            }

            IFormFile file = profileInputViewModel.ProfileAvatarFiles[0];

            if (file.Length <= 0 || file.Length > 10 * 1024 * 1024)
            {
                return Ok(new { result = false, errorMessage = _localizer["File Size must be smaller than 10MB."].Value });
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                return Ok(new { result = false, errorMessage = _localizer["Only .jpg or jpeg or .png file allowed"].Value });
            }

            // ClamAV 검사 전 메모리 스트림 복사
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            if (!await _clamavRepository.ScanWithClamavAsync(memoryStream))
            {
                return BadRequest("File may be infected with a virus.");
            }

            // 이미지 유효성 확인 (Magick.NET 사용, SVG 차단 포함)
            memoryStream.Position = 0;
            try
            {
                using var image = new MagickImage(memoryStream);
                if (image.Format == MagickFormat.Svg)
                {
                    return Ok(new { result = false, errorMessage = _localizer["SVG format is not allowed"].Value });
                }
            }
            catch
            {
                return Ok(new { result = false, errorMessage = _localizer["Invalid image file."].Value });
            }

            _ = HttpContext.Session.TryGetValue(
                EnumHelper.GetDescription(Session.Account),
                out byte[] resultByte);

            string avatarPath = Path.Combine(_hostEnvironment.ContentRootPath, "wwwroot", "upload", "Management", "Profile", "Avatar");
            if (!Directory.Exists(avatarPath))
            {
                Directory.CreateDirectory(avatarPath);
            }

            string guid = Guid.NewGuid().ToString().ToUpper();
            string avatarFile = $"{guid}{extension}";
            string filePath = Path.Combine(avatarPath, avatarFile);

        #pragma warning disable SCS0018
            await using (var stream = new FileStream(filePath, FileMode.Create))
        #pragma warning restore SCS0018
            {
                memoryStream.Position = 0;
                await memoryStream.CopyToAsync(stream);
                await stream.FlushAsync();
            }

            // 실행 권한 제거 (chmod 644)
            var chmod = new ProcessStartInfo
            {
                FileName = "/bin/chmod",
#pragma warning disable SCS0001
                Arguments = $"644 \"{filePath}\"",
#pragma warning restore SCS0001
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (var process = Process.Start(chmod))
            {
                await process?.WaitForExitAsync()!;
            }

            try
            {
                Account tempAccount = await _accountRepository.GetAccountByEmailAsync(
                    JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);

                tempAccount.AvatarImagePath = $"/upload/Management/Profile/Avatar/{avatarFile}";
                await _accountRepository.UpdateAccountAsync(tempAccount);

                return Ok(new { result = true });
            }
            catch
            {
                return Ok(new { result = false, errorMessage = _localizer["Input is invalid"].Value });
            }
        }

        #endregion

        #region ProfileTimeZone

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.Admin)]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> UpdateProfileTimeZone(ProfileInputViewModel profileInputViewModel)
        {
            _ = ModelState.Remove(nameof(profileInputViewModel.Nickname));
            _ = ModelState.Remove(nameof(profileInputViewModel.Password));
            _ = ModelState.Remove(nameof(profileInputViewModel.NewPassword));

            if (ModelState.IsValid)
            {
                try
                {
                    _ = HttpContext.Session.TryGetValue(
                        Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                            .Utilities.Session.Account), out byte[] resultByte);
                    Account tempAccount = await _accountRepository.GetAccountByEmailAsync(JsonConvert
                        .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);
                    tempAccount.TimeZoneIanaId = profileInputViewModel.TimeZoneIanaId;
                    await _accountRepository.UpdateAccountAsync(tempAccount);

                    HttpContext.Session.Remove(
                        Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                            .Utilities.Session.Account));
                    tempAccount.HashedPassword = null;
                    HttpContext.Session.Set(
                        Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                            .Utilities.Session.Account),
                        Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(tempAccount)));
                }
                catch
                {
                    Console.WriteLine(_localizer["Input is invalid"].Value);
                }
            }

            return RedirectToAction("Profile", "Management");
        }

        #endregion

        #region ProfilePassword

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.Admin)]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> UpdateProfilePassword([FromBody] ProfileInputViewModel profileInputViewModel)
        {
            _ = ModelState.Remove(nameof(profileInputViewModel.Nickname));
            _ = ModelState.Remove(nameof(profileInputViewModel.TimeZoneIanaId));
            
            if (ModelState.IsValid)
            {
                try
                {
                    _ = HttpContext.Session.TryGetValue(
                        Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                            .Utilities.Session.Account), out byte[] resultByte);
                    Account tempAccount = await _accountRepository.GetAccountByEmailAsync(JsonConvert
                        .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);

                    if (_rsa.Verify(profileInputViewModel.Password ?? "", tempAccount.HashedPassword)) // right password
                    {
                        tempAccount.HashedPassword = _rsa.Sign(profileInputViewModel.NewPassword);
                        await _accountRepository.UpdateAccountAsync(tempAccount);
                        return Json(new
                            { result = true, message = _localizer["Password change successfully done."].Value });
                    }
                    else // wrong password
                    {
                        return Json(new
                            { result = false, error = _localizer["Invalid password. Please check again."].Value });
                    }
                }
                catch
                {
                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                }
            }
            else
            {
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
            }
        }

        #endregion

        #endregion

        #endregion

        #region Account

        #region Create

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

                if (ModelState.IsValid)
                {
                    #region Email 형식 체크

                    static bool IsValidEmail(string email)
                    {
                        if (!MailAddress.TryCreate(email, out MailAddress mailAddress))
                        {
                            return false;
                        }

                        // And if you want to be more strict:
                        string[] hostParts = mailAddress.Host.Split('.');
                        if (hostParts.Length == 1)
                        {
                            return false; // No dot.
                        }

                        if (hostParts.Any(p => p == string.Empty))
                        {
                            return false; // Double dot.
                        }

                        if (hostParts[^1].Length < 2)
                        {
                            return false; // TLD only one letter.
                        }

                        if (mailAddress.User.Contains(' '))
                        {
                            return false;
                        }

                        if (mailAddress.User.Split('.').Any(p => p == string.Empty))
                        {
                            return false; // Double dot or dot at end of user part.
                        }

                        return true;
                    }

                    if (!IsValidEmail(accountInputViewModel.Email)) // 유효하지 않은 메일 형식
                    {
                        return Json(new { result = false, error = _localizer["Invalid email form"].Value });
                    }

                    #endregion

                    #region Password 형식 체크

                    if (!new Regex(
                                "^((?=.*?[A-Z])(?=.*?[a-z])(?=.*?[0-9])|(?=.*?[A-Z])(?=.*?[a-z])(?=.*?[^a-zA-Z0-9])|(?=.*?[A-Z])(?=.*?[0-9])(?=.*?[^a-zA-Z0-9])|(?=.*?[a-z])(?=.*?[0-9])(?=.*?[^a-zA-Z0-9])).{8,}$")
                            .IsMatch(accountInputViewModel.Password)) // 유효하지 않은 비밀번호 형식
                    {
                        return Json(new
                        {
                            result = false,
                            error = _localizer[
                                    "Password must be at least 8 characters and contain at 3 of 4 of the following: upper case (A-Z), lower case (a-z), number (0-9) and special character (e.g. !@#$%^&*)"]
                                .Value
                        });
                    }

                    #endregion

                    #region Nickname 형식 체크

                    if (string.IsNullOrEmpty(accountInputViewModel.Nickname))
                    {
                        return Json(new { result = false, error = _localizer["Please enter Nickname"].Value });
                    }
                    else if (accountInputViewModel.Nickname.Length is not (>= 1 and <= 255))
                    {
                        return Json(new
                        {
                            result = false, error = _localizer["Nickname is must be between 1 and 255 characters"].Value
                        });
                    }

                    #endregion

                    #region Role 체크

                    if (accountInputViewModel.Role is not (Role.Admin or Role.User))
                    {
                        accountInputViewModel.Role = Role.User;
                    }

                    #endregion

                    #region TimeZone 형식 체크

                    if (string.IsNullOrEmpty(accountInputViewModel.TimeZoneIanaId))
                    {
                        return Json(new { result = false, error = _localizer["Please select time zone"].Value });
                    }

                    #endregion

                    #region 이미 입력된 이메일로 계정이 있는 지 확인

                    if (await _accountRepository.GetAccountByEmailAsync(accountInputViewModel.Email) != null)
                    {
                        return Json(new
                            { result = false, error = _localizer["This account has already been created."].Value });
                    }

                    #endregion

                    #region 계정 생성

                    await _accountRepository.CreateAccountAsync
                    (
                        new Account()
                        {
                            Email = accountInputViewModel.Email,
                            HashedPassword = _rsa.Sign(accountInputViewModel.Password), // 암호화
                            Nickname = accountInputViewModel.Nickname,
                            AvatarImagePath = "/upload/Management/Profile/default-avatar.jpg",
                            Role = accountInputViewModel.Role,
                            TimeZoneIanaId = accountInputViewModel.TimeZoneIanaId,
                            Locked = false,
                            LoginAttempt = 0,
                            EmailConfirmed = true,
                            AgreedServiceTerms = true,
                            RegistrationToken = null,
                            ResetPasswordToken = null,
                            Created = DateTime.UtcNow,
                            Updated = DateTime.UtcNow,
                            Message = EnumHelper.GetDescription(AccountMessage.Success),
                            Deleted = false
                        }
                    );
                    return Json(new { result = true, message = _localizer["Account create successfully done."].Value });

                    #endregion
                }
                else
                {
                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                }
            }
            catch
            {
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
            }
        }

        #endregion

        #region Read

        [HttpGet]
        public async Task<IActionResult> Account(string wholeSearch)
        {
            if (HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest") // ajax
            {
                List<AccountOutputViewModel> accountOutputViewModels = [];
                _ = HttpContext.Session.TryGetValue(
                    Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                        .Utilities.Session.Account), out byte[] resultByte);

                string loginedAccountTimeZoneIanaId = JsonConvert
                    .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).TimeZoneIanaId;

                #region Accounts

                foreach (Account item in await _accountRepository.GetAllAsync())
                {
                    if (string.IsNullOrEmpty(wholeSearch))
                    {
                        accountOutputViewModels.Add(new AccountOutputViewModel()
                        {
                            Email = item.Email,
                            HashedPassword = item.HashedPassword,
                            Nickname = item.Nickname,
                            AvatarImagePath = item.AvatarImagePath,
                            Role = item.Role,
                            TimeZoneIanaId = item.TimeZoneIanaId,
                            Locked = item.Locked,
                            LoginAttempt = item.LoginAttempt,
                            EmailConfirmed = item.EmailConfirmed,
                            AgreedServiceTerms = item.AgreedServiceTerms,
                            RegistrationToken = item.RegistrationToken,
                            ResetPasswordToken = item.ResetPasswordToken,
                            Created = item.Created.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId),
                            Updated = item.Updated.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId),
                            Message = item.Message,
                            Deleted = item.Deleted
                        });
                    }
                    else
                    {
                        if (item.Email.ToString().Contains(wholeSearch) ||
                            (item.HashedPassword?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.Nickname?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.AvatarImagePath?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.Role?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.TimeZoneIanaId?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.Locked.ToString() ?? "").Contains(wholeSearch) ||
                            (item.LoginAttempt.ToString() ?? "").Contains(wholeSearch) ||
                            (item.EmailConfirmed.ToString() ?? "").Contains(wholeSearch) ||
                            (item.AgreedServiceTerms.ToString() ?? "").Contains(wholeSearch) ||
                            (item.RegistrationToken?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.ResetPasswordToken?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.Created.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId).ToString() ?? "")
                            .Contains(wholeSearch) ||
                            (item.Updated.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId).ToString() ?? "")
                            .Contains(wholeSearch) ||
                            (item.Message?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.Deleted.ToString() ?? "").Contains(wholeSearch))
                        {
                            accountOutputViewModels.Add(new AccountOutputViewModel()
                            {
                                Email = item.Email,
                                HashedPassword = item.HashedPassword,
                                Nickname = item.Nickname,
                                AvatarImagePath = item.AvatarImagePath,
                                Role = item.Role,
                                TimeZoneIanaId = item.TimeZoneIanaId,
                                Locked = item.Locked,
                                LoginAttempt = item.LoginAttempt,
                                EmailConfirmed = item.EmailConfirmed,
                                AgreedServiceTerms = item.AgreedServiceTerms,
                                RegistrationToken = item.RegistrationToken,
                                ResetPasswordToken = item.ResetPasswordToken,
                                Created = item.Created.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId),
                                Updated = item.Updated.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId),
                                Message = item.Message,
                                Deleted = item.Deleted
                            });
                        }
                    }
                }

                #endregion

                IQueryable<AccountOutputViewModel> result = accountOutputViewModels.OrderBy(m => m.Email)
                    .ThenBy(m => m.Nickname).ThenByDescending(m => m.Created).ThenByDescending(m => m.Updated)
                    .AsQueryable();
                return PartialView("_AccountGrid", result);
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.Admin)]
        public async Task<IActionResult> IsAccountExists(string email)
        {
            try
            {
                Account tempAccount = await _accountRepository.GetAccountByEmailAsync(email);

                return tempAccount == null
                    ? Json(new
                    {
                        result = false, error = _localizer["Fail to find the account by given email address"].Value
                    })
                    : (IActionResult)Json(new { result = true, account = tempAccount });
            }
            catch
            {
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
            }
        }

        #endregion

        #region Update

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

                if (ModelState.IsValid)
                {
                    Account tempAccount = await _accountRepository.GetAccountByEmailAsync(accountInputViewModel.Email);

                    if (tempAccount == null)
                    {
                        return Json(new { result = false, error = _localizer["Email address is wrong"].Value });
                    }

                    tempAccount.Email = accountInputViewModel.Email;
                    tempAccount.HashedPassword = _rsa.Sign(accountInputViewModel.Password); // 암호화
                    tempAccount.Role = accountInputViewModel.Role;
                    tempAccount.TimeZoneIanaId = accountInputViewModel.TimeZoneIanaId;
                    tempAccount.Locked = accountInputViewModel.Locked;
                    if (accountInputViewModel.Locked == false)
                    {
                        tempAccount.LoginAttempt = 0;
                    }

                    tempAccount.EmailConfirmed = accountInputViewModel.EmailConfirmed;
                    tempAccount.AgreedServiceTerms = accountInputViewModel.AgreedServiceTerms;
                    tempAccount.Updated = DateTime.UtcNow;
                    tempAccount.Message = accountInputViewModel.Message;
                    tempAccount.Deleted = accountInputViewModel.Deleted;

                    await _accountRepository.UpdateAccountAsync(tempAccount);

                    return Json(new { result = true, message = _localizer["Successfully updated the account"].Value });
                }
                else
                {
                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                }
            }
            catch
            {
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
            }
        }

        #endregion

        #region Delete

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.Admin)]
        public async Task<IActionResult> DeleteAccount([FromBody] AccountInputViewModel accountInputViewModel)
        {
            try
            {
                Account tempAccount = await _accountRepository.GetAccountByEmailAsync(accountInputViewModel.Email);

                if (tempAccount == null)
                {
                    return Json(new
                    {
                        result = false, error = _localizer["Fail to find the account by given email address"].Value
                    });
                }
                else
                {
                    tempAccount.Deleted = true;
                    await _accountRepository.UpdateAccountAsync(tempAccount);
                    return Json(new { result = true, message = _localizer["Successfully deleted the account"].Value });
                }
            }
            catch
            {
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
            }
        }

        #endregion

        #region Excel

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.Admin)]
        public async Task<IActionResult> ExportExcelAccount(string fileName = "")
        {
            await Task.Yield();

            List<AccountOutputViewModel> accountOutputViewModels = new();
            _ = HttpContext.Session.TryGetValue(
                Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous.Utilities
                    .Session.Account), out byte[] resultByte);
            string loginedAccountTimeZoneIanaId = JsonConvert
                .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).TimeZoneIanaId;

            #region Accounts

            foreach (Account item in await _accountRepository.GetAllAsync())
            {
                accountOutputViewModels.Add(new AccountOutputViewModel()
                {
                    Email = item.Email,
                    HashedPassword = item.HashedPassword,
                    Nickname = item.Nickname,
                    AvatarImagePath = item.AvatarImagePath,
                    Role = item.Role,
                    TimeZoneIanaId = item.TimeZoneIanaId,
                    Locked = item.Locked,
                    LoginAttempt = item.LoginAttempt,
                    EmailConfirmed = item.EmailConfirmed,
                    AgreedServiceTerms = item.AgreedServiceTerms,
                    RegistrationToken = item.RegistrationToken,
                    ResetPasswordToken = item.ResetPasswordToken,
                    Created = item.Created.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId),
                    Updated = item.Updated.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId),
                    Message = item.Message,
                    Deleted = item.Deleted
                });
            }

            accountOutputViewModels = accountOutputViewModels.OrderBy(m => m.Email).ThenBy(m => m.Nickname)
                .ThenByDescending(m => m.Created).ThenByDescending(m => m.Updated).ToList();

            #endregion

            MemoryStream stream = new();

            using (SpreadsheetDocument document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
            {
                WorkbookPart workbookPart = document.AddWorkbookPart();
                workbookPart.Workbook = new Workbook();

                WorksheetPart worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                worksheetPart.Worksheet = new Worksheet(new SheetData());

                Sheets sheets = document.WorkbookPart.Workbook.AppendChild(new Sheets());
                Sheet sheet = new Sheet()
                {
                    Id = document.WorkbookPart.GetIdOfPart(worksheetPart),
                    SheetId = 1,
                    Name = "Sheet1"
                };
                sheets.Append(sheet);

                SheetData sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>();

                Cell CreateCell(string value)
                {
                    return new Cell()
                    {
                        CellValue = new CellValue(value),
                        DataType = CellValues.String
                    };
                }

                Row headerRow = new Row();
                headerRow.Append(
                    CreateCell(_localizer[nameof(AccountOutputViewModel.Email)].Value),
                    CreateCell(_localizer[nameof(AccountOutputViewModel.HashedPassword)].Value),
                    CreateCell(_localizer[nameof(AccountOutputViewModel.Nickname)].Value),
                    CreateCell(_localizer[nameof(AccountOutputViewModel.AvatarImagePath)].Value),
                    CreateCell(_localizer[nameof(AccountOutputViewModel.Role)].Value),
                    CreateCell(_localizer[nameof(AccountOutputViewModel.TimeZoneIanaId)].Value),
                    CreateCell(_localizer[nameof(AccountOutputViewModel.Locked)].Value),
                    CreateCell(_localizer[nameof(AccountOutputViewModel.LoginAttempt)].Value),
                    CreateCell(_localizer[nameof(AccountOutputViewModel.EmailConfirmed)].Value),
                    CreateCell(_localizer[nameof(AccountOutputViewModel.AgreedServiceTerms)].Value),
                    CreateCell(_localizer[nameof(AccountOutputViewModel.RegistrationToken)].Value),
                    CreateCell(_localizer[nameof(AccountOutputViewModel.ResetPasswordToken)].Value),
                    CreateCell(_localizer[nameof(AccountOutputViewModel.Created)].Value),
                    CreateCell(_localizer[nameof(AccountOutputViewModel.Updated)].Value),
                    CreateCell(_localizer[nameof(AccountOutputViewModel.Message)].Value),
                    CreateCell(_localizer[nameof(AccountOutputViewModel.Deleted)].Value)
                );
                sheetData.Append(headerRow);

                int recordIndex = 2;
                foreach (AccountOutputViewModel item in accountOutputViewModels)
                {
                    Row recordRow = new Row()
                    {
                        RowIndex = (uint)recordIndex
                    };

                    recordRow.Append(
                        CreateCell(item.Email),
                        CreateCell(item.HashedPassword),
                        CreateCell(item.Nickname),
                        CreateCell(item.AvatarImagePath),
                        CreateCell(item.Role),
                        CreateCell(item.TimeZoneIanaId),
                        CreateCell(item.Locked.ToString()),
                        CreateCell(item.LoginAttempt.ToString()),
                        CreateCell(item.EmailConfirmed.ToString()),
                        CreateCell(item.AgreedServiceTerms.ToString()),
                        CreateCell(item.RegistrationToken),
                        CreateCell(item.ResetPasswordToken),
                        CreateCell(item.Created.ToString()),
                        CreateCell(item.Updated.ToString()),
                        CreateCell(item.Message),
                        CreateCell(item.Deleted.ToString())
                    );
                    sheetData.Append(recordRow);

                    recordIndex++;
                }

                workbookPart.Workbook.Save();
            }

            stream.Position = 0;
            string excelName =
                $"{fileName}-{DateTime.UtcNow.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId):yyyy-MM-dd-HH-mm-ss-fff}.xlsx";

            return File(stream, "application/octet-stream", excelName);
        }

        #endregion

        #endregion

        #region Menu

        #region Create

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.Admin)]
        public async Task<IActionResult> CreateCategory([FromBody] MenuInputViewModel menuInputViewModel)
        {
            try
            {
                _ = ModelState.Remove(nameof(menuInputViewModel
                    .Action)); // remove ModelState check in Action (in Request parameters, Action can be null)

                if (ModelState.IsValid)
                {
                    Category category = new()
                    {
                        Name = menuInputViewModel.Name,
                        DisplayName = menuInputViewModel.DisplayName,
                        IconPath = menuInputViewModel.IconPath,
                        Controller = menuInputViewModel.Controller,
                        Action = menuInputViewModel.Action,
                        Role = menuInputViewModel.Role,
                        Order = menuInputViewModel.Order
                    };
                    await _categoryRepository.CreateCategoryAsync(category);

                    return Json(new { result = true, message = _localizer["Successfully created the category"].Value });
                }
                else
                {
                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                }
            }
            catch
            {
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.Admin)]
        public async Task<IActionResult> CreateSubCategory([FromBody] MenuInputViewModel menuInputViewModel)
        {
            try
            {
                _ = ModelState.Remove(nameof(menuInputViewModel
                    .Controller)); // remove ModelState check in Controller (in Request parameters, Controller can be null)

                if (ModelState.IsValid)
                {
                    SubCategory subCategory = new()
                    {
                        CategoryId = menuInputViewModel.CategoryId,
                        Name = menuInputViewModel.Name,
                        DisplayName = menuInputViewModel.DisplayName,
                        IconPath = menuInputViewModel.IconPath,
                        Action = menuInputViewModel.Action,
                        Role = menuInputViewModel.Role,
                        Order = menuInputViewModel.Order
                    };
                    await _subCategoryRepository.CreateSubCategoryAsync(subCategory);

                    return Json(new
                        { result = true, message = _localizer["Successfully created the subCategory"].Value });
                }
                else
                {
                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                }
            }
            catch
            {
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
            }
        }

        #endregion

        #region Read

        [HttpGet]
        public async Task<IActionResult> Menu(string wholeSearch)
        {
            if (HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest") // ajax
            {
                List<MenuOutputViewModel> menuOutputViewModels = [];

                #region AdminCategories

                foreach (Category item in await _categoryRepository.GetCategoryByRoleAsync(Role.Admin))
                {
                    if (string.IsNullOrEmpty(wholeSearch))
                    {
                        menuOutputViewModels.Add(new MenuOutputViewModel()
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
                        });
                    }
                    else
                    {
                        if (item.Id.ToString().Contains(wholeSearch) ||
                            (item.Name?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.DisplayName?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.IconPath?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.Controller?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.Action?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.Role?.ToString() ?? "").Contains(wholeSearch) ||
                            item.Order.ToString().Contains(wholeSearch))
                        {
                            menuOutputViewModels.Add(new MenuOutputViewModel()
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
                            });
                        }
                    }
                }

                #endregion

                #region UserCategories

                foreach (Category item in await _categoryRepository.GetCategoryByRoleAsync(Role.User))
                {
                    if (string.IsNullOrEmpty(wholeSearch))
                    {
                        menuOutputViewModels.Add(new MenuOutputViewModel()
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
                        });
                    }
                    else
                    {
                        if (item.Id.ToString().Contains(wholeSearch) ||
                            (item.Name?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.DisplayName?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.IconPath?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.Controller?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.Action?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.Role?.ToString() ?? "").Contains(wholeSearch) ||
                            item.Order.ToString().Contains(wholeSearch))
                        {
                            menuOutputViewModels.Add(new MenuOutputViewModel()
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
                            });
                        }
                    }
                }

                #endregion

                #region AnonymousCategories

                foreach (Category item in await _categoryRepository.GetCategoryByRoleAsync(Role.Anonymous))
                {
                    if (string.IsNullOrEmpty(wholeSearch))
                    {
                        menuOutputViewModels.Add(new MenuOutputViewModel()
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
                        });
                    }
                    else
                    {
                        if (item.Id.ToString().Contains(wholeSearch) ||
                            (item.Name?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.DisplayName?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.IconPath?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.Controller?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.Action?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.Role?.ToString() ?? "").Contains(wholeSearch) ||
                            item.Order.ToString().Contains(wholeSearch))
                        {
                            menuOutputViewModels.Add(new MenuOutputViewModel()
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
                            });
                        }
                    }
                }

                #endregion

                #region AdminSubCategories

                foreach (SubCategory item in await _subCategoryRepository.GetSubCategoryByRoleAsync(Role.Admin))
                {
                    if (string.IsNullOrEmpty(wholeSearch))
                    {
                        menuOutputViewModels.Add(new MenuOutputViewModel()
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
                        });
                    }
                    else
                    {
                        if (item.Id.ToString().Contains(wholeSearch) ||
                            item.CategoryId.ToString().Contains(wholeSearch) ||
                            (item.Name?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.DisplayName?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.IconPath?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.Action?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.Role?.ToString() ?? "").Contains(wholeSearch) ||
                            item.Order.ToString().Contains(wholeSearch))
                        {
                            menuOutputViewModels.Add(new MenuOutputViewModel()
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
                            });
                        }
                    }
                }

                #endregion

                #region UserSubCategories

                foreach (SubCategory item in await _subCategoryRepository.GetSubCategoryByRoleAsync(Role.User))
                {
                    if (string.IsNullOrEmpty(wholeSearch))
                    {
                        menuOutputViewModels.Add(new MenuOutputViewModel()
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
                        });
                    }
                    else
                    {
                        if (item.Id.ToString().Contains(wholeSearch) ||
                            item.CategoryId.ToString().Contains(wholeSearch) ||
                            (item.Name?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.DisplayName?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.IconPath?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.Action?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.Role?.ToString() ?? "").Contains(wholeSearch) ||
                            item.Order.ToString().Contains(wholeSearch))
                        {
                            menuOutputViewModels.Add(new MenuOutputViewModel()
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
                            });
                        }
                    }
                }

                #endregion

                #region AnonymousSubCategories

                foreach (SubCategory item in await _subCategoryRepository.GetSubCategoryByRoleAsync(Role.Anonymous))
                {
                    if (string.IsNullOrEmpty(wholeSearch))
                    {
                        menuOutputViewModels.Add(new MenuOutputViewModel()
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
                        });
                    }
                    else
                    {
                        if (item.Id.ToString().Contains(wholeSearch) ||
                            item.CategoryId.ToString().Contains(wholeSearch) ||
                            (item.Name?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.DisplayName?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.IconPath?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.Action?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.Role?.ToString() ?? "").Contains(wholeSearch) ||
                            item.Order.ToString().Contains(wholeSearch))
                        {
                            menuOutputViewModels.Add(new MenuOutputViewModel()
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
                            });
                        }
                    }
                }

                #endregion

                IQueryable<MenuOutputViewModel> result = menuOutputViewModels.OrderByDescending(m => m.Id)
                    .ThenByDescending(m => m.CategoryId).AsQueryable();
                return PartialView("_MenuGrid", result);
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.Admin)]
        public async Task<IActionResult> IsCategoryExists(int id)
        {
            try
            {
                List<MenuOutputViewModel> menuOutputViewModels = [];

                #region AdminCategories

                foreach (Category item in await _categoryRepository.GetCategoryByRoleAsync(Role.Admin))
                {
                    menuOutputViewModels.Add(new MenuOutputViewModel()
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
                    });
                }

                #endregion

                #region UserCategories

                foreach (Category item in await _categoryRepository.GetCategoryByRoleAsync(Role.User))
                {
                    menuOutputViewModels.Add(new MenuOutputViewModel()
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
                    });
                }

                #endregion

                #region AnonymousCategories

                foreach (Category item in await _categoryRepository.GetCategoryByRoleAsync(Role.Anonymous))
                {
                    menuOutputViewModels.Add(new MenuOutputViewModel()
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
                    });
                }

                #endregion

                MenuOutputViewModel selectedCategory = menuOutputViewModels.Where(a => a.Id == id).FirstOrDefault();
                if (selectedCategory == null)
                {
                    throw new Exception(); // DB에서 못 찾음
                }

                return Json(new
                    { result = true, category = menuOutputViewModels.Where(a => a.Id == id).FirstOrDefault() });
            }
            catch
            {
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.Admin)]
        public async Task<IActionResult> IsSubCategoryExists(int id)
        {
            try
            {
                List<MenuOutputViewModel> menuOutputViewModels = [];

                #region AdminSubCategories

                foreach (SubCategory item in await _subCategoryRepository.GetSubCategoryByRoleAsync(Role.Admin))
                {
                    menuOutputViewModels.Add(new MenuOutputViewModel()
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
                    });
                }

                #endregion

                #region UserSubCategories

                foreach (SubCategory item in await _subCategoryRepository.GetSubCategoryByRoleAsync(Role.User))
                {
                    menuOutputViewModels.Add(new MenuOutputViewModel()
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
                    });
                }

                #endregion

                #region AnonymousSubCategories

                foreach (SubCategory item in await _subCategoryRepository.GetSubCategoryByRoleAsync(Role.Anonymous))
                {
                    menuOutputViewModels.Add(new MenuOutputViewModel()
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
                    });
                }

                #endregion

                MenuOutputViewModel selectedCategory = menuOutputViewModels.Where(a => a.Id == id).FirstOrDefault();
                if (selectedCategory == null)
                {
                    throw new Exception(); // DB에서 못 찾음
                }

                return Json(new
                    { result = true, subCategory = menuOutputViewModels.Where(a => a.Id == id).FirstOrDefault() });
            }
            catch
            {
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
            }
        }

        #endregion

        #region Update

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.Admin)]
        public async Task<IActionResult> UpdateCategory([FromBody] MenuInputViewModel menuInputViewModel)
        {
            try
            {
                _ = ModelState.Remove(nameof(menuInputViewModel
                    .Action)); // remove ModelState check in Action (in Request parameters, Action can be null)

                if (ModelState.IsValid)
                {
                    Category category = new()
                    {
                        Id = menuInputViewModel.Id,
                        Name = menuInputViewModel.Name,
                        DisplayName = menuInputViewModel.DisplayName,
                        IconPath = menuInputViewModel.IconPath,
                        Controller = menuInputViewModel.Controller,
                        Action = menuInputViewModel.Action,
                        Role = menuInputViewModel.Role,
                        Order = menuInputViewModel.Order
                    };
                    await _categoryRepository.UpdateCategoryAsync(category);

                    return Json(new { result = true, message = _localizer["Successfully updated the category"].Value });
                }
                else
                {
                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                }
            }
            catch
            {
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.Admin)]
        public async Task<IActionResult> UpdateSubCategory([FromBody] MenuInputViewModel menuInputViewModel)
        {
            try
            {
                _ = ModelState.Remove(nameof(menuInputViewModel
                    .Controller)); // remove ModelState check in Controller (in Request parameters, Controller can be null)

                if (ModelState.IsValid)
                {
                    SubCategory subCategory = new()
                    {
                        Id = menuInputViewModel.Id,
                        CategoryId = menuInputViewModel.CategoryId,
                        Name = menuInputViewModel.Name,
                        DisplayName = menuInputViewModel.DisplayName,
                        IconPath = menuInputViewModel.IconPath,
                        Action = menuInputViewModel.Action,
                        Role = menuInputViewModel.Role,
                        Order = menuInputViewModel.Order
                    };
                    await _subCategoryRepository.UpdateSubCategoryAsync(subCategory);

                    return Json(new
                        { result = true, message = _localizer["Successfully updated the subCategory"].Value });
                }
                else
                {
                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                }
            }
            catch
            {
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
            }
        }

        #endregion

        #region Delete

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.Admin)]
        public async Task<IActionResult> DeleteCategory([FromBody] MenuInputViewModel menuInputViewModel)
        {
            try
            {
                _ = ModelState.Remove(nameof(menuInputViewModel
                    .Action)); // remove ModelState check in Action (in Request parameters, Action can be null)

                if (ModelState.IsValid)
                {
                    Category category = new()
                    {
                        Id = menuInputViewModel.Id,
                        Name = menuInputViewModel.Name,
                        DisplayName = menuInputViewModel.DisplayName,
                        IconPath = menuInputViewModel.IconPath,
                        Controller = menuInputViewModel.Controller,
                        Action = menuInputViewModel.Action,
                        Role = menuInputViewModel.Role,
                        Order = menuInputViewModel.Order
                    };
                    await _categoryRepository.DeleteCategoryAsync(category);

                    return Json(new { result = true, message = _localizer["Successfully deleted the category"].Value });
                }
                else
                {
                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                }
            }
            catch
            {
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.Admin)]
        public async Task<IActionResult> DeleteSubCategory([FromBody] MenuInputViewModel menuInputViewModel)
        {
            try
            {
                _ = ModelState.Remove(nameof(menuInputViewModel
                    .Controller)); // remove ModelState check in Controller (in Request parameters, Controller can be null)

                if (ModelState.IsValid)
                {
                    SubCategory subCategory = new()
                    {
                        Id = menuInputViewModel.Id,
                        CategoryId = menuInputViewModel.CategoryId,
                        Name = menuInputViewModel.Name,
                        DisplayName = menuInputViewModel.DisplayName,
                        IconPath = menuInputViewModel.IconPath,
                        Action = menuInputViewModel.Action,
                        Role = menuInputViewModel.Role,
                        Order = menuInputViewModel.Order
                    };
                    await _subCategoryRepository.DeleteSubCategoryAsync(subCategory);

                    return Json(new
                        { result = true, message = _localizer["Successfully deleted the subCategory"].Value });
                }
                else
                {
                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                }
            }
            catch
            {
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
            }
        }

        #endregion

        #region Excel

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.Admin)]
        public async Task<IActionResult> ExportExcelMenu(string fileName = "")
        {
            await Task.Yield();

            List<MenuOutputViewModel> menuOutputViewModels = [];
            _ = HttpContext.Session.TryGetValue(
                Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous.Utilities
                    .Session.Account), out byte[] resultByte);
            string loginedAccountTimeZoneIanaId = JsonConvert
                .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).TimeZoneIanaId;

            #region AdminCategories
            foreach (Category item in await _categoryRepository.GetCategoryByRoleAsync(Role.Admin))
            {
                menuOutputViewModels.Add(new MenuOutputViewModel()
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
                });
            }
            #endregion

            #region UserCategories
            foreach (Category item in await _categoryRepository.GetCategoryByRoleAsync(Role.User))
            {
                menuOutputViewModels.Add(new MenuOutputViewModel()
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
                });
            }
            #endregion

            #region AnonymousCategories
            foreach (Category item in await _categoryRepository.GetCategoryByRoleAsync(Role.Anonymous))
            {
                menuOutputViewModels.Add(new MenuOutputViewModel()
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
                });
            }
            #endregion

            #region AdminSubCategories
            foreach (SubCategory item in await _subCategoryRepository.GetSubCategoryByRoleAsync(Role.Admin))
            {
                menuOutputViewModels.Add(new MenuOutputViewModel()
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
                });
            }
            #endregion

            #region UserSubCategories
            foreach (SubCategory item in await _subCategoryRepository.GetSubCategoryByRoleAsync(Role.User))
            {
                menuOutputViewModels.Add(new MenuOutputViewModel()
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
                });
            }
            #endregion

            #region AnonymousSubCategories
            foreach (SubCategory item in await _subCategoryRepository.GetSubCategoryByRoleAsync(Role.Anonymous))
            {
                menuOutputViewModels.Add(new MenuOutputViewModel()
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
                });
            }
            #endregion

            menuOutputViewModels = menuOutputViewModels.OrderByDescending(m => m.Id).ThenByDescending(m => m.CategoryId)
                .ToList();

            MemoryStream stream = new MemoryStream();

            using (SpreadsheetDocument spreadsheetDocument =
                   SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
            {
                WorkbookPart workbookPart = spreadsheetDocument.AddWorkbookPart();
                workbookPart.Workbook = new Workbook();

                WorksheetPart worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                worksheetPart.Worksheet = new Worksheet(new SheetData());

                Sheets sheets = spreadsheetDocument.WorkbookPart.Workbook.AppendChild(new Sheets());
                Sheet sheet = new Sheet()
                {
                    Id = spreadsheetDocument.WorkbookPart.GetIdOfPart(worksheetPart),
                    SheetId = 1,
                    Name = "Sheet1"
                };
                sheets.Append(sheet);

                SheetData sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>();

                Cell CreateCell(string value)
                {
                    return new Cell()
                    {
                        CellValue = new CellValue(value),
                        DataType = CellValues.String
                    };
                }

                Row headerRow = new Row();
                headerRow.Append(
                    CreateCell(_localizer[nameof(MenuOutputViewModel.Id).ToString()].Value),
                    CreateCell(_localizer[nameof(MenuOutputViewModel.CategoryId).ToString()].Value),
                    CreateCell(_localizer[nameof(MenuOutputViewModel.Name).ToString()].Value),
                    CreateCell(_localizer[nameof(MenuOutputViewModel.DisplayName).ToString()].Value),
                    CreateCell(_localizer[nameof(MenuOutputViewModel.IconPath).ToString()].Value),
                    CreateCell(_localizer[nameof(MenuOutputViewModel.Controller).ToString()].Value),
                    CreateCell(_localizer[nameof(MenuOutputViewModel.Action).ToString()].Value),
                    CreateCell(_localizer[nameof(MenuOutputViewModel.Role).ToString()].Value),
                    CreateCell(_localizer[nameof(MenuOutputViewModel.Order).ToString()].Value)
                );
                sheetData.Append(headerRow);

                foreach (MenuOutputViewModel item in menuOutputViewModels)
                {
                    Row dataRow = new Row();
                    dataRow.Append(
                        CreateCell(item.Id.ToString()),
                        CreateCell(item.CategoryId?.ToString() ?? string.Empty),
                        CreateCell(item.Name),
                        CreateCell(item.DisplayName),
                        CreateCell(item.IconPath),
                        CreateCell(item.Controller),
                        CreateCell(item.Action),
                        CreateCell(item.Role),
                        CreateCell(item.Order.ToString())
                    );
                    sheetData.Append(dataRow);
                }

                workbookPart.Workbook.Save();
            }

            stream.Position = 0;
            string excelName =
                $"{fileName}-{DateTime.UtcNow.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId):yyyy-MM-dd-HH-mm-ss-fff}.xlsx";

            return File(stream, "application/octet-stream", excelName);
        }

        #endregion

        #endregion

        #region PrivateNote

        #region Create

        #region Write

        #region PrivateNoteBoard

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.Admin)]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> WritePrivateNoteBoard(BoardInputViewModel boardInputViewModel)
        {
            try
            {
                _ = ModelState.Remove(nameof(boardInputViewModel.Locked));

                if (ModelState.IsValid)
                {
                    boardInputViewModel.Content ??= ""; // 내용이 아무것도 입력되지 않았다면

                    #region HtmlSanitizer

                    HtmlSanitizer sanitizer = new();
                    _ = sanitizer.AllowedAttributes.Add("class");
                    string sanitized = sanitizer.Sanitize(boardInputViewModel.Content);
                    boardInputViewModel.Content = sanitized;

                    #endregion

                    #region Decrypt img tag file path

                    HtmlDocument htmlDocument = new();
                    htmlDocument.LoadHtml(boardInputViewModel.Content);

                    foreach (HtmlNode imgTag in htmlDocument.DocumentNode.Descendants("img") ?? [])
                    {
                        string encryptedFilePath = imgTag.GetAttributeValue("alt", "");
                        _ = imgTag.SetAttributeValue("alt", _rsa.Decrypt(encryptedFilePath));
                    }

                    boardInputViewModel.Content = htmlDocument.DocumentNode.OuterHtml;

                    #endregion

                    if (boardInputViewModel.Title.Length is not (> 0 and <= 100)) // 제목 길이
                    {
                        return Json(new
                        {
                            result = false,
                            error = _localizer["Title length is must be between 1 and 100 characters."].Value
                        });
                    }

                    if (boardInputViewModel.Content.Length > 16384) // Content Maxlength: 16KB
                    {
                        return Json(new
                        {
                            result = false,
                            error = _localizer["content length is must be between 0 and 16384 characters."].Value
                        });
                    }

                    if (boardInputViewModel.UploadedFile != null) // 파일 첨부가 되었다면
                    {
                        if (!(Path.GetExtension(boardInputViewModel.UploadedFile.FileName) == ".zip")) // zip 확장자만 가능
                        {
                            return Json(new
                                { result = false, error = _localizer["Only zip extension allowed."].Value });
                        }

                        if (boardInputViewModel.UploadedFile.Length is not (> 0 and <= 10485760)) // 10MB 이하만 가능
                        {
                            return Json(new
                            {
                                result = false,
                                error = _localizer["uploaded file size must be smaller than 10MB."].Value
                            });
                        }
                    }

                    try
                    {
                        bool isAccountSessionExist = HttpContext.Session.TryGetValue(
                            Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                                .Utilities.Session.Account), out byte[] resultByte);

                        if (isAccountSessionExist)
                        {
                            Account loginedAccount =
                                JsonConvert.DeserializeObject<Account>(
                                    Encoding.Default.GetString(resultByte)); // Get Session
                            Board board = new();

                            if (loginedAccount.Role == Role.Admin) // 관리자인 경우
                            {
                                board.Type = EnumHelper.GetDescription(BoardType.PrivateNote); // 사적 메모
                                board.Title = boardInputViewModel.Title;
                                board.Content = boardInputViewModel.Content;
                                board.Writer = loginedAccount.Nickname;
                                board.Created = DateTime.UtcNow;
                                board.Updated = DateTime.UtcNow;
                                board.View = 0;
                                board.Deleted = false;
                                //board.Locked = boardInputViewModel.Locked;
                                board.Noticed = boardInputViewModel.Noticed;
                            }
                            else if (loginedAccount.Role == Role.User) // 일반 사용자인 경우
                            {
                                board.Type = EnumHelper.GetDescription(BoardType.PrivateNote); // 사적 메모
                                board.Title = boardInputViewModel.Title;
                                board.Content = boardInputViewModel.Content;
                                board.Writer = loginedAccount.Nickname;
                                board.Created = DateTime.UtcNow;
                                board.Updated = DateTime.UtcNow;
                                board.View = 0;
                                board.Deleted = false;
                                //board.Locked = boardInputViewModel.Locked;
                            }
                            else // 비로그인 사용자인 경우
                            {
                                return Json(new
                                    { result = false, error = _localizer["Please Login to write board"].Value });
                            }

                            await _boardRepository.WriteBoardAsync(board);

                            #region 첨부 파일 저장

                            if (boardInputViewModel.UploadedFile != null) // 첨부 파일 존재 시,
                            {
                                if (boardInputViewModel.UploadedFile
                                        .Length is
                                    > 0 and <= 10485760) // boardInputViewModel.UploadedFile Length : 파일 크기 0MB 이상, 10MB 이하
                                {
                                    var createdBoardId = board.Id; // 첨부파일 FK 해서 로직 추가할 것

                                    string boardAttachedFilePath = Path.Combine("upload", "Management",
                                        EnumHelper.GetDescription(BoardType.PrivateNote), "boardAttachedFiles",
                                        $"{createdBoardId}");

                                    string guid = Guid.NewGuid().ToString().ToUpper();
                                    string attachedFile =
                                        $"{guid}{Path.GetExtension(boardInputViewModel.UploadedFile.FileName)}";
                                    string filePath = Path.Combine(boardAttachedFilePath, attachedFile);

                                    _ = await _fileRepository.UploadAsync(boardInputViewModel.UploadedFile, filePath);

                                    await _boardAttachedFileRepository.SaveBoardAttachedFileAsync(
                                        new BoardAttachedFile()
                                        {
                                            BoardId = createdBoardId,
                                            Size = Convert.ToInt32(boardInputViewModel.UploadedFile.Length),
                                            Name = Path.GetFileNameWithoutExtension(boardInputViewModel.UploadedFile
                                                .FileName),
                                            Extension = Path.GetExtension(boardInputViewModel.UploadedFile.FileName),
                                            Path =
                                                $"upload/Management/{EnumHelper.GetDescription(BoardType.PrivateNote)}/boardAttachedFiles/{createdBoardId}/{attachedFile}"
                                        });
                                }
                                else
                                {
                                    return Ok(new
                                    {
                                        result = false,
                                        errorMessage = _localizer["File Size must be smaller than 10MB."].Value
                                    });
                                }
                            }

                            #endregion

                            return Json(new
                            {
                                result = true, message = _localizer["The board has been successfully created."].Value
                            });
                        }
                        else
                        {
                            return Json(new
                                { result = false, error = _localizer["Please Login to write board"].Value });
                        }
                    }
                    catch
                    {
                        return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                    }
                }
                else
                {
                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                }
            }
            catch
            {
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
            }
        }

        #endregion

        #region PrivateNoteComment

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.Admin)]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> WritePrivateNoteComment(
            [FromBody] BoardCommentInputViewModel boardCommentInputViewModel)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    if (string.IsNullOrEmpty(boardCommentInputViewModel.Content))
                    {
                        return Json(new { result = false, error = _localizer["Please enter a comment."].Value });
                    }

                    #region HtmlSanitizer

                    HtmlSanitizer sanitizer = new();
                    _ = sanitizer.AllowedAttributes.Add("class");
                    string sanitized = sanitizer.Sanitize(boardCommentInputViewModel.Content);
                    boardCommentInputViewModel.Content = sanitized;

                    #endregion

                    try
                    {
                        bool isAccountSessionExist = HttpContext.Session.TryGetValue(
                            Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                                .Utilities.Session.Account), out byte[] resultByte);

                        if (isAccountSessionExist)
                        {
                            Account loginedAccount =
                                JsonConvert.DeserializeObject<Account>(
                                    Encoding.Default.GetString(resultByte)); // Get Session

                            IEnumerable<Board> privateNoteBoards =
                                await _boardRepository.GetOneTypeBoardsAsync(
                                    EnumHelper.GetDescription(BoardType.PrivateNote));
                            privateNoteBoards = privateNoteBoards.Where(m => m.Writer == loginedAccount.Nickname)
                                .Where(m => m.Deleted == false).OrderByDescending(a => a.Id);
                            Board privateNoteBoard =
                                privateNoteBoards.FirstOrDefault(m => m.Id == boardCommentInputViewModel.BoardId);

                            if (privateNoteBoard.Id != boardCommentInputViewModel.BoardId)
                            {
                                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                            }

                            IEnumerable<BoardComment> totalBoardCommentsInSelectedBoard =
                                await _boardCommentRepository.GetOneBoardCommentsAsync(boardCommentInputViewModel
                                    .BoardId);

                            BoardComment boardComment = new()
                            {
                                BoardId = boardCommentInputViewModel.BoardId,
                                Order = totalBoardCommentsInSelectedBoard?.Count() ?? default,
                                AvatarImagePath = loginedAccount.AvatarImagePath,
                                Writer = loginedAccount.Nickname,
                                Content = boardCommentInputViewModel.Content,
                                Created = DateTime.UtcNow,
                                Deleted = false
                            };
                            await _boardCommentRepository.WriteBoardCommentAsync(boardComment);

                            return Json(new
                            {
                                result = true, boardId = boardCommentInputViewModel.BoardId,
                                page = boardCommentInputViewModel.DetailCurrentPage
                            });
                        }
                        else
                        {
                            return Json(new
                                { result = false, error = _localizer["Please Login to write comment."].Value });
                        }
                    }
                    catch
                    {
                        return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                    }
                }
                else
                {
                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                }
            }
            catch
            {
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
            }
        }

        #endregion

        #endregion

        #region Summernote Image File Upload
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.Admin)]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> UploadImageFile(IFormFile summernoteImageFile)
        {
            if (summernoteImageFile == null)
            {
                return Ok(new { result = false, errorMessage = _localizer["Please attach a file."].Value });
            }

            if (summernoteImageFile.Length <= 0 || summernoteImageFile.Length > 10 * 1024 * 1024)
            {
                return Ok(new { result = false, errorMessage = _localizer["File Size must be smaller than 10MB."].Value });
            }

            // 확장자 검사
            var ext = Path.GetExtension(summernoteImageFile.FileName).ToLowerInvariant();
            if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
            {
                return Ok(new { result = false, errorMessage = _localizer["Only .jpg, .jpeg or .png file allowed."].Value });
            }

            try
            {
                // 실제 이미지 여부 검사 (Magick.NET)
                using var image = new MagickImage(summernoteImageFile.OpenReadStream());
                // Magick.NET에서 예외가 발생하면 이미지가 아니거나 허용되지 않는 포맷임
            }
            catch (MagickException)
            {
                return Ok(new { result = false, errorMessage = _localizer["Invalid image file."].Value });
            }

            _ = HttpContext.Session.TryGetValue(
                Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous.Utilities.Session.Account),
                out byte[] resultByte);

            var loginedAccount = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte));

            string summernoteImagePath = Path.Combine("upload", "Management",
                EnumHelper.GetDescription(BoardType.PrivateNote), "summernote", "images");
            string imageFile = $"{Guid.NewGuid():N}{ext}";
            string filePath = Path.Combine(summernoteImagePath, imageFile);

            var uploadResult = await _fileRepository.UploadAsync(summernoteImageFile, filePath);

            if (uploadResult)
            {
                var fileStream = await _fileRepository.DownloadAsync(filePath);
                return Ok(new
                {
                    result = true,
                    file = File(fileStream, summernoteImageFile.ContentType, imageFile),
                    filePath = _rsa.Encrypt(filePath)
                });
            }
            else
            {
                return Ok(new { result = false, errorMessage = _localizer["Input is invalid"].Value });
            }
        }
        #endregion

        #endregion

        #region Read

        #region Edit, List, Detail, Write

        public async Task<IActionResult> PrivateNote(string method = "list", int? boardId = null, int page = 1,
            string searchType = "", string searchText = "")
        {
            if (method == "write") // 글쓰기 (write)
            {
                PrivateNoteOutputViewModel privateNoteOutputViewModel = new();

                #region 게시판 글쓰기

                privateNoteOutputViewModel.Method = method;

                #endregion

                bool isAccountSessionExist = HttpContext.Session.TryGetValue(
                    Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                        .Utilities.Session.Account), out _); // 로그인이 되어 계정 세션이 생겼는지 확인

                return isAccountSessionExist ? View(privateNoteOutputViewModel) : RedirectToAction("Login", "Account");
            }
            else if (method == "detail") // 상세보기 (detail)
            {
                _ = HttpContext.Session.TryGetValue(
                    Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                        .Utilities.Session.Account), out byte[] resultByte); // 작성자 또는 관리자인 경우
                Account loginedAccount =
                    JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)); // Get Session

                if (boardId == null)
                {
                    return RedirectToAction("PrivateNote", "Management");
                }

                PrivateNoteOutputViewModel privateNoteOutputViewModel = new()
                {
                    Method = method,
                    DetailCurrentPage = page,
                    DetailBoardId = boardId ?? default
                };

                IEnumerable<Board> privateNoteBoards =
                    await _boardRepository.GetOneTypeBoardsAsync(EnumHelper.GetDescription(BoardType.PrivateNote));
                privateNoteBoards = privateNoteBoards.Where(m => m.Writer == loginedAccount.Nickname)
                    .Where(m => m.Deleted == false).OrderByDescending(a => a.Id);
                List<BoardAttachedFile> boardAttachedFiles = await _boardAttachedFileRepository.GetAllAsync();

                try
                {
                    Board privateNoteBoard = privateNoteBoards.FirstOrDefault(m => m.Id == boardId);

                    if (privateNoteBoard.Locked)
                    {
                        if (!(loginedAccount.Nickname == privateNoteBoard.Writer || loginedAccount.Role == Role.Admin))
                        {
                            return RedirectToAction("PrivateNote", "Management");
                        }
                    }

                    privateNoteBoard.View++;
                    await _boardRepository.UpdateBoardAsync(privateNoteBoard);
                    BoardAttachedFile attachedFile = boardAttachedFiles.FirstOrDefault(m => m.BoardId == boardId);

                    #region Encrypt img tag file path

                    HtmlDocument htmlDocument = new();
                    htmlDocument.LoadHtml(privateNoteBoard.Content);
                    bool isImgTagIncluded = false;

                    foreach (HtmlNode imgTag in htmlDocument.DocumentNode.Descendants("img") ?? [])
                    {
                        isImgTagIncluded = true;
                        string filePath = imgTag.GetAttributeValue("alt", "");

                        // 파일을 다운로드하는 메서드
                        byte[] fileData = await _fileRepository.DownloadAsync(filePath);

                        if (fileData != null)
                        {
                            // ContentType을 추출하기 위한 FileExtensionContentTypeProvider 사용
                            FileExtensionContentTypeProvider provider = new();

                            // 기본 contentType은 "application/octet-stream"으로 설정
                            if (!provider.TryGetContentType(filePath, out string contentType))
                            {
                                contentType = "application/octet-stream";
                            }

                            // 파일 데이터를 Base64로 변환
                            string base64Data = Convert.ToBase64String(fileData);

                            // img 태그에 base64 데이터 저장 (src에 넣는 대신 data-file 속성 사용)
                            _ = imgTag.SetAttributeValue("data-file", base64Data);
                            _ = imgTag.SetAttributeValue("data-contenttype", contentType);
                            _ = imgTag.SetAttributeValue("alt", _rsa.Encrypt(filePath));
                        }
                    }

                    privateNoteBoard.Content = htmlDocument.DocumentNode.OuterHtml;

                    #endregion

                    privateNoteOutputViewModel.BoardOutputViewModel =
                        new Maroik.WebSite.Models.ViewModels.Management.BoardOutputViewModel()
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

                    if (!string.IsNullOrEmpty(privateNoteOutputViewModel?.BoardOutputViewModel?.BoardAttachedFilePath ??
                                              ""))
                    {
                        byte[] fileData = await _fileRepository.DownloadAsync(
                            privateNoteOutputViewModel?.BoardOutputViewModel?.BoardAttachedFilePath ?? "");

                        if (fileData != null)
                        {
                            // ContentType을 추출하기 위한 FileExtensionContentTypeProvider 사용
                            FileExtensionContentTypeProvider provider = new();

                            // 기본 contentType은 "application/octet-stream"으로 설정
                            if (!provider.TryGetContentType(
                                    privateNoteOutputViewModel?.BoardOutputViewModel?.BoardAttachedFilePath ?? "",
                                    out string contentType))
                            {
                                contentType = "application/octet-stream";
                            }

                            // 파일 데이터를 Base64로 변환
                            privateNoteOutputViewModel.BoardOutputViewModel.BoardAttachedFileBase64Data =
                                Convert.ToBase64String(fileData);
                            privateNoteOutputViewModel.BoardOutputViewModel.BoardAttachedFileContentType = contentType;
                        }
                    }

                    return View(privateNoteOutputViewModel);
                }
                catch
                {
                    return RedirectToAction("PrivateNote", "Management");
                }
            }
            else if (method == "edit") // 수정 (edit)
            {
                if (boardId == null)
                {
                    return RedirectToAction("PrivateNote", "Management");
                }

                PrivateNoteOutputViewModel privateNoteOutputViewModel = new()
                {
                    Method = method,
                    EditCurrentPage = page,
                    EditBoardId = boardId ?? default
                };

                _ = HttpContext.Session.TryGetValue(
                    Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                        .Utilities.Session.Account), out byte[] resultByte);
                Account loginedAccount = await _accountRepository.GetAccountByEmailAsync(JsonConvert
                    .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);

                IEnumerable<Board> privateNoteBoards =
                    await _boardRepository.GetOneTypeBoardsAsync(EnumHelper.GetDescription(BoardType.PrivateNote));
                privateNoteBoards = privateNoteBoards.Where(m => m.Writer == loginedAccount.Nickname)
                    .Where(m => m.Deleted == false).OrderByDescending(a => a.Id);
                List<BoardAttachedFile> boardAttachedFiles = await _boardAttachedFileRepository.GetAllAsync();

                try
                {
                    Board privateNoteBoard = privateNoteBoards.FirstOrDefault(m => m.Id == boardId);

                    if (privateNoteBoard.Writer != loginedAccount.Nickname) // 작성자와 로그인한 사용자가 같지 않으면
                    {
                        return RedirectToAction("PrivateNote", "Management");
                    }

                    BoardAttachedFile attachedFile = boardAttachedFiles.FirstOrDefault(m => m.BoardId == boardId);

                    #region Encrypt img tag file path

                    HtmlDocument htmlDocument = new();
                    htmlDocument.LoadHtml(privateNoteBoard.Content);
                    bool isImgTagIncluded = false;

                    foreach (HtmlNode imgTag in htmlDocument.DocumentNode.Descendants("img") ?? [])
                    {
                        isImgTagIncluded = true;
                        string filePath = imgTag.GetAttributeValue("alt", "");

                        // 파일을 다운로드하는 메서드
                        byte[] fileData = await _fileRepository.DownloadAsync(filePath);

                        if (fileData != null)
                        {
                            // ContentType을 추출하기 위한 FileExtensionContentTypeProvider 사용
                            FileExtensionContentTypeProvider provider = new();

                            // 기본 contentType은 "application/octet-stream"으로 설정
                            if (!provider.TryGetContentType(filePath, out string contentType))
                            {
                                contentType = "application/octet-stream";
                            }

                            // 파일 데이터를 Base64로 변환
                            string base64Data = Convert.ToBase64String(fileData);

                            // img 태그에 base64 데이터 저장 (src에 넣는 대신 data-file 속성 사용)
                            _ = imgTag.SetAttributeValue("data-file", base64Data);
                            _ = imgTag.SetAttributeValue("data-contenttype", contentType);
                            _ = imgTag.SetAttributeValue("alt", _rsa.Encrypt(filePath));
                        }
                    }

                    privateNoteBoard.Content = htmlDocument.DocumentNode.OuterHtml;

                    #endregion

                    privateNoteOutputViewModel.BoardOutputViewModel =
                        new Maroik.WebSite.Models.ViewModels.Management.BoardOutputViewModel()
                        {
                            Title = privateNoteBoard.Title,
                            Writer = privateNoteBoard.Writer,
                            Views = privateNoteBoard.View,
                            Content = privateNoteBoard.Content,
                            Locked = privateNoteBoard.Locked,
                            Updated = privateNoteBoard.Updated,
                            BoardAttachedFileName = attachedFile?.Name ?? "",
                            BoardAttachedFileExtension = attachedFile?.Extension ?? "",
                            BoardAttachedFileSize = attachedFile?.Size ?? 0,
                            BoardAttachedFilePath = attachedFile?.Path ?? "",
                            IsImgTagIncluded = isImgTagIncluded
                        };

                    if (!string.IsNullOrEmpty(privateNoteOutputViewModel?.BoardOutputViewModel?.BoardAttachedFilePath ??
                                              ""))
                    {
                        byte[] fileData = await _fileRepository.DownloadAsync(
                            privateNoteOutputViewModel?.BoardOutputViewModel?.BoardAttachedFilePath ?? "");

                        if (fileData != null)
                        {
                            // ContentType을 추출하기 위한 FileExtensionContentTypeProvider 사용
                            FileExtensionContentTypeProvider provider = new();

                            // 기본 contentType은 "application/octet-stream"으로 설정
                            if (!provider.TryGetContentType(
                                    privateNoteOutputViewModel?.BoardOutputViewModel?.BoardAttachedFilePath ?? "",
                                    out string contentType))
                            {
                                contentType = "application/octet-stream";
                            }

                            // 파일 데이터를 Base64로 변환
                            privateNoteOutputViewModel.BoardOutputViewModel.BoardAttachedFileBase64Data =
                                Convert.ToBase64String(fileData);
                            privateNoteOutputViewModel.BoardOutputViewModel.BoardAttachedFileContentType = contentType;
                        }
                    }

                    return View(privateNoteOutputViewModel);
                }
                catch
                {
                    return RedirectToAction("PrivateNote", "Management");
                }
            }
            else // 목록 (list)
            {
                bool isAccountSessionExist = HttpContext.Session.TryGetValue(
                    Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                        .Utilities.Session.Account), out byte[] resultByte);
                Account loginedAccount =
                    JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)); // Get Session

                #region 게시판 기본 페이징 로직

                const int pageSize = 5;
                if (page < 1)
                {
                    page = 1;
                }

                #endregion

                #region 관련된 모든 게시판 데이터 가져오는 로직

                IEnumerable<Board> privateNoteBoards =
                    await _boardRepository.GetOneTypeBoardsAsync(EnumHelper.GetDescription(BoardType.PrivateNote));
                privateNoteBoards = privateNoteBoards.Where(m => m.Writer == loginedAccount.Nickname)
                    .Where(m => m.Deleted == false && m.Noticed == false).OrderByDescending(a => a.Id);

                #endregion

                #region 검색어 로직

                if (searchType == "Title" && !string.IsNullOrEmpty(searchText))
                {
                    if (isAccountSessionExist) // 로그인이 되어 있다면
                    {
                        if (loginedAccount.Role == Role.Admin) // 관리자는 모든 게시물을 볼 수 있음
                        {
                            privateNoteBoards = privateNoteBoards.Where(a => a.Title.Contains(searchText));
                        }
                        else if (loginedAccount.Role == Role.User) // 사용자는 보통 글 + 자신이 쓴 비밀글만 볼 수 있음
                        {
                            List<Board> searchPrivateNoteBoards = [];
                            List<Board> noLockedPrivateNoteBoards = privateNoteBoards
                                .Where(a => a.Title.Contains(searchText)).Where(a => !a.Locked).ToList();
                            List<Board> myLockedPrivateNoteBoards = privateNoteBoards
                                .Where(a => a.Title.Contains(searchText))
                                .Where(a => a.Locked && a.Writer == loginedAccount.Nickname).ToList();
                            searchPrivateNoteBoards.AddRange(noLockedPrivateNoteBoards);
                            searchPrivateNoteBoards.AddRange(myLockedPrivateNoteBoards);

                            privateNoteBoards = searchPrivateNoteBoards;
                        }
                        else // 로그인 시, Admin 또는 User 권한이 없다면 버그임
                        {
                        }
                    }
                    else // 로그인이 되지 않았다면
                    {
                        privateNoteBoards = privateNoteBoards.Where(a => a.Title.Contains(searchText))
                            .Where(a => !a.Locked); // 비 로그인 시, 비밀글은 검색 되지 않도록 해야함
                    }
                }
                else if (searchType == "Writer" && !string.IsNullOrEmpty(searchText))
                {
                    privateNoteBoards = privateNoteBoards.Where(a => a.Writer.Contains(searchText));
                }

                #endregion

                PrivateNoteOutputViewModel privateNoteOutputViewModel = new();

                #region 공지사항

                privateNoteOutputViewModel.NoticeBoards = [];

                #endregion

                #region 게시판 목록

                privateNoteOutputViewModel.Method = "list";

                #endregion

                #region 게시판 페이징 로직

                privateNoteOutputViewModel.Pager = new Pager(privateNoteBoards.Count(), page, pageSize);

                #endregion

                #region 게시판 데이터 로직

                privateNoteOutputViewModel.Boards = privateNoteBoards.Skip((page - 1) * pageSize)
                    .Take(privateNoteOutputViewModel.Pager.PageSize).ToList();

                #endregion

                ViewBag.SelectedSearchType = searchType;
                ViewBag.TypedSearchText = searchText;
                return View(privateNoteOutputViewModel);
            }
        }

        #endregion

        #region IsBoardExists

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.Admin)]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> IsBoardExists(int id)
        {
            try
            {
                _ = HttpContext.Session.TryGetValue(
                    Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                        .Utilities.Session.Account), out byte[] resultByte);
                Account loginedAccount = await _accountRepository.GetAccountByEmailAsync(JsonConvert
                    .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);

                IEnumerable<Board> privateNoteBoards =
                    await _boardRepository.GetOneTypeBoardsAsync(EnumHelper.GetDescription(BoardType.PrivateNote));
                privateNoteBoards = privateNoteBoards.Where(m => m.Writer == loginedAccount.Nickname)
                    .Where(m => m.Deleted == false).OrderByDescending(a => a.Id);

                if (privateNoteBoards == null)
                {
                    return Json(new { result = false, error = _localizer["No PrivateNoteBoard exists"].Value });
                }
                else
                {
                    Board tempPrivateNoteBoard = privateNoteBoards.Where(a => a.Id == id).FirstOrDefault();

                    return tempPrivateNoteBoard == null
                        ? Json(new { result = false, error = _localizer["Input is invalid"].Value })
                        : (IActionResult)Json(new { result = true, PrivateNoteBoard = tempPrivateNoteBoard });
                }
            }
            catch
            {
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
            }
        }

        #endregion

        #endregion

        #region Update

        #region Edit

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.Admin)]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> EditPrivateNoteBoard(BoardInputViewModel boardInputViewModel)
        {
            try
            {
                _ = ModelState.Remove(nameof(boardInputViewModel.Locked));

                if (ModelState.IsValid)
                {
                    boardInputViewModel.Content ??= ""; // 내용이 아무것도 입력되지 않았다면

                    #region HtmlSanitizer

                    HtmlSanitizer sanitizer = new();
                    _ = sanitizer.AllowedAttributes.Add("class");
                    string sanitized = sanitizer.Sanitize(boardInputViewModel.Content);
                    boardInputViewModel.Content = sanitized;

                    #endregion

                    #region Decrypt img tag file path

                    HtmlDocument htmlDocument = new();
                    htmlDocument.LoadHtml(boardInputViewModel.Content);

                    foreach (HtmlNode imgTag in htmlDocument.DocumentNode.Descendants("img") ?? [])
                    {
                        string encryptedFilePath = imgTag.GetAttributeValue("alt", "");
                        _ = imgTag.SetAttributeValue("alt", _rsa.Decrypt(encryptedFilePath));
                    }

                    boardInputViewModel.Content = htmlDocument.DocumentNode.OuterHtml;

                    #endregion

                    if (boardInputViewModel.Title.Length is not (> 0 and <= 100)) // 제목 길이
                    {
                        return Json(new
                        {
                            result = false,
                            error = _localizer["Title length is must be between 1 and 100 characters."].Value
                        });
                    }

                    if (boardInputViewModel.Content.Length > 16384) // Content Maxlength: 16KB
                    {
                        return Json(new
                        {
                            result = false,
                            error = _localizer["content length is must be between 0 and 16384 characters."].Value
                        });
                    }

                    if (boardInputViewModel.UploadedFile != null) // 파일 첨부가 되었다면
                    {
                        if (!(Path.GetExtension(boardInputViewModel.UploadedFile.FileName) == ".zip")) // zip 확장자만 가능
                        {
                            return Json(new
                                { result = false, error = _localizer["Only zip extension allowed."].Value });
                        }

                        if (boardInputViewModel.UploadedFile.Length is not (> 0 and <= 10485760)) // 10MB 이하만 가능
                        {
                            return Json(new
                            {
                                result = false,
                                error = _localizer["uploaded file size must be smaller than 10MB."].Value
                            });
                        }
                    }

                    try
                    {
                        bool isAccountSessionExist = HttpContext.Session.TryGetValue(
                            Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                                .Utilities.Session.Account), out byte[] resultByte);

                        if (isAccountSessionExist)
                        {
                            Account loginedAccount =
                                JsonConvert.DeserializeObject<Account>(
                                    Encoding.Default.GetString(resultByte)); // Get Session

                            IEnumerable<Board> privateNoteBoards =
                                await _boardRepository.GetOneTypeBoardsAsync(
                                    EnumHelper.GetDescription(BoardType.PrivateNote));
                            privateNoteBoards = privateNoteBoards.Where(m => m.Writer == loginedAccount.Nickname)
                                .Where(m => m.Deleted == false).OrderByDescending(a => a.Id);

                            Board tempPrivateNoteBoard = privateNoteBoards.Where(a => a.Id == boardInputViewModel.Id)
                                .FirstOrDefault();

                            if (tempPrivateNoteBoard.Writer != loginedAccount.Nickname)
                            {
                                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                            }

                            tempPrivateNoteBoard.Title = boardInputViewModel.Title;
                            tempPrivateNoteBoard.Content = boardInputViewModel.Content;
                            //tempPrivateNoteBoard.Locked = boardInputViewModel.Locked;
                            tempPrivateNoteBoard.Updated = DateTime.UtcNow;

                            await _boardRepository.UpdateBoardAsync(tempPrivateNoteBoard);

                            List<BoardAttachedFile> boardAttachedFiles =
                                await _boardAttachedFileRepository?.GetAllAsync();
                            BoardAttachedFile previousBoardAttachedFile =
                                boardAttachedFiles?.FirstOrDefault(m => m.BoardId == boardInputViewModel.Id);

                            #region 첨부 파일 저장

                            #region 기존 첨부 파일 O && 신 첨부 파일 O

                            if (previousBoardAttachedFile != null && boardInputViewModel.UploadedFile != null)
                            {
                                if (boardInputViewModel.UploadedFile
                                        .Length is
                                    > 0 and <= 10485760) // boardInputViewModel.UploadedFile Length : 파일 크기 0MB 이상, 10MB 이하
                                {
                                    int createdBoardId = boardInputViewModel.Id; // 첨부파일 FK 해서 로직 추가할 것

                                    string boardAttachedFilePath = Path.Combine("upload", "Management",
                                        EnumHelper.GetDescription(BoardType.PrivateNote), "boardAttachedFiles",
                                        $"{createdBoardId}");
                                    if (!Directory.Exists(boardAttachedFilePath))
                                    {
                                        _ = Directory.CreateDirectory(boardAttachedFilePath);
                                    }

                                    string guid = Guid.NewGuid().ToString().ToUpper();
                                    string attachedFile =
                                        $"{guid}{Path.GetExtension(boardInputViewModel.UploadedFile.FileName)}";
                                    string filePath = Path.Combine(boardAttachedFilePath, attachedFile);

                                    _ = await _fileRepository.UploadAsync(boardInputViewModel.UploadedFile, filePath);

                                    previousBoardAttachedFile.Size =
                                        Convert.ToInt32(boardInputViewModel.UploadedFile.Length);
                                    previousBoardAttachedFile.Name =
                                        Path.GetFileNameWithoutExtension(boardInputViewModel.UploadedFile.FileName);
                                    previousBoardAttachedFile.Extension =
                                        Path.GetExtension(boardInputViewModel.UploadedFile.FileName);
                                    previousBoardAttachedFile.Path =
                                        $"upload/Management/{EnumHelper.GetDescription(BoardType.PrivateNote)}/boardAttachedFiles/{createdBoardId}/{attachedFile}";

                                    await _boardAttachedFileRepository.UpdateBoardAttachedFileAsync(
                                        previousBoardAttachedFile);

                                    return Json(new
                                    {
                                        result = true,
                                        message = _localizer["The board has been successfully updated."].Value
                                    });
                                }
                                else
                                {
                                    return Ok(new
                                    {
                                        result = false,
                                        errorMessage = _localizer["File Size must be smaller than 10MB."].Value
                                    });
                                }
                            }

                            #endregion

                            #region 기존 첨부 파일 O && 신 첨부 파일 X

                            else if (previousBoardAttachedFile != null && boardInputViewModel.UploadedFile == null)
                            {
                                previousBoardAttachedFile.Size = 0;
                                previousBoardAttachedFile.Name = "";
                                previousBoardAttachedFile.Extension = "";
                                previousBoardAttachedFile.Path = "";

                                await _boardAttachedFileRepository.UpdateBoardAttachedFileAsync(
                                    previousBoardAttachedFile);

                                return Json(new
                                {
                                    result = true,
                                    message = _localizer["The board has been successfully updated."].Value
                                });
                            }

                            #endregion

                            #region 기존 첨부 파일 X && 신 첨부 파일 O

                            else if (previousBoardAttachedFile == null && boardInputViewModel.UploadedFile != null)
                            {
                                if (boardInputViewModel.UploadedFile
                                        .Length is
                                    > 0 and <= 10485760) // boardInputViewModel.UploadedFile Length : 파일 크기 0MB 이상, 10MB 이하
                                {
                                    int createdBoardId = boardInputViewModel.Id; // 첨부파일 FK 해서 로직 추가할 것

                                    string boardAttachedFilePath = Path.Combine("upload", "Management",
                                        EnumHelper.GetDescription(BoardType.PrivateNote), "boardAttachedFiles",
                                        $"{createdBoardId}");

                                    string guid = Guid.NewGuid().ToString().ToUpper();
                                    string attachedFile =
                                        $"{guid}{Path.GetExtension(boardInputViewModel.UploadedFile.FileName)}";
                                    string filePath = Path.Combine(boardAttachedFilePath, attachedFile);

                                    _ = await _fileRepository.UploadAsync(boardInputViewModel.UploadedFile, filePath);

                                    await _boardAttachedFileRepository.SaveBoardAttachedFileAsync(
                                        new BoardAttachedFile()
                                        {
                                            BoardId = createdBoardId,
                                            Size = Convert.ToInt32(boardInputViewModel.UploadedFile.Length),
                                            Name = Path.GetFileNameWithoutExtension(boardInputViewModel.UploadedFile
                                                .FileName),
                                            Extension = Path.GetExtension(boardInputViewModel.UploadedFile.FileName),
                                            Path =
                                                $"upload/Management/{EnumHelper.GetDescription(BoardType.PrivateNote)}/boardAttachedFiles/{createdBoardId}/{attachedFile}"
                                        });

                                    return Json(new
                                    {
                                        result = true,
                                        message = _localizer["The board has been successfully updated."].Value
                                    });
                                }
                                else
                                {
                                    return Ok(new
                                    {
                                        result = false,
                                        errorMessage = _localizer["File Size must be smaller than 10MB."].Value
                                    });
                                }
                            }

                            #endregion

                            #region 기존 첨부 파일 X && 신 첨부 파일 X

                            else
                            {
                                return Json(new
                                {
                                    result = true,
                                    message = _localizer["The board has been successfully updated."].Value
                                });
                            }

                            #endregion

                            #endregion
                        }
                        else
                        {
                            return Json(new { result = false, error = _localizer["Please Login to edit board"].Value });
                        }
                    }
                    catch
                    {
                        return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                    }
                }
                else
                {
                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                }
            }
            catch
            {
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
            }
        }

        #endregion

        #endregion

        #region Delete

        #region Board

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.Admin)]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> DeleteBoard([FromBody] BoardInputViewModel boardInputViewModel)
        {
            try
            {
                _ = HttpContext.Session.TryGetValue(
                    Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                        .Utilities.Session.Account), out byte[] resultByte);
                Account loginedAccount = await _accountRepository.GetAccountByEmailAsync(JsonConvert
                    .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);

                IEnumerable<Board> privateNoteBoards =
                    await _boardRepository.GetOneTypeBoardsAsync(EnumHelper.GetDescription(BoardType.PrivateNote));
                privateNoteBoards = privateNoteBoards.Where(m => m.Writer == loginedAccount.Nickname)
                    .Where(m => m.Deleted == false).OrderByDescending(a => a.Id);

                if (privateNoteBoards == null)
                {
                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                }
                else
                {
                    Board tempPrivateNoteBoard =
                        privateNoteBoards.Where(a => a.Id == boardInputViewModel.Id).FirstOrDefault();

                    if (tempPrivateNoteBoard == null)
                    {
                        return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                    }
                    else
                    {
                        if (loginedAccount.Nickname == tempPrivateNoteBoard.Writer) // 방문한 사람이 작성자인 경우
                        {
                            #region 게시물 삭제

                            tempPrivateNoteBoard.Deleted = true;
                            await _boardRepository.DeleteBoardAsync(tempPrivateNoteBoard);

                            #endregion

                            return Json(new
                            {
                                result = true, message = _localizer["The board has been successfully deleted."].Value
                            });
                        }
                        else
                        {
                            return Json(new
                                { result = true, message = _localizer["You do not have permission to delete."].Value });
                        }
                    }
                }
            }
            catch
            {
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
            }
        }

        #endregion

        #region BoardComment

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.Admin)]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> DeleteComment(int id)
        {
            try
            {
                BoardComment boardComment = await _boardCommentRepository.GetBoardCommentAsync(id);

                if (boardComment == null)
                {
                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                }
                else
                {
                    _ = HttpContext.Session.TryGetValue(
                        Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                            .Utilities.Session.Account), out byte[] resultByte);
                    Account loginedAccount = await _accountRepository.GetAccountByEmailAsync(JsonConvert
                        .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);

                    if (loginedAccount.Nickname == boardComment.Writer) // 삭제하는 사람이 작성자인 경우
                    {
                        #region 게시물 삭제

                        boardComment.Deleted = true;
                        await _boardCommentRepository.DeleteBoardCommentAsync(boardComment);

                        #endregion

                        return Json(new { result = true });
                    }
                    else
                    {
                        return Json(new
                            { result = true, message = _localizer["You do not have permission to delete."].Value });
                    }
                }
            }
            catch
            {
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
            }
        }

        #endregion

        #endregion

        #endregion
    }
}