using System;
using System.Collections.Generic;

namespace Maroik.Core.PostgreSQL.Models;

/// <summary>
/// BoardComment
/// </summary>
public partial class BoardComment
{
    /// <summary>
    /// PK
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Board Id
    /// </summary>
    public long BoardId { get; set; }

    /// <summary>
    /// Order
    /// </summary>
    public long Order { get; set; }

    /// <summary>
    /// AvatarImagePath
    /// </summary>
    public string AvatarImagePath { get; set; } = null!;

    /// <summary>
    /// Writer
    /// </summary>
    public string Writer { get; set; } = null!;

    /// <summary>
    /// Content
    /// </summary>
    public string Content { get; set; } = null!;

    /// <summary>
    /// Created
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// Deleted
    /// </summary>
    public bool Deleted { get; set; }

    /// <summary>The post this comment belongs to.</summary>
    public virtual Board Board { get; set; } = null!;
}
