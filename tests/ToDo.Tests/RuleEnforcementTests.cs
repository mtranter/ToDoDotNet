using System.Net;
using Shouldly;
using ToDo.Tests.Infrastructure;

namespace ToDo.Tests;

/// <summary>
/// The error paths. These are where a blackbox suite earns its keep: they prove the layering holds
/// all the way from a domain invariant out to a status code.
/// </summary>
public sealed class RuleEnforcementTests(TodoApiFixture fixture) : TodoApiTest(fixture)
{
    [Fact]
    public async Task An_empty_title_is_rejected() =>
        await (await Api.CreateListAsync("")).ShouldBeProblem(HttpStatusCode.BadRequest);

    [Fact]
    public async Task An_over_long_title_is_rejected() =>
        await (await Api.CreateListAsync(new string('x', 201))).ShouldBeProblem(HttpStatusCode.BadRequest);

    [Fact]
    public async Task A_due_date_in_the_past_is_rejected()
    {
        var list = await NewListAsync();

        await (await Api.AddItemAsync(list.Id, "Milk", DateTimeOffset.UtcNow.AddDays(-1), list.ETag))
            .ShouldBeProblem(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task An_unknown_list_is_not_found() =>
        (await Api.GetListAsync(Guid.CreateVersion7())).StatusCode.ShouldBe(HttpStatusCode.NotFound);

    [Fact]
    public async Task An_unknown_item_is_not_found()
    {
        var list = await NewListAsync();

        await (await Api.CompleteItemAsync(list.Id, Guid.CreateVersion7(), list.ETag))
            .ShouldBeProblem(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// The aggregate, not the service, decides item membership: an item from another list reaches
    /// <c>TodoItemNotFoundException</c> and is mapped to 404 at the HTTP edge.
    /// </summary>
    [Fact]
    public async Task An_item_from_another_list_is_not_found()
    {
        var (_, otherItemId, _) = await NewListWithItemAsync();
        var list = await NewListAsync();

        var problem = await (await Api.RemoveItemAsync(list.Id, otherItemId, list.ETag))
            .ShouldBeAndReadJson(HttpStatusCode.NotFound);

        problem
            .ShouldHaveValueAt("$.title", "Not Found")
            .ShouldHaveValueAt("$.detail", $"Todo item {otherItemId} does not belong to this list.");
    }

    [Fact]
    public async Task Completing_an_already_complete_item_is_a_conflict()
    {
        var (listId, itemId, etag) = await NewListWithItemAsync();

        var completed = await (await Api.CompleteItemAsync(listId, itemId, etag)).ShouldBeOkAndReadJson();

        await (await Api.CompleteItemAsync(listId, itemId, completed.ETag))
            .ShouldBeProblem(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Reopening_an_item_that_is_not_complete_is_a_conflict()
    {
        var (listId, itemId, etag) = await NewListWithItemAsync();

        await (await Api.ReopenItemAsync(listId, itemId, etag)).ShouldBeProblem(HttpStatusCode.Conflict);
    }

    /// <summary>
    /// The regression guard for the rule that a due date is checked when it is <em>assigned</em>,
    /// not on every subsequent change. Get this wrong and an item becomes uneditable the moment it
    /// falls overdue — which is exactly when someone wants to edit it.
    /// </summary>
    [Fact]
    public async Task An_overdue_item_can_still_be_renamed()
    {
        var list = await NewListAsync();
        var due = DateTimeOffset.UtcNow.AddSeconds(2);

        var withItem = await (await Api.AddItemAsync(list.Id, "Milk", due, list.ETag)).ShouldBeOkAndReadJson();
        var itemId = withItem.IdAt("$.items[0].id");

        await Task.Delay(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

        var overdue = await (await Api.GetListAsync(list.Id)).ShouldBeOkAndReadJson();
        overdue.ShouldHaveValueAt("$.items[0].isOverdue", "true");

        // Echo back the due date exactly as the API reported it, the way a real client would.
        // Postgres stores timestamptz to the microsecond while .NET keeps 100ns ticks, so a
        // locally-held copy is not the same instant once it has been round-tripped.
        var storedDueDate = DateTimeOffset.Parse(overdue.ValueAt<string>("$.items[0].dueDate"));

        var renamed = await (await Api.UpdateItemAsync(list.Id, itemId, "Oat milk", storedDueDate, withItem.ETag))
            .ShouldBeOkAndReadJson();

        renamed.ShouldHaveValueAt("$.items[0].title", "Oat milk");
        renamed.ShouldHaveValueAt("$.items[0].isOverdue", "true");
    }

    private async Task<(Guid Id, string ETag)> NewListAsync()
    {
        var json = await (await Api.CreateListAsync("Groceries")).ShouldBeCreatedAndReadJson();

        return (json.IdAt("$.id"), json.ETag);
    }

    private async Task<(Guid ListId, Guid ItemId, string ETag)> NewListWithItemAsync()
    {
        var list = await NewListAsync();
        var json = await (await Api.AddItemAsync(list.Id, "Milk", null, list.ETag)).ShouldBeOkAndReadJson();

        return (list.Id, json.IdAt("$.items[0].id"), json.ETag);
    }
}
