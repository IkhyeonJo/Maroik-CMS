using ErrorOr;
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
            if (isTransfer && request.PaymentMethod == request.MyDepositAsset)
                return await unitOfWork.FailAsync(SameAssetResult, ct);

            // GetAssetsOrdinalAsync takes row locks held until commit/rollback, always in ordinal
            // name order, so that concurrent transactions touching the same asset pair (here,
            // elsewhere in this class, or in IncomeService) cannot deadlock on each other.
            Dictionary<string, Asset> touchedAssets = await assetBalance.GetAssetsOrdinalAsync(
                accountEmail, [request.PaymentMethod, isTransfer ? request.MyDepositAsset : null], ct);

            Asset? paymentAsset = !string.IsNullOrEmpty(request.PaymentMethod) && touchedAssets.TryGetValue(request.PaymentMethod, out var pay) ? pay : null;
            Asset? depositAsset = isTransfer && !string.IsNullOrEmpty(request.MyDepositAsset) && touchedAssets.TryGetValue(request.MyDepositAsset, out var dep) ? dep : null;

            if (paymentAsset == null)
                return await unitOfWork.FailAsync(AssetNotFoundResult, ct);
            if (paymentAsset.Deleted)
                return await unitOfWork.FailAsync(DeletedAssetResult, ct);

            if (isTransfer)
            {
                if (depositAsset == null)
                    return await unitOfWork.FailAsync(AssetNotFoundResult, ct);
                if (depositAsset.Deleted)
                    return await unitOfWork.FailAsync(DeletedAssetResult, ct);
                if (paymentAsset.Balance.Currency != depositAsset.Balance.Currency)
                    return await unitOfWork.FailAsync(CurrencyMismatchResult, ct);
            }

            string currency = paymentAsset.Balance.Currency.Value;
            string? myDepositAsset = isTransfer ? request.MyDepositAsset : null;

            var expenditureResult = Expenditure.Record(accountEmail, request.MainClass, request.SubClass,
                request.Content, Math.Abs(request.Amount), currency, request.PaymentMethod, myDepositAsset, utcNow, request.Note, request.Created);
            if (expenditureResult.IsError)
                return await unitOfWork.FailAsync(expenditureResult.FirstError, ct);

            var expenditure = expenditureResult.Value;
            await expenditureRepository.CreateAsync(expenditure, ct);

            var withdrawResult = paymentAsset.Withdraw(expenditure.Amount, utcNow);
            if (withdrawResult.IsError)
                return await unitOfWork.FailAsync(withdrawResult.FirstError, ct);
            await assetBalance.SaveAsync(paymentAsset, ct);

            if (depositAsset != null)
            {
                var depositResult = depositAsset.Deposit(expenditure.Amount, utcNow);
                if (depositResult.IsError)
                    return await unitOfWork.FailAsync(depositResult.FirstError, ct);
                await assetBalance.SaveAsync(depositAsset, ct);
            }

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

            if (classResult.Value && request.PaymentMethod == request.MyDepositAsset)
                return await unitOfWork.FailAsync(SameAssetResult, ct);

            // FOR UPDATE: lock the record for the rest of the transaction so a concurrent
            // UpdateAsync/DeleteAsync of the same expenditure cannot read a stale amount here and
            // drift the linked asset balances.
            Expenditure? previous = await expenditureRepository.FindByEmailAndIdForUpdateAsync(accountEmail, request.Id, ct);
            if (previous == null)
                return await unitOfWork.FailAsync(ServiceResult.NotFound("Expenditure.NotFound", "The expenditure record could not be found."), ct);

            // Lock every asset this update can touch (old + new payment/deposit) in a single
            // ordinal-ordered batch so concurrent transactions on the same asset pair cannot deadlock.
            Dictionary<string, Asset> touchedAssets = await assetBalance.GetAssetsOrdinalAsync(accountEmail,
                [request.PaymentMethod, classResult.Value ? request.MyDepositAsset : null, previous.PaymentMethod, previous.MyDepositAsset], ct);

            Asset? newPayAsset = !string.IsNullOrEmpty(request.PaymentMethod) && touchedAssets.TryGetValue(request.PaymentMethod, out var np) ? np : null;
            Asset? newDepAsset = classResult.Value && !string.IsNullOrEmpty(request.MyDepositAsset) && touchedAssets.TryGetValue(request.MyDepositAsset, out var nd) ? nd : null;

            // A record must not be retargeted onto a since-archived asset, matching Create: a
            // deleted asset's balance must never be touched.
            if (newPayAsset?.Deleted == true || newDepAsset?.Deleted == true)
                return await unitOfWork.FailAsync(DeletedAssetResult, ct);

            // Matches CreateAsync: the payment asset is required regardless of transfer/non-transfer,
            // so a retarget onto a nonexistent asset name fails clean here instead of silently
            // persisting the update and only surfacing as an AdjustAssetBalancesAsync rollback later.
            if (newPayAsset == null)
                return await unitOfWork.FailAsync(AssetNotFoundResult, ct);

            if (classResult.Value)
            {
                if (newDepAsset == null)
                    return await unitOfWork.FailAsync(AssetNotFoundResult, ct);
                if (newPayAsset.Balance.Currency != newDepAsset.Balance.Currency)
                    return await unitOfWork.FailAsync(CurrencyMismatchResult, ct);
            }

            Asset? prevPayAsset = !string.IsNullOrEmpty(previous.PaymentMethod) && touchedAssets.TryGetValue(previous.PaymentMethod, out var pp) ? pp : null;
            if (prevPayAsset?.Deleted == true)
                return await unitOfWork.FailAsync(DeletedAssetResult, ct);
            if (!string.IsNullOrEmpty(previous.MyDepositAsset))
            {
                Asset? prevDepAsset = touchedAssets.GetValueOrDefault(previous.MyDepositAsset);
                if (prevDepAsset?.Deleted == true)
                    return await unitOfWork.FailAsync(DeletedAssetResult, ct);
            }

            // newPayAsset is guaranteed non-null past the AssetNotFoundResult guard above.
            string newCurrency = newPayAsset.Balance.Currency.Value;
            string? myDepositAsset = classResult.Value ? request.MyDepositAsset : null;

            var oldSnapshot = new ExpenditureSnapshot(previous.PaymentMethod, previous.MyDepositAsset, previous.Amount.Amount, previous.Amount.Currency.Value);

            var updateResult = previous.Update(request.MainClass, request.SubClass, request.Content,
                Math.Abs(request.Amount), newCurrency, request.PaymentMethod, myDepositAsset, request.Note, utcNow, request.Created);
            if (updateResult.IsError)
                return await unitOfWork.FailAsync(updateResult.FirstError, ct);

            await expenditureRepository.UpdateEntityAsync(previous, ct);
            var adjustResult = await AdjustAssetBalancesAsync(touchedAssets, oldSnapshot,
                new ExpenditureSnapshot(previous.PaymentMethod, previous.MyDepositAsset, previous.Amount.Amount, previous.Amount.Currency.Value), utcNow, ct);
            if (adjustResult.IsError)
                return await unitOfWork.FailAsync(adjustResult.FirstError, ct);

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
                return await unitOfWork.FailAsync(ServiceResult.NotFound("Expenditure.NotFound", "The expenditure record could not be found."), ct);

            // An expenditure referencing a since-archived (soft-deleted) asset must not be
            // deletable, matching Create/Update: a deleted asset's balance must not be touched.
            Dictionary<string, Asset> assetsToCheck = await assetBalance.GetAssetsOrdinalAsync(
                accountEmail, [expenditure.PaymentMethod, expenditure.MyDepositAsset], ct);
            if (!string.IsNullOrEmpty(expenditure.PaymentMethod) &&
                assetsToCheck.TryGetValue(expenditure.PaymentMethod, out var payAssetToCheck) && payAssetToCheck.Deleted || !string.IsNullOrEmpty(expenditure.MyDepositAsset) &&
                assetsToCheck.TryGetValue(expenditure.MyDepositAsset, out var depAssetToCheck) && depAssetToCheck.Deleted)
                return await unitOfWork.FailAsync(DeletedAssetResult, ct);

            var adjustResult = await AdjustAssetBalancesAsync(assetsToCheck,
                new ExpenditureSnapshot(expenditure.PaymentMethod, expenditure.MyDepositAsset, expenditure.Amount.Amount, expenditure.Amount.Currency.Value),
                null, utcNow, ct);
            if (adjustResult.IsError)
                return await unitOfWork.FailAsync(adjustResult.FirstError, ct);

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

    /// <summary>Failure returned when a transfer's payment asset and deposit asset are the same asset.</summary>
    private static ServiceResult SameAssetResult => ServiceResult.Validation(
        "Expenditure.SameAsset", "The PaymentMethod and MyDepositAsset value cannot be the same.");

    /// <summary>Failure returned when a transfer's payment asset and deposit asset hold different currencies.</summary>
    private static ServiceResult CurrencyMismatchResult => ServiceResult.Validation(
        "Expenditure.CurrencyMismatch", "PaymentMethod MonetaryUnit must be same as MyDepositAsset MonetaryUnit.");

    /// <summary>Failure returned when a referenced asset does not exist on the caller's account.</summary>
    private static ServiceResult AssetNotFoundResult => ServiceResult.NotFound(
        "Expenditure.AssetNotFound", "The selected asset could not be found.");

    /// <summary>Failure returned when a referenced asset has been soft-deleted.</summary>
    private static ServiceResult DeletedAssetResult => ServiceResult.Conflict(
        "Expenditure.AssetDeleted", "Actions cannot be executed with assets that have already been deleted.");

    /// <summary>Generic failure returned (after logging) when a write throws unexpectedly.</summary>
    private static ServiceResult UnexpectedFailure => ServiceResult.Failure(
        "Expenditure.Unexpected", ServiceResult.TemporaryErrorKey);

    /// <summary>
    /// Reverts <paramref name="toRevert"/>'s asset impact and applies <paramref name="toApply"/>'s asset impact.
    /// Pass null for toApply when deleting. Applies against <paramref name="touchedAssets"/> — the
    /// dictionary the caller already fetched (via <see cref="AssetBalanceOrdinalLockExtensions.GetAssetsOrdinalAsync"/>)
    /// for its own pre-write validation — instead of fetching the same assets again.
    /// </summary>
    private async Task<ErrorOr<Success>> AdjustAssetBalancesAsync(
        Dictionary<string, Asset> touchedAssets, ExpenditureSnapshot toRevert, ExpenditureSnapshot? toApply, DateTime utcNow, CancellationToken ct = default)
    {
        List<AssetBalanceAdjustment> adjustments =
        [
            new(toRevert.PaymentMethod, toRevert.Amount, IsDeposit: true, Currency: toRevert.Currency),
            new(toRevert.MyDepositAsset, toRevert.Amount, IsDeposit: false, Currency: toRevert.Currency)
        ];
        if (!toApply.HasValue)
            return await assetBalance.ApplyAssetBalanceAdjustmentsAsync(
                touchedAssets, adjustments, "Expenditure.AssetNotFound", "The asset referenced by this expenditure could not be found.", utcNow, ct);
        adjustments.Add(new AssetBalanceAdjustment(toApply.Value.PaymentMethod, toApply.Value.Amount, IsDeposit: false, Currency: toApply.Value.Currency));
        adjustments.Add(new AssetBalanceAdjustment(toApply.Value.MyDepositAsset, toApply.Value.Amount, IsDeposit: true, Currency: toApply.Value.Currency));

        return await assetBalance.ApplyAssetBalanceAdjustmentsAsync(
            touchedAssets, adjustments, "Expenditure.AssetNotFound", "The asset referenced by this expenditure could not be found.", utcNow, ct);
    }

    /// <summary>
    /// Captures the asset-related fields of an <see cref="Expenditure"/> at a point in time (including
    /// the currency the amount was recorded in) so that the corresponding asset balances can be
    /// correctly reversed during update or delete operations.
    /// </summary>
    private readonly record struct ExpenditureSnapshot(string? PaymentMethod, string? MyDepositAsset, decimal Amount, string Currency);
}
