using Maroik.Common.DataAccess.Contracts;
using Maroik.Common.DataAccess.Models;
using Maroik.Common.Miscellaneous.Extensions;
using Maroik.Common.Miscellaneous.Utilities;
using Maroik.WebSite.Models.ViewModels.Account;
using Maroik.WebSite.Models.ViewModels.DashBoard;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Text;

namespace Maroik.WebSite.Controllers
{
    public class DashBoardController(
        IIncomeRepository incomeRepository,
        IExpenditureRepository expenditureRepository,
        IAssetRepository assetRepository,
        IAccountRepository accountRepository)
        : Controller
    {
        private const string HostResourceFilePath = "/app/Maroik.Log/HostResourceInfo.txt";
        private const string DockerResourceFilePath = "/app/Maroik.Log/DockerResourceInfo.txt";

        #region 언어 변경
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.Admin)]
        [RequiredHttpPostAccess(Role = Role.User)]
        [RequiredHttpPostAccess(Role = Role.Anonymous)]
        public IActionResult CultureManagement([FromBody] LoginInputViewModel loginInputViewModel)
        {
            Response.Cookies.Append(CookieRequestCultureProvider.DefaultCookieName, CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(loginInputViewModel.Culture)),
                new CookieOptions { Expires = DateTimeOffset.Now.AddDays(30), Secure = true, HttpOnly = true, SameSite = SameSiteMode.Strict });

            return Json(new { result = true });
        }
        #endregion

        #region Admin
        [HttpGet]
        public IActionResult AdminIndex()
        {
            var hostResourceFileText = string.Empty;
            var dockerResourceFileText = string.Empty;

            if (System.IO.File.Exists(HostResourceFilePath))
            {
                hostResourceFileText = System.IO.File.ReadAllText(HostResourceFilePath);
            }

            if (System.IO.File.Exists(DockerResourceFilePath))
            {
                dockerResourceFileText = System.IO.File.ReadAllText(DockerResourceFilePath);
            }

            ViewBag.HostResourceFileText = hostResourceFileText;
            ViewBag.DockerResourceFileText = dockerResourceFileText;

            return View();
        }
        #endregion

        #region User

        #region Read
        [HttpGet]
        public async Task<IActionResult> UserIndex(string year, string month)
        {
            _ = HttpContext.Session.TryGetValue(EnumHelper.GetDescription(Session.Account), out var resultByte);

            var loginedAccountTimeZoneIanaId = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte ?? [])).TimeZoneIanaId;

            var userIndexOutputViewModel = new UserIndexOutputViewModel();
            var currentLoginUser = await accountRepository.GetAccountByEmailAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte ?? [])).Email);
            var assets = (await assetRepository.GetAssetsAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte ?? [])).Email)) ?? [];

            #region For Demo User [Only Demo purpose]
            if (currentLoginUser?.Email == "demo@maroik.com")
            {
                year = "2025";
                month = "06";
            }
            #endregion
            
            #region Year & Month
            if (string.IsNullOrEmpty(year))
            {
                year = DateTime.UtcNow.Year.ToString();
            }
            else
            {
                try
                {
                    year = Convert.ToInt32(year).ToString();
                }
                catch
                {
                    year = DateTime.UtcNow.Year.ToString();
                }
            }

            if (string.IsNullOrEmpty(month))
            {
                month = DateTime.UtcNow.Month.ToString("00");
            }
            else
            {
                try
                {
                    month = Convert.ToInt32(month).ToString("00");
                }
                catch
                {
                    month = DateTime.UtcNow.Month.ToString("00");
                }
            }
            #endregion

            #region Set if currentLoginUser's DefaultMonetaryUnit is not existed in Assets
            switch (currentLoginUser?.DefaultMonetaryUnit)
            {
                case null when assets.Count == 0:
                {
                    if (currentLoginUser != null)
                    {
                        currentLoginUser.DefaultMonetaryUnit = null;
                        await accountRepository.UpdateAccountAsync(currentLoginUser);
                    }

                    break;
                }
                case null when assets.Count != 0:
                {
                    var mostUsedMonetaryUnit = assets
                        .GroupBy(a => a.MonetaryUnit)
                        .OrderByDescending(g => g.Count())
                        .FirstOrDefault();
                    
                    if (currentLoginUser != null)
                    {
                        currentLoginUser.DefaultMonetaryUnit = mostUsedMonetaryUnit?.Key;
                        await accountRepository.UpdateAccountAsync(currentLoginUser);
                    }

                    break;
                }
                default:
                {
                    if (currentLoginUser?.DefaultMonetaryUnit != null && assets.Count == 0)
                    {
                        currentLoginUser.DefaultMonetaryUnit = null;
                        await accountRepository.UpdateAccountAsync(currentLoginUser);
                    }
                    else if (currentLoginUser?.DefaultMonetaryUnit != null && assets.Count != 0)
                    {
                        if (!assets.Select(x => x.MonetaryUnit).Distinct().ToList().Contains(currentLoginUser.DefaultMonetaryUnit))
                        {
                            var mostUsedMonetaryUnit = assets
                                .GroupBy(a => a.MonetaryUnit)
                                .OrderByDescending(g => g.Count())
                                .FirstOrDefault();
                            currentLoginUser.DefaultMonetaryUnit = mostUsedMonetaryUnit?.Key;
                            await accountRepository.UpdateAccountAsync(currentLoginUser);
                        }
                    }

                    break;
                }
            }

            userIndexOutputViewModel.DefaultMonetaryUnit = currentLoginUser?.DefaultMonetaryUnit;
            #endregion
            
            #region Current Year Income/Expenditure & MonetaryUnits
            if (userIndexOutputViewModel.DefaultMonetaryUnit != null)
            {
                var currentYear = $"{year}";
                var currentLoginUserEmail = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte ?? [])).Email;

                var incomes = await incomeRepository.GetCurrentYearIncomesAsync(currentLoginUserEmail, currentYear, JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte ?? [])).TimeZoneIanaId);
                userIndexOutputViewModel.IncomeYearOutputViewModels = [];
                foreach (var item in incomes)
                {
                    userIndexOutputViewModel.IncomeYearOutputViewModels.Add(new IncomeOutputViewModel()
                    {
                        Id = item.Id,
                        MainClass = item.MainClass,
                        SubClass = item.SubClass,
                        Content = item.Content,
                        Amount = Convert.ToDecimal(item.Amount.TrimTrailingZeros()),
                        MonetaryUnit = assets.FirstOrDefault(x => x.ProductName == item.DepositMyAssetProductName)?.MonetaryUnit,
                        DepositMyAssetProductName = item.DepositMyAssetProductName,
                        Created = item.Created.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId),
                        Updated = item.Updated.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId),
                        Note = item.Note
                    });
                }

                var expenditures = await expenditureRepository.GetCurrentYearExpendituresAsync(currentLoginUserEmail, currentYear, JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte ?? [])).TimeZoneIanaId);
                userIndexOutputViewModel.ExpenditureYearOutputViewModels = [];
                foreach (var item in expenditures)
                {
                    userIndexOutputViewModel.ExpenditureYearOutputViewModels.Add(new ExpenditureOutputViewModel()
                    {
                        Id = item.Id,
                        MainClass = item.MainClass,
                        SubClass = item.SubClass,
                        Content = item.Content,
                        Amount = Convert.ToDecimal(item.Amount.TrimTrailingZeros()),
                        MonetaryUnit = assets.FirstOrDefault(x => x.ProductName == item.PaymentMethod)?.MonetaryUnit,
                        PaymentMethod = item.PaymentMethod,
                        Note = item.Note,
                        MyDepositAsset = item.MyDepositAsset,
                        Created = item.Created.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId),
                        Updated = item.Updated.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId)
                    });
                }
                userIndexOutputViewModel.IncomeYearOutputViewModels = userIndexOutputViewModel.IncomeYearOutputViewModels.Where(x => x.MonetaryUnit == userIndexOutputViewModel.DefaultMonetaryUnit).ToList();
                userIndexOutputViewModel.ExpenditureYearOutputViewModels = userIndexOutputViewModel.ExpenditureYearOutputViewModels.Where(x => x.MonetaryUnit == userIndexOutputViewModel.DefaultMonetaryUnit).ToList();
            }
            #endregion

            #region Current Year & Month Income/Expenditure & MonetaryUnits
            if (userIndexOutputViewModel.DefaultMonetaryUnit != null)
            {
                var currentYearMonth = $"{year}-{month}";
                var currentLoginUserEmail = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte ?? [])).Email;

                var incomes = await incomeRepository.GetCurrentYearMonthIncomesAsync(currentLoginUserEmail, currentYearMonth, JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte ?? [])).TimeZoneIanaId);
                userIndexOutputViewModel.IncomeYearMonthOutputViewModels = [];
                foreach (var item in incomes)
                {
                    userIndexOutputViewModel.IncomeYearMonthOutputViewModels.Add(new IncomeOutputViewModel()
                    {
                        Id = item.Id,
                        MainClass = item.MainClass,
                        SubClass = item.SubClass,
                        Content = item.Content,
                        Amount = Convert.ToDecimal(item.Amount.TrimTrailingZeros()),
                        MonetaryUnit = assets.FirstOrDefault(x => x.ProductName == item.DepositMyAssetProductName)?.MonetaryUnit,
                        DepositMyAssetProductName = item.DepositMyAssetProductName,
                        Created = item.Created.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId),
                        Updated = item.Updated.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId),
                        Note = item.Note
                    });
                }

                var expenditures = await expenditureRepository.GetCurrentYearMonthExpendituresAsync(currentLoginUserEmail, currentYearMonth, JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte ?? [])).TimeZoneIanaId);
                userIndexOutputViewModel.ExpenditureYearMonthOutputViewModels = [];
                foreach (var item in expenditures)
                {
                    userIndexOutputViewModel.ExpenditureYearMonthOutputViewModels.Add(new ExpenditureOutputViewModel()
                    {
                        Id = item.Id,
                        MainClass = item.MainClass,
                        SubClass = item.SubClass,
                        Content = item.Content,
                        Amount = Convert.ToDecimal(item.Amount.TrimTrailingZeros()),
                        MonetaryUnit = assets.FirstOrDefault(x => x.ProductName == item.PaymentMethod)?.MonetaryUnit,
                        PaymentMethod = item.PaymentMethod,
                        Note = item.Note,
                        MyDepositAsset = item.MyDepositAsset,
                        Created = item.Created.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId),
                        Updated = item.Updated.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId)
                    });
                }
                userIndexOutputViewModel.IncomeYearMonthOutputViewModels = userIndexOutputViewModel.IncomeYearMonthOutputViewModels.Where(x => x.MonetaryUnit == userIndexOutputViewModel.DefaultMonetaryUnit).ToList();
                userIndexOutputViewModel.ExpenditureYearMonthOutputViewModels = userIndexOutputViewModel.ExpenditureYearMonthOutputViewModels.Where(x => x.MonetaryUnit == userIndexOutputViewModel.DefaultMonetaryUnit).ToList();
            }
            #endregion

            #region Set MonetaryUnits
            if (assets?.Count != 0)
            {
                userIndexOutputViewModel.MonetaryUnits = assets?.Select(x => x?.MonetaryUnit).Distinct().ToList() ?? [];
            }
            #endregion

            #region StartYear
            var firstCreatedIncome = (await incomeRepository.GetIncomesAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte ?? [])).Email)).OrderBy(x => x.Created).ToList().FirstOrDefault() ?? new Income();
            var firstCreatedExpenditure = (await expenditureRepository.GetExpendituresAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte ?? [])).Email)).OrderBy(x => x.Created).ToList().FirstOrDefault() ?? new Expenditure();

            if (firstCreatedIncome.Created == DateTime.MinValue)
            {
                firstCreatedIncome.Created = DateTime.MaxValue;
            }

            if (firstCreatedExpenditure.Created == DateTime.MinValue)
            {
                firstCreatedExpenditure.Created = DateTime.MaxValue;
            }

            var firstCreatedIncomeOrExpenditure = DateTime.Compare(firstCreatedIncome.Created, firstCreatedExpenditure.Created) < 0
                ? firstCreatedIncome.Created
                : firstCreatedExpenditure.Created;
            if (firstCreatedIncomeOrExpenditure == DateTime.MaxValue)
            {
                firstCreatedIncomeOrExpenditure = DateTime.UtcNow;
            }

            firstCreatedIncomeOrExpenditure = firstCreatedIncomeOrExpenditure.ConvertTimeByTimeZoneIanaId(loginedAccountTimeZoneIanaId);
            #endregion

            ViewBag.StartYear = firstCreatedIncomeOrExpenditure.Year.ToString();
            ViewBag.SelectedYear = year;
            ViewBag.SelectedMonth = month;

            return View(userIndexOutputViewModel);
        }
        #endregion

        #region Update
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequiredHttpPostAccess(Role = Role.User)]
        public async Task<IActionResult> UserUpdateDefaultMonetary([FromBody] UserIndexInputViewModel userIndexInputViewModel)
        {
            try
            {
                _ = HttpContext.Session.TryGetValue(EnumHelper.GetDescription(Session.Account), out var resultByte);
                var assets = (await assetRepository.GetAssetsAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte ?? [])).Email)) ?? [];

                var currentLoginUser = await accountRepository.GetAccountByEmailAsync(JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte ?? [])).Email);

                currentLoginUser.DefaultMonetaryUnit = !assets.Select(x => x.MonetaryUnit).Distinct().ToList().Contains(userIndexInputViewModel.DefaultMonetaryUnit)
                    ? null
                    : userIndexInputViewModel.DefaultMonetaryUnit;

                await accountRepository.UpdateAccountAsync(currentLoginUser);

                return Json(new { result = true });

            }
            catch
            {
                return Json(new { result = false });
            }
        }
        #endregion

        #endregion

        #region Anonymous
        [HttpGet]
        public IActionResult AnonymousIndex()
        {
            var isAccountSessionExist = HttpContext.Session.TryGetValue(EnumHelper.GetDescription(Session.Account), out var resultByte);

            if (!isAccountSessionExist) return View();
            var loginedAccount = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte));
            return loginedAccount.Role switch
            {
                Role.Admin => RedirectToAction("AdminIndex", "DashBoard"),
                Role.User => RedirectToAction("UserIndex", "DashBoard"),
                _ => null
            };
        }
        #endregion
    }
}
