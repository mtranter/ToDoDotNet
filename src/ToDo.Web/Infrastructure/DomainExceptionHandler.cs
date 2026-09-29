using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ToDo.Domain;

namespace ToDo.Web.Infrastructure;

/// <summary>
/// Translates broken domain rules into HTTP. The analogue of Spring's <c>@ControllerAdvice</c>.
/// </summary>
/// <remarks>
/// Domain exceptions reach here untouched: the Application layer never catches them, because a
/// broken invariant is not an application outcome (docs/adr/0005).
/// </remarks>
internal sealed class DomainExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not TodoDomainException domainException)
        {
            return false;
        }

        var (status, title) = domainException switch
        {
            InvalidDueDateException => (StatusCodes.Status400BadRequest, "Invalid Due Date"),
            InvalidStateTransitionException => (StatusCodes.Status409Conflict, "Conflict"),
            TodoItemNotFoundException => (StatusCodes.Status404NotFound, "Not Found"),
            _ => (StatusCodes.Status400BadRequest, "Invalid Request"),
        };

        httpContext.Response.StatusCode = status;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = domainException.Message,
            },
        });
    }
}
