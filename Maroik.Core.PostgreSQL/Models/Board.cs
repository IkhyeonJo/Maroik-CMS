using System;
using System.Collections.Generic;

namespace Maroik.Core.PostgreSQL.Models;

/// <summary>
/// Board
/// </summary>
public partial class Board
{
    /// <summary>
    /// PK
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Type
    /// </summary>
    public string Type { get; set; } = null!;

    /// <summary>
    /// Title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Content
    /// </summary>
    public string Content { get; set; } = null!;

    /// <summary>
    /// Writer
    /// </summary>
    public string Writer { get; set; } = null!;

    /// <summary>
    /// Created
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// Updated
    /// </summary>
    public DateTime Updated { get; set; }

    /// <summary>
    /// View
    /// </summary>
    public long View { get; set; }

    /// <summary>
    /// Deleted
    /// </summary>
    public bool Deleted { get; set; }

    /// <summary>
    /// Locked
    /// </summary>
    public bool Locked { get; set; }

    /// <summary>
    /// Noticed
    /// </summary>
    public bool Noticed { get; set; }

    /// <summary>Files attached to this post.</summary>
    public virtual ICollection<BoardAttachedFile> BoardAttachedFiles { get; set; } = new List<BoardAttachedFile>();

    /// <summary>Comments on this post (soft-deleted ones included).</summary>
    public virtual ICollection<BoardComment> BoardComments { get; set; } = new List<BoardComment>();

    /// <summary>The account that wrote this post (Writer → Account.Nickname).</summary>
    public virtual Account WriterNavigation { get; set; } = null!;
}
