namespace ToDo.Repository.Persistence;

/// <summary>
/// The storage shape of a todo list. Mirrors the table, not the aggregate.
/// </summary>
/// <remarks>
/// Named <c>Record</c> rather than <c>Dao</c> because in the Java tradition a DAO is the access
/// object — the thing <see cref="TodoListRepository"/> is. This is the data.
/// </remarks>
internal sealed class TodoListRecord
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Optimistic concurrency token. Lives on the root only: the aggregate is the consistency boundary.</summary>
    public int Version { get; set; }

    public List<TodoItemRecord> Items { get; set; } = [];
}
