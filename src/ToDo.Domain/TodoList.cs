namespace ToDo.Domain;

/// <summary>
/// A named collection of <see cref="TodoItem"/>s, and the only aggregate root in this domain.
/// </summary>
/// <remarks>
/// The item collection is private and surfaced read-only. Every change to an item goes through a
/// method on this class, which is what makes the aggregate boundary a fact rather than a convention.
/// <para>
/// <c>now</c> is passed in rather than read from <c>DateTimeOffset.UtcNow</c> so the domain has no
/// ambient dependency and time-sensitive rules stay testable.
/// </para>
/// </remarks>
public sealed class TodoList
{
    private readonly List<TodoItem> _items;

    public TodoListId Id { get; }
    public TodoTitle Title { get; private set; }
    public DateTimeOffset CreatedAt { get; }

    public IReadOnlyCollection<TodoItem> Items => _items.AsReadOnly();

    private TodoList(TodoListId id, TodoTitle title, DateTimeOffset createdAt, List<TodoItem> items)
    {
        Id = id;
        Title = title;
        CreatedAt = createdAt;
        _items = items;
    }

    public static TodoList Create(TodoTitle title, DateTimeOffset now) =>
        new(TodoListId.New(), title, now, []);

    /// <summary>Rebuilds a list from state that was already valid when it was stored.</summary>
    public static TodoList Rehydrate(
        TodoListId id,
        TodoTitle title,
        DateTimeOffset createdAt,
        IEnumerable<TodoItem> items) =>
        new(id, title, createdAt, [.. items]);

    public void Rename(TodoTitle title) => Title = title;

    public TodoItemId AddItem(TodoTitle title, DateTimeOffset? dueDate, DateTimeOffset now)
    {
        var item = TodoItem.Create(title, dueDate, now);
        _items.Add(item);
        return item.Id;
    }

    public void RenameItem(TodoItemId id, TodoTitle title) => Require(id).Rename(title);

    public void SetItemDueDate(TodoItemId id, DateTimeOffset? dueDate, DateTimeOffset now) =>
        Require(id).SetDueDate(dueDate, now);

    public void CompleteItem(TodoItemId id, DateTimeOffset now) => Require(id).Complete(now);

    public void ReopenItem(TodoItemId id) => Require(id).Reopen();

    public void RemoveItem(TodoItemId id) => _items.Remove(Require(id));

    private TodoItem Require(TodoItemId id) =>
        _items.FirstOrDefault(item => item.Id == id) ?? throw new TodoItemNotFoundException(id);
}
