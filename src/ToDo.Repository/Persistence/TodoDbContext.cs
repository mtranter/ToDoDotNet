using Microsoft.EntityFrameworkCore;

namespace ToDo.Repository.Persistence;

internal sealed class TodoDbContext(DbContextOptions<TodoDbContext> options) : DbContext(options)
{
    public DbSet<TodoListRecord> TodoLists => Set<TodoListRecord>();

    public DbSet<TodoItemRecord> TodoItems => Set<TodoItemRecord>();

    // Applied by hand rather than via ApplyConfigurationsFromAssembly: there are two of them,
    // and naming them costs one line each while assembly scanning costs a reflection pass and
    // the ability to see what is registered.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new Configurations.TodoListConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.TodoItemConfiguration());
    }
}
