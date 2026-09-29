// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Website.Models.ViewModels.Dashboard;

/// <summary>
/// JSON request body of <c>DashboardController.UserUpdateDefaultMonetary</c>: the currency the user picked
/// in the dashboard's currency selector, to be saved as the account's default.
/// </summary>
public class UserIndexInputViewModel
{
    /// <summary>
    /// The currency code the user has selected as their default display currency (e.g. "KRW", "USD").
    /// Saved only when it is the currency of one of the account's non-deleted assets; otherwise the default is cleared.
    /// </summary>
    public string? DefaultMonetaryUnit { get; set; }
}
