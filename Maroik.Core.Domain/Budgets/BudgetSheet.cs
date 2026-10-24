using ErrorOr;
using Maroik.Core.Domain.Errors;
using Maroik.Core.Domain.Primitives;
using Maroik.Core.Domain.ValueObjects;

namespace Maroik.Core.Domain.Budgets;

/// <summary>
/// Aggregate root for one budget sheet: an account's income or expenditure sheet of one year. Its lines and cells are rows of their
/// own (<see cref="BudgetSheetLine"/>, <see cref="BudgetSheetCell"/>); the sheet row is what concurrent edits lock.
/// </summary>
public sealed class BudgetSheet : AggregateRoot<long>
{
    /// <summary>The earliest year a sheet can have.</summary>
    public const int MinYear = 1;

    /// <summary>The latest year a sheet can have.</summary>
    public const int MaxYear = 9999;

    /// <summary>Email of the owning account.</summary>
    public Email AccountEmail { get; }

    /// <summary>Income or expenditure.</summary>
    public BudgetKind Kind { get; }

    /// <summary>The year.</summary>
    public int Year { get; }

    /// <summary>UTC timestamp when the sheet was created.</summary>
    public DateTime Created { get; }

    /// <summary>UTC timestamp of the most recent edit.</summary>
    public DateTime Updated { get; private set; }

    /// <summary>The name formulas use for the sheet (e.g. Income2026).</summary>
    public string Name => BudgetSheetName.Format(Kind, Year);

    /// <summary>Assigns every field verbatim.</summary>
    private BudgetSheet(long id, Email accountEmail, BudgetKind kind, int year, DateTime created, DateTime updated) : base(id)
    {
        AccountEmail = accountEmail;
        Kind = kind;
        Year = year;
        Created = created;
        Updated = updated;
    }

    /// <summary>A new sheet, refused for an unknown kind, a year outside <see cref="MinYear"/>..<see cref="MaxYear"/> or a bad e-mail.</summary>
    public static ErrorOr<BudgetSheet> Create(string? accountEmailValue, BudgetKind kind, int year, DateTime utcNow)
    {
        if (!Enum.IsDefined(kind))
            return DomainError.Validation("Budget.KindInvalid", "Unknown budget kind.");
        if (year is < MinYear or > MaxYear)
            return DomainError.Validation("Budget.YearInvalid", "The year must be between {0} and {1}.", MinYear, MaxYear);

        ErrorOr<Email> email = Email.Create(accountEmailValue);
        if (email.IsError) return email.Errors;

        return new BudgetSheet(0, email.Value, kind, year, utcNow, utcNow);
    }

    /// <summary>Rebuilds a sheet from trusted repository data.</summary>
    public static BudgetSheet Reconstitute(long id, string accountEmail, string kind, int year, DateTime created, DateTime updated) =>
        new(id, Email.FromTrustedSource(accountEmail), Enum.Parse<BudgetKind>(kind), year, created, updated);

    /// <summary>Stamps an edit of the sheet's lines or cells.</summary>
    public void Touch(DateTime utcNow) => Updated = utcNow;
}
