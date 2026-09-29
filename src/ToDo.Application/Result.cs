using System.Diagnostics.CodeAnalysis;

namespace ToDo.Application;

/// <summary>The kinds of failure that are part of normal operation rather than a broken rule.</summary>
public enum ErrorKind
{
    NotFound,
    ConcurrencyConflict,
}

public sealed record Error(ErrorKind Kind, string Message)
{
    public static Error NotFound(string message) => new(ErrorKind.NotFound, message);

    public static Error ConcurrencyConflict(string message) => new(ErrorKind.ConcurrencyConflict, message);
}

/// <summary>
/// The outcome of an application operation that was expected to be able to fail.
/// </summary>
/// <remarks>
/// Only expected outcomes travel this way. A broken domain invariant is not an expected outcome:
/// it stays a <see cref="ToDo.Domain.TodoDomainException"/> and is mapped at the edge.
/// <para>
/// A class rather than a struct for the same reason <see cref="ToDo.Domain.TodoTitle"/> is:
/// <c>default(Result&lt;T&gt;)</c> would otherwise produce a result that is neither a success nor
/// a failure.
/// </para>
/// </remarks>
public sealed class Result<T>
{
    private readonly T? _value;

    private Result(T value)
    {
        _value = value;
        IsSuccess = true;
    }

    private Result(Error error)
    {
        Error = error;
        IsSuccess = false;
    }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess { get; }

    public Error? Error { get; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot read the value of a failed result ({Error!.Kind}).");

    public static implicit operator Result<T>(T value) => new(value);

    public static implicit operator Result<T>(Error error) => new(error);
}

/// <summary>Stands in for "succeeded, with nothing to return".</summary>
public readonly record struct Unit
{
    public static readonly Unit Value = default;
}
