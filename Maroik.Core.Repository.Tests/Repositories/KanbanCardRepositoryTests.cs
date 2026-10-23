using Maroik.Core.Domain.Kanbans;
using Maroik.Core.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OrmKanbanCard = Maroik.Core.PostgreSQL.Models.KanbanCard;

namespace Maroik.Core.Repository.Tests.Repositories;

/// <summary>
/// Tests for <see cref="KanbanCardRepository"/> against a real PostgreSQL database (Testcontainers) preloaded with the
/// <c>Init.sql</c> schema and seed data. <c>KanbanCard</c> has the FK <c>KanbanCard_fk_0</c> to <c>Account.Email</c> and the checks
/// <c>KanbanCard_Lane_check</c> (the four lanes) and <c>KanbanCard_Title_check</c> (not blank). Each test scopes itself to its own
/// unique account e-mails, and every lookup is owner-scoped: another account's card id behaves as "not found" (K2).
/// </summary>
public sealed class KanbanCardRepositoryTests(DatabaseFixture database) : RepositoryTestBase(database)
{
    /// <summary>The repository under test, over this test's database.</summary>
    private KanbanCardRepository Sut => new(Context);

    /// <summary>The fixed creation time of the cards these tests create.</summary>
    private static readonly DateTime Now = new(2026, 5, 24, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Creates a valid card for <paramref name="email"/> through the repository and returns its stored id.</summary>
    private async Task<long> CreateAsync(string email, string title, string lane = "ToDo", string? description = null, DateOnly? dueDate = null)
    {
        await EnsureAccountsAsync(email);
        await Sut.CreateAsync(KanbanCard.Create(email, lane, title, description, dueDate, Now).Value, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();
        return await Context.KanbanCards.AsNoTracking().Where(c => c.AccountEmail == email && c.Title == title).Select(c => c.Id).SingleAsync(TestContext.Current.CancellationToken);
    }

    // -- CreateAsync / GetByAccountEmailAsync ---------------------------------------

    /// <summary>A created card reads back with every field, including its lane, description and due date.</summary>
    [Fact]
    public async Task CreateAsync_ThenGetByAccountEmailAsync_ReadsBackEveryField()
    {
        string email = UniqueEmail();
        long id = await CreateAsync(email, "Write the report", "InProgress", "Line 1\nLine 2", new DateOnly(2026, 5, 28));

        KanbanCard card = Assert.Single(await Sut.GetByAccountEmailAsync(email, TestContext.Current.CancellationToken));

        Assert.Equal(id, card.Id);
        Assert.Equal(email, card.AccountEmail.Value);
        Assert.Equal("InProgress", card.Lane);
        Assert.Equal("Write the report", card.Title);
        Assert.Equal("Line 1\nLine 2", card.Description);
        Assert.Equal(new DateOnly(2026, 5, 28), card.DueDate);
        Assert.Equal(Now, card.Created);
        Assert.Equal(Now, card.Updated);
    }

    /// <summary>A card without a description or due date stores both as NULL and reads them back as none.</summary>
    [Fact]
    public async Task CreateAsync_StoresNoDescriptionAndNoDueDate_AsNull()
    {
        string email = UniqueEmail();
        long id = await CreateAsync(email, "Plain card");

        OrmKanbanCard row = await Context.KanbanCards.AsNoTracking().SingleAsync(c => c.Id == id, TestContext.Current.CancellationToken);
        KanbanCard card = Assert.Single(await Sut.GetByAccountEmailAsync(email, TestContext.Current.CancellationToken));

        Assert.Null(row.Description);
        Assert.Null(row.DueDate);
        Assert.Null(card.Description);
        Assert.Null(card.DueDate);
    }

    /// <summary>Each account sees only its own cards.</summary>
    [Fact]
    public async Task GetByAccountEmailAsync_ReturnsOnlyTheAccountsCards()
    {
        string alice = UniqueEmail("alice");
        string bob = UniqueEmail("bob");
        await CreateAsync(alice, "Alice 1");
        await CreateAsync(alice, "Alice 2", "Done");
        await CreateAsync(bob, "Bob 1");

        List<KanbanCard> cards = await Sut.GetByAccountEmailAsync(alice, TestContext.Current.CancellationToken);

        Assert.Equal(["Alice 1", "Alice 2"], cards.Select(c => c.Title).Order(StringComparer.Ordinal));
    }

    // -- FindByEmailAndIdAsync -------------------------------------------------------

    /// <summary>The owner finds the card by id; another account asking for the same id gets nothing.</summary>
    [Fact]
    public async Task FindByEmailAndIdAsync_FindsOnlyTheOwnersCard()
    {
        string owner = UniqueEmail("owner");
        string other = UniqueEmail("other");
        await EnsureAccountsAsync(other);
        long id = await CreateAsync(owner, "Mine");

        KanbanCard? found = await Sut.FindByEmailAndIdAsync(owner, id, TestContext.Current.CancellationToken);
        KanbanCard? notFound = await Sut.FindByEmailAndIdAsync(other, id, TestContext.Current.CancellationToken);

        Assert.Equal("Mine", found?.Title);
        Assert.Null(notFound);
    }

    // -- UpdateEntityAsync ------------------------------------------------------------

    /// <summary>An update persists the new title, description and due date and the Updated stamp, and keeps the lane and Created.</summary>
    [Fact]
    public async Task UpdateEntityAsync_PersistsTheNewContent_AndKeepsTheLane()
    {
        string email = UniqueEmail();
        long id = await CreateAsync(email, "Old title", "Backlog", "Old body", new DateOnly(2026, 5, 28));
        KanbanCard card = (await Sut.FindByEmailAndIdAsync(email, id, TestContext.Current.CancellationToken))!;
        DateTime later = Now.AddHours(2);
        Assert.False(card.Update("New title", null, new DateOnly(2026, 6, 1), later).IsError);

        await Sut.UpdateEntityAsync(card, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        OrmKanbanCard row = await Context.KanbanCards.AsNoTracking().SingleAsync(c => c.Id == id, TestContext.Current.CancellationToken);
        Assert.Equal("New title", row.Title);
        Assert.Null(row.Description);
        Assert.Equal(new DateOnly(2026, 6, 1), row.DueDate);
        Assert.Equal("Backlog", row.Lane);
        Assert.Equal(Now, row.Created);
        Assert.Equal(later, row.Updated);
    }

    // -- DeleteByEmailAndIdAsync ------------------------------------------------------

    /// <summary>The owner's delete removes the card.</summary>
    [Fact]
    public async Task DeleteByEmailAndIdAsync_RemovesTheOwnersCard()
    {
        string email = UniqueEmail();
        long id = await CreateAsync(email, "Doomed");

        await Sut.DeleteByEmailAndIdAsync(email, id, TestContext.Current.CancellationToken);

        Assert.False(await Context.KanbanCards.AsNoTracking().AnyAsync(c => c.Id == id, TestContext.Current.CancellationToken));
    }

    /// <summary>A delete naming another account's card id leaves that card in place.</summary>
    [Fact]
    public async Task DeleteByEmailAndIdAsync_LeavesAnotherAccountsCard()
    {
        string owner = UniqueEmail("owner");
        string other = UniqueEmail("other");
        await EnsureAccountsAsync(other);
        long id = await CreateAsync(owner, "Keep me");

        await Sut.DeleteByEmailAndIdAsync(other, id, TestContext.Current.CancellationToken);

        Assert.True(await Context.KanbanCards.AsNoTracking().AnyAsync(c => c.Id == id, TestContext.Current.CancellationToken));
    }

    // -- Schema checks ----------------------------------------------------------------

    /// <summary>An unsaved card row for <paramref name="email"/>.</summary>
    private static OrmKanbanCard Row(string email, string lane, string title) => new()
    {
        AccountEmail = email, Lane = lane, Title = title, Created = Now, Updated = Now,
    };

    /// <summary>PostgreSQL enforces <c>KanbanCard_Lane_check</c>: a lane outside the four is refused.</summary>
    [Fact]
    public async Task TheDatabase_RefusesAnUnknownLane()
    {
        string email = UniqueEmail();
        await EnsureAccountsAsync(email);
        await Context.KanbanCards.AddAsync(Row(email, "Archive", "Title"), TestContext.Current.CancellationToken);

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => Context.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Equal("KanbanCard_Lane_check", Assert.IsType<PostgresException>(error.InnerException).ConstraintName);
    }

    /// <summary>PostgreSQL enforces <c>KanbanCard_Title_check</c>: a blank title is refused.</summary>
    [Fact]
    public async Task TheDatabase_RefusesABlankTitle()
    {
        string email = UniqueEmail();
        await EnsureAccountsAsync(email);
        await Context.KanbanCards.AddAsync(Row(email, "Done", "   "), TestContext.Current.CancellationToken);

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => Context.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Equal("KanbanCard_Title_check", Assert.IsType<PostgresException>(error.InnerException).ConstraintName);
    }

    /// <summary>PostgreSQL enforces <c>KanbanCard_fk_0</c>: a card needs an existing account.</summary>
    [Fact]
    public async Task TheDatabase_RefusesACardOfAnUnknownAccount()
    {
        await Context.KanbanCards.AddAsync(Row(UniqueEmail("nobody"), "Done", "Title"), TestContext.Current.CancellationToken);

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => Context.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Equal("KanbanCard_fk_0", Assert.IsType<PostgresException>(error.InnerException).ConstraintName);
    }
}
