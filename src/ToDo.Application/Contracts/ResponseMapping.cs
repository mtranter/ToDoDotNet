using ToDo.Domain;

namespace ToDo.Application.Contracts;

/// <summary>Domain to response mapping. Hand-written, on purpose: see docs/adr/0002.</summary>
internal static class ResponseMapping
{
    public static TodoListResponse ToResponse(this TodoList list, DateTimeOffset now) =>
        new(
            list.Id.Value,
            list.Title.Value,
            list.CreatedAt,
            [.. list.Items.Select(item => item.ToResponse(now))]);

    public static TodoItemResponse ToResponse(this TodoItem item, DateTimeOffset now) =>
        new(
            item.Id.Value,
            item.Title.Value,
            item.DueDate,
            item.IsComplete,
            item.CreatedAt,
            item.CompletedAt,
            IsOverdue: !item.IsComplete && item.DueDate is { } due && due < now);

    public static TodoListSummaryResponse ToResponse(this TodoListSummary summary) =>
        new(summary.Id.Value, summary.Title, summary.ItemCount, summary.CompletedCount);
}
