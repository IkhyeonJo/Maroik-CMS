using Maroik.Core.Domain.Calendar;
using Maroik.Core.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using OrmCalendarEventAttachedFile = Maroik.Core.PostgreSQL.Models.CalendarEventAttachedFile;

namespace Maroik.Core.Repository.Tests.Repositories;

/// <summary>
/// Tests for <see cref="CalendarEventAttachedFileRepository"/> against a real PostgreSQL database
/// (Testcontainers) preloaded with the <c>Init.sql</c> schema and seed data.
/// <c>CalendarEventAttachedFile</c> carries the FK <c>CalendarEventAttachedFile_fk_0</c> to
/// <c>CalendarEvent.ID</c> (unenforced by InMemory), so every test creates a real parent event.
/// </summary>
public sealed class CalendarEventAttachedFileRepositoryTests(DatabaseFixture database) : RepositoryTestBase(database)
{
    private CalendarEventAttachedFileRepository Sut => new(Context);

    /// <summary>An unsaved attachment row for event <paramref name="calendarEventId"/>.</summary>
    private static OrmCalendarEventAttachedFile MakeFile(long calendarEventId, string name = "doc.pdf") => new()
    {
        CalendarEventId = calendarEventId,
        Name = name,
        Extension = ".pdf",
        Path = $"/upload/calendar/{name}",
        Size = 1024
    };

    /// <summary>Inserts <paramref name="files"/>, saves, and clears the change tracker so later reads hit the database.</summary>
    private async Task SeedAsync(params OrmCalendarEventAttachedFile[] files)
    {
        await Context.CalendarEventAttachedFiles.AddRangeAsync(files);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
    }

    // -- FindByCalendarEventIdAsync -----------------------------------------

    /// <summary>Verifies that <c>FindByCalendarEventIdAsync</c> returns the file when found.</summary>
    [Fact]
    public async Task FindByCalendarEventIdAsync_ReturnsFile_WhenFound()
    {
        long eventId = await InsertCalendarEventAsync();
        await SeedAsync(MakeFile(eventId, "slide.pdf"));

        CalendarEventAttachedFile? result = await Sut.FindByCalendarEventIdAsync(eventId, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(eventId, result.CalendarEventId);
        Assert.Equal("slide.pdf", result.Name);
    }

    /// <summary>Verifies that <c>FindByCalendarEventIdAsync</c> returns null when not found.</summary>
    [Fact]
    public async Task FindByCalendarEventIdAsync_ReturnsNull_WhenNotFound()
    {
        CalendarEventAttachedFile? result = await Sut.FindByCalendarEventIdAsync(long.MaxValue, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    // -- CreateAsync -----------------------------------------

    /// <summary>Verifies that <c>CreateAsync</c> persists an attached-file row.</summary>
    [Fact]
    public async Task CreateAsync_PersistsFile()
    {
        long eventId = await InsertCalendarEventAsync();

        var file = CalendarEventAttachedFile.Reconstitute(
            id: 0, calendarEventId: eventId, size: 2048, name: "new.pdf",
            extension: ".pdf", path: "/upload/calendar/new.pdf");

        await Sut.CreateAsync(file, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        OrmCalendarEventAttachedFile? saved = await Context.CalendarEventAttachedFiles
            .FirstOrDefaultAsync(f => f.CalendarEventId == eventId, TestContext.Current.CancellationToken);
        Assert.NotNull(saved);
        Assert.Equal("new.pdf", saved.Name);
    }

    /// <summary>PostgreSQL enforces <c>CalendarEventAttachedFile_fk_0</c> — a file for an unknown event throws (InMemory did not).</summary>
    [Fact]
    public async Task CreateAsync_Throws_WhenParentEventDoesNotExist()
    {
        var file = CalendarEventAttachedFile.Reconstitute(
            id: 0, calendarEventId: long.MaxValue, size: 1, name: "orphan.pdf",
            extension: ".pdf", path: "/x");

        await Assert.ThrowsAsync<DbUpdateException>(
            () => Sut.CreateAsync(file, TestContext.Current.CancellationToken));
    }

    // -- UpdateEntityAsync ---------------------------------

    /// <summary>Verifies that <c>UpdateEntityAsync</c> persists changes to an existing file.</summary>
    [Fact]
    public async Task UpdateEntityAsync_UpdatesExistingFile()
    {
        long eventId = await InsertCalendarEventAsync();
        var seed = MakeFile(eventId, "old.pdf");
        await SeedAsync(seed);

        var file = CalendarEventAttachedFile.Reconstitute(
            id: seed.Id, calendarEventId: eventId, size: 9999, name: "updated.pdf",
            extension: ".pdf", path: "/upload/calendar/updated.pdf");

        await Sut.UpdateEntityAsync(file, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        OrmCalendarEventAttachedFile? updated = await Context.CalendarEventAttachedFiles
            .FirstOrDefaultAsync(f => f.Id == seed.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal("updated.pdf", updated.Name);
        Assert.Equal(9999, updated.Size);
    }

    // -- DeleteByCalendarEventIdAsync ---------------------------------------

    /// <summary>Verifies that <c>DeleteByCalendarEventIdAsync</c> removes only the target event's files.</summary>
    [Fact]
    public async Task DeleteByCalendarEventIdAsync_RemovesFilesByEventId()
    {
        long removeEvent = await InsertCalendarEventAsync();
        long keepEvent = await InsertCalendarEventAsync();
        await SeedAsync(MakeFile(removeEvent, "x.pdf"), MakeFile(removeEvent, "y.pdf"), MakeFile(keepEvent, "z.pdf"));

        await Sut.DeleteByCalendarEventIdAsync(removeEvent, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        Assert.False(await Context.CalendarEventAttachedFiles.AnyAsync(f => f.CalendarEventId == removeEvent, TestContext.Current.CancellationToken));
        Assert.True(await Context.CalendarEventAttachedFiles.AnyAsync(f => f.CalendarEventId == keepEvent, TestContext.Current.CancellationToken));
    }
}
