using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace ToDo.Tests.Infrastructure;

/// <summary>
/// A thin typed wrapper over <see cref="HttpClient"/> so tests read as scenarios rather than as
/// HTTP plumbing. Deliberately returns raw responses: assertions belong in the tests.
/// </summary>
public sealed class TodoApiClient(HttpClient http)
{
    public Task<HttpResponseMessage> GetListsAsync(int? page = null, int? pageSize = null)
    {
        var query = new List<string>();

        if (page is { } p)
        {
            query.Add($"page={p}");
        }

        if (pageSize is { } size)
        {
            query.Add($"pageSize={size}");
        }

        return http.GetAsync(query.Count == 0 ? "/lists" : $"/lists?{string.Join('&', query)}");
    }

    public Task<HttpResponseMessage> CreateListAsync(string title) =>
        http.PostAsJsonAsync("/lists", new { title });

    public Task<HttpResponseMessage> GetListAsync(Guid listId) => http.GetAsync($"/lists/{listId}");

    public Task<HttpResponseMessage> RenameListAsync(Guid listId, string title, string? etag) =>
        SendAsync(HttpMethod.Patch, $"/lists/{listId}", etag, new { title });

    public Task<HttpResponseMessage> DeleteListAsync(Guid listId, string? etag) =>
        SendAsync(HttpMethod.Delete, $"/lists/{listId}", etag);

    public Task<HttpResponseMessage> AddItemAsync(Guid listId, string title, DateTimeOffset? dueDate, string? etag) =>
        SendAsync(HttpMethod.Post, $"/lists/{listId}/items", etag, new { title, dueDate });

    public Task<HttpResponseMessage> UpdateItemAsync(
        Guid listId, Guid itemId, string title, DateTimeOffset? dueDate, string? etag) =>
        SendAsync(HttpMethod.Patch, $"/lists/{listId}/items/{itemId}", etag, new { title, dueDate });

    public Task<HttpResponseMessage> RemoveItemAsync(Guid listId, Guid itemId, string? etag) =>
        SendAsync(HttpMethod.Delete, $"/lists/{listId}/items/{itemId}", etag);

    public Task<HttpResponseMessage> CompleteItemAsync(Guid listId, Guid itemId, string? etag) =>
        SendAsync(HttpMethod.Post, $"/lists/{listId}/items/{itemId}/complete", etag);

    public Task<HttpResponseMessage> ReopenItemAsync(Guid listId, Guid itemId, string? etag) =>
        SendAsync(HttpMethod.Post, $"/lists/{listId}/items/{itemId}/reopen", etag);

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string uri, string? etag, object? body = null)
    {
        var request = new HttpRequestMessage(method, uri);

        if (etag is not null)
        {
            request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(etag));
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return http.SendAsync(request);
    }
}
