using Maroik.Core.Domain.Menu;
namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Repository abstraction over <see cref="SubCategory"/> (navigation dropdown item) persistence.
/// </summary>
public interface ISubCategoryRepository : IGenericRepository<SubCategory>
{
    /// <summary>Returns all sub-category (dropdown menu item) rows.</summary>
    Task<List<SubCategory>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Returns the sub-category with the given ID, or <see langword="null"/> if none exists.</summary>
    Task<SubCategory?> FindByIdAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Returns the sub-category with the given ID, row-locked (<c>FOR UPDATE</c>) for the rest of
    /// the caller's transaction, or <see langword="null"/> if it doesn't exist.
    /// </summary>
    Task<SubCategory?> FindByIdForUpdateAsync(long id, CancellationToken ct = default);

    /// <summary>Deletes the sub-category with the given ID.</summary>
    Task DeleteByIdAsync(long id, CancellationToken ct = default);
}
