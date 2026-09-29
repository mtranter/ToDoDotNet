using System.Net;
using Shouldly;
using ToDo.Tests.Infrastructure;

namespace ToDo.Tests;

public sealed class TodoListEndpointTests(TodoApiFixture fixture) : TodoApiTest(fixture)
{
    [Fact]
    public async Task Creating_a_list_returns_it_with_a_location_and_an_etag()
    {
        var response = await Api.CreateListAsync("Groceries");

        var json = await response.ShouldBeCreatedAndReadJson();

        json.ShouldHaveValueAt("$.title", "Groceries");
        json.ShouldHaveCountAt("$.items", 0);

        json.ETag.ShouldBe("\"1\"");
        response.Headers.Location!.ToString().ShouldBe($"/lists/{json.IdAt("$.id")}");
    }

    [Fact]
    public async Task A_created_list_can_be_read_back()
    {
        var listId = (await (await Api.CreateListAsync("Groceries")).ShouldBeCreatedAndReadJson()).IdAt("$.id");

        var json = await (await Api.GetListAsync(listId)).ShouldBeOkAndReadJson();

        json.ShouldHaveValueAt("$.id", listId);
        json.ShouldHaveValueAt("$.title", "Groceries");
        json.ETag.ShouldBe("\"1\"");
    }

    [Fact]
    public async Task Renaming_a_list_keeps_its_items_and_advances_the_etag()
    {
        var created = await (await Api.CreateListAsync("Grocries")).ShouldBeCreatedAndReadJson();
        var listId = created.IdAt("$.id");

        var withItem = await (await Api.AddItemAsync(listId, "Milk", null, created.ETag)).ShouldBeOkAndReadJson();

        var renamed = await (await Api.RenameListAsync(listId, "Groceries", withItem.ETag)).ShouldBeOkAndReadJson();

        renamed.ShouldHaveValueAt("$.title", "Groceries");
        renamed.ShouldHaveCountAt("$.items", 1);
        renamed.ETag.ShouldBe("\"3\"");
    }

    [Fact]
    public async Task Deleting_a_list_takes_its_items_with_it()
    {
        var created = await (await Api.CreateListAsync("Groceries")).ShouldBeCreatedAndReadJson();
        var listId = created.IdAt("$.id");

        var withItem = await (await Api.AddItemAsync(listId, "Milk", null, created.ETag)).ShouldBeOkAndReadJson();

        (await Api.DeleteListAsync(listId, withItem.ETag)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await Api.GetListAsync(listId)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // The items went with it: the summary projection counts nothing at all.
        var page = await (await Api.GetListsAsync()).ShouldBeOkAndReadJson();
        page.ShouldHaveCountAt("$.items", 0);
        page.ShouldHaveValueAt("$.totalCount", 0);
    }

    [Fact]
    public async Task The_collection_returns_summaries_rather_than_items()
    {
        var created = await (await Api.CreateListAsync("Groceries")).ShouldBeCreatedAndReadJson();
        var listId = created.IdAt("$.id");

        var one = await (await Api.AddItemAsync(listId, "Milk", null, created.ETag)).ShouldBeOkAndReadJson();
        var two = await (await Api.AddItemAsync(listId, "Bread", null, one.ETag)).ShouldBeOkAndReadJson();
        var itemId = two.IdAt("$.items[0].id");
        var completed = await (await Api.CompleteItemAsync(listId, itemId, two.ETag)).ShouldBeOkAndReadJson();

        completed.ShouldNotBeNull();

        var page = await (await Api.GetListsAsync()).ShouldBeOkAndReadJson();

        page.ShouldHaveCountAt("$.items", 1);
        page.ShouldHaveValueAt("$.items[0].title", "Groceries");
        page.ShouldHaveValueAt("$.items[0].itemCount", 2);
        page.ShouldHaveValueAt("$.items[0].completedCount", 1);

        // A summary is a projection, not the aggregate: there is no items array on it.
        page.ShouldHaveNoValueAt("$.items[0].items");

        // A collection is not an aggregate, so it carries no ETag.
        page.Response.Headers.ETag.ShouldBeNull();
    }

    [Fact]
    public async Task The_collection_pages()
    {
        foreach (var name in new[] { "One", "Two", "Three" })
        {
            (await Api.CreateListAsync(name)).StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        var page = await (await Api.GetListsAsync(page: 2, pageSize: 2)).ShouldBeOkAndReadJson();

        page.ShouldHaveCountAt("$.items", 1);
        page.ShouldHaveValueAt("$.page", 2);
        page.ShouldHaveValueAt("$.pageSize", 2);
        page.ShouldHaveValueAt("$.totalCount", 3);
    }
}
