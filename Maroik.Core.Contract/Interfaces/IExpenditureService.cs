using Maroik.Core.Contract.Dtos;

namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Service interface for expenditure business logic. Enforces ownership checks before delegating to the repository.
/// </summary>
public interface IExpenditureService
{
    /// <summary>Returns all expenditure records for the given account.</summary>
    Task<List<ExpenditureResponse>> GetExpendituresAsync(string accountEmail, CancellationToken ct = default);

    /// <summary>Returns expenditure records for the given account whose fields contain <paramref name="search"/>, filtered in the database.</summary>
    Task<List<ExpenditureResponse>> SearchExpendituresAsync(string accountEmail, string search, CancellationToken ct = default);

    /// <summary>Returns a single expenditure record owned by the account, or null if not found.</summary>
    Task<ExpenditureResponse?> GetByIdAsync(string accountEmail, long id, CancellationToken ct = default);

    /// <summary>Validates and creates a new expenditure record owned by the given account.</summary>
    Task<ServiceResult> CreateAsync(string accountEmail, ExpenditureRequest request, CancellationToken ct = default);

    /// <summary>Validates and updates an expenditure record, verifying it belongs to the given account.</summary>
    Task<ServiceResult> UpdateAsync(string accountEmail, ExpenditureRequest request, CancellationToken ct = default);

    /// <summary>Deletes an expenditure record, verifying it belongs to the given account.</summary>
    Task<ServiceResult> DeleteAsync(string accountEmail, long id, CancellationToken ct = default);
}
