using ToDo.Application.Contracts;

namespace ToDo.Application;

/// <summary>
/// The application's entire useful surface. One service, because items are only reachable through
/// their list anyway (docs/adr/0003).
/// </summary>
/// <remarks>
/// Every write returns the full updated list together with its new token, so a caller always
/// leaves holding a fresh ETag and never needs a follow-up read.
/// </remarks>
public interface ITodoListService
{
    Task<Result<Versioned<TodoListResponse>>> CreateListAsync(
        CreateListCommand command, CancellationToken cancellationToken);

    Task<Result<Versioned<TodoListResponse>>> GetListAsync(
        Guid listId, CancellationToken cancellationToken);

    Task<Result<PagedResponse<TodoListSummaryResponse>>> GetListPageAsync(
        int page, int pageSize, CancellationToken cancellationToken);

    Task<Result<Versioned<TodoListResponse>>> RenameListAsync(
        Guid listId, RenameListCommand command, ConcurrencyToken expected, CancellationToken cancellationToken);

    Task<Result<Unit>> DeleteListAsync(
        Guid listId, ConcurrencyToken expected, CancellationToken cancellationToken);

    Task<Result<Versioned<TodoListResponse>>> AddItemAsync(
        Guid listId, AddItemCommand command, ConcurrencyToken expected, CancellationToken cancellationToken);

    Task<Result<Versioned<TodoListResponse>>> UpdateItemAsync(
        Guid listId, Guid itemId, UpdateItemCommand command, ConcurrencyToken expected, CancellationToken cancellationToken);

    Task<Result<Versioned<TodoListResponse>>> CompleteItemAsync(
        Guid listId, Guid itemId, ConcurrencyToken expected, CancellationToken cancellationToken);

    Task<Result<Versioned<TodoListResponse>>> ReopenItemAsync(
        Guid listId, Guid itemId, ConcurrencyToken expected, CancellationToken cancellationToken);

    Task<Result<Versioned<TodoListResponse>>> RemoveItemAsync(
        Guid listId, Guid itemId, ConcurrencyToken expected, CancellationToken cancellationToken);
}
