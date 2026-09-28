using Maroik.Core.Contract.Dtos;

namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Service interface for managing the sidebar navigation menu (categories and sub-categories).
/// Used by the admin Management > Menu page. The write methods take <c>actorEmail</c>, the signed-in
/// administrator making the change, so the audit log records who changed the menu.
/// </summary>
public interface IMenuService
{
    /// <summary>Returns all top-level navigation categories.</summary>
    Task<IEnumerable<CategoryResponse>> GetAllCategoriesAsync(CancellationToken ct = default);

    /// <summary>Returns all sub-category (dropdown menu item) rows.</summary>
    Task<IEnumerable<SubCategoryResponse>> GetAllSubCategoriesAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns the top-level navigation categories whose ID/Name/DisplayName/IconPath/Controller/
    /// Action/Role/Order contains <paramref name="search"/> (ordinal substring match), or every
    /// category when <paramref name="search"/> is null or empty. Backs the Management &gt; Menu
    /// grid's "whole search" box.
    /// </summary>
    Task<IEnumerable<CategoryResponse>> SearchCategoriesAsync(string? search, CancellationToken ct = default);

    /// <summary>
    /// Returns the sub-category rows whose ID/CategoryId/Name/DisplayName/IconPath/Action/Role/Order
    /// contains <paramref name="search"/> (ordinal substring match), or every sub-category when
    /// <paramref name="search"/> is null or empty. Backs the Management &gt; Menu grid's "whole
    /// search" box.
    /// </summary>
    Task<IEnumerable<SubCategoryResponse>> SearchSubCategoriesAsync(string? search, CancellationToken ct = default);

    /// <summary>Returns a single category by its ID, or null.</summary>
    Task<CategoryResponse?> GetCategoryByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Returns a single sub-category by its ID, or null.</summary>
    Task<SubCategoryResponse?> GetSubCategoryByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Creates a new top-level navigation category.</summary>
    Task<ServiceResult> CreateCategoryAsync(CategoryRequest request, string actorEmail, CancellationToken ct = default);

    /// <summary>Updates an existing navigation category.</summary>
    Task<ServiceResult> UpdateCategoryAsync(CategoryRequest request, string actorEmail, CancellationToken ct = default);

    /// <summary>Deletes a navigation category and its sub-categories.</summary>
    Task<ServiceResult> DeleteCategoryAsync(CategoryRequest request, string actorEmail, CancellationToken ct = default);

    /// <summary>Creates a new sub-category under an existing category.</summary>
    Task<ServiceResult> CreateSubCategoryAsync(SubCategoryRequest request, string actorEmail, CancellationToken ct = default);

    /// <summary>Updates an existing sub-category.</summary>
    Task<ServiceResult> UpdateSubCategoryAsync(SubCategoryRequest request, string actorEmail, CancellationToken ct = default);

    /// <summary>Deletes a sub-category.</summary>
    Task<ServiceResult> DeleteSubCategoryAsync(SubCategoryRequest request, string actorEmail, CancellationToken ct = default);
}
