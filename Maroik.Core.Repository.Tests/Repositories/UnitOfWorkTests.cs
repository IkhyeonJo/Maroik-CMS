using System.Data;
using Maroik.Core.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using OrmAccount = Maroik.Core.PostgreSQL.Models.Account;

namespace Maroik.Core.Repository.Tests.Repositories;

/// <summary>
/// Tests for <see cref="UnitOfWork"/> against a real, transaction-capable PostgreSQL provider
/// (Testcontainers). Covers both the null-guard paths (<c>if (_transaction == null) return;</c>)
/// and — now that a real transaction can actually begin, unlike under the removed EF Core InMemory
/// provider — the re-entrancy guard and the sequential-reuse path.
/// </summary>
public sealed class UnitOfWorkTests(DatabaseFixture database) : RepositoryTestBase(database)
{
    /// <summary>A unit of work over a fresh context.</summary>
    private UnitOfWork NewUnitOfWork() => new(NewDbContext());

    // -- Null-guard paths (no BeginAsync) -------------------------------------

    /// <summary>Verifies that <c>CommitAsync</c> does not throw when being was not called.</summary>
    [Fact]
    public async Task CommitAsync_DoesNotThrow_WhenBeginWasNotCalled()
    {
        await using var uow = NewUnitOfWork();

        Exception? ex = await Record.ExceptionAsync(() => uow.CommitAsync(TestContext.Current.CancellationToken));

        Assert.Null(ex);
    }

    /// <summary>Verifies that <c>RollbackAsync</c> does not throw when being was not called.</summary>
    [Fact]
    public async Task RollbackAsync_DoesNotThrow_WhenBeginWasNotCalled()
    {
        await using var uow = NewUnitOfWork();

        Exception? ex = await Record.ExceptionAsync(() => uow.RollbackAsync(TestContext.Current.CancellationToken));

        Assert.Null(ex);
    }

    /// <summary>Verifies that <c>DisposeAsync</c> does not throw when no transaction was started.</summary>
    [Fact]
    public async Task DisposeAsync_DoesNotThrow_WhenNoTransactionWasStarted()
    {
        var uow = NewUnitOfWork();

        Exception? ex = await Record.ExceptionAsync(async () => await uow.DisposeAsync());

        Assert.Null(ex);
    }

    // -- Real-transaction paths --------------------------------------------------

    /// <summary>A second <c>BeginAsync</c> while a transaction is still live throws instead of discarding the first.</summary>
    [Fact]
    public async Task BeginAsync_WhileTransactionActive_Throws()
    {
        await using var uow = NewUnitOfWork();
        await uow.BeginAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => uow.BeginAsync(TestContext.Current.CancellationToken));

        await uow.RollbackAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>After a commit the slot is cleared, so the same instance can run a second Begin→Commit cycle.</summary>
    [Fact]
    public async Task BeginAsync_AfterCommit_CanStartAnotherTransaction()
    {
        await using var uow = NewUnitOfWork();
        await uow.BeginAsync(TestContext.Current.CancellationToken);
        await uow.CommitAsync(TestContext.Current.CancellationToken);

        Exception? ex = await Record.ExceptionAsync(async () =>
        {
            await uow.BeginAsync(TestContext.Current.CancellationToken);
            await uow.CommitAsync(TestContext.Current.CancellationToken);
        });

        Assert.Null(ex);
    }

    /// <summary>Calling <c>RollbackAsync</c> after a commit is a harmless no-op (the catch-block pattern in services).</summary>
    [Fact]
    public async Task RollbackAsync_AfterCommit_DoesNotThrow()
    {
        await using var uow = NewUnitOfWork();
        await uow.BeginAsync(TestContext.Current.CancellationToken);
        await uow.CommitAsync(TestContext.Current.CancellationToken);

        Exception? ex = await Record.ExceptionAsync(() => uow.RollbackAsync(TestContext.Current.CancellationToken));

        Assert.Null(ex);
    }

    /// <summary>
    /// Passing an explicit isolation level actually opens the transaction at that level — this is
    /// what lets a Service (e.g. BoardService.GetBoardPageAsync) request a stronger guarantee than
    /// the provider default without any repository opening a transaction of its own.
    /// </summary>
    [Fact]
    public async Task BeginAsync_WithIsolationLevel_OpensTransactionAtThatLevel()
    {
        await using var context = NewDbContext();
        var uow = new UnitOfWork(context);

        await uow.BeginAsync(TestContext.Current.CancellationToken, IsolationLevel.RepeatableRead);

        Assert.Equal(IsolationLevel.RepeatableRead, context.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel);

        await uow.RollbackAsync(TestContext.Current.CancellationToken);
    }

    // -- Failed commit ------------------------------------------------------------

