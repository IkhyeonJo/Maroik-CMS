using Maroik.Core.Domain.Finance;
namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Repository abstraction over <see cref="Expenditure"/> persistence.
/// </summary>
public interface IExpenditureRepository : IGenericRepository<Expenditure>
{
    /// <summary>Returns all expenditure records for the given account.</summary>
    Task<List<Expenditure>> GetByAccountEmailAsync(string accountEmail, CancellationToken ct = default);

    /// <summary>
    /// Returns expenditure records for the given account whose MainClass, SubClass, Content,
    /// PaymentMethod, MyDepositAsset, Note, Amount, Created, Updated, or currency contains
    /// <paramref name="search"/> — filtered in the database instead of loading every record.
    /// </summary>
    Task<List<Expenditure>> SearchByAccountEmailAsync(string accountEmail, string search, CancellationToken ct = default);

    /// <summary>Returns the expenditure record identified by account email and ID, or null if not found.</summary>
    Task<Expenditure?> FindByEmailAndIdAsync(string accountEmail, long id, CancellationToken ct = default);

    /// <summary>
    /// Same as <see cref="FindByEmailAndIdAsync"/> but takes a row lock (<c>SELECT … FOR UPDATE</c>)
    /// on the matched row, held until the ambient <see cref="IUnitOfWork"/> transaction ends. Used by
    /// update/delete flows so two concurrent edits of the same record cannot both read a stale
    /// amount and drift the linked asset balance. Must be called inside an open transaction.
    /// </summary>
    Task<Expenditure?> FindByEmailAndIdForUpdateAsync(string accountEmail, long id, CancellationToken ct = default);

    /// <summary>Returns expenditure records for the given account whose date falls within the given range.</summary>
    Task<List<Expenditure>> GetByAccountEmailAndDateRangeAsync(string accountEmail, DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>Returns the earliest-created expenditure record for the given account, or null if none exists.</summary>
    Task<Expenditure?> GetFirstByAccountEmailOrderedByCreatedAsync(string accountEmail, CancellationToken ct = default);

    /// <summary>Deletes the expenditure record with the given ID.</summary>
    Task DeleteByIdAsync(long id, CancellationToken ct = default);
}
