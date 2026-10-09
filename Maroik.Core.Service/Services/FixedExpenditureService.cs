using ErrorOr;
using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Finance;
using Maroik.Core.Domain.Errors;
using Maroik.Core.Service.Mappers;

namespace Maroik.Core.Service.Services;

/// <summary>
/// Implementation of <see cref="IFixedExpenditureService"/> for managing recurring expenditure entries.
/// Validates the expenditure class and deposit day before persisting, and verifies that the
/// referenced asset(s) exist on the caller's account, are not deleted, differ from each other and
/// share a currency before write operations. (The <c>Noticed</c> / <c>Expired</c> display flags are not
/// computed here — see <see cref="FixedSchedulePolicy"/>.)
/// A fixed-expenditure entry is a schedule, not a balance movement, so these operations are single
/// non-transactional repository writes and read the referenced asset without a row lock.
/// </summary>
public class FixedExpenditureService(IFixedExpenditureRepository fixedExpenditureRepository, IAssetBalanceStore assetBalance, TimeProvider timeProvider) : IFixedExpenditureService
{
    /// <inheritdoc />
    public async Task<List<FixedExpenditureResponse>> GetFixedExpendituresAsync(string accountEmail, CancellationToken ct = default)
        =>
        [
            .. (await fixedExpenditureRepository.GetByAccountEmailAsync(accountEmail, ct)).Select(FixedExpenditureMapper
                .ToResponse)
        ];

    /// <inheritdoc />
    public async Task<List<FixedExpenditureResponse>> SearchFixedExpendituresAsync(string accountEmail, string search, CancellationToken ct = default)
        =>
        [
            .. (await fixedExpenditureRepository.SearchByAccountEmailAsync(accountEmail, search, ct)).Select(
                FixedExpenditureMapper.ToResponse)
        ];

    /// <inheritdoc />
    public async Task<FixedExpenditureResponse?> GetByIdAsync(string accountEmail, long id, CancellationToken ct = default)
    {
        var item = await fixedExpenditureRepository.FindByEmailAndIdAsync(accountEmail, id, ct);
        return item == null ? null : FixedExpenditureMapper.ToResponse(item);
    }

    /// <inheritdoc />
    public async Task<ServiceResult> CreateAsync(string accountEmail, FixedExpenditureRequest request, CancellationToken ct = default)
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var validation = ValidateClassAndDate(request);
        if (validation.IsError) return ServiceResult.FromError(validation.FirstError);
        bool requiresDepositAsset = validation.Value;

        Asset? payAsset = await assetBalance.GetAssetForReadAsync(accountEmail, request.PaymentMethod ?? "", ct);
        if (payAsset == null) return AssetNotFoundResult;
        if (payAsset.Deleted) return DeletedAssetResult;

        string? myDepositAsset = null;
        if (requiresDepositAsset)
        {
            if (request.PaymentMethod == request.MyDepositAsset)
                return SameAssetResult;

            Asset? depAsset = await assetBalance.GetAssetForReadAsync(accountEmail, request.MyDepositAsset ?? "", ct);
            if (depAsset == null) return AssetNotFoundResult;
            if (depAsset.Deleted) return DeletedAssetResult;
            if (payAsset.Balance.Currency != depAsset.Balance.Currency)
                return CurrencyMismatchResult;

            myDepositAsset = request.MyDepositAsset;
        }

        string currency = payAsset.Balance.Currency.Value;
        var registerResult = FixedExpenditure.Register(accountEmail, request.MainClass, request.SubClass,
            request.Content, Math.Abs(request.Amount), currency, request.PaymentMethod,
            myDepositAsset, request.DepositMonth, request.DepositDay, request.MaturityDate, utcNow, request.Note);

        if (registerResult.IsError)
            return ServiceResult.FromError(registerResult.FirstError);

