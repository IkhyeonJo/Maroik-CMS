using Maroik.Core.Contract.Dtos;

namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Service interface for fixed (recurring) expenditure business logic.
/// </summary>
public interface IFixedExpenditureService
{
    /// <summary>Returns all fixed expenditure records for the given account.</summary>
    Task<List<FixedExpenditureResponse>> GetFixedExpendituresAsync(string accountEmail, CancellationToken ct = default);

    /// <summary>Returns fixed expenditure records for the given account whose fields contain <paramref name="search"/>, filtered in the database.</summary>
    Task<List<FixedExpenditureResponse>> SearchFixedExpendituresAsync(string accountEmail, string search, CancellationToken ct = default);

    /// <summary>Returns a single fixed expenditure record owned by the account, or null if not found.</summary>
    Task<FixedExpenditureResponse?> GetByIdAsync(string accountEmail, long id, CancellationToken ct = default);

    /// <summary>Validates and creates a new fixed expenditure record.</summary>
    Task<ServiceResult> CreateAsync(string accountEmail, FixedExpenditureRequest request, CancellationToken ct = default);

    /// <summary>Validates and updates an existing fixed expenditure record.</summary>
    Task<ServiceResult> UpdateAsync(string accountEmail, FixedExpenditureRequest request, CancellationToken ct = default);

    /// <summary>Deletes a fixed expenditure record, verifying it belongs to the given account.</summary>
    Task<ServiceResult> DeleteAsync(string accountEmail, long id, CancellationToken ct = default);
}
