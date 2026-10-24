namespace Maroik.Core.Contract.Dtos;

/// <summary>A problem of a formula being typed: where it is, its message key and arguments, and whether it blocks saving.</summary>
public sealed class BudgetProblemResponse
{
    /// <summary>Its start index.</summary>
    public int Start { get; init; }

    /// <summary>Its length (0 = "something is missing here").</summary>
    public int Length { get; init; }

    /// <summary>The message template (the resx key).</summary>
    public string ErrorKey { get; init; } = "";

    /// <summary>The template's arguments.</summary>
    public object[] ErrorArgs { get; init; } = [];

    /// <summary>True when the formula cannot be saved.</summary>
    public bool Blocking { get; init; }
}
