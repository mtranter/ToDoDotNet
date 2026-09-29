using ToDo.Application.Contracts;
using ToDo.Domain;

namespace ToDo.Application;

/// <inheritdoc />
/// <remarks>
/// Every write follows one shape, captured once in <see cref="MutateAsync"/>: load through the
/// port, fail with <see cref="ErrorKind.NotFound"/> if it is missing, mutate the domain object,
/// save, and fail with <see cref="ErrorKind.ConcurrencyConflict"/> if the token was stale.
/// <para>
/// Notice what this class does not do: it never catches a <see cref="TodoDomainException"/>.
/// Broken invariants are not application outcomes, so they travel past here untouched and are
/// mapped at the HTTP edge.
/// </para>
/// </remarks>
public sealed class TodoListService(ITodoListRepository repository, TimeProvider timeProvider) : ITodoListService
{
    public async Task<Result<Versioned<TodoListResponse>>> CreateListAsync(
        CreateListCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var list = TodoList.Create(TodoTitle.Create(command.Title), now);

        var token = await repository.AddAsync(list, cancellationToken);

        return new Versioned<TodoListResponse>(list.ToResponse(now), token);
    }

    public async Task<Result<Versioned<TodoListResponse>>> GetListAsync(
        Guid listId, CancellationToken cancellationToken)
    {
        if (await repository.GetAsync(new TodoListId(listId), cancellationToken) is not { } loaded)
        {
            return NoSuchList(listId);
        }

        var now = timeProvider.GetUtcNow();

        return new Versioned<TodoListResponse>(loaded.Value.ToResponse(now), loaded.Token);
    }

    public async Task<Result<PagedResponse<TodoListSummaryResponse>>> GetListPageAsync(
        int page, int pageSize, CancellationToken cancellationToken)
    {
        var summaries = await repository.GetPageAsync(page, pageSize, cancellationToken);
        var total = await repository.CountAsync(cancellationToken);

        return new PagedResponse<TodoListSummaryResponse>(
            [.. summaries.Select(summary => summary.ToResponse())],
            page,
            pageSize,
            total);
    }

    public Task<Result<Versioned<TodoListResponse>>> RenameListAsync(
        Guid listId, RenameListCommand command, ConcurrencyToken expected, CancellationToken cancellationToken) =>
        MutateAsync(listId, expected, (list, _) => list.Rename(TodoTitle.Create(command.Title)), cancellationToken);

    public async Task<Result<Unit>> DeleteListAsync(
        Guid listId, ConcurrencyToken expected, CancellationToken cancellationToken)
    {
        var id = new TodoListId(listId);

        if (await repository.GetAsync(id, cancellationToken) is null)
        {
            return NoSuchList(listId);
        }

        return await repository.DeleteAsync(id, expected, cancellationToken)
            ? Unit.Value
            : StaleToken(listId);
    }

    public Task<Result<Versioned<TodoListResponse>>> AddItemAsync(
        Guid listId, AddItemCommand command, ConcurrencyToken expected, CancellationToken cancellationToken) =>
        MutateAsync(
            listId,
            expected,
            (list, now) => list.AddItem(TodoTitle.Create(command.Title), command.DueDate, now),
            cancellationToken);

    public Task<Result<Versioned<TodoListResponse>>> UpdateItemAsync(
        Guid listId, Guid itemId, UpdateItemCommand command, ConcurrencyToken expected, CancellationToken cancellationToken) =>
        MutateAsync(
            listId,
            expected,
            (list, now) =>
            {
                var id = new TodoItemId(itemId);
                list.RenameItem(id, TodoTitle.Create(command.Title));
                list.SetItemDueDate(id, command.DueDate, now);
            },
            cancellationToken);

    public Task<Result<Versioned<TodoListResponse>>> CompleteItemAsync(
        Guid listId, Guid itemId, ConcurrencyToken expected, CancellationToken cancellationToken) =>
        MutateAsync(listId, expected, (list, now) => list.CompleteItem(new TodoItemId(itemId), now), cancellationToken);

    public Task<Result<Versioned<TodoListResponse>>> ReopenItemAsync(
        Guid listId, Guid itemId, ConcurrencyToken expected, CancellationToken cancellationToken) =>
        MutateAsync(listId, expected, (list, _) => list.ReopenItem(new TodoItemId(itemId)), cancellationToken);

    public Task<Result<Versioned<TodoListResponse>>> RemoveItemAsync(
        Guid listId, Guid itemId, ConcurrencyToken expected, CancellationToken cancellationToken) =>
        MutateAsync(listId, expected, (list, _) => list.RemoveItem(new TodoItemId(itemId)), cancellationToken);

    private async Task<Result<Versioned<TodoListResponse>>> MutateAsync(
        Guid listId,
        ConcurrencyToken expected,
        Action<TodoList, DateTimeOffset> mutate,
        CancellationToken cancellationToken)
    {
        if (await repository.GetAsync(new TodoListId(listId), cancellationToken) is not { } loaded)
        {
            return NoSuchList(listId);
        }

        var list = loaded.Value;

        // An unknown item is not checked for here: the aggregate throws TodoItemNotFoundException,
        // which the HTTP edge maps to 404 like any other broken domain rule.
        var now = timeProvider.GetUtcNow();
        mutate(list, now);

        if (await repository.UpdateAsync(list, expected, cancellationToken) is not { } token)
        {
            return StaleToken(listId);
        }

        return new Versioned<TodoListResponse>(list.ToResponse(now), token);
    }

    private static Error NoSuchList(Guid listId) => Error.NotFound($"Todo list {listId} was not found.");

    private static Error StaleToken(Guid listId) =>
        Error.ConcurrencyConflict($"Todo list {listId} was modified by someone else. Re-read it and try again.");
}
