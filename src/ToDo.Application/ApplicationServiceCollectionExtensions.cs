using Microsoft.Extensions.DependencyInjection;

namespace ToDo.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddTodoApplication(this IServiceCollection services)
    {
        services.AddScoped<ITodoListService, TodoListService>();
        services.TryAddSingletonTimeProvider();

        return services;
    }

    private static void TryAddSingletonTimeProvider(this IServiceCollection services)
    {
        if (services.All(descriptor => descriptor.ServiceType != typeof(TimeProvider)))
        {
            services.AddSingleton(TimeProvider.System);
        }
    }
}
