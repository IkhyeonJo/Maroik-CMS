// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>One folder or file of a File Manager drive, as the screen lists it.</summary>
public class FileManagerItemResponse
{
    /// <summary>Node ID.</summary>
    public long Id { get; set; }

    /// <summary>The folder it is in (null = the drive root).</summary>
    public long? ParentId { get; set; }

    /// <summary>The name shown to the user.</summary>
    public string Name { get; set; } = "";

    /// <summary>True for a folder.</summary>
    public bool IsFolder { get; set; }

    /// <summary>The file's size in bytes (0 for a folder).</summary>
    public long Size { get; set; }

    /// <summary>UTC time of the last change.</summary>
    public DateTime Updated { get; set; }

    /// <summary>For a folder: the number of finished items directly in it (U3).</summary>
    public int ItemCount { get; set; }

    /// <summary>Whether the file may be previewed (an image within the preview limit, F9).</summary>
    public bool Previewable { get; set; }

    /// <summary>The names of the folders above it, from the root down (shown with a search result, U5).</summary>
    public List<string> Location { get; set; } = [];
}
