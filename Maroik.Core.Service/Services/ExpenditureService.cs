using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Finance;
using Maroik.Core.Service.Extensions;
using Maroik.Core.Service.Mappers;
using Microsoft.Extensions.Logging;

namespace Maroik.Core.Service.Services;

/// <summary>
/// Implementation of <see cref="IExpenditureService"/> for managing expenditure transaction records.
/// Asset ownership is verified before create/update operations to prevent cross-account data access.
/// All write operations are wrapped in a database transaction via <see cref="IUnitOfWork"/>.
/// </summary>
public class ExpenditureService(
    IExpenditureRepository expenditureRepository,
    IAssetBalanceStore assetBalance,
    IUnitOfWork unitOfWork,
    ILogger<ExpenditureService> logger,
    TimeProvider timeProvider) : IExpenditureService
{
    /// <inheritdoc />
    public async Task<List<ExpenditureResponse>> GetExpendituresAsync(string accountEmail, CancellationToken ct = default)
        =>
        [
            .. (await expenditureRepository.GetByAccountEmailAsync(accountEmail, ct)).Select(ExpenditureMapper
                .ToResponse)
        ];

    /// <inheritdoc />
    public async Task<List<ExpenditureResponse>> SearchExpendituresAsync(string accountEmail, string search, CancellationToken ct = default)
        =>
        [
            .. (await expenditureRepository.SearchByAccountEmailAsync(accountEmail, search, ct)).Select(
                ExpenditureMapper.ToResponse)
        ];

    /// <inheritdoc />
    public async Task<ExpenditureResponse?> GetByIdAsync(string accountEmail, long id, CancellationToken ct = default)
    {
        var expenditure = await expenditureRepository.FindByEmailAndIdAsync(accountEmail, id, ct);
        return expenditure == null ? null : ExpenditureMapper.ToResponse(expenditure);
    }

    /// <inheritdoc />
    public async Task<ServiceResult> CreateAsync(string accountEmail, ExpenditureRequest request, CancellationToken ct = default)
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        await unitOfWork.BeginAsync(ct: ct);
        try
        {
            var classResult = ExpenditureClassPolicy.Validate(request.MainClass, request.SubClass);
            if (classResult.IsError)
                return await unitOfWork.FailAsync(classResult.FirstError, ct);

            bool isTransfer = classResult.Value;
            // The transfer rule (ExpenditureClassPolicy.ResolveDepositAsset) is checked before any asset is looked up or locked.
            var depositCheck = ExpenditureClassPolicy.ResolveDepositAsset("Expenditure", isTransfer, request.PaymentMethod ?? "", request.MyDepositAsset);
            if (depositCheck.IsError)
                return await unitOfWork.FailAsync(depositCheck.FirstError, ct);

            // GetAssetsOrdinalAsync takes row locks held until commit/rollback, always in ordinal
            // name order, so that concurrent transactions touching the same asset pair (here,
            // elsewhere in this class, or in IncomeService) cannot deadlock on each other.
            Dictionary<string, Asset> touchedAssets = await assetBalance.GetAssetsOrdinalAsync(
                accountEmail, [request.PaymentMethod, isTransfer ? request.MyDepositAsset : null], ct);

            Asset? paymentAsset = touchedAssets.FindAsset(request.PaymentMethod);
            if (!AssetUsagePolicy.CanRecordExpenditure(isTransfer, paymentAsset, touchedAssets.FindAsset(request.MyDepositAsset), out var refusal))
                return await unitOfWork.FailAsync(AssetRefusalMapper.ToResult(AssetRefusalMapper.Expenditure, refusal), ct);

            string currency = paymentAsset.Balance.Currency.Value;
            var expenditureResult = Expenditure.Record(accountEmail, request.MainClass, request.SubClass,
                request.Content, Math.Abs(request.Amount), currency, request.PaymentMethod, depositCheck.Value, utcNow, request.Note, request.Created);
            if (expenditureResult.IsError)
                return await unitOfWork.FailAsync(expenditureResult.FirstError, ct);

            var expenditure = expenditureResult.Value;
            await expenditureRepository.CreateAsync(expenditure, ct);

            var moveResult = await assetBalance.ApplyBalanceMovementsAsync(
                touchedAssets, expenditure.BalanceMovements(), utcNow, ct);
            if (moveResult.IsError)
                return await unitOfWork.FailAsync(moveResult.FirstError, ct);

            await unitOfWork.CommitAsync(ct);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create expenditure for account {AccountEmail}", accountEmail);
            return await unitOfWork.FailAsync(UnexpectedFailure, ct);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> UpdateAsync(string accountEmail, ExpenditureRequest request, CancellationToken ct = default)
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        await unitOfWork.BeginAsync(ct: ct);
        try
        {
            var classResult = ExpenditureClassPolicy.Validate(request.MainClass, request.SubClass);
            if (classResult.IsError)
                return await unitOfWork.FailAsync(classResult.FirstError, ct);

            bool isTransfer = classResult.Value;
            // The transfer rule (ExpenditureClassPolicy.ResolveDepositAsset) is checked before any asset is looked up or locked.
            var depositCheck = ExpenditureClassPolicy.ResolveDepositAsset("Expenditure", isTransfer, request.PaymentMethod ?? "", request.MyDepositAsset);
            if (depositCheck.IsError)
                return await unitOfWork.FailAsync(depositCheck.FirstError, ct);

            // FOR UPDATE: lock the record for the rest of the transaction so a concurrent
            // UpdateAsync/DeleteAsync of the same expenditure cannot read a stale amount here and
            // drift the linked asset balances.
            Expenditure? previous = await expenditureRepository.FindByEmailAndIdForUpdateAsync(accountEmail, request.Id, ct);
            if (previous == null)
                return await unitOfWork.FailAsync(ServiceResult.NotFound("Expenditure.NotFound", ServiceErrorKeys.ExpenditureNotFound), ct);

            // Lock every asset this update can touch (old + new payment/deposit) in a single
            // ordinal-ordered batch so concurrent transactions on the same asset pair cannot deadlock.
            Dictionary<string, Asset> touchedAssets = await assetBalance.GetAssetsOrdinalAsync(accountEmail,
                [request.PaymentMethod, isTransfer ? request.MyDepositAsset : null, previous.PaymentMethod, previous.MyDepositAsset], ct);

            Asset? newPayAsset = touchedAssets.FindAsset(request.PaymentMethod);
            if (!AssetUsagePolicy.CanRetargetExpenditure(isTransfer, newPayAsset, touchedAssets.FindAsset(request.MyDepositAsset),
                    touchedAssets.FindAsset(previous.PaymentMethod), touchedAssets.FindAsset(previous.MyDepositAsset), out var refusal))
                return await unitOfWork.FailAsync(AssetRefusalMapper.ToResult(AssetRefusalMapper.Expenditure, refusal), ct);

            string newCurrency = newPayAsset.Balance.Currency.Value;
            IReadOnlyList<BalanceMovement> before = previous.BalanceMovements();

            var updateResult = previous.Update(request.MainClass, request.SubClass, request.Content,
                Math.Abs(request.Amount), newCurrency, request.PaymentMethod, depositCheck.Value, request.Note, utcNow, request.Created);
            if (updateResult.IsError)
                return await unitOfWork.FailAsync(updateResult.FirstError, ct);

            await expenditureRepository.UpdateEntityAsync(previous, ct);
            var moveResult = await assetBalance.ApplyBalanceMovementsAsync(touchedAssets,
                BalanceMovement.Replacing(before, previous.BalanceMovements()), utcNow, ct);
            if (moveResult.IsError)
                return await unitOfWork.FailAsync(moveResult.FirstError, ct);

            await unitOfWork.CommitAsync(ct);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update expenditure {ExpenditureId} for account {AccountEmail}", request.Id, accountEmail);
            return await unitOfWork.FailAsync(UnexpectedFailure, ct);
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
            // same expenditure cannot reverse its asset impact twice or from a stale amount.
            Expenditure? expenditure = await expenditureRepository.FindByEmailAndIdForUpdateAsync(accountEmail, id, ct);
            if (expenditure == null)
                return await unitOfWork.FailAsync(ServiceResult.NotFound("Expenditure.NotFound", ServiceErrorKeys.ExpenditureNotFound), ct);

            Dictionary<string, Asset> touchedAssets = await assetBalance.GetAssetsOrdinalAsync(
                accountEmail, [expenditure.PaymentMethod, expenditure.MyDepositAsset], ct);
            if (!AssetUsagePolicy.CanMoveBack([touchedAssets.FindAsset(expenditure.PaymentMethod), touchedAssets.FindAsset(expenditure.MyDepositAsset)], out var refusal))
                return await unitOfWork.FailAsync(AssetRefusalMapper.ToResult(AssetRefusalMapper.Expenditure, refusal), ct);

            var moveResult = await assetBalance.ApplyBalanceMovementsAsync(touchedAssets,
                BalanceMovement.Undoing(expenditure.BalanceMovements()), utcNow, ct);
            if (moveResult.IsError)
                return await unitOfWork.FailAsync(moveResult.FirstError, ct);

            await expenditureRepository.DeleteByIdAsync(expenditure.Id, ct);

            await unitOfWork.CommitAsync(ct);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete expenditure {ExpenditureId} for account {AccountEmail}", id, accountEmail);
            return await unitOfWork.FailAsync(UnexpectedFailure, ct);
        }
    }

    /// <summary>Generic failure returned (after logging) when a write throws unexpectedly.</summary>
    private static ServiceResult UnexpectedFailure => ServiceResult.Failure(
        "Expenditure.Unexpected", ServiceResult.TemporaryErrorKey);
}
