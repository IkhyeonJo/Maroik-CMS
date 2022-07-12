using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MyLaboratory.WebSite.Models;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System;
using Microsoft.EntityFrameworkCore;
using MyLaboratory.WebSite.Services;
using Microsoft.AspNetCore.Authorization;
using MyLaboratory.WebSite.Common;
using System.Linq;
using NonFactors.Mvc.Grid;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Http;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using System.Threading;
using OfficeOpenXml;
using MyLaboratory.WebSite.Contracts;
using MyLaboratory.WebSite.Models.ViewModels.Management;
using MyLaboratory.WebSite.Helpers;
using Newtonsoft.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Net.Mail;
using Microsoft.AspNetCore.Mvc.Localization;
using MyLaboratory.WebSite.Models.ViewModels.AccountBook;
using MyLaboratory.WebSite.Models.ViewModels.Notice;
using MyLaboratory.Common.DataAccess.Data;
using MyLaboratory.Common.DataAccess.Models;
using MyLaboratory.Common.DataAccess.Contracts;
using MyLaboratory.Common.DataAccess.Services;

namespace MyLaboratory.WebSite.Controllers
{
    public class NoticeController : Controller
    {
        private readonly IHtmlLocalizer<NoticeController> localizer;
        private readonly ILogger<NoticeController> logger;
        private readonly IAccountRepository accountRepository;
        private readonly IHostEnvironment hostEnvironment;
        private readonly ICategoryRepository categoryRepository;
        private readonly ISubCategoryRepository subCategoryRepository;
        private readonly IAssetRepository assetRepository;
        private readonly IIncomeRepository incomeRepository;
        private readonly IExpenditureRepository expenditureRepository;
        private readonly IFixedIncomeRepository fixedIncomeRepository;
        private readonly IFixedExpenditureRepository fixedExpenditureRepository;
        public NoticeController(IHtmlLocalizer<NoticeController> localizer, ILogger<NoticeController> logger, IAccountRepository accountRepository, IHostEnvironment hostEnvironment, ICategoryRepository categoryRepository, ISubCategoryRepository subCategoryRepository, IAssetRepository assetRepository, IIncomeRepository incomeRepository, IExpenditureRepository expenditureRepository, IFixedIncomeRepository fixedIncomeRepository, IFixedExpenditureRepository fixedExpenditureRepository)
        {
            this.localizer = localizer;
            this.logger = logger;
            this.accountRepository = accountRepository;
            this.hostEnvironment = hostEnvironment;
            this.categoryRepository = categoryRepository;
            this.subCategoryRepository = subCategoryRepository;
            this.assetRepository = assetRepository;
            this.incomeRepository = incomeRepository;
            this.expenditureRepository = expenditureRepository;
            this.fixedIncomeRepository = fixedIncomeRepository;
            this.fixedExpenditureRepository = fixedExpenditureRepository;
        }

        #region 고정 수입

