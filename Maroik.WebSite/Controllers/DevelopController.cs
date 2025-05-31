using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;

namespace Maroik.WebSite.Controllers
{
    public class DevelopController : Controller
    {
        private readonly IHtmlLocalizer<DevelopController> _localizer;
        private readonly ILogger<DevelopController> _logger;

        public DevelopController(IHtmlLocalizer<DevelopController> localizer, ILogger<DevelopController> logger)
        {
            _localizer = localizer;
            _logger = logger;
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