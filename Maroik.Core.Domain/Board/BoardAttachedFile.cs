using ErrorOr;
using Maroik.Core.Domain.Localization;
using Maroik.Core.Domain.Primitives;

namespace Maroik.Core.Domain.Board;

/// <summary>
/// Aggregate root representing a file attached to a <see cref="Board"/> post.
/// Persisted independently via its own <c>IBoardAttachedFileRepository</c> (not hydrated or
/// cascaded through the <see cref="Board"/> aggregate), which is why it is an aggregate root
/// rather than a child entity; it references its post by <see cref="BoardId"/> only.
/// </summary>
public sealed class BoardAttachedFile : AggregateRoot<long>
{
    /// <summary>ID of the parent board post.</summary>
    public long BoardId { get; private set; }

    /// <summary>File size in bytes.</summary>
    public long Size { get; private set; }

    /// <summary>Original file name (e.g. "report.pdf").</summary>
    public string Name { get; private set; }

    /// <summary>File extension including the dot (e.g. ".pdf"). Null when there is no extension.</summary>
    public string? Extension { get; private set; }

    /// <summary>Server-side storage path where the file is saved.</summary>
    public string Path { get; private set; }

    private BoardAttachedFile(
        long id,
        long boardId,
        long size,
        string name,
        string? extension,
        string path) : base(id)
    {
        BoardId = boardId;
        Size = size;
        Name = name;
        Extension = extension;
        Path = path;
    }

    /// <summary>
    /// Rebuilds a <see cref="BoardAttachedFile"/> from trusted data from the repository layer.
    /// Bypasses validation — use only with data from the trusted repository layer.
    /// </summary>
    public static BoardAttachedFile Reconstitute(
        long id, long boardId, long size, string? name, string? extension, string? path)
        => new(id, boardId, size, name ?? "", extension, path ?? "");

    /// <summary>Creates a new attached-file entity after validating required fields.</summary>
    public static ErrorOr<BoardAttachedFile> Create(
        long boardId,
        long size,
        string? name,
        string? extension,
        string? path)
    {
        if (string.IsNullOrWhiteSpace(name))
            return LocalizableError.Validation("BoardAttachedFile.NameEmpty", "File name cannot be empty.");

        if (string.IsNullOrWhiteSpace(path))
            return LocalizableError.Validation("BoardAttachedFile.PathEmpty", "File storage path cannot be empty.");

        if (size < 0)
            return LocalizableError.Validation("BoardAttachedFile.InvalidSize", "File size cannot be negative.");

        // Name, Extension and Path are each persisted in a character varying(255) column: reject an
        // over-long value here (a clean validation error, before anything is uploaded) instead of a raw
        // "value too long" (SQLSTATE 22001) from the write.
        if (name.Length > ShortTextPolicy.MaxLength)
            return LocalizableError.Validation("BoardAttachedFile.NameTooLong", "File name must be {0} characters or fewer.", ShortTextPolicy.MaxLength);

        if (extension?.Length > ShortTextPolicy.MaxLength)
            return LocalizableError.Validation("BoardAttachedFile.ExtensionTooLong", "File extension must be {0} characters or fewer.", ShortTextPolicy.MaxLength);

        if (path.Length > ShortTextPolicy.MaxLength)
            return LocalizableError.Validation("BoardAttachedFile.PathTooLong", "File storage path must be {0} characters or fewer.", ShortTextPolicy.MaxLength);

        return new BoardAttachedFile(0, boardId, size, name, extension, path);
    }
}
