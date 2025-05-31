using Maroik.Common.DataAccess.Contracts;
using Maroik.Common.DataAccess.Models;
using Maroik.Common.Miscellaneous.Extensions;
using Maroik.Common.Miscellaneous.Utilities;
using Maroik.WebSite.Models.ViewModels.Notice;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Newtonsoft.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System.Text;
using System.Text.RegularExpressions;

namespace Maroik.WebSite.Controllers
{
    public class NoticeController : Controller
    {
        private readonly IHtmlLocalizer<NoticeController> _localizer;
        private readonly ILogger<NoticeController> _logger;
        private readonly IAccountRepository _accountRepository;
        private readonly IHostEnvironment _hostEnvironment;
        private readonly ICategoryRepository _categoryRepository;
        private readonly ISubCategoryRepository _subCategoryRepository;
        private readonly IAssetRepository _assetRepository;
        private readonly IIncomeRepository _incomeRepository;
        private readonly IExpenditureRepository _expenditureRepository;
        private readonly IFixedIncomeRepository _fixedIncomeRepository;
        private readonly IFixedExpenditureRepository _fixedExpenditureRepository;

        public NoticeController(IHtmlLocalizer<NoticeController> localizer, ILogger<NoticeController> logger,
            IAccountRepository accountRepository, IHostEnvironment hostEnvironment,
            ICategoryRepository categoryRepository, ISubCategoryRepository subCategoryRepository,
            IAssetRepository assetRepository, IIncomeRepository incomeRepository,
            IExpenditureRepository expenditureRepository, IFixedIncomeRepository fixedIncomeRepository,
            IFixedExpenditureRepository fixedExpenditureRepository)
        {
            _localizer = localizer;
            _logger = logger;
            _accountRepository = accountRepository;
            _hostEnvironment = hostEnvironment;
            _categoryRepository = categoryRepository;
            _subCategoryRepository = subCategoryRepository;
            _assetRepository = assetRepository;
            _incomeRepository = incomeRepository;
            _expenditureRepository = expenditureRepository;
            _fixedIncomeRepository = fixedIncomeRepository;
            _fixedExpenditureRepository = fixedExpenditureRepository;
        }

        #region FixedIncome

        #region Create

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> CreateFixedIncome(
            [FromBody] FixedIncomeInputViewModel fixedIncomeInputViewModel)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    try
                    {
                        fixedIncomeInputViewModel.Amount = Math.Abs(fixedIncomeInputViewModel.Amount);
                        _ = HttpContext.Session.TryGetValue(
                            Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                                .Utilities.Session.Account), out byte[] resultByte);

                        #region Validation of MainClass & SubClass Value

                        if (fixedIncomeInputViewModel.MainClass == "RegularIncome")
                        {
                            if (fixedIncomeInputViewModel.SubClass is not ("LaborIncome" or
                                "BusinessIncome" or
                                "PensionIncome" or
                                "FinancialIncome" or
                                "RentalIncome" or
                                "OtherIncome"))
                            {
                                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                            }
                        }
                        else if (fixedIncomeInputViewModel.MainClass == "IrregularIncome")
                        {
                            if (fixedIncomeInputViewModel.SubClass is not ("LaborIncome" or
                                "OtherIncome"))
                            {
                                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                            }
                        }

                        #endregion

                        #region Validate of DepositMonth

                        if (fixedIncomeInputViewModel.DepositMonth is not (>= 1 and <= 12))
                        {
                            return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                        }

                        #endregion

                        #region Validate of DepositDay

                        if (fixedIncomeInputViewModel.DepositMonth is 1 or
                            3 or
                            5 or
                            7 or
                            8 or
                            10 or
                            12)
                        {
                            if (fixedIncomeInputViewModel.DepositDay is not (>= 1 and <= 31))
                            {
                                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                            }
                        }
                        else if (fixedIncomeInputViewModel.DepositMonth == 2)
                        {
                            if (fixedIncomeInputViewModel.DepositDay is not (>= 1 and <= 29))
                            {
                                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                            }
                        }
                        else if (fixedIncomeInputViewModel.DepositMonth is 4 or
                                 6 or
                                 9 or
                                 11)
                        {
                            if (fixedIncomeInputViewModel.DepositDay is not (>= 1 and <= 30))
                            {
                                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                            }
                        }

                        #endregion

                        #region MaturityDate 형식 체크 (윤년 또는 월에 따른 일도 체크함)

                        if (!new Regex(@"^\d{4}-((0[1-9])|(1[012]))-((0[1-9]|[12]\d)|3[01])$").IsMatch(
                                fixedIncomeInputViewModel
                                    .MaturityDate)) // https://stackoverflow.com/questions/5247219/regular-expression-to-detect-yyyy-mm-dd/15233484
                        {
                            return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                        }

                        #endregion

                        #region 고정 수입 생성

                        await _fixedIncomeRepository.CreateFixedIncomeAsync
                        (
                            new FixedIncome()
                            {
                                AccountEmail = JsonConvert
                                    .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email,
                                MainClass = fixedIncomeInputViewModel.MainClass,
                                SubClass = fixedIncomeInputViewModel.SubClass,
                                Content = fixedIncomeInputViewModel.Content ?? "",
                                Amount = Math.Abs(fixedIncomeInputViewModel.Amount),
                                DepositMyAssetProductName = fixedIncomeInputViewModel.DepositMyAssetProductName,
                                DepositMonth = fixedIncomeInputViewModel.DepositMonth,
                                DepositDay = fixedIncomeInputViewModel.DepositDay,
                                MaturityDate = new DateTime(
                                    Convert.ToInt32(Regex.Split(fixedIncomeInputViewModel.MaturityDate, "-")[0]
                                        .ToString()),
                                    Convert.ToInt32(Regex.Split(fixedIncomeInputViewModel.MaturityDate, "-")[1]
                                        .ToString()),
                                    Convert.ToInt32(Regex.Split(fixedIncomeInputViewModel.MaturityDate, "-")[2]
                                        .ToString())),
                                Created = DateTime.UtcNow,
                                Updated = DateTime.UtcNow,
                                Note = fixedIncomeInputViewModel.Note ?? ""
                            }
                        );

                        #endregion

                        return Json(new
                        {
                            result = true, message = _localizer["The fixedIncome has been successfully created."].Value
                        });
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

        #region Read