    /// <summary>
    /// When the flush inside <c>CommitAsync</c> fails (here a primary-key violation), the entities the
    /// aborted unit of work left tracked must be discarded. <c>CommitAsync</c> clears its transaction
    /// slot in <c>finally</c>, so the <c>RollbackAsync</c> a catch block calls afterward is a no-op
    /// and would never reach <c>ChangeTracker.Clear()</c> — leaving the failed <c>Added</c> entity on
    /// the request-scoped context, where a retry's next <c>SaveChanges</c> re-flushes it and fails
    /// the same way (the CalendarShared_pk retry loops in CalendarService).
    /// </summary>
    [Fact]
    public async Task CommitAsync_WhenSaveChangesFails_ClearsTheChangeTracker()
    {
        var ct = TestContext.Current.CancellationToken;
        string email = UniqueEmail("dup");
        await EnsureAccountsAsync(email);

        await using var context = NewDbContext();
        var uow = new UnitOfWork(context);
        await uow.BeginAsync(ct);
        context.Accounts.Add(new OrmAccount
        {
            Email = email, // same primary key as the row just seeded
            Nickname = Unique("Dup-" + Guid.NewGuid().ToString("N")[..8]),
            HashedPassword = "$2a$13$placeholder",
            AvatarImagePath = "/upload/Management/Profile/default-avatar.jpg",
            Role = "User",
            TimeZoneIanaId = "UTC",
            EmailConfirmed = true,
            AgreedServiceTerms = true,
            SecurityStamp = "stamp",
            Created = DateTime.UtcNow,
            Updated = DateTime.UtcNow,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => uow.CommitAsync(ct));
        // The catch-block pattern services use: must stay a harmless no-op.
        await uow.RollbackAsync(ct);

        Assert.Empty(context.ChangeTracker.Entries());
    }

    /// <summary>After a failed commit the same instance can run a fresh Beginning→Commit cycle without re-flushing the failed entity.</summary>
    [Fact]
    public async Task CommitAsync_AfterFailedCommit_NextCycleDoesNotReflushTheFailedEntity()
    {
        var ct = TestContext.Current.CancellationToken;
        string email = UniqueEmail("dup-retry");
        await EnsureAccountsAsync(email);

        await using var context = NewDbContext();
        var uow = new UnitOfWork(context);
        await uow.BeginAsync(ct);
        context.Accounts.Add(new OrmAccount
        {
            Email = email,
            Nickname = Unique("DupRetry-" + Guid.NewGuid().ToString("N")[..8]),
            HashedPassword = "$2a$13$placeholder",
            AvatarImagePath = "/upload/Management/Profile/default-avatar.jpg",
            Role = "User",
            TimeZoneIanaId = "UTC",
            EmailConfirmed = true,
            AgreedServiceTerms = true,
            SecurityStamp = "stamp",
            Created = DateTime.UtcNow,
            Updated = DateTime.UtcNow,
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => uow.CommitAsync(ct));
        await uow.RollbackAsync(ct);

        Exception? ex = await Record.ExceptionAsync(async () =>
        {
            await uow.BeginAsync(ct);
            await uow.CommitAsync(ct);
        });

        Assert.Null(ex);
    }

    // -- A transaction the database has already ended ----------------------------------

    /// <summary>Begins a unit of work and then kills its database session, so the transaction can no longer be rolled back normally.</summary>
    private async Task<(UnitOfWork Uow, PostgreSQL.Data.ApplicationDbContext Context)> BeginAndKillTheSessionAsync()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var context = NewDbContext();
        var uow = new UnitOfWork(context);
        await uow.BeginAsync(ct);
        int pid = await context.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync(ct);
        await Context.Database.ExecuteSqlAsync($"SELECT pg_terminate_backend({pid})", ct);
        return (uow, context);
    }

    /// <summary>Rolling back after the database dropped the session does not throw (there is nothing left to roll back) and still clears the tracker.</summary>
    [Fact]
    public async Task RollbackAsync_DoesNotThrow_WhenTheDatabaseAlreadyEndedTheTransaction()
    {
        var (uow, context) = await BeginAndKillTheSessionAsync();
        context.Accounts.Add(new OrmAccount { Email = UniqueEmail("tracked"), Nickname = Unique("Tracked"), HashedPassword = "x", AvatarImagePath = "/a", Role = "User", TimeZoneIanaId = "UTC", SecurityStamp = "s" });

        Exception? ex = await Record.ExceptionAsync(() => uow.RollbackAsync(TestContext.Current.CancellationToken));

        Assert.Null(ex);
        Assert.Empty(context.ChangeTracker.Entries()); // the aborted unit of work leaves nothing tracked
        await uow.DisposeAsync();
    }

    /// <summary>Disposing a unit of work whose transaction the database already ended does not throw either.</summary>
    [Fact]
    public async Task DisposeAsync_DoesNotThrow_WhenTheDatabaseAlreadyEndedTheTransaction()
    {
        var (uow, context) = await BeginAndKillTheSessionAsync();

        Exception? ex = await Record.ExceptionAsync(async () => await uow.DisposeAsync());

        Assert.Null(ex);
        Assert.Empty(context.ChangeTracker.Entries());
    }
}
