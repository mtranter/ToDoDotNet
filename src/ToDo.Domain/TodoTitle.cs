namespace ToDo.Domain;

/// <summary>
/// The short human-readable label carried by both a <see cref="TodoList"/> and a <see cref="TodoItem"/>.
/// </summary>
/// <remarks>
/// Deliberately a class rather than a record struct: a struct value object can be bypassed
/// entirely with <c>default(TodoTitle)</c>, which would hand you an instance holding a null
/// string that never passed <see cref="Create"/>. Reference types have no such back door.
/// </remarks>
public sealed record TodoTitle
{
    public const int MaxLength = 200;

    public string Value { get; }

    private TodoTitle(string value) => Value = value;

    public static TodoTitle Create(string value)
    {
        var trimmed = value.Trim();

        return new TodoTitle(trimmed);
    }

    public override string ToString() => Value;
}
