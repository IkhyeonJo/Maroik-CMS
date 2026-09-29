using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Menu;
using Maroik.Core.Service.Extensions;
using Maroik.Core.Service.Mappers;
using Microsoft.Extensions.Logging;

namespace Maroik.Core.Service.Services;

/// <summary>
/// Implementation of <see cref="IMenuService"/> that delegates navigation menu management
/// to <see cref="ICategoryRepository"/> and <see cref="ISubCategoryRepository"/>.
/// All write operations turn a caught exception into a <see cref="ServiceResult.Failure"/>, and the
/// updates turn a missing row into a <see cref="ServiceResult.NotFound"/> (a delete of a missing row is
/// a no-op success), so the controller can surface a
/// user-friendly error (and react to its kind) without exposing internal details.
/// </summary>
public class MenuService(
    ICategoryRepository categoryRepository,
    ISubCategoryRepository subCategoryRepository,
    IUnitOfWork unitOfWork,
    ILogger<MenuService> logger) : IMenuService
{
    /// <inheritdoc />
    public async Task<IEnumerable<CategoryResponse>> GetAllCategoriesAsync(CancellationToken ct = default)
        => (await categoryRepository.GetAllAsync(ct)).Select(CategoryMapper.ToResponse);

    /// <inheritdoc />
    public async Task<IEnumerable<SubCategoryResponse>> GetAllSubCategoriesAsync(CancellationToken ct = default)
        => (await subCategoryRepository.GetAllAsync(ct)).Select(SubCategoryMapper.ToResponse);

    /// <inheritdoc />
    public async Task<IEnumerable<CategoryResponse>> SearchCategoriesAsync(string? search, CancellationToken ct = default)
    {
        IEnumerable<CategoryResponse> categories = await GetAllCategoriesAsync(ct);
        return string.IsNullOrEmpty(search) ? categories : categories.Where(item => Matches(item, search));
    }

    /// <inheritdoc />
    public async Task<IEnumerable<SubCategoryResponse>> SearchSubCategoriesAsync(string? search, CancellationToken ct = default)
    {
        IEnumerable<SubCategoryResponse> subCategories = await GetAllSubCategoriesAsync(ct);
        return string.IsNullOrEmpty(search) ? subCategories : subCategories.Where(item => Matches(item, search));
    }

    /// <summary>Whole-search predicate for a category row — mirrors the sub-category overload below.</summary>
    private static bool Matches(CategoryResponse item, string search) =>
        item.Id.ToString().Contains(search) ||
        (item.Name ?? "").Contains(search) ||
        (item.DisplayName ?? "").Contains(search) ||
        (item.IconPath ?? "").Contains(search) ||
        (item.Controller ?? "").Contains(search) ||
        (item.Action ?? "").Contains(search) ||
        (item.Role ?? "").Contains(search) ||
        item.Order.ToString().Contains(search);

    /// <summary>Whole-search predicate for a sub-category row — mirrors the category overload above.</summary>
    private static bool Matches(SubCategoryResponse item, string search) =>
        item.Id.ToString().Contains(search) ||
        item.CategoryId.ToString().Contains(search) ||
        (item.Name ?? "").Contains(search) ||
        (item.DisplayName ?? "").Contains(search) ||
        (item.IconPath ?? "").Contains(search) ||
        (item.Action ?? "").Contains(search) ||
        (item.Role ?? "").Contains(search) ||
        item.Order.ToString().Contains(search);

    /// <inheritdoc />
    public async Task<CategoryResponse?> GetCategoryByIdAsync(int id, CancellationToken ct = default)
    {
        Category? category = await categoryRepository.FindByIdAsync(id, ct);
        return category == null ? null : CategoryMapper.ToResponse(category);
    }

    /// <inheritdoc />
    public async Task<SubCategoryResponse?> GetSubCategoryByIdAsync(int id, CancellationToken ct = default)
    {
        SubCategory? sub = await subCategoryRepository.FindByIdAsync(id, ct);
        return sub == null ? null : SubCategoryMapper.ToResponse(sub);
    }

    /// <inheritdoc />
    public async Task<ServiceResult> CreateCategoryAsync(CategoryRequest request, string actorEmail, CancellationToken ct = default)
    {
        try
        {
            var result = Category.Create(request.Name, request.DisplayName, request.IconPath,
                request.Controller, request.Action, request.Role, request.Order);
            if (result.IsError)
                return ServiceResult.FromError(result.FirstError);

            await categoryRepository.CreateAsync(result.Value, ct);
            logger.LogInformation("Menu category {Name} created (role {Role}) by admin {Admin}", request.Name, request.Role, actorEmail);
            return ServiceResult.Ok();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to create category {Name}", request.Name);
            return ServiceResult.Failure("Menu.CreateCategoryFailed", "Input is invalid");
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> UpdateCategoryAsync(CategoryRequest request, string actorEmail, CancellationToken ct = default)
    {
        await unitOfWork.BeginAsync(ct);
        try
        {
            // FOR UPDATE, inside this explicit transaction: without BeginAsync/CommitAsync around
            // it, the lock would be released the instant this SELECT completes (its own implicit,
            // auto-committed transaction) and buy nothing against a concurrent update racing in
            // between this read and the write below.
            Category? category = await categoryRepository.FindByIdForUpdateAsync(request.Id, ct);
            if (category == null)
                return await unitOfWork.FailAsync(ServiceResult.NotFound("Category.NotFound", "Input is invalid"), ct);

            var updateResult = category.Update(request.Name, request.DisplayName, request.IconPath,
                request.Controller, request.Action, request.Role, request.Order);
            if (updateResult.IsError)
                return await unitOfWork.FailAsync(updateResult.FirstError, ct);

            await categoryRepository.UpdateEntityAsync(category, ct);
            await unitOfWork.CommitAsync(ct);
            logger.LogInformation("Menu category {CategoryId} ({Name}) updated (role {Role}) by admin {Admin}", request.Id, request.Name, request.Role, actorEmail);
            return ServiceResult.Ok();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to update category {Name}", request.Name);
            await unitOfWork.RollbackAsync(ct);
            return ServiceResult.Failure("Menu.UpdateCategoryFailed", "Input is invalid");
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> DeleteCategoryAsync(CategoryRequest request, string actorEmail, CancellationToken ct = default)
    {
        try
        {
            await categoryRepository.DeleteByIdAsync(request.Id, ct);
            logger.LogInformation("Menu category {CategoryId} ({Name}) deleted by admin {Admin}", request.Id, request.Name, actorEmail);
            return ServiceResult.Ok();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to delete category {Name}", request.Name);
            return ServiceResult.Failure("Menu.DeleteCategoryFailed", "Input is invalid");
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> CreateSubCategoryAsync(SubCategoryRequest request, string actorEmail, CancellationToken ct = default)
    {
        try
        {
            var result = SubCategory.Create(request.CategoryId, request.Name, request.DisplayName,
                request.IconPath, request.Action, request.Role, request.Order);
            if (result.IsError)
                return ServiceResult.FromError(result.FirstError);

            await subCategoryRepository.CreateAsync(result.Value, ct);
            logger.LogInformation("Menu sub-category {Name} created (role {Role}) by admin {Admin}", request.Name, request.Role, actorEmail);
            return ServiceResult.Ok();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to create sub-category {Name}", request.Name);
            return ServiceResult.Failure("Menu.CreateSubCategoryFailed", "Input is invalid");
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> UpdateSubCategoryAsync(SubCategoryRequest request, string actorEmail, CancellationToken ct = default)
    {
        await unitOfWork.BeginAsync(ct);
        try
        {
            // FOR UPDATE, inside this explicit transaction — see UpdateCategoryAsync for why the
            // transaction wrapper is required for the lock to mean anything.
            SubCategory? subCategory = await subCategoryRepository.FindByIdForUpdateAsync(request.Id, ct);
            if (subCategory == null)
                return await unitOfWork.FailAsync(ServiceResult.NotFound("SubCategory.NotFound", "Input is invalid"), ct);

            var updateResult = subCategory.Update(request.CategoryId, request.Name,
                request.DisplayName, request.IconPath, request.Action, request.Role, request.Order);
            if (updateResult.IsError)
                return await unitOfWork.FailAsync(updateResult.FirstError, ct);

            await subCategoryRepository.UpdateEntityAsync(subCategory, ct);
            await unitOfWork.CommitAsync(ct);
            logger.LogInformation("Menu sub-category {SubCategoryId} ({Name}) updated (role {Role}) by admin {Admin}", request.Id, request.Name, request.Role, actorEmail);
            return ServiceResult.Ok();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to update sub-category {Name}", request.Name);
            await unitOfWork.RollbackAsync(ct);
            return ServiceResult.Failure("Menu.UpdateSubCategoryFailed", "Input is invalid");
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> DeleteSubCategoryAsync(SubCategoryRequest request, string actorEmail, CancellationToken ct = default)
    {
        try
        {
            await subCategoryRepository.DeleteByIdAsync(request.Id, ct);
            logger.LogInformation("Menu sub-category {SubCategoryId} ({Name}) deleted by admin {Admin}", request.Id, request.Name, actorEmail);
            return ServiceResult.Ok();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to delete sub-category {Name}", request.Name);
            return ServiceResult.Failure("Menu.DeleteSubCategoryFailed", "Input is invalid");
        }
    }
}
