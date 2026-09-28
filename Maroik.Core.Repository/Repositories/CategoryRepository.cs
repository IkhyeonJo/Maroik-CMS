using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Menu;
using Maroik.Core.PostgreSQL.Data;
using Microsoft.EntityFrameworkCore;
using OrmCategory = Maroik.Core.PostgreSQL.Models.Category;

namespace Maroik.Core.Repository.Repositories;

/// <summary>
/// EF Core repository for <see cref="Category"/> domain objects,
/// representing top-level navigation menu categories.
/// </summary>
public class CategoryRepository(ApplicationDbContext context)
    : GenericRepository<Category, OrmCategory>(context), ICategoryRepository
{
    /// <summary>The underlying <see cref="DbSet{TEntity}"/> for <see cref="OrmCategory"/> rows.</summary>
    protected override DbSet<OrmCategory> Set => Context.Categories;

    /// <summary>Maps a persisted <see cref="OrmCategory"/> row to the <see cref="Category"/> domain object.</summary>
    protected override Category ToDomain(OrmCategory e)
        => Category.Reconstitute(e.Id, e.Name, e.DisplayName, e.IconPath, e.Controller, e.Action, e.Role, e.Order);

    /// <summary>Maps a <see cref="Category"/> domain object to its <see cref="OrmCategory"/> persistence representation.</summary>
    protected override OrmCategory ToEntity(Category c) => new()
    {
        Id = c.Id,
        Name = c.Name ?? "",
        DisplayName = c.DisplayName ?? "",
        IconPath = c.IconPath ?? "",
        Controller = c.Controller ?? "",
        Action = c.Action,
        Role = c.Role ?? "Admin",
        Order = c.Order
    };

    /// <inheritdoc />
    public Task<List<Category>> GetAllAsync(CancellationToken ct = default)
        => QueryAllAsync(ct: ct);

    /// <inheritdoc />
    public Task<Category?> FindByIdAsync(long id, CancellationToken ct = default)
        => QueryFirstAsync(e => e.Id == id, ct: ct);

    /// <inheritdoc />
    public async Task<Category?> FindByIdForUpdateAsync(long id, CancellationToken ct = default)
    {
        // Raw SQL is required because EF Core LINQ cannot emit FOR UPDATE. AsNoTracking so a
        // pre-lock instance already tracked in this context can't snap back and defeat the lock.
        var entities = await Set.FromSqlInterpolated(
            $"""
             SELECT *
             FROM "Category"
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
