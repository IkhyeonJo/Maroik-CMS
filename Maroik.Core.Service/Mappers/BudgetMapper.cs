using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Budgets;
using Maroik.Core.Domain.Formulas;

namespace Maroik.Core.Service.Mappers;

/// <summary>Maps the budget domain's views (<see cref="BudgetSheetView"/>, <see cref="BudgetCellView"/>, <see cref="BudgetFormulaPreview"/>) to their DTOs.</summary>
public static class BudgetMapper
{
    /// <summary>A sheet.</summary>
    public static BudgetSheetResponse ToResponse(BudgetSheetView view) => new()
    {
        Kind = view.Kind.ToString(),
        Year = view.Year,
        Exists = view.Exists,
        Rows = [.. view.Rows.Select(r => new BudgetRowResponse { Number = r.Number, Kind = r.Kind.ToString(), MainClass = r.MainClass, SubClass = r.SubClass, Currency = r.Currency, EditableColumns = r.EditableColumns })],
        Cells = [.. view.Cells.Select(ToResponse)],
        Unbudgeted = [.. view.Unbudgeted.Select(u => new BudgetUnbudgetedResponse
            { MainClass = u.Line.MainClass, SubClass = u.Line.SubClass, Currency = u.Line.Currency, Months = [.. u.Months] })],
    };

    /// <summary>A cell.</summary>
    public static BudgetCellResponse ToResponse(BudgetCellView cell) => new()
    {
        Address = cell.Address,
        Role = cell.Role.ToString(),
        Input = cell.Input,
        Value = ToResponse(cell.Value),
        Budget = cell.Status?.Budget,
        Actual = cell.Status?.Actual,
        Remaining = cell.Status?.Remaining,
        State = cell.Status?.State.ToString(),
    };

    /// <summary>A value.</summary>
    public static BudgetValueResponse ToResponse(FormulaValue value) => value switch
    {
        NumberValue n => new BudgetValueResponse { Kind = "Number", Number = n.Value },
        TextValue t => new BudgetValueResponse { Kind = "Text", Text = t.Value },
        BooleanValue b => new BudgetValueResponse { Kind = "Boolean", Text = b.Value ? "TRUE" : "FALSE" },
        ErrorValue e => new BudgetValueResponse { Kind = "Error", Text = FormulaLexer.ErrorText(e.Kind) },
        _ => new BudgetValueResponse { Kind = "Empty" },
    };

    /// <summary>A preview: tokens, references, problems (as message keys and arguments) and the value.</summary>
    public static BudgetPreviewResponse ToResponse(BudgetFormulaPreview preview) => new()
    {
        Tokens = [.. preview.Highlight.Tokens.Select(t => new BudgetTokenResponse
            { Start = t.Span.Start, Length = t.Span.Length, Class = t.Class, Reference = t.Reference, Partner = t.Partner })],
        References = [.. preview.Highlight.References.Select(r => new BudgetReferenceResponse { Index = r.Index, TopLeft = r.TopLeft, BottomRight = r.BottomRight })],
        Problems = [.. preview.Highlight.Problems.Select(p =>
        {
            ServiceResult message = ServiceResult.FromError(p.Diagnostic.Error);
            return new BudgetProblemResponse { Start = p.Diagnostic.Span.Start, Length = p.Diagnostic.Span.Length, ErrorKey = message.ErrorKey, ErrorArgs = message.ErrorArgs, Blocking = p.Blocking };
        })],
        CanSave = preview.Highlight.CanSave,
        Value = preview.Value is null ? null : ToResponse(preview.Value),
    };
}
