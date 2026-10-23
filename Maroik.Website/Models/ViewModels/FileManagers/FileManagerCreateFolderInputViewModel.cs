// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Website.Models.ViewModels.FileManagers;

/// <summary>A new folder (F1), or one a folder upload needs (<see cref="Merge"/>: an existing folder of that name is reused, F2-C).</summary>
public class FileManagerCreateFolderInputViewModel
{
    /// <summary>The parent folder (null = the root).</summary>
    public long? ParentId { get; set; }

    /// <summary>The folder name.</summary>
    public string? Name { get; set; }

    /// <summary>Reuse an existing folder of that name instead of refusing it.</summary>
    public bool Merge { get; set; }
}
