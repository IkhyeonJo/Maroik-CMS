using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Menu;
using Maroik.Core.PostgreSQL.Data;
using Microsoft.EntityFrameworkCore;
using OrmSubCategory = Maroik.Core.PostgreSQL.Models.SubCategory;

namespace Maroik.Core.Repository.Repositories;

/// <summary>
/// EF Core repository for <see cref="SubCategory"/> domain objects,
/// representing child navigation menu items under a <see cref="Category"/>.
/// </summary>
public class SubCategoryRepository(ApplicationDbContext context)
    : GenericRepository<SubCategory, OrmSubCategory>(context), ISubCategoryRepository
{
    /// <summary>The underlying <see cref="DbSet{TEntity}"/> for <see cref="OrmSubCategory"/> rows.</summary>
    protected override DbSet<OrmSubCategory> Set => Context.SubCategories;

    /// <summary>Maps a persisted <see cref="OrmSubCategory"/> row to the <see cref="SubCategory"/> domain object.</summary>
    protected override SubCategory ToDomain(OrmSubCategory e)
        => SubCategory.Reconstitute(e.Id, e.CategoryId, e.Name, e.DisplayName, e.IconPath, e.Action, e.Role, e.Order);

    /// <summary>Maps a <see cref="SubCategory"/> domain object to its <see cref="OrmSubCategory"/> persistence representation.</summary>
    protected override OrmSubCategory ToEntity(SubCategory s) => new()
    {
        Id = s.Id,
        CategoryId = s.CategoryId,
        Name = s.Name ?? "",
        DisplayName = s.DisplayName ?? "",
        IconPath = s.IconPath ?? "",
        Action = s.Action ?? "",
        Role = s.Role ?? "Admin",
        Order = s.Order
    };

    /// <inheritdoc />
    public Task<List<SubCategory>> GetAllAsync(CancellationToken ct = default)
        => QueryAllAsync(ct: ct);

    /// <inheritdoc />
    public Task<SubCategory?> FindByIdAsync(long id, CancellationToken ct = default)
        => QueryFirstAsync(e => e.Id == id, ct: ct);

    /// <inheritdoc />
    public async Task<SubCategory?> FindByIdForUpdateAsync(long id, CancellationToken ct = default)
    {
        // Raw SQL is required because EF Core LINQ cannot emit FOR UPDATE. AsNoTracking so a
        // pre-lock instance already tracked in this context can't snap back and defeat the lock.
        var entities = await Set.FromSqlInterpolated(
            $"""
             SELECT *
             FROM "SubCategory"
             WHERE "Id" = {id}
             LIMIT 1
             FOR UPDATE
             """).AsNoTracking().ToListAsync(ct);

        var entity = entities.FirstOrDefault();
        return entity == null ? null : ToDomain(entity);
    }

    /// <inheritdoc />
    public Task DeleteByIdAsync(long id, CancellationToken ct = default)
        => DeleteWhereAsync(e => e.Id == id, ct);
}
