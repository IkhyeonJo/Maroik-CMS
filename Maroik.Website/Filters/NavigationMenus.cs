using Maroik.Core.Contract.Dtos;

namespace Maroik.Website.Filters;

/// <summary>
/// The seeded navigation menu (categories + sub-categories) split by role. Loaded once per request by
/// <see cref="AuthorizationFilter"/> — it is the input to the allow/deny decision — and handed to
/// <see cref="ViewBagPopulatorFilter"/> via <c>HttpContext.Items</c>, which publishes it to the ViewBag
/// for the layout. The two steps are separate filters because an authorization filter runs before the
/// controller instance exists, so it has no ViewBag to write to.
/// </summary>
internal sealed record NavigationMenus(
    List<CategoryResponse> AdminCategories,
    List<SubCategoryResponse> AdminSubCategories,
    List<CategoryResponse> UserCategories,
    List<SubCategoryResponse> UserSubCategories,
    List<CategoryResponse> AnonymousCategories,
    List<SubCategoryResponse> AnonymousSubCategories);
