using System;
using System.Collections.Generic;

namespace Maroik.Core.PostgreSQL.Models;

/// <summary>
/// FileManagerNode
/// </summary>
public partial class FileManagerNode
{
    /// <summary>
    /// PK
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Account Email (ID)
    /// </summary>
    public string AccountEmail { get; set; } = null!;

    /// <summary>
    /// Parent folder Id (NULL = the drive root)
    /// </summary>
    public long? ParentId { get; set; }

    /// <summary>
    /// Name shown to the user
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Folder (true) or file (false)
    /// </summary>
    public bool IsFolder { get; set; }

    /// <summary>
    /// File size in bytes (0 for a folder)
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// upload/FileManager/{GUID} (NULL for a folder)
    /// </summary>
    public string? StoragePath { get; set; }

    /// <summary>
    /// Upload started but not completed
    /// </summary>
    public bool Uploading { get; set; }

    /// <summary>
    /// Created
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// Updated
    /// </summary>
    public DateTime Updated { get; set; }

    /// <summary>The account that owns this node.</summary>
    public virtual Account AccountEmailNavigation { get; set; } = null!;

    /// <summary>The nodes directly inside this folder.</summary>
    public virtual ICollection<FileManagerNode> InverseParent { get; set; } = new List<FileManagerNode>();

    /// <summary>The folder this node is in, or null for the drive root.</summary>
    public virtual FileManagerNode? Parent { get; set; }
}
