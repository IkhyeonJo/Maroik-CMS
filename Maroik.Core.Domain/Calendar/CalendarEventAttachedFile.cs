using ErrorOr;
using Maroik.Core.Domain.Errors;
using Maroik.Core.Domain.Primitives;

namespace Maroik.Core.Domain.Calendar;

/// <summary>
/// Aggregate root representing a file attached to a <see cref="CalendarEvent"/>.
/// Persisted independently via its own <c>ICalendarEventAttachedFileRepository</c> (not hydrated
/// or cascaded through the <see cref="CalendarEvent"/> aggregate), which is why it is an aggregate
/// root rather than a child entity; it references its event by <see cref="CalendarEventId"/> only.
/// </summary>
public sealed class CalendarEventAttachedFile : AggregateRoot<long>
{
    /// <summary>ID of the parent calendar event.</summary>
    public long CalendarEventId { get; private set; }

    /// <summary>File size in bytes.</summary>
    public long Size { get; private set; }

    /// <summary>Original file name (e.g. "agenda.zip").</summary>
    public string Name { get; private set; }

    /// <summary>File extension including the dot (e.g. ".zip"). Null when there is no extension.</summary>
    public string? Extension { get; private set; }

    /// <summary>Server-side storage path of the file.</summary>
    public string Path { get; private set; }

    /// <summary>Sets every field; reached only through <see cref="Reconstitute"/> / <see cref="Create"/>.</summary>
    private CalendarEventAttachedFile(
        long id,
        long calendarEventId,
        long size,
        string name,
        string? extension,
        string path) : base(id)
    {
        CalendarEventId = calendarEventId;
        Size = size;
        Name = name;
        Extension = extension;
        Path = path;
    }

    /// <summary>
    /// Rebuilds a <see cref="CalendarEventAttachedFile"/> from trusted data from the repository layer.
    /// Bypasses validation — use only with data from the trusted repository layer.
    /// </summary>
    public static CalendarEventAttachedFile Reconstitute(
        long id, long calendarEventId, long size, string? name, string? extension, string? path)
        => new(id, calendarEventId, size, name ?? "", extension, path ?? "");

    /// <summary>Creates a new attached-file entity after validating required fields.</summary>
    public static ErrorOr<CalendarEventAttachedFile> Create(
        long calendarEventId,
        long size,
        string? name,
        string? extension,
        string? path)
    {
        if (string.IsNullOrWhiteSpace(name))
            return DomainError.Validation("CalendarEventAttachedFile.NameEmpty", "File name cannot be empty.");

        if (string.IsNullOrWhiteSpace(path))
            return DomainError.Validation("CalendarEventAttachedFile.PathEmpty", "File storage path cannot be empty.");

        if (size < 0)
            return DomainError.Validation("CalendarEventAttachedFile.InvalidSize", "File size cannot be negative.");

        // Name, Extension and Path are each persisted in a character varying(255) column: reject an
        // over-long value here (a clean validation error, before anything is uploaded) instead of a raw
        // "value too long" (SQLSTATE 22001) from the write.
        if (name.Length > ShortTextPolicy.MaxLength)
            return DomainError.Validation("CalendarEventAttachedFile.NameTooLong", "File name must be {0} characters or fewer.", ShortTextPolicy.MaxLength);

        if (extension?.Length > ShortTextPolicy.MaxLength)
            return DomainError.Validation("CalendarEventAttachedFile.ExtensionTooLong", "File extension must be {0} characters or fewer.", ShortTextPolicy.MaxLength);

        if (path.Length > ShortTextPolicy.MaxLength)
            return DomainError.Validation("CalendarEventAttachedFile.PathTooLong", "File storage path must be {0} characters or fewer.", ShortTextPolicy.MaxLength);

        return new CalendarEventAttachedFile(0, calendarEventId, size, name, extension, path);
    }
}
