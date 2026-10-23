using ErrorOr;
using Maroik.Core.Domain.Errors;
using Maroik.Core.Domain.Extensions;
using Maroik.Core.Domain.Primitives;
using Maroik.Core.Domain.ValueObjects;

namespace Maroik.Core.Domain.Kanbans;

/// <summary>
/// Aggregate root for one card of a user's personal Kanban board. A card belongs to one account and lives in the lane it was created
/// in for its whole life (K11): nothing here can change <see cref="Lane"/>, so moving work to another lane means deleting the card
/// and creating a new one there. Its title and description follow the post limits (<see cref="TitledContentPolicy"/>, K8); the
/// description is plain text and the due date a date without a time (K10).
/// </summary>
public sealed class KanbanCard : AggregateRoot<long>
{
    /// <summary>Email of the owning account.</summary>
    public Email AccountEmail { get; }

    /// <summary>The lane (a <see cref="KanbanLanes.Ordered"/> value), fixed at creation.</summary>
    public string Lane { get; }

    /// <summary>The card's title: 1 to <see cref="TitledContentPolicy.MaxTitleLength"/> characters, not blank.</summary>
    public string Title { get; private set; }

    /// <summary>Optional plain-text description (line breaks kept); null when none was given.</summary>
    public string? Description { get; private set; }

    /// <summary>Optional due date (a date only, no time).</summary>
    public DateOnly? DueDate { get; private set; }

    /// <summary>UTC timestamp when the card was created; with <see cref="Entity{TId}.Id"/> it orders the lane (K12).</summary>
    public DateTime Created { get; }

    /// <summary>UTC timestamp of the most recent update.</summary>
    public DateTime Updated { get; private set; }

    /// <summary>Assigns every field verbatim (used by both the factory and reconstitution).</summary>
    private KanbanCard(long id, Email accountEmail, string lane, string title, string? description, DateOnly? dueDate,
        DateTime created, DateTime updated) : base(id)
    {
        AccountEmail = accountEmail;
        Lane = lane;
        Title = title;
        Description = description;
        DueDate = dueDate;
        Created = created;
        Updated = updated;
    }

    /// <summary>
    /// Rebuilds a <see cref="KanbanCard"/> from trusted raw values from the repository layer.
    /// Bypasses validation — use only with data from the trusted repository layer.
    /// </summary>
    public static KanbanCard Reconstitute(long id, string accountEmail, string lane, string title, string? description,
        DateOnly? dueDate, DateTime created, DateTime updated) =>
        new(id, Email.FromTrustedSource(accountEmail), lane, title, description, dueDate, created, updated);

    /// <summary>Creates a new card in <paramref name="lane"/>, stamped with <paramref name="utcNow"/>.</summary>
    public static ErrorOr<KanbanCard> Create(string? accountEmailValue, string? lane, string? title, string? description,
        DateOnly? dueDate, DateTime utcNow)
    {
        if (!KanbanLanes.IsKnown(lane))
            return DomainError.Validation("KanbanCard.LaneInvalid", "'{0}' is not a Kanban lane.", lane ?? "");

        var contentResult = ValidateContent(title, description);
        if (contentResult.IsError) return contentResult.Errors;

        var emailResult = Email.Create(accountEmailValue);
        if (emailResult.IsError) return emailResult.Errors;

        return new KanbanCard(0, emailResult.Value, lane, contentResult.Value, NormalizeDescription(description), dueDate, utcNow, utcNow);
    }

    /// <summary>Replaces the title, description and due date. The lane stays as it is (K11).</summary>
    public ErrorOr<Success> Update(string? title, string? description, DateOnly? dueDate, DateTime utcNow)
    {
        var contentResult = ValidateContent(title, description);
        if (contentResult.IsError) return contentResult.Errors;

        Title = contentResult.Value;
        Description = NormalizeDescription(description);
        DueDate = dueDate;
        Updated = utcNow;
        return Result.Success;
    }

    /// <summary>
    /// The due-date state the board shows (K10): <see cref="KanbanDueState.Overdue"/> once the due date is before today,
    /// <see cref="KanbanDueState.Upcoming"/> while it is today or later. "Today" is the owner's local date in
    /// <paramref name="timeZoneIanaId"/> at <paramref name="utcNow"/> — decided here, not by the browser's clock.
    /// </summary>
    public KanbanDueState DueStateAt(DateTime utcNow, string timeZoneIanaId)
    {
        if (DueDate is not { } dueDate)
            return KanbanDueState.None;

        DateOnly today = DateOnly.FromDateTime(utcNow.ConvertTimeByTimeZoneIanaId(timeZoneIanaId));
        return dueDate < today ? KanbanDueState.Overdue : KanbanDueState.Upcoming;
    }

    /// <summary>
    /// The title and description rules shared by <see cref="Create"/> and <see cref="Update"/>, returning the title once it is
    /// confirmed non-blank so callers keep the compiler's non-null flow.
    /// </summary>
    private static ErrorOr<string> ValidateContent(string? title, string? description)
    {
        if (string.IsNullOrWhiteSpace(title))
            return DomainError.Validation("KanbanCard.TitleEmpty", "Card title cannot be empty.");

        if (title.Length > TitledContentPolicy.MaxTitleLength)
            return DomainError.Validation("KanbanCard.TitleTooLong", "Card title must be {0} characters or fewer.", TitledContentPolicy.MaxTitleLength);

        if (description?.Length > TitledContentPolicy.MaxBodyLength)
            return DomainError.Validation("KanbanCard.DescriptionTooLong", "Card description must be {0} characters or fewer.", TitledContentPolicy.MaxBodyLength);

        return title;
    }

    /// <summary>A blank description is stored as none.</summary>
    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description;
}
