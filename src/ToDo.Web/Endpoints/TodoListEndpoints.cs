using Microsoft.AspNetCore.Mvc;
using ToDo.Application;
using ToDo.Web.Contracts;
using ToDo.Web.Infrastructure;
using App = ToDo.Application.Contracts;

namespace ToDo.Web.Endpoints;

/// <summary>
/// The HTTP surface. Items are nested under their list because a list is the only thing that
/// exists independently (docs/adr/0003).
/// </summary>
internal static class TodoListEndpoints
{
    private const int MaxPageSize = 100;

    public static IEndpointRouteBuilder MapTodoLists(this IEndpointRouteBuilder app)
    {
        var lists = app.MapGroup("/lists").WithTags("Todo Lists");

        lists.MapGet("/", GetPage);
        lists.MapPost("/", Create);
        lists.MapGet("/{listId:guid}", Get);
        lists.MapPatch("/{listId:guid}", Rename);
        lists.MapDelete("/{listId:guid}", Delete);

        lists.MapPost("/{listId:guid}/items", AddItem);
        lists.MapPatch("/{listId:guid}/items/{itemId:guid}", UpdateItem);
        lists.MapDelete("/{listId:guid}/items/{itemId:guid}", RemoveItem);

        // Complete and Reopen are guarded state transitions, not field edits, so they are actions
        // rather than a PATCH of an isComplete flag.
        lists.MapPost("/{listId:guid}/items/{itemId:guid}/complete", CompleteItem);
        lists.MapPost("/{listId:guid}/items/{itemId:guid}/reopen", ReopenItem);

        return app;
    }

    private static async Task<IResult> GetPage(
        ITodoListService service,
        HttpResponse response,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1 || pageSize < 1)
        {
            return Problems.Create(StatusCodes.Status400BadRequest, "Invalid Paging", "page and pageSize must be at least 1.");
        }

        var result = await service.GetListPageAsync(page, Math.Min(pageSize, MaxPageSize), cancellationToken);

        // A collection is not an aggregate, so it carries no ETag.
        return result.IsSuccess
            ? TypedResults.Ok(result.Value.ToWire())
            : Problems.FromError(result.Error);
    }

    private static async Task<IResult> Create(
        CreateListRequest request, ITodoListService service, HttpResponse response, CancellationToken cancellationToken)
    {
        var result = await service.CreateListAsync(request.ToCommand(), cancellationToken);

        if (!result.IsSuccess)
        {
            return Problems.FromError(result.Error);
        }

        var (created, token) = result.Value;
        response.Headers.ETag = ETags.Format(token);

        return TypedResults.Created($"/lists/{created.Id}", created.ToWire());
    }

    private static async Task<IResult> Get(
        Guid listId, ITodoListService service, HttpResponse response, CancellationToken cancellationToken) =>
        Respond(await service.GetListAsync(listId, cancellationToken), response);

    private static Task<IResult> Rename(
        Guid listId,
        RenameListRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        ITodoListService service,
        HttpResponse response,
        CancellationToken cancellationToken) =>
        Guarded(ifMatch, response, token => service.RenameListAsync(listId, request.ToCommand(), token, cancellationToken));

    private static async Task<IResult> Delete(
        Guid listId,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        ITodoListService service,
        CancellationToken cancellationToken)
    {
        if (!ETags.TryRead(ifMatch, out var token, out var error))
        {
            return error;
        }

        var result = await service.DeleteListAsync(listId, token, cancellationToken);

        return result.IsSuccess ? TypedResults.NoContent() : Problems.FromError(result.Error);
    }

    private static Task<IResult> AddItem(
        Guid listId,
        AddItemRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        ITodoListService service,
        HttpResponse response,
        CancellationToken cancellationToken) =>
        Guarded(ifMatch, response, token => service.AddItemAsync(listId, request.ToCommand(), token, cancellationToken));

    private static Task<IResult> UpdateItem(
        Guid listId,
        Guid itemId,
        UpdateItemRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        ITodoListService service,
        HttpResponse response,
        CancellationToken cancellationToken) =>
        Guarded(ifMatch, response, token => service.UpdateItemAsync(listId, itemId, request.ToCommand(), token, cancellationToken));

    private static Task<IResult> RemoveItem(
        Guid listId,
        Guid itemId,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        ITodoListService service,
        HttpResponse response,
        CancellationToken cancellationToken) =>
        Guarded(ifMatch, response, token => service.RemoveItemAsync(listId, itemId, token, cancellationToken));

    private static Task<IResult> CompleteItem(
        Guid listId,
        Guid itemId,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        ITodoListService service,
        HttpResponse response,
        CancellationToken cancellationToken) =>
        Guarded(ifMatch, response, token => service.CompleteItemAsync(listId, itemId, token, cancellationToken));

    private static Task<IResult> ReopenItem(
        Guid listId,
        Guid itemId,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        ITodoListService service,
        HttpResponse response,
        CancellationToken cancellationToken) =>
        Guarded(ifMatch, response, token => service.ReopenItemAsync(listId, itemId, token, cancellationToken));

    /// <summary>Every write follows the same preconditions, so they are stated once.</summary>
    private static async Task<IResult> Guarded(
        string? ifMatch,
        HttpResponse response,
        Func<ConcurrencyToken, Task<Result<Versioned<App.TodoListResponse>>>> operation)
    {
        if (!ETags.TryRead(ifMatch, out var token, out var error))
        {
            return error;
        }

        return Respond(await operation(token), response);
    }

    private static IResult Respond(Result<Versioned<App.TodoListResponse>> result, HttpResponse response)
    {
        if (!result.IsSuccess)
        {
            return Problems.FromError(result.Error);
        }

        var (list, token) = result.Value;

        // Writes hand back a fresh ETag, so a caller never needs a follow-up read to keep writing.
        response.Headers.ETag = ETags.Format(token);

        return TypedResults.Ok(list.ToWire());
    }
}
