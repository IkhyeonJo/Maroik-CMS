using Maroik.Core.Contract.Dtos;

namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Service interface for fixed (recurring) income business logic.
/// </summary>
public interface IFixedIncomeService
{
    /// <summary>Returns all fixed income records for the given account.</summary>
    Task<List<FixedIncomeResponse>> GetFixedIncomesAsync(string accountEmail, CancellationToken ct = default);

    /// <summary>Returns fixed income records for the given account whose fields contain <paramref name="search"/>, filtered in the database.</summary>
    Task<List<FixedIncomeResponse>> SearchFixedIncomesAsync(string accountEmail, string search, CancellationToken ct = default);

    /// <summary>Returns a single fixed income record owned by the account, or null if not found.</summary>
    Task<FixedIncomeResponse?> GetByIdAsync(string accountEmail, long id, CancellationToken ct = default);

    /// <summary>Validates and creates a new fixed income record.</summary>
    Task<ServiceResult> CreateAsync(string accountEmail, FixedIncomeRequest request, CancellationToken ct = default);

    /// <summary>Validates and updates an existing fixed income record.</summary>
    Task<ServiceResult> UpdateAsync(string accountEmail, FixedIncomeRequest request, CancellationToken ct = default);

    /// <summary>Deletes a fixed income record, verifying it belongs to the given account.</summary>
    Task<ServiceResult> DeleteAsync(string accountEmail, long id, CancellationToken ct = default);
}
