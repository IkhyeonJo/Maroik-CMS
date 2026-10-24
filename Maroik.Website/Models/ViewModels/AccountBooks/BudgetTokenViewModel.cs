namespace Maroik.Website.Models.ViewModels.AccountBooks;

/// <summary>A coloured token of the formula being typed.</summary>
public sealed class BudgetTokenViewModel
{
    /// <summary>Its start index.</summary>
    public int Start { get; init; }

    /// <summary>Its length.</summary>
    public int Length { get; init; }

    /// <summary>Its colour class.</summary>
    public string Class { get; init; } = "";

    /// <summary>Its reference colour index, or -1.</summary>
    public int Reference { get; init; } = -1;

    /// <summary>Its partner parenthesis's token index, or -1.</summary>
    public int Partner { get; init; } = -1;
}
