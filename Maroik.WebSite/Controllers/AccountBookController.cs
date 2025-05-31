using Maroik.Common.DataAccess.Contracts;
using Maroik.Common.DataAccess.Models;
using Maroik.Common.Miscellaneous.Extensions;
using Maroik.Common.Miscellaneous.Utilities;
using Maroik.WebSite.Models.ViewModels.AccountBook;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Newtonsoft.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System.Text;

namespace Maroik.WebSite.Controllers
{
    public class AccountBookController : Controller
    {
        private readonly IHtmlLocalizer<AccountBookController> _localizer;
        private readonly ILogger<AccountBookController> _logger;
        private readonly IAccountRepository _accountRepository;
        private readonly IHostEnvironment _hostEnvironment;
        private readonly ICategoryRepository _categoryRepository;
        private readonly ISubCategoryRepository _subCategoryRepository;
        private readonly IAssetRepository _assetRepository;
        private readonly IIncomeRepository _incomeRepository;
        private readonly IExpenditureRepository _expenditureRepository;
        private readonly IFixedExpenditureRepository _fixedExpenditureRepository;

        public AccountBookController(IHtmlLocalizer<AccountBookController> localizer,
            ILogger<AccountBookController> logger, IAccountRepository accountRepository,
            IHostEnvironment hostEnvironment, ICategoryRepository categoryRepository,
            ISubCategoryRepository subCategoryRepository, IAssetRepository assetRepository,
            IIncomeRepository incomeRepository, IExpenditureRepository expenditureRepository,
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
            _fixedExpenditureRepository = fixedExpenditureRepository;
        }

        #region Asset

        #region Create

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> CreateAsset([FromBody] AssetInputViewModel assetInputViewModel)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    _ = HttpContext.Session.TryGetValue(
                        Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                            .Utilities.Session.Account), out byte[] resultByte);

                    if (await _assetRepository.GetAssetAsync(
                            JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email,
                            assetInputViewModel.ProductName) != null)
                    {
                        return Json(new { result = false, error = _localizer["The asset already exists."].Value });
                    }

                    try
                    {
                        #region Validation of item Value

                        if (assetInputViewModel.Item is not ("FreeDepositAndWithdrawal" or
                            "TrustAsset" or
                            "CashAsset" or
                            "SavingsAsset" or
                            "InvestmentAsset" or
                            "RealEstate" or
                            "Movables" or
                            "OtherPhysicalAsset" or
                            "InsuranceAsset"))
                        {
                            return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                        }

                        #endregion

                        await _assetRepository.CreateAssetAsync
                        (
                            new Asset()
                            {
                                ProductName = assetInputViewModel.ProductName,
                                AccountEmail = JsonConvert
                                    .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email,
                                Item = assetInputViewModel.Item,
                                Amount = assetInputViewModel.Amount,
                                MonetaryUnit = assetInputViewModel.MonetaryUnit,
                                Created = DateTime.UtcNow,
                                Updated = DateTime.UtcNow,
                                Note = assetInputViewModel.Note ?? "",
                                Deleted = false
                            }
                        );
                    }
                    catch
                    {
                        return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                    }

