// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>An open File Manager folder: its place in the drive and its items (folders first, then by name).</summary>
public class FileManagerFolderResponse
{
    /// <summary>The open folder (null = the drive root).</summary>
    public long? FolderId { get; set; }

    /// <summary>The folders from the root's first level down to the open folder (empty at the root).</summary>
    public List<FileManagerCrumbResponse> Breadcrumb { get; set; } = [];

    /// <summary>The finished items directly in the folder.</summary>
    public List<FileManagerItemResponse> Items { get; set; } = [];
}
