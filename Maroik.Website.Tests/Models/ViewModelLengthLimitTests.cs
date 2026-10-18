using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Maroik.Core.Domain.Accounts;
using Maroik.Core.Domain.Primitives;
using Maroik.Website.Models.ViewModels.Accounts;
using Maroik.Website.Models.ViewModels.Managements;
using ForumBoardInputViewModel = Maroik.Website.Models.ViewModels.Forums.BoardInputViewModel;
using ManagementBoardInputViewModel = Maroik.Website.Models.ViewModels.Managements.BoardInputViewModel;

namespace Maroik.Website.Tests.Models;

/// <summary>
/// Every <see cref="StringLengthAttribute"/> on a view model reads its bounds from the domain policy that owns them, so the
/// form-side check (a UX mirror; the domain re-validates) moves with the policy instead of keeping numbers of its own.
/// </summary>
public class ViewModelLengthLimitTests
{
    /// <summary>The expected (maximum, minimum) of each view-model property that carries a <see cref="StringLengthAttribute"/>.</summary>
    private static readonly Dictionary<(Type Type, string Property), (int Maximum, int Minimum)> _expected = new()
    {
        [(typeof(ForumBoardInputViewModel), nameof(ForumBoardInputViewModel.Title))] = (TitledContentPolicy.MaxTitleLength, 1),
        [(typeof(ManagementBoardInputViewModel), nameof(ManagementBoardInputViewModel.Title))] = (TitledContentPolicy.MaxTitleLength, 1),
        [(typeof(LoginInputViewModel), nameof(LoginInputViewModel.Password))] = (PasswordPolicy.MaxByteLength, PasswordPolicy.MinLength),
        [(typeof(LoginInputViewModel), nameof(LoginInputViewModel.Nickname))] = (ShortTextPolicy.MaxLength, 1),
        [(typeof(LoginOutputViewModel), nameof(LoginOutputViewModel.Password))] = (PasswordPolicy.MaxByteLength, PasswordPolicy.MinLength),
        [(typeof(LoginOutputViewModel), nameof(LoginOutputViewModel.ConfirmPassword))] = (PasswordPolicy.MaxByteLength, PasswordPolicy.MinLength),
        [(typeof(LoginOutputViewModel), nameof(LoginOutputViewModel.Nickname))] = (ShortTextPolicy.MaxLength, 1),
        // The current password is only compared with the stored hash, so it has no minimum (ProfileInputViewModel).
        [(typeof(ProfileInputViewModel), nameof(ProfileInputViewModel.Password))] = (PasswordPolicy.MaxByteLength, 0),
        [(typeof(ProfileInputViewModel), nameof(ProfileInputViewModel.NewPassword))] = (PasswordPolicy.MaxByteLength, PasswordPolicy.MinLength),
        [(typeof(ProfileOutputViewModel), nameof(ProfileOutputViewModel.Password))] = (PasswordPolicy.MaxByteLength, PasswordPolicy.MinLength),
        [(typeof(ProfileOutputViewModel), nameof(ProfileOutputViewModel.NewPassword))] = (PasswordPolicy.MaxByteLength, PasswordPolicy.MinLength),
        [(typeof(ProfileOutputViewModel), nameof(ProfileOutputViewModel.ConfirmNewPassword))] = (PasswordPolicy.MaxByteLength, PasswordPolicy.MinLength),
    };

    /// <summary>
    /// Each length-limited view-model property is in <see cref="_expected"/> with the policy's bounds, and nothing else carries a
    /// <see cref="StringLengthAttribute"/> — an output model the views only display (a post's detail) has no limit to state.
    /// </summary>
    [Fact]
    public void EveryStringLength_OnAViewModel_ReadsItsBoundsFromThePolicy()
    {
        var actual = typeof(LoginInputViewModel).Assembly.GetTypes()
            .Where(type => type.Namespace?.StartsWith("Maroik.Website.Models.ViewModels", StringComparison.Ordinal) == true)
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(property => (Type: type, Property: property, Limit: property.GetCustomAttribute<StringLengthAttribute>())))
            .Where(entry => entry.Limit != null)
            .ToDictionary(entry => (entry.Type, entry.Property.Name), entry => (entry.Limit!.MaximumLength, entry.Limit.MinimumLength));

        Assert.Equal(
            _expected.OrderBy(e => $"{e.Key.Type.FullName}.{e.Key.Property}", StringComparer.Ordinal).Select(e => $"{e.Key.Type.FullName}.{e.Key.Property} {e.Value}"),
            actual.OrderBy(e => $"{e.Key.Type.FullName}.{e.Key.Item2}", StringComparer.Ordinal).Select(e => $"{e.Key.Type.FullName}.{e.Key.Item2} {e.Value}"));
    }

    /// <summary>
    /// <see cref="StringLengthAttribute"/> reports a too-long and a too-short value with the same message, so the message states both
    /// bounds — a password past the ceiling is not told it "must be at least" the minimum.
    /// </summary>
    [Theory]
    [InlineData(PasswordPolicy.MaxByteLength + 1)]
    [InlineData(PasswordPolicy.MinLength - 1)]
    public void APasswordOutOfBounds_IsToldBothBounds(int length)
    {
        var model = new LoginOutputViewModel { Password = new string('a', length) };
        var results = new List<ValidationResult>();

        Validator.TryValidateProperty(model.Password, new ValidationContext(model) { MemberName = nameof(LoginOutputViewModel.Password) }, results);

        Assert.Contains(results, r => r.ErrorMessage == $"The Password must be between {PasswordPolicy.MinLength} and {PasswordPolicy.MaxByteLength} characters long.");
    }
}
