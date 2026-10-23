// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Website.Models.ViewModels.FileManagers;

/// <summary>The left panel's data: the folder tree and the storage usage (U1–U3, F7).</summary>
public class FileManagerDriveViewModel
{
    /// <summary>The usage label, "12.3 GB / 30 GB".</summary>
    public string Usage { get; set; } = "";

    /// <summary>The usage bar's fill, 0–100 (100 once the quota is reached or exceeded).</summary>
    public int UsedPercent { get; set; }

    /// <summary>The number of items directly at the root.</summary>
    public int RootItemCount { get; set; }

    /// <summary>Every folder, with its parent and item count.</summary>
    public List<FileManagerItemViewModel> Folders { get; set; } = [];
}
