// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Website.Models.ViewModels.FileManagers;

/// <summary>Starts the upload of one file.</summary>
public class FileManagerUploadInputViewModel
{
    /// <summary>The folder to upload into (null = the root).</summary>
    public long? ParentId { get; set; }

    /// <summary>The file's name.</summary>
    public string? Name { get; set; }

    /// <summary>The file's size in bytes.</summary>
    public long Size { get; set; }

    /// <summary>
    /// What to do with a name already used in the folder: a <c>FileManagerConflictChoice</c> name ("Ask", "Overwrite" or "KeepBoth");
    /// anything else is invalid input.
    /// </summary>
    public string? Conflict { get; set; }
}
