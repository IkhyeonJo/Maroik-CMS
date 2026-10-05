using System.Data;

namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Unit-of-Work abstraction that wraps a database transaction.
/// Implement the using-await pattern: begin → execute repository calls → commit (or rollback on error).
/// Inherits <see cref="IAsyncDisposable"/> so the transaction is automatically rolled back
/// if <see cref="CommitAsync"/> was never called.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    /// <summary>
    /// Opens a new database transaction, at <paramref name="isolationLevel"/> when given (otherwise
    /// the provider default). A Service method needing a stronger guarantee than the default — e.g.
    /// pinning a multi-query read to one snapshot — asks for it here; a repository must never open a
    /// transaction of its own to get one.
    /// </summary>
    Task BeginAsync(IsolationLevel? isolationLevel = null, CancellationToken ct = default);

    /// <summary>Flushes any pending changes and commits the current transaction.</summary>
    Task CommitAsync(CancellationToken ct = default);

    /// <summary>Rolls back all changes made within the current transaction.</summary>
    Task RollbackAsync(CancellationToken ct = default);
}
