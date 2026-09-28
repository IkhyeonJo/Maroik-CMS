using Maroik.Core.Domain.Finance;
namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Repository abstraction over <see cref="FixedIncome"/> (recurring income) persistence.
/// </summary>
public interface IFixedIncomeRepository : IGenericRepository<FixedIncome>
{
    /// <summary>Returns all fixed income records for the given account.</summary>
    Task<List<FixedIncome>> GetByAccountEmailAsync(string accountEmail, CancellationToken ct = default);

    /// <summary>Returns fixed income records for the given account whose fields contain <paramref name="search"/>, filtered in the database.</summary>
    Task<List<FixedIncome>> SearchByAccountEmailAsync(string accountEmail, string search, CancellationToken ct = default);

    /// <summary>Returns the fixed income record identified by account email and ID, or null if not found.</summary>
    Task<FixedIncome?> FindByEmailAndIdAsync(string accountEmail, long id, CancellationToken ct = default);

    /// <summary>Deletes the fixed income record with the given ID.</summary>
    Task DeleteByIdAsync(long id, CancellationToken ct = default);
}
