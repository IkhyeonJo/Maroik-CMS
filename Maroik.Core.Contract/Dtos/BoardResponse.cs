// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Data transfer object returned when reading a board post.
/// </summary>
public class BoardResponse
{
    /// <summary>Board post ID (primary key).</summary>
    public long Id { get; set; }

    /// <summary>Board type (e.g. "FreeForum", "PrivateNote").</summary>
    public string? Type { get; set; }

    /// <summary>Post title.</summary>
    public string? Title { get; set; }

    /// <summary>Post body in HTML format.</summary>
    public string? Content { get; set; }

    /// <summary>Nickname of the author.</summary>
    public string? Writer { get; set; }

    /// <summary>UTC creation timestamp.</summary>
    public DateTime Created { get; set; }

    /// <summary>UTC last-edited timestamp.</summary>
    public DateTime Updated { get; set; }

    /// <summary>Total view count.</summary>
    public long View { get; set; }

    /// <summary>Soft-delete flag.</summary>
    public bool Deleted { get; set; }

    /// <summary>When true, new comments cannot be added.</summary>
    public bool Locked { get; set; }

    /// <summary>When true, the post is pinned at the top as a notice.</summary>
    public bool Noticed { get; set; }
}
