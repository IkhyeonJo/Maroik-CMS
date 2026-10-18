using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Finances;
using Maroik.Core.Service.Extensions;
using Maroik.Core.Service.Mappers;
using Microsoft.Extensions.Logging;

namespace Maroik.Core.Service.Services;

/// <summary>
/// Implementation of <see cref="IIncomeService"/> for managing income transaction records.
/// Asset ownership is verified before create/update operations to prevent cross-account data access.
/// All write operations are wrapped in a database transaction via <see cref="IUnitOfWork"/>.
/// </summary>
public class IncomeService(IIncomeRepository incomeRepository, IAssetBalanceStore assetBalance, 
    IUnitOfWork unitOfWork, ILogger<IncomeService> logger, TimeProvider timeProvider) : IIncomeService
{
    /// <inheritdoc />
    public async Task<List<IncomeResponse>> GetIncomesAsync(string accountEmail, CancellationToken ct = default)
        => [.. (await incomeRepository.GetByAccountEmailAsync(accountEmail, ct)).Select(IncomeMapper.ToResponse)];

    /// <inheritdoc />
    public async Task<List<IncomeResponse>> SearchIncomesAsync(string accountEmail, string search, CancellationToken ct = default)
        =>
        [
            .. (await incomeRepository.SearchByAccountEmailAsync(accountEmail, search, ct)).Select(IncomeMapper
                .ToResponse)
        ];

    /// <inheritdoc />
    public async Task<IncomeResponse?> GetByIdAsync(string accountEmail, long id, CancellationToken ct = default)
    {
        var income = await incomeRepository.FindByEmailAndIdAsync(accountEmail, id, ct);
        return income == null ? null : IncomeMapper.ToResponse(income);
    }

    /// <inheritdoc />
    public async Task<ServiceResult> CreateAsync(string accountEmail, IncomeRequest request, CancellationToken ct = default)
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        await unitOfWork.BeginAsync(ct: ct);
        try
        {
            var classResult = IncomeClassPolicy.Validate(request.MainClass, request.SubClass);
            if (classResult.IsError)
                return await unitOfWork.FailAsync(classResult.FirstError, ct);

            Asset? depositAsset = await assetBalance.GetAssetAsync(accountEmail, request.DepositMyAssetProductName ?? "", ct);
            if (!AssetUsagePolicy.CanUse(depositAsset, out var refusal))
                return await unitOfWork.FailAsync(AssetRefusalMapper.ToResult(AssetRefusalMapper.Income, refusal), ct);

            string currency = depositAsset.Balance.Currency.Value;
            var incomeResult = Income.Record(accountEmail, request.MainClass, request.SubClass,
                request.Content, Math.Abs(request.Amount), currency, request.DepositMyAssetProductName, utcNow, request.Note, request.Created);
            if (incomeResult.IsError)
                return await unitOfWork.FailAsync(incomeResult.FirstError, ct);

            var income = incomeResult.Value;
            await incomeRepository.CreateAsync(income, ct);

            Dictionary<string, Asset> touchedAssets = new(StringComparer.Ordinal) { [depositAsset.ProductName] = depositAsset };
            var moveResult = await assetBalance.ApplyBalanceMovementsAsync(
                touchedAssets, income.BalanceMovements(), utcNow, ct);
            if (moveResult.IsError)
                return await unitOfWork.FailAsync(moveResult.FirstError, ct);

            await unitOfWork.CommitAsync(ct);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create income for account {AccountEmail}", accountEmail);
            await unitOfWork.RollbackAsync(ct);
            return UnexpectedFailure;
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> UpdateAsync(string accountEmail, IncomeRequest request, CancellationToken ct = default)
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        await unitOfWork.BeginAsync(ct: ct);
        try
        {
            var classResult = IncomeClassPolicy.Validate(request.MainClass, request.SubClass);
            if (classResult.IsError)
                return await unitOfWork.FailAsync(classResult.FirstError, ct);

            // FOR UPDATE: lock the record for the rest of the transaction so a concurrent
            // UpdateAsync/DeleteAsync of the same income cannot read a stale amount here and
            // drift the linked asset balance.
            Income? previous = await incomeRepository.FindByEmailAndIdForUpdateAsync(accountEmail, request.Id, ct);
            if (previous == null)
                return await unitOfWork.FailAsync(ServiceResult.NotFound("Income.NotFound", ServiceErrorKeys.IncomeNotFound), ct);

            // Lock the previous and new deposit assets in a single ordinal-ordered batch so
            // concurrent transactions on the same asset pair (here or in ExpenditureService)
            // cannot deadlock on each other.
            Dictionary<string, Asset> touchedAssets = await assetBalance.GetAssetsOrdinalAsync(accountEmail,
                [previous.DepositMyAssetProductName, request.DepositMyAssetProductName], ct);

            Asset? prevAsset = touchedAssets.FindAsset(previous.DepositMyAssetProductName);
            Asset? newAsset = touchedAssets.FindAsset(request.DepositMyAssetProductName);
            if (!AssetUsagePolicy.CanRetargetIncome(prevAsset, request.DepositMyAssetProductName, newAsset, out var refusal))
                return await unitOfWork.FailAsync(AssetRefusalMapper.ToResult(AssetRefusalMapper.Income, refusal), ct);
            string newCurrency = newAsset?.Balance.Currency.Value ?? prevAsset?.Balance.Currency.Value ?? "";

            IReadOnlyList<BalanceMovement> before = previous.BalanceMovements();

            var updateResult = previous.Update(request.MainClass, request.SubClass, request.Content,
                Math.Abs(request.Amount), newCurrency, request.DepositMyAssetProductName, request.Note, utcNow, request.Created);
            if (updateResult.IsError)
                return await unitOfWork.FailAsync(updateResult.FirstError, ct);

            await incomeRepository.UpdateEntityAsync(previous, ct);
            var moveResult = await assetBalance.ApplyBalanceMovementsAsync(touchedAssets,
                BalanceMovement.Replacing(before, previous.BalanceMovements()), utcNow, ct);
            if (moveResult.IsError)
                return await unitOfWork.FailAsync(moveResult.FirstError, ct);

            await unitOfWork.CommitAsync(ct);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update income {IncomeId} for account {AccountEmail}", request.Id, accountEmail);
            await unitOfWork.RollbackAsync(ct);
            return UnexpectedFailure;
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> DeleteAsync(string accountEmail, long id, CancellationToken ct = default)
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        await unitOfWork.BeginAsync(ct: ct);
        try
        {
            // FOR UPDATE: see UpdateAsync — lock the record so a concurrent edit/delete of the
            // same income cannot reverse its asset impact twice or from a stale amount.
            Income? income = await incomeRepository.FindByEmailAndIdForUpdateAsync(accountEmail, id, ct);
            if (income == null)
                return await unitOfWork.FailAsync(ServiceResult.NotFound("Income.NotFound", ServiceErrorKeys.IncomeNotFound), ct);

            // An income referencing a since-archived (soft-deleted) asset must not be deletable,
            // matching Create/Update: a deleted asset's balance must not be touched.
            Dictionary<string, Asset> touchedAssets = await assetBalance.GetAssetsOrdinalAsync(
                accountEmail, [income.DepositMyAssetProductName], ct);
            if (!AssetUsagePolicy.CanMoveBack([touchedAssets.FindAsset(income.DepositMyAssetProductName)], out var refusal))
                return await unitOfWork.FailAsync(AssetRefusalMapper.ToResult(AssetRefusalMapper.Income, refusal), ct);

            var moveResult = await assetBalance.ApplyBalanceMovementsAsync(touchedAssets,
                BalanceMovement.Undoing(income.BalanceMovements()), utcNow, ct);
            if (moveResult.IsError)
                return await unitOfWork.FailAsync(moveResult.FirstError, ct);

            await incomeRepository.DeleteByIdAsync(income.Id, ct);

            await unitOfWork.CommitAsync(ct);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete income {IncomeId} for account {AccountEmail}", id, accountEmail);
            await unitOfWork.RollbackAsync(ct);
            return UnexpectedFailure;
        }
    }

    /// <summary>Generic failure returned (after logging) when a write throws unexpectedly.</summary>
    private static ServiceResult UnexpectedFailure => ServiceResult.Failure(
        "Income.Unexpected", ServiceResult.TemporaryErrorKey);
}
