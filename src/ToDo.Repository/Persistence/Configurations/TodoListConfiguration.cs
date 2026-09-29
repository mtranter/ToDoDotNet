using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToDo.Domain;

namespace ToDo.Repository.Persistence.Configurations;

/// <summary>
/// One configuration class per record, rather than a monolithic <c>OnModelCreating</c>.
/// </summary>
internal sealed class TodoListConfiguration : IEntityTypeConfiguration<TodoListRecord>
{
    public void Configure(EntityTypeBuilder<TodoListRecord> builder)
    {
        builder.ToTable("todo_list");

        builder.HasKey(record => record.Id);

        builder.Property(record => record.Id).ValueGeneratedNever();

        builder.Property(record => record.Title)
            .HasMaxLength(TodoTitle.MaxLength)
            .IsRequired();

        // Npgsql maps DateTimeOffset to timestamptz and insists the offset is zero. Everything in
        // this codebase comes from TimeProvider.GetUtcNow(), so it always is.
        builder.Property(record => record.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(record => record.Version).IsConcurrencyToken();

        builder.HasMany(record => record.Items)
            .WithOne()
            .HasForeignKey(item => item.TodoListId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
