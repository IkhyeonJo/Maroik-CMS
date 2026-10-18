using Microsoft.AspNetCore.Mvc;

namespace Maroik.Website.Tests.Filters;

/// <summary>Minimal <see cref="Controller"/> subclass used as the action target in filter tests.</summary>
internal class FakeController : Controller
{
    /// <summary>No-op action used only as a target for the filter pipeline under test.</summary>
    public IActionResult Index() => Ok();
}
