namespace Maroik.Website.Models.ViewModels.AccountBooks;

/// <summary>The Income Budget / Expenditure Budget page: which sheet it opens and what its "add line" form offers.</summary>
public sealed class BudgetPageViewModel
{
    /// <summary>Income or Expenditure.</summary>
    public string Kind { get; init; } = "";

    /// <summary>The year the page opens on (the account's current year).</summary>
    public int Year { get; init; }

    /// <summary>The main classes of the kind, in taxonomy order.</summary>
    public IReadOnlyList<string> MainClasses { get; init; } = [];

    /// <summary>The sub-classes of each main class.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> SubClassesByMainClass { get; init; } = new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>The localized name of every main class and sub-class of the kind.</summary>
    public IReadOnlyDictionary<string, string> ClassLabels { get; init; } = new Dictionary<string, string>();

    /// <summary>The currency labels of the account's assets (the currency field's suggestions).</summary>
    public IReadOnlyList<string> Currencies { get; init; } = [];

    /// <summary>The localized caption of each column that has one (B..M the months, N the year total), by column letter.</summary>
    public IReadOnlyDictionary<string, string> ColumnCaptions { get; init; } = new Dictionary<string, string>();
}
