using Maroik.Core.Contract.Dtos;

namespace Maroik.Website.Models.ViewModels.Layouts;

/// <summary>Result of matching the current controller/action against a role's Category/SubCategory lists.</summary>
public sealed record ActiveMenuMatch(bool IsMatch, CategoryResponse? Category, SubCategoryResponse? SubCategory);
