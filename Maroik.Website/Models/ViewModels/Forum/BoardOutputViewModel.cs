// ReSharper disable PropertyCanBeMadeInitOnly.Global
using System.ComponentModel.DataAnnotations;

namespace Maroik.Website.Models.ViewModels.Forum;

/// <summary>
/// Read-only view model for displaying the full detail of a single board post.
/// Includes the post body, metadata, view count, and any attached file
/// already resolved to Base64 for inline display.
/// </summary>
public class BoardOutputViewModel
{
    /// <summary>Post title.</summary>
    [Required(ErrorMessage = "Title is required.")]
    [StringLength(100, ErrorMessage = "Must be between 1 and 100 characters.", MinimumLength = 1)]
    public string? Title { get; set; }

    /// <summary>HTML body of the post as saved from the Summernote editor.</summary>
    public string? Content { get; set; }

    /// <summary>When <see langword="true"/> the post is locked and comments are disabled.</summary>
    public bool Locked { get; set; }

    /// <summary>Nickname of the account that created the post.</summary>
    public string? Writer { get; set; }

    /// <summary>UTC timestamp of the most recent edit to this post.</summary>
    public DateTime Updated { get; set; }

    /// <summary>Total number of times this post has been viewed.</summary>
    public long Views { get; set; }

    /// <summary>Original file name of the attached file (null when no attachment exists).</summary>
    public string? BoardAttachedFileName { get; set; }

    /// <summary>Base64-encoded bytes of the attached file, used for inline preview or download link generation.</summary>
    public string? BoardAttachedFileBase64Data { get; set; }

    /// <summary>MIME type of the attached file (e.g. "application/pdf"), used for data-URI or content-type headers.</summary>
    public string? BoardAttachedFileContentType { get; set; }

    /// <summary>File extension of the attached file (e.g. ".pdf"), used for icon selection in the view.</summary>
    public string? BoardAttachedFileExtension { get; set; }

    /// <summary>Size of the attached file in bytes, displayed in the attachment info row.</summary>
    public long BoardAttachedFileSize { get; set; }

    /// <summary>Server-side storage path of the attached file, passed to the file-storage service for download.</summary>
    public string? BoardAttachedFilePath { get; set; }

    /// <summary>
    /// When <see langword="true"/> the post content already contains <c>&lt;img&gt;</c> tags
    /// (embedded by Summernote), so the view can skip additional image preview.
    /// </summary>
    public bool IsImgTagIncluded { get; set; }

    /// <summary>When <see langword="true"/> the current viewer wrote this post and may modify it.</summary>
    public bool CanModify { get; set; }

    /// <summary>When <see langword="true"/> the current viewer wrote this post or is an Admin, and may delete it.</summary>
    public bool CanDelete { get; set; }
}
