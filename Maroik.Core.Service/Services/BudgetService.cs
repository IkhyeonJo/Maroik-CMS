using ErrorOr;
using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Accounts;
using Maroik.Core.Domain.Budgets;
using Maroik.Core.Domain.Extensions;
using Maroik.Core.Domain.Finances;
using Maroik.Core.Service.Mappers;
using Microsoft.Extensions.Logging;

namespace Maroik.Core.Service.Services;

/// <summary>
/// Implementation of <see cref="IBudgetService"/>, the budget sheets. Every request loads the account's whole workbook — its stored
/// sheets, the sheet asked for, last year's (for the carry-over) and any sheet a formula names, the ones not stored yet with their
/// default lines — together with the actual records of those years, and lets <see cref="BudgetWorkbook"/> calculate it: values are
/// never stored, so they can never be stale.
/// <para>
/// A sheet is stored on its first write, in a transaction of its own that locks the account row (two first writes cannot create it
/// twice); each write then runs in its own transaction with the sheet row locked (FOR UPDATE), so concurrent edits of one sheet are
/// serialized and the last committed edit of a cell wins. A failure is logged, rolled back and answered with the temporary error.
/// </para>
/// </summary>
public class BudgetService(
    IBudgetSheetRepository sheetRepository,
    IBudgetSheetLineRepository lineRepository,
    IBudgetSheetCellRepository cellRepository,
    IAccountRepository accountRepository,
    IAssetRepository assetRepository,
    IIncomeRepository incomeRepository,
    IExpenditureRepository expenditureRepository,
    IUnitOfWork unitOfWork,
    ILogger<BudgetService> logger,
    TimeProvider timeProvider) : IBudgetService
{
    /// <summary>The most sheets one workbook loads (a formula naming more sheets reads #REF! for the rest).</summary>
    private const int MaxSheets = 60;

    /// <summary>The current UTC time.</summary>
    private DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;

    /// <summary>The temporary error a failure is answered with.</summary>
    private static ServiceResult Temporary() => ServiceResult.Failure("Budget.Failed", ServiceResult.TemporaryErrorKey);

    /// <summary>The refusal of a bad kind, year or e-mail, or null when the sheet can exist.</summary>
    private ServiceResult? Refusal(string accountEmail, BudgetKind kind, int year) =>
        BudgetSheet.Create(accountEmail, kind, year, UtcNow) is { IsError: true } refused ? ServiceResult.FromError(refused.FirstError) : null;

    /// <inheritdoc />
    public async Task<(ServiceResult Result, BudgetSheetResponse? Sheet)> GetSheetAsync(string accountEmail, BudgetKind kind, int year, string timeZoneIanaId, CancellationToken ct = default)
    {
        if (Refusal(accountEmail, kind, year) is { } refused) return (refused, null);
        BudgetWorkbook workbook = await LoadAsync(accountEmail, kind, year, timeZoneIanaId, [], ct);
        return (ServiceResult.Ok(), BudgetMapper.ToResponse(workbook.View(kind, year)));
    }

    /// <inheritdoc />
    public async Task<(ServiceResult Result, BudgetPreviewResponse? Preview)> PreviewAsync(string accountEmail, BudgetKind kind, int year, string timeZoneIanaId, string address, string input, CancellationToken ct = default)
    {
        if (Refusal(accountEmail, kind, year) is { } refused) return (refused, null);
        BudgetWorkbook workbook = await LoadAsync(accountEmail, kind, year, timeZoneIanaId, [input], ct);
        return (ServiceResult.Ok(), BudgetMapper.ToResponse(workbook.Preview(kind, year, address, input)));
    }

    /// <inheritdoc />
    public Task<(ServiceResult Result, BudgetEditResponse? Edit)> EditAsync(string accountEmail, BudgetKind kind, int year, string timeZoneIanaId, IReadOnlyList<BudgetCellEdit> edits, CancellationToken ct = default) =>
        WriteAsync(accountEmail, kind, year, timeZoneIanaId, [.. edits.Select(e => e.Input ?? "")], workbook => workbook.Edit(kind, year, edits),
            "edit", ct);

    /// <inheritdoc />
    public Task<(ServiceResult Result, BudgetEditResponse? Edit)> CopyAsync(string accountEmail, BudgetKind kind, int year, string timeZoneIanaId, string source, string target, CancellationToken ct = default) =>
        WriteAsync(accountEmail, kind, year, timeZoneIanaId, [], workbook => workbook.Copy(kind, year, source, target),
            "paste into", ct);

    /// <inheritdoc />
    public async Task<(ServiceResult Result, BudgetSheetResponse? Sheet, int Copied)> CarryOverAsync(string accountEmail, BudgetKind kind, int year, string timeZoneIanaId, CancellationToken ct = default)
    {
        var (result, edit) = await WriteAsync(accountEmail, kind, year, timeZoneIanaId, [], workbook => workbook.CarryOver(kind, year),
            "carry over into", ct);
        if (!result.Success) return (result, null, 0);
        var (_, sheet) = await GetSheetAsync(accountEmail, kind, year, timeZoneIanaId, ct);
        return (result, sheet, edit!.Written);
    }

    /// <summary>
    /// One cell write: the sheet is stored first when it is not yet, then — in a transaction with the sheet row locked — the workbook is
    /// loaded, <paramref name="apply"/> turns the request into inputs to store (or refuses it), and they are stored. A failure is logged
    /// with <paramref name="action"/> (what was being done, e.g. "edit").
    /// </summary>
    private async Task<(ServiceResult Result, BudgetEditResponse? Edit)> WriteAsync(string accountEmail, BudgetKind kind, int year, string timeZoneIanaId,
        IReadOnlyList<string> typed, Func<BudgetWorkbook, ErrorOr<BudgetEdit>> apply, string action, CancellationToken ct)
    {
        if (Refusal(accountEmail, kind, year) is { } refused) return (refused, null);
        if (await EnsureSheetAsync(accountEmail, kind, year, ct) is { } notCreated) return (notCreated, null);

        await unitOfWork.BeginAsync(ct: ct);
        try
        {
            BudgetSheet sheet = (await sheetRepository.FindForUpdateAsync(accountEmail, kind, year, ct))!;
            BudgetWorkbook workbook = await LoadAsync(accountEmail, kind, year, timeZoneIanaId, typed, ct);
            ErrorOr<BudgetEdit> edit = apply(workbook);
            if (edit.IsError)
            {
                await unitOfWork.RollbackAsync(ct);
                return (ServiceResult.FromError(edit.FirstError), null);
            }

            await SaveAsync(sheet, edit.Value.Writes, ct);
            await unitOfWork.CommitAsync(ct);
            return (ServiceResult.Ok(), new BudgetEditResponse { Changed = [.. edit.Value.Changed.Select(BudgetMapper.ToResponse)], Written = edit.Value.Writes.Count });
        }
        catch (Exception e)
        {
            logger.LogError(e, "Budget: failed to {Action} {Sheet} of {Email}", action, BudgetSheetName.Format(kind, year), accountEmail);
            await unitOfWork.RollbackAsync(ct);
            return (Temporary(), null);
        }
    }

    /// <summary>Stores the inputs of an edit: a new input is added, a changed one updated, a cleared one deleted; the sheet is stamped.</summary>
    private async Task SaveAsync(BudgetSheet sheet, IReadOnlyList<BudgetCellWrite> writes, CancellationToken ct)
    {
        foreach (BudgetCellWrite write in writes)
        {
            BudgetSheetCell? existing = await cellRepository.FindAsync(sheet.Id, write.RowKey, write.Column, ct);
            if (write.Input is null)
            {
                if (existing is not null) await cellRepository.DeleteAsync(sheet.Id, write.RowKey, write.Column, ct);
            }
            else if (existing is not null)
            {
                existing.ChangeInput(write.Input, UtcNow);
                await cellRepository.UpdateEntityAsync(existing, ct);
            }
            else
            {
                await cellRepository.CreateAsync(BudgetSheetCell.Create(sheet.Id, write.RowKey, write.Column, write.Input, UtcNow), ct);
            }
        }

        sheet.Touch(UtcNow);
        await sheetRepository.UpdateEntityAsync(sheet, ct);
    }

    /// <summary>
    /// Stores the sheet with its default lines when it is not stored yet, in a transaction that locks the account row (a sheet another
    /// request stored meanwhile is used as it is). Returns the failure, or null.
    /// </summary>
    private async Task<ServiceResult?> EnsureSheetAsync(string accountEmail, BudgetKind kind, int year, CancellationToken ct)
    {
        if (await IsStoredAsync(accountEmail, kind, year, ct)) return null;

        await unitOfWork.BeginAsync(ct: ct);
        try
        {
            await accountRepository.FindByEmailForUpdateAsync(accountEmail, ct);
            if (await IsStoredAsync(accountEmail, kind, year, ct))
            {
                await unitOfWork.RollbackAsync(ct);
                return null;
            }

            BudgetSheet sheet = BudgetSheet.Create(accountEmail, kind, year, UtcNow).Value;
            await sheetRepository.CreateWithLinesAsync(sheet, BudgetLine.Defaults(kind, await DefaultCurrencyAsync(accountEmail, ct)), ct);
            await unitOfWork.CommitAsync(ct);
            return null;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Budget: failed to create {Sheet} of {Email}", BudgetSheetName.Format(kind, year), accountEmail);
            await unitOfWork.RollbackAsync(ct);
            return Temporary();
        }
    }

    /// <summary>True when the account has stored the sheet.</summary>
    private async Task<bool> IsStoredAsync(string accountEmail, BudgetKind kind, int year, CancellationToken ct) =>
        (await sheetRepository.GetByAccountEmailAsync(accountEmail, ct)).Any(s => s.Kind == kind && s.Year == year);

    /// <inheritdoc />
    public async Task<(ServiceResult Result, BudgetSheetResponse? Sheet)> AddLineAsync(string accountEmail, BudgetKind kind, int year, string timeZoneIanaId, string? mainClass, string? subClass, string? currency, CancellationToken ct = default)
    {
        if (Refusal(accountEmail, kind, year) is { } refused) return (refused, null);
        if (await EnsureSheetAsync(accountEmail, kind, year, ct) is { } notCreated) return (notCreated, null);

        ServiceResult result = await ChangeLinesAsync(accountEmail, kind, year, async (sheet, lines) =>
        {
            ErrorOr<BudgetLine> line = BudgetLine.Create(kind, mainClass, subClass, currency);
            if (line.IsError) return ServiceResult.FromError(line.FirstError);
            if (lines.Contains(line.Value)) return ServiceResult.Conflict("Budget.LineExists", ServiceErrorKeys.BudgetLineExists);
            await lineRepository.CreateAsync(BudgetSheetLine.Create(sheet.Id, line.Value, UtcNow), ct);
            return ServiceResult.Ok();
        }, ct);
        return result.Success ? await GetSheetAsync(accountEmail, kind, year, timeZoneIanaId, ct) : (result, null);
    }

    /// <inheritdoc />
    public async Task<(ServiceResult Result, BudgetSheetResponse? Sheet)> RemoveLineAsync(string accountEmail, BudgetKind kind, int year, string timeZoneIanaId, string? mainClass, string? subClass, string? currency, CancellationToken ct = default)
    {
        if (Refusal(accountEmail, kind, year) is { } refused) return (refused, null);

        ServiceResult result = await ChangeLinesAsync(accountEmail, kind, year, async (sheet, lines) =>
        {
            ErrorOr<BudgetLine> line = BudgetLine.Create(kind, mainClass, subClass, currency);
            if (line.IsError || !lines.Contains(line.Value)) return LineNotFound();
            await lineRepository.DeleteAsync(sheet.Id, line.Value, ct);
            await cellRepository.DeleteRowAsync(sheet.Id, line.Value.Key, ct);
            return ServiceResult.Ok();
        }, ct);
        return result.Success ? await GetSheetAsync(accountEmail, kind, year, timeZoneIanaId, ct) : (result, null);
    }

    /// <summary>The refusal of a line the sheet does not have.</summary>
    private static ServiceResult LineNotFound() => ServiceResult.NotFound("Budget.LineNotFound", ServiceErrorKeys.BudgetLineNotFound);

    /// <summary>
    /// Changes a sheet's lines in a transaction with the sheet row locked: <paramref name="change"/> gets the sheet and its lines and
    /// returns its outcome (a refusal rolls back). A sheet that is not stored has no line to change.
    /// </summary>
    private async Task<ServiceResult> ChangeLinesAsync(string accountEmail, BudgetKind kind, int year,
        Func<BudgetSheet, IReadOnlyList<BudgetLine>, Task<ServiceResult>> change, CancellationToken ct)
    {
        await unitOfWork.BeginAsync(ct: ct);
        try
        {
            BudgetSheet? sheet = await sheetRepository.FindForUpdateAsync(accountEmail, kind, year, ct);
            if (sheet is null)
            {
                await unitOfWork.RollbackAsync(ct);
                return LineNotFound();
            }

            List<BudgetLine> lines = [.. (await lineRepository.GetBySheetIdsAsync([sheet.Id], ct)).Select(l => l.Line)];
            ServiceResult result = await change(sheet, lines);
            if (!result.Success)
            {
                await unitOfWork.RollbackAsync(ct);
                return result;
            }

            sheet.Touch(UtcNow);
            await sheetRepository.UpdateEntityAsync(sheet, ct);
            await unitOfWork.CommitAsync(ct);
            return result;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Budget: failed to change the lines of {Sheet} of {Email}", BudgetSheetName.Format(kind, year), accountEmail);
            await unitOfWork.RollbackAsync(ct);
            return Temporary();
        }
    }

    /// <summary>The currency a new sheet's lines get: the account's default monetary unit as the dashboard resolves it (null without assets).</summary>
    private async Task<string?> DefaultCurrencyAsync(string accountEmail, CancellationToken ct)
    {
        Account? account = await accountRepository.FindByEmailAsync(accountEmail, ct);
        List<Asset> assets = await assetRepository.GetByAccountEmailAsync(accountEmail, ct);
        return DefaultMonetaryUnitPolicy.Resolve(account?.DefaultMonetaryUnit, assets)?.Value;
    }

    /// <summary>
    /// The account's workbook for the sheet asked for: every stored sheet, the sheet itself and last year's, and every sheet a stored
    /// input or <paramref name="typed"/> names (at most <see cref="MaxSheets"/>), with the actual records of their years.
    /// </summary>
    private async Task<BudgetWorkbook> LoadAsync(string accountEmail, BudgetKind kind, int year, string timeZoneIanaId, IReadOnlyList<string> typed, CancellationToken ct)
    {
        List<BudgetSheet> stored = await sheetRepository.GetByAccountEmailAsync(accountEmail, ct);
        List<long> ids = [.. stored.Select(s => s.Id)];
        List<BudgetSheetLine> lines = ids.Count == 0 ? [] : await lineRepository.GetBySheetIdsAsync(ids, ct);
        List<BudgetSheetCell> cells = ids.Count == 0 ? [] : await cellRepository.GetBySheetIdsAsync(ids, ct);

        List<(BudgetKind Kind, int Year)> wanted = [(kind, year), (kind, year - 1), .. stored.Select(s => (s.Kind, s.Year))];
        foreach (BudgetSheet sheet in stored)
            foreach (BudgetSheetCell cell in cells.Where(c => c.SheetId == sheet.Id))
                wanted.AddRange(BudgetWorkbook.SheetsReferencedBy(cell.Input, sheet.Year));
        foreach (string input in typed)
            wanted.AddRange(BudgetWorkbook.SheetsReferencedBy(input, year));

        string? currency = await DefaultCurrencyAsync(accountEmail, ct);
        List<BudgetSheetData> data = [];
        foreach (var (k, y) in wanted.Where(w => w.Year is >= BudgetSheet.MinYear and <= BudgetSheet.MaxYear).Distinct().Take(MaxSheets))
        {
            BudgetSheet? sheet = stored.FirstOrDefault(s => s.Kind == k && s.Year == y);
            data.Add(sheet is null
                ? new BudgetSheetData(k, y, false, BudgetLine.Defaults(k, currency), new Dictionary<BudgetCellKey, string>())
                : new BudgetSheetData(k, y, true, [.. lines.Where(l => l.SheetId == sheet.Id).Select(l => l.Line)],
                    cells.Where(c => c.SheetId == sheet.Id).ToDictionary(c => new BudgetCellKey(c.RowKey, c.Column), c => c.Input)));
        }

        List<Income> incomes = [];
        List<Expenditure> expenditures = [];
        foreach (int y in data.Select(d => d.Year).Distinct())
        {
            DateTime from = new DateTime(y, 1, 1).ConvertToUtcByTimeZoneIanaId(timeZoneIanaId);
            DateTime to = y == BudgetSheet.MaxYear ? DateTime.MaxValue : new DateTime(y + 1, 1, 1).ConvertToUtcByTimeZoneIanaId(timeZoneIanaId);
            incomes.AddRange(await incomeRepository.GetByAccountEmailAndDateRangeAsync(accountEmail, from, to, ct));
            expenditures.AddRange(await expenditureRepository.GetByAccountEmailAndDateRangeAsync(accountEmail, from, to, ct));
        }

        return new BudgetWorkbook(data, BudgetActuals.From(incomes, expenditures, timeZoneIanaId));
    }
}
