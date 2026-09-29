using ToDo.Application;

namespace ToDo.Web.Infrastructure;

/// <summary>
/// Moves the concurrency token across the HTTP boundary using the mechanism HTTP already has for
/// exactly this: <c>ETag</c> on the way out, <c>If-Match</c> on the way back in.
/// </summary>
internal static class ETags
{
    public static string Format(ConcurrencyToken token) => $"\"{token.Value}\"";

    /// <summary>
    /// Reads an <c>If-Match</c> header. Absent yields <c>428 Precondition Required</c>, because
    /// letting an unconditional write through would silently defeat the point of the token.
    /// </summary>
    public static bool TryRead(string? ifMatch, out ConcurrencyToken token, out IResult error)
    {
        token = default;

        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            error = Problems.Create(
                StatusCodes.Status428PreconditionRequired,
                "Precondition Required",
                "This request must carry an If-Match header holding the ETag from your last read of the list.");

            return false;
        }

        if (!int.TryParse(ifMatch.Trim().Trim('"'), out var value))
        {
            error = Problems.Create(
                StatusCodes.Status400BadRequest,
                "Malformed If-Match",
                $"'{ifMatch}' is not an ETag this API issued.");

            return false;
        }

        token = new ConcurrencyToken(value);
        error = Results.Empty;

        return true;
    }
}
