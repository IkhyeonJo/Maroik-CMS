// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Website.Models.ViewModels.FileManagers;

/// <summary>One chunk of an upload, posted as a multipart form (P5).</summary>
public class FileManagerChunkInputViewModel
{
    /// <summary>The upload id.</summary>
    public Guid UploadId { get; set; }

    /// <summary>Where in the file the chunk starts.</summary>
    public long Offset { get; set; }

    /// <summary>The chunk's bytes.</summary>
    public IFormFile? Chunk { get; set; }
}
