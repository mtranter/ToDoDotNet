using ToDo.Application;

namespace ToDo.Web.Infrastructure;

/// <summary>Every failure leaves this API as an RFC 9457 problem document.</summary>
internal static class Problems
{
    public static IResult Create(int status, string title, string detail) =>
        Results.Problem(detail: detail, statusCode: status, title: title);

    /// <summary>
    /// Maps the application's expected outcomes. Note there are only two: everything else that can
    /// go wrong is a broken domain rule, and those are handled by <see cref="DomainExceptionHandler"/>.
    /// </summary>
    public static IResult FromError(Error error) => error.Kind switch
    {
        ErrorKind.NotFound => Create(StatusCodes.Status404NotFound, "Not Found", error.Message),
        ErrorKind.ConcurrencyConflict => Create(
            StatusCodes.Status412PreconditionFailed, "Precondition Failed", error.Message),
        _ => Create(StatusCodes.Status500InternalServerError, "Unexpected Error", error.Message),
    };
}
