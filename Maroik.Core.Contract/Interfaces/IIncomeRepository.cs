using Maroik.Core.Domain.Finance;
namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Repository abstraction over <see cref="Income"/> persistence.
/// </summary>
public interface IIncomeRepository : IGenericRepository<Income>
{
    /// <summary>Returns all income records for the given account.</summary>
    Task<List<Income>> GetByAccountEmailAsync(string accountEmail, CancellationToken ct = default);

    /// <summary>
    /// Returns income records for the given account whose MainClass, SubClass, Content,
    /// DepositMyAssetProductName, Note, Amount, Created, Updated, or currency contains
    /// <paramref name="search"/> — filtered in the database instead of loading every record.
    /// </summary>
    Task<List<Income>> SearchByAccountEmailAsync(string accountEmail, string search, CancellationToken ct = default);

    /// <summary>Returns the income record identified by account email and ID, or null if not found.</summary>
    Task<Income?> FindByEmailAndIdAsync(string accountEmail, long id, CancellationToken ct = default);

    /// <summary>
    /// Same as <see cref="FindByEmailAndIdAsync"/> but takes a row lock (<c>SELECT … FOR UPDATE</c>)
    /// on the matched row, held until the ambient <see cref="IUnitOfWork"/> transaction ends. Used by
    /// update/delete flows so two concurrent edits of the same record cannot both read a stale
    /// amount and drift the linked asset balance. Must be called inside an open transaction.
    /// </summary>
    Task<Income?> FindByEmailAndIdForUpdateAsync(string accountEmail, long id, CancellationToken ct = default);

    /// <summary>Returns income records for the given account whose date falls within the given range.</summary>
    Task<List<Income>> GetByAccountEmailAndDateRangeAsync(string accountEmail, DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>Returns the earliest-created income record for the given account, or null if none exists.</summary>
    Task<Income?> GetFirstByAccountEmailOrderedByCreatedAsync(string accountEmail, CancellationToken ct = default);

    /// <summary>Deletes the income record with the given ID.</summary>
    Task DeleteByIdAsync(long id, CancellationToken ct = default);
}
