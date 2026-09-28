using Maroik.Core.Contract.Dtos;
using Maroik.Website.Models.ViewModels.Shared;

namespace Maroik.Website.Extensions;

/// <summary>
/// Resolves which navigation Category/SubCategory matches the current controller/action.
/// Single source of truth for the matching algorithm previously duplicated across
/// <c>AuthorizationFilter</c>, <c>_MainLeftSideBar.cshtml</c> and <c>_BodyPageHeader.cshtml</c>.
/// </summary>
public static class MenuNavigationMatcher
{
    extension(IEnumerable<CategoryResponse> categories)
    {
        /// <summary>
        /// Finds the first Category (and, for multi-level categories, the matching SubCategory)
        /// whose Controller/Action matches the current route. Mirrors the same two-part predicate
        /// (single categories have a direct Action; multi-level categories match via their children)
        /// that <c>AuthorizationFilter</c> used to gate access, so allow/deny outcomes are unchanged.
        /// </summary>
        public ActiveMenuMatch ResolveActiveMenuItem(
            IEnumerable<SubCategoryResponse> subCategories, string controllerName, string actionName)
        {
            var subCategoryList = subCategories as IReadOnlyCollection<SubCategoryResponse> ?? [.. subCategories];

            foreach (var category in categories)
            {
                if (!string.IsNullOrEmpty(category.Action))
                {
                    if (category.Controller == controllerName && category.Action == actionName)
                    {
                        return new ActiveMenuMatch(true, category, null);
                    }
                }
                else
                {
                    var matchedSubCategory = subCategoryList.FirstOrDefault(subCategory =>
                        subCategory.CategoryId == category.Id &&
                        category.Controller == controllerName &&
                        subCategory.Action == actionName);

                    if (matchedSubCategory != null)
                    {
                        return new ActiveMenuMatch(true, category, matchedSubCategory);
                    }
                }
            }

            return new ActiveMenuMatch(false, null, null);
        }
    }
}
