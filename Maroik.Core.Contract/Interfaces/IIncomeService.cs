using Maroik.Core.Contract.Dtos;

namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Service interface for income business logic. Enforces ownership checks before delegating to the repository.
/// </summary>
public interface IIncomeService
{
    /// <summary>Returns all income records for the given account.</summary>
    Task<List<IncomeResponse>> GetIncomesAsync(string accountEmail, CancellationToken ct = default);

    /// <summary>Returns income records for the given account whose fields contain <paramref name="search"/>, filtered in the database.</summary>
    Task<List<IncomeResponse>> SearchIncomesAsync(string accountEmail, string search, CancellationToken ct = default);

    /// <summary>Returns a single income record owned by the account, or null if not found.</summary>
    Task<IncomeResponse?> GetByIdAsync(string accountEmail, long id, CancellationToken ct = default);

    /// <summary>Validates and creates a new income record owned by the given account.</summary>
    Task<ServiceResult> CreateAsync(string accountEmail, IncomeRequest request, CancellationToken ct = default);

    /// <summary>Validates and updates an income record, verifying it belongs to the given account.</summary>
    Task<ServiceResult> UpdateAsync(string accountEmail, IncomeRequest request, CancellationToken ct = default);

    /// <summary>Deletes an income record, verifying it belongs to the given account.</summary>
    Task<ServiceResult> DeleteAsync(string accountEmail, long id, CancellationToken ct = default);
}
