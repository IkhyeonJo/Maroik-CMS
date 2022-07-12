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
using MyLaboratory.WebSite.Helpers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Localization;

namespace MyLaboratory.WebSite.Controllers
{
    public class DevelopController : Controller
    {
        private readonly IHtmlLocalizer<DevelopController> localizer;
        private readonly ILogger<DevelopController> logger;

        public DevelopController(IHtmlLocalizer<DevelopController> localizer, ILogger<DevelopController> logger)
        {
            this.localizer = localizer;
            this.logger = logger;
        }

        #region WebAPI

        #region Read
        [HttpGet]
        public IActionResult API()
        {
            return View();
        }
        #endregion

        #endregion
    }
}