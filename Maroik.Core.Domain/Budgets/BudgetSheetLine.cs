using Maroik.Core.Domain.Primitives;

namespace Maroik.Core.Domain.Budgets;

/// <summary>Aggregate root for one line of a budget sheet (persisted on its own; the sheet is referenced by id).</summary>
public sealed class BudgetSheetLine : AggregateRoot<long>
{
    /// <summary>The sheet's id.</summary>
    public long SheetId { get; }

    /// <summary>The line (classes and currency).</summary>
    public BudgetLine Line { get; }

    /// <summary>UTC timestamp when the line was added.</summary>
    public DateTime Created { get; }

    /// <summary>Assigns every field verbatim.</summary>
    private BudgetSheetLine(long id, long sheetId, BudgetLine line, DateTime created) : base(id)
    {
        SheetId = sheetId;
        Line = line;
        Created = created;
    }

    /// <summary>A new line row of a sheet (the line was validated by <see cref="BudgetLine.Create"/>).</summary>
    public static BudgetSheetLine Create(long sheetId, BudgetLine line, DateTime utcNow) => new(0, sheetId, line, utcNow);

    /// <summary>Rebuilds a line row from trusted repository data.</summary>
    public static BudgetSheetLine Reconstitute(long id, long sheetId, string mainClass, string subClass, string currency, DateTime created) =>
        new(id, sheetId, new BudgetLine(mainClass, subClass, currency), created);
}
