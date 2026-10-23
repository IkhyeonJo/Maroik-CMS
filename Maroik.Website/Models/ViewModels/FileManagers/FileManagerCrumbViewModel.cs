// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Website.Models.ViewModels.FileManagers;

/// <summary>One breadcrumb step (U4).</summary>
public class FileManagerCrumbViewModel
{
    /// <summary>Folder ID.</summary>
    public long Id { get; set; }

    /// <summary>Folder name.</summary>
    public string Name { get; set; } = "";
}
