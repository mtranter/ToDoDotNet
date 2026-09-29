namespace ToDo.Application.Contracts;

public sealed record CreateListCommand(string Title);

public sealed record RenameListCommand(string Title);

public sealed record AddItemCommand(string Title, DateTimeOffset? DueDate);

/// <summary>
/// Replaces both mutable fields of an item. A <c>null</c> <see cref="DueDate"/> clears it.
/// </summary>
/// <remarks>
/// Both fields must be supplied. JSON cannot distinguish "absent" from "null" without a wrapper
/// type, and a tri-state wrapper would cost more clarity than partial updates are worth here.
/// </remarks>
public sealed record UpdateItemCommand(string Title, DateTimeOffset? DueDate);
