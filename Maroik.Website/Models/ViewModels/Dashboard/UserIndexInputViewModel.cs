// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Website.Models.ViewModels.Dashboard;

/// <summary>
/// Slim input model for the Dashboard index page.
/// Carries only the user's preferred currency so the controller can
/// filter or label monetary values before building <see cref="UserIndexOutputViewModel"/>.
/// </summary>
public class UserIndexInputViewModel
{
    /// <summary>
    /// The currency code the user has selected as their default display currency (e.g. "KRW", "USD").
    /// Passed as a query parameter or form field when the user changes the currency selector.
    /// </summary>
    public string? DefaultMonetaryUnit { get; set; }
}
