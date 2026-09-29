namespace ToDo.Application;

/// <summary>
/// The version of a stored aggregate, used to detect a concurrent write.
/// </summary>
/// <remarks>
/// This lives here and not in the Domain on purpose: a version is a fact about a row, not a fact
/// about a todo list. See docs/adr/0004.
/// </remarks>
public readonly record struct ConcurrencyToken(int Value);

/// <summary>An aggregate paired with the token that must be echoed back to write to it.</summary>
public readonly record struct Versioned<T>(T Value, ConcurrencyToken Token);