                    return Json(new
                        { result = true, message = _localizer["The asset has been successfully created."].Value });
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
        public async Task<IActionResult> Asset(string wholeSearch)
        {
            if (HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest") // ajax
            {
                List<AssetOutputViewModel> assetOutputViewModels = [];

                #region Assets

                _ = HttpContext.Session.TryGetValue(
                    Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                        .Utilities.Session.Account), out byte[] resultByte);

                string loginedAccountTimeZoneIanaId = JsonConvert
                    .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).TimeZoneIanaId;

                foreach (Asset item in await _assetRepository.GetAssetsAsync(JsonConvert
                             .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email))
                {
                    if (string.IsNullOrEmpty(wholeSearch))
                    {
                        assetOutputViewModels.Add(new AssetOutputViewModel()
                        {
                            ProductName = item.ProductName,
                            Item = _localizer[item.Item.ToString()].Value,
                            Amount = Convert.ToDecimal(item.Amount.TrimTrailingZeros()),
                            MonetaryUnit = item.MonetaryUnit,
                            Created = item.Created.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId),
                            Updated = item.Updated.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId),
                            Note = item.Note,
                            Deleted = item.Deleted
                        });
                    }
                    else
                    {
                        if ((item.ProductName?.ToString() ?? "").Contains(wholeSearch) ||
                            _localizer[item.Item?.ToString() ?? ""].Value.Contains(wholeSearch) ||
                            (item.Amount.TrimTrailingZeros() ?? "").Contains(wholeSearch) ||
                            (item.MonetaryUnit?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.Created.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId).ToString() ?? "")
                            .Contains(wholeSearch) ||
                            (item.Updated.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId).ToString() ?? "")
                            .Contains(wholeSearch) ||
                            (item.Note?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.Deleted.ToString() ?? "").Contains(wholeSearch))
                        {
                            assetOutputViewModels.Add(new AssetOutputViewModel()
                            {
                                ProductName = item.ProductName,
                                Item = _localizer[item.Item.ToString()].Value,
                                Amount = Convert.ToDecimal(item.Amount.TrimTrailingZeros()),
                                MonetaryUnit = item.MonetaryUnit,
                                Created = item.Created.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId),
                                Updated = item.Updated.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId),
                                Note = item.Note,
                                Deleted = item.Deleted
                            });
                        }
                    }
                }

                #endregion

                IQueryable<AssetOutputViewModel> result =
                    assetOutputViewModels.OrderBy(m => m.ProductName).AsQueryable();
                return PartialView("_AssetGrid", result);
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> IsAssetExists(string productName)
        {
            try
            {
                _ = HttpContext.Session.TryGetValue(
                    Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                        .Utilities.Session.Account), out byte[] resultByte);

                Asset tempAsset = await _assetRepository.GetAssetAsync(
                    JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, productName);

                return tempAsset == null
                    ? Json(new
                    {
                        result = false, error = _localizer["Fail to find the asset by given product name"].Value
                    })
                    : (IActionResult)Json(new { result = true, asset = tempAsset });
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
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> UpdateAsset([FromBody] AssetInputViewModel assetInputViewModel)
        {
            try
            {
                _ = ModelState.Remove(nameof(assetInputViewModel.MonetaryUnit));

                if (ModelState.IsValid)
                {
                    try
                    {
                        #region Validation of item Value

                        if (assetInputViewModel.Item is not ("FreeDepositAndWithdrawal" or
                            "TrustAsset" or
                            "CashAsset" or
                            "SavingsAsset" or
                            "InvestmentAsset" or
                            "RealEstate" or
                            "Movables" or
                            "OtherPhysicalAsset" or
                            "InsuranceAsset"))
                        {
                            return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                        }

                        #endregion

                        _ = HttpContext.Session.TryGetValue(
                            Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                                .Utilities.Session.Account), out byte[] resultByte);

                        Asset tempAsset = await _assetRepository.GetAssetAsync(
                            JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email,
                            assetInputViewModel.OriginalProductName);

                        if (tempAsset == null)
                        {
                            return Json(new
                            {
                                result = false, error = _localizer["Fail to find the asset by given product name"].Value
                            });
                        }
                        else
                        {
                            tempAsset.ProductName = assetInputViewModel.ProductName;
                            tempAsset.Item = assetInputViewModel.Item;
                            tempAsset.Amount = assetInputViewModel.Amount;
                            tempAsset.MonetaryUnit = assetInputViewModel.MonetaryUnit;
                            tempAsset.Note = assetInputViewModel.Note;
                            tempAsset.Deleted = assetInputViewModel.Deleted;
                            tempAsset.Updated = DateTime.UtcNow;

                            if (await _assetRepository.UpdateAssetWithProductNameAsync(tempAsset,
                                    assetInputViewModel.OriginalProductName) > 0)
                            {
                                _ = await _fixedExpenditureRepository.UpdateMyDepositAssetWithProductNameAsync(
                                    tempAsset.AccountEmail, assetInputViewModel.OriginalProductName,
                                    assetInputViewModel.ProductName);
                                _ = await _expenditureRepository.UpdateMyDepositAssetWithProductNameAsync(
                                    tempAsset.AccountEmail, assetInputViewModel.OriginalProductName,
                                    assetInputViewModel.ProductName);

                                return Json(new
                                {
                                    result = true,
                                    message = _localizer["The asset has been successfully updated."].Value
                                });
                            }
                            else
                            {
                                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                            }
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

        #region Delete

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> DeleteAsset([FromBody] AssetInputViewModel assetInputViewModel)
        {
            try
            {
                _ = HttpContext.Session.TryGetValue(
                    Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                        .Utilities.Session.Account), out byte[] resultByte);

                Asset tempAsset = await _assetRepository.GetAssetAsync(
                    JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email,
                    assetInputViewModel.ProductName);

                if (tempAsset == null)
                {
                    return Json(new
                        { result = false, error = _localizer["Fail to find the asset by given product name"].Value });
                }
                else
                {
                    tempAsset.Deleted = true;
                    await _assetRepository.UpdateAssetAsync(tempAsset);
                    return Json(new
                        { result = true, message = _localizer["The asset has been successfully deleted."].Value });
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
        public async Task<IActionResult> ExportExcelAsset(string fileName = "")
        {
            await Task.Yield();

            List<AssetOutputViewModel> assetOutputViewModels = new List<AssetOutputViewModel>();

            _ = HttpContext.Session.TryGetValue(
                Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous.Utilities
                    .Session.Account), out byte[] resultByte);
            string loginedAccountTimeZoneIanaId = JsonConvert
                .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).TimeZoneIanaId;

            foreach (Asset item in await _assetRepository.GetAssetsAsync(JsonConvert
                         .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email))
            {
                assetOutputViewModels.Add(new AssetOutputViewModel()
                {
                    ProductName = item.ProductName,
                    Item = item.Item,
                    Amount = Convert.ToDecimal(item.Amount.TrimTrailingZeros()),
                    MonetaryUnit = item.MonetaryUnit,
                    Created = item.Created.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId),
                    Updated = item.Updated.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId),
                    Note = item.Note,
                    Deleted = item.Deleted
                });
            }

            assetOutputViewModels = assetOutputViewModels.OrderBy(m => m.ProductName).ToList();

            MemoryStream stream = new MemoryStream();
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
                    CreateCell(_localizer[nameof(AssetOutputViewModel.ProductName)].Value),
                    CreateCell(_localizer[nameof(AssetOutputViewModel.Item)].Value),
                    CreateCell(_localizer[nameof(AssetOutputViewModel.Amount)].Value),
                    CreateCell(_localizer[nameof(AssetOutputViewModel.MonetaryUnit)].Value),
                    CreateCell(_localizer[nameof(AssetOutputViewModel.Created)].Value),
                    CreateCell(_localizer[nameof(AssetOutputViewModel.Updated)].Value),
                    CreateCell(_localizer[nameof(AssetOutputViewModel.Note)].Value),
                    CreateCell(_localizer[nameof(AssetOutputViewModel.Deleted)].Value)
                );
                sheetData.Append(headerRow);

                foreach (var item in assetOutputViewModels)
                {
                    Row dataRow = new Row();
                    dataRow.Append(
                        CreateCell(item.ProductName),
                        CreateCell(_localizer[item?.Item?.ToString() ?? ""].Value),
                        CreateCell(Convert.ToDecimal(item.Amount.TrimTrailingZeros()).ToString()),
                        CreateCell(item.MonetaryUnit),
                        CreateCell(item.Created.ToString()),
                        CreateCell(item.Updated.ToString()),
                        CreateCell(item.Note),
                        CreateCell(item.Deleted.ToString())
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

        #region Income

        #region Create

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> CreateIncome([FromBody] IncomeInputViewModel incomeInputViewModel)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    try
                    {
                        incomeInputViewModel.Amount = Math.Abs(incomeInputViewModel.Amount);
                        _ = HttpContext.Session.TryGetValue(
                            Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                                .Utilities.Session.Account), out byte[] resultByte);

                        #region Validate ProductName's Deletion

                        Asset tempAssetToValidate = await _assetRepository.GetAssetAsync(
                            JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email,
                            incomeInputViewModel.DepositMyAssetProductName);
                        if (tempAssetToValidate.Deleted)
                        {
                            return Json(new
                            {
                                result = false,
                                error = _localizer[
                                    "Actions cannot be executed with assets that have already been deleted."].Value
                            });
                        }

                        #endregion

                        #region Validation of MainClass & SubClass Value

                        if (incomeInputViewModel.MainClass == "RegularIncome")
                        {
                            if (incomeInputViewModel.SubClass is not ("LaborIncome" or
                                "BusinessIncome" or
                                "PensionIncome" or
                                "FinancialIncome" or
                                "RentalIncome" or
                                "OtherIncome"))
                            {
                                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                            }
                        }
                        else if (incomeInputViewModel.MainClass == "IrregularIncome")
                        {
                            if (incomeInputViewModel.SubClass is not ("LaborIncome" or
                                "OtherIncome"))
                            {
                                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                            }
                        }

                        #endregion

                        #region 수입 생성

                        await _incomeRepository.CreateIncomeAsync
                        (
                            new Income()
                            {
                                AccountEmail = JsonConvert
                                    .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email,
                                MainClass = incomeInputViewModel.MainClass,
                                SubClass = incomeInputViewModel.SubClass,
                                Content = incomeInputViewModel.Content ?? "",
                                Amount = Math.Abs(incomeInputViewModel.Amount),
                                DepositMyAssetProductName = incomeInputViewModel.DepositMyAssetProductName,
                                Created = incomeInputViewModel.Created,
                                Updated = DateTime.UtcNow,
                                Note = incomeInputViewModel.Note ?? ""
                            }
                        );

                        #endregion

                        #region 자산 업데이트

                        Asset tempAsset = await _assetRepository.GetAssetAsync(
                            JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email,
                            incomeInputViewModel.DepositMyAssetProductName);
                        tempAsset.Amount += Math.Abs(incomeInputViewModel.Amount);
                        tempAsset.Updated = DateTime.UtcNow;
                        await _assetRepository.UpdateAssetAsync(tempAsset);

                        #endregion

                        return Json(new
                            { result = true, message = _localizer["The income has been successfully created."].Value });
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
        public async Task<IActionResult> Income(string wholeSearch)
        {
            if (HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest") // ajax
            {
                List<IncomeOutputViewModel> incomeOutputViewModels = [];

                #region Incomes

                _ = HttpContext.Session.TryGetValue(
                    Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                        .Utilities.Session.Account), out byte[] resultByte);

                string loginedAccountTimeZoneIanaId = JsonConvert
                    .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).TimeZoneIanaId;

                List<Asset> assets = await _assetRepository.GetAssetsAsync(JsonConvert
                    .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);

                foreach (Income item in await _incomeRepository.GetIncomesAsync(JsonConvert
                             .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email))
                {
                    if (string.IsNullOrEmpty(wholeSearch))
                    {
                        incomeOutputViewModels.Add(new IncomeOutputViewModel()
                        {
                            Id = item.Id,
                            MainClass = _localizer[item.MainClass.ToString()].Value,
                            SubClass = _localizer[item.SubClass.ToString()].Value,
                            Content = item.Content,
                            Amount = Convert.ToDecimal(item.Amount.TrimTrailingZeros()),
                            MonetaryUnit = assets.Where(x => x.ProductName == item.DepositMyAssetProductName)
                                .FirstOrDefault().MonetaryUnit,
                            DepositMyAssetProductName = item.DepositMyAssetProductName,
                            Created = item.Created.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId),
                            Updated = item.Updated.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId),
                            Note = item.Note
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
                            (item.DepositMyAssetProductName?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.Created.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId).ToString() ?? "")
                            .Contains(wholeSearch) ||
                            (item.Updated.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId).ToString() ?? "")
                            .Contains(wholeSearch) ||
                            (item.Note?.ToString() ?? "").Contains(wholeSearch))
                        {
                            incomeOutputViewModels.Add(new IncomeOutputViewModel()
                            {
                                Id = item.Id,
                                MainClass = _localizer[item.MainClass.ToString()].Value,
                                SubClass = _localizer[item.SubClass.ToString()].Value,
                                Content = item.Content,
                                Amount = Convert.ToDecimal(item.Amount.TrimTrailingZeros()),
                                MonetaryUnit = assets.Where(x => x.ProductName == item.DepositMyAssetProductName)
                                    .FirstOrDefault().MonetaryUnit,
                                DepositMyAssetProductName = item.DepositMyAssetProductName,
                                Created = item.Created.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId),
                                Updated = item.Updated.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId),
                                Note = item.Note
                            });
                        }
                    }
                }

                #endregion

                IQueryable<IncomeOutputViewModel> result = incomeOutputViewModels.OrderByDescending(m => m.Created)
                    .ThenByDescending(m => m.Updated).AsQueryable();
                return PartialView("_IncomeGrid", result);
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> IsIncomeExists(int id)
        {
            try
            {
                _ = HttpContext.Session.TryGetValue(
                    Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                        .Utilities.Session.Account), out byte[] resultByte);
                string loginedAccountTimeZoneIanaId = JsonConvert
                    .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).TimeZoneIanaId;
                List<Income> tempIncomes = await _incomeRepository.GetIncomesAsync(JsonConvert
                    .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);

                if (tempIncomes == null)
                {
                    return Json(new { result = false, error = _localizer["No income exists"].Value });
                }
                else
                {
                    Income tempIncome = tempIncomes.Where(a => a.Id == id).FirstOrDefault();

                    if (tempIncome == null)
                    {
                        return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                    }
                    else
                    {
                        tempIncome.Created =
                            tempIncome.Created.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId);
                        return Json(new { result = true, income = tempIncome });
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
        public async Task<IActionResult> GetIncomeAmountLabel(string productName)
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
        public async Task<IActionResult> UpdateIncome([FromBody] IncomeInputViewModel incomeInputViewModel)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    try
                    {
                        incomeInputViewModel.Amount = Math.Abs(incomeInputViewModel.Amount);
                        _ = HttpContext.Session.TryGetValue(
                            Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                                .Utilities.Session.Account), out byte[] resultByte);

                        #region Validation of MainClass & SubClass Value

                        if (incomeInputViewModel.MainClass == "RegularIncome")
                        {
                            if (incomeInputViewModel.SubClass is not ("LaborIncome" or
                                "BusinessIncome" or
                                "PensionIncome" or
                                "FinancialIncome" or
                                "RentalIncome" or
                                "OtherIncome"))
                            {
                                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                            }
                        }
                        else if (incomeInputViewModel.MainClass == "IrregularIncome")
                        {
                            if (incomeInputViewModel.SubClass is not ("LaborIncome" or
                                "OtherIncome"))
                            {
                                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                            }
                        }

                        #endregion

                        #region 수입 & 자산 업데이트

                        List<Income> tempIncomes = await _incomeRepository.GetIncomesAsync(JsonConvert
                            .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);

                        if (tempIncomes == null)
                        {
                            return Json(new { result = false, error = _localizer["No income exists"].Value });
                        }
                        else
                        {
                            Income tempIncome = tempIncomes.Where(a => a.Id == incomeInputViewModel.Id)
                                .FirstOrDefault();

                            if (tempIncome == null)
                            {
                                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                            }
                            else
                            {
                                #region Validate ProductName's Deletion

                                Asset tempAssetToValidate = await _assetRepository.GetAssetAsync(
                                    JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte))
                                        .Email, tempIncome.DepositMyAssetProductName);
                                if (tempAssetToValidate.Deleted)
                                {
                                    return Json(new
                                    {
                                        result = false,
                                        error = _localizer[
                                                "Actions cannot be executed with assets that have already been deleted."]
                                            .Value
                                    });
                                }

                                #endregion

                                string previousTempIncomeDepositMyAssetProductName =
                                    tempIncome.DepositMyAssetProductName;
                                decimal previousTempIncomeAmount = tempIncome.Amount;

                                #region 수입 업데이트

                                tempIncome.MainClass = incomeInputViewModel.MainClass;
                                tempIncome.SubClass = incomeInputViewModel.SubClass;
                                tempIncome.Content = incomeInputViewModel.Content;
                                tempIncome.Amount = incomeInputViewModel.Amount;
                                tempIncome.DepositMyAssetProductName = incomeInputViewModel.DepositMyAssetProductName;
                                tempIncome.Note = incomeInputViewModel.Note;
                                tempIncome.Created = incomeInputViewModel.Created;
                                tempIncome.Updated = DateTime.UtcNow;

                                await _incomeRepository.UpdateIncomeAsync(tempIncome);

                                #endregion

                                #region 자산 업데이트

                                if (tempIncome.DepositMyAssetProductName ==
                                    previousTempIncomeDepositMyAssetProductName) // 같은 경우
                                {
                                    #region 자산 업데이트

                                    Asset tempAsset = await _assetRepository.GetAssetAsync(
                                        JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte))
                                            .Email, tempIncome.DepositMyAssetProductName);
                                    tempAsset.Amount = tempAsset.Amount - Math.Abs(previousTempIncomeAmount) +
                                                       Math.Abs(tempIncome.Amount);
                                    tempAsset.Updated = DateTime.UtcNow;
                                    await _assetRepository.UpdateAssetAsync(tempAsset);

                                    #endregion
                                }
                                else // 다른 경우
                                {
                                    #region 구 자산 업데이트

                                    Asset tempAsset = await _assetRepository.GetAssetAsync(
                                        JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte))
                                            .Email, previousTempIncomeDepositMyAssetProductName);
                                    tempAsset.Amount -= Math.Abs(previousTempIncomeAmount);
                                    tempAsset.Updated = DateTime.UtcNow;
                                    await _assetRepository.UpdateAssetAsync(tempAsset);

                                    #endregion

                                    #region 신 자산 업데이트

                                    tempAsset = await _assetRepository.GetAssetAsync(
                                        JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte))
                                            .Email, tempIncome.DepositMyAssetProductName);
                                    tempAsset.Amount += Math.Abs(tempIncome.Amount);
                                    tempAsset.Updated = DateTime.UtcNow;
                                    await _assetRepository.UpdateAssetAsync(tempAsset);

                                    #endregion
                                }

                                #endregion

                                return Json(new
                                {
                                    result = true,
                                    message = _localizer["The income has been successfully updated."].Value
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
        public async Task<IActionResult> DeleteIncome([FromBody] IncomeInputViewModel incomeInputViewModel)
        {
            try
            {
                _ = HttpContext.Session.TryGetValue(
                    Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                        .Utilities.Session.Account), out byte[] resultByte);

                List<Income> tempIncomes = await _incomeRepository.GetIncomesAsync(JsonConvert
                    .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);

                if (tempIncomes == null)
                {
                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                }
                else
                {
                    Income tempIncome = tempIncomes.Where(a => a.Id == incomeInputViewModel.Id).FirstOrDefault();

                    if (tempIncome == null)
                    {
                        return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                    }
                    else
                    {
                        #region Validate ProductName's Deletion

                        Asset tempAssetToValidate = await _assetRepository.GetAssetAsync(
                            JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email,
                            tempIncome.DepositMyAssetProductName);
                        if (tempAssetToValidate.Deleted)
                        {
                            return Json(new
                            {
                                result = false,
                                error = _localizer[
                                    "Actions cannot be executed with assets that have already been deleted."].Value
                            });
                        }

                        #endregion

                        #region 자산 업데이트

                        Asset tempAsset = await _assetRepository.GetAssetAsync(
                            JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email,
                            tempIncome.DepositMyAssetProductName);
                        tempAsset.Amount -= Math.Abs(tempIncome.Amount);
                        tempAsset.Updated = DateTime.UtcNow;
                        await _assetRepository.UpdateAssetAsync(tempAsset);

                        #endregion

                        #region 수입 삭제

                        await _incomeRepository.DeleteIncomeAsync(tempIncome);

                        #endregion

                        return Json(new
                            { result = true, message = _localizer["The income has been successfully deleted."].Value });
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
        public async Task<IActionResult> ExportExcelIncome(string fileName = "")
        {
            await Task.Yield();

            List<IncomeOutputViewModel> incomeOutputViewModels = new List<IncomeOutputViewModel>();

            #region Incomes

            _ = HttpContext.Session.TryGetValue(
                Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous.Utilities
                    .Session.Account), out byte[] resultByte);
            string loginedAccountTimeZoneIanaId = JsonConvert
                .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).TimeZoneIanaId;

            List<Asset> assets = await _assetRepository.GetAssetsAsync(JsonConvert
                .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);

            foreach (Income item in await _incomeRepository.GetIncomesAsync(JsonConvert
                         .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email))
            {
                incomeOutputViewModels.Add(new IncomeOutputViewModel()
                {
                    MainClass = item.MainClass,
                    SubClass = item.SubClass,
                    Content = item.Content,
                    Amount = Convert.ToDecimal(item.Amount.TrimTrailingZeros()),
                    MonetaryUnit = assets.Where(x => x.ProductName == item.DepositMyAssetProductName).FirstOrDefault()
                        .MonetaryUnit,
                    DepositMyAssetProductName = item.DepositMyAssetProductName,
                    Created = item.Created.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId),
                    Updated = item.Updated.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId),
                    Note = item.Note
                });
            }

            incomeOutputViewModels = incomeOutputViewModels.OrderByDescending(m => m.Created)
                .ThenByDescending(m => m.Updated).ToList();

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
                    CreateCell(_localizer[nameof(IncomeOutputViewModel.MainClass)].Value),
                    CreateCell(_localizer[nameof(IncomeOutputViewModel.SubClass)].Value),
                    CreateCell(_localizer[nameof(IncomeOutputViewModel.Content)].Value),
                    CreateCell(_localizer[nameof(IncomeOutputViewModel.Amount)].Value),
                    CreateCell(_localizer[nameof(IncomeOutputViewModel.MonetaryUnit)].Value),
                    CreateCell(_localizer[nameof(IncomeOutputViewModel.DepositMyAssetProductName)].Value),
                    CreateCell(_localizer[nameof(IncomeOutputViewModel.Created)].Value),
                    CreateCell(_localizer[nameof(IncomeOutputViewModel.Updated)].Value),
                    CreateCell(_localizer[nameof(IncomeOutputViewModel.Note)].Value)
                );
                sheetData.Append(headerRow);

                foreach (var item in incomeOutputViewModels)
                {
                    Row dataRow = new Row();
                    dataRow.Append(
                        CreateCell(_localizer[item?.MainClass?.ToString() ?? ""].Value),
                        CreateCell(_localizer[item?.SubClass?.ToString() ?? ""].Value),
                        CreateCell(item.Content),
                        CreateCell(Convert.ToDecimal(item.Amount.TrimTrailingZeros()).ToString()),
                        CreateCell(item.MonetaryUnit),
                        CreateCell(item.DepositMyAssetProductName),
                        CreateCell(item.Created.ToString()),
                        CreateCell(item.Updated.ToString()),
                        CreateCell(item.Note)
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

        #region Expenditure

        #region Create

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> CreateExpenditure(
            [FromBody] ExpenditureInputViewModel expenditureInputViewModel)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    expenditureInputViewModel.Amount = Math.Abs(expenditureInputViewModel.Amount);
                    _ = HttpContext.Session.TryGetValue(
                        Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                            .Utilities.Session.Account), out byte[] resultByte);

                    #region Validate ProductName's Deletion

                    Asset tempExpenditureAssetToValidate = await _assetRepository.GetAssetAsync(
                        JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email,
                        expenditureInputViewModel.PaymentMethod);
                    if (tempExpenditureAssetToValidate.Deleted)
                    {
                        return Json(new
                        {
                            result = false,
                            error = _localizer["Actions cannot be executed with assets that have already been deleted."]
                                .Value
                        });
                    }

                    if (!string.IsNullOrEmpty(expenditureInputViewModel.MyDepositAsset))
                    {
                        Asset tempIncomeAssetToValidate = await _assetRepository.GetAssetAsync(
                            JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email,
                            expenditureInputViewModel.MyDepositAsset);
                        if (tempIncomeAssetToValidate.Deleted)
                        {
                            return Json(new
                            {
                                result = false,
                                error = _localizer[
                                    "Actions cannot be executed with assets that have already been deleted."].Value
                            });
                        }
                    }

                    #endregion

                    #region 대분류 = '정기저축'

                    if (expenditureInputViewModel.MainClass == "RegularSavings")
                    {
                        if (expenditureInputViewModel.SubClass is "Deposit" or "Investment")
                        {
                            try
                            {
                                if (expenditureInputViewModel.PaymentMethod == expenditureInputViewModel.MyDepositAsset)
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
                                        .Email, expenditureInputViewModel.PaymentMethod);
                                Asset incomeAsset = await _assetRepository.GetAssetAsync(
                                    JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte))
                                        .Email, expenditureInputViewModel.MyDepositAsset);

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

                                #region 지출 생성

                                await _expenditureRepository.CreateExpenditureAsync
                                (
                                    new Expenditure()
                                    {
                                        AccountEmail = JsonConvert
                                            .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email,
                                        MainClass = expenditureInputViewModel.MainClass,
                                        SubClass = expenditureInputViewModel.SubClass,
                                        Content = expenditureInputViewModel.Content ?? "",
                                        Amount = Math.Abs(expenditureInputViewModel.Amount),
                                        PaymentMethod = expenditureInputViewModel.PaymentMethod,
                                        MyDepositAsset = expenditureInputViewModel.MyDepositAsset,
                                        Created = expenditureInputViewModel.Created,
                                        Updated = DateTime.UtcNow,
                                        Note = expenditureInputViewModel.Note ?? ""
                                    }
                                );

                                #endregion

                                #region 지출 자산 업데이트

                                expenditureAsset.Amount -= Math.Abs(expenditureInputViewModel.Amount);
                                expenditureAsset.Updated = DateTime.UtcNow;
                                await _assetRepository.UpdateAssetAsync(expenditureAsset);

                                #endregion

                                #region 수입 자산 업데이트

                                incomeAsset.Amount += Math.Abs(expenditureInputViewModel.Amount);
                                incomeAsset.Updated = DateTime.UtcNow;
                                await _assetRepository.UpdateAssetAsync(incomeAsset);

                                #endregion

                                return Json(new
                                {
                                    result = true,
                                    message = _localizer["The expenditure has been successfully created."].Value
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

                    else if (expenditureInputViewModel.MainClass == "NonConsumerSpending")
                    {
                        if (expenditureInputViewModel.SubClass is "PublicPension" or "DebtRepayment")
                        {
                            try
                            {
                                if (expenditureInputViewModel.PaymentMethod == expenditureInputViewModel.MyDepositAsset)
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
                                        .Email, expenditureInputViewModel.PaymentMethod);
                                Asset incomeAsset = await _assetRepository.GetAssetAsync(
                                    JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte))
                                        .Email, expenditureInputViewModel.MyDepositAsset);

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

                                #region 지출 생성

                                await _expenditureRepository.CreateExpenditureAsync
                                (
                                    new Expenditure()
                                    {
                                        AccountEmail = JsonConvert
                                            .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email,
                                        MainClass = expenditureInputViewModel.MainClass,
                                        SubClass = expenditureInputViewModel.SubClass,
                                        Content = expenditureInputViewModel.Content ?? "",
                                        Amount = Math.Abs(expenditureInputViewModel.Amount),
                                        PaymentMethod = expenditureInputViewModel.PaymentMethod,
                                        MyDepositAsset = expenditureInputViewModel.MyDepositAsset,
                                        Created = expenditureInputViewModel.Created,
                                        Updated = DateTime.UtcNow,
                                        Note = expenditureInputViewModel.Note ?? ""
                                    }
                                );

                                #endregion

                                #region 지출 자산 업데이트

                                expenditureAsset.Amount -= Math.Abs(expenditureInputViewModel.Amount);
                                expenditureAsset.Updated = DateTime.UtcNow;
                                await _assetRepository.UpdateAssetAsync(expenditureAsset);

                                #endregion

                                #region 수입 자산 업데이트

                                incomeAsset.Amount += Math.Abs(expenditureInputViewModel.Amount);
                                incomeAsset.Updated = DateTime.UtcNow;
                                await _assetRepository.UpdateAssetAsync(incomeAsset);

                                #endregion

                                return Json(new
                                {
                                    result = true,
                                    message = _localizer["The expenditure has been successfully created."].Value
                                });
                            }
                            catch
                            {
                                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                            }
                        }
                        else if (expenditureInputViewModel.SubClass is "Tax" or
                                 "SocialInsurance" or
                                 "InterHouseholdTranserExpenses" or
                                 "NonProfitOrganizationTransfer")
                        {
                            try
                            {
                                Asset expenditureAsset = await _assetRepository.GetAssetAsync(
                                    JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte))
                                        .Email, expenditureInputViewModel.PaymentMethod);

                                if (expenditureAsset == null)
                                {
                                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                                }

                                #region 지출 생성

                                await _expenditureRepository.CreateExpenditureAsync
                                (
                                    new Expenditure()
                                    {
                                        AccountEmail = JsonConvert
                                            .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email,
                                        MainClass = expenditureInputViewModel.MainClass,
                                        SubClass = expenditureInputViewModel.SubClass,
                                        Content = expenditureInputViewModel.Content ?? "",
                                        Amount = Math.Abs(expenditureInputViewModel.Amount),
                                        PaymentMethod = expenditureInputViewModel.PaymentMethod,
                                        MyDepositAsset = "",
                                        Created = expenditureInputViewModel.Created,
                                        Updated = DateTime.UtcNow,
                                        Note = expenditureInputViewModel.Note ?? ""
                                    }
                                );

                                #endregion

                                #region 지출 자산 업데이트

                                expenditureAsset.Amount -= Math.Abs(expenditureInputViewModel.Amount);
                                expenditureAsset.Updated = DateTime.UtcNow;
                                await _assetRepository.UpdateAssetAsync(expenditureAsset);

                                #endregion

                                return Json(new
                                {
                                    result = true,
                                    message = _localizer["The expenditure has been successfully created."].Value
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

                    else if (expenditureInputViewModel.MainClass == "ConsumerSpending")
                    {
                        if (expenditureInputViewModel.SubClass is "MealOrEatOutExpenses" or
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
                                Asset expenditureAsset = await _assetRepository.GetAssetAsync(
                                    JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte))
                                        .Email, expenditureInputViewModel.PaymentMethod);

                                if (expenditureAsset == null)
                                {
                                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                                }

                                #region 지출 생성

                                await _expenditureRepository.CreateExpenditureAsync
                                (
                                    new Expenditure()
                                    {
                                        AccountEmail = JsonConvert
                                            .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email,
                                        MainClass = expenditureInputViewModel.MainClass,
                                        SubClass = expenditureInputViewModel.SubClass,
                                        Content = expenditureInputViewModel.Content ?? "",
                                        Amount = Math.Abs(expenditureInputViewModel.Amount),
                                        PaymentMethod = expenditureInputViewModel.PaymentMethod,
                                        MyDepositAsset = "",
                                        Created = expenditureInputViewModel.Created,
                                        Updated = DateTime.UtcNow,
                                        Note = expenditureInputViewModel.Note ?? ""
                                    }
                                );

                                #endregion

                                #region 지출 자산 업데이트

                                expenditureAsset.Amount -= Math.Abs(expenditureInputViewModel.Amount);
                                expenditureAsset.Updated = DateTime.UtcNow;
                                await _assetRepository.UpdateAssetAsync(expenditureAsset);

                                #endregion

                                return Json(new
                                {
                                    result = true,
                                    message = _localizer["The expenditure has been successfully created."].Value
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
        public async Task<IActionResult> Expenditure(string wholeSearch)
        {
            if (HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest") // ajax
            {
                List<ExpenditureOutputViewModel> expenditureOutputViewModels = [];

                #region Expenditures

                _ = HttpContext.Session.TryGetValue(
                    Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                        .Utilities.Session.Account), out byte[] resultByte);

                string loginedAccountTimeZoneIanaId = JsonConvert
                    .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).TimeZoneIanaId;

                List<Asset> assets = await _assetRepository.GetAssetsAsync(JsonConvert
                    .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);

                foreach (Expenditure item in await _expenditureRepository.GetExpendituresAsync(JsonConvert
                             .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email))
                {
                    if (string.IsNullOrEmpty(wholeSearch))
                    {
                        expenditureOutputViewModels.Add(new ExpenditureOutputViewModel()
                        {
                            Id = item.Id,
                            MainClass = _localizer[item.MainClass.ToString()].Value,
                            SubClass = _localizer[item.SubClass.ToString()].Value,
                            Content = item.Content,
                            Amount = Convert.ToDecimal(item.Amount.TrimTrailingZeros()),
                            MonetaryUnit = assets.Where(x => x.ProductName == item.PaymentMethod).FirstOrDefault()
                                .MonetaryUnit,
                            PaymentMethod = item.PaymentMethod,
                            Note = item.Note,
                            MyDepositAsset = item.MyDepositAsset,
                            Created = item.Created.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId),
                            Updated = item.Updated.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId)
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
                            (item.PaymentMethod?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.Note?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.MyDepositAsset?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.Created.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId).ToString() ?? "")
                            .Contains(wholeSearch) ||
                            (item.Updated.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId).ToString() ?? "")
                            .Contains(wholeSearch))
                        {
                            expenditureOutputViewModels.Add(new ExpenditureOutputViewModel()
                            {
                                Id = item.Id,
                                MainClass = _localizer[item.MainClass.ToString()].Value,
                                SubClass = _localizer[item.SubClass.ToString()].Value,
                                Content = item.Content,
                                Amount = Convert.ToDecimal(item.Amount.TrimTrailingZeros()),
                                MonetaryUnit = assets.Where(x => x.ProductName == item.PaymentMethod).FirstOrDefault()
                                    .MonetaryUnit,
                                PaymentMethod = item.PaymentMethod,
                                Note = item.Note,
                                MyDepositAsset = item.MyDepositAsset,
                                Created = item.Created.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId),
                                Updated = item.Updated.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId)
                            });
                        }
                    }
                }

                #endregion

                IQueryable<ExpenditureOutputViewModel> result = expenditureOutputViewModels
                    .OrderByDescending(m => m.Created).ThenByDescending(m => m.Updated).AsQueryable();
                return PartialView("_ExpenditureGrid", result);
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> IsExpenditureExists(int id)
        {
            try
            {
                _ = HttpContext.Session.TryGetValue(
                    Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                        .Utilities.Session.Account), out byte[] resultByte);
                string loginedAccountTimeZoneIanaId = JsonConvert
                    .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).TimeZoneIanaId;
                List<Expenditure> tempExpenditures =
                    await _expenditureRepository.GetExpendituresAsync(JsonConvert
                        .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);

                if (tempExpenditures == null)
                {
                    return Json(new { result = false, error = _localizer["No expenditure exists"].Value });
                }
                else
                {
                    Expenditure tempExpenditure = tempExpenditures.Where(a => a.Id == id).FirstOrDefault();

                    if (tempExpenditure == null)
                    {
                        return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                    }
                    else
                    {
                        tempExpenditure.Created =
                            tempExpenditure.Created.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId);
                        return Json(new { result = true, expenditure = tempExpenditure });
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
        public async Task<IActionResult> GetExpenditureAmountLabel(string productName)
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
        public async Task<IActionResult> UpdateExpenditure(
            [FromBody] ExpenditureInputViewModel expenditureInputViewModel)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    expenditureInputViewModel.Amount = Math.Abs(expenditureInputViewModel.Amount);
                    _ = HttpContext.Session.TryGetValue(
                        Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                            .Utilities.Session.Account), out byte[] resultByte);

                    #region 대분류 = '정기저축'

                    if (expenditureInputViewModel.MainClass == "RegularSavings")
                    {
                        if (expenditureInputViewModel.SubClass is "Deposit" or "Investment")
                        {
                            try
                            {
                                if (expenditureInputViewModel.PaymentMethod == expenditureInputViewModel.MyDepositAsset)
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
                                        .Email, expenditureInputViewModel.PaymentMethod);
                                Asset incomeAsset = await _assetRepository.GetAssetAsync(
                                    JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte))
                                        .Email, expenditureInputViewModel.MyDepositAsset);

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

                                List<Expenditure> tempExpenditures =
                                    await _expenditureRepository.GetExpendituresAsync(JsonConvert
                                        .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);
                                Expenditure previousTempExpenditure = tempExpenditures
                                    .Where(a => a.Id == expenditureInputViewModel.Id).FirstOrDefault();
                                Expenditure currentTempExpenditure = new()
                                {
                                    Id = expenditureInputViewModel.Id,
                                    AccountEmail = JsonConvert
                                        .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email,
                                    MainClass = expenditureInputViewModel.MainClass,
                                    SubClass = expenditureInputViewModel.SubClass,
                                    Content = expenditureInputViewModel?.Content ?? "",
                                    Amount = expenditureInputViewModel.Amount,
                                    PaymentMethod = expenditureInputViewModel.PaymentMethod,
                                    MyDepositAsset = expenditureInputViewModel?.MyDepositAsset ?? "",
                                    Created = expenditureInputViewModel.Created,
                                    Updated = DateTime.UtcNow,
                                    Note = expenditureInputViewModel?.Note ?? "",
                                    Asset = previousTempExpenditure.Asset
                                };

                                #region Validate ProductName's Deletion

                                Asset tempExpenditureAssetToValidate =
                                    await _assetRepository.GetAssetAsync(
                                        JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte))
                                            .Email, previousTempExpenditure.PaymentMethod);
                                if (tempExpenditureAssetToValidate.Deleted)
                                {
                                    return Json(new
                                    {
                                        result = false,
                                        error = _localizer[
                                                "Actions cannot be executed with assets that have already been deleted."]
                                            .Value
                                    });
                                }

                                if (!string.IsNullOrEmpty(previousTempExpenditure.MyDepositAsset))
                                {
                                    Asset tempIncomeAssetToValidate =
                                        await _assetRepository.GetAssetAsync(
                                            JsonConvert.DeserializeObject<Account>(
                                                Encoding.Default.GetString(resultByte)).Email,
                                            previousTempExpenditure.MyDepositAsset);
                                    if (tempIncomeAssetToValidate.Deleted)
                                    {
                                        return Json(new
                                        {
                                            result = false,
                                            error = _localizer[
                                                    "Actions cannot be executed with assets that have already been deleted."]
                                                .Value
                                        });
                                    }
                                }

                                #endregion

                                #region 과거 지출 값 [(결제수단(O)), (내입금자산(X))], 현재 지출 값 [(결제수단(O)), (내입금자산(O))]

                                if (!string.IsNullOrEmpty(previousTempExpenditure.PaymentMethod) &&
                                    string.IsNullOrEmpty(previousTempExpenditure.MyDepositAsset) &&
                                    !string.IsNullOrEmpty(currentTempExpenditure.PaymentMethod) &&
                                    !string.IsNullOrEmpty(currentTempExpenditure.MyDepositAsset))
                                {
                                    #region 지출 업데이트

                                    await _expenditureRepository.UpdateExpenditureAsync(currentTempExpenditure);

                                    #endregion

                                    if (previousTempExpenditure.PaymentMethod ==
                                        currentTempExpenditure.PaymentMethod) // else 보고 참조함.
                                    {
                                        Asset pastExpenditureAsset =
                                            await _assetRepository.GetAssetAsync(
                                                JsonConvert.DeserializeObject<Account>(
                                                    Encoding.Default.GetString(resultByte)).Email,
                                                previousTempExpenditure.PaymentMethod);
                                        pastExpenditureAsset.Amount = pastExpenditureAsset.Amount +
                                                                      Math.Abs(previousTempExpenditure.Amount) -
                                                                      Math.Abs(currentTempExpenditure.Amount);
                                        pastExpenditureAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                        Asset currentIncomeAsset =
                                            await _assetRepository.GetAssetAsync(
                                                JsonConvert.DeserializeObject<Account>(
                                                    Encoding.Default.GetString(resultByte)).Email,
                                                currentTempExpenditure.MyDepositAsset);
                                        currentIncomeAsset.Amount += Math.Abs(currentTempExpenditure.Amount);
                                        currentIncomeAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(currentIncomeAsset);
                                    }
                                    else if (previousTempExpenditure.PaymentMethod ==
                                             currentTempExpenditure.MyDepositAsset) // else 보고 참조함.
                                    {
                                        Asset pastExpenditureAsset =
                                            await _assetRepository.GetAssetAsync(
                                                JsonConvert.DeserializeObject<Account>(
                                                    Encoding.Default.GetString(resultByte)).Email,
                                                previousTempExpenditure.PaymentMethod);
                                        pastExpenditureAsset.Amount = pastExpenditureAsset.Amount +
                                                                      Math.Abs(previousTempExpenditure.Amount) +
                                                                      Math.Abs(currentTempExpenditure.Amount);
                                        pastExpenditureAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                        Asset currentExpenditureAsset =
                                            await _assetRepository.GetAssetAsync(
                                                JsonConvert.DeserializeObject<Account>(
                                                    Encoding.Default.GetString(resultByte)).Email,
                                                currentTempExpenditure.PaymentMethod);
                                        currentExpenditureAsset.Amount -= Math.Abs(currentTempExpenditure.Amount);
                                        currentExpenditureAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(currentExpenditureAsset);
                                    }
                                    else
                                    {
                                        #region 자산 업데이트

                                        Asset pastExpenditureAsset =
                                            await _assetRepository.GetAssetAsync(
                                                JsonConvert.DeserializeObject<Account>(
                                                    Encoding.Default.GetString(resultByte)).Email,
                                                previousTempExpenditure.PaymentMethod);
                                        //var pastIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, previousTempExpenditure.MyDepositAsset);

                                        Asset currentExpenditureAsset =
                                            await _assetRepository.GetAssetAsync(
                                                JsonConvert.DeserializeObject<Account>(
                                                    Encoding.Default.GetString(resultByte)).Email,
                                                currentTempExpenditure.PaymentMethod);
                                        Asset currentIncomeAsset =
                                            await _assetRepository.GetAssetAsync(
                                                JsonConvert.DeserializeObject<Account>(
                                                    Encoding.Default.GetString(resultByte)).Email,
                                                currentTempExpenditure.MyDepositAsset);

                                        #region 과거 자산

                                        pastExpenditureAsset.Amount += Math.Abs(previousTempExpenditure.Amount);
                                        pastExpenditureAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                        //pastIncomeAsset.Amount -= Math.Abs(previousTempExpenditure.Amount);
                                        //pastIncomeAsset.Updated = DateTime.UtcNow;
                                        //await assetRepository.UpdateAssetAsync(pastIncomeAsset);

                                        #endregion

                                        #region 현재 자산

                                        currentExpenditureAsset.Amount -= Math.Abs(currentTempExpenditure.Amount);
                                        currentExpenditureAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(currentExpenditureAsset);

                                        currentIncomeAsset.Amount += Math.Abs(currentTempExpenditure.Amount);
                                        currentIncomeAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(currentIncomeAsset);

                                        #endregion

                                        #endregion
                                    }

                                    return Json(new
                                    {
                                        result = true,
                                        message = _localizer["The expenditure has been successfully updated."].Value
                                    });
                                }

                                #endregion

                                #region 과거 지출 값 [(결제수단(O)), (내입금자산(O))], 현재 지출 값 [(결제수단(O)), (내입금자산(O))]

                                else if (!string.IsNullOrEmpty(previousTempExpenditure.PaymentMethod) &&
                                         !string.IsNullOrEmpty(previousTempExpenditure.MyDepositAsset) &&
                                         !string.IsNullOrEmpty(currentTempExpenditure.PaymentMethod) &&
                                         !string.IsNullOrEmpty(currentTempExpenditure.MyDepositAsset))
                                {
                                    #region 지출 업데이트

                                    await _expenditureRepository.UpdateExpenditureAsync(currentTempExpenditure);

                                    #endregion

                                    if (previousTempExpenditure.PaymentMethod == currentTempExpenditure.PaymentMethod)
                                    {
                                        if (previousTempExpenditure.MyDepositAsset ==
                                            currentTempExpenditure.MyDepositAsset)
                                        {
                                            #region 자산 업데이트

                                            Asset pastExpenditureAsset =
                                                await _assetRepository.GetAssetAsync(
                                                    JsonConvert.DeserializeObject<Account>(
                                                        Encoding.Default.GetString(resultByte)).Email,
                                                    previousTempExpenditure.PaymentMethod);
                                            Asset pastIncomeAsset =
                                                await _assetRepository.GetAssetAsync(
                                                    JsonConvert.DeserializeObject<Account>(
                                                        Encoding.Default.GetString(resultByte)).Email,
                                                    previousTempExpenditure.MyDepositAsset);

                                            pastExpenditureAsset.Amount = pastExpenditureAsset.Amount +
                                                                          Math.Abs(previousTempExpenditure.Amount) -
                                                                          Math.Abs(currentTempExpenditure.Amount);
                                            pastExpenditureAsset.Updated = DateTime.UtcNow;
                                            await _assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                            pastIncomeAsset.Amount = pastIncomeAsset.Amount -
                                                                     Math.Abs(previousTempExpenditure.Amount) +
                                                                     Math.Abs(currentTempExpenditure.Amount);
                                            pastIncomeAsset.Updated = DateTime.UtcNow;
                                            await _assetRepository.UpdateAssetAsync(pastIncomeAsset);

                                            #endregion

                                            //#region 자산 업데이트

                                            //#region 과거 자산
                                            //var pastExpenditureAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, previousTempExpenditure.PaymentMethod);
                                            //var pastIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, previousTempExpenditure.MyDepositAsset);

                                            //var currentExpenditureAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.PaymentMethod);
                                            //var currentIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.MyDepositAsset);

                                            //pastExpenditureAsset.Amount += Math.Abs(previousTempExpenditure.Amount);
                                            //pastExpenditureAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                            //pastIncomeAsset.Amount -= Math.Abs(previousTempExpenditure.Amount);
                                            //pastIncomeAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(pastIncomeAsset);
                                            //#endregion

                                            //#region 현재 자산
                                            //currentExpenditureAsset.Amount -= Math.Abs(currentTempExpenditure.Amount);
                                            //currentExpenditureAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(currentExpenditureAsset);

                                            //currentIncomeAsset.Amount += Math.Abs(currentTempExpenditure.Amount);
                                            //currentIncomeAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(currentIncomeAsset);
                                            //#endregion

                                            //#endregion
                                        }
                                        else
                                        {
                                            #region 자산 업데이트

                                            Asset pastExpenditureAsset =
                                                await _assetRepository.GetAssetAsync(
                                                    JsonConvert.DeserializeObject<Account>(
                                                        Encoding.Default.GetString(resultByte)).Email,
                                                    previousTempExpenditure.PaymentMethod);
                                            Asset pastIncomeAsset =
                                                await _assetRepository.GetAssetAsync(
                                                    JsonConvert.DeserializeObject<Account>(
                                                        Encoding.Default.GetString(resultByte)).Email,
                                                    previousTempExpenditure.MyDepositAsset);

                                            Asset currentIncomeAsset =
                                                await _assetRepository.GetAssetAsync(
                                                    JsonConvert.DeserializeObject<Account>(
                                                        Encoding.Default.GetString(resultByte)).Email,
                                                    currentTempExpenditure.MyDepositAsset);

                                            pastExpenditureAsset.Amount = pastExpenditureAsset.Amount +
                                                                          Math.Abs(previousTempExpenditure.Amount) -
                                                                          Math.Abs(currentTempExpenditure.Amount);
                                            pastExpenditureAsset.Updated = DateTime.UtcNow;
                                            await _assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                            pastIncomeAsset.Amount -= Math.Abs(previousTempExpenditure.Amount);
                                            pastIncomeAsset.Updated = DateTime.UtcNow;
                                            await _assetRepository.UpdateAssetAsync(pastIncomeAsset);

                                            currentIncomeAsset.Amount += Math.Abs(currentTempExpenditure.Amount);
                                            currentIncomeAsset.Updated = DateTime.UtcNow;
                                            await _assetRepository.UpdateAssetAsync(currentIncomeAsset);

                                            #endregion

                                            //#region 자산 업데이트

                                            //#region 과거 자산
                                            //var pastExpenditureAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, previousTempExpenditure.PaymentMethod);
                                            //var pastIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, previousTempExpenditure.MyDepositAsset);

                                            //var currentExpenditureAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.PaymentMethod);
                                            //var currentIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.MyDepositAsset);

                                            //pastExpenditureAsset.Amount += Math.Abs(previousTempExpenditure.Amount);
                                            //pastExpenditureAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                            //pastIncomeAsset.Amount -= Math.Abs(previousTempExpenditure.Amount);
                                            //pastIncomeAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(pastIncomeAsset);
                                            //#endregion

                                            //#region 현재 자산
                                            //currentExpenditureAsset.Amount -= Math.Abs(currentTempExpenditure.Amount);
                                            //currentExpenditureAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(currentExpenditureAsset);

                                            //currentIncomeAsset.Amount += Math.Abs(currentTempExpenditure.Amount);
                                            //currentIncomeAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(currentIncomeAsset);
                                            //#endregion

                                            //#endregion
                                        }
                                    }
                                    else if (previousTempExpenditure.PaymentMethod ==
                                             currentTempExpenditure.MyDepositAsset)
                                    {
                                        if (previousTempExpenditure.MyDepositAsset ==
                                            currentTempExpenditure.PaymentMethod)
                                        {
                                            #region 자산 업데이트

                                            Asset pastExpenditureAsset =
                                                await _assetRepository.GetAssetAsync(
                                                    JsonConvert.DeserializeObject<Account>(
                                                        Encoding.Default.GetString(resultByte)).Email,
                                                    previousTempExpenditure.PaymentMethod);
                                            Asset pastIncomeAsset =
                                                await _assetRepository.GetAssetAsync(
                                                    JsonConvert.DeserializeObject<Account>(
                                                        Encoding.Default.GetString(resultByte)).Email,
                                                    previousTempExpenditure.MyDepositAsset);

                                            pastExpenditureAsset.Amount = pastExpenditureAsset.Amount +
                                                                          Math.Abs(previousTempExpenditure.Amount) +
                                                                          Math.Abs(currentTempExpenditure.Amount);
                                            pastExpenditureAsset.Updated = DateTime.UtcNow;
                                            await _assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                            pastIncomeAsset.Amount = pastIncomeAsset.Amount -
                                                                     Math.Abs(previousTempExpenditure.Amount) -
                                                                     Math.Abs(currentTempExpenditure.Amount);
                                            pastIncomeAsset.Updated = DateTime.UtcNow;
                                            await _assetRepository.UpdateAssetAsync(pastIncomeAsset);

                                            #endregion

                                            //#region 자산 업데이트

                                            //#region 과거 자산
                                            //var pastExpenditureAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, previousTempExpenditure.PaymentMethod);
                                            //var pastIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, previousTempExpenditure.MyDepositAsset);

                                            //var currentExpenditureAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.PaymentMethod);
                                            //var currentIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.MyDepositAsset);

                                            //pastExpenditureAsset.Amount += Math.Abs(previousTempExpenditure.Amount);
                                            //pastExpenditureAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                            //pastIncomeAsset.Amount -= Math.Abs(previousTempExpenditure.Amount);
                                            //pastIncomeAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(pastIncomeAsset);
                                            //#endregion

                                            //#region 현재 자산
                                            //currentExpenditureAsset.Amount -= Math.Abs(currentTempExpenditure.Amount);
                                            //currentExpenditureAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(currentExpenditureAsset);

                                            //currentIncomeAsset.Amount += Math.Abs(currentTempExpenditure.Amount);
                                            //currentIncomeAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(currentIncomeAsset);
                                            //#endregion

                                            //#endregion
                                        }
                                        else
                                        {
                                            #region 자산 업데이트

                                            #region 과거 자산

                                            Asset pastExpenditureAsset =
                                                await _assetRepository.GetAssetAsync(
                                                    JsonConvert.DeserializeObject<Account>(
                                                        Encoding.Default.GetString(resultByte)).Email,
                                                    previousTempExpenditure.PaymentMethod);
                                            Asset pastIncomeAsset =
                                                await _assetRepository.GetAssetAsync(
                                                    JsonConvert.DeserializeObject<Account>(
                                                        Encoding.Default.GetString(resultByte)).Email,
                                                    previousTempExpenditure.MyDepositAsset);

                                            Asset currentExpenditureAsset =
                                                await _assetRepository.GetAssetAsync(
                                                    JsonConvert.DeserializeObject<Account>(
                                                        Encoding.Default.GetString(resultByte)).Email,
                                                    currentTempExpenditure.PaymentMethod);

                                            pastExpenditureAsset.Amount = pastExpenditureAsset.Amount +
                                                                          Math.Abs(previousTempExpenditure.Amount) +
                                                                          Math.Abs(currentTempExpenditure.Amount);
                                            pastExpenditureAsset.Updated = DateTime.UtcNow;
                                            await _assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                            pastIncomeAsset.Amount -= Math.Abs(previousTempExpenditure.Amount);
                                            pastIncomeAsset.Updated = DateTime.UtcNow;
                                            await _assetRepository.UpdateAssetAsync(pastIncomeAsset);

                                            #endregion

                                            #region 현재 자산

                                            currentExpenditureAsset.Amount -= Math.Abs(currentTempExpenditure.Amount);
                                            currentExpenditureAsset.Updated = DateTime.UtcNow;
                                            await _assetRepository.UpdateAssetAsync(currentExpenditureAsset);

                                            #endregion

                                            #endregion

                                            //#region 자산 업데이트

                                            //#region 과거 자산
                                            //var pastExpenditureAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, previousTempExpenditure.PaymentMethod);
                                            //var pastIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, previousTempExpenditure.MyDepositAsset);

                                            //var currentExpenditureAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.PaymentMethod);
                                            //var currentIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.MyDepositAsset);

                                            //pastExpenditureAsset.Amount += Math.Abs(previousTempExpenditure.Amount);
                                            //pastExpenditureAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                            //pastIncomeAsset.Amount -= Math.Abs(previousTempExpenditure.Amount);
                                            //pastIncomeAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(pastIncomeAsset);
                                            //#endregion

                                            //#region 현재 자산
                                            //currentExpenditureAsset.Amount -= Math.Abs(currentTempExpenditure.Amount);
                                            //currentExpenditureAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(currentExpenditureAsset);

                                            //currentIncomeAsset.Amount += Math.Abs(currentTempExpenditure.Amount);
                                            //currentIncomeAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(currentIncomeAsset);
                                            //#endregion

                                            //#endregion
                                        }
                                    }
                                    else if (previousTempExpenditure.MyDepositAsset ==
                                             currentTempExpenditure.PaymentMethod)
                                    {
                                        if (previousTempExpenditure.PaymentMethod ==
                                            currentTempExpenditure.MyDepositAsset)
                                        {
                                            #region 자산 업데이트

                                            #region 과거 자산

                                            Asset pastExpenditureAsset =
                                                await _assetRepository.GetAssetAsync(
                                                    JsonConvert.DeserializeObject<Account>(
                                                        Encoding.Default.GetString(resultByte)).Email,
                                                    previousTempExpenditure.PaymentMethod);
                                            Asset pastIncomeAsset =
                                                await _assetRepository.GetAssetAsync(
                                                    JsonConvert.DeserializeObject<Account>(
                                                        Encoding.Default.GetString(resultByte)).Email,
                                                    previousTempExpenditure.MyDepositAsset);

                                            pastExpenditureAsset.Amount = pastExpenditureAsset.Amount +
                                                                          Math.Abs(previousTempExpenditure.Amount) +
                                                                          Math.Abs(currentTempExpenditure.Amount);
                                            pastExpenditureAsset.Updated = DateTime.UtcNow;
                                            await _assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                            pastIncomeAsset.Amount = pastIncomeAsset.Amount -
                                                                     Math.Abs(previousTempExpenditure.Amount) -
                                                                     Math.Abs(currentTempExpenditure.Amount);
                                            pastIncomeAsset.Updated = DateTime.UtcNow;
                                            await _assetRepository.UpdateAssetAsync(pastIncomeAsset);

                                            #endregion

                                            #endregion

                                            //#region 자산 업데이트

                                            //#region 과거 자산
                                            //var pastExpenditureAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, previousTempExpenditure.PaymentMethod);
                                            //var pastIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, previousTempExpenditure.MyDepositAsset);

                                            //var currentExpenditureAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.PaymentMethod);
                                            //var currentIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.MyDepositAsset);

                                            //pastExpenditureAsset.Amount += Math.Abs(previousTempExpenditure.Amount);
                                            //pastExpenditureAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                            //pastIncomeAsset.Amount -= Math.Abs(previousTempExpenditure.Amount);
                                            //pastIncomeAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(pastIncomeAsset);
                                            //#endregion

                                            //#region 현재 자산
                                            //currentExpenditureAsset.Amount -= Math.Abs(currentTempExpenditure.Amount);
                                            //currentExpenditureAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(currentExpenditureAsset);

                                            //currentIncomeAsset.Amount += Math.Abs(currentTempExpenditure.Amount);
                                            //currentIncomeAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(currentIncomeAsset);
                                            //#endregion

                                            //#endregion
                                        }
                                        else
                                        {
                                            #region 자산 업데이트

                                            #region 과거 자산

                                            Asset pastExpenditureAsset =
                                                await _assetRepository.GetAssetAsync(
                                                    JsonConvert.DeserializeObject<Account>(
                                                        Encoding.Default.GetString(resultByte)).Email,
                                                    previousTempExpenditure.PaymentMethod);
                                            Asset pastIncomeAsset =
                                                await _assetRepository.GetAssetAsync(
                                                    JsonConvert.DeserializeObject<Account>(
                                                        Encoding.Default.GetString(resultByte)).Email,
                                                    previousTempExpenditure.MyDepositAsset);

                                            Asset currentIncomeAsset =
                                                await _assetRepository.GetAssetAsync(
                                                    JsonConvert.DeserializeObject<Account>(
                                                        Encoding.Default.GetString(resultByte)).Email,
                                                    currentTempExpenditure.MyDepositAsset);

                                            pastExpenditureAsset.Amount += Math.Abs(previousTempExpenditure.Amount);
                                            pastExpenditureAsset.Updated = DateTime.UtcNow;
                                            await _assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                            pastIncomeAsset.Amount = pastIncomeAsset.Amount -
                                                                     Math.Abs(previousTempExpenditure.Amount) -
                                                                     Math.Abs(currentTempExpenditure.Amount);
                                            pastIncomeAsset.Updated = DateTime.UtcNow;
                                            await _assetRepository.UpdateAssetAsync(pastIncomeAsset);

                                            #endregion

                                            #region 현재 자산

                                            currentIncomeAsset.Amount += Math.Abs(currentTempExpenditure.Amount);
                                            currentIncomeAsset.Updated = DateTime.UtcNow;
                                            await _assetRepository.UpdateAssetAsync(currentIncomeAsset);

                                            #endregion

                                            #endregion

                                            //#region 자산 업데이트

                                            //#region 과거 자산
                                            //var pastExpenditureAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, previousTempExpenditure.PaymentMethod);
                                            //var pastIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, previousTempExpenditure.MyDepositAsset);

                                            //var currentExpenditureAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.PaymentMethod);
                                            //var currentIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.MyDepositAsset);

                                            //pastExpenditureAsset.Amount += Math.Abs(previousTempExpenditure.Amount);
                                            //pastExpenditureAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                            //pastIncomeAsset.Amount -= Math.Abs(previousTempExpenditure.Amount);
                                            //pastIncomeAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(pastIncomeAsset);
                                            //#endregion

                                            //#region 현재 자산
                                            //currentExpenditureAsset.Amount -= Math.Abs(currentTempExpenditure.Amount);
                                            //currentExpenditureAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(currentExpenditureAsset);

                                            //currentIncomeAsset.Amount += Math.Abs(currentTempExpenditure.Amount);
                                            //currentIncomeAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(currentIncomeAsset);
                                            //#endregion

                                            //#endregion
                                        }
                                    }
                                    else if (previousTempExpenditure.MyDepositAsset ==
                                             currentTempExpenditure.MyDepositAsset)
                                    {
                                        if (previousTempExpenditure.PaymentMethod ==
                                            currentTempExpenditure.PaymentMethod)
                                        {
                                            #region 자산 업데이트

                                            #region 과거 자산

                                            Asset pastExpenditureAsset =
                                                await _assetRepository.GetAssetAsync(
                                                    JsonConvert.DeserializeObject<Account>(
                                                        Encoding.Default.GetString(resultByte)).Email,
                                                    previousTempExpenditure.PaymentMethod);
                                            Asset pastIncomeAsset =
                                                await _assetRepository.GetAssetAsync(
                                                    JsonConvert.DeserializeObject<Account>(
                                                        Encoding.Default.GetString(resultByte)).Email,
                                                    previousTempExpenditure.MyDepositAsset);

                                            pastExpenditureAsset.Amount = pastExpenditureAsset.Amount +
                                                                          Math.Abs(previousTempExpenditure.Amount) -
                                                                          Math.Abs(currentTempExpenditure.Amount);
                                            pastExpenditureAsset.Updated = DateTime.UtcNow;
                                            await _assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                            pastIncomeAsset.Amount = pastIncomeAsset.Amount -
                                                                     Math.Abs(previousTempExpenditure.Amount) +
                                                                     Math.Abs(currentTempExpenditure.Amount);
                                            pastIncomeAsset.Updated = DateTime.UtcNow;
                                            await _assetRepository.UpdateAssetAsync(pastIncomeAsset);

                                            #endregion

                                            #endregion

                                            //#region 자산 업데이트

                                            //#region 과거 자산
                                            //var pastExpenditureAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, previousTempExpenditure.PaymentMethod);
                                            //var pastIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, previousTempExpenditure.MyDepositAsset);

                                            //var currentExpenditureAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.PaymentMethod);
                                            //var currentIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.MyDepositAsset);

                                            //pastExpenditureAsset.Amount += Math.Abs(previousTempExpenditure.Amount);
                                            //pastExpenditureAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                            //pastIncomeAsset.Amount -= Math.Abs(previousTempExpenditure.Amount);
                                            //pastIncomeAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(pastIncomeAsset);
                                            //#endregion

                                            //#region 현재 자산
                                            //currentExpenditureAsset.Amount -= Math.Abs(currentTempExpenditure.Amount);
                                            //currentExpenditureAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(currentExpenditureAsset);

                                            //currentIncomeAsset.Amount += Math.Abs(currentTempExpenditure.Amount);
                                            //currentIncomeAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(currentIncomeAsset);
                                            //#endregion

                                            //#endregion
                                        }
                                        else
                                        {
                                            #region 자산 업데이트

                                            #region 과거 자산

                                            Asset pastExpenditureAsset =
                                                await _assetRepository.GetAssetAsync(
                                                    JsonConvert.DeserializeObject<Account>(
                                                        Encoding.Default.GetString(resultByte)).Email,
                                                    previousTempExpenditure.PaymentMethod);
                                            Asset pastIncomeAsset =
                                                await _assetRepository.GetAssetAsync(
                                                    JsonConvert.DeserializeObject<Account>(
                                                        Encoding.Default.GetString(resultByte)).Email,
                                                    previousTempExpenditure.MyDepositAsset);

                                            Asset currentExpenditureAsset =
                                                await _assetRepository.GetAssetAsync(
                                                    JsonConvert.DeserializeObject<Account>(
                                                        Encoding.Default.GetString(resultByte)).Email,
                                                    currentTempExpenditure.PaymentMethod);

                                            pastExpenditureAsset.Amount += Math.Abs(previousTempExpenditure.Amount);
                                            pastExpenditureAsset.Updated = DateTime.UtcNow;
                                            await _assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                            pastIncomeAsset.Amount = pastIncomeAsset.Amount -
                                                                     Math.Abs(previousTempExpenditure.Amount) +
                                                                     Math.Abs(currentTempExpenditure.Amount);
                                            pastIncomeAsset.Updated = DateTime.UtcNow;
                                            await _assetRepository.UpdateAssetAsync(pastIncomeAsset);

                                            #endregion

                                            #region 현재 자산

                                            currentExpenditureAsset.Amount -= Math.Abs(currentTempExpenditure.Amount);
                                            currentExpenditureAsset.Updated = DateTime.UtcNow;
                                            await _assetRepository.UpdateAssetAsync(currentExpenditureAsset);

                                            #endregion

                                            #endregion

                                            //#region 자산 업데이트

                                            //#region 과거 자산
                                            //var pastExpenditureAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, previousTempExpenditure.PaymentMethod);
                                            //var pastIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, previousTempExpenditure.MyDepositAsset);

                                            //var currentExpenditureAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.PaymentMethod);
                                            //var currentIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.MyDepositAsset);

                                            //pastExpenditureAsset.Amount += Math.Abs(previousTempExpenditure.Amount);
                                            //pastExpenditureAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                            //pastIncomeAsset.Amount -= Math.Abs(previousTempExpenditure.Amount);
                                            //pastIncomeAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(pastIncomeAsset);
                                            //#endregion

                                            //#region 현재 자산
                                            //currentExpenditureAsset.Amount -= Math.Abs(currentTempExpenditure.Amount);
                                            //currentExpenditureAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(currentExpenditureAsset);

                                            //currentIncomeAsset.Amount += Math.Abs(currentTempExpenditure.Amount);
                                            //currentIncomeAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(currentIncomeAsset);
                                            //#endregion

                                            //#endregion
                                        }
                                    }
                                    else
                                    {
                                        #region 자산 업데이트

                                        #region 과거 자산

                                        Asset pastExpenditureAsset =
                                            await _assetRepository.GetAssetAsync(
                                                JsonConvert.DeserializeObject<Account>(
                                                    Encoding.Default.GetString(resultByte)).Email,
                                                previousTempExpenditure.PaymentMethod);
                                        Asset pastIncomeAsset = await _assetRepository.GetAssetAsync(
                                            JsonConvert.DeserializeObject<Account>(
                                                Encoding.Default.GetString(resultByte)).Email,
                                            previousTempExpenditure.MyDepositAsset);

                                        Asset currentExpenditureAsset =
                                            await _assetRepository.GetAssetAsync(
                                                JsonConvert.DeserializeObject<Account>(
                                                    Encoding.Default.GetString(resultByte)).Email,
                                                currentTempExpenditure.PaymentMethod);
                                        Asset currentIncomeAsset =
                                            await _assetRepository.GetAssetAsync(
                                                JsonConvert.DeserializeObject<Account>(
                                                    Encoding.Default.GetString(resultByte)).Email,
                                                currentTempExpenditure.MyDepositAsset);

                                        pastExpenditureAsset.Amount += Math.Abs(previousTempExpenditure.Amount);
                                        pastExpenditureAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                        pastIncomeAsset.Amount -= Math.Abs(previousTempExpenditure.Amount);
                                        pastIncomeAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(pastIncomeAsset);

                                        #endregion

                                        #region 현재 자산

                                        currentExpenditureAsset.Amount -= Math.Abs(currentTempExpenditure.Amount);
                                        currentExpenditureAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(currentExpenditureAsset);

                                        currentIncomeAsset.Amount += Math.Abs(currentTempExpenditure.Amount);
                                        currentIncomeAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(currentIncomeAsset);

                                        #endregion

                                        #endregion
                                    }

                                    return Json(new
                                    {
                                        result = true,
                                        message = _localizer["The expenditure has been successfully updated."].Value
                                    });
                                }

                                #endregion

                                #region 잘못된 경우

                                else
                                {
                                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
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

                    #endregion

                    #region 대분류 = '비소비지출'

                    else if (expenditureInputViewModel.MainClass == "NonConsumerSpending")
                    {
                        if (expenditureInputViewModel.SubClass is "PublicPension" or "DebtRepayment")
                        {
                            try
                            {
                                if (expenditureInputViewModel.PaymentMethod == expenditureInputViewModel.MyDepositAsset)
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
                                        .Email, expenditureInputViewModel.PaymentMethod);
                                Asset incomeAsset = await _assetRepository.GetAssetAsync(
                                    JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte))
                                        .Email, expenditureInputViewModel.MyDepositAsset);

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

                                List<Expenditure> tempExpenditures =
                                    await _expenditureRepository.GetExpendituresAsync(JsonConvert
                                        .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);
                                Expenditure previousTempExpenditure = tempExpenditures
                                    .Where(a => a.Id == expenditureInputViewModel.Id).FirstOrDefault();
                                Expenditure currentTempExpenditure = new()
                                {
                                    Id = expenditureInputViewModel.Id,
                                    AccountEmail = JsonConvert
                                        .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email,
                                    MainClass = expenditureInputViewModel.MainClass,
                                    SubClass = expenditureInputViewModel.SubClass,
                                    Content = expenditureInputViewModel?.Content ?? "",
                                    Amount = expenditureInputViewModel.Amount,
                                    PaymentMethod = expenditureInputViewModel.PaymentMethod,
                                    MyDepositAsset = expenditureInputViewModel?.MyDepositAsset ?? "",
                                    Created = expenditureInputViewModel.Created,
                                    Updated = DateTime.UtcNow,
                                    Note = expenditureInputViewModel?.Note ?? "",
                                    Asset = previousTempExpenditure.Asset
                                };

                                #region Validate ProductName's Deletion

                                Asset tempExpenditureAssetToValidate =
                                    await _assetRepository.GetAssetAsync(
                                        JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte))
                                            .Email, previousTempExpenditure.PaymentMethod);
                                if (tempExpenditureAssetToValidate.Deleted)
                                {
                                    return Json(new
                                    {
                                        result = false,
                                        error = _localizer[
                                                "Actions cannot be executed with assets that have already been deleted."]
                                            .Value
                                    });
                                }

                                if (!string.IsNullOrEmpty(previousTempExpenditure.MyDepositAsset))
                                {
                                    Asset tempIncomeAssetToValidate =
                                        await _assetRepository.GetAssetAsync(
                                            JsonConvert.DeserializeObject<Account>(
                                                Encoding.Default.GetString(resultByte)).Email,
                                            previousTempExpenditure.MyDepositAsset);
                                    if (tempIncomeAssetToValidate.Deleted)
                                    {
                                        return Json(new
                                        {
                                            result = false,
                                            error = _localizer[
                                                    "Actions cannot be executed with assets that have already been deleted."]
                                                .Value
                                        });
                                    }
                                }

                                #endregion

                                #region 과거 지출 값 [(결제수단(O)), (내입금자산(X))], 현재 지출 값 [(결제수단(O)), (내입금자산(O))]

                                if (!string.IsNullOrEmpty(previousTempExpenditure.PaymentMethod) &&
                                    string.IsNullOrEmpty(previousTempExpenditure.MyDepositAsset) &&
                                    !string.IsNullOrEmpty(currentTempExpenditure.PaymentMethod) &&
                                    !string.IsNullOrEmpty(currentTempExpenditure.MyDepositAsset))
                                {
                                    #region 지출 업데이트

                                    await _expenditureRepository.UpdateExpenditureAsync(currentTempExpenditure);

                                    #endregion

                                    if (previousTempExpenditure.PaymentMethod == currentTempExpenditure.PaymentMethod)
                                    {
                                        #region 자산 업데이트

                                        Asset pastExpenditureAsset =
                                            await _assetRepository.GetAssetAsync(
                                                JsonConvert.DeserializeObject<Account>(
                                                    Encoding.Default.GetString(resultByte)).Email,
                                                previousTempExpenditure.PaymentMethod);
                                        Asset currentIncomeAsset =
                                            await _assetRepository.GetAssetAsync(
                                                JsonConvert.DeserializeObject<Account>(
                                                    Encoding.Default.GetString(resultByte)).Email,
                                                currentTempExpenditure.MyDepositAsset);

                                        pastExpenditureAsset.Amount = pastExpenditureAsset.Amount +
                                                                      Math.Abs(previousTempExpenditure.Amount) -
                                                                      Math.Abs(currentTempExpenditure.Amount);
                                        pastExpenditureAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                        currentIncomeAsset.Amount += Math.Abs(currentTempExpenditure.Amount);
                                        currentIncomeAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(currentIncomeAsset);

                                        #endregion

                                        //#region 자산 업데이트

                                        //#region 과거 자산
                                        //var pastExpenditureAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, previousTempExpenditure.PaymentMethod);
                                        //var pastIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, previousTempExpenditure.MyDepositAsset);

                                        //var currentExpenditureAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.PaymentMethod);
                                        //var currentIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.MyDepositAsset);

                                        //pastExpenditureAsset.Amount += Math.Abs(previousTempExpenditure.Amount);
                                        //pastExpenditureAsset.Updated = DateTime.UtcNow;
                                        //await assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                        //pastIncomeAsset.Amount -= Math.Abs(previousTempExpenditure.Amount);
                                        //pastIncomeAsset.Updated = DateTime.UtcNow;
                                        //await assetRepository.UpdateAssetAsync(pastIncomeAsset);
                                        //#endregion

                                        //#region 현재 자산
                                        //currentExpenditureAsset.Amount -= Math.Abs(currentTempExpenditure.Amount);
                                        //currentExpenditureAsset.Updated = DateTime.UtcNow;
                                        //await assetRepository.UpdateAssetAsync(currentExpenditureAsset);

                                        //currentIncomeAsset.Amount += Math.Abs(currentTempExpenditure.Amount);
                                        //currentIncomeAsset.Updated = DateTime.UtcNow;
                                        //await assetRepository.UpdateAssetAsync(currentIncomeAsset);
                                        //#endregion

                                        //#endregion
                                    }
                                    else if (previousTempExpenditure.PaymentMethod ==
                                             currentTempExpenditure.MyDepositAsset)
                                    {
                                        #region 자산 업데이트

                                        #region 과거 자산

                                        Asset pastExpenditureAsset =
                                            await _assetRepository.GetAssetAsync(
                                                JsonConvert.DeserializeObject<Account>(
                                                    Encoding.Default.GetString(resultByte)).Email,
                                                previousTempExpenditure.PaymentMethod);

                                        Asset currentExpenditureAsset =
                                            await _assetRepository.GetAssetAsync(
                                                JsonConvert.DeserializeObject<Account>(
                                                    Encoding.Default.GetString(resultByte)).Email,
                                                currentTempExpenditure.PaymentMethod);

                                        pastExpenditureAsset.Amount = pastExpenditureAsset.Amount +
                                                                      Math.Abs(previousTempExpenditure.Amount) +
                                                                      Math.Abs(currentTempExpenditure.Amount);
                                        pastExpenditureAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                        #endregion

                                        #region 현재 자산

                                        currentExpenditureAsset.Amount -= Math.Abs(currentTempExpenditure.Amount);
                                        currentExpenditureAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(currentExpenditureAsset);

                                        #endregion

                                        #endregion

                                        //#region 자산 업데이트

                                        //#region 과거 자산
                                        //var pastExpenditureAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, previousTempExpenditure.PaymentMethod);
                                        //var pastIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, previousTempExpenditure.MyDepositAsset);

                                        //var currentExpenditureAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.PaymentMethod);
                                        //var currentIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.MyDepositAsset);

                                        //pastExpenditureAsset.Amount += Math.Abs(previousTempExpenditure.Amount);
                                        //pastExpenditureAsset.Updated = DateTime.UtcNow;
                                        //await assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                        //pastIncomeAsset.Amount -= Math.Abs(previousTempExpenditure.Amount);
                                        //pastIncomeAsset.Updated = DateTime.UtcNow;
                                        //await assetRepository.UpdateAssetAsync(pastIncomeAsset);
                                        //#endregion

                                        //#region 현재 자산
                                        //currentExpenditureAsset.Amount -= Math.Abs(currentTempExpenditure.Amount);
                                        //currentExpenditureAsset.Updated = DateTime.UtcNow;
                                        //await assetRepository.UpdateAssetAsync(currentExpenditureAsset);

                                        //currentIncomeAsset.Amount += Math.Abs(currentTempExpenditure.Amount);
                                        //currentIncomeAsset.Updated = DateTime.UtcNow;
                                        //await assetRepository.UpdateAssetAsync(currentIncomeAsset);
                                        //#endregion

                                        //#endregion
                                    }
                                    else
                                    {
                                        #region 자산 업데이트

                                        #region 과거 자산

                                        Asset pastExpenditureAsset =
                                            await _assetRepository.GetAssetAsync(
                                                JsonConvert.DeserializeObject<Account>(
                                                    Encoding.Default.GetString(resultByte)).Email,
                                                previousTempExpenditure.PaymentMethod);
                                        //var pastIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, previousTempExpenditure.MyDepositAsset);

                                        Asset currentExpenditureAsset =
                                            await _assetRepository.GetAssetAsync(
                                                JsonConvert.DeserializeObject<Account>(
                                                    Encoding.Default.GetString(resultByte)).Email,
                                                currentTempExpenditure.PaymentMethod);
                                        Asset currentIncomeAsset =
                                            await _assetRepository.GetAssetAsync(
                                                JsonConvert.DeserializeObject<Account>(
                                                    Encoding.Default.GetString(resultByte)).Email,
                                                currentTempExpenditure.MyDepositAsset);

                                        pastExpenditureAsset.Amount += Math.Abs(previousTempExpenditure.Amount);
                                        pastExpenditureAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                        //pastIncomeAsset.Amount -= Math.Abs(previousTempExpenditure.Amount);
                                        //pastIncomeAsset.Updated = DateTime.UtcNow;
                                        //await assetRepository.UpdateAssetAsync(pastIncomeAsset);

                                        #endregion

                                        #region 현재 자산

                                        currentExpenditureAsset.Amount -= Math.Abs(currentTempExpenditure.Amount);
                                        currentExpenditureAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(currentExpenditureAsset);

                                        currentIncomeAsset.Amount += Math.Abs(currentTempExpenditure.Amount);
                                        currentIncomeAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(currentIncomeAsset);

                                        #endregion

                                        #endregion
                                    }

                                    return Json(new
                                    {
                                        result = true,
                                        message = _localizer["The expenditure has been successfully updated."].Value
                                    });
                                }

                                #endregion

                                #region 과거 지출 값 [(결제수단(O)), (내입금자산(O))], 현재 지출 값 [(결제수단(O)), (내입금자산(O))]

                                else if (!string.IsNullOrEmpty(previousTempExpenditure.PaymentMethod) &&
                                         !string.IsNullOrEmpty(previousTempExpenditure.MyDepositAsset) &&
                                         !string.IsNullOrEmpty(currentTempExpenditure.PaymentMethod) &&
                                         !string.IsNullOrEmpty(currentTempExpenditure.MyDepositAsset))
                                {
                                    #region 지출 업데이트

                                    await _expenditureRepository.UpdateExpenditureAsync(currentTempExpenditure);

                                    #endregion

                                    if (previousTempExpenditure.PaymentMethod == currentTempExpenditure.PaymentMethod)
                                    {
                                        if (previousTempExpenditure.MyDepositAsset ==
                                            currentTempExpenditure.MyDepositAsset)
                                        {
                                            #region 자산 업데이트

                                            Asset pastExpenditureAsset =
                                                await _assetRepository.GetAssetAsync(
                                                    JsonConvert.DeserializeObject<Account>(
                                                        Encoding.Default.GetString(resultByte)).Email,
                                                    previousTempExpenditure.PaymentMethod);
                                            Asset pastIncomeAsset =
                                                await _assetRepository.GetAssetAsync(
                                                    JsonConvert.DeserializeObject<Account>(
                                                        Encoding.Default.GetString(resultByte)).Email,
                                                    previousTempExpenditure.MyDepositAsset);

                                            pastExpenditureAsset.Amount = pastExpenditureAsset.Amount +
                                                                          Math.Abs(previousTempExpenditure.Amount) -
                                                                          Math.Abs(currentTempExpenditure.Amount);
                                            pastExpenditureAsset.Updated = DateTime.UtcNow;
                                            await _assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                            pastIncomeAsset.Amount = pastIncomeAsset.Amount -
                                                                     Math.Abs(previousTempExpenditure.Amount) +
                                                                     Math.Abs(currentTempExpenditure.Amount);
                                            pastIncomeAsset.Updated = DateTime.UtcNow;
                                            await _assetRepository.UpdateAssetAsync(pastIncomeAsset);

                                            #endregion

                                            //#region 자산 업데이트

                                            //#region 과거 자산
                                            //var pastExpenditureAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, previousTempExpenditure.PaymentMethod);
                                            //var pastIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, previousTempExpenditure.MyDepositAsset);

                                            //var currentExpenditureAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.PaymentMethod);
                                            //var currentIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.MyDepositAsset);

                                            //pastExpenditureAsset.Amount += Math.Abs(previousTempExpenditure.Amount);
                                            //pastExpenditureAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                            //pastIncomeAsset.Amount -= Math.Abs(previousTempExpenditure.Amount);
                                            //pastIncomeAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(pastIncomeAsset);
                                            //#endregion

                                            //#region 현재 자산
                                            //currentExpenditureAsset.Amount -= Math.Abs(currentTempExpenditure.Amount);
                                            //currentExpenditureAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(currentExpenditureAsset);

                                            //currentIncomeAsset.Amount += Math.Abs(currentTempExpenditure.Amount);
                                            //currentIncomeAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(currentIncomeAsset);
                                            //#endregion

                                            //#endregion
                                        }
                                        else
                                        {
                                            #region 자산 업데이트

                                            Asset pastExpenditureAsset =
                                                await _assetRepository.GetAssetAsync(
                                                    JsonConvert.DeserializeObject<Account>(
                                                        Encoding.Default.GetString(resultByte)).Email,
                                                    previousTempExpenditure.PaymentMethod);
                                            Asset pastIncomeAsset =
                                                await _assetRepository.GetAssetAsync(
                                                    JsonConvert.DeserializeObject<Account>(
                                                        Encoding.Default.GetString(resultByte)).Email,
                                                    previousTempExpenditure.MyDepositAsset);

                                            Asset currentIncomeAsset =
                                                await _assetRepository.GetAssetAsync(
                                                    JsonConvert.DeserializeObject<Account>(
                                                        Encoding.Default.GetString(resultByte)).Email,
                                                    currentTempExpenditure.MyDepositAsset);

                                            pastExpenditureAsset.Amount = pastExpenditureAsset.Amount +
                                                                          Math.Abs(previousTempExpenditure.Amount) -
                                                                          Math.Abs(currentTempExpenditure.Amount);
                                            pastExpenditureAsset.Updated = DateTime.UtcNow;
                                            await _assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                            pastIncomeAsset.Amount -= Math.Abs(previousTempExpenditure.Amount);
                                            pastIncomeAsset.Updated = DateTime.UtcNow;
                                            await _assetRepository.UpdateAssetAsync(pastIncomeAsset);

                                            currentIncomeAsset.Amount += Math.Abs(currentTempExpenditure.Amount);
                                            currentIncomeAsset.Updated = DateTime.UtcNow;
                                            await _assetRepository.UpdateAssetAsync(currentIncomeAsset);

                                            #endregion

                                            //#region 자산 업데이트

                                            //#region 과거 자산
                                            //var pastExpenditureAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, previousTempExpenditure.PaymentMethod);
                                            //var pastIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, previousTempExpenditure.MyDepositAsset);

                                            //var currentExpenditureAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.PaymentMethod);
                                            //var currentIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.MyDepositAsset);

                                            //pastExpenditureAsset.Amount += Math.Abs(previousTempExpenditure.Amount);
                                            //pastExpenditureAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                            //pastIncomeAsset.Amount -= Math.Abs(previousTempExpenditure.Amount);
                                            //pastIncomeAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(pastIncomeAsset);
                                            //#endregion

                                            //#region 현재 자산
                                            //currentExpenditureAsset.Amount -= Math.Abs(currentTempExpenditure.Amount);
                                            //currentExpenditureAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(currentExpenditureAsset);

                                            //currentIncomeAsset.Amount += Math.Abs(currentTempExpenditure.Amount);
                                            //currentIncomeAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(currentIncomeAsset);
                                            //#endregion

                                            //#endregion
                                        }
                                    }
                                    else if (previousTempExpenditure.PaymentMethod ==
                                             currentTempExpenditure.MyDepositAsset)
                                    {
                                        if (previousTempExpenditure.MyDepositAsset ==
                                            currentTempExpenditure.PaymentMethod)
                                        {
                                            #region 자산 업데이트

                                            Asset pastExpenditureAsset =
                                                await _assetRepository.GetAssetAsync(
                                                    JsonConvert.DeserializeObject<Account>(
                                                        Encoding.Default.GetString(resultByte)).Email,
                                                    previousTempExpenditure.PaymentMethod);
                                            Asset pastIncomeAsset =
                                                await _assetRepository.GetAssetAsync(
                                                    JsonConvert.DeserializeObject<Account>(
                                                        Encoding.Default.GetString(resultByte)).Email,
                                                    previousTempExpenditure.MyDepositAsset);

                                            pastExpenditureAsset.Amount = pastExpenditureAsset.Amount +
                                                                          Math.Abs(previousTempExpenditure.Amount) +
                                                                          Math.Abs(currentTempExpenditure.Amount);
                                            pastExpenditureAsset.Updated = DateTime.UtcNow;
                                            await _assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                            pastIncomeAsset.Amount = pastIncomeAsset.Amount -
                                                                     Math.Abs(previousTempExpenditure.Amount) -
                                                                     Math.Abs(currentTempExpenditure.Amount);
                                            pastIncomeAsset.Updated = DateTime.UtcNow;
                                            await _assetRepository.UpdateAssetAsync(pastIncomeAsset);

                                            #endregion

                                            //#region 자산 업데이트

                                            //#region 과거 자산
                                            //var pastExpenditureAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, previousTempExpenditure.PaymentMethod);
                                            //var pastIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, previousTempExpenditure.MyDepositAsset);

                                            //var currentExpenditureAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.PaymentMethod);
                                            //var currentIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.MyDepositAsset);

                                            //pastExpenditureAsset.Amount += Math.Abs(previousTempExpenditure.Amount);
                                            //pastExpenditureAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                            //pastIncomeAsset.Amount -= Math.Abs(previousTempExpenditure.Amount);
                                            //pastIncomeAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(pastIncomeAsset);
                                            //#endregion

                                            //#region 현재 자산
                                            //currentExpenditureAsset.Amount -= Math.Abs(currentTempExpenditure.Amount);
                                            //currentExpenditureAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(currentExpenditureAsset);

                                            //currentIncomeAsset.Amount += Math.Abs(currentTempExpenditure.Amount);
                                            //currentIncomeAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(currentIncomeAsset);
                                            //#endregion

                                            //#endregion
                                        }
                                        else
                                        {
                                            #region 자산 업데이트

                                            #region 과거 자산

                                            Asset pastExpenditureAsset =
                                                await _assetRepository.GetAssetAsync(
                                                    JsonConvert.DeserializeObject<Account>(
                                                        Encoding.Default.GetString(resultByte)).Email,
                                                    previousTempExpenditure.PaymentMethod);
                                            Asset pastIncomeAsset =
                                                await _assetRepository.GetAssetAsync(
                                                    JsonConvert.DeserializeObject<Account>(
                                                        Encoding.Default.GetString(resultByte)).Email,
                                                    previousTempExpenditure.MyDepositAsset);

                                            Asset currentExpenditureAsset =
                                                await _assetRepository.GetAssetAsync(
                                                    JsonConvert.DeserializeObject<Account>(
                                                        Encoding.Default.GetString(resultByte)).Email,
                                                    currentTempExpenditure.PaymentMethod);

                                            pastExpenditureAsset.Amount = pastExpenditureAsset.Amount +
                                                                          Math.Abs(previousTempExpenditure.Amount) +
                                                                          Math.Abs(currentTempExpenditure.Amount);
                                            pastExpenditureAsset.Updated = DateTime.UtcNow;
                                            await _assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                            pastIncomeAsset.Amount -= Math.Abs(previousTempExpenditure.Amount);
                                            pastIncomeAsset.Updated = DateTime.UtcNow;
                                            await _assetRepository.UpdateAssetAsync(pastIncomeAsset);

                                            #endregion

                                            #region 현재 자산

                                            currentExpenditureAsset.Amount -= Math.Abs(currentTempExpenditure.Amount);
                                            currentExpenditureAsset.Updated = DateTime.UtcNow;
                                            await _assetRepository.UpdateAssetAsync(currentExpenditureAsset);

                                            #endregion

                                            #endregion

                                            //#region 자산 업데이트

                                            //#region 과거 자산
                                            //var pastExpenditureAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, previousTempExpenditure.PaymentMethod);
                                            //var pastIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, previousTempExpenditure.MyDepositAsset);

                                            //var currentExpenditureAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.PaymentMethod);
                                            //var currentIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.MyDepositAsset);

                                            //pastExpenditureAsset.Amount += Math.Abs(previousTempExpenditure.Amount);
                                            //pastExpenditureAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                            //pastIncomeAsset.Amount -= Math.Abs(previousTempExpenditure.Amount);
                                            //pastIncomeAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(pastIncomeAsset);
                                            //#endregion

                                            //#region 현재 자산
                                            //currentExpenditureAsset.Amount -= Math.Abs(currentTempExpenditure.Amount);
                                            //currentExpenditureAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(currentExpenditureAsset);

                                            //currentIncomeAsset.Amount += Math.Abs(currentTempExpenditure.Amount);
                                            //currentIncomeAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(currentIncomeAsset);
                                            //#endregion

                                            //#endregion
                                        }
                                    }
                                    else if (previousTempExpenditure.MyDepositAsset ==
                                             currentTempExpenditure.PaymentMethod)
                                    {
                                        if (previousTempExpenditure.PaymentMethod ==
                                            currentTempExpenditure.MyDepositAsset)
                                        {
                                            #region 자산 업데이트

                                            #region 과거 자산

                                            Asset pastExpenditureAsset =
                                                await _assetRepository.GetAssetAsync(
                                                    JsonConvert.DeserializeObject<Account>(
                                                        Encoding.Default.GetString(resultByte)).Email,
                                                    previousTempExpenditure.PaymentMethod);
                                            Asset pastIncomeAsset =
                                                await _assetRepository.GetAssetAsync(
                                                    JsonConvert.DeserializeObject<Account>(
                                                        Encoding.Default.GetString(resultByte)).Email,
                                                    previousTempExpenditure.MyDepositAsset);

                                            pastExpenditureAsset.Amount = pastExpenditureAsset.Amount +
                                                                          Math.Abs(previousTempExpenditure.Amount) +
                                                                          Math.Abs(currentTempExpenditure.Amount);
                                            pastExpenditureAsset.Updated = DateTime.UtcNow;
                                            await _assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                            pastIncomeAsset.Amount = pastIncomeAsset.Amount -
                                                                     Math.Abs(previousTempExpenditure.Amount) -
                                                                     Math.Abs(currentTempExpenditure.Amount);
                                            pastIncomeAsset.Updated = DateTime.UtcNow;
                                            await _assetRepository.UpdateAssetAsync(pastIncomeAsset);

                                            #endregion

                                            #endregion

                                            //#region 자산 업데이트

                                            //#region 과거 자산
                                            //var pastExpenditureAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, previousTempExpenditure.PaymentMethod);
                                            //var pastIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, previousTempExpenditure.MyDepositAsset);

                                            //var currentExpenditureAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.PaymentMethod);
                                            //var currentIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.MyDepositAsset);

                                            //pastExpenditureAsset.Amount += Math.Abs(previousTempExpenditure.Amount);
                                            //pastExpenditureAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                            //pastIncomeAsset.Amount -= Math.Abs(previousTempExpenditure.Amount);
                                            //pastIncomeAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(pastIncomeAsset);
                                            //#endregion

                                            //#region 현재 자산
                                            //currentExpenditureAsset.Amount -= Math.Abs(currentTempExpenditure.Amount);
                                            //currentExpenditureAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(currentExpenditureAsset);

                                            //currentIncomeAsset.Amount += Math.Abs(currentTempExpenditure.Amount);
                                            //currentIncomeAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(currentIncomeAsset);
                                            //#endregion

                                            //#endregion
                                        }
                                        else
                                        {
                                            #region 자산 업데이트

                                            #region 과거 자산

                                            Asset pastExpenditureAsset =
                                                await _assetRepository.GetAssetAsync(
                                                    JsonConvert.DeserializeObject<Account>(
                                                        Encoding.Default.GetString(resultByte)).Email,
                                                    previousTempExpenditure.PaymentMethod);
                                            Asset pastIncomeAsset =
                                                await _assetRepository.GetAssetAsync(
                                                    JsonConvert.DeserializeObject<Account>(
                                                        Encoding.Default.GetString(resultByte)).Email,
                                                    previousTempExpenditure.MyDepositAsset);

                                            Asset currentIncomeAsset =
                                                await _assetRepository.GetAssetAsync(
                                                    JsonConvert.DeserializeObject<Account>(
                                                        Encoding.Default.GetString(resultByte)).Email,
                                                    currentTempExpenditure.MyDepositAsset);

                                            pastExpenditureAsset.Amount += Math.Abs(previousTempExpenditure.Amount);
                                            pastExpenditureAsset.Updated = DateTime.UtcNow;
                                            await _assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                            pastIncomeAsset.Amount = pastIncomeAsset.Amount -
                                                                     Math.Abs(previousTempExpenditure.Amount) -
                                                                     Math.Abs(currentTempExpenditure.Amount);
                                            pastIncomeAsset.Updated = DateTime.UtcNow;
                                            await _assetRepository.UpdateAssetAsync(pastIncomeAsset);

                                            #endregion

                                            #region 현재 자산

                                            currentIncomeAsset.Amount += Math.Abs(currentTempExpenditure.Amount);
                                            currentIncomeAsset.Updated = DateTime.UtcNow;
                                            await _assetRepository.UpdateAssetAsync(currentIncomeAsset);

                                            #endregion

                                            #endregion

                                            //#region 자산 업데이트

                                            //#region 과거 자산
                                            //var pastExpenditureAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, previousTempExpenditure.PaymentMethod);
                                            //var pastIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, previousTempExpenditure.MyDepositAsset);

                                            //var currentExpenditureAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.PaymentMethod);
                                            //var currentIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.MyDepositAsset);

                                            //pastExpenditureAsset.Amount += Math.Abs(previousTempExpenditure.Amount);
                                            //pastExpenditureAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                            //pastIncomeAsset.Amount -= Math.Abs(previousTempExpenditure.Amount);
                                            //pastIncomeAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(pastIncomeAsset);
                                            //#endregion

                                            //#region 현재 자산
                                            //currentExpenditureAsset.Amount -= Math.Abs(currentTempExpenditure.Amount);
                                            //currentExpenditureAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(currentExpenditureAsset);

                                            //currentIncomeAsset.Amount += Math.Abs(currentTempExpenditure.Amount);
                                            //currentIncomeAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(currentIncomeAsset);
                                            //#endregion

                                            //#endregion
                                        }
                                    }
                                    else if (previousTempExpenditure.MyDepositAsset ==
                                             currentTempExpenditure.MyDepositAsset)
                                    {
                                        if (previousTempExpenditure.PaymentMethod ==
                                            currentTempExpenditure.PaymentMethod)
                                        {
                                            #region 자산 업데이트

                                            #region 과거 자산

                                            Asset pastExpenditureAsset =
                                                await _assetRepository.GetAssetAsync(
                                                    JsonConvert.DeserializeObject<Account>(
                                                        Encoding.Default.GetString(resultByte)).Email,
                                                    previousTempExpenditure.PaymentMethod);
                                            Asset pastIncomeAsset =
                                                await _assetRepository.GetAssetAsync(
                                                    JsonConvert.DeserializeObject<Account>(
                                                        Encoding.Default.GetString(resultByte)).Email,
                                                    previousTempExpenditure.MyDepositAsset);

                                            pastExpenditureAsset.Amount = pastExpenditureAsset.Amount +
                                                                          Math.Abs(previousTempExpenditure.Amount) -
                                                                          Math.Abs(currentTempExpenditure.Amount);
                                            pastExpenditureAsset.Updated = DateTime.UtcNow;
                                            await _assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                            pastIncomeAsset.Amount = pastIncomeAsset.Amount -
                                                                     Math.Abs(previousTempExpenditure.Amount) +
                                                                     Math.Abs(currentTempExpenditure.Amount);
                                            pastIncomeAsset.Updated = DateTime.UtcNow;
                                            await _assetRepository.UpdateAssetAsync(pastIncomeAsset);

                                            #endregion

                                            #endregion

                                            //#region 자산 업데이트

                                            //#region 과거 자산
                                            //var pastExpenditureAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, previousTempExpenditure.PaymentMethod);
                                            //var pastIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, previousTempExpenditure.MyDepositAsset);

                                            //var currentExpenditureAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.PaymentMethod);
                                            //var currentIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.MyDepositAsset);

                                            //pastExpenditureAsset.Amount += Math.Abs(previousTempExpenditure.Amount);
                                            //pastExpenditureAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                            //pastIncomeAsset.Amount -= Math.Abs(previousTempExpenditure.Amount);
                                            //pastIncomeAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(pastIncomeAsset);
                                            //#endregion

                                            //#region 현재 자산
                                            //currentExpenditureAsset.Amount -= Math.Abs(currentTempExpenditure.Amount);
                                            //currentExpenditureAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(currentExpenditureAsset);

                                            //currentIncomeAsset.Amount += Math.Abs(currentTempExpenditure.Amount);
                                            //currentIncomeAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(currentIncomeAsset);
                                            //#endregion

                                            //#endregion
                                        }
                                        else
                                        {
                                            #region 자산 업데이트

                                            #region 과거 자산

                                            Asset pastExpenditureAsset =
                                                await _assetRepository.GetAssetAsync(
                                                    JsonConvert.DeserializeObject<Account>(
                                                        Encoding.Default.GetString(resultByte)).Email,
                                                    previousTempExpenditure.PaymentMethod);
                                            Asset pastIncomeAsset =
                                                await _assetRepository.GetAssetAsync(
                                                    JsonConvert.DeserializeObject<Account>(
                                                        Encoding.Default.GetString(resultByte)).Email,
                                                    previousTempExpenditure.MyDepositAsset);

                                            Asset currentExpenditureAsset =
                                                await _assetRepository.GetAssetAsync(
                                                    JsonConvert.DeserializeObject<Account>(
                                                        Encoding.Default.GetString(resultByte)).Email,
                                                    currentTempExpenditure.PaymentMethod);

                                            pastExpenditureAsset.Amount += Math.Abs(previousTempExpenditure.Amount);
                                            pastExpenditureAsset.Updated = DateTime.UtcNow;
                                            await _assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                            pastIncomeAsset.Amount = pastIncomeAsset.Amount -
                                                                     Math.Abs(previousTempExpenditure.Amount) +
                                                                     Math.Abs(currentTempExpenditure.Amount);
                                            pastIncomeAsset.Updated = DateTime.UtcNow;
                                            await _assetRepository.UpdateAssetAsync(pastIncomeAsset);

                                            #endregion

                                            #region 현재 자산

                                            currentExpenditureAsset.Amount -= Math.Abs(currentTempExpenditure.Amount);
                                            currentExpenditureAsset.Updated = DateTime.UtcNow;
                                            await _assetRepository.UpdateAssetAsync(currentExpenditureAsset);

                                            #endregion

                                            #endregion

                                            //#region 자산 업데이트

                                            //#region 과거 자산
                                            //var pastExpenditureAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, previousTempExpenditure.PaymentMethod);
                                            //var pastIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, previousTempExpenditure.MyDepositAsset);

                                            //var currentExpenditureAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.PaymentMethod);
                                            //var currentIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.MyDepositAsset);

                                            //pastExpenditureAsset.Amount += Math.Abs(previousTempExpenditure.Amount);
                                            //pastExpenditureAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                            //pastIncomeAsset.Amount -= Math.Abs(previousTempExpenditure.Amount);
                                            //pastIncomeAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(pastIncomeAsset);
                                            //#endregion

                                            //#region 현재 자산
                                            //currentExpenditureAsset.Amount -= Math.Abs(currentTempExpenditure.Amount);
                                            //currentExpenditureAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(currentExpenditureAsset);

                                            //currentIncomeAsset.Amount += Math.Abs(currentTempExpenditure.Amount);
                                            //currentIncomeAsset.Updated = DateTime.UtcNow;
                                            //await assetRepository.UpdateAssetAsync(currentIncomeAsset);
                                            //#endregion

                                            //#endregion
                                        }
                                    }
                                    else
                                    {
                                        #region 자산 업데이트

                                        #region 과거 자산

                                        Asset pastExpenditureAsset =
                                            await _assetRepository.GetAssetAsync(
                                                JsonConvert.DeserializeObject<Account>(
                                                    Encoding.Default.GetString(resultByte)).Email,
                                                previousTempExpenditure.PaymentMethod);
                                        Asset pastIncomeAsset = await _assetRepository.GetAssetAsync(
                                            JsonConvert.DeserializeObject<Account>(
                                                Encoding.Default.GetString(resultByte)).Email,
                                            previousTempExpenditure.MyDepositAsset);

                                        Asset currentExpenditureAsset =
                                            await _assetRepository.GetAssetAsync(
                                                JsonConvert.DeserializeObject<Account>(
                                                    Encoding.Default.GetString(resultByte)).Email,
                                                currentTempExpenditure.PaymentMethod);
                                        Asset currentIncomeAsset =
                                            await _assetRepository.GetAssetAsync(
                                                JsonConvert.DeserializeObject<Account>(
                                                    Encoding.Default.GetString(resultByte)).Email,
                                                currentTempExpenditure.MyDepositAsset);

                                        pastExpenditureAsset.Amount += Math.Abs(previousTempExpenditure.Amount);
                                        pastExpenditureAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                        pastIncomeAsset.Amount -= Math.Abs(previousTempExpenditure.Amount);
                                        pastIncomeAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(pastIncomeAsset);

                                        #endregion

                                        #region 현재 자산

                                        currentExpenditureAsset.Amount -= Math.Abs(currentTempExpenditure.Amount);
                                        currentExpenditureAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(currentExpenditureAsset);

                                        currentIncomeAsset.Amount += Math.Abs(currentTempExpenditure.Amount);
                                        currentIncomeAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(currentIncomeAsset);

                                        #endregion

                                        #endregion
                                    }

                                    return Json(new
                                    {
                                        result = true,
                                        message = _localizer["The expenditure has been successfully updated."].Value
                                    });
                                }

                                #endregion

                                #region 잘못된 경우

                                else
                                {
                                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                                }

                                #endregion
                            }
                            catch
                            {
                                return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                            }
                        }
                        else if (expenditureInputViewModel.SubClass is "Tax" or
                                 "SocialInsurance" or
                                 "InterHouseholdTranserExpenses" or
                                 "NonProfitOrganizationTransfer")
                        {
                            try
                            {
                                List<Expenditure> tempExpenditures =
                                    await _expenditureRepository.GetExpendituresAsync(JsonConvert
                                        .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);
                                Expenditure previousTempExpenditure = tempExpenditures
                                    .Where(a => a.Id == expenditureInputViewModel.Id).FirstOrDefault();
                                Expenditure currentTempExpenditure = new()
                                {
                                    Id = expenditureInputViewModel.Id,
                                    AccountEmail = JsonConvert
                                        .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email,
                                    MainClass = expenditureInputViewModel.MainClass,
                                    SubClass = expenditureInputViewModel.SubClass,
                                    Content = expenditureInputViewModel?.Content ?? "",
                                    Amount = expenditureInputViewModel.Amount,
                                    PaymentMethod = expenditureInputViewModel.PaymentMethod,
                                    MyDepositAsset = "",
                                    Created = expenditureInputViewModel.Created,
                                    Updated = DateTime.UtcNow,
                                    Note = expenditureInputViewModel?.Note ?? "",
                                    Asset = previousTempExpenditure.Asset
                                };

                                #region Validate ProductName's Deletion

                                Asset tempExpenditureAssetToValidate =
                                    await _assetRepository.GetAssetAsync(
                                        JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte))
                                            .Email, previousTempExpenditure.PaymentMethod);
                                if (tempExpenditureAssetToValidate.Deleted)
                                {
                                    return Json(new
                                    {
                                        result = false,
                                        error = _localizer[
                                                "Actions cannot be executed with assets that have already been deleted."]
                                            .Value
                                    });
                                }

                                if (!string.IsNullOrEmpty(previousTempExpenditure.MyDepositAsset))
                                {
                                    Asset tempIncomeAssetToValidate =
                                        await _assetRepository.GetAssetAsync(
                                            JsonConvert.DeserializeObject<Account>(
                                                Encoding.Default.GetString(resultByte)).Email,
                                            previousTempExpenditure.MyDepositAsset);
                                    if (tempIncomeAssetToValidate.Deleted)
                                    {
                                        return Json(new
                                        {
                                            result = false,
                                            error = _localizer[
                                                    "Actions cannot be executed with assets that have already been deleted."]
                                                .Value
                                        });
                                    }
                                }

                                #endregion

                                #region 과거 지출 값 [(결제수단(O)), (내입금자산(X))], 현재 지출 값 [(결제수단(O)), (내입금자산(X))]

                                if (!string.IsNullOrEmpty(previousTempExpenditure.PaymentMethod) &&
                                    string.IsNullOrEmpty(previousTempExpenditure.MyDepositAsset) &&
                                    !string.IsNullOrEmpty(currentTempExpenditure.PaymentMethod) &&
                                    string.IsNullOrEmpty(currentTempExpenditure.MyDepositAsset))
                                {
                                    #region 지출 업데이트

                                    await _expenditureRepository.UpdateExpenditureAsync(currentTempExpenditure);

                                    #endregion

                                    if (previousTempExpenditure.PaymentMethod == currentTempExpenditure.PaymentMethod)
                                    {
                                        #region 자산 업데이트

                                        #region 과거 자산

                                        Asset pastExpenditureAsset =
                                            await _assetRepository.GetAssetAsync(
                                                JsonConvert.DeserializeObject<Account>(
                                                    Encoding.Default.GetString(resultByte)).Email,
                                                previousTempExpenditure.PaymentMethod);
                                        pastExpenditureAsset.Amount = pastExpenditureAsset.Amount +
                                                                      Math.Abs(previousTempExpenditure.Amount) -
                                                                      Math.Abs(currentTempExpenditure.Amount);
                                        pastExpenditureAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                        #endregion

                                        #endregion
                                    }
                                    else
                                    {
                                        #region 자산 업데이트

                                        #region 과거 자산

                                        Asset pastExpenditureAsset =
                                            await _assetRepository.GetAssetAsync(
                                                JsonConvert.DeserializeObject<Account>(
                                                    Encoding.Default.GetString(resultByte)).Email,
                                                previousTempExpenditure.PaymentMethod);
                                        //var pastIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, previousTempExpenditure.MyDepositAsset);

                                        Asset currentExpenditureAsset =
                                            await _assetRepository.GetAssetAsync(
                                                JsonConvert.DeserializeObject<Account>(
                                                    Encoding.Default.GetString(resultByte)).Email,
                                                currentTempExpenditure.PaymentMethod);
                                        //var currentIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.MyDepositAsset);

                                        pastExpenditureAsset.Amount += Math.Abs(previousTempExpenditure.Amount);
                                        pastExpenditureAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                        //pastIncomeAsset.Amount -= Math.Abs(previousTempExpenditure.Amount);
                                        //pastIncomeAsset.Updated = DateTime.UtcNow;
                                        //await assetRepository.UpdateAssetAsync(pastIncomeAsset);

                                        #endregion

                                        #region 현재 자산

                                        currentExpenditureAsset.Amount -= Math.Abs(currentTempExpenditure.Amount);
                                        currentExpenditureAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(currentExpenditureAsset);

                                        //currentIncomeAsset.Amount += Math.Abs(currentTempExpenditure.Amount);
                                        //currentIncomeAsset.Updated = DateTime.UtcNow;
                                        //await assetRepository.UpdateAssetAsync(currentIncomeAsset);

                                        #endregion

                                        #endregion
                                    }

                                    return Json(new
                                    {
                                        result = true,
                                        message = _localizer["The expenditure has been successfully updated."].Value
                                    });
                                }

                                #endregion

                                #region 과거 지출 값 [(결제수단(O)), (내입금자산(O))], 현재 지출 값 [(결제수단(O)), (내입금자산(X))]

                                else if (!string.IsNullOrEmpty(previousTempExpenditure.PaymentMethod) &&
                                         !string.IsNullOrEmpty(previousTempExpenditure.MyDepositAsset) &&
                                         !string.IsNullOrEmpty(currentTempExpenditure.PaymentMethod) &&
                                         string.IsNullOrEmpty(currentTempExpenditure.MyDepositAsset))
                                {
                                    #region 지출 업데이트

                                    await _expenditureRepository.UpdateExpenditureAsync(currentTempExpenditure);

                                    #endregion

                                    if (previousTempExpenditure.PaymentMethod == currentTempExpenditure.PaymentMethod)
                                    {
                                        #region 자산 업데이트

                                        #region 과거 자산

                                        Asset pastExpenditureAsset =
                                            await _assetRepository.GetAssetAsync(
                                                JsonConvert.DeserializeObject<Account>(
                                                    Encoding.Default.GetString(resultByte)).Email,
                                                previousTempExpenditure.PaymentMethod);
                                        Asset pastIncomeAsset = await _assetRepository.GetAssetAsync(
                                            JsonConvert.DeserializeObject<Account>(
                                                Encoding.Default.GetString(resultByte)).Email,
                                            previousTempExpenditure.MyDepositAsset);

                                        pastExpenditureAsset.Amount = pastExpenditureAsset.Amount +
                                                                      Math.Abs(previousTempExpenditure.Amount) -
                                                                      Math.Abs(currentTempExpenditure.Amount);
                                        pastExpenditureAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                        pastIncomeAsset.Amount -= Math.Abs(previousTempExpenditure.Amount);
                                        pastIncomeAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(pastIncomeAsset);

                                        #endregion

                                        #endregion
                                    }
                                    else if (previousTempExpenditure.MyDepositAsset ==
                                             currentTempExpenditure.PaymentMethod)
                                    {
                                        #region 자산 업데이트

                                        #region 과거 자산

                                        Asset pastExpenditureAsset =
                                            await _assetRepository.GetAssetAsync(
                                                JsonConvert.DeserializeObject<Account>(
                                                    Encoding.Default.GetString(resultByte)).Email,
                                                previousTempExpenditure.PaymentMethod);
                                        Asset pastIncomeAsset = await _assetRepository.GetAssetAsync(
                                            JsonConvert.DeserializeObject<Account>(
                                                Encoding.Default.GetString(resultByte)).Email,
                                            previousTempExpenditure.MyDepositAsset);

                                        pastExpenditureAsset.Amount += Math.Abs(previousTempExpenditure.Amount);
                                        pastExpenditureAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                        pastIncomeAsset.Amount = pastIncomeAsset.Amount -
                                                                 Math.Abs(previousTempExpenditure.Amount) -
                                                                 Math.Abs(currentTempExpenditure.Amount);
                                        pastIncomeAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(pastIncomeAsset);

                                        #endregion

                                        #endregion
                                    }
                                    else
                                    {
                                        #region 자산 업데이트

                                        #region 과거 자산

                                        Asset pastExpenditureAsset =
                                            await _assetRepository.GetAssetAsync(
                                                JsonConvert.DeserializeObject<Account>(
                                                    Encoding.Default.GetString(resultByte)).Email,
                                                previousTempExpenditure.PaymentMethod);
                                        Asset pastIncomeAsset = await _assetRepository.GetAssetAsync(
                                            JsonConvert.DeserializeObject<Account>(
                                                Encoding.Default.GetString(resultByte)).Email,
                                            previousTempExpenditure.MyDepositAsset);

                                        Asset currentExpenditureAsset =
                                            await _assetRepository.GetAssetAsync(
                                                JsonConvert.DeserializeObject<Account>(
                                                    Encoding.Default.GetString(resultByte)).Email,
                                                currentTempExpenditure.PaymentMethod);

                                        pastExpenditureAsset.Amount += Math.Abs(previousTempExpenditure.Amount);
                                        pastExpenditureAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                        pastIncomeAsset.Amount -= Math.Abs(previousTempExpenditure.Amount);
                                        pastIncomeAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(pastIncomeAsset);

                                        #endregion

                                        #region 현재 자산

                                        currentExpenditureAsset.Amount -= Math.Abs(currentTempExpenditure.Amount);
                                        currentExpenditureAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(currentExpenditureAsset);

                                        #endregion

                                        #endregion
                                    }

                                    return Json(new
                                    {
                                        result = true,
                                        message = _localizer["The expenditure has been successfully updated."].Value
                                    });
                                }

                                #endregion

                                #region 잘못된 경우

                                else
                                {
                                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
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

                    #endregion

                    #region 대분류 = '소비지출'

                    else if (expenditureInputViewModel.MainClass == "ConsumerSpending")
                    {
                        if (expenditureInputViewModel.SubClass is "MealOrEatOutExpenses" or
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
                                List<Expenditure> tempExpenditures =
                                    await _expenditureRepository.GetExpendituresAsync(JsonConvert
                                        .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);
                                Expenditure previousTempExpenditure = tempExpenditures
                                    .Where(a => a.Id == expenditureInputViewModel.Id).FirstOrDefault();
                                Expenditure currentTempExpenditure = new()
                                {
                                    Id = expenditureInputViewModel.Id,
                                    AccountEmail = JsonConvert
                                        .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email,
                                    MainClass = expenditureInputViewModel.MainClass,
                                    SubClass = expenditureInputViewModel.SubClass,
                                    Content = expenditureInputViewModel?.Content ?? "",
                                    Amount = expenditureInputViewModel.Amount,
                                    PaymentMethod = expenditureInputViewModel.PaymentMethod,
                                    MyDepositAsset = "",
                                    Created = expenditureInputViewModel.Created,
                                    Updated = DateTime.UtcNow,
                                    Note = expenditureInputViewModel?.Note ?? "",
                                    Asset = previousTempExpenditure.Asset
                                };

                                #region Validate ProductName's Deletion

                                Asset tempExpenditureAssetToValidate =
                                    await _assetRepository.GetAssetAsync(
                                        JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte))
                                            .Email, previousTempExpenditure.PaymentMethod);
                                if (tempExpenditureAssetToValidate.Deleted)
                                {
                                    return Json(new
                                    {
                                        result = false,
                                        error = _localizer[
                                                "Actions cannot be executed with assets that have already been deleted."]
                                            .Value
                                    });
                                }

                                if (!string.IsNullOrEmpty(previousTempExpenditure.MyDepositAsset))
                                {
                                    Asset tempIncomeAssetToValidate =
                                        await _assetRepository.GetAssetAsync(
                                            JsonConvert.DeserializeObject<Account>(
                                                Encoding.Default.GetString(resultByte)).Email,
                                            previousTempExpenditure.MyDepositAsset);
                                    if (tempIncomeAssetToValidate.Deleted)
                                    {
                                        return Json(new
                                        {
                                            result = false,
                                            error = _localizer[
                                                    "Actions cannot be executed with assets that have already been deleted."]
                                                .Value
                                        });
                                    }
                                }

                                #endregion

                                #region 과거 지출 값 [(결제수단(O)), (내입금자산(X))], 현재 지출 값 [(결제수단(O)), (내입금자산(X))]

                                if (!string.IsNullOrEmpty(previousTempExpenditure.PaymentMethod) &&
                                    string.IsNullOrEmpty(previousTempExpenditure.MyDepositAsset) &&
                                    !string.IsNullOrEmpty(currentTempExpenditure.PaymentMethod) &&
                                    string.IsNullOrEmpty(currentTempExpenditure.MyDepositAsset))
                                {
                                    #region 지출 업데이트

                                    await _expenditureRepository.UpdateExpenditureAsync(currentTempExpenditure);

                                    #endregion

                                    if (previousTempExpenditure.PaymentMethod == currentTempExpenditure.PaymentMethod)
                                    {
                                        #region 자산 업데이트

                                        #region 과거 자산

                                        Asset pastExpenditureAsset =
                                            await _assetRepository.GetAssetAsync(
                                                JsonConvert.DeserializeObject<Account>(
                                                    Encoding.Default.GetString(resultByte)).Email,
                                                previousTempExpenditure.PaymentMethod);
                                        //var pastIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, previousTempExpenditure.MyDepositAsset);

                                        //var currentExpenditureAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.PaymentMethod);
                                        //var currentIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.MyDepositAsset);

                                        pastExpenditureAsset.Amount = pastExpenditureAsset.Amount +
                                                                      Math.Abs(previousTempExpenditure.Amount) -
                                                                      Math.Abs(currentTempExpenditure.Amount);
                                        pastExpenditureAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                        //pastIncomeAsset.Amount -= Math.Abs(previousTempExpenditure.Amount);
                                        //pastIncomeAsset.Updated = DateTime.UtcNow;
                                        //await assetRepository.UpdateAssetAsync(pastIncomeAsset);

                                        #endregion

                                        #region 현재 자산

                                        //currentExpenditureAsset.Amount -= Math.Abs(currentTempExpenditure.Amount);
                                        //currentExpenditureAsset.Updated = DateTime.UtcNow;
                                        //await assetRepository.UpdateAssetAsync(currentExpenditureAsset);

                                        //currentIncomeAsset.Amount += Math.Abs(currentTempExpenditure.Amount);
                                        //currentIncomeAsset.Updated = DateTime.UtcNow;
                                        //await assetRepository.UpdateAssetAsync(currentIncomeAsset);

                                        #endregion

                                        #endregion
                                    }
                                    else
                                    {
                                        #region 자산 업데이트

                                        #region 과거 자산

                                        Asset pastExpenditureAsset =
                                            await _assetRepository.GetAssetAsync(
                                                JsonConvert.DeserializeObject<Account>(
                                                    Encoding.Default.GetString(resultByte)).Email,
                                                previousTempExpenditure.PaymentMethod);
                                        //var pastIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, previousTempExpenditure.MyDepositAsset);

                                        Asset currentExpenditureAsset =
                                            await _assetRepository.GetAssetAsync(
                                                JsonConvert.DeserializeObject<Account>(
                                                    Encoding.Default.GetString(resultByte)).Email,
                                                currentTempExpenditure.PaymentMethod);
                                        //var currentIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.MyDepositAsset);

                                        pastExpenditureAsset.Amount += Math.Abs(previousTempExpenditure.Amount);
                                        pastExpenditureAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                        //pastIncomeAsset.Amount -= Math.Abs(previousTempExpenditure.Amount);
                                        //pastIncomeAsset.Updated = DateTime.UtcNow;
                                        //await assetRepository.UpdateAssetAsync(pastIncomeAsset);

                                        #endregion

                                        #region 현재 자산

                                        currentExpenditureAsset.Amount -= Math.Abs(currentTempExpenditure.Amount);
                                        currentExpenditureAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(currentExpenditureAsset);

                                        //currentIncomeAsset.Amount += Math.Abs(currentTempExpenditure.Amount);
                                        //currentIncomeAsset.Updated = DateTime.UtcNow;
                                        //await assetRepository.UpdateAssetAsync(currentIncomeAsset);

                                        #endregion

                                        #endregion
                                    }

                                    return Json(new
                                    {
                                        result = true,
                                        message = _localizer["The expenditure has been successfully updated."].Value
                                    });
                                }

                                #endregion

                                #region 과거 지출 값 [(결제수단(O)), (내입금자산(O))], 현재 지출 값 [(결제수단(O)), (내입금자산(X))]

                                else if (!string.IsNullOrEmpty(previousTempExpenditure.PaymentMethod) &&
                                         !string.IsNullOrEmpty(previousTempExpenditure.MyDepositAsset) &&
                                         !string.IsNullOrEmpty(currentTempExpenditure.PaymentMethod) &&
                                         string.IsNullOrEmpty(currentTempExpenditure.MyDepositAsset))
                                {
                                    #region 지출 업데이트

                                    await _expenditureRepository.UpdateExpenditureAsync(currentTempExpenditure);

                                    #endregion

                                    if (previousTempExpenditure.PaymentMethod == currentTempExpenditure.PaymentMethod)
                                    {
                                        #region 자산 업데이트

                                        #region 과거 자산

                                        Asset pastExpenditureAsset =
                                            await _assetRepository.GetAssetAsync(
                                                JsonConvert.DeserializeObject<Account>(
                                                    Encoding.Default.GetString(resultByte)).Email,
                                                previousTempExpenditure.PaymentMethod);
                                        Asset pastIncomeAsset = await _assetRepository.GetAssetAsync(
                                            JsonConvert.DeserializeObject<Account>(
                                                Encoding.Default.GetString(resultByte)).Email,
                                            previousTempExpenditure.MyDepositAsset);

                                        //var currentExpenditureAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.PaymentMethod);
                                        //var currentIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.MyDepositAsset);

                                        pastExpenditureAsset.Amount = pastExpenditureAsset.Amount +
                                                                      Math.Abs(previousTempExpenditure.Amount) -
                                                                      Math.Abs(currentTempExpenditure.Amount);
                                        pastExpenditureAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                        pastIncomeAsset.Amount -= Math.Abs(previousTempExpenditure.Amount);
                                        pastIncomeAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(pastIncomeAsset);

                                        #endregion

                                        #region 현재 자산

                                        //currentExpenditureAsset.Amount -= Math.Abs(currentTempExpenditure.Amount);
                                        //currentExpenditureAsset.Updated = DateTime.UtcNow;
                                        //await assetRepository.UpdateAssetAsync(currentExpenditureAsset);

                                        //currentIncomeAsset.Amount += Math.Abs(currentTempExpenditure.Amount);
                                        //currentIncomeAsset.Updated = DateTime.UtcNow;
                                        //await assetRepository.UpdateAssetAsync(currentIncomeAsset);

                                        #endregion

                                        #endregion
                                    }
                                    else if (previousTempExpenditure.MyDepositAsset ==
                                             currentTempExpenditure.PaymentMethod)
                                    {
                                        #region 자산 업데이트

                                        #region 과거 자산

                                        Asset pastExpenditureAsset =
                                            await _assetRepository.GetAssetAsync(
                                                JsonConvert.DeserializeObject<Account>(
                                                    Encoding.Default.GetString(resultByte)).Email,
                                                previousTempExpenditure.PaymentMethod);
                                        Asset pastIncomeAsset = await _assetRepository.GetAssetAsync(
                                            JsonConvert.DeserializeObject<Account>(
                                                Encoding.Default.GetString(resultByte)).Email,
                                            previousTempExpenditure.MyDepositAsset);

                                        //var currentExpenditureAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.PaymentMethod);
                                        //var currentIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.MyDepositAsset);

                                        pastExpenditureAsset.Amount += Math.Abs(previousTempExpenditure.Amount);
                                        pastExpenditureAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                        pastIncomeAsset.Amount = pastIncomeAsset.Amount -
                                                                 Math.Abs(previousTempExpenditure.Amount) -
                                                                 Math.Abs(currentTempExpenditure.Amount);
                                        pastIncomeAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(pastIncomeAsset);

                                        #endregion

                                        #region 현재 자산

                                        //currentExpenditureAsset.Amount -= Math.Abs(currentTempExpenditure.Amount);
                                        //currentExpenditureAsset.Updated = DateTime.UtcNow;
                                        //await assetRepository.UpdateAssetAsync(currentExpenditureAsset);

                                        //currentIncomeAsset.Amount += Math.Abs(currentTempExpenditure.Amount);
                                        //currentIncomeAsset.Updated = DateTime.UtcNow;
                                        //await assetRepository.UpdateAssetAsync(currentIncomeAsset);

                                        #endregion

                                        #endregion
                                    }
                                    else
                                    {
                                        #region 자산 업데이트

                                        #region 과거 자산

                                        Asset pastExpenditureAsset =
                                            await _assetRepository.GetAssetAsync(
                                                JsonConvert.DeserializeObject<Account>(
                                                    Encoding.Default.GetString(resultByte)).Email,
                                                previousTempExpenditure.PaymentMethod);
                                        Asset pastIncomeAsset = await _assetRepository.GetAssetAsync(
                                            JsonConvert.DeserializeObject<Account>(
                                                Encoding.Default.GetString(resultByte)).Email,
                                            previousTempExpenditure.MyDepositAsset);

                                        Asset currentExpenditureAsset =
                                            await _assetRepository.GetAssetAsync(
                                                JsonConvert.DeserializeObject<Account>(
                                                    Encoding.Default.GetString(resultByte)).Email,
                                                currentTempExpenditure.PaymentMethod);
                                        //var currentIncomeAsset = await assetRepository.GetAssetAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email, currentTempExpenditure.MyDepositAsset);

                                        pastExpenditureAsset.Amount += Math.Abs(previousTempExpenditure.Amount);
                                        pastExpenditureAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(pastExpenditureAsset);

                                        pastIncomeAsset.Amount -= Math.Abs(previousTempExpenditure.Amount);
                                        pastIncomeAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(pastIncomeAsset);

                                        #endregion

                                        #region 현재 자산

                                        currentExpenditureAsset.Amount -= Math.Abs(currentTempExpenditure.Amount);
                                        currentExpenditureAsset.Updated = DateTime.UtcNow;
                                        await _assetRepository.UpdateAssetAsync(currentExpenditureAsset);

                                        //currentIncomeAsset.Amount += Math.Abs(currentTempExpenditure.Amount);
                                        //currentIncomeAsset.Updated = DateTime.UtcNow;
                                        //await assetRepository.UpdateAssetAsync(currentIncomeAsset);

                                        #endregion

                                        #endregion
                                    }

                                    return Json(new
                                    {
                                        result = true,
                                        message = _localizer["The expenditure has been successfully updated."].Value
                                    });
                                }

                                #endregion

                                #region 잘못된 경우

                                else
                                {
                                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
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
        public async Task<IActionResult> DeleteExpenditure(
            [FromBody] ExpenditureInputViewModel expenditureInputViewModel)
        {
            try
            {
                _ = HttpContext.Session.TryGetValue(
                    Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous
                        .Utilities.Session.Account), out byte[] resultByte);

                List<Expenditure> tempExpenditures =
                    await _expenditureRepository.GetExpendituresAsync(JsonConvert
                        .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);

                if (tempExpenditures == null)
                {
                    return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                }
                else
                {
                    Expenditure tempExpenditure = tempExpenditures.Where(a => a.Id == expenditureInputViewModel.Id)
                        .FirstOrDefault();
                    if (tempExpenditure == null)
                    {
                        return Json(new { result = false, error = _localizer["Input is invalid"].Value });
                    }
                    else
                    {
                        #region Validate ProductName's Deletion

                        Asset tempExpenditureAssetToValidate = await _assetRepository.GetAssetAsync(
                            JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email,
                            tempExpenditure.PaymentMethod);
                        if (tempExpenditureAssetToValidate.Deleted)
                        {
                            return Json(new
                            {
                                result = false,
                                error = _localizer[
                                    "Actions cannot be executed with assets that have already been deleted."].Value
                            });
                        }

                        if (!string.IsNullOrEmpty(tempExpenditure.MyDepositAsset))
                        {
                            Asset tempIncomeAssetToValidate = await _assetRepository.GetAssetAsync(
                                JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email,
                                tempExpenditure.MyDepositAsset);
                            if (tempIncomeAssetToValidate.Deleted)
                            {
                                return Json(new
                                {
                                    result = false,
                                    error = _localizer[
                                        "Actions cannot be executed with assets that have already been deleted."].Value
                                });
                            }
                        }

                        #endregion

                        if (!string.IsNullOrEmpty(tempExpenditure.MyDepositAsset))
                        {
                            #region 지출 삭제

                            await _expenditureRepository.DeleteExpenditureAsync(tempExpenditure);

                            #endregion

                            #region 자산 업데이트

                            Asset expenditureAsset = await _assetRepository.GetAssetAsync(
                                JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email,
                                tempExpenditure.PaymentMethod);
                            Asset incomeAsset = await _assetRepository.GetAssetAsync(
                                JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email,
                                tempExpenditure.MyDepositAsset);

                            expenditureAsset.Amount += Math.Abs(tempExpenditure.Amount);
                            expenditureAsset.Updated = DateTime.UtcNow;
                            await _assetRepository.UpdateAssetAsync(expenditureAsset);

                            incomeAsset.Amount -= Math.Abs(tempExpenditure.Amount);
                            incomeAsset.Updated = DateTime.UtcNow;
                            await _assetRepository.UpdateAssetAsync(incomeAsset);

                            #endregion

                            return Json(new
                            {
                                result = true,
                                message = _localizer["The expenditure has been successfully deleted."].Value
                            });
                        }
                        else
                        {
                            #region 지출 삭제

                            await _expenditureRepository.DeleteExpenditureAsync(tempExpenditure);

                            #endregion

                            #region 자산 업데이트

                            Asset expenditureAsset = await _assetRepository.GetAssetAsync(
                                JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email,
                                tempExpenditure.PaymentMethod);

                            expenditureAsset.Amount += Math.Abs(tempExpenditure.Amount);
                            expenditureAsset.Updated = DateTime.UtcNow;
                            await _assetRepository.UpdateAssetAsync(expenditureAsset);

                            #endregion

                            return Json(new
                            {
                                result = true,
                                message = _localizer["The expenditure has been successfully deleted."].Value
                            });
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

        #region Excel

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> ExportExcelExpenditure(string fileName = "")
        {
            await Task.Yield();

            List<ExpenditureOutputViewModel> expenditureOutputViewModels = new List<ExpenditureOutputViewModel>();

            #region Expenditures

            _ = HttpContext.Session.TryGetValue(
                Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous.Utilities
                    .Session.Account), out byte[] resultByte);
            string loginedAccountTimeZoneIanaId = JsonConvert
                .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).TimeZoneIanaId;

            List<Asset> assets = await _assetRepository.GetAssetsAsync(JsonConvert
                .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);

            foreach (Expenditure item in await _expenditureRepository.GetExpendituresAsync(JsonConvert
                         .DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email))
            {
                expenditureOutputViewModels.Add(new ExpenditureOutputViewModel()
                {
                    MainClass = _localizer[item.MainClass.ToString()].Value,
                    SubClass = _localizer[item.SubClass.ToString()].Value,
                    Content = item.Content,
                    Amount = Convert.ToDecimal(item.Amount.TrimTrailingZeros()),
                    MonetaryUnit = assets.Where(x => x.ProductName == item.PaymentMethod).FirstOrDefault().MonetaryUnit,
                    PaymentMethod = item.PaymentMethod,
                    Note = item.Note,
                    MyDepositAsset = item.MyDepositAsset,
                    Created = item.Created.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId),
                    Updated = item.Updated.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId)
                });
            }

            expenditureOutputViewModels = expenditureOutputViewModels.OrderByDescending(m => m.Created)
                .ThenByDescending(m => m.Updated).ToList();

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
                    CreateCell(_localizer[nameof(ExpenditureOutputViewModel.MainClass)].Value),
                    CreateCell(_localizer[nameof(ExpenditureOutputViewModel.SubClass)].Value),
                    CreateCell(_localizer[nameof(ExpenditureOutputViewModel.Content)].Value),
                    CreateCell(_localizer[nameof(ExpenditureOutputViewModel.Amount)].Value),
                    CreateCell(_localizer[nameof(ExpenditureOutputViewModel.MonetaryUnit)].Value),
                    CreateCell(_localizer[nameof(ExpenditureOutputViewModel.PaymentMethod)].Value),
                    CreateCell(_localizer[nameof(ExpenditureOutputViewModel.Note)].Value),
                    CreateCell(_localizer[nameof(ExpenditureOutputViewModel.MyDepositAsset)].Value),
                    CreateCell(_localizer[nameof(ExpenditureOutputViewModel.Created)].Value),
                    CreateCell(_localizer[nameof(ExpenditureOutputViewModel.Updated)].Value)
                );
                sheetData.Append(headerRow);

                foreach (var item in expenditureOutputViewModels)
                {
                    Row dataRow = new Row();
                    dataRow.Append(
                        CreateCell(_localizer[item?.MainClass?.ToString() ?? ""].Value),
                        CreateCell(_localizer[item?.SubClass?.ToString() ?? ""].Value),
                        CreateCell(item.Content),
                        CreateCell(Convert.ToDecimal(item.Amount.TrimTrailingZeros()).ToString()),
                        CreateCell(item.MonetaryUnit),
                        CreateCell(item.PaymentMethod),
                        CreateCell(item.Note),
                        CreateCell(item.MyDepositAsset),
                        CreateCell(item.Created.ToString()),
                        CreateCell(item.Updated.ToString())
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