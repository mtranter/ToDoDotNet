using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToDo.Application;
using ToDo.Repository.Persistence;

namespace ToDo.Repository;

/// <summary>
/// The single public surface of this project. <c>ToDo.Web</c> references the assembly only to call
/// this from its composition root; nothing else in the API may touch a type from here.
/// </summary>
public static class RepositoryServiceCollectionExtensions
{
    public static IServiceCollection AddTodoRepository(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<TodoDbContext>(options =>
            options
                .UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention());

        services.AddScoped<ITodoListRepository, TodoListRepository>();

        return services;
    }

    /// <summary>Applies any outstanding migrations. A demo convenience — see the README.</summary>
    public static async Task MigrateTodoDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<TodoDbContext>()
            .Database.MigrateAsync(cancellationToken);
    }
}
