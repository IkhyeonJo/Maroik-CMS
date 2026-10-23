// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Website.Models.ViewModels.FileManagers;

/// <summary>The folder to open (null = the root).</summary>
public class FileManagerFolderInputViewModel
{
    /// <summary>Folder ID, or null for the root.</summary>
    public long? FolderId { get; set; }
}
