using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Account;
using Maroik.Core.Domain.Board;
using Maroik.Core.PostgreSQL.Data;
using Microsoft.EntityFrameworkCore;
using OrmBoard = Maroik.Core.PostgreSQL.Models.Board;

namespace Maroik.Core.Repository.Repositories;

/// <summary>
/// EF Core repository for <see cref="Board"/> domain objects.
/// Provides board-specific operations such as incrementing the view counter and returning the generated ID on write.
/// </summary>
public class BoardRepository(ApplicationDbContext context)
    : GenericRepository<Board, OrmBoard>(context), IBoardRepository
{
    /// <summary>The underlying <see cref="DbSet{TEntity}"/> for <see cref="OrmBoard"/> rows.</summary>
    protected override DbSet<OrmBoard> Set => Context.Boards;

    /// <inheritdoc />
    public async Task IncrementViewAsync(long id, CancellationToken ct = default)
        => await Context.Boards
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.View, b => b.View + 1), ct);

    /// <inheritdoc />
    public async Task<long> WriteBoardAsync(Board board, CancellationToken ct = default)
    {
        OrmBoard entity = ToEntity(board);
        _ = await Context.Boards.AddAsync(entity, ct);
        // Unlike the deferred CreateAsync, this flush is unconditional: the caller needs the
        // database-generated ID immediately (to build the attachment's storage path within the
        // same unit of work). Inside a transaction the INSERT is still uncommitted until commit.
        _ = await Context.SaveChangesAsync(ct);
        return entity.Id;
    }

    /// <summary>Maps a persisted <see cref="OrmBoard"/> row to the <see cref="Board"/> domain object.</summary>
    protected override Board ToDomain(OrmBoard e) => Board.Reconstitute(
        e.Id, e.Type, e.Title, e.Content,
        e.Writer, e.Created, e.Updated, e.View, e.Deleted, e.Locked, e.Noticed);

    /// <summary>Maps a <see cref="Board"/> domain object to its <see cref="OrmBoard"/> persistence representation.</summary>
    protected override OrmBoard ToEntity(Board b) => new()
    {
        Id = b.Id,
        Type = b.Type,
        Title = b.Title,
        Content = b.Content ?? "",
        Writer = b.Writer,
        Created = b.Created,
        Updated = b.Updated,
        View = b.View,
        Deleted = b.Deleted,
        Locked = b.Locked,
        Noticed = b.Noticed
    };

    /// <inheritdoc />
    public Task<List<Board>> GetNoticedAsync(string type, CancellationToken ct = default)
        // Pure-display read (rendered as the noticed-post list, never saved back): AsNoTracking
        // skips the change-tracking snapshot/identity-map overhead this method has no use for.
        => QueryAsync(e => e.Type == type && !e.Deleted && e.Noticed,
            q => q.OrderByDescending(e => e.Id), noTracking: true, ct: ct);

    /// <summary>The LIKE escape character <see cref="GenericRepository{TDomain,TEntity}.ToLikePattern"/> uses (Postgres's default).</summary>
    private const string LikeEscape = "\\";

    /// <inheritdoc />
    public async Task<(List<Board> Items, int TotalCount)> QueryPageAsync(BoardPageQuery query, CancellationToken ct = default)
    {
        IQueryable<OrmBoard> baseQuery = Set.Where(x => x.Type == query.Type && !x.Deleted && !x.Noticed);
        if (query.OwnerNickname != null)
            baseQuery = baseQuery.Where(x => x.Writer == query.OwnerNickname);

        // Match the search term literally. string.Contains translate to a LIKE whose parameter's
        // %/_ are still wildcards, so "50%" or "a_b" would search as a pattern; escape them the same
        // way the raw-SQL search paths (SearchAsync / ExpenditureRepository) do via ToLikePattern.
        // EF.Functions.Like keeps the case-sensitive semantics string.Contains had here. The escape
        // character must be passed explicitly: the two-argument overload is translated to
        // `LIKE @p ESCAPE ''` (no escape character at all), so the backslashes ToLikePattern adds would be
        // matched literally and a term containing `_`, `%` or `\` (e.g. the nickname "john_doe") found nothing.
        string searchPattern = ToLikePattern(query.SearchText ?? string.Empty);

        IQueryable<OrmBoard> filtered;

        // Applies the "private (locked) posts are never searchable" rule to a set of search
        // matches and orders the result: an Admin sees every match; a regular User sees the
        // non-locked matches plus their own locked matches (their own ordered after, preserving
        // the group order via SQL UNION ALL rather than a single ID sort); an anonymous / other
        // viewer sees only non-locked matches. Shared by the Title AND Writer search branches so
        // both enforce the locked-visibility split identically — a Writer search must not be a
        // back door to enumerating another account's locked posts.
        IQueryable<OrmBoard> ApplyLockedVisibility(IQueryable<OrmBoard> matches)
        {
            if (!query.IsLoggedIn)
                return matches.Where(x => !x.Locked).OrderByDescending(x => x.Id);

            switch (query.ViewerRole)
            {
                case Role.Admin:
                    return matches.OrderByDescending(x => x.Id);
                case Role.User:
                {
                    IQueryable<OrmBoard> noLocked = matches.Where(x => !x.Locked);
                    IQueryable<OrmBoard> ownLocked = matches.Where(x => x.Locked && x.Writer == query.ViewerNickname);

                    return noLocked.Select(x => new { Board = x, Group = 0 })
                        .Concat(ownLocked.Select(x => new { Board = x, Group = 1 }))
                        .OrderBy(x => x.Group).ThenByDescending(x => x.Board.Id)
                        .Select(x => x.Board);
                }
                default:
                    // Logged in but neither Admin nor User (Role only ever has these two values for an
                    // active session — unreachable in practice). Matches the original code: no case fires,
                    // so the list is left as the unfiltered, unsearched base query.
                    return baseQuery.OrderByDescending(x => x.Id);
            }
        }

        if (query.OwnerNickname != null)
        {
            // Already scoped to the caller's own posts (PrivateNote) — no role/Locked visibility
            // split needed, matching ManagementController's PrivateNote list action.
            IQueryable<OrmBoard> scoped = query.SearchType switch
            {
                "Title" when !string.IsNullOrEmpty(query.SearchText) => baseQuery.Where(x => EF.Functions.Like(x.Title, searchPattern, LikeEscape)),
                "Writer" when !string.IsNullOrEmpty(query.SearchText) => baseQuery.Where(x => EF.Functions.Like(x.Writer, searchPattern, LikeEscape)),
                _ => baseQuery
            };
            filtered = scoped.OrderByDescending(x => x.Id);
        }
        else
            filtered = query.SearchType switch
            {
                "Title" when !string.IsNullOrEmpty(query.SearchText) => ApplyLockedVisibility(
                    baseQuery.Where(x => EF.Functions.Like(x.Title, searchPattern, LikeEscape))),
                "Writer" when !string.IsNullOrEmpty(query.SearchText) => ApplyLockedVisibility(
                    baseQuery.Where(x => EF.Functions.Like(x.Writer, searchPattern, LikeEscape))),
                _ => baseQuery.OrderByDescending(x => x.Id)
            };

        // The count and the page fetch are two separate round trips; under Postgres's default READ
        // COMMITTED isolation each sees its own latest-committed snapshot, so a concurrent insert or
        // delete between them can produce a TotalCount inconsistent with the returned page (or a row
        // skipped/duplicated across adjacent pages). Closing that window requires pinning both reads
        // to one snapshot, i.e. a REPEATABLE READ transaction — but transaction boundaries belong to
        // the Service layer via IUnitOfWork, not this repository (see BoardService.GetBoardPageAsync,
        // which wraps this call accordingly). A caller that doesn't need that guarantee may call this
        // outside any transaction; it still returns a correct, merely not snapshot-pinned, result.
        int totalCount = await filtered.CountAsync(ct);

        // Page comes straight from the ?page= query string. (Page - 1) * PageSize in int arithmetic
        // wraps for a huge value (e.g. 600000000 * 5) into a negative OFFSET, which Postgres rejects
        // ("OFFSET must not be negative") — a 500 for any visitor. Compute in long, floor at 0, and
        // cap at int.MaxValue: an offset past the last row simply returns an empty page.
        long offset = Math.Max(0L, (long)query.Page - 1) * query.PageSize;
        int skip = (int)Math.Min(offset, int.MaxValue);

        List<OrmBoard> page = await filtered.Skip(skip).Take(query.PageSize).ToListAsync(ct);
        return (page.Select(ToDomain).ToList(), totalCount);
    }

    /// <inheritdoc />
    public Task<Board?> FindActiveByIdAsync(long id, CancellationToken ct = default)
        => QueryFirstAsync(e => e.Id == id && e.Deleted == false, ct: ct);

    /// <inheritdoc />
    public async Task<Board?> FindActiveByIdForUpdateAsync(long id, CancellationToken ct = default)
    {
        // Raw SQL is required because EF Core LINQ cannot emit FOR UPDATE.
        // ToListAsync on the raw root executes the SQL verbatim (no composition),
        // keeping FOR UPDATE at the top level of the statement.
        // AsNoTracking is required too: if this Board's row was already tracked earlier in the
        // same DbContext scope, a tracking query performs identity resolution and hands back that
        // already-tracked (pre-lock) instance instead of the row this FOR UPDATE just (re-)read —
        // silently defeating the lock's whole purpose. AsNoTracking always materializes a fresh
        // instance from what was actually just selected, bypassing that snap-back.
        var entities = await Set.FromSqlInterpolated(
            $"""
             SELECT *
             FROM "Board"
             WHERE "Id" = {id} AND "Deleted" = false
             LIMIT 1
             FOR UPDATE
             """).AsNoTracking().ToListAsync(ct);

        var entity = entities.FirstOrDefault();
        return entity == null ? null : ToDomain(entity);
    }
}
