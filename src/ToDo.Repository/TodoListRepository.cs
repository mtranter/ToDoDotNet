using Microsoft.EntityFrameworkCore;
using ToDo.Application;
using ToDo.Domain;
using ToDo.Repository.Persistence;

namespace ToDo.Repository;

/// <summary>The EF Core adapter behind <see cref="ITodoListRepository"/>.</summary>
internal sealed class TodoListRepository(TodoDbContext db) : ITodoListRepository
{
    public async Task<Versioned<TodoList>?> GetAsync(TodoListId id, CancellationToken cancellationToken)
    {
        var record = await db.TodoLists
            .Include(list => list.Items)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(list => list.Id == id.Value, cancellationToken);

        return record is null
            ? null
            : new Versioned<TodoList>(record.ToDomain(), new ConcurrencyToken(record.Version));
    }

    public async Task<ConcurrencyToken> AddAsync(TodoList list, CancellationToken cancellationToken)
    {
        var record = list.ToNewRecord();

        db.TodoLists.Add(record);
        await db.SaveChangesAsync(cancellationToken);

        return new ConcurrencyToken(record.Version);
    }

    public async Task<ConcurrencyToken?> UpdateAsync(
        TodoList list, ConcurrencyToken expected, CancellationToken cancellationToken)
    {
        // The extra SELECT that hexagonal purity costs us: the aggregate arrives detached, so the
        // tracked graph has to be fetched before it can be diffed. See docs/adr/0002.
        var record = await db.TodoLists
            .Include(stored => stored.Items)
            .AsSplitQuery()
            .FirstOrDefaultAsync(stored => stored.Id == list.Id.Value, cancellationToken);

        if (record is null)
        {
            return null;
        }

        list.ReconcileInto(record);

        // Two versions are in scope here and the ergonomic one is wrong.
        //
        // `record.Version` is what the SELECT above just read — using it would compare the stored
        // value against itself, so the check would always pass and concurrent writers would
        // silently overwrite each other. The value that matters is the one the *client* was
        // holding, which is why it is written into OriginalValue: that is what EF puts in the
        // WHERE clause.
        db.Entry(record).Property(stored => stored.Version).OriginalValue = expected.Value;
        record.Version = expected.Value + 1;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return null;
        }

        return new ConcurrencyToken(record.Version);
    }

    public async Task<bool> DeleteAsync(TodoListId id, ConcurrencyToken expected, CancellationToken cancellationToken)
    {
        // ExecuteDelete bypasses the change tracker, so EF performs no cascade of its own. The
        // items are removed by the ON DELETE CASCADE that TodoListConfiguration puts on the FK.
        var deleted = await db.TodoLists
            .Where(list => list.Id == id.Value && list.Version == expected.Value)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted > 0;
    }

    public async Task<IReadOnlyList<TodoListSummary>> GetPageAsync(
        int page, int pageSize, CancellationToken cancellationToken) =>
        await db.TodoLists
            .OrderBy(list => list.Id) // UUIDv7 is time-ordered, so this is creation order.
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            // Projected straight into the read model: no aggregate is ever built on this path.
            .Select(list => new TodoListSummary(
                new TodoListId(list.Id),
                list.Title,
                list.Items.Count,
                list.Items.Count(item => item.IsComplete)))
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(CancellationToken cancellationToken) =>
        db.TodoLists.CountAsync(cancellationToken);
}
