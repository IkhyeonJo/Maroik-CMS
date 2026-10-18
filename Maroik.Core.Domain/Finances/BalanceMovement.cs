using Maroik.Core.Domain.ValueObjects;

namespace Maroik.Core.Domain.Finances;

/// <summary>
/// One change an income / expenditure record makes to an asset's balance: <see cref="Amount"/> deposited into, or
/// withdrawn from, the asset named <see cref="AssetName"/>. A record lists its own movements
/// (<see cref="Income.BalanceMovements"/>, <see cref="Expenditure.BalanceMovements"/>); <see cref="Asset.Apply"/> carries one out.
/// </summary>
/// <param name="AssetName">Product name of the asset the movement applies to.</param>
/// <param name="Amount">Amount moved, in the record's currency.</param>
/// <param name="IsDeposit"><see langword="true"/> to deposit into the asset, <see langword="false"/> to withdraw from it.</param>
public readonly record struct BalanceMovement(string AssetName, Money Amount, bool IsDeposit)
{
    /// <summary>The movement that undoes this one: the same amount on the same asset, in the other direction.</summary>
    public BalanceMovement Reversed() => this with { IsDeposit = !IsDeposit };

    /// <summary>The movements that undo <paramref name="recorded"/> (deleting the record), in their order.</summary>
    public static IReadOnlyList<BalanceMovement> Undoing(IEnumerable<BalanceMovement> recorded) =>
        [.. recorded.Select(movement => movement.Reversed())];

    /// <summary>
    /// The movements that turn <paramref name="before"/> into <paramref name="after"/> (editing the record): first
    /// undo every earlier movement, then apply every new one.
    /// </summary>
    public static IReadOnlyList<BalanceMovement> Replacing(IEnumerable<BalanceMovement> before, IEnumerable<BalanceMovement> after) =>
        [.. Undoing(before), .. after];
}
