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
using MyLaboratory.WebSite.Contracts;
using MyLaboratory.Common.DataAccess.Contracts;
using System.Threading.Tasks;
using Newtonsoft.Json;
using MyLaboratory.Common.DataAccess.Models;
using System.Text;
using MyLaboratory.WebSite.Models.ViewModels.DashBoard;

namespace MyLaboratory.WebSite.Controllers
{
    public class DashBoardController : Controller
    {
        private readonly ILogger<DashBoardController> logger;
        private readonly IIncomeRepository incomeRepository;
        private readonly IExpenditureRepository expenditureRepository;

        public DashBoardController(ILogger<DashBoardController> logger, IIncomeRepository incomeRepository, IExpenditureRepository expenditureRepository)
        {
            this.logger = logger;
            this.incomeRepository = incomeRepository;
            this.expenditureRepository = expenditureRepository;
        }

        #region Admin
        [HttpGet]
        public IActionResult AdminIndex()
        {
            return View();
        }
        #endregion

        #region User
        [HttpGet]
        public async Task<IActionResult> UserIndex()
        {
            #region Current Year & Month Income/Expenditure
            HttpContext.Session.TryGetValue("AccountSession", out byte[] resultByte);
            var currentYearMonth = "2022-07";
            var currentLoginUserEmail = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)).Email;

            var incomes = await incomeRepository.GetCurrentYearMonthIncomesAsync(currentLoginUserEmail, currentYearMonth);
            var expenditures = await expenditureRepository.GetCurrentYearMonthExpendituresAsync(currentLoginUserEmail, currentYearMonth);

            var userIndexOutputViewModel = new UserIndexOutputViewModel();
            userIndexOutputViewModel.Incomes = incomes;
            userIndexOutputViewModel.Expenditures = expenditures;
            #endregion

            return View(userIndexOutputViewModel);
        }
        #endregion

    }
}