        [HttpGet]
        public async Task<IActionResult> FixedIncome(string wholeSearch)
        {
            if (HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest") // ajax
            {
                List<FixedIncomeOutputViewModel> fixedIncomeOutputViewModels = [];

                #region FixedIncomes

                _ = HttpContext.Session.TryGetValue(
                    Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                        .Utilities.Session.Account), out byte[] resultByte);

                string loginedAccountTimeZoneIanaId = JsonConvert
                    .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).TimeZoneIanaId;

                DateTime currentDate =
                    new(Convert.ToInt32(Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"), "-")[0]),
                        Convert.ToInt32(Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"), "-")[1]),
                        Convert.ToInt32(Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"), "-")[2]));

                List<Asset> assets = await _assetRepository.GetAssetsAsync(JsonConvert
                    .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);

                foreach (FixedIncome item in await _fixedIncomeRepository.GetFixedIncomesAsync(JsonConvert
                             .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email))
                {
                    if (string.IsNullOrEmpty(wholeSearch))
                    {
                        bool noticedResult = false;

                        try
                        {
                            noticedResult =
                                currentDate.Subtract(new DateTime(
                                    Convert.ToInt32(Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"),
                                        "-")[0]), item.DepositMonth, item.DepositDay)).TotalDays <= 0 && Math.Abs(
                                    currentDate.Subtract(new DateTime(
                                        Convert.ToInt32(
                                            Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"), "-")[0]),
                                        item.DepositMonth, item.DepositDay)).TotalDays) <=
                                ServerSetting.NoticeMaturityDateDay;
                        }
                        catch // 윤년이 아닌데 2월 29일로 초기화 했을 때 예외 발생
                        {
                            noticedResult = false;
                        }

                        if (item.Unpunctuality) // 시간 미엄수 체크 시, 알림 뜨도록 설정
                        {
                            noticedResult = true;
                        }

                        fixedIncomeOutputViewModels.Add(new FixedIncomeOutputViewModel()
                        {
                            Id = item.Id,
                            MainClass = _localizer[item.MainClass.ToString()].Value,
                            SubClass = _localizer[item.SubClass.ToString()].Value,
                            Content = item.Content,
                            Amount = Convert.ToDecimal(item.Amount.TrimTrailingZeros()),
                            MonetaryUnit = assets.Where(x => x.ProductName == item.DepositMyAssetProductName)
                                .FirstOrDefault().MonetaryUnit,
                            DepositMonth = item.DepositMonth,
                            DepositDay = item.DepositDay,
                            MaturityDate = item.MaturityDate.ToString("yyyy-MM-dd"),
                            Note = item.Note,
                            DepositMyAssetProductName = item.DepositMyAssetProductName,
                            Created = item.Created.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId),
                            Updated = item.Updated.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId),
                            Noticed = noticedResult,
                            Unpunctuality = item.Unpunctuality,
                            Expired = item.MaturityDate.Subtract(currentDate).TotalDays < 0
                        });
                    }
                    else
                    {
                        if (_localizer[item.MainClass?.ToString() ?? ""].Value.Contains(wholeSearch) ||
                            _localizer[item.SubClass?.ToString() ?? ""].Value.Contains(wholeSearch) ||
                            (item.Content?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.Amount.TrimTrailingZeros() ?? "").Contains(wholeSearch) ||
                            (assets.Where(x => x.ProductName == item.DepositMyAssetProductName).FirstOrDefault()
                                .MonetaryUnit.ToString() ?? "").Contains(wholeSearch) ||
                            (item.DepositMonth.ToString() ?? "").Contains(wholeSearch) ||
                            (item.DepositDay.ToString() ?? "").Contains(wholeSearch) ||
                            (item.MaturityDate.ToString("yyyy-MM-dd") ?? "").Contains(wholeSearch) ||
                            (item.Note?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.DepositMyAssetProductName?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.Created.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId).ToString() ?? "")
                            .Contains(wholeSearch) ||
                            (item.Updated.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId).ToString() ?? "")
                            .Contains(wholeSearch))
                        {
                            bool noticedResult = false;

                            try
                            {
                                noticedResult =
                                    currentDate.Subtract(new DateTime(
                                        Convert.ToInt32(
                                            Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"), "-")[0]),
                                        item.DepositMonth, item.DepositDay)).TotalDays <= 0 && Math.Abs(currentDate
                                        .Subtract(new DateTime(
                                            Convert.ToInt32(
                                                Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"), "-")
                                                    [0]), item.DepositMonth, item.DepositDay)).TotalDays) <=
                                    ServerSetting.NoticeMaturityDateDay;
                            }
                            catch // 윤년이 아닌데 2월 29일로 초기화 했을 때 예외 발생
                            {
                                noticedResult = false;
                            }

                            if (item.Unpunctuality) // 시간 미엄수 체크 시, 알림 뜨도록 설정
                            {
                                noticedResult = true;
                            }

                            fixedIncomeOutputViewModels.Add(new FixedIncomeOutputViewModel()
                            {
                                Id = item.Id,
                                MainClass = _localizer[item.MainClass.ToString()].Value,
                                SubClass = _localizer[item.SubClass.ToString()].Value,
                                Content = item.Content,
                                Amount = Convert.ToDecimal(item.Amount.TrimTrailingZeros()),
                                MonetaryUnit = assets.Where(x => x.ProductName == item.DepositMyAssetProductName)
                                    .FirstOrDefault().MonetaryUnit,
                                DepositMonth = item.DepositMonth,
                                DepositDay = item.DepositDay,
                                MaturityDate = item.MaturityDate.ToString("yyyy-MM-dd"),
                                Note = item.Note,
                                DepositMyAssetProductName = item.DepositMyAssetProductName,
                                Created = item.Created.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId),
                                Updated = item.Updated.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId),
                                Noticed = noticedResult,
                                Unpunctuality = item.Unpunctuality,
                                Expired = item.MaturityDate.Subtract(currentDate).TotalDays < 0
                            });
                        }
                    }
                }

                #endregion

                IQueryable<FixedIncomeOutputViewModel> result = fixedIncomeOutputViewModels
                    .OrderByDescending(a => a.Expired).ThenByDescending(m => m.Noticed)
                    .ThenByDescending(m => m.Unpunctuality).ThenByDescending(a => a.Created)
                    .ThenByDescending(a => a.Updated).AsQueryable();
                return PartialView("_FixedIncomeGrid", result);
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> IsFixedIncomeExists(int id)
        {
            try
            {
                _ = HttpContext.Session.TryGetValue(
                    Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                        .Utilities.Session.Account), out byte[] resultByte);

                List<FixedIncome> tempFixedIncomes =
                    await _fixedIncomeRepository.GetFixedIncomesAsync(JsonConvert
                        .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);

                if (tempFixedIncomes == null)
                {
                    return Json(new { result = false, error = _localizer["No fixedIncome exists"].Value });
                }
                else
                {
                    FixedIncome tempFixedIncome = tempFixedIncomes.Where(a => a.Id == id).FirstOrDefault();

                    if (tempFixedIncome == null)
                    {
                        return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                    }
                    else
                    {
                        FixedIncomeOutputViewModel tempFixedIncomeOutputModel = new()
                        {
                            Id = tempFixedIncome.Id,
                            MainClass = tempFixedIncome.MainClass,
                            SubClass = tempFixedIncome.SubClass,
                            Content = tempFixedIncome.Content,
                            Amount = Convert.ToDecimal(tempFixedIncome.Amount.TrimTrailingZeros()),
                            DepositMonth = tempFixedIncome.DepositMonth,
                            DepositDay = tempFixedIncome.DepositDay,
                            MaturityDate = tempFixedIncome.MaturityDate.ToString("yyyy-MM-dd"),
                            Note = tempFixedIncome.Note,
                            DepositMyAssetProductName = tempFixedIncome.DepositMyAssetProductName,
                            Unpunctuality = tempFixedIncome.Unpunctuality
                        };
                        return Json(new { result = true, fixedIncome = tempFixedIncomeOutputModel });
                    }
                }
            }
            catch
            {
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> GetFixedIncomeAmountLabel(string productName)
        {
            try
            {
                string label = _localizer["Amount"].Value;

                _ = HttpContext.Session.TryGetValue(
                    Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                        .Utilities.Session.Account), out byte[] resultByte);

                Asset asset =
                    (await _assetRepository.GetAssetsAsync(JsonConvert
                        .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email))
                    .Where(x => !x.Deleted).Where(x => x.ProductName == productName).FirstOrDefault() ?? new Asset();

                if (!string.IsNullOrEmpty(asset.MonetaryUnit))
                {
                    label = label.GetAmountLabel(asset.MonetaryUnit);
                }

                return Json(new { result = true, label });
            }
            catch
            {
                return Json(new { result = false, label = _localizer["Amount"].Value });
            }
        }

        #endregion

        #region Update

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> UpdateFixedIncome(
            [FromBody] FixedIncomeInputViewModel fixedIncomeInputViewModel)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    try
                    {
                        fixedIncomeInputViewModel.Amount = Math.Abs(fixedIncomeInputViewModel.Amount);
                        _ = HttpContext.Session.TryGetValue(
                            Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                                .Utilities.Session.Account), out byte[] resultByte);

                        #region Validation of MainClass & SubClass Value

                        if (fixedIncomeInputViewModel.MainClass == "RegularIncome")
                        {
                            if (fixedIncomeInputViewModel.SubClass is not ("LaborIncome" or
                                "BusinessIncome" or
                                "PensionIncome" or
                                "FinancialIncome" or
                                "RentalIncome" or
                                "OtherIncome"))
                            {
                                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                            }
                        }
                        else if (fixedIncomeInputViewModel.MainClass == "IrregularIncome")
                        {
                            if (fixedIncomeInputViewModel.SubClass is not ("LaborIncome" or
                                "OtherIncome"))
                            {
                                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                            }
                        }

                        #endregion

                        #region Validate of DepositMonth

                        if (fixedIncomeInputViewModel.DepositMonth is not (>= 1 and <= 12))
                        {
                            return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                        }

                        #endregion

                        #region Validate of DepositDay

