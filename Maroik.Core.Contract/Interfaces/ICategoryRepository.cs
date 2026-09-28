using Maroik.Core.Domain.Menu;
namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Repository abstraction over <see cref="Category"/> (navigation menu) persistence.
/// </summary>
public interface ICategoryRepository : IGenericRepository<Category>
{
    /// <summary>Returns all top-level navigation categories.</summary>
    Task<List<Category>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Returns the category with the given ID, or <see langword="null"/> if none exists.</summary>
    Task<Category?> FindByIdAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Returns the category with the given ID, row-locked (<c>FOR UPDATE</c>) for the rest of the
    /// caller's transaction, or <see langword="null"/> if it doesn't exist.
    /// </summary>
    Task<Category?> FindByIdForUpdateAsync(long id, CancellationToken ct = default);

    /// <summary>Deletes the category with the given ID.</summary>
    Task DeleteByIdAsync(long id, CancellationToken ct = default);
}
