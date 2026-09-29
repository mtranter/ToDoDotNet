using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToDo.Domain;

namespace ToDo.Repository.Persistence.Configurations;

internal sealed class TodoItemConfiguration : IEntityTypeConfiguration<TodoItemRecord>
{
    public void Configure(EntityTypeBuilder<TodoItemRecord> builder)
    {
        builder.ToTable("todo_item");

        builder.HasKey(record => record.Id);

        builder.Property(record => record.Id).ValueGeneratedNever();

        builder.Property(record => record.Title)
            .HasMaxLength(TodoTitle.MaxLength)
            .IsRequired();

        builder.Property(record => record.DueDate).HasColumnType("timestamptz");
        builder.Property(record => record.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(record => record.CompletedAt).HasColumnType("timestamptz");

        builder.HasIndex(record => record.TodoListId);
    }
}
