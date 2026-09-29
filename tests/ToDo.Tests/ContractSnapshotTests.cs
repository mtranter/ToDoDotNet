using ToDo.Tests.Infrastructure;

namespace ToDo.Tests;

/// <summary>
/// One snapshot per endpoint happy path. Run <c>dotnet run --project tests/ToDo.Tests</c> after an
/// intentional contract change, then approve the <c>.received.</c> file over the <c>.verified.</c> one.
/// </summary>
public sealed class ContractSnapshotTests(TodoApiFixture fixture) : TodoApiTest(fixture)
{
    [Fact]
    public async Task Create_list() => await Verify(await Api.CreateListAsync("Groceries"));

    [Fact]
    public async Task Get_list()
    {
        var list = await NewListAsync();

        await Verify(await Api.GetListAsync(list.Id));
    }

    [Fact]
    public async Task Get_lists()
    {
        await NewListAsync();

        await Verify(await Api.GetListsAsync());
    }

    [Fact]
    public async Task Rename_list()
    {
        var list = await NewListAsync();

        await Verify(await Api.RenameListAsync(list.Id, "Shopping", list.ETag));
    }

    [Fact]
    public async Task Add_item()
    {
        var list = await NewListAsync();

        await Verify(await Api.AddItemAsync(list.Id, "Milk", null, list.ETag));
    }

    [Fact]
    public async Task Complete_item()
    {
        var (listId, itemId, etag) = await NewListWithItemAsync();

        await Verify(await Api.CompleteItemAsync(listId, itemId, etag));
    }

    [Fact]
    public async Task Remove_item()
    {
        var (listId, itemId, etag) = await NewListWithItemAsync();

        await Verify(await Api.RemoveItemAsync(listId, itemId, etag));
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
