namespace Maroik.Core.Domain.Primitives;

/// <summary>Base class for all aggregate roots.</summary>
/// <typeparam name="TId">Type of the aggregate's primary key.</typeparam>
public abstract class AggregateRoot<TId>(TId id) : Entity<TId>(id)
    where TId : notnull;
