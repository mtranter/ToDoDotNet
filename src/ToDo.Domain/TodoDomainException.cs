namespace ToDo.Domain;

/// <summary>Base type for every rule this domain refuses to break.</summary>
public abstract class TodoDomainException(string message) : Exception(message);

public sealed class InvalidDueDateException(string message) : TodoDomainException(message);

public sealed class InvalidStateTransitionException(string message) : TodoDomainException(message);

public sealed class TodoItemNotFoundException(TodoItemId id)
    : TodoDomainException($"Todo item {id.Value} does not belong to this list.");
