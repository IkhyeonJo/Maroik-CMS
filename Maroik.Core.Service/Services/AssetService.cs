using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Finance;
using Maroik.Core.Service.Extensions;
using Maroik.Core.Service.Mappers;
using Microsoft.Extensions.Logging;

namespace Maroik.Core.Service.Services;

/// <summary>
/// Implementation of <see cref="IAssetService"/> for managing personal financial assets.
/// A rename runs inside a database transaction (via <see cref="IUnitOfWork"/>) that updates the
/// <c>Asset</c> row's primary key; every table that references it — <c>Income</c>,
/// <c>FixedIncome</c>, and both the <c>PaymentMethod</c> and <c>MyDepositAsset</c> columns of
/// <c>Expenditure</c> / <c>FixedExpenditure</c> — carries an <c>ON UPDATE CASCADE</c> foreign key,
/// so the database propagates the new name to all of them as part of that same statement.
/// </summary>
public class AssetService(
    IAssetRepository assetRepository,
    IUnitOfWork unitOfWork,
    ILogger<AssetService> logger,
    TimeProvider timeProvider) : IAssetService
{
    /// <inheritdoc />
    public async Task<List<AssetResponse>> GetAssetsAsync(string accountEmail, CancellationToken ct = default)
        => [.. (await assetRepository.GetByAccountEmailAsync(accountEmail, ct)).Select(AssetMapper.ToResponse)];

    /// <inheritdoc />
    public async Task<List<AssetResponse>> SearchAssetsAsync(string accountEmail, string search, CancellationToken ct = default)
        =>
        [
            .. (await assetRepository.SearchByAccountEmailAsync(accountEmail, search, ct)).Select(
                AssetMapper.ToResponse)
        ];

    /// <inheritdoc />
    public async Task<AssetResponse?> GetAssetAsync(string accountEmail, string productName, CancellationToken ct = default)
    {
        Asset? asset = await FindAssetAsync(accountEmail, productName, ct);
        return asset == null ? null : AssetMapper.ToResponse(asset);
    }

    /// <inheritdoc />
    public async Task<ServiceResult> CreateAsync(string accountEmail, AssetRequest request, CancellationToken ct = default)
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        if (!AssetItems.IsKnown(request.Item))
            return ServiceResult.Validation("Asset.ItemInvalid", "Asset category (item) is not a recognised value.");

        // Reject duplicate product names within the same account, including a soft-deleted asset
        // under that name: its row still occupies the (ProductName, AccountEmail) primary key, and
        // reviving it in place would silently re-attach that old row's historical transaction
        // history (Expenditure/FixedExpenditure/Income/FixedIncome references) to a "new" asset the
        // user believes is unrelated. A name freed up by soft-deletion is never reusable.
        Asset? existing = await FindAssetAsync(accountEmail, request.ProductName ?? "", ct);
        if (existing != null)
            return ServiceResult.Conflict("Asset.Duplicate", "The asset already exists.");

        var createResult = Asset.Create(
            request.ProductName,
            accountEmail,
            request.Item,
            request.Amount,
            request.MonetaryUnit,
            utcNow,
            request.Note);

        if (createResult.IsError)
            return ServiceResult.FromError(createResult.FirstError);

        var asset = createResult.Value;

        try
        {
            await assetRepository.CreateAsync(asset, ct);
        }
        catch (Exception e) when (e.IsPostgresUniqueViolation())
        {
            // The prior FindAssetAsync check above is a read-then-write race: two concurrent
            // requests for the same (ProductName, AccountEmail) can both pass it. The primary
            // key constraint on that pair still rejects the second insert — turn that into the
            // same friendly result instead of letting the raw DB exception escape.
            return ServiceResult.Conflict("Asset.Duplicate", "The asset already exists.");
        }
        catch (Exception e)
        {
            // Every other write in this class turns an unexpected failure into a result; without this
            // one, a database error other than a duplicate key escaped as an unhandled 500.
            logger.LogError(e, "Failed to create asset {ProductName} for account {AccountEmail}", request.ProductName, accountEmail);
            return ServiceResult.Failure("Asset.CreateFailed", ServiceResult.TemporaryErrorKey);
        }

        return ServiceResult.Ok();
    }

    /// <inheritdoc />
    public async Task<ServiceResult> UpdateAsync(string accountEmail, AssetRequest request, string originalProductName, CancellationToken ct = default)
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        await unitOfWork.BeginAsync(ct: ct);
        try
        {
            if (!AssetItems.IsKnown(request.Item))
            {
                await unitOfWork.RollbackAsync(ct);
                return ServiceResult.Validation("Asset.ItemInvalid", "Asset category (item) is not a recognised value.");
            }

            // Row-locks the asset for the rest of this transaction (held until commit/rollback),
            // so a concurrent Income/Expenditure balance mutation on the same asset can't be lost
            // under this action's absolute-overwrite of Balance.
            Asset? asset = await assetRepository.FindByEmailAndProductNameForUpdateAsync(accountEmail, originalProductName, ct);
            if (asset == null)
            {
                await unitOfWork.RollbackAsync(ct);
                return ServiceResult.NotFound("Asset.NotFound", "Fail to find the asset by given product name");
            }

            // Fall back to the existing currency when the request does not specify one.
            string currency = request.MonetaryUnit ?? asset.Balance.Currency.Value;
            var updateResult = asset.Update(request.ProductName ?? originalProductName, request.Item ?? asset.Item, request.Amount, currency, request.Note, request.Deleted, utcNow);
            if (updateResult.IsError)
            {
                await unitOfWork.RollbackAsync(ct);
                return ServiceResult.FromError(updateResult.FirstError);
            }

            int updated = await assetRepository.UpdateAssetWithProductNameAsync(asset, originalProductName, ct);
            if (updated <= 0)
            {
                await unitOfWork.RollbackAsync(ct);
                return ServiceResult.Failure("Asset.UpdateFailed", "Input is invalid");
            }

            // A ProductName change propagates to every referencing row — Income / FixedIncome and
            // both the PaymentMethod and MyDepositAsset columns of Expenditure / FixedExpenditure —
            // through those tables' ON UPDATE CASCADE foreign keys, as part of the UPDATE above.
            // Nothing to cascade in application code.

            await unitOfWork.CommitAsync(ct);
            return ServiceResult.Ok();
        }
        catch (Exception ex) when (ex.IsPostgresUniqueViolation())
        {
            // Renaming to a product name that already exists for this account hits the same
            // (ProductName, AccountEmail) primary key CreateAsync guards against — report the
            // same friendly message instead of the generic fallback below.
            await unitOfWork.RollbackAsync(ct);
            return ServiceResult.Conflict("Asset.Duplicate", "The asset already exists.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update asset {OriginalProductName} for account {AccountEmail}", originalProductName, accountEmail);
            await unitOfWork.RollbackAsync(ct);
            return ServiceResult.Failure("Asset.UpdateFailed", ServiceResult.TemporaryErrorKey);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> DeleteAsync(string accountEmail, string productName, CancellationToken ct = default)
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        await unitOfWork.BeginAsync(ct: ct);
        try
        {
            // Row-lock the asset for the rest of this transaction, exactly as UpdateAsync does: the
            // soft-delete is a full-row UPDATE (UpdateEntityAsync writes every column), so without the
            // lock a balance mutation committed by a concurrent Income/Expenditure between the read
            // and the write would be silently overwritten with this pre-lock snapshot.
            Asset? asset = await assetRepository.FindByEmailAndProductNameForUpdateAsync(accountEmail, productName, ct);
            if (asset == null)
            {
                await unitOfWork.RollbackAsync(ct);
                return ServiceResult.NotFound("Asset.NotFound", "Fail to find the asset by given product name");
            }

            // Soft-delete preserves the record for historical reporting while hiding it from active use.
            asset.SoftDelete(utcNow);

            await assetRepository.UpdateEntityAsync(asset, ct);
            await unitOfWork.CommitAsync(ct);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete asset {ProductName} for account {AccountEmail}", productName, accountEmail);
            await unitOfWork.RollbackAsync(ct);
            return ServiceResult.Failure("Asset.DeleteFailed", ServiceResult.TemporaryErrorKey);
        }
    }

    /// <summary>Unlocked lookup of the account's asset by product name (soft-deleted assets included).</summary>
    private Task<Asset?> FindAssetAsync(string accountEmail, string productName, CancellationToken ct = default)
        => assetRepository.FindByEmailAndProductNameAsync(accountEmail, productName, ct);
}
