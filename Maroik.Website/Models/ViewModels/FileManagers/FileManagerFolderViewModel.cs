// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Website.Models.ViewModels.FileManagers;

/// <summary>An open folder: its breadcrumb and its items, folders first.</summary>
public class FileManagerFolderViewModel
{
    /// <summary>The open folder (null = the drive root).</summary>
    public long? FolderId { get; set; }

    /// <summary>The folders from the root's first level down to the open one.</summary>
    public List<FileManagerCrumbViewModel> Breadcrumb { get; set; } = [];

    /// <summary>The items.</summary>
    public List<FileManagerItemViewModel> Items { get; set; } = [];
}
