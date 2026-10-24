namespace Maroik.Website.Models.ViewModels.AccountBooks;

/// <summary>The formula bar's colouring, outlines, problems and the value the input would have.</summary>
public sealed class BudgetPreviewViewModel
{
    /// <summary>The tokens.</summary>
    public List<BudgetTokenViewModel> Tokens { get; init; } = [];

    /// <summary>The references.</summary>
    public List<BudgetReferenceViewModel> References { get; init; } = [];

    /// <summary>The problems.</summary>
    public List<BudgetProblemViewModel> Problems { get; init; } = [];

    /// <summary>True when nothing blocks saving.</summary>
    public bool CanSave { get; init; }

    /// <summary>The value as shown, or empty.</summary>
    public string Value { get; init; } = "";
}