                        if (fixedIncomeInputViewModel.DepositMonth is 1 or
                            3 or
                            5 or
                            7 or
                            8 or
                            10 or
                            12)
                        {
                            if (fixedIncomeInputViewModel.DepositDay is not (>= 1 and <= 31))
                            {
                                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                            }
                        }
                        else if (fixedIncomeInputViewModel.DepositMonth == 2)
                        {
                            if (fixedIncomeInputViewModel.DepositDay is not (>= 1 and <= 29))
                            {
                                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                            }
                        }
                        else if (fixedIncomeInputViewModel.DepositMonth is 4 or
                                 6 or
                                 9 or
                                 11)
                        {
                            if (fixedIncomeInputViewModel.DepositDay is not (>= 1 and <= 30))
                            {
                                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                            }
                        }

                        #endregion

                        #region MaturityDate 형식 체크 (윤년 또는 월에 따른 일도 체크함)

                        if (!new Regex(@"^\d{4}-((0[1-9])|(1[012]))-((0[1-9]|[12]\d)|3[01])$").IsMatch(
                                fixedIncomeInputViewModel
                                    .MaturityDate)) // https://stackoverflow.com/questions/5247219/regular-expression-to-detect-yyyy-mm-dd/15233484
                        {
                            return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                        }

                        #endregion

                        #region 고정 수입 업데이트

                        List<FixedIncome> tempFixedIncomes =
                            await _fixedIncomeRepository.GetFixedIncomesAsync(JsonConvert
                                .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);

                        if (tempFixedIncomes == null)
                        {
                            return Json(new { result = false, error = _localizer["No fixedIncome exists"].Value });
                        }
                        else
                        {
                            FixedIncome tempFixedIncome = tempFixedIncomes
                                .Where(a => a.Id == fixedIncomeInputViewModel.Id).FirstOrDefault();

                            if (tempFixedIncome == null)
                            {
                                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                            }
                            else
                            {
                                #region 고정 수입 업데이트

                                tempFixedIncome.MainClass = fixedIncomeInputViewModel.MainClass;
                                tempFixedIncome.SubClass = fixedIncomeInputViewModel.SubClass;
                                tempFixedIncome.Content = fixedIncomeInputViewModel.Content ?? "";
                                tempFixedIncome.Amount = Math.Abs(fixedIncomeInputViewModel.Amount);
                                tempFixedIncome.DepositMyAssetProductName =
                                    fixedIncomeInputViewModel.DepositMyAssetProductName;
                                tempFixedIncome.DepositMonth = fixedIncomeInputViewModel.DepositMonth;
                                tempFixedIncome.DepositDay = fixedIncomeInputViewModel.DepositDay;
                                tempFixedIncome.MaturityDate = new DateTime(
                                    Convert.ToInt32(Regex.Split(fixedIncomeInputViewModel.MaturityDate, "-")[0]
                                        .ToString()),
                                    Convert.ToInt32(Regex.Split(fixedIncomeInputViewModel.MaturityDate, "-")[1]
                                        .ToString()),
                                    Convert.ToInt32(Regex.Split(fixedIncomeInputViewModel.MaturityDate, "-")[2]
                                        .ToString()));
                                tempFixedIncome.Updated = DateTime.UtcNow;
                                tempFixedIncome.Note = fixedIncomeInputViewModel.Note ?? "";
                                tempFixedIncome.Unpunctuality = fixedIncomeInputViewModel.Unpunctuality;

                                await _fixedIncomeRepository.UpdateFixedIncomeAsync(tempFixedIncome);

                                #endregion

                                return Json(new
                                {
                                    result = true,
                                    message = _localizer["The fixedIncome has been successfully updated."].Value
                                });
                            }
                        }

                        #endregion
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

        #region Delete

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> DeleteFixedIncome(
            [FromBody] FixedIncomeInputViewModel fixedIncomeInputViewModel)
        {
            try
            {
                _ = HttpContext.Session.TryGetValue(
                    Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                        .Utilities.Session.Account), out byte[] resultByte);

                List<FixedIncome> tempFixedIncomes =
                    await _fixedIncomeRepository.GetFixedIncomesAsync(JsonConvert
                        .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);

                if (tempFixedIncomes == null)
                {
                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                }
                else
                {
                    FixedIncome tempFixedIncome = tempFixedIncomes.Where(a => a.Id == fixedIncomeInputViewModel.Id)
                        .FirstOrDefault();

                    if (tempFixedIncome == null)
                    {
                        return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                    }
                    else
                    {
                        #region 고정 수입 삭제

                        await _fixedIncomeRepository.DeleteFixedIncomeAsync(tempFixedIncome);

                        #endregion

                        return Json(new
                        {
                            result = true, message = _localizer["The fixedIncome has been successfully deleted."].Value
                        });
                    }
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
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> ExportExcelFixedIncome(string fileName = "")
        {
            await Task.Yield();

            var account = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(HttpContext.Session
                .TryGetValue(
                    Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                        .Utilities.Session.Account), out byte[] resultByte)
                ? resultByte
                : Array.Empty<byte>()));
            string loginedAccountTimeZoneIanaId = account.TimeZoneIanaId;
            DateTime currentDate = DateTime.UtcNow.ToLocalTime();

            List<Asset> assets = await _assetRepository.GetAssetsAsync(account.Email);
            var fixedIncomeList = await _fixedIncomeRepository.GetFixedIncomesAsync(account.Email);

            var fixedIncomeOutputViewModels = fixedIncomeList.Select(item =>
                {
                    bool noticedResult = false;

                    try
                    {
                        var maturityDate = new DateTime(currentDate.Year, item.DepositMonth, item.DepositDay);
                        noticedResult = currentDate.Subtract(maturityDate).TotalDays <= 0 &&
                                        Math.Abs(currentDate.Subtract(maturityDate).TotalDays) <=
                                        ServerSetting.NoticeMaturityDateDay;
                    }
                    catch
                    {
                        noticedResult = false;
                    }

                    if (item.Unpunctuality)
                    {
                        noticedResult = true;
                    }

                    var asset = assets.FirstOrDefault(x => x.ProductName == item.DepositMyAssetProductName);
                    return new FixedIncomeOutputViewModel
                    {
                        MainClass = _localizer[item.MainClass.ToString()].Value,
                        SubClass = _localizer[item.SubClass.ToString()].Value,
                        Content = item.Content,
                        Amount = Convert.ToDecimal(item.Amount.TrimTrailingZeros()),
                        MonetaryUnit = asset?.MonetaryUnit,
                        DepositMonth = item.DepositMonth,
                        DepositDay = item.DepositDay,
                        MaturityDate = item.MaturityDate.ToString("yyyy-MM-dd"),
                        Note = item.Note,
                        DepositMyAssetProductName = item.DepositMyAssetProductName,
                        Created = item.Created.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId),
                        Updated = item.Updated.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId),
                        Noticed = noticedResult,
                        Unpunctuality = item.Unpunctuality,
                        Expired = item.MaturityDate.Subtract(currentDate).TotalDays < 0
                    };
                }).OrderByDescending(a => a.Expired)
                .ThenByDescending(m => m.Noticed)
                .ThenByDescending(m => m.Unpunctuality)
                .ThenByDescending(a => a.Created)
                .ThenByDescending(a => a.Updated)
                .ToList();

            MemoryStream stream = new();

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

                Cell CreateCell(string text)
                {
                    return new Cell
                    {
                        DataType = CellValues.String,
                        CellValue = new CellValue(text)
                    };
                }

                Row headerRow = new Row();
                headerRow.Append(
                    CreateCell(_localizer[nameof(FixedIncomeOutputViewModel.MainClass)].Value),
                    CreateCell(_localizer[nameof(FixedIncomeOutputViewModel.SubClass)].Value),
                    CreateCell(_localizer[nameof(FixedIncomeOutputViewModel.Content)].Value),
                    CreateCell(_localizer[nameof(FixedIncomeOutputViewModel.Amount)].Value),
                    CreateCell(_localizer[nameof(FixedIncomeOutputViewModel.MonetaryUnit)].Value),
                    CreateCell(_localizer[nameof(FixedIncomeOutputViewModel.DepositMonth)].Value),
                    CreateCell(_localizer[nameof(FixedIncomeOutputViewModel.DepositDay)].Value),
                    CreateCell(_localizer[nameof(FixedIncomeOutputViewModel.MaturityDate)].Value),
                    CreateCell(_localizer[nameof(FixedIncomeOutputViewModel.Note)].Value),
                    CreateCell(_localizer[nameof(FixedIncomeOutputViewModel.DepositMyAssetProductName)].Value),
                    CreateCell(_localizer[nameof(FixedIncomeOutputViewModel.Created)].Value),
                    CreateCell(_localizer[nameof(FixedIncomeOutputViewModel.Updated)].Value),
                    CreateCell(_localizer[nameof(FixedIncomeOutputViewModel.Noticed)].Value),
                    CreateCell(_localizer[nameof(FixedIncomeOutputViewModel.Expired)].Value)
                );
                sheetData.Append(headerRow);