        #region Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> CreateFixedIncome([FromBody] FixedIncomeInputViewModel fixedIncomeInputViewModel)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    try
                    {
                        fixedIncomeInputViewModel.Amount = Math.Abs(fixedIncomeInputViewModel.Amount);
                        HttpContext.Session.TryGetValue("AccountSession", out byte[] resultByte);

                        #region Validation of MainClass & SubClass Value
                        if (fixedIncomeInputViewModel.MainClass == "RegularIncome")
                        {
                            if (!(fixedIncomeInputViewModel.SubClass == "LaborIncome" ||
                                fixedIncomeInputViewModel.SubClass == "BusinessIncome" ||
                                fixedIncomeInputViewModel.SubClass == "PensionIncome" ||
                                fixedIncomeInputViewModel.SubClass == "FinancialIncome" ||
                                fixedIncomeInputViewModel.SubClass == "RentalIncome" ||
                                fixedIncomeInputViewModel.SubClass == "OtherIncome"))
                            {
                                return Json(new { result = false, error = localizer["Input is invalid"].Value });
                            }
                        }
                        else if (fixedIncomeInputViewModel.MainClass == "IrregularIncome")
                        {
                            if (!(fixedIncomeInputViewModel.SubClass == "LaborIncome" ||
                                fixedIncomeInputViewModel.SubClass == "OtherIncome"))
                            {
                                return Json(new { result = false, error = localizer["Input is invalid"].Value });
                            }
                        }
                        #endregion

                        #region Validate of DepositMonth
                        if (!(fixedIncomeInputViewModel.DepositMonth >= 1 && fixedIncomeInputViewModel.DepositMonth <= 12))
                        {
                            return Json(new { result = false, error = localizer["Input is invalid"].Value });
                        }
                        #endregion

                        #region Validate of DepositDay
                        if (fixedIncomeInputViewModel.DepositMonth == 1 ||
                            fixedIncomeInputViewModel.DepositMonth == 3 ||
                            fixedIncomeInputViewModel.DepositMonth == 5 ||
                            fixedIncomeInputViewModel.DepositMonth == 7 ||
                            fixedIncomeInputViewModel.DepositMonth == 8 ||
                            fixedIncomeInputViewModel.DepositMonth == 10 ||
                            fixedIncomeInputViewModel.DepositMonth == 12)
                        {
                            if (!(fixedIncomeInputViewModel.DepositDay >= 1 && fixedIncomeInputViewModel.DepositDay <= 31))
                            {
                                return Json(new { result = false, error = localizer["Input is invalid"].Value });
                            }
                        }
                        else if (fixedIncomeInputViewModel.DepositMonth == 2)
                        {
                            if (!(fixedIncomeInputViewModel.DepositDay >= 1 && fixedIncomeInputViewModel.DepositDay <= 29))
                            {
                                return Json(new { result = false, error = localizer["Input is invalid"].Value });
                            }
                        }
                        else if (fixedIncomeInputViewModel.DepositMonth == 4 ||
                                fixedIncomeInputViewModel.DepositMonth == 6 ||
                                fixedIncomeInputViewModel.DepositMonth == 9 ||
                                fixedIncomeInputViewModel.DepositMonth == 11)
                        {
                            if (!(fixedIncomeInputViewModel.DepositDay >= 1 && fixedIncomeInputViewModel.DepositDay <= 30))
                            {
                                return Json(new { result = false, error = localizer["Input is invalid"].Value });
                            }
                        }
                        #endregion

                        #region MaturityDate 형식 체크 (윤년 또는 월에 따른 일도 체크함)
                        if (!new Regex(@"^\d{4}-((0[1-9])|(1[012]))-((0[1-9]|[12]\d)|3[01])$").IsMatch(fixedIncomeInputViewModel.MaturityDate)) // https://stackoverflow.com/questions/5247219/regular-expression-to-detect-yyyy-mm-dd/15233484
                        {
                            return Json(new { result = false, error = localizer["Input is invalid"].Value });
                        }
                        #endregion

                        #region 고정 수입 생성
                        await fixedIncomeRepository.CreateFixedIncomeAsync
                            (
                            new FixedIncome()
                            {
                                AccountEmail = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email,
                                MainClass = fixedIncomeInputViewModel.MainClass,
                                SubClass = fixedIncomeInputViewModel.SubClass,
                                Contents = fixedIncomeInputViewModel.Contents ?? "",
                                Amount = Math.Abs(fixedIncomeInputViewModel.Amount),
                                DepositMyAssetProductName = fixedIncomeInputViewModel.DepositMyAssetProductName,
                                DepositMonth = fixedIncomeInputViewModel.DepositMonth,
                                DepositDay = fixedIncomeInputViewModel.DepositDay,
                                MaturityDate = new DateTime(Convert.ToInt32(Regex.Split(fixedIncomeInputViewModel.MaturityDate, "-")[0].ToString()), Convert.ToInt32(Regex.Split(fixedIncomeInputViewModel.MaturityDate, "-")[1].ToString()), Convert.ToInt32(Regex.Split(fixedIncomeInputViewModel.MaturityDate, "-")[2].ToString())),
                                Created = DateTime.UtcNow,
                                Updated = DateTime.UtcNow,
                                Note = fixedIncomeInputViewModel.Note ?? ""
                            }
                            );
                        #endregion

                        return Json(new { result = true, message = localizer["The fixedIncome has been successfully created."].Value });
                    }
                    catch (Exception e)
                    {
                        return Json(new { result = false, error = e.ToString() });
                    }
                }
                else
                {
                    return Json(new { result = false, error = localizer["Input is invalid"].Value });
                }
            }
            catch (Exception e)
            {
                return Json(new { result = false, error = e.ToString() });
            }
        }
        #endregion

        #region Read
        [HttpGet]
        public async Task<IActionResult> FixedIncome(string wholeSearch)
        {
            if (HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest") // ajax
            {
                List<FixedIncomeOutputViewModel> fixedIncomeOutputViewModels = new List<FixedIncomeOutputViewModel>();

                #region FixedIncomes
                HttpContext.Session.TryGetValue("AccountSession", out byte[] resultByte);

                DateTime currentDate = new DateTime(Convert.ToInt32(Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"), "-")[0]), Convert.ToInt32(Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"), "-")[1]), Convert.ToInt32(Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"), "-")[2]));

                foreach (var item in await fixedIncomeRepository.GetFixedIncomesAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email))
                {
                    if (string.IsNullOrEmpty(wholeSearch))
                    {
                        bool noticedResult = false;

                        try
                        {
                            noticedResult = currentDate.Subtract(new DateTime(Convert.ToInt32(Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"), "-")[0]), item.DepositMonth, item.DepositDay)).TotalDays <= 0 && Math.Abs(currentDate.Subtract(new DateTime(Convert.ToInt32(Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"), "-")[0]), item.DepositMonth, item.DepositDay)).TotalDays) <= ServerSetting.NoticeMaturityDateDay;
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
                            MainClass = localizer[item.MainClass.ToString()].Value,
                            SubClass = localizer[item.SubClass.ToString()].Value,
                            Contents = item.Contents,
                            Amount = item.Amount,
                            DepositMonth = item.DepositMonth,
                            DepositDay = item.DepositDay,
                            MaturityDate = item.MaturityDate.ToString("yyyy-MM-dd"),
                            Note = item.Note,
                            DepositMyAssetProductName = item.DepositMyAssetProductName,
                            Created = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(item.Created, OSTimeZone.DestinationTimeZoneId),
                            Updated = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(item.Updated, OSTimeZone.DestinationTimeZoneId),
                            Noticed = noticedResult,
                            Expired = item.MaturityDate.Subtract(currentDate).TotalDays < 0
                        });
                    }
                    else
                    {
                        if (localizer[item.MainClass?.ToString() ?? ""].Value.Contains(wholeSearch) ||
                            localizer[item.SubClass?.ToString() ?? ""].Value.Contains(wholeSearch) ||
                            (item.Contents?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.Amount.ToString() ?? "").Contains(wholeSearch) ||
                            (item.DepositMonth.ToString() ?? "").Contains(wholeSearch) ||
                            (item.DepositDay.ToString() ?? "").Contains(wholeSearch) ||
                            (item.MaturityDate.ToString("yyyy-MM-dd") ?? "").Contains(wholeSearch) ||
                            (item.Note?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.DepositMyAssetProductName?.ToString() ?? "").Contains(wholeSearch) ||
                            (TimeZoneInfo.ConvertTimeBySystemTimeZoneId(item.Created, OSTimeZone.DestinationTimeZoneId).ToString() ?? "").Contains(wholeSearch) ||
                            (TimeZoneInfo.ConvertTimeBySystemTimeZoneId(item.Updated, OSTimeZone.DestinationTimeZoneId).ToString() ?? "").Contains(wholeSearch))
                        {
                            bool noticedResult = false;

                            try
                            {
                                noticedResult = currentDate.Subtract(new DateTime(Convert.ToInt32(Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"), "-")[0]), item.DepositMonth, item.DepositDay)).TotalDays <= 0 && Math.Abs(currentDate.Subtract(new DateTime(Convert.ToInt32(Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"), "-")[0]), item.DepositMonth, item.DepositDay)).TotalDays) <= ServerSetting.NoticeMaturityDateDay;
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
                                MainClass = localizer[item.MainClass.ToString()].Value,
                                SubClass = localizer[item.SubClass.ToString()].Value,
                                Contents = item.Contents,
                                Amount = item.Amount,
                                DepositMonth = item.DepositMonth,
                                DepositDay = item.DepositDay,
                                MaturityDate = item.MaturityDate.ToString("yyyy-MM-dd"),
                                Note = item.Note,
                                DepositMyAssetProductName = item.DepositMyAssetProductName,
                                Created = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(item.Created, OSTimeZone.DestinationTimeZoneId),
                                Updated = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(item.Updated, OSTimeZone.DestinationTimeZoneId),
                                Noticed = noticedResult,
                                Expired = item.MaturityDate.Subtract(currentDate).TotalDays < 0
                            });
                        }
                    }
                }
                #endregion

                var result = fixedIncomeOutputViewModels.OrderByDescending(a => a.Expired).ThenByDescending(a => a.Noticed).AsQueryable();
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
                HttpContext.Session.TryGetValue("AccountSession", out byte[] resultByte);

                var tempFixedIncomes = await fixedIncomeRepository.GetFixedIncomesAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);

                if (tempFixedIncomes == null)
                {
                    return Json(new { result = false, error = localizer["No fixedIncome exists"].Value });
                }
                else
                {
                    var tempFixedIncome = tempFixedIncomes.Where(a => a.Id == id).FirstOrDefault();

                    if (tempFixedIncome == null)
                    {
                        return Json(new { result = false, error = localizer["Input is invalid"].Value });
                    }
                    else
                    {
                        var tempFixedIncomeOutputModel = new FixedIncomeOutputViewModel()
                        {
                            Id = tempFixedIncome.Id,
                            MainClass = tempFixedIncome.MainClass,
                            SubClass = tempFixedIncome.SubClass,
                            Contents = tempFixedIncome.Contents,
                            Amount = tempFixedIncome.Amount,
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
            catch (Exception e)
            {
                return Json(new { result = false, error = e.ToString() });
            }
        }
        #endregion

        #region Update
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> UpdateFixedIncome([FromBody] FixedIncomeInputViewModel fixedIncomeInputViewModel)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    try
                    {
                        fixedIncomeInputViewModel.Amount = Math.Abs(fixedIncomeInputViewModel.Amount);
                        HttpContext.Session.TryGetValue("AccountSession", out byte[] resultByte);

                        #region Validation of MainClass & SubClass Value
                        if (fixedIncomeInputViewModel.MainClass == "RegularIncome")
                        {
                            if (!(fixedIncomeInputViewModel.SubClass == "LaborIncome" ||
                                fixedIncomeInputViewModel.SubClass == "BusinessIncome" ||
                                fixedIncomeInputViewModel.SubClass == "PensionIncome" ||
                                fixedIncomeInputViewModel.SubClass == "FinancialIncome" ||
                                fixedIncomeInputViewModel.SubClass == "RentalIncome" ||
                                fixedIncomeInputViewModel.SubClass == "OtherIncome"))
                            {
                                return Json(new { result = false, error = localizer["Input is invalid"].Value });
                            }
                        }
                        else if (fixedIncomeInputViewModel.MainClass == "IrregularIncome")
                        {
                            if (!(fixedIncomeInputViewModel.SubClass == "LaborIncome" ||
                                fixedIncomeInputViewModel.SubClass == "OtherIncome"))
                            {
                                return Json(new { result = false, error = localizer["Input is invalid"].Value });
                            }
                        }
                        #endregion

                        #region Validate of DepositMonth
                        if (!(fixedIncomeInputViewModel.DepositMonth >= 1 && fixedIncomeInputViewModel.DepositMonth <= 12))
                        {
                            return Json(new { result = false, error = localizer["Input is invalid"].Value });
                        }
                        #endregion

                        #region Validate of DepositDay
                        if (fixedIncomeInputViewModel.DepositMonth == 1 ||
                            fixedIncomeInputViewModel.DepositMonth == 3 ||
                            fixedIncomeInputViewModel.DepositMonth == 5 ||
                            fixedIncomeInputViewModel.DepositMonth == 7 ||
                            fixedIncomeInputViewModel.DepositMonth == 8 ||
                            fixedIncomeInputViewModel.DepositMonth == 10 ||
                            fixedIncomeInputViewModel.DepositMonth == 12)
                        {
                            if (!(fixedIncomeInputViewModel.DepositDay >= 1 && fixedIncomeInputViewModel.DepositDay <= 31))
                            {
                                return Json(new { result = false, error = localizer["Input is invalid"].Value });
                            }
                        }
                        else if (fixedIncomeInputViewModel.DepositMonth == 2)
                        {
                            if (!(fixedIncomeInputViewModel.DepositDay >= 1 && fixedIncomeInputViewModel.DepositDay <= 29))
                            {
                                return Json(new { result = false, error = localizer["Input is invalid"].Value });
                            }
                        }
                        else if (fixedIncomeInputViewModel.DepositMonth == 4 ||
                                fixedIncomeInputViewModel.DepositMonth == 6 ||
                                fixedIncomeInputViewModel.DepositMonth == 9 ||
                                fixedIncomeInputViewModel.DepositMonth == 11)
                        {
                            if (!(fixedIncomeInputViewModel.DepositDay >= 1 && fixedIncomeInputViewModel.DepositDay <= 30))
                            {
                                return Json(new { result = false, error = localizer["Input is invalid"].Value });
                            }
                        }
                        #endregion

                        #region MaturityDate 형식 체크 (윤년 또는 월에 따른 일도 체크함)
                        if (!new Regex(@"^\d{4}-((0[1-9])|(1[012]))-((0[1-9]|[12]\d)|3[01])$").IsMatch(fixedIncomeInputViewModel.MaturityDate)) // https://stackoverflow.com/questions/5247219/regular-expression-to-detect-yyyy-mm-dd/15233484
                        {
                            return Json(new { result = false, error = localizer["Input is invalid"].Value });
                        }
                        #endregion

                        #region 고정 수입 업데이트
                        var tempFixedIncomes = await fixedIncomeRepository.GetFixedIncomesAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);

                        if (tempFixedIncomes == null)
                        {
                            return Json(new { result = false, error = localizer["No fixedIncome exists"].Value });
                        }
                        else
                        {
                            var tempFixedIncome = tempFixedIncomes.Where(a => a.Id == fixedIncomeInputViewModel.Id).FirstOrDefault();

                            if (tempFixedIncome == null)
                            {
                                return Json(new { result = false, error = localizer["Input is invalid"].Value });
                            }
                            else
                            {
                                #region 고정 수입 업데이트
                                tempFixedIncome.MainClass = fixedIncomeInputViewModel.MainClass;
                                tempFixedIncome.SubClass = fixedIncomeInputViewModel.SubClass;
                                tempFixedIncome.Contents = fixedIncomeInputViewModel.Contents ?? "";
                                tempFixedIncome.Amount = Math.Abs(fixedIncomeInputViewModel.Amount);
                                tempFixedIncome.DepositMyAssetProductName = fixedIncomeInputViewModel.DepositMyAssetProductName;
                                tempFixedIncome.DepositMonth = fixedIncomeInputViewModel.DepositMonth;
                                tempFixedIncome.DepositDay = fixedIncomeInputViewModel.DepositDay;
                                tempFixedIncome.MaturityDate = new DateTime(Convert.ToInt32(Regex.Split(fixedIncomeInputViewModel.MaturityDate, "-")[0].ToString()), Convert.ToInt32(Regex.Split(fixedIncomeInputViewModel.MaturityDate, "-")[1].ToString()), Convert.ToInt32(Regex.Split(fixedIncomeInputViewModel.MaturityDate, "-")[2].ToString()));
                                tempFixedIncome.Updated = DateTime.UtcNow;
                                tempFixedIncome.Note = fixedIncomeInputViewModel.Note ?? "";
                                tempFixedIncome.Unpunctuality = fixedIncomeInputViewModel.Unpunctuality;

                                await fixedIncomeRepository.UpdateFixedIncomeAsync(tempFixedIncome);
                                #endregion

                                return Json(new { result = true, message = localizer["The fixedIncome has been successfully updated."].Value });
                            }
                        }
                        #endregion
                    }
                    catch (Exception e)
                    {
                        return Json(new { result = false, error = e.ToString() });
                    }
                }
                else
                {
                    return Json(new { result = false, error = localizer["Input is invalid"].Value });
                }
            }
            catch (Exception e)
            {
                return Json(new { result = false, error = e.ToString() });
            }
        }
        #endregion

        #region Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> DeleteFixedIncome([FromBody] FixedIncomeInputViewModel fixedIncomeInputViewModel)
        {
            try
            {
                HttpContext.Session.TryGetValue("AccountSession", out byte[] resultByte);

                var tempFixedIncomes = await fixedIncomeRepository.GetFixedIncomesAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);

                if (tempFixedIncomes == null)
                {
                    return Json(new { result = false, error = localizer["Input is invalid"].Value });
                }
                else
                {
                    var tempFixedIncome = tempFixedIncomes.Where(a => a.Id == fixedIncomeInputViewModel.Id).FirstOrDefault();

                    if (tempFixedIncome == null)
                    {
                        return Json(new { result = false, error = localizer["Input is invalid"].Value });
                    }
                    else
                    {
                        #region 고정 수입 삭제
                        await fixedIncomeRepository.DeleteFixedIncomeAsync(tempFixedIncome);
                        #endregion

                        return Json(new { result = true, message = localizer["The fixedIncome has been successfully deleted."].Value });
                    }
                }
            }
            catch (Exception e)
            {
                return Json(new { result = false, error = e.ToString() });
            }
        }
        #endregion

        #region Excel
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> ExportExcelFixedIncome(string fileName = "")
        {
            // query data from database
            await Task.Yield();

            List<FixedIncomeOutputViewModel> fixedIncomeOutputViewModels = new List<FixedIncomeOutputViewModel>();

            #region FixedIncomes

            HttpContext.Session.TryGetValue("AccountSession", out byte[] resultByte);

            DateTime currentDate = new DateTime(Convert.ToInt32(Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"), "-")[0]), Convert.ToInt32(Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"), "-")[1]), Convert.ToInt32(Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"), "-")[2]));

            foreach (var item in await fixedIncomeRepository.GetFixedIncomesAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email))
            {
                bool noticedResult = false;

                try
                {
                    noticedResult = currentDate.Subtract(new DateTime(Convert.ToInt32(Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"), "-")[0]), item.DepositMonth, item.DepositDay)).TotalDays <= 0 && Math.Abs(currentDate.Subtract(new DateTime(Convert.ToInt32(Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"), "-")[0]), item.DepositMonth, item.DepositDay)).TotalDays) <= ServerSetting.NoticeMaturityDateDay;
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
                    MainClass = localizer[item.MainClass.ToString()].Value,
                    SubClass = localizer[item.SubClass.ToString()].Value,
                    Contents = item.Contents,
                    Amount = item.Amount,
                    DepositMonth = item.DepositMonth,
                    DepositDay = item.DepositDay,
                    MaturityDate = item.MaturityDate.ToString("yyyy-MM-dd"),
                    Note = item.Note,
                    DepositMyAssetProductName = item.DepositMyAssetProductName,
                    Created = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(item.Created, OSTimeZone.DestinationTimeZoneId),
                    Updated = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(item.Updated, OSTimeZone.DestinationTimeZoneId),
                    Noticed = noticedResult,
                    Expired = item.MaturityDate.Subtract(currentDate).TotalDays < 0
                });
            }
            #endregion

            var stream = new MemoryStream();

            using (var package = new ExcelPackage(stream))
            {
                var workSheetMenuOutputViewModels = package.Workbook.Worksheets.Add("Sheet1");

                workSheetMenuOutputViewModels.Row(1).Height = 20;
                workSheetMenuOutputViewModels.Row(1).Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                workSheetMenuOutputViewModels.Row(1).Style.Font.Bold = true;
                workSheetMenuOutputViewModels.Cells[1, 1].Value = localizer[nameof(FixedIncomeOutputViewModel.MainClass).ToString()].Value;
                workSheetMenuOutputViewModels.Cells[1, 2].Value = localizer[nameof(FixedIncomeOutputViewModel.SubClass).ToString()].Value;
                workSheetMenuOutputViewModels.Cells[1, 3].Value = localizer[nameof(FixedIncomeOutputViewModel.Contents).ToString()].Value;
                workSheetMenuOutputViewModels.Cells[1, 4].Value = localizer[nameof(FixedIncomeOutputViewModel.Amount).ToString()].Value;
                workSheetMenuOutputViewModels.Cells[1, 5].Value = localizer[nameof(FixedIncomeOutputViewModel.DepositMonth).ToString()].Value;
                workSheetMenuOutputViewModels.Cells[1, 6].Value = localizer[nameof(FixedIncomeOutputViewModel.DepositDay).ToString()].Value;
                workSheetMenuOutputViewModels.Cells[1, 7].Value = localizer[nameof(FixedIncomeOutputViewModel.MaturityDate).ToString()].Value;
                workSheetMenuOutputViewModels.Cells[1, 8].Value = localizer[nameof(FixedIncomeOutputViewModel.Note).ToString()].Value;
                workSheetMenuOutputViewModels.Cells[1, 9].Value = localizer[nameof(FixedIncomeOutputViewModel.DepositMyAssetProductName).ToString()].Value;
                workSheetMenuOutputViewModels.Cells[1, 10].Value = localizer[nameof(FixedIncomeOutputViewModel.Created).ToString()].Value;
                workSheetMenuOutputViewModels.Cells[1, 11].Value = localizer[nameof(FixedIncomeOutputViewModel.Updated).ToString()].Value;
                workSheetMenuOutputViewModels.Cells[1, 12].Value = localizer[nameof(FixedIncomeOutputViewModel.Noticed).ToString()].Value;
                workSheetMenuOutputViewModels.Cells[1, 13].Value = localizer[nameof(FixedIncomeOutputViewModel.Expired).ToString()].Value;

                int recordIndex = 2;
                foreach (var item in fixedIncomeOutputViewModels.OrderByDescending(a => a.Expired).ThenByDescending(a => a.Noticed))
                {
                    workSheetMenuOutputViewModels.Cells[recordIndex, 1].Value = localizer[item?.MainClass?.ToString() ?? ""].Value;
                    workSheetMenuOutputViewModels.Cells[recordIndex, 2].Value = localizer[item?.SubClass?.ToString() ?? ""].Value;
                    workSheetMenuOutputViewModels.Cells[recordIndex, 3].Value = item.Contents;
                    workSheetMenuOutputViewModels.Cells[recordIndex, 4].Value = item.Amount;
                    workSheetMenuOutputViewModels.Cells[recordIndex, 5].Value = item.DepositMonth;
                    workSheetMenuOutputViewModels.Cells[recordIndex, 6].Value = item.DepositDay;
                    workSheetMenuOutputViewModels.Cells[recordIndex, 7].Value = item.MaturityDate;
                    workSheetMenuOutputViewModels.Cells[recordIndex, 8].Value = item.Note;
                    workSheetMenuOutputViewModels.Cells[recordIndex, 9].Value = item.DepositMyAssetProductName;
                    workSheetMenuOutputViewModels.Cells[recordIndex, 10].Value = item.Created.ToString();
                    workSheetMenuOutputViewModels.Cells[recordIndex, 11].Value = item.Updated.ToString();
                    workSheetMenuOutputViewModels.Cells[recordIndex, 12].Value = item.Noticed.ToString();
                    workSheetMenuOutputViewModels.Cells[recordIndex, 13].Value = item.Expired.ToString();
                    recordIndex++;
                }

                package.Save();
            }
            stream.Position = 0;
            string excelName = $"{fileName}-{TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, OSTimeZone.DestinationTimeZoneId).ToString("yyyy-MM-dd-HH-mm-ss-fff")}.xlsx";

            return File(stream, "application/octet-stream", excelName);
        }
        #endregion

        #endregion

        #region 고정 지출

        #region Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> CreateFixedExpenditure([FromBody] FixedExpenditureInputViewModel fixedExpenditureInputViewModel)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    fixedExpenditureInputViewModel.Amount = Math.Abs(fixedExpenditureInputViewModel.Amount);
                    HttpContext.Session.TryGetValue("AccountSession", out byte[] resultByte);

                    #region 대분류 = '정기저축'
                    if (fixedExpenditureInputViewModel.MainClass == "RegularSavings")
                    {
                        if (fixedExpenditureInputViewModel.SubClass == "Deposit" || fixedExpenditureInputViewModel.SubClass == "MyAssetTransfer" || fixedExpenditureInputViewModel.SubClass == "Investment")
                        {
                            try
                            {
                                if (fixedExpenditureInputViewModel.PaymentMethod == fixedExpenditureInputViewModel.MyDepositAsset)
                                {
                                    return Json(new { result = false, error = localizer["The PaymentMethod and MyDepositAsset value cannot be the same."].Value });
                                }

                                #region 고정 지출 생성
                                await fixedExpenditureRepository.CreateFixedExpenditureAsync
                                    (
                                    new FixedExpenditure()
                                    {
                                        AccountEmail = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email,
                                        MainClass = fixedExpenditureInputViewModel.MainClass,
                                        SubClass = fixedExpenditureInputViewModel.SubClass,
                                        Contents = fixedExpenditureInputViewModel.Contents ?? "",
                                        Amount = Math.Abs(fixedExpenditureInputViewModel.Amount),
                                        PaymentMethod = fixedExpenditureInputViewModel.PaymentMethod,
                                        MyDepositAsset = fixedExpenditureInputViewModel.MyDepositAsset,
                                        DepositMonth = fixedExpenditureInputViewModel.DepositMonth,
                                        DepositDay = fixedExpenditureInputViewModel.DepositDay,
                                        MaturityDate = new DateTime(Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate, "-")[0].ToString()), Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate, "-")[1].ToString()), Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate, "-")[2].ToString())),
                                        Created = DateTime.UtcNow,
                                        Updated = DateTime.UtcNow,
                                        Note = fixedExpenditureInputViewModel.Note ?? ""
                                    }
                                    );
                                #endregion

                                return Json(new { result = true, message = localizer["The fixedExpenditure has been successfully created."].Value });
                            }
                            catch (Exception e)
                            {
                                return Json(new { result = false, error = e.ToString() });
                            }
                        }
                        else
                        {
                            return Json(new { result = false, error = localizer["Input is invalid"].Value });
                        }
                    }
                    #endregion

                    #region 대분류 = '비소비지출'
                    else if (fixedExpenditureInputViewModel.MainClass == "NonConsumerSpending")
                    {
                        if (fixedExpenditureInputViewModel.SubClass == "PublicPension" || fixedExpenditureInputViewModel.SubClass == "DebtRepayment")
                        {
                            try
                            {
                                if (fixedExpenditureInputViewModel.PaymentMethod == fixedExpenditureInputViewModel.MyDepositAsset)
                                {
                                    return Json(new { result = false, error = localizer["The PaymentMethod and MyDepositAsset value cannot be the same."].Value });
                                }

                                #region 고정 지출 생성
                                await fixedExpenditureRepository.CreateFixedExpenditureAsync
                                    (
                                    new FixedExpenditure()
                                    {
                                        AccountEmail = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email,
                                        MainClass = fixedExpenditureInputViewModel.MainClass,
                                        SubClass = fixedExpenditureInputViewModel.SubClass,
                                        Contents = fixedExpenditureInputViewModel.Contents ?? "",
                                        Amount = Math.Abs(fixedExpenditureInputViewModel.Amount),
                                        PaymentMethod = fixedExpenditureInputViewModel.PaymentMethod,
                                        MyDepositAsset = fixedExpenditureInputViewModel.MyDepositAsset,
                                        DepositMonth = fixedExpenditureInputViewModel.DepositMonth,
                                        DepositDay = fixedExpenditureInputViewModel.DepositDay,
                                        MaturityDate = new DateTime(Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate, "-")[0].ToString()), Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate, "-")[1].ToString()), Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate, "-")[2].ToString())),
                                        Created = DateTime.UtcNow,
                                        Updated = DateTime.UtcNow,
                                        Note = fixedExpenditureInputViewModel.Note ?? ""
                                    }
                                    );
                                #endregion

                                return Json(new { result = true, message = localizer["The fixedExpenditure has been successfully created."].Value });
                            }
                            catch (Exception e)
                            {
                                return Json(new { result = false, error = e.ToString() });
                            }
                        }
                        else if (fixedExpenditureInputViewModel.SubClass == "Tax" ||
                            fixedExpenditureInputViewModel.SubClass == "SocialInsurance" ||
                            fixedExpenditureInputViewModel.SubClass == "InterHouseholdTranserExpenses" ||
                            fixedExpenditureInputViewModel.SubClass == "NonProfitOrganizationTransfer")
                        {
                            try
                            {
                                #region 고정 지출 생성
                                await fixedExpenditureRepository.CreateFixedExpenditureAsync
                                    (
                                    new FixedExpenditure()
                                    {
                                        AccountEmail = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email,
                                        MainClass = fixedExpenditureInputViewModel.MainClass,
                                        SubClass = fixedExpenditureInputViewModel.SubClass,
                                        Contents = fixedExpenditureInputViewModel.Contents ?? "",
                                        Amount = Math.Abs(fixedExpenditureInputViewModel.Amount),
                                        PaymentMethod = fixedExpenditureInputViewModel.PaymentMethod,
                                        MyDepositAsset = "",
                                        DepositMonth = fixedExpenditureInputViewModel.DepositMonth,
                                        DepositDay = fixedExpenditureInputViewModel.DepositDay,
                                        MaturityDate = new DateTime(Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate, "-")[0].ToString()), Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate, "-")[1].ToString()), Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate, "-")[2].ToString())),
                                        Created = DateTime.UtcNow,
                                        Updated = DateTime.UtcNow,
                                        Note = fixedExpenditureInputViewModel.Note ?? ""
                                    }
                                    );
                                #endregion

                                return Json(new { result = true, message = localizer["The fixedExpenditure has been successfully created."].Value });
                            }
                            catch (Exception e)
                            {
                                return Json(new { result = false, error = e.ToString() });
                            }
                        }
                        else
                        {
                            return Json(new { result = false, error = localizer["Input is invalid"].Value });
                        }
                    }
                    #endregion

                    #region 대분류 = '소비지출'
                    else if (fixedExpenditureInputViewModel.MainClass == "ConsumerSpending")
                    {
                        if (fixedExpenditureInputViewModel.SubClass == "MealOrEatOutExpenses" ||
                            fixedExpenditureInputViewModel.SubClass == "HousingOrSuppliesCost" ||
                            fixedExpenditureInputViewModel.SubClass == "EducationExpenses" ||
                            fixedExpenditureInputViewModel.SubClass == "MedicalExpenses" ||
                            fixedExpenditureInputViewModel.SubClass == "TransportationCost" ||
                            fixedExpenditureInputViewModel.SubClass == "CommunicationCost" ||
                            fixedExpenditureInputViewModel.SubClass == "LeisureOrCulture" ||
                            fixedExpenditureInputViewModel.SubClass == "ClothingOrShoes" ||
                            fixedExpenditureInputViewModel.SubClass == "PinMoney" ||
                            fixedExpenditureInputViewModel.SubClass == "ProtectionTypeInsurance" ||
                            fixedExpenditureInputViewModel.SubClass == "OtherExpenses" ||
                            fixedExpenditureInputViewModel.SubClass == "UnknownExpenditure")
                        {
                            try
                            {
                                #region 고정 지출 생성
                                await fixedExpenditureRepository.CreateFixedExpenditureAsync
                                    (
                                    new FixedExpenditure()
                                    {
                                        AccountEmail = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email,
                                        MainClass = fixedExpenditureInputViewModel.MainClass,
                                        SubClass = fixedExpenditureInputViewModel.SubClass,
                                        Contents = fixedExpenditureInputViewModel.Contents ?? "",
                                        Amount = Math.Abs(fixedExpenditureInputViewModel.Amount),
                                        PaymentMethod = fixedExpenditureInputViewModel.PaymentMethod,
                                        MyDepositAsset = "",
                                        DepositMonth = fixedExpenditureInputViewModel.DepositMonth,
                                        DepositDay = fixedExpenditureInputViewModel.DepositDay,
                                        MaturityDate = new DateTime(Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate, "-")[0].ToString()), Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate, "-")[1].ToString()), Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate, "-")[2].ToString())),
                                        Created = DateTime.UtcNow,
                                        Updated = DateTime.UtcNow,
                                        Note = fixedExpenditureInputViewModel.Note ?? ""
                                    }
                                    );
                                #endregion

                                return Json(new { result = true, message = localizer["The fixedExpenditure has been successfully created."].Value });
                            }
                            catch (Exception e)
                            {
                                return Json(new { result = false, error = e.ToString() });
                            }
                        }
                        else
                        {
                            return Json(new { result = false, error = localizer["Input is invalid"].Value });
                        }
                    }
                    #endregion

                    #region 대분류에 없는 값
                    else
                    {
                        return Json(new { result = false, error = localizer["Input is invalid"].Value });
                    }
                    #endregion
                }
                else
                {
                    return Json(new { result = false, error = localizer["Input is invalid"].Value });
                }
            }
            catch (Exception e)
            {
                return Json(new { result = false, error = e.ToString() });
            }
        }
        #endregion

        #region Read
        [HttpGet]
        public async Task<IActionResult> FixedExpenditure(string wholeSearch)
        {
            if (HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest") // ajax
            {
                List<FixedExpenditureOutputViewModel> fixedExpenditureOutputViewModels = new List<FixedExpenditureOutputViewModel>();

                #region FixedExpenditures
                HttpContext.Session.TryGetValue("AccountSession", out byte[] resultByte);

                DateTime currentDate = new DateTime(Convert.ToInt32(Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"), "-")[0]), Convert.ToInt32(Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"), "-")[1]), Convert.ToInt32(Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"), "-")[2]));

                foreach (var item in await fixedExpenditureRepository.GetFixedExpendituresAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email))
                {
                    if (string.IsNullOrEmpty(wholeSearch))
                    {
                        bool noticedResult = false;

                        try
                        {
                            noticedResult = currentDate.Subtract(new DateTime(Convert.ToInt32(Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"), "-")[0]), item.DepositMonth, item.DepositDay)).TotalDays <= 0 && Math.Abs(currentDate.Subtract(new DateTime(Convert.ToInt32(Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"), "-")[0]), item.DepositMonth, item.DepositDay)).TotalDays) <= ServerSetting.NoticeMaturityDateDay;
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
                            MainClass = localizer[item.MainClass.ToString()].Value,
                            SubClass = localizer[item.SubClass.ToString()].Value,
                            Contents = item.Contents,
                            Amount = item.Amount,
                            PaymentMethod = item.PaymentMethod,
                            MyDepositAsset = item.MyDepositAsset,
                            DepositMonth = item.DepositMonth,
                            DepositDay = item.DepositDay,
                            MaturityDate = item.MaturityDate.ToString("yyyy-MM-dd"),
                            Created = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(item.Created, OSTimeZone.DestinationTimeZoneId),
                            Updated = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(item.Updated, OSTimeZone.DestinationTimeZoneId),
                            Note = item.Note,
                            Noticed = noticedResult,
                            Expired = item.MaturityDate.Subtract(currentDate).TotalDays < 0
                        });
                    }
                    else
                    {
                        if (localizer[item.MainClass?.ToString() ?? ""].Value.Contains(wholeSearch) ||
                            localizer[item.SubClass?.ToString() ?? ""].Value.Contains(wholeSearch) ||
                            (item.Contents?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.Amount.ToString() ?? "").Contains(wholeSearch) ||
                            (item.PaymentMethod.ToString() ?? "").Contains(wholeSearch) ||
                            (item.MyDepositAsset?.ToString() ?? "").Contains(wholeSearch) ||
                            (item.DepositMonth.ToString() ?? "").Contains(wholeSearch) ||
                            (item.DepositDay.ToString() ?? "").Contains(wholeSearch) ||
                            (item.MaturityDate.ToString("yyyy-MM-dd") ?? "").Contains(wholeSearch) ||
                            (TimeZoneInfo.ConvertTimeBySystemTimeZoneId(item.Created, OSTimeZone.DestinationTimeZoneId).ToString() ?? "").Contains(wholeSearch) ||
                            (TimeZoneInfo.ConvertTimeBySystemTimeZoneId(item.Updated, OSTimeZone.DestinationTimeZoneId).ToString() ?? "").Contains(wholeSearch) ||
                            (item.Note?.ToString() ?? "").Contains(wholeSearch))
                        {
                            bool noticedResult = false;

                            try
                            {
                                noticedResult = currentDate.Subtract(new DateTime(Convert.ToInt32(Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"), "-")[0]), item.DepositMonth, item.DepositDay)).TotalDays <= 0 && Math.Abs(currentDate.Subtract(new DateTime(Convert.ToInt32(Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"), "-")[0]), item.DepositMonth, item.DepositDay)).TotalDays) <= ServerSetting.NoticeMaturityDateDay;
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
                                MainClass = localizer[item.MainClass.ToString()].Value,
                                SubClass = localizer[item.SubClass.ToString()].Value,
                                Contents = item.Contents,
                                Amount = item.Amount,
                                PaymentMethod = item.PaymentMethod,
                                MyDepositAsset = item.MyDepositAsset,
                                DepositMonth = item.DepositMonth,
                                DepositDay = item.DepositDay,
                                MaturityDate = item.MaturityDate.ToString("yyyy-MM-dd"),
                                Created = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(item.Created, OSTimeZone.DestinationTimeZoneId),
                                Updated = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(item.Updated, OSTimeZone.DestinationTimeZoneId),
                                Note = item.Note,
                                Noticed = noticedResult,
                                Expired = item.MaturityDate.Subtract(currentDate).TotalDays < 0
                            });
                        }
                    }
                }
                #endregion

                var result = fixedExpenditureOutputViewModels.OrderByDescending(a => a.Expired).ThenByDescending(a => a.Noticed).AsQueryable();
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
                HttpContext.Session.TryGetValue("AccountSession", out byte[] resultByte);

                var tempFixedExpenditures = await fixedExpenditureRepository.GetFixedExpendituresAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);

                if (tempFixedExpenditures == null)
                {
                    return Json(new { result = false, error = localizer["No fixedExpenditure exists"].Value });
                }
                else
                {
                    var tempFixedExpenditure = tempFixedExpenditures.Where(a => a.Id == id).FirstOrDefault();

                    if (tempFixedExpenditure == null)
                    {
                        return Json(new { result = false, error = localizer["Input is invalid"].Value });
                    }
                    else
                    {
                        var tempFixedIncomeOutputModel = new FixedExpenditureOutputViewModel()
                        {
                            Id = tempFixedExpenditure.Id,
                            MainClass = tempFixedExpenditure.MainClass,
                            SubClass = tempFixedExpenditure.SubClass,
                            Contents = tempFixedExpenditure.Contents,
                            Amount = tempFixedExpenditure.Amount,
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
            catch (Exception e)
            {
                return Json(new { result = false, error = e.ToString() });
            }
        }
        #endregion

        #region Update
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> UpdateFixedExpenditure([FromBody] FixedExpenditureInputViewModel fixedExpenditureInputViewModel)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    fixedExpenditureInputViewModel.Amount = Math.Abs(fixedExpenditureInputViewModel.Amount);
                    HttpContext.Session.TryGetValue("AccountSession", out byte[] resultByte);

                    #region 대분류 = '정기저축'
                    if (fixedExpenditureInputViewModel.MainClass == "RegularSavings")
                    {
                        if (fixedExpenditureInputViewModel.SubClass == "Deposit" || fixedExpenditureInputViewModel.SubClass == "MyAssetTransfer" || fixedExpenditureInputViewModel.SubClass == "Investment")
                        {
                            try
                            {
                                if (fixedExpenditureInputViewModel.PaymentMethod == fixedExpenditureInputViewModel.MyDepositAsset)
                                {
                                    return Json(new { result = false, error = localizer["The PaymentMethod and MyDepositAsset value cannot be the same."].Value });
                                }

                                var tempFixedExpenditures = await fixedExpenditureRepository.GetFixedExpendituresAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);
                                var tempFixedExpenditure = tempFixedExpenditures.Where(a => a.Id == fixedExpenditureInputViewModel.Id).FirstOrDefault();

                                if (tempFixedExpenditure == null)
                                {
                                    return Json(new { result = false, error = localizer["Input is invalid"].Value });
                                }

                                tempFixedExpenditure.Id = fixedExpenditureInputViewModel.Id;
                                tempFixedExpenditure.AccountEmail = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email;
                                tempFixedExpenditure.MainClass = fixedExpenditureInputViewModel.MainClass;
                                tempFixedExpenditure.SubClass = fixedExpenditureInputViewModel.SubClass;
                                tempFixedExpenditure.Contents = fixedExpenditureInputViewModel.Contents ?? "";
                                tempFixedExpenditure.Amount = fixedExpenditureInputViewModel.Amount;
                                tempFixedExpenditure.DepositMonth = fixedExpenditureInputViewModel.DepositMonth;
                                tempFixedExpenditure.DepositDay = fixedExpenditureInputViewModel.DepositDay;
                                tempFixedExpenditure.MaturityDate = new DateTime(Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate, "-")[0].ToString()), Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate, "-")[1].ToString()), Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate, "-")[2].ToString()));
                                tempFixedExpenditure.Note = fixedExpenditureInputViewModel?.Note ?? "";
                                tempFixedExpenditure.PaymentMethod = fixedExpenditureInputViewModel.PaymentMethod;
                                tempFixedExpenditure.MyDepositAsset = fixedExpenditureInputViewModel.MyDepositAsset ?? "";
                                tempFixedExpenditure.Updated = DateTime.UtcNow;
                                tempFixedExpenditure.Unpunctuality = fixedExpenditureInputViewModel.Unpunctuality;

                                await fixedExpenditureRepository.UpdateFixedExpenditureAsync(tempFixedExpenditure);

                                return Json(new { result = true, message = localizer["The fixedExpenditure has been successfully updated."].Value });
                            }
                            catch (Exception e)
                            {
                                return Json(new { result = false, error = e.ToString() });
                            }
                        }
                        else
                        {
                            return Json(new { result = false, error = localizer["Input is invalid"].Value });
                        }
                    }
                    #endregion

                    #region 대분류 = '비소비지출'
                    else if (fixedExpenditureInputViewModel.MainClass == "NonConsumerSpending")
                    {
                        if (fixedExpenditureInputViewModel.SubClass == "PublicPension" || fixedExpenditureInputViewModel.SubClass == "DebtRepayment")
                        {
                            try
                            {
                                if (fixedExpenditureInputViewModel.PaymentMethod == fixedExpenditureInputViewModel.MyDepositAsset)
                                {
                                    return Json(new { result = false, error = localizer["The PaymentMethod and MyDepositAsset value cannot be the same."].Value });
                                }

                                var tempFixedExpenditures = await fixedExpenditureRepository.GetFixedExpendituresAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);
                                var tempFixedExpenditure = tempFixedExpenditures.Where(a => a.Id == fixedExpenditureInputViewModel.Id).FirstOrDefault();

                                if (tempFixedExpenditure == null)
                                {
                                    return Json(new { result = false, error = localizer["Input is invalid"].Value });
                                }

                                tempFixedExpenditure.Id = fixedExpenditureInputViewModel.Id;
                                tempFixedExpenditure.AccountEmail = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email;
                                tempFixedExpenditure.MainClass = fixedExpenditureInputViewModel.MainClass;
                                tempFixedExpenditure.SubClass = fixedExpenditureInputViewModel.SubClass;
                                tempFixedExpenditure.Contents = fixedExpenditureInputViewModel.Contents ?? "";
                                tempFixedExpenditure.Amount = fixedExpenditureInputViewModel.Amount;
                                tempFixedExpenditure.DepositMonth = fixedExpenditureInputViewModel.DepositMonth;
                                tempFixedExpenditure.DepositDay = fixedExpenditureInputViewModel.DepositDay;
                                tempFixedExpenditure.MaturityDate = new DateTime(Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate, "-")[0].ToString()), Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate, "-")[1].ToString()), Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate, "-")[2].ToString()));
                                tempFixedExpenditure.Note = fixedExpenditureInputViewModel?.Note ?? "";
                                tempFixedExpenditure.PaymentMethod = fixedExpenditureInputViewModel.PaymentMethod;
                                tempFixedExpenditure.MyDepositAsset = fixedExpenditureInputViewModel.MyDepositAsset ?? "";
                                tempFixedExpenditure.Updated = DateTime.UtcNow;
                                tempFixedExpenditure.Unpunctuality = fixedExpenditureInputViewModel.Unpunctuality;

                                await fixedExpenditureRepository.UpdateFixedExpenditureAsync(tempFixedExpenditure);

                                return Json(new { result = true, message = localizer["The fixedExpenditure has been successfully updated."].Value });
                            }
                            catch (Exception e)
                            {
                                return Json(new { result = false, error = e.ToString() });
                            }
                        }
                        else if (fixedExpenditureInputViewModel.SubClass == "Tax" ||
                            fixedExpenditureInputViewModel.SubClass == "SocialInsurance" ||
                            fixedExpenditureInputViewModel.SubClass == "InterHouseholdTranserExpenses" ||
                            fixedExpenditureInputViewModel.SubClass == "NonProfitOrganizationTransfer")
                        {
                            try
                            {
                                var tempFixedExpenditures = await fixedExpenditureRepository.GetFixedExpendituresAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);
                                var tempFixedExpenditure = tempFixedExpenditures.Where(a => a.Id == fixedExpenditureInputViewModel.Id).FirstOrDefault();

                                if (tempFixedExpenditure == null)
                                {
                                    return Json(new { result = false, error = localizer["Input is invalid"].Value });
                                }

                                tempFixedExpenditure.Id = fixedExpenditureInputViewModel.Id;
                                tempFixedExpenditure.AccountEmail = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email;
                                tempFixedExpenditure.MainClass = fixedExpenditureInputViewModel.MainClass;
                                tempFixedExpenditure.SubClass = fixedExpenditureInputViewModel.SubClass;
                                tempFixedExpenditure.Contents = fixedExpenditureInputViewModel.Contents ?? "";
                                tempFixedExpenditure.Amount = fixedExpenditureInputViewModel.Amount;
                                tempFixedExpenditure.DepositMonth = fixedExpenditureInputViewModel.DepositMonth;
                                tempFixedExpenditure.DepositDay = fixedExpenditureInputViewModel.DepositDay;
                                tempFixedExpenditure.MaturityDate = new DateTime(Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate, "-")[0].ToString()), Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate, "-")[1].ToString()), Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate, "-")[2].ToString()));
                                tempFixedExpenditure.Note = fixedExpenditureInputViewModel?.Note ?? "";
                                tempFixedExpenditure.PaymentMethod = fixedExpenditureInputViewModel.PaymentMethod;
                                tempFixedExpenditure.MyDepositAsset = "";
                                tempFixedExpenditure.Updated = DateTime.UtcNow;
                                tempFixedExpenditure.Unpunctuality = fixedExpenditureInputViewModel.Unpunctuality;

                                await fixedExpenditureRepository.UpdateFixedExpenditureAsync(tempFixedExpenditure);

                                return Json(new { result = true, message = localizer["The fixedExpenditure has been successfully updated."].Value });
                            }
                            catch (Exception e)
                            {
                                return Json(new { result = false, error = e.ToString() });
                            }
                        }
                        else
                        {
                            return Json(new { result = false, error = localizer["Input is invalid"].Value });
                        }
                    }
                    #endregion

                    #region 대분류 = '소비지출'
                    else if (fixedExpenditureInputViewModel.MainClass == "ConsumerSpending")
                    {
                        if (fixedExpenditureInputViewModel.SubClass == "MealOrEatOutExpenses" ||
                            fixedExpenditureInputViewModel.SubClass == "HousingOrSuppliesCost" ||
                            fixedExpenditureInputViewModel.SubClass == "EducationExpenses" ||
                            fixedExpenditureInputViewModel.SubClass == "MedicalExpenses" ||
                            fixedExpenditureInputViewModel.SubClass == "TransportationCost" ||
                            fixedExpenditureInputViewModel.SubClass == "CommunicationCost" ||
                            fixedExpenditureInputViewModel.SubClass == "LeisureOrCulture" ||
                            fixedExpenditureInputViewModel.SubClass == "ClothingOrShoes" ||
                            fixedExpenditureInputViewModel.SubClass == "PinMoney" ||
                            fixedExpenditureInputViewModel.SubClass == "ProtectionTypeInsurance" ||
                            fixedExpenditureInputViewModel.SubClass == "OtherExpenses" ||
                            fixedExpenditureInputViewModel.SubClass == "UnknownExpenditure")
                        {
                            try
                            {
                                var tempFixedExpenditures = await fixedExpenditureRepository.GetFixedExpendituresAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);
                                var tempFixedExpenditure = tempFixedExpenditures.Where(a => a.Id == fixedExpenditureInputViewModel.Id).FirstOrDefault();

                                if (tempFixedExpenditure == null)
                                {
                                    return Json(new { result = false, error = localizer["Input is invalid"].Value });
                                }

                                tempFixedExpenditure.Id = fixedExpenditureInputViewModel.Id;
                                tempFixedExpenditure.AccountEmail = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email;
                                tempFixedExpenditure.MainClass = fixedExpenditureInputViewModel.MainClass;
                                tempFixedExpenditure.SubClass = fixedExpenditureInputViewModel.SubClass;
                                tempFixedExpenditure.Contents = fixedExpenditureInputViewModel.Contents ?? "";
                                tempFixedExpenditure.Amount = fixedExpenditureInputViewModel.Amount;
                                tempFixedExpenditure.DepositMonth = fixedExpenditureInputViewModel.DepositMonth;
                                tempFixedExpenditure.DepositDay = fixedExpenditureInputViewModel.DepositDay;
                                tempFixedExpenditure.MaturityDate = new DateTime(Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate, "-")[0].ToString()), Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate, "-")[1].ToString()), Convert.ToInt32(Regex.Split(fixedExpenditureInputViewModel.MaturityDate, "-")[2].ToString()));
                                tempFixedExpenditure.Note = fixedExpenditureInputViewModel?.Note ?? "";
                                tempFixedExpenditure.PaymentMethod = fixedExpenditureInputViewModel.PaymentMethod;
                                tempFixedExpenditure.MyDepositAsset = "";
                                tempFixedExpenditure.Updated = DateTime.UtcNow;
                                tempFixedExpenditure.Unpunctuality = fixedExpenditureInputViewModel.Unpunctuality;

                                await fixedExpenditureRepository.UpdateFixedExpenditureAsync(tempFixedExpenditure);

                                return Json(new { result = true, message = localizer["The fixedExpenditure has been successfully updated."].Value });
                            }
                            catch (Exception e)
                            {
                                return Json(new { result = false, error = e.ToString() });
                            }
                        }
                        else
                        {
                            return Json(new { result = false, error = localizer["Input is invalid"].Value });
                        }
                    }
                    #endregion

                    #region 대분류에 없는 값
                    else
                    {
                        return Json(new { result = false, error = localizer["Input is invalid"].Value });
                    }
                    #endregion
                }
                else
                {
                    return Json(new { result = false, error = localizer["Input is invalid"].Value });
                }
            }
            catch (Exception e)
            {
                return Json(new { result = false, error = e.ToString() });
            }
        }
        #endregion

        #region Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> DeleteFixedExpenditure([FromBody] FixedExpenditureInputViewModel fixedExpenditureInputViewModel)
        {
            try
            {
                HttpContext.Session.TryGetValue("AccountSession", out byte[] resultByte);

                var tempFixedExpenditures = await fixedExpenditureRepository.GetFixedExpendituresAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email);

                if (tempFixedExpenditures == null)
                {
                    return Json(new { result = false, error = localizer["Input is invalid"].Value });
                }
                else
                {
                    var tempFixedExpenditure = tempFixedExpenditures.Where(a => a.Id == fixedExpenditureInputViewModel.Id).FirstOrDefault();
                    if (tempFixedExpenditure == null)
                    {
                        return Json(new { result = false, error = localizer["Input is invalid"].Value });
                    }
                    else
                    {
                        await fixedExpenditureRepository.DeleteFixedExpenditureAsync(tempFixedExpenditure);
                        return Json(new { result = true, message = localizer["The fixedExpenditure has been successfully deleted."].Value });
                    }
                }
            }
            catch (Exception e)
            {
                return Json(new { result = false, error = e.ToString() });
            }
        }
        #endregion

        #region Excel
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> ExportExcelFixedExpenditure(string fileName = "")
        {
            // query data from database
            await Task.Yield();

            List<FixedExpenditureOutputViewModel> fixedExpenditureOutputViewModels = new List<FixedExpenditureOutputViewModel>();

            #region FixedExpenditures

            HttpContext.Session.TryGetValue("AccountSession", out byte[] resultByte);

            DateTime currentDate = new DateTime(Convert.ToInt32(Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"), "-")[0]), Convert.ToInt32(Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"), "-")[1]), Convert.ToInt32(Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"), "-")[2]));

            foreach (var item in await fixedExpenditureRepository.GetFixedExpendituresAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email))
            {
                bool noticedResult = false;

                try
                {
                    noticedResult = currentDate.Subtract(new DateTime(Convert.ToInt32(Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"), "-")[0]), item.DepositMonth, item.DepositDay)).TotalDays <= 0 && Math.Abs(currentDate.Subtract(new DateTime(Convert.ToInt32(Regex.Split(DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd"), "-")[0]), item.DepositMonth, item.DepositDay)).TotalDays) <= ServerSetting.NoticeMaturityDateDay;
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
                    MainClass = localizer[item.MainClass.ToString()].Value,
                    SubClass = localizer[item.SubClass.ToString()].Value,
                    Contents = item.Contents,
                    Amount = item.Amount,
                    PaymentMethod = item.PaymentMethod,
                    MyDepositAsset = item.MyDepositAsset,
                    DepositMonth = item.DepositMonth,
                    DepositDay = item.DepositDay,
                    MaturityDate = item.MaturityDate.ToString("yyyy-MM-dd"),
                    Created = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(item.Created, OSTimeZone.DestinationTimeZoneId),
                    Updated = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(item.Updated, OSTimeZone.DestinationTimeZoneId),
                    Note = item.Note,
                    Noticed = noticedResult,
                    Expired = item.MaturityDate.Subtract(currentDate).TotalDays < 0
                });
            }
            #endregion

            var stream = new MemoryStream();

            using (var package = new ExcelPackage(stream))
            {
                var workSheetMenuOutputViewModels = package.Workbook.Worksheets.Add("Sheet1");

                workSheetMenuOutputViewModels.Row(1).Height = 20;
                workSheetMenuOutputViewModels.Row(1).Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                workSheetMenuOutputViewModels.Row(1).Style.Font.Bold = true;
                workSheetMenuOutputViewModels.Cells[1, 1].Value = localizer[nameof(FixedExpenditureOutputViewModel.MainClass).ToString()].Value;
                workSheetMenuOutputViewModels.Cells[1, 2].Value = localizer[nameof(FixedExpenditureOutputViewModel.SubClass).ToString()].Value;
                workSheetMenuOutputViewModels.Cells[1, 3].Value = localizer[nameof(FixedExpenditureOutputViewModel.Contents).ToString()].Value;
                workSheetMenuOutputViewModels.Cells[1, 4].Value = localizer[nameof(FixedExpenditureOutputViewModel.Amount).ToString()].Value;
                workSheetMenuOutputViewModels.Cells[1, 5].Value = localizer[nameof(FixedExpenditureOutputViewModel.PaymentMethod).ToString()].Value;
                workSheetMenuOutputViewModels.Cells[1, 6].Value = localizer[nameof(FixedExpenditureOutputViewModel.MyDepositAsset).ToString()].Value;
                workSheetMenuOutputViewModels.Cells[1, 7].Value = localizer[nameof(FixedExpenditureOutputViewModel.DepositMonth).ToString()].Value;
                workSheetMenuOutputViewModels.Cells[1, 8].Value = localizer[nameof(FixedExpenditureOutputViewModel.DepositDay).ToString()].Value;
                workSheetMenuOutputViewModels.Cells[1, 9].Value = localizer[nameof(FixedExpenditureOutputViewModel.MaturityDate).ToString()].Value;
                workSheetMenuOutputViewModels.Cells[1, 10].Value = localizer[nameof(FixedExpenditureOutputViewModel.Created).ToString()].Value;
                workSheetMenuOutputViewModels.Cells[1, 11].Value = localizer[nameof(FixedExpenditureOutputViewModel.Updated).ToString()].Value;
                workSheetMenuOutputViewModels.Cells[1, 12].Value = localizer[nameof(FixedExpenditureOutputViewModel.Note).ToString()].Value;
                workSheetMenuOutputViewModels.Cells[1, 13].Value = localizer[nameof(FixedExpenditureOutputViewModel.Noticed).ToString()].Value;
                workSheetMenuOutputViewModels.Cells[1, 14].Value = localizer[nameof(FixedExpenditureOutputViewModel.Expired).ToString()].Value;

                int recordIndex = 2;
                foreach (var item in fixedExpenditureOutputViewModels.OrderByDescending(a => a.Expired).ThenByDescending(a => a.Noticed))
                {
                    workSheetMenuOutputViewModels.Cells[recordIndex, 1].Value = localizer[item?.MainClass?.ToString() ?? ""].Value;
                    workSheetMenuOutputViewModels.Cells[recordIndex, 2].Value = localizer[item?.SubClass?.ToString() ?? ""].Value;
                    workSheetMenuOutputViewModels.Cells[recordIndex, 3].Value = item.Contents;
                    workSheetMenuOutputViewModels.Cells[recordIndex, 4].Value = item.Amount;
                    workSheetMenuOutputViewModels.Cells[recordIndex, 5].Value = item.PaymentMethod;
                    workSheetMenuOutputViewModels.Cells[recordIndex, 6].Value = item.MyDepositAsset;
                    workSheetMenuOutputViewModels.Cells[recordIndex, 7].Value = item.DepositMonth;
                    workSheetMenuOutputViewModels.Cells[recordIndex, 8].Value = item.DepositDay;
                    workSheetMenuOutputViewModels.Cells[recordIndex, 9].Value = item.MaturityDate;
                    workSheetMenuOutputViewModels.Cells[recordIndex, 10].Value = item.Created.ToString();
                    workSheetMenuOutputViewModels.Cells[recordIndex, 11].Value = item.Updated.ToString();
                    workSheetMenuOutputViewModels.Cells[recordIndex, 12].Value = item.Note;
                    workSheetMenuOutputViewModels.Cells[recordIndex, 13].Value = item.Noticed.ToString();
                    workSheetMenuOutputViewModels.Cells[recordIndex, 14].Value = item.Expired.ToString();
                    recordIndex++;
                }

                package.Save();
            }
            stream.Position = 0;
            string excelName = $"{fileName}-{TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, OSTimeZone.DestinationTimeZoneId).ToString("yyyy-MM-dd-HH-mm-ss-fff")}.xlsx";

            return File(stream, "application/octet-stream", excelName);
        }
        #endregion

        #endregion
    }
}