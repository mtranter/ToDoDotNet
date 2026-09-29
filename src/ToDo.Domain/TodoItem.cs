namespace ToDo.Domain;

/// <summary>
/// A single piece of work recorded inside exactly one <see cref="TodoList"/>.
/// </summary>
/// <remarks>
/// Every mutator is <c>internal</c>, so only <see cref="TodoList"/> can change an item.
/// There is no way to reach one of these except through the list that holds it.
/// </remarks>
public sealed class TodoItem
{
    public TodoItemId Id { get; }
    public TodoTitle Title { get; private set; }
    public DateTimeOffset? DueDate { get; private set; }
    public bool IsComplete { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? CompletedAt { get; private set; }

    private TodoItem(
        TodoItemId id,
        TodoTitle title,
        DateTimeOffset? dueDate,
        bool isComplete,
        DateTimeOffset createdAt,
        DateTimeOffset? completedAt)
    {
        Id = id;
        Title = title;
        DueDate = dueDate;
        IsComplete = isComplete;
        CreatedAt = createdAt;
        CompletedAt = completedAt;
    }

    internal static TodoItem Create(TodoTitle title, DateTimeOffset? dueDate, DateTimeOffset now)
    {
        GuardDueDate(dueDate, now);
        return new TodoItem(TodoItemId.New(), title, dueDate, isComplete: false, createdAt: now, completedAt: null);
    }

    /// <summary>
    /// Rebuilds an item from state that was already valid when it was stored.
    /// </summary>
    /// <remarks>
    /// Intentionally skips <see cref="GuardDueDate"/>. An item whose due date has since passed is
    /// Overdue, which is a perfectly valid state; re-running a creation-time rule on load would
    /// make it impossible to read your own data back.
    /// </remarks>
    public static TodoItem Rehydrate(
        TodoItemId id,
        TodoTitle title,
        DateTimeOffset? dueDate,
        bool isComplete,
        DateTimeOffset createdAt,
        DateTimeOffset? completedAt) =>
        new(id, title, dueDate, isComplete, createdAt, completedAt);

    internal void Rename(TodoTitle title) => Title = title;

    internal void SetDueDate(DateTimeOffset? dueDate, DateTimeOffset now)
    {
        // Re-sending the value an item already has is not an assignment, so it is not checked.
        // Without this, an item becomes uneditable the moment it falls overdue: a full-replacement
        // PATCH resends the existing due date, which is now in the past, and the guard rejects a
        // change the caller never asked for.
        if (dueDate == DueDate)
        {
            return;
        }

        GuardDueDate(dueDate, now);
        DueDate = dueDate;
    }

    internal void Complete(DateTimeOffset now)
    {
        if (IsComplete)
        {
            throw new InvalidStateTransitionException($"Todo item {Id} is already complete.");
        }

        IsComplete = true;
        CompletedAt = now;
    }

    internal void Reopen()
    {
        if (!IsComplete)
        {
            throw new InvalidStateTransitionException($"Todo item {Id} is not complete.");
        }

        IsComplete = false;
        CompletedAt = null;
    }

    // The rule bites when a due date is assigned, never afterwards.
    private static void GuardDueDate(DateTimeOffset? dueDate, DateTimeOffset now)
    {
        if (dueDate is { } due && due <= now)
        {
            throw new InvalidDueDateException($"A due date must be in the future, but {due:O} is not after {now:O}.");
        }
    }
}
