// ReSharper disable UnusedAutoPropertyAccessor.Global
using System.ComponentModel.DataAnnotations;
using Maroik.Core.Domain.Primitives;

namespace Maroik.Website.Models.ViewModels.Managements;

/// <summary>
/// Form model for creating or editing the caller's own PrivateNote post.
/// A private note is single-owner content, so it has no pin/lock affordances
/// (see <see cref="Maroik.Core.Domain.Boards.Board.IsPrivateNote"/>).
/// </summary>
public class BoardInputViewModel
{
    /// <summary>Database row ID; 0 for a new post, positive for an edit.</summary>
    public int Id { get; set; }

    /// <summary>Post title; must be 1 to <see cref="TitledContentPolicy.MaxTitleLength"/> characters.</summary>
    [Required(ErrorMessage = "Title is required.")]
    [StringLength(TitledContentPolicy.MaxTitleLength, ErrorMessage = "Must be between {2} and {1} characters.", MinimumLength = 1)]
    public string? Title { get; set; }

    /// <summary>HTML body of the post produced by the Summernote WYSIWYG editor.</summary>
    public string? Content { get; set; }

    /// <summary>Optional single file attachment uploaded with the post (max one file per post).</summary>
    public IFormFile? UploadedFile { get; set; }
}