        var fixedExpenditure = registerResult.Value;
        ApplyUnpunctuality(fixedExpenditure, request.Unpunctuality, utcNow);
        await fixedExpenditureRepository.CreateAsync(fixedExpenditure, ct);
        return ServiceResult.Ok();
    }

    /// <inheritdoc />
    public async Task<ServiceResult> UpdateAsync(string accountEmail, FixedExpenditureRequest request, CancellationToken ct = default)
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var validation = ValidateClassAndDate(request);
        if (validation.IsError) return ServiceResult.FromError(validation.FirstError);
        bool requiresDepositAsset = validation.Value;

        Asset? payAsset = await assetBalance.GetAssetForReadAsync(accountEmail, request.PaymentMethod ?? "", ct);
        if (payAsset == null) return AssetNotFoundResult;
        if (payAsset.Deleted) return DeletedAssetResult;

        if (requiresDepositAsset)
        {
            if (request.PaymentMethod == request.MyDepositAsset)
                return SameAssetResult;

            var depAsset = await assetBalance.GetAssetForReadAsync(accountEmail, request.MyDepositAsset ?? "", ct);
            if (depAsset == null) return AssetNotFoundResult;
            if (depAsset.Deleted) return DeletedAssetResult;
            if (payAsset.Balance.Currency != depAsset.Balance.Currency)
                return CurrencyMismatchResult;
        }

        FixedExpenditure? fe = await fixedExpenditureRepository.FindByEmailAndIdAsync(accountEmail, request.Id, ct);
        if (fe == null)
            return ServiceResult.NotFound("FixedExpenditure.NotFound", ServiceErrorKeys.FixedExpenditureNotFound);

        string currency = payAsset.Balance.Currency.Value;
        string? myDepositAsset = requiresDepositAsset ? request.MyDepositAsset : null;

        var updateResult = fe.Update(request.MainClass, request.SubClass, request.Content,
            Math.Abs(request.Amount), currency, request.PaymentMethod, myDepositAsset,
            request.DepositMonth, request.DepositDay, request.MaturityDate, request.Note, utcNow);

        if (updateResult.IsError) return ServiceResult.FromError(updateResult.FirstError);

        ApplyUnpunctuality(fe, request.Unpunctuality, utcNow);
        await fixedExpenditureRepository.UpdateEntityAsync(fe, ct);
        return ServiceResult.Ok();
    }

    /// <inheritdoc />
    public async Task<ServiceResult> DeleteAsync(string accountEmail, long id, CancellationToken ct = default)
    {
        _ = timeProvider.GetUtcNow().UtcDateTime;
        FixedExpenditure? fe = await fixedExpenditureRepository.FindByEmailAndIdAsync(accountEmail, id, ct);
        if (fe == null)
            return ServiceResult.NotFound("FixedExpenditure.NotFound", ServiceErrorKeys.FixedExpenditureNotFound);

        await fixedExpenditureRepository.DeleteByIdAsync(fe.Id, ct);
        return ServiceResult.Ok();
    }

    /// <summary>
    /// Applies the user's "always notify" choice (see <c>FixedIncomeService.ApplyUnpunctuality</c>):
    /// <c>FixedExpenditure.Update</c>/<c>Register</c> do not carry it, so it must be applied here.
    /// </summary>
    private static void ApplyUnpunctuality(FixedExpenditure fixedExpenditure, bool unpunctuality, DateTime utcNow)
    {
        if (unpunctuality)
            fixedExpenditure.MarkUnpunctual(utcNow);
        else
            fixedExpenditure.ClearUnpunctuality(utcNow);
    }

    /// <summary>Failure returned when a transfer's payment asset and deposit asset are the same asset.</summary>
    private static ServiceResult SameAssetResult => ServiceResult.Validation(
        "FixedExpenditure.SameAsset", ServiceErrorKeys.SameAsset);

    /// <summary>Failure returned when a transfer's payment asset and deposit asset hold different currencies.</summary>
    private static ServiceResult CurrencyMismatchResult => ServiceResult.Validation(
        "FixedExpenditure.CurrencyMismatch", ServiceErrorKeys.CurrencyMismatch);

    /// <summary>Failure returned when a referenced asset does not exist on the caller's account.</summary>
    private static ServiceResult AssetNotFoundResult => ServiceResult.NotFound(
        "FixedExpenditure.AssetNotFound", ServiceErrorKeys.SelectedAssetNotFound);

    /// <summary>Failure returned when a referenced asset has been soft-deleted.</summary>
    private static ServiceResult DeletedAssetResult => ServiceResult.Conflict(
        "FixedExpenditure.AssetDeleted", ServiceErrorKeys.AssetAlreadyDeleted);

    /// <summary>
    /// Validates the MainClass/SubClass combination and the deposit month/day of a fixed-expenditure
    /// request, and reports whether the entry requires a deposit asset (savings / debt-repayment
    /// categories) as the success value. The maturity date is already a parsed <see cref="DateTime"/>
    /// here — the controller enforces its yyyy-MM-dd string format via ParseExact.
    /// </summary>
    private static ErrorOr<bool> ValidateClassAndDate(FixedExpenditureRequest req)
    {
        var classResult = ExpenditureClassPolicy.Validate(req.MainClass, req.SubClass);
        if (classResult.IsError) return classResult.FirstError;
        bool requiresDeposit = classResult.Value;

        if (req.DepositMonth is < 1 or > 12)
            return Error.Validation("FixedExpenditure.DepositMonth", "Deposit month must be between 1 and 12.");

        if (!FixedSchedulePolicy.IsValidDepositDate(req.DepositMonth, req.DepositDay))
            return DomainError.Validation("FixedExpenditure.DepositDay",
                "Deposit day must be between 1 and {0} for month {1}.",
                FixedSchedulePolicy.MaxDepositDay(req.DepositMonth), req.DepositMonth);

        return requiresDeposit;
    }
}
