using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ToDo.Repository.Persistence;

/// <summary>
/// Used only by <c>dotnet ef</c> at design time. The connection string here is never used to talk
/// to a database — EF just needs a configured provider to work out what SQL to generate.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Invoked only by dotnet ef tooling, never at runtime.")]
internal sealed class TodoDbContextFactory : IDesignTimeDbContextFactory<TodoDbContext>
{
    public TodoDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<TodoDbContext>()
            .UseNpgsql("Host=localhost;Database=todo;Username=todo;Password=todo")
            .UseSnakeCaseNamingConvention()
            .Options);
}
