namespace ToDo.Application.Contracts;

public sealed record TodoListResponse(
    Guid Id,
    string Title,
    DateTimeOffset CreatedAt,
    IReadOnlyList<TodoItemResponse> Items);

public sealed record TodoItemResponse(
    Guid Id,
    string Title,
    DateTimeOffset? DueDate,
    bool IsComplete,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    bool IsOverdue);

public sealed record TodoListSummaryResponse(Guid Id, string Title, int ItemCount, int CompletedCount);

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
