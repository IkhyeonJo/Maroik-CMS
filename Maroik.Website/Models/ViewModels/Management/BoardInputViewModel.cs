// ReSharper disable UnusedAutoPropertyAccessor.Global
using System.ComponentModel.DataAnnotations;

namespace Maroik.Website.Models.ViewModels.Management;

/// <summary>
/// Form model for creating or editing the caller's own PrivateNote post.
/// A private note is single-owner content, so it has no pin/lock affordances
/// (see <see cref="Maroik.Core.Domain.Board.Board.IsPrivateNote"/>).
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

    /// <summary>Optional single file attachment uploaded with the post (max one file per post).</summary>
    public IFormFile? UploadedFile { get; set; }
}
