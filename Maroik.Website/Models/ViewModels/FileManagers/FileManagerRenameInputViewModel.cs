// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Website.Models.ViewModels.FileManagers;

/// <summary>A rename (F4).</summary>
public class FileManagerRenameInputViewModel
{
    /// <summary>The item to rename.</summary>
    public long Id { get; set; }

    /// <summary>The new name.</summary>
    public string? Name { get; set; }
}
