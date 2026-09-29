namespace ToDo.Repository.Persistence;

/// <summary>The storage shape of a todo item. Has no version of its own; see <see cref="TodoListRecord.Version"/>.</summary>
internal sealed class TodoItemRecord
{
    public Guid Id { get; set; }

    public Guid TodoListId { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTimeOffset? DueDate { get; set; }

    public bool IsComplete { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}
