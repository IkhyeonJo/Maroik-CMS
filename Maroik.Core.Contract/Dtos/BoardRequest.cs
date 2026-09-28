// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Data transfer object used to create or update a board post.
/// Supports multiple board types (FreeForum, PrivateNote) through the <see cref="Type"/> field.
/// </summary>
public class BoardRequest
{
    /// <summary>Board post ID (auto-incremented primary key; 0 for new posts).</summary>
    public long Id { get; set; }

    /// <summary>Board type identifier (e.g. "FreeForum", "PrivateNote").</summary>
    public string? Type { get; set; }

    /// <summary>Post title (max 255 characters).</summary>
    public string? Title { get; set; }

    /// <summary>Post body content in Summernote WYSIWYG HTML format.</summary>
    public string? Content { get; set; }

    /// <summary>Nickname of the author.</summary>
    public string? Writer { get; set; }

    /// <summary>When true, the post is locked and no new comments can be added.</summary>
    public bool Locked { get; set; }

    /// <summary>When true, the post is pinned at the top of the list as a notice.</summary>
    public bool Noticed { get; set; }
}
