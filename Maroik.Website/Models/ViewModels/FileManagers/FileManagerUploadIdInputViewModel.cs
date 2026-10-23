// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Website.Models.ViewModels.FileManagers;

/// <summary>Names an upload to complete or abort; a completion also says whether it overwrites a file of the same name.</summary>
public class FileManagerUploadIdInputViewModel
{
    /// <summary>The upload id.</summary>
    public Guid UploadId { get; set; }

    /// <summary>For a completion: replace a finished file of the same name.</summary>
    public bool Overwrite { get; set; }
}
