using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Kanbans;
using Maroik.Core.PostgreSQL.Contexts;
using Microsoft.EntityFrameworkCore;
using OrmKanbanCard = Maroik.Core.PostgreSQL.Models.KanbanCard;

namespace Maroik.Core.Repository.Repositories;

/// <summary>
/// EF Core repository for <see cref="KanbanCard"/> domain objects. Every query and delete is filtered by the owning account's
/// email as well as the id, so a card of another account is neither found nor deleted (K2).
/// </summary>
public class KanbanCardRepository(ApplicationDbContext context)
    : GenericRepository<KanbanCard, OrmKanbanCard>(context), IKanbanCardRepository
{
    /// <summary>The underlying <see cref="DbSet{TEntity}"/> for <see cref="OrmKanbanCard"/> rows.</summary>
    protected override DbSet<OrmKanbanCard> Set => Context.KanbanCards;

    /// <summary>Maps a persisted <see cref="OrmKanbanCard"/> row to the <see cref="KanbanCard"/> domain object.</summary>
    protected override KanbanCard ToDomain(OrmKanbanCard e) => KanbanCard.Reconstitute(
        e.Id, e.AccountEmail, e.Lane, e.Title, e.Description, e.DueDate, e.Created, e.Updated);

    /// <summary>Maps a <see cref="KanbanCard"/> domain object to its <see cref="OrmKanbanCard"/> persistence representation.</summary>
    protected override OrmKanbanCard ToEntity(KanbanCard card) => new()
    {
        Id = card.Id,
        AccountEmail = card.AccountEmail.Value,
        Lane = card.Lane,
        Title = card.Title,
        Description = card.Description,
        DueDate = card.DueDate,
        Created = card.Created,
        Updated = card.Updated
    };

    /// <inheritdoc />
    public Task<List<KanbanCard>> GetByAccountEmailAsync(string accountEmail, CancellationToken ct = default)
        => QueryAsync(e => e.AccountEmail == accountEmail, noTracking: true, ct: ct);

    /// <inheritdoc />
    public Task<KanbanCard?> FindByEmailAndIdAsync(string accountEmail, long id, CancellationToken ct = default)
        => QueryFirstAsync(e => e.AccountEmail == accountEmail && e.Id == id, ct: ct);

    /// <inheritdoc />
    public Task DeleteByEmailAndIdAsync(string accountEmail, long id, CancellationToken ct = default)
        => DeleteWhereAsync(e => e.AccountEmail == accountEmail && e.Id == id, ct);
}
