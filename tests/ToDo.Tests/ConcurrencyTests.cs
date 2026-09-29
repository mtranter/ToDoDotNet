using System.Net;
using Shouldly;
using ToDo.Tests.Infrastructure;

namespace ToDo.Tests;

/// <summary>
/// The behaviour docs/adr/0004 exists to protect. If the repository ever compared the version it
/// had just read instead of the one the client supplied, every test here would still pass except
/// <see cref="A_stale_token_is_refused"/>.
/// </summary>
public sealed class ConcurrencyTests(TodoApiFixture fixture) : TodoApiTest(fixture)
{
    [Fact]
    public async Task A_write_without_an_if_match_header_is_refused()
    {
        var list = await NewListAsync();

        var response = await Api.AddItemAsync(list.Id, "Milk", null, etag: null);

        await response.ShouldBeProblem(HttpStatusCode.PreconditionRequired);
    }

    [Fact]
    public async Task A_stale_token_is_refused()
    {
        var list = await NewListAsync();

        // First writer wins and moves the version on.
        var first = await (await Api.AddItemAsync(list.Id, "Milk", null, list.ETag)).ShouldBeOkAndReadJson();
        first.ETag.ShouldNotBe(list.ETag);

        // Second writer is still holding the token it read before that write.
        var second = await Api.AddItemAsync(list.Id, "Bread", null, list.ETag);

        await second.ShouldBeProblem(HttpStatusCode.PreconditionFailed);

        // ...and the losing write left no trace.
        var current = await (await Api.GetListAsync(list.Id)).ShouldBeOkAndReadJson();
        current.ShouldHaveCountAt("$.items", 1);
        current.ShouldHaveValueAt("$.items[0].title", "Milk");
    }

    [Fact]
    public async Task Mutating_an_item_advances_the_lists_token()
    {
        // The token lives on the aggregate root, so an item-level change is a change to the list.
        var list = await NewListAsync();
        var withItem = await (await Api.AddItemAsync(list.Id, "Milk", null, list.ETag)).ShouldBeOkAndReadJson();
        var itemId = withItem.IdAt("$.items[0].id");

        var completed = await (await Api.CompleteItemAsync(list.Id, itemId, withItem.ETag)).ShouldBeOkAndReadJson();

        completed.ETag.ShouldBe("\"3\"");

        // The token the client held before completing the item is now stale for the list itself.
        await (await Api.RenameListAsync(list.Id, "Shopping", withItem.ETag))
            .ShouldBeProblem(HttpStatusCode.PreconditionFailed);
    }

    [Fact]
    public async Task A_delete_with_a_stale_token_is_refused_and_changes_nothing()
    {
        var list = await NewListAsync();
        var withItem = await (await Api.AddItemAsync(list.Id, "Milk", null, list.ETag)).ShouldBeOkAndReadJson();

        withItem.ShouldNotBeNull();

        await (await Api.DeleteListAsync(list.Id, list.ETag)).ShouldBeProblem(HttpStatusCode.PreconditionFailed);

        (await Api.GetListAsync(list.Id)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_malformed_if_match_is_rejected()
    {
        var list = await NewListAsync();

        await (await Api.RenameListAsync(list.Id, "Shopping", "\"not-a-version\""))
            .ShouldBeProblem(HttpStatusCode.BadRequest);
    }

    private async Task<(Guid Id, string ETag)> NewListAsync()
    {
        var json = await (await Api.CreateListAsync("Groceries")).ShouldBeCreatedAndReadJson();

        return (json.IdAt("$.id"), json.ETag);
    }
}
