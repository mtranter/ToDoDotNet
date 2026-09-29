using System.Net;
using System.Text.Json.Nodes;
using Json.Path;
using Shouldly;

namespace ToDo.Tests.Infrastructure;

/// <summary>
/// JSONPath assertions over a response body — lightweight contract testing for the browser clients
/// that will consume this API. .NET has no built-in JSONPath, so this sits on JsonPath.Net
/// (RFC 9535) over System.Text.Json.
/// </summary>
public sealed class JsonResponse(JsonNode root, HttpResponseMessage response)
{
    public HttpResponseMessage Response { get; } = response;

    /// <summary>The ETag the server issued, ready to be echoed back as If-Match.</summary>
    public string ETag => Response.Headers.ETag?.ToString()
        ?? throw new InvalidOperationException("The response carried no ETag.");

    public JsonResponse ShouldHaveValueAt(string path, object? expected)
    {
        var actual = SingleMatch(path);

        if (expected is null)
        {
            actual?.GetValueKind().ShouldBe(System.Text.Json.JsonValueKind.Null, $"at {path}");
            return this;
        }

        actual.ShouldNotBeNull($"Expected a value at {path}, but the path matched nothing.");
        actual.ToJsonString().Trim('"').ShouldBe(expected.ToString(), $"at {path}");

        return this;
    }

    public JsonResponse ShouldHaveCountAt(string path, int expected)
    {
        var node = SingleMatch(path);

        node.ShouldNotBeNull($"Expected an array at {path}, but the path matched nothing.");
        node.AsArray().Count.ShouldBe(expected, $"at {path}");

        return this;
    }

    public JsonResponse ShouldHaveNoValueAt(string path)
    {
        Matches(path).Count.ShouldBe(0, $"Expected nothing at {path}, but the path matched.");

        return this;
    }

    public T ValueAt<T>(string path)
    {
        var node = SingleMatch(path);

        node.ShouldNotBeNull($"Expected a value at {path}, but the path matched nothing.");

        return node.GetValue<T>();
    }

    public Guid IdAt(string path) => Guid.Parse(ValueAt<string>(path));

    private JsonNode? SingleMatch(string path)
    {
        var matches = Matches(path);

        matches.Count.ShouldBeLessThanOrEqualTo(1, $"{path} matched more than one node.");

        return matches.Count == 0 ? null : matches[0].Value;
    }

    private NodeList Matches(string path) => JsonPath.Parse(path).Evaluate(root).Matches;
}

public static class HttpResponseAssertions
{
    public static Task<JsonResponse> ShouldBeOkAndReadJson(this HttpResponseMessage response) =>
        response.ShouldBeAndReadJson(HttpStatusCode.OK);

    public static Task<JsonResponse> ShouldBeCreatedAndReadJson(this HttpResponseMessage response) =>
        response.ShouldBeAndReadJson(HttpStatusCode.Created);

    public static async Task<JsonResponse> ShouldBeAndReadJson(
        this HttpResponseMessage response, HttpStatusCode expected)
    {
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(expected, body);

        return new JsonResponse(JsonNode.Parse(body) ?? throw new InvalidOperationException("Empty body."), response);
    }

    public static async Task ShouldBeProblem(this HttpResponseMessage response, HttpStatusCode expected)
    {
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(expected, body);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }
}
