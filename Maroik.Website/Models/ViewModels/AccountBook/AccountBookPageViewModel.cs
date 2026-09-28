// ReSharper disable PropertyCanBeMadeInitOnly.Global
using Maroik.Core.Contract.Dtos;

namespace Maroik.Website.Models.ViewModels.AccountBook;

/// <summary>
/// Page-level view model for the Asset, Income, and Expenditure pages.
/// Carries the active asset list for dropdowns and the user's current local time
/// for pre-populating date/time pickers.
/// </summary>
public class AccountBookPageViewModel
{
    /// <summary>Active (non-deleted) assets belonging to the logged-in user, used to populate the asset-selection dropdowns.</summary>
    public List<AssetResponse> Assets { get; set; } = [];

    /// <summary>Product name of the asset that should be pre-selected in the asset-selection dropdowns.</summary>
    public string? DefaultAssetProductName => Assets.FirstOrDefault()?.ProductName;

    /// <summary>Today's date string (date-picker format) in the user's local time zone.</summary>
    public string CurrentDate { get; set; } = "";

    /// <summary>Current hour component (0–23) in the user's local time zone, used to pre-populate the time picker.</summary>
    public int CurrentHour { get; set; }

    /// <summary>Current minute component (0–59) in the user's local time zone.</summary>
    public int CurrentMinute { get; set; }

    /// <summary>Current second component (0–59) in the user's local time zone.</summary>
    public int CurrentSecond { get; set; }
}
