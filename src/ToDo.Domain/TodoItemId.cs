namespace ToDo.Domain;

/// <summary>Identity of a <see cref="TodoItem"/>, scoped to the list that holds it.</summary>
public readonly record struct TodoItemId(Guid Value)
{
    public static TodoItemId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
