using ToDo.Domain;

namespace ToDo.Repository.Persistence;

/// <summary>
/// Hand-written mapping between the aggregate and its storage shape (docs/adr/0002).
/// </summary>
internal static class RecordMapping
{
    public static TodoList ToDomain(this TodoListRecord record) =>
        TodoList.Rehydrate(
            new TodoListId(record.Id),
            TodoTitle.Create(record.Title),
            record.CreatedAt,
            record.Items
                .OrderBy(item => item.Id)
                .Select(item => item.ToDomain()));

    public static TodoItem ToDomain(this TodoItemRecord record) =>
        TodoItem.Rehydrate(
            new TodoItemId(record.Id),
            TodoTitle.Create(record.Title),
            record.DueDate,
            record.IsComplete,
            record.CreatedAt,
            record.CompletedAt);

    public static TodoListRecord ToNewRecord(this TodoList list) =>
        new()
        {
            Id = list.Id.Value,
            Title = list.Title.Value,
            CreatedAt = list.CreatedAt,
            Version = InitialVersion,
            Items = [.. list.Items.Select(item => item.ToNewRecord(list.Id))],
        };

    public static TodoItemRecord ToNewRecord(this TodoItem item, TodoListId listId) =>
        new()
        {
            Id = item.Id.Value,
            TodoListId = listId.Value,
            Title = item.Title.Value,
            DueDate = item.DueDate,
            IsComplete = item.IsComplete,
            CreatedAt = item.CreatedAt,
            CompletedAt = item.CompletedAt,
        };

    public const int InitialVersion = 1;

    /// <summary>
    /// Reconciles a tracked record graph against a detached aggregate.
    /// </summary>
    /// <remarks>
    /// Ids are generated in the Domain, so matching is an exact join with no heuristics and no
    /// need to guess which child is "new". Removing an item from the tracked collection is enough
    /// to delete it: the relationship is required, so EF treats the orphan as a delete.
    /// </remarks>
    public static void ReconcileInto(this TodoList list, TodoListRecord record)
    {
        record.Title = list.Title.Value;

        var domainItems = list.Items.ToDictionary(item => item.Id.Value);

        foreach (var existing in record.Items.ToList())
        {
            if (domainItems.TryGetValue(existing.Id, out var item))
            {
                existing.CopyFrom(item);
            }
            else
            {
                record.Items.Remove(existing);
            }
        }

        var storedIds = record.Items.Select(item => item.Id).ToHashSet();

        foreach (var added in list.Items.Where(item => !storedIds.Contains(item.Id.Value)))
        {
            record.Items.Add(added.ToNewRecord(list.Id));
        }
    }

    private static void CopyFrom(this TodoItemRecord record, TodoItem item)
    {
        record.Title = item.Title.Value;
        record.DueDate = item.DueDate;
        record.IsComplete = item.IsComplete;
        record.CompletedAt = item.CompletedAt;
    }
}