                foreach (var item in fixedIncomeOutputViewModels)
                {
                    Row dataRow = new Row();
                    dataRow.Append(
                        CreateCell(item.MainClass),
                        CreateCell(item.SubClass),
                        CreateCell(item.Content),
                        CreateCell(item.Amount.ToString()),
                        CreateCell(item.MonetaryUnit),
                        CreateCell(item.DepositMonth.ToString()),
                        CreateCell(item.DepositDay.ToString()),
                        CreateCell(item.MaturityDate),
                        CreateCell(item.Note),
                        CreateCell(item.DepositMyAssetProductName),
                        CreateCell(item.Created.ToString()),
                        CreateCell(item.Updated.ToString()),
                        CreateCell(item.Noticed.ToString()),
                        CreateCell(item.Expired.ToString())
                    );
                    sheetData.Append(dataRow);
                }

                workbookPart.Workbook.Save();
            }

            stream.Position = 0;
            string excelName =
                $"{fileName}-{DateTime.UtcNow.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId):yyyy-MM-dd-HH-mm-ss-fff}.xlsx";
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", excelName);
        }

        #endregion

        #endregion

        #region FixedExpenditure

        #region Create

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> CreateFixedExpenditure(
            [FromBody] FixedExpenditureInputViewModel fixedExpenditureInputViewModel)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    fixedExpenditureInputViewModel.Amount = Math.Abs(fixedExpenditureInputViewModel.Amount);
                    _ = HttpContext.Session.TryGetValue(
                        Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                            .Utilities.Session.Account), out byte[] resultByte);

                    #region 대분류 = '정기저축'

                    if (fixedExpenditureInputViewModel.MainClass == "RegularSavings")
                    {
                        if (fixedExpenditureInputViewModel.SubClass is "Deposit" or "Investment")
                        {
                            try
                            {
                                if (fixedExpenditureInputViewModel.PaymentMethod ==
                                    fixedExpenditureInputViewModel.MyDepositAsset)
                                {
                                    return Json(new
                                    {
                                        result = false,
                                        error = _localizer[
                                            "The PaymentMethod and MyDepositAsset value cannot be the same."].Value
                                    });
                                }

                                Asset expenditureAsset = await _assetRepository.GetAssetAsync(
                                    JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte))
                                        .Email, fixedExpenditureInputViewModel.PaymentMethod);
                                Asset incomeAsset = await _assetRepository.GetAssetAsync(
                                    JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte))
                                        .Email, fixedExpenditureInputViewModel.MyDepositAsset);

                                if (expenditureAsset == null || incomeAsset == null)
                                {
                                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                                }

                                if (expenditureAsset.MonetaryUnit != incomeAsset.MonetaryUnit)
                                {
                                    return Json(new
                                    {
                                        result = false,
                                        error = _localizer[
                                                "PaymentMethod MonetaryUnit must be same as MyDepositAsset MonetaryUnit."]
                                            .Value
                                    });
                                }

                                #region 고정 지출 생성

                                await _fixedExpenditureRepository.CreateFixedExpenditureAsync
                                (
                                    new FixedExpenditure()
                                    {
                                        AccountEmail = JsonConvert
                                            .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email,
                                        MainClass = fixedExpenditureInputViewModel.MainClass,
                                        SubClass = fixedExpenditureInputViewModel.SubClass,
                                        Content = fixedExpenditureInputViewModel.Content ?? "",
                                        Amount = Math.Abs(fixedExpenditureInputViewModel.Amount),
                                        PaymentMethod = fixedExpenditureInputViewModel.PaymentMethod,
                                        MyDepositAsset = fixedExpenditureInputViewModel.MyDepositAsset,
                                        DepositMonth = fixedExpenditureInputViewModel.DepositMonth,
                                        DepositDay = fixedExpenditureInputViewModel.DepositDay,
                                        MaturityDate = new DateTime(
                                            Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate,
                                                "-")[0].ToString()),
                                            Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate,
                                                "-")[1].ToString()),
                                            Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate,
                                                "-")[2].ToString())),
                                        Created = DateTime.UtcNow,
                                        Updated = DateTime.UtcNow,
                                        Note = fixedExpenditureInputViewModel.Note ?? ""
                                    }
                                );

                                #endregion

                                return Json(new
                                {
                                    result = true,
                                    message = _localizer["The fixedExpenditure has been successfully created."].Value
                                });
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

                    #region 대분류 = '비소비지출'

                    else if (fixedExpenditureInputViewModel.MainClass == "NonConsumerSpending")
                    {
                        if (fixedExpenditureInputViewModel.SubClass is "PublicPension" or "DebtRepayment")
                        {
                            try
                            {
                                if (fixedExpenditureInputViewModel.PaymentMethod ==
                                    fixedExpenditureInputViewModel.MyDepositAsset)
                                {
                                    return Json(new
                                    {
                                        result = false,
                                        error = _localizer[
                                            "The PaymentMethod and MyDepositAsset value cannot be the same."].Value
                                    });
                                }

                                Asset expenditureAsset = await _assetRepository.GetAssetAsync(
                                    JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte))
                                        .Email, fixedExpenditureInputViewModel.PaymentMethod);
                                Asset incomeAsset = await _assetRepository.GetAssetAsync(
                                    JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte))
                                        .Email, fixedExpenditureInputViewModel.MyDepositAsset);

                                if (expenditureAsset == null || incomeAsset == null)
                                {
                                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                                }

                                if (expenditureAsset.MonetaryUnit != incomeAsset.MonetaryUnit)
                                {
                                    return Json(new
                                    {
                                        result = false,
                                        error = _localizer[
                                                "PaymentMethod MonetaryUnit must be same as MyDepositAsset MonetaryUnit."]
                                            .Value
                                    });
                                }

                                #region 고정 지출 생성

                                await _fixedExpenditureRepository.CreateFixedExpenditureAsync
                                (
                                    new FixedExpenditure()
                                    {
                                        AccountEmail = JsonConvert
                                            .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email,
                                        MainClass = fixedExpenditureInputViewModel.MainClass,
                                        SubClass = fixedExpenditureInputViewModel.SubClass,
                                        Content = fixedExpenditureInputViewModel.Content ?? "",
                                        Amount = Math.Abs(fixedExpenditureInputViewModel.Amount),
                                        PaymentMethod = fixedExpenditureInputViewModel.PaymentMethod,
                                        MyDepositAsset = fixedExpenditureInputViewModel.MyDepositAsset,
                                        DepositMonth = fixedExpenditureInputViewModel.DepositMonth,
                                        DepositDay = fixedExpenditureInputViewModel.DepositDay,
                                        MaturityDate = new DateTime(
                                            Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate,
                                                "-")[0].ToString()),
                                            Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate,
                                                "-")[1].ToString()),
                                            Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate,
                                                "-")[2].ToString())),
                                        Created = DateTime.UtcNow,
                                        Updated = DateTime.UtcNow,
                                        Note = fixedExpenditureInputViewModel.Note ?? ""
                                    }
                                );

                                #endregion

                                return Json(new
                                {
                                    result = true,
                                    message = _localizer["The fixedExpenditure has been successfully created."].Value
                                });
                            }
                            catch
                            {
                                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                            }
                        }
                        else if (fixedExpenditureInputViewModel.SubClass is "Tax" or
                                 "SocialInsurance" or
                                 "InterHouseholdTranserExpenses" or
                                 "NonProfitOrganizationTransfer")
                        {
                            try
                            {
                                #region 고정 지출 생성

                                await _fixedExpenditureRepository.CreateFixedExpenditureAsync
                                (
                                    new FixedExpenditure()
                                    {
                                        AccountEmail = JsonConvert
                                            .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email,
                                        MainClass = fixedExpenditureInputViewModel.MainClass,
                                        SubClass = fixedExpenditureInputViewModel.SubClass,
                                        Content = fixedExpenditureInputViewModel.Content ?? "",
                                        Amount = Math.Abs(fixedExpenditureInputViewModel.Amount),
                                        PaymentMethod = fixedExpenditureInputViewModel.PaymentMethod,
                                        MyDepositAsset = "",
                                        DepositMonth = fixedExpenditureInputViewModel.DepositMonth,
                                        DepositDay = fixedExpenditureInputViewModel.DepositDay,
                                        MaturityDate = new DateTime(
                                            Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate,
                                                "-")[0].ToString()),
                                            Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate,
                                                "-")[1].ToString()),
                                            Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate,
                                                "-")[2].ToString())),
                                        Created = DateTime.UtcNow,
                                        Updated = DateTime.UtcNow,
                                        Note = fixedExpenditureInputViewModel.Note ?? ""
                                    }
                                );

                                #endregion

                                return Json(new
                                {
                                    result = true,
                                    message = _localizer["The fixedExpenditure has been successfully created."].Value
                                });
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

                    #region 대분류 = '소비지출'

                    else if (fixedExpenditureInputViewModel.MainClass == "ConsumerSpending")
                    {
                        if (fixedExpenditureInputViewModel.SubClass is "MealOrEatOutExpenses" or
                            "HousingOrSuppliesCost" or
                            "EducationExpenses" or
                            "MedicalExpenses" or
                            "TransportationCost" or
                            "CommunicationCost" or
                            "LeisureOrCulture" or
                            "ClothingOrShoes" or
                            "PinMoney" or
                            "ProtectionTypeInsurance" or
                            "OtherExpenses" or
                            "UnknownExpenditure")
                        {
                            try
                            {
                                #region 고정 지출 생성

                                await _fixedExpenditureRepository.CreateFixedExpenditureAsync
                                (
                                    new FixedExpenditure()
                                    {
                                        AccountEmail = JsonConvert
                                            .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email,
                                        MainClass = fixedExpenditureInputViewModel.MainClass,
                                        SubClass = fixedExpenditureInputViewModel.SubClass,
                                        Content = fixedExpenditureInputViewModel.Content ?? "",
                                        Amount = Math.Abs(fixedExpenditureInputViewModel.Amount),
                                        PaymentMethod = fixedExpenditureInputViewModel.PaymentMethod,
                                        MyDepositAsset = "",
                                        DepositMonth = fixedExpenditureInputViewModel.DepositMonth,
                                        DepositDay = fixedExpenditureInputViewModel.DepositDay,
                                        MaturityDate = new DateTime(
                                            Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate,
                                                "-")[0].ToString()),
                                            Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate,
                                                "-")[1].ToString()),
                                            Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate,
                                                "-")[2].ToString())),
                                        Created = DateTime.UtcNow,
                                        Updated = DateTime.UtcNow,
                                        Note = fixedExpenditureInputViewModel.Note ?? ""
                                    }
                                );

                                #endregion

                                return Json(new
                                {
                                    result = true,
                                    message = _localizer["The fixedExpenditure has been successfully created."].Value
                                });
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

                    #region 대분류에 없는 값

                    else
                    {
                        return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                    }

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
        public async Task<IActionResult> FixedExpenditure(string wholeSearch)
        {
            if (HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest") // ajax
            {
                List<FixedExpenditureOutputViewModel> fixedExpenditureOutputViewModels = [];

                #region FixedExpenditures

                _ = HttpContext.Session.TryGetValue(
                    Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                        .Utilities.Session.Account), out byte[] resultByte);

                string loginedAccountTimeZoneIanaId = JsonConvert
                    .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).TimeZoneIanaId;

                DateTime currentDate =
                    new(Convert.ToInt32(Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"), "-")[0]),
                        Convert.ToInt32(Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"), "-")[1]),
                        Convert.ToInt32(Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"), "-")[2]));

                List<Asset> assets = await _assetRepository.GetAssetsAsync(JsonConvert
                    .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);

                foreach (FixedExpenditure item in await _fixedExpenditureRepository.GetFixedExpendituresAsync(
                             JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email))
                {
                    if (string.IsNullOrEmpty(wholeSearch))
                    {
                        bool noticedResult = false;

                        try
                        {
                            noticedResult =
                                currentDate.Subtract(new DateTime(
                                    Convert.ToInt32(Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"),
                                        "-")[0]), item.DepositMonth, item.DepositDay)).TotalDays <= 0 && Math.Abs(
                                    currentDate.Subtract(new DateTime(
                                        Convert.ToInt32(
                                            Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"), "-")[0]),
                                        item.DepositMonth, item.DepositDay)).TotalDays) <=
                                ServerSetting.NoticeMaturityDateDay;
                        }
                        catch // 윤년이 아닌데 2월 29일로 초기화 했을 때 예외 발생
                        {
                            noticedResult = false;
                        }

                        if (item.Unpunctuality) // 시간 미엄수 체크 시, 알림 뜨도록 설정
                        {
                            noticedResult = true;
                        }

                        fixedExpenditureOutputViewModels.Add(new FixedExpenditureOutputViewModel()
                        {
                            Id = item.Id,
                            MainClass = _localizer[item.MainClass.ToString()].Value,
                            SubClass = _localizer[item.SubClass.ToString()].Value,
                            Content = item.Content,
                            Amount = Convert.ToDecimal(item.Amount.TrimTrailingZeros()),
                            MonetaryUnit = assets.Where(x => x.ProductName == item.PaymentMethod).FirstOrDefault()
                                .MonetaryUnit,
                            PaymentMethod = item.PaymentMethod,
                            MyDepositAsset = item.MyDepositAsset,
                            DepositMonth = item.DepositMonth,
                            DepositDay = item.DepositDay,
                            MaturityDate = item.MaturityDate.ToString("yyyy-MM-dd"),
                            Created = item.Created.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId),
                            Updated = item.Updated.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId),
                            Note = item.Note,
                            Noticed = noticedResult,
                            Unpunctuality = item.Unpunctuality,
                            Expired = item.MaturityDate.Subtract(currentDate).TotalDays < 0
                        });
                    }
                    else
                    {
                        if (_localizer[item.MainClass?.ToString() ?? ""].Value.Contains(wholeSearch) ||
                            _localizer[item.SubClass?.ToString() ?? ""].Value.Contains(wholeSearch) ||
                            (item.Content?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.Amount.TrimTrailingZeros() ?? "").Contains(wholeSearch) ||
                            (assets.Where(x => x.ProductName == item.PaymentMethod).FirstOrDefault().MonetaryUnit
                                .ToString() ?? "").Contains(wholeSearch) ||
                            (item.PaymentMethod.ToString() ?? "").Contains(wholeSearch) ||
                            (item.MyDepositAsset?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.DepositMonth.ToString() ?? "").Contains(wholeSearch) ||
                            (item.DepositDay.ToString() ?? "").Contains(wholeSearch) ||
                            (item.MaturityDate.ToString("yyyy-MM-dd") ?? "").Contains(wholeSearch) ||
                            (item.Created.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId).ToString() ?? "")
                            .Contains(wholeSearch) ||
                            (item.Updated.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId).ToString() ?? "")
                            .Contains(wholeSearch) ||
                            (item.Note?.ToString() ?? "").Contains(wholeSearch))
                        {
                            bool noticedResult = false;

                            try
                            {
                                noticedResult =
                                    currentDate.Subtract(new DateTime(
                                        Convert.ToInt32(
                                            Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"), "-")[0]),
                                        item.DepositMonth, item.DepositDay)).TotalDays <= 0 && Math.Abs(currentDate
                                        .Subtract(new DateTime(
                                            Convert.ToInt32(
                                                Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"), "-")
                                                    [0]), item.DepositMonth, item.DepositDay)).TotalDays) <=
                                    ServerSetting.NoticeMaturityDateDay;
                            }
                            catch // 윤년이 아닌데 2월 29일로 초기화 했을 때 예외 발생
                            {
                                noticedResult = false;
                            }

                            if (item.Unpunctuality) // 시간 미엄수 체크 시, 알림 뜨도록 설정
                            {
                                noticedResult = true;
                            }

                            fixedExpenditureOutputViewModels.Add(new FixedExpenditureOutputViewModel()
                            {
                                Id = item.Id,
                                MainClass = _localizer[item.MainClass.ToString()].Value,
                                SubClass = _localizer[item.SubClass.ToString()].Value,
                                Content = item.Content,
                                Amount = Convert.ToDecimal(item.Amount.TrimTrailingZeros()),
                                MonetaryUnit = assets.Where(x => x.ProductName == item.PaymentMethod).FirstOrDefault()
                                    .MonetaryUnit,
                                PaymentMethod = item.PaymentMethod,
                                MyDepositAsset = item.MyDepositAsset,
                                DepositMonth = item.DepositMonth,
                                DepositDay = item.DepositDay,
                                MaturityDate = item.MaturityDate.ToString("yyyy-MM-dd"),
                                Created = item.Created.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId),
                                Updated = item.Updated.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId),
                                Note = item.Note,
                                Noticed = noticedResult,
                                Unpunctuality = item.Unpunctuality,
                                Expired = item.MaturityDate.Subtract(currentDate).TotalDays < 0
                            });
                        }
                    }
                }

                #endregion

                IQueryable<FixedExpenditureOutputViewModel> result = fixedExpenditureOutputViewModels
                    .OrderByDescending(a => a.Expired).ThenByDescending(m => m.Noticed)
                    .ThenByDescending(m => m.Unpunctuality).ThenByDescending(a => a.Created)
                    .ThenByDescending(a => a.Updated).AsQueryable();
                return PartialView("_FixedExpenditureGrid", result);
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> IsFixedExpenditureExists(int id)
        {
            try
            {
                _ = HttpContext.Session.TryGetValue(
                    Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                        .Utilities.Session.Account), out byte[] resultByte);

                List<FixedExpenditure> tempFixedExpenditures =
                    await _fixedExpenditureRepository.GetFixedExpendituresAsync(JsonConvert
                        .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);

                if (tempFixedExpenditures == null)
                {
                    return Json(new { result = false, error = _localizer["No fixedExpenditure exists"].Value });
                }
                else
                {
                    FixedExpenditure tempFixedExpenditure =
                        tempFixedExpenditures.Where(a => a.Id == id).FirstOrDefault();

                    if (tempFixedExpenditure == null)
                    {
                        return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                    }
                    else
                    {
                        FixedExpenditureOutputViewModel tempFixedIncomeOutputModel = new()
                        {
                            Id = tempFixedExpenditure.Id,
                            MainClass = tempFixedExpenditure.MainClass,
                            SubClass = tempFixedExpenditure.SubClass,
                            Content = tempFixedExpenditure.Content,
                            Amount = Convert.ToDecimal(tempFixedExpenditure.Amount.TrimTrailingZeros()),
                            DepositMonth = tempFixedExpenditure.DepositMonth,
                            DepositDay = tempFixedExpenditure.DepositDay,
                            MaturityDate = tempFixedExpenditure.MaturityDate.ToString("yyyy-MM-dd"),
                            Note = tempFixedExpenditure.Note,
                            PaymentMethod = tempFixedExpenditure.PaymentMethod,
                            MyDepositAsset = tempFixedExpenditure.MyDepositAsset ?? "",
                            Unpunctuality = tempFixedExpenditure.Unpunctuality
                        };
                        return Json(new { result = true, fixedExpenditure = tempFixedIncomeOutputModel });
                    }
                }
            }
            catch
            {
                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> GetFixedExpenditureAmountLabel(string productName)
        {
            try
            {
                string label = _localizer["Amount"].Value;

                _ = HttpContext.Session.TryGetValue(
                    Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                        .Utilities.Session.Account), out byte[] resultByte);

                Asset asset =
                    (await _assetRepository.GetAssetsAsync(JsonConvert
                        .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email))
                    .Where(x => !x.Deleted).Where(x => x.ProductName == productName).FirstOrDefault() ?? new Asset();

                if (!string.IsNullOrEmpty(asset.MonetaryUnit))
                {
                    label = label.GetAmountLabel(asset.MonetaryUnit);
                }

                return Json(new { result = true, label });
            }
            catch
            {
                return Json(new { result = false, label = _localizer["Amount"].Value });
            }
        }

        #endregion

        #region Update

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> UpdateFixedExpenditure(
            [FromBody] FixedExpenditureInputViewModel fixedExpenditureInputViewModel)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    fixedExpenditureInputViewModel.Amount = Math.Abs(fixedExpenditureInputViewModel.Amount);
                    _ = HttpContext.Session.TryGetValue(
                        Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                            .Utilities.Session.Account), out byte[] resultByte);

                    #region 대분류 = '정기저축'

                    if (fixedExpenditureInputViewModel.MainClass == "RegularSavings")
                    {
                        if (fixedExpenditureInputViewModel.SubClass is "Deposit" or "Investment")
                        {
                            try
                            {
                                if (fixedExpenditureInputViewModel.PaymentMethod ==
                                    fixedExpenditureInputViewModel.MyDepositAsset)
                                {
                                    return Json(new
                                    {
                                        result = false,
                                        error = _localizer[
                                            "The PaymentMethod and MyDepositAsset value cannot be the same."].Value
                                    });
                                }

                                Asset expenditureAsset = await _assetRepository.GetAssetAsync(
                                    JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte))
                                        .Email, fixedExpenditureInputViewModel.PaymentMethod);
                                Asset incomeAsset = await _assetRepository.GetAssetAsync(
                                    JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte))
                                        .Email, fixedExpenditureInputViewModel.MyDepositAsset);

                                if (expenditureAsset == null || incomeAsset == null)
                                {
                                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                                }

                                if (expenditureAsset.MonetaryUnit != incomeAsset.MonetaryUnit)
                                {
                                    return Json(new
                                    {
                                        result = false,
                                        error = _localizer[
                                                "PaymentMethod MonetaryUnit must be same as MyDepositAsset MonetaryUnit."]
                                            .Value
                                    });
                                }

                                List<FixedExpenditure> tempFixedExpenditures =
                                    await _fixedExpenditureRepository.GetFixedExpendituresAsync(JsonConvert
                                        .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);
                                FixedExpenditure tempFixedExpenditure = tempFixedExpenditures
                                    .Where(a => a.Id == fixedExpenditureInputViewModel.Id).FirstOrDefault();

                                if (tempFixedExpenditure == null)
                                {
                                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                                }

                                tempFixedExpenditure.Id = fixedExpenditureInputViewModel.Id;
                                tempFixedExpenditure.AccountEmail = JsonConvert
                                    .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email;
                                tempFixedExpenditure.MainClass = fixedExpenditureInputViewModel.MainClass;
                                tempFixedExpenditure.SubClass = fixedExpenditureInputViewModel.SubClass;
                                tempFixedExpenditure.Content = fixedExpenditureInputViewModel.Content ?? "";
                                tempFixedExpenditure.Amount = fixedExpenditureInputViewModel.Amount;
                                tempFixedExpenditure.DepositMonth = fixedExpenditureInputViewModel.DepositMonth;
                                tempFixedExpenditure.DepositDay = fixedExpenditureInputViewModel.DepositDay;
                                tempFixedExpenditure.MaturityDate = new DateTime(
                                    Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate, "-")[0]
                                        .ToString()),
                                    Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate, "-")[1]
                                        .ToString()),
                                    Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate, "-")[2]
                                        .ToString()));
                                tempFixedExpenditure.Note = fixedExpenditureInputViewModel?.Note ?? "";
                                tempFixedExpenditure.PaymentMethod = fixedExpenditureInputViewModel.PaymentMethod;
                                tempFixedExpenditure.MyDepositAsset =
                                    fixedExpenditureInputViewModel.MyDepositAsset ?? "";
                                tempFixedExpenditure.Updated = DateTime.UtcNow;
                                tempFixedExpenditure.Unpunctuality = fixedExpenditureInputViewModel.Unpunctuality;

                                await _fixedExpenditureRepository.UpdateFixedExpenditureAsync(tempFixedExpenditure);

                                return Json(new
                                {
                                    result = true,
                                    message = _localizer["The fixedExpenditure has been successfully updated."].Value
                                });
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

                    #region 대분류 = '비소비지출'

                    else if (fixedExpenditureInputViewModel.MainClass == "NonConsumerSpending")
                    {
                        if (fixedExpenditureInputViewModel.SubClass is "PublicPension" or "DebtRepayment")
                        {
                            try
                            {
                                if (fixedExpenditureInputViewModel.PaymentMethod ==
                                    fixedExpenditureInputViewModel.MyDepositAsset)
                                {
                                    return Json(new
                                    {
                                        result = false,
                                        error = _localizer[
                                            "The PaymentMethod and MyDepositAsset value cannot be the same."].Value
                                    });
                                }

                                Asset expenditureAsset = await _assetRepository.GetAssetAsync(
                                    JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte))
                                        .Email, fixedExpenditureInputViewModel.PaymentMethod);
                                Asset incomeAsset = await _assetRepository.GetAssetAsync(
                                    JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte))
                                        .Email, fixedExpenditureInputViewModel.MyDepositAsset);

                                if (expenditureAsset == null || incomeAsset == null)
                                {
                                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                                }

                                if (expenditureAsset.MonetaryUnit != incomeAsset.MonetaryUnit)
                                {
                                    return Json(new
                                    {
                                        result = false,
                                        error = _localizer[
                                                "PaymentMethod MonetaryUnit must be same as MyDepositAsset MonetaryUnit."]
                                            .Value
                                    });
                                }

                                List<FixedExpenditure> tempFixedExpenditures =
                                    await _fixedExpenditureRepository.GetFixedExpendituresAsync(JsonConvert
                                        .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);
                                FixedExpenditure tempFixedExpenditure = tempFixedExpenditures
                                    .Where(a => a.Id == fixedExpenditureInputViewModel.Id).FirstOrDefault();

                                if (tempFixedExpenditure == null)
                                {
                                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                                }

                                tempFixedExpenditure.Id = fixedExpenditureInputViewModel.Id;
                                tempFixedExpenditure.AccountEmail = JsonConvert
                                    .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email;
                                tempFixedExpenditure.MainClass = fixedExpenditureInputViewModel.MainClass;
                                tempFixedExpenditure.SubClass = fixedExpenditureInputViewModel.SubClass;
                                tempFixedExpenditure.Content = fixedExpenditureInputViewModel.Content ?? "";
                                tempFixedExpenditure.Amount = fixedExpenditureInputViewModel.Amount;
                                tempFixedExpenditure.DepositMonth = fixedExpenditureInputViewModel.DepositMonth;
                                tempFixedExpenditure.DepositDay = fixedExpenditureInputViewModel.DepositDay;
                                tempFixedExpenditure.MaturityDate = new DateTime(
                                    Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate, "-")[0]
                                        .ToString()),
                                    Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate, "-")[1]
                                        .ToString()),
                                    Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate, "-")[2]
                                        .ToString()));
                                tempFixedExpenditure.Note = fixedExpenditureInputViewModel?.Note ?? "";
                                tempFixedExpenditure.PaymentMethod = fixedExpenditureInputViewModel.PaymentMethod;
                                tempFixedExpenditure.MyDepositAsset =
                                    fixedExpenditureInputViewModel.MyDepositAsset ?? "";
                                tempFixedExpenditure.Updated = DateTime.UtcNow;
                                tempFixedExpenditure.Unpunctuality = fixedExpenditureInputViewModel.Unpunctuality;

                                await _fixedExpenditureRepository.UpdateFixedExpenditureAsync(tempFixedExpenditure);

                                return Json(new
                                {
                                    result = true,
                                    message = _localizer["The fixedExpenditure has been successfully updated."].Value
                                });
                            }
                            catch
                            {
                                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                            }
                        }
                        else if (fixedExpenditureInputViewModel.SubClass is "Tax" or
                                 "SocialInsurance" or
                                 "InterHouseholdTranserExpenses" or
                                 "NonProfitOrganizationTransfer")
                        {
                            try
                            {
                                List<FixedExpenditure> tempFixedExpenditures =
                                    await _fixedExpenditureRepository.GetFixedExpendituresAsync(JsonConvert
                                        .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);
                                FixedExpenditure tempFixedExpenditure = tempFixedExpenditures
                                    .Where(a => a.Id == fixedExpenditureInputViewModel.Id).FirstOrDefault();

                                if (tempFixedExpenditure == null)
                                {
                                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                                }

                                tempFixedExpenditure.Id = fixedExpenditureInputViewModel.Id;
                                tempFixedExpenditure.AccountEmail = JsonConvert
                                    .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email;
                                tempFixedExpenditure.MainClass = fixedExpenditureInputViewModel.MainClass;
                                tempFixedExpenditure.SubClass = fixedExpenditureInputViewModel.SubClass;
                                tempFixedExpenditure.Content = fixedExpenditureInputViewModel.Content ?? "";
                                tempFixedExpenditure.Amount = fixedExpenditureInputViewModel.Amount;
                                tempFixedExpenditure.DepositMonth = fixedExpenditureInputViewModel.DepositMonth;
                                tempFixedExpenditure.DepositDay = fixedExpenditureInputViewModel.DepositDay;
                                tempFixedExpenditure.MaturityDate = new DateTime(
                                    Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate, "-")[0]
                                        .ToString()),
                                    Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate, "-")[1]
                                        .ToString()),
                                    Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate, "-")[2]
                                        .ToString()));
                                tempFixedExpenditure.Note = fixedExpenditureInputViewModel?.Note ?? "";
                                tempFixedExpenditure.PaymentMethod = fixedExpenditureInputViewModel.PaymentMethod;
                                tempFixedExpenditure.MyDepositAsset = "";
                                tempFixedExpenditure.Updated = DateTime.UtcNow;
                                tempFixedExpenditure.Unpunctuality = fixedExpenditureInputViewModel.Unpunctuality;

                                await _fixedExpenditureRepository.UpdateFixedExpenditureAsync(tempFixedExpenditure);

                                return Json(new
                                {
                                    result = true,
                                    message = _localizer["The fixedExpenditure has been successfully updated."].Value
                                });
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

                    #region 대분류 = '소비지출'

                    else if (fixedExpenditureInputViewModel.MainClass == "ConsumerSpending")
                    {
                        if (fixedExpenditureInputViewModel.SubClass is "MealOrEatOutExpenses" or
                            "HousingOrSuppliesCost" or
                            "EducationExpenses" or
                            "MedicalExpenses" or
                            "TransportationCost" or
                            "CommunicationCost" or
                            "LeisureOrCulture" or
                            "ClothingOrShoes" or
                            "PinMoney" or
                            "ProtectionTypeInsurance" or
                            "OtherExpenses" or
                            "UnknownExpenditure")
                        {
                            try
                            {
                                List<FixedExpenditure> tempFixedExpenditures =
                                    await _fixedExpenditureRepository.GetFixedExpendituresAsync(JsonConvert
                                        .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);
                                FixedExpenditure tempFixedExpenditure = tempFixedExpenditures
                                    .Where(a => a.Id == fixedExpenditureInputViewModel.Id).FirstOrDefault();

                                if (tempFixedExpenditure == null)
                                {
                                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                                }

                                tempFixedExpenditure.Id = fixedExpenditureInputViewModel.Id;
                                tempFixedExpenditure.AccountEmail = JsonConvert
                                    .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email;
                                tempFixedExpenditure.MainClass = fixedExpenditureInputViewModel.MainClass;
                                tempFixedExpenditure.SubClass = fixedExpenditureInputViewModel.SubClass;
                                tempFixedExpenditure.Content = fixedExpenditureInputViewModel.Content ?? "";
                                tempFixedExpenditure.Amount = fixedExpenditureInputViewModel.Amount;
                                tempFixedExpenditure.DepositMonth = fixedExpenditureInputViewModel.DepositMonth;
                                tempFixedExpenditure.DepositDay = fixedExpenditureInputViewModel.DepositDay;
                                tempFixedExpenditure.MaturityDate = new DateTime(
                                    Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate, "-")[0]
                                        .ToString()),
                                    Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate, "-")[1]
                                        .ToString()),
                                    Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate, "-")[2]
                                        .ToString()));
                                tempFixedExpenditure.Note = fixedExpenditureInputViewModel?.Note ?? "";
                                tempFixedExpenditure.PaymentMethod = fixedExpenditureInputViewModel.PaymentMethod;
                                tempFixedExpenditure.MyDepositAsset = "";
                                tempFixedExpenditure.Updated = DateTime.UtcNow;
                                tempFixedExpenditure.Unpunctuality = fixedExpenditureInputViewModel.Unpunctuality;

                                await _fixedExpenditureRepository.UpdateFixedExpenditureAsync(tempFixedExpenditure);

                                return Json(new
                                {
                                    result = true,
                                    message = _localizer["The fixedExpenditure has been successfully updated."].Value
                                });
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

                    #region 대분류에 없는 값

                    else
                    {
                        return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                    }

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

        #region Delete

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> DeleteFixedExpenditure(
            [FromBody] FixedExpenditureInputViewModel fixedExpenditureInputViewModel)
        {
            try
            {
                _ = HttpContext.Session.TryGetValue(
                    Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                        .Utilities.Session.Account), out byte[] resultByte);

                List<FixedExpenditure> tempFixedExpenditures =
                    await _fixedExpenditureRepository.GetFixedExpendituresAsync(JsonConvert
                        .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);

                if (tempFixedExpenditures == null)
                {
                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                }
                else
                {
                    FixedExpenditure tempFixedExpenditure = tempFixedExpenditures
                        .Where(a => a.Id == fixedExpenditureInputViewModel.Id).FirstOrDefault();
                    if (tempFixedExpenditure == null)
                    {
                        return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                    }
                    else
                    {
                        await _fixedExpenditureRepository.DeleteFixedExpenditureAsync(tempFixedExpenditure);
                        return Json(new
                        {
                            result = true,
                            message = _localizer["The fixedExpenditure has been successfully deleted."].Value
                        });
                    }
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
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> ExportExcelFixedExpenditure(string fileName = "")
        {
            await Task.Yield();

            List<FixedExpenditureOutputViewModel> fixedExpenditureOutputViewModels =
                new List<FixedExpenditureOutputViewModel>();

            #region FixedExpenditures

            _ = HttpContext.Session.TryGetValue(
                Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous.Utilities
                    .Session.Account), out byte[] resultByte);

            string loginedAccountTimeZoneIanaId = JsonConvert
                .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).TimeZoneIanaId;

            DateTime currentDate =
                new(Convert.ToInt32(Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"), "-")[0]),
                    Convert.ToInt32(Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"), "-")[1]),
                    Convert.ToInt32(Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"), "-")[2]));

            List<Asset> assets = await _assetRepository.GetAssetsAsync(JsonConvert
                .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);

            foreach (FixedExpenditure item in await _fixedExpenditureRepository.GetFixedExpendituresAsync(JsonConvert
                         .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email))
            {
                bool noticedResult = false;

                try
                {
                    noticedResult =
                        currentDate.Subtract(new DateTime(
                            Convert.ToInt32(Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"), "-")[0]),
                            item.DepositMonth, item.DepositDay)).TotalDays <= 0 && Math.Abs(currentDate
                            .Subtract(new DateTime(
                                Convert.ToInt32(
                                    Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"), "-")[0]),
                                item.DepositMonth, item.DepositDay)).TotalDays) <= ServerSetting.NoticeMaturityDateDay;
                }
                catch // 윤년이 아닌데 2월 29일로 초기화 했을 때 예외 발생
                {
                    noticedResult = false;
                }

                if (item.Unpunctuality) // 시간 미엄수 체크 시, 알림 뜨도록 설정
                {
                    noticedResult = true;
                }

                fixedExpenditureOutputViewModels.Add(new FixedExpenditureOutputViewModel()
                {
                    MainClass = _localizer[item.MainClass.ToString()].Value,
                    SubClass = _localizer[item.SubClass.ToString()].Value,
                    Content = item.Content,
                    Amount = Convert.ToDecimal(item.Amount.TrimTrailingZeros()),
                    MonetaryUnit = assets.Where(x => x.ProductName == item.PaymentMethod).FirstOrDefault().MonetaryUnit,
                    PaymentMethod = item.PaymentMethod,
                    MyDepositAsset = item.MyDepositAsset,
                    DepositMonth = item.DepositMonth,
                    DepositDay = item.DepositDay,
                    MaturityDate = item.MaturityDate.ToString("yyyy-MM-dd"),
                    Created = item.Created.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId),
                    Updated = item.Updated.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId),
                    Note = item.Note,
                    Noticed = noticedResult,
                    Unpunctuality = item.Unpunctuality,
                    Expired = item.MaturityDate.Subtract(currentDate).TotalDays < 0
                });
            }

            fixedExpenditureOutputViewModels = fixedExpenditureOutputViewModels.OrderByDescending(a => a.Expired)
                .ThenByDescending(m => m.Noticed).ThenByDescending(m => m.Unpunctuality)
                .ThenByDescending(a => a.Created).ThenByDescending(a => a.Updated).ToList();

            #endregion

            MemoryStream stream = new();

            using (var spreadsheetDocument = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
            {
                var workbookPart = spreadsheetDocument.AddWorkbookPart();
                workbookPart.Workbook = new Workbook();
                var sheets = workbookPart.Workbook.AppendChild(new Sheets());

                var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                Worksheet worksheet = new Worksheet();
                SheetData sheetData = new SheetData();
                worksheet.Append(sheetData);
                worksheetPart.Worksheet = worksheet;

                Sheet sheet = new Sheet()
                {
                    Id = workbookPart.GetIdOfPart(worksheetPart),
                    SheetId = 1,
                    Name = "Sheet1"
                };
                sheets.Append(sheet);

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
                    CreateCell(_localizer[nameof(FixedExpenditureOutputViewModel.MainClass)].Value),
                    CreateCell(_localizer[nameof(FixedExpenditureOutputViewModel.SubClass)].Value),
                    CreateCell(_localizer[nameof(FixedExpenditureOutputViewModel.Content)].Value),
                    CreateCell(_localizer[nameof(FixedExpenditureOutputViewModel.Amount)].Value),
                    CreateCell(_localizer[nameof(FixedExpenditureOutputViewModel.MonetaryUnit)].Value),
                    CreateCell(_localizer[nameof(FixedExpenditureOutputViewModel.PaymentMethod)].Value),
                    CreateCell(_localizer[nameof(FixedExpenditureOutputViewModel.MyDepositAsset)].Value),
                    CreateCell(_localizer[nameof(FixedExpenditureOutputViewModel.DepositMonth)].Value),
                    CreateCell(_localizer[nameof(FixedExpenditureOutputViewModel.DepositDay)].Value),
                    CreateCell(_localizer[nameof(FixedExpenditureOutputViewModel.MaturityDate)].Value),
                    CreateCell(_localizer[nameof(FixedExpenditureOutputViewModel.Created)].Value),
                    CreateCell(_localizer[nameof(FixedExpenditureOutputViewModel.Updated)].Value),
                    CreateCell(_localizer[nameof(FixedExpenditureOutputViewModel.Note)].Value),
                    CreateCell(_localizer[nameof(FixedExpenditureOutputViewModel.Noticed)].Value),
                    CreateCell(_localizer[nameof(FixedExpenditureOutputViewModel.Expired)].Value)
                );
                sheetData.Append(headerRow);

                foreach (var item in fixedExpenditureOutputViewModels)
                {
                    Row dataRow = new Row();
                    dataRow.Append(
                        CreateCell(_localizer[item?.MainClass?.ToString() ?? ""].Value),
                        CreateCell(_localizer[item?.SubClass?.ToString() ?? ""].Value),
                        CreateCell(item.Content),
                        CreateCell(Convert.ToDecimal(item.Amount.TrimTrailingZeros()).ToString()),
                        CreateCell(item.MonetaryUnit),
                        CreateCell(item.PaymentMethod),
                        CreateCell(item.MyDepositAsset),
                        CreateCell(item.DepositMonth.ToString()),
                        CreateCell(item.DepositDay.ToString()),
                        CreateCell(item.MaturityDate),
                        CreateCell(item.Created.ToString()),
                        CreateCell(item.Updated.ToString()),
                        CreateCell(item.Note),
                        CreateCell(item.Noticed.ToString()),
                        CreateCell(item.Expired.ToString())
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
    }
}