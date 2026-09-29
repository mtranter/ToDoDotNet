namespace ToDo.Domain;

/// <summary>
/// Identity of a <see cref="TodoList"/>. A distinct type from <see cref="TodoItemId"/> so the
/// compiler refuses to let one stand in for the other.
/// </summary>
public readonly record struct TodoListId(Guid Value)
{
    // UUIDv7 is time-ordered, so ids generated here stay index-friendly in Postgres.
    public static TodoListId New() => new(Guid.CreateVersion7());

}
