namespace Maroik.Website.Models.ViewModels.AccountBooks;

/// <summary>A problem of the formula being typed, with its localized message.</summary>
public sealed class BudgetProblemViewModel
{
    /// <summary>Its start index.</summary>
    public int Start { get; init; }

    /// <summary>Its length.</summary>
    public int Length { get; init; }

    /// <summary>The localized message.</summary>
    public string Message { get; init; } = "";

    /// <summary>True when the formula cannot be saved.</summary>
    public bool Blocking { get; init; }
}
