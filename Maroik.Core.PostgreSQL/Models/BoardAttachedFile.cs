using System;
using System.Collections.Generic;

namespace Maroik.Core.PostgreSQL.Models;

/// <summary>
/// BoardAttachedFile
/// </summary>
public partial class BoardAttachedFile
{
    /// <summary>
    /// PK
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Parent Board Id
    /// </summary>
    public long BoardId { get; set; }

    /// <summary>
    /// Size (Byte)
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Name
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Extension
    /// </summary>
    public string? Extension { get; set; }

    /// <summary>
    /// Path
    /// </summary>
    public string Path { get; set; } = null!;

    /// <summary>The post this file is attached to.</summary>
    public virtual Board Board { get; set; } = null!;
}
