namespace ToDo.Web.Contracts;

/// <summary>
/// The wire shape. Structurally close to the Application's response types today, and deliberately
/// a separate declaration so the HTTP contract can move independently of the application's.
/// </summary>
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
