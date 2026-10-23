// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
using Maroik.Core.Domain.FileManagers;

namespace Maroik.Core.Contract.Dtos;

/// <summary>Starts the upload of one file into a File Manager folder.</summary>
public class FileManagerUploadRequest
{
    /// <summary>The folder to upload into (null = the drive root).</summary>
    public long? ParentId { get; set; }

    /// <summary>The file's name.</summary>
    public string? Name { get; set; }

    /// <summary>The file's size in bytes (reserved against the quota until the upload ends).</summary>
    public long Size { get; set; }

    /// <summary>What to do when a finished file of the same name is already in the folder.</summary>
    public FileManagerConflictChoice Conflict { get; set; }
}
