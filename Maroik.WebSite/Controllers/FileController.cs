using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;

namespace Maroik.WebSite.Controllers
{
    public class FileController : Controller
    {
        private readonly IHtmlLocalizer<FileController> _localizer;
        private readonly ILogger<FileController> _logger;

        public FileController(IHtmlLocalizer<FileController> localizer, ILogger<FileController> logger)
        {
            _localizer = localizer;
            _logger = logger;
        }

        #region File

        #region Read
        [HttpGet]
        public IActionResult AdminIndex()
        {
            return View();
        }

        [HttpGet]
        public IActionResult UserIndex()
        {
            return View();
        }

        [HttpGet]
        public IActionResult AnonymousIndex()
        {
            return View();
        }
        #endregion

        #endregion
    }
}