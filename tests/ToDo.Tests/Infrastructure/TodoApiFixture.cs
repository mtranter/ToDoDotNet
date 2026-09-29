using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Testcontainers.PostgreSql;
using ToDo.Repository;

namespace ToDo.Tests.Infrastructure;

/// <summary>
/// One Postgres container and one host for the whole test run.
/// </summary>
/// <remarks>
/// The API is hosted in-process by <see cref="WebApplicationFactory{T}"/> but is still only ever
/// reached over HTTP, so every test is blackbox (docs/adr/0006). The Spring equivalent is
/// <c>@SpringBootTest(webEnvironment = RANDOM_PORT)</c> with <c>@Testcontainers</c>.
/// </remarks>
public sealed class TodoApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("todo")
        .WithUsername("todo")
        .WithPassword("todo")
        .Build();

    private WebApplicationFactory<Program>? _factory;

    private WebApplicationFactory<Program> Factory =>
        _factory ?? throw new InvalidOperationException("The fixture has not been initialised.");

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        // Pointed at the container by environment variable rather than by ConfigureAppConfiguration.
        // WebApplication.CreateBuilder adds appsettings.json *after* the factory's configuration
        // callbacks run, so an in-memory source would lose to the file. Environment variables are
        // added last of all, so they win.
        Environment.SetEnvironmentVariable("ConnectionStrings__Todo", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("Logging__LogLevel__Default", "Warning");
        Environment.SetEnvironmentVariable("Logging__LogLevel__Microsoft.EntityFrameworkCore", "Warning");

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));

        // Migrations run once, here, rather than relying on the app's Development-only startup hook:
        // the tests should exercise the schema a deployment would actually get.
        await Factory.Services.MigrateTodoDatabaseAsync();
    }

    public TodoApiClient CreateClient() => new(Factory.CreateClient());

    /// <summary>
    /// Isolation between tests. Truncation rather than a transaction rollback, because the API and
    /// the test hold different connections and a test-owned transaction would be invisible to it.
    /// </summary>
    public async Task ResetAsync()
    {
        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "TRUNCATE todo_item, todo_list RESTART IDENTITY CASCADE;", connection);

        await command.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _postgres.DisposeAsync();
    }
}

[CollectionDefinition(nameof(TodoApiCollection))]
public sealed class TodoApiCollection : ICollectionFixture<TodoApiFixture>;

/// <summary>Base class that hands each test a clean database and a client.</summary>
[Collection(nameof(TodoApiCollection))]
public abstract class TodoApiTest(TodoApiFixture fixture) : IAsyncLifetime
{
    protected TodoApiClient Api { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await fixture.ResetAsync();
        Api = fixture.CreateClient();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
