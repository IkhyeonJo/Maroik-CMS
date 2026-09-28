using System.Diagnostics;
using Maroik.Website.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace Maroik.Website.Controllers;

/// <summary>Handles application error and access-denied pages.</summary>
public class ExceptionController : Controller
{
    /// <summary>Initializes a new instance of <see cref="ExceptionController"/> with the supplied dependencies.</summary>
    public ExceptionController()
    {
    }

    /// <summary>Returns the access-denied view.</summary>
    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    /// <summary>
    /// Returns the generic error view. Reached through <c>UseExceptionHandler("/Exception/Error")</c>,
    /// which re-executes the pipeline with the <em>original</em> request's HTTP method — a POST that
    /// threw is re-executed as a POST. An action restricted to GET would therefore answer such a
    /// request with an empty 405 instead of the error page, so every method is accepted. The global
    /// <c>AutoValidateAntiforgeryToken</c> is bypassed for the same reason: the handler is re-running
    /// a request whose body is already consumed, and the page it renders is inert (no state change).
    /// </summary>
    [AcceptVerbs("GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS")]
    [IgnoreAntiforgeryToken]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
