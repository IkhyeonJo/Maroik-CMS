using Maroik.Core.Contract.Dtos;
using Maroik.Website.Models.ViewModels.Shared;

namespace Maroik.Website.Mappings;

/// <summary>
/// Maps navigation Category/SubCategory DTOs into the sorted, active-flagged sidebar view model.
/// Match/active-flag predicates mirror <see cref="Maroik.Website.Extensions.MenuNavigationMatcher.ResolveActiveMenuItem"/>,
/// the single source of truth for the matching algorithm (used by <c>AuthorizationFilter</c> for
/// access gating), so this mapper's active-flags stay consistent with that gating decision.
/// </summary>
public static class MenuNavigationMapper
{
    extension(IEnumerable<CategoryResponse> categories)
    {
        /// <summary>
        /// Builds the full, sorted, active-flagged sidebar tree for rendering.
        /// </summary>
        public SidebarMenuViewModel ToSidebarMenu(
            IEnumerable<SubCategoryResponse> subCategories, string controllerName, string actionName)
        {
            var subCategoryList = subCategories.ToList();

            var items = categories
                .OrderBy(category => category.Order)
                .Select(category =>
                {
                    var isSingleCategory = !string.IsNullOrEmpty(category.Action);
                    if (isSingleCategory)
                    {
                        return new SidebarCategoryItem
                        {
                            Category = category,
                            IsActive = category.Controller == controllerName && category.Action == actionName,
                            SubCategories = []
                        };
                    }

                    var subItems = subCategoryList
                        .Where(subCategory => subCategory.CategoryId == category.Id)
                        .OrderBy(subCategory => subCategory.Order)
                        .Select(subCategory => new SidebarSubCategoryItem
                        {
                            SubCategory = subCategory,
                            IsActive = category.Controller == controllerName && subCategory.Action == actionName
                        })
                        .ToList();

                    return new SidebarCategoryItem
                    {
                        Category = category,
                        IsActive = subItems.Any(subItem => subItem.IsActive),
                        SubCategories = subItems
                    };
                })
                .ToList();

            return new SidebarMenuViewModel { Categories = items };
        }
    }
}
