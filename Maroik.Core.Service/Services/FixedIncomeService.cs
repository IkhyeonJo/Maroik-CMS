using ErrorOr;
using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Finance;
using Maroik.Core.Domain.Localization;
using Maroik.Core.Service.Mappers;

namespace Maroik.Core.Service.Services;

/// <summary>
/// Implementation of <see cref="IFixedIncomeService"/> for managing recurring income entries.
/// Validates the income class and the deposit day against the deposit month's maximum
/// (<see cref="FixedSchedulePolicy.MaxDepositDay"/>: 29–31, February always 29) and requires the
/// deposit asset to exist and not be deleted before persisting. (The <c>Noticed</c> / <c>Expired</c>
/// display flags are not computed here — see <see cref="FixedSchedulePolicy"/>.)
/// A fixed-income entry is a schedule, not a balance movement, so these operations are single
/// non-transactional repository writes and read the referenced asset without a row lock.
/// </summary>
public class FixedIncomeService(IFixedIncomeRepository fixedIncomeRepository, IAssetBalanceStore assetBalance, TimeProvider timeProvider) : IFixedIncomeService
{
    /// <inheritdoc />
    public async Task<List<FixedIncomeResponse>> GetFixedIncomesAsync(string accountEmail, CancellationToken ct = default)
        =>
        [
            .. (await fixedIncomeRepository.GetByAccountEmailAsync(accountEmail, ct)).Select(FixedIncomeMapper
                .ToResponse)
        ];

    /// <inheritdoc />
    public async Task<List<FixedIncomeResponse>> SearchFixedIncomesAsync(string accountEmail, string search, CancellationToken ct = default)
        =>
        [
            .. (await fixedIncomeRepository.SearchByAccountEmailAsync(accountEmail, search, ct)).Select(
                FixedIncomeMapper.ToResponse)
        ];

    /// <inheritdoc />
    public async Task<FixedIncomeResponse?> GetByIdAsync(string accountEmail, long id, CancellationToken ct = default)
    {
        var item = await fixedIncomeRepository.FindByEmailAndIdAsync(accountEmail, id, ct);
        return item == null ? null : FixedIncomeMapper.ToResponse(item);
    }

    /// <inheritdoc />
    public async Task<ServiceResult> CreateAsync(string accountEmail, FixedIncomeRequest request, CancellationToken ct = default)
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var validation = ValidateRequest(request);
        if (validation.IsError) return ServiceResult.FromError(validation.FirstError);

        Asset? asset = await assetBalance.GetAssetForReadAsync(accountEmail, request.DepositMyAssetProductName ?? "", ct);
        if (asset == null)
            return ServiceResult.NotFound("FixedIncome.AssetNotFound", "The selected asset could not be found.");
        if (asset.Deleted)
            return DeletedAssetResult;

        string currency = asset.Balance.Currency.Value;
        var registerResult = FixedIncome.Register(accountEmail, request.MainClass, request.SubClass,
            request.Content, Math.Abs(request.Amount), currency,
            request.DepositMyAssetProductName, request.DepositMonth, request.DepositDay,
            request.MaturityDate, utcNow, request.Note);

        if (registerResult.IsError)
            return ServiceResult.FromError(registerResult.FirstError);

        var fixedIncome = registerResult.Value;
        ApplyUnpunctuality(fixedIncome, request.Unpunctuality, utcNow);
        await fixedIncomeRepository.CreateAsync(fixedIncome, ct);
        return ServiceResult.Ok();
    }

    /// <inheritdoc />
    public async Task<ServiceResult> UpdateAsync(string accountEmail, FixedIncomeRequest request, CancellationToken ct = default)
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var validation = ValidateRequest(request);
        if (validation.IsError) return ServiceResult.FromError(validation.FirstError);

        FixedIncome? fi = await fixedIncomeRepository.FindByEmailAndIdAsync(accountEmail, request.Id, ct);
        if (fi == null)
            return ServiceResult.NotFound("FixedIncome.NotFound", "The fixed-income record could not be found.");

        Asset? asset = await assetBalance.GetAssetForReadAsync(accountEmail, request.DepositMyAssetProductName ?? "", ct);
        if (asset == null)
            return ServiceResult.NotFound("FixedIncome.AssetNotFound", "The selected asset could not be found.");
        if (asset.Deleted)
            return DeletedAssetResult;
        string currency = asset.Balance.Currency.Value;

        var updateResult = fi.Update(request.MainClass, request.SubClass, request.Content,
            Math.Abs(request.Amount), currency, request.DepositMyAssetProductName,
            request.DepositMonth, request.DepositDay, request.MaturityDate, request.Note, utcNow);

        if (updateResult.IsError) return ServiceResult.FromError(updateResult.FirstError);

        ApplyUnpunctuality(fi, request.Unpunctuality, utcNow);
        await fixedIncomeRepository.UpdateEntityAsync(fi, ct);
        return ServiceResult.Ok();
    }

    /// <inheritdoc />
    public async Task<ServiceResult> DeleteAsync(string accountEmail, long id, CancellationToken ct = default)
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        FixedIncome? fi = await fixedIncomeRepository.FindByEmailAndIdAsync(accountEmail, id, ct);
        if (fi == null)
            return ServiceResult.NotFound("FixedIncome.NotFound", "The fixed-income record could not be found.");

        await fixedIncomeRepository.DeleteByIdAsync(fi.Id, ct);
        return ServiceResult.Ok();
    }

    /// <summary>
    /// Applies the user's "always notify" choice. <c>FixedIncome.Update</c>/<c>Register</c> do not
    /// carry it, so without this the edit form's Unpunctuality checkbox would reach the request and
    /// then be silently dropped — the flag could never be set or cleared.
    /// </summary>
    private static void ApplyUnpunctuality(FixedIncome fixedIncome, bool unpunctuality, DateTime utcNow)
    {
        if (unpunctuality)
            fixedIncome.MarkUnpunctual(utcNow);
        else
            fixedIncome.ClearUnpunctuality(utcNow);
    }

    /// <summary>Failure returned when the chosen deposit asset has been soft-deleted.</summary>
    private static ServiceResult DeletedAssetResult => ServiceResult.Conflict(
        "FixedIncome.AssetDeleted", "Actions cannot be executed with assets that have already been deleted.");

    /// <summary>
    /// Validates the income class and deposit month/day of a fixed-income request.
    /// (The maturity date is already a parsed <see cref="DateTime"/> by this point — the
    /// controller enforces its yyyy-MM-dd string format via ParseExact.)
    /// </summary>
    private static ErrorOr<Success> ValidateRequest(FixedIncomeRequest request)
    {
        var classResult = IncomeClassPolicy.Validate(request.MainClass, request.SubClass);
        if (classResult.IsError) return classResult.FirstError;

        if (request.DepositMonth is < 1 or > 12)
            return Error.Validation("FixedIncome.DepositMonth", "Deposit month must be between 1 and 12.");

        if (!FixedSchedulePolicy.IsValidDepositDate(request.DepositMonth, request.DepositDay))
            return LocalizableError.Validation("FixedIncome.DepositDay",
                "Deposit day must be between 1 and {0} for month {1}.",
                FixedSchedulePolicy.MaxDepositDay(request.DepositMonth), request.DepositMonth);

        return Result.Success;
    }
}
