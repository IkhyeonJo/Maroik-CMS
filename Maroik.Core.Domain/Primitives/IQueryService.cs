namespace Maroik.Core.Domain.Primitives;

/// <summary>
/// Marker interface for pure query services that perform no state mutations.
/// Implementing this interface signals that all operations are side-effect-free reads,
/// enabling consumers to safely call them without triggering write transactions.
/// </summary>
public interface IQueryService;
