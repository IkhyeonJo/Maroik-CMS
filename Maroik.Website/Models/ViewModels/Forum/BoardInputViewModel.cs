// ReSharper disable UnusedAutoPropertyAccessor.Global
using System.ComponentModel.DataAnnotations;

namespace Maroik.Website.Models.ViewModels.Forum;

/// <summary>
/// Form model for creating or editing a board post (FreeForum or PrivateNote).
/// Bound from the POST request when a user submits the post editor.
/// </summary>
public class BoardInputViewModel
{
    /// <summary>Database row ID; 0 for a new post, positive for an edit.</summary>
    public int Id { get; set; }

    /// <summary>Post title; must be 1–100 characters.</summary>
    [Required(ErrorMessage = "Title is required.")]
    [StringLength(100, ErrorMessage = "Must be between 1 and 100 characters.", MinimumLength = 1)]
    public string? Title { get; set; }

    /// <summary>HTML body of the post produced by the Summernote WYSIWYG editor.</summary>
    public string? Content { get; set; }

    /// <summary>When <see langword="true"/> the post is pinned to the top of the list as a notice.</summary>
    public bool Noticed { get; set; }

    /// <summary>When <see langword="true"/> the post is read-only and comments cannot be added.</summary>
    public bool Locked { get; set; }

    /// <summary>Optional single file attachment uploaded with the post (max one file per post).</summary>
    public IFormFile? UploadedFile { get; set; }
}
