using App = ToDo.Application.Contracts;

namespace ToDo.Web.Contracts;

/// <summary>The third and final mapping hop: Application contracts to wire contracts.</summary>
internal static class ContractMapping
{
    public static App.CreateListCommand ToCommand(this CreateListRequest request) => new(request.Title);

    public static App.RenameListCommand ToCommand(this RenameListRequest request) => new(request.Title);

    public static App.AddItemCommand ToCommand(this AddItemRequest request) => new(request.Title, request.DueDate);

    public static App.UpdateItemCommand ToCommand(this UpdateItemRequest request) => new(request.Title, request.DueDate);

    public static TodoListResponse ToWire(this App.TodoListResponse response) =>
        new(response.Id, response.Title, response.CreatedAt, [.. response.Items.Select(item => item.ToWire())]);

    public static TodoItemResponse ToWire(this App.TodoItemResponse item) =>
        new(item.Id, item.Title, item.DueDate, item.IsComplete, item.CreatedAt, item.CompletedAt, item.IsOverdue);

    public static PagedResponse<TodoListSummaryResponse> ToWire(this App.PagedResponse<App.TodoListSummaryResponse> page) =>
        new(
            [.. page.Items.Select(summary => new TodoListSummaryResponse(
                summary.Id, summary.Title, summary.ItemCount, summary.CompletedCount))],
            page.Page,
            page.PageSize,
            page.TotalCount);
}
