using SmartGarage.Application.Common;

namespace SmartGarage.API.Extensions;

/// <summary>
/// Writes the standard error body. Every failure response in the API goes through here -
/// exceptions, rejected tokens and refused permissions alike - so clients parse one shape.
/// </summary>
public static class ApiErrorWriter
{
    public static ApiErrorResponse Create(
        HttpContext context,
        int status,
        string detail,
        IReadOnlyDictionary<string, string[]>? errors = null) =>
        new()
        {
            Status = status,
            Title = TitleFor(status),
            Detail = detail,
            CorrelationId = context.TraceIdentifier,
            Errors = errors,
        };

    public static Task WriteAsync(
        HttpContext context,
        int status,
        string detail,
        IReadOnlyDictionary<string, string[]>? errors = null)
    {
        // Once the body is under way the status line is already on the wire and cannot be
        // changed, so there is nothing correct left to write.
        if (context.Response.HasStarted)
        {
            return Task.CompletedTask;
        }

        context.Response.Clear();
        context.Response.StatusCode = status;

        return context.Response.WriteAsJsonAsync(
            Create(context, status, detail, errors), context.RequestAborted);
    }

    private static string TitleFor(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "Neispravni podaci",
        StatusCodes.Status401Unauthorized => "Prijava je obavezna",
        StatusCodes.Status403Forbidden => "Pristup nije dozvoljen",
        StatusCodes.Status404NotFound => "Zapis nije pronađen",
        StatusCodes.Status409Conflict => "Sukob sa postojećim podacima",
        StatusCodes.Status422UnprocessableEntity => "Radnja nije dozvoljena",
        StatusCodes.Status502BadGateway => "Vanjski servis nije dostupan",
        _ => "Greška na serveru",
    };
}
