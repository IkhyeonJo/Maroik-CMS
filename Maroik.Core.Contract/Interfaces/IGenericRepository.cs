namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Base repository abstraction providing the create and update persistence operations
/// shared by all entity-specific repositories.
/// </summary>
public interface IGenericRepository<in TDomain>
{
    /// <summary>Inserts a new entity.</summary>
    Task CreateAsync(TDomain domain, CancellationToken ct = default);

    /// <summary>Persists changes to an existing entity.</summary>
    Task UpdateEntityAsync(TDomain domain, CancellationToken ct = default);
}
