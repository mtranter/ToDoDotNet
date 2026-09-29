using ToDo.Domain;

namespace ToDo.Application;

/// <summary>
/// The port through which the application reaches storage. Expressed entirely in Domain types plus
/// the concurrency token; nothing in this file knows that Postgres or EF Core exist.
/// </summary>
public interface ITodoListRepository
{
    Task<Versioned<TodoList>?> GetAsync(TodoListId id, CancellationToken cancellationToken);

    /// <returns>The token the newly stored list starts life with.</returns>
    Task<ConcurrencyToken> AddAsync(TodoList list, CancellationToken cancellationToken);

    /// <returns>
    /// The list's new token, or <c>null</c> when <paramref name="expected"/> no longer matches the
    /// stored version. Returning the new token keeps the rule for how a version advances inside the
    /// adapter, where it belongs.
    /// </returns>
    Task<ConcurrencyToken?> UpdateAsync(TodoList list, ConcurrencyToken expected, CancellationToken cancellationToken);

    /// <returns><c>false</c> when the list is missing or <paramref name="expected"/> is stale.</returns>
    Task<bool> DeleteAsync(TodoListId id, ConcurrencyToken expected, CancellationToken cancellationToken);

    Task<IReadOnlyList<TodoListSummary>> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<int> CountAsync(CancellationToken cancellationToken);
}

/// <summary>
/// A read model projected straight out of storage. Deliberately never materialises a
/// <see cref="TodoList"/>: the read path and the write path are different shapes.
/// </summary>
public sealed record TodoListSummary(TodoListId Id, string Title, int ItemCount, int CompletedCount);
