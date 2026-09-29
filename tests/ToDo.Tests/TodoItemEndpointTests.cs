using Shouldly;
using ToDo.Tests.Infrastructure;

namespace ToDo.Tests;

public sealed class TodoItemEndpointTests(TodoApiFixture fixture) : TodoApiTest(fixture)
{
    [Fact]
    public async Task An_item_is_added_through_its_list()
    {
        var list = await NewListAsync();
        var due = DateTimeOffset.UtcNow.AddDays(3);

        var json = await (await Api.AddItemAsync(list.Id, "Milk", due, list.ETag)).ShouldBeOkAndReadJson();

        json.ShouldHaveCountAt("$.items", 1);
        json.ShouldHaveValueAt("$.items[0].title", "Milk");
        json.ShouldHaveValueAt("$.items[0].isComplete", "false");
        json.ShouldHaveValueAt("$.items[0].isOverdue", "false");
        json.ShouldHaveValueAt("$.items[0].completedAt", null);
    }

    [Fact]
    public async Task Completing_an_item_records_when_it_happened()
    {
        var (listId, itemId, etag) = await NewListWithItemAsync();

        var json = await (await Api.CompleteItemAsync(listId, itemId, etag)).ShouldBeOkAndReadJson();

        json.ShouldHaveValueAt("$.items[0].isComplete", "true");
        json.ValueAt<string>("$.items[0].completedAt").ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Reopening_a_completed_item_clears_its_completion()
    {
        var (listId, itemId, etag) = await NewListWithItemAsync();

        var completed = await (await Api.CompleteItemAsync(listId, itemId, etag)).ShouldBeOkAndReadJson();
        var reopened = await (await Api.ReopenItemAsync(listId, itemId, completed.ETag)).ShouldBeOkAndReadJson();

        reopened.ShouldHaveValueAt("$.items[0].isComplete", "false");
        reopened.ShouldHaveValueAt("$.items[0].completedAt", null);
    }

    [Fact]
    public async Task Updating_an_item_replaces_both_mutable_fields()
    {
        var (listId, itemId, etag) = await NewListWithItemAsync();
        var due = DateTimeOffset.UtcNow.AddDays(5);

        var updated = await (await Api.UpdateItemAsync(listId, itemId, "Oat milk", due, etag)).ShouldBeOkAndReadJson();

        updated.ShouldHaveValueAt("$.items[0].title", "Oat milk");
        updated.ValueAt<string>("$.items[0].dueDate").ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task A_null_due_date_clears_it()
    {
        var list = await NewListAsync();
        var withItem = await (await Api.AddItemAsync(list.Id, "Milk", DateTimeOffset.UtcNow.AddDays(1), list.ETag))
            .ShouldBeOkAndReadJson();
        var itemId = withItem.IdAt("$.items[0].id");

        var cleared = await (await Api.UpdateItemAsync(list.Id, itemId, "Milk", null, withItem.ETag))
            .ShouldBeOkAndReadJson();

        cleared.ShouldHaveValueAt("$.items[0].dueDate", null);
    }

    [Fact]
    public async Task Removing_an_item_leaves_the_list_behind()
    {
        var list = await NewListAsync();
        var one = await (await Api.AddItemAsync(list.Id, "Milk", null, list.ETag)).ShouldBeOkAndReadJson();
        var two = await (await Api.AddItemAsync(list.Id, "Bread", null, one.ETag)).ShouldBeOkAndReadJson();

        var removed = await (await Api.RemoveItemAsync(list.Id, two.IdAt("$.items[0].id"), two.ETag))
            .ShouldBeOkAndReadJson();

        removed.ShouldHaveCountAt("$.items", 1);
        removed.ShouldHaveValueAt("$.items[0].title", "Bread");
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
