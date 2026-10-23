// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Website.Models.ViewModels.FileManagers;

/// <summary>One folder or file as the File Manager page draws it (a tile, a list row, a tree node or a search result).</summary>
public class FileManagerItemViewModel
{
    /// <summary>Node ID.</summary>
    public long Id { get; set; }

    /// <summary>The folder it is in (null = the drive root).</summary>
    public long? ParentId { get; set; }

    /// <summary>The name.</summary>
    public string Name { get; set; } = "";

    /// <summary>True for a folder.</summary>
    public bool IsFolder { get; set; }

    /// <summary>The size label ("12.3 MB"); empty for a folder.</summary>
    public string Size { get; set; } = "";

    /// <summary>The last change in the user's time zone, "yyyy-MM-dd HH:mm".</summary>
    public string Modified { get; set; } = "";

    /// <summary>The Font Awesome classes of its icon (file type colours, U7).</summary>
    public string Icon { get; set; } = "";

    /// <summary>Whether a Preview action is offered (decided by the server, F9).</summary>
    public bool Previewable { get; set; }

    /// <summary>For a search result: where it is ("My Drive / Documents"); empty otherwise.</summary>
    public string Location { get; set; } = "";

    /// <summary>For a folder: the number of items directly in it (U3).</summary>
    public int ItemCount { get; set; }
}
