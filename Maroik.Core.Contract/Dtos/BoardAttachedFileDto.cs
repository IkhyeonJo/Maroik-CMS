// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Data transfer object returned when reading a file attached to a board post.
/// </summary>
public class BoardAttachedFileDto
{
    /// <summary>Attached file ID (primary key).</summary>
    public long Id { get; set; }

    /// <summary>ID of the parent board post.</summary>
    public long BoardId { get; set; }

    /// <summary>File size in bytes.</summary>
    public long Size { get; set; }

    /// <summary>Original file name.</summary>
    public string? Name { get; set; }

    /// <summary>File extension (e.g. ".pdf").</summary>
    public string? Extension { get; set; }

    /// <summary>Server-side storage path of the file.</summary>
    public string? Path { get; set; }
}
