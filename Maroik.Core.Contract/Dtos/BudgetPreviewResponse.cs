namespace Maroik.Core.Contract.Dtos;

/// <summary>What the editor shows for an input being typed.</summary>
public sealed class BudgetPreviewResponse
{
    /// <summary>The coloured tokens.</summary>
    public List<BudgetTokenResponse> Tokens { get; init; } = [];

    /// <summary>The distinct references.</summary>
    public List<BudgetReferenceResponse> References { get; init; } = [];

    /// <summary>The problems.</summary>
    public List<BudgetProblemResponse> Problems { get; init; } = [];

    /// <summary>True when nothing blocks saving.</summary>
    public bool CanSave { get; init; }

    /// <summary>The value the formula would have, or null.</summary>
    public BudgetValueResponse? Value { get; init; }
}
