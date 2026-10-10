using SmartGarage.API.Extensions;
using SmartGarage.Application.Abstractions;
using SmartGarage.Application.Services;
using SmartGarage.Domain.Exceptions;

namespace SmartGarage.API.Middleware;

/// <summary>
/// The only place an exception becomes an HTTP response.
/// <para>
/// A <see cref="DomainException"/> was raised on purpose and its message is meant for the
/// user. Anything else is a defect: the client gets a generic message and a correlation id,
/// while the stack trace goes to Serilog and the <c>ErrorLogs</c> table. The instructions
/// forbid exposing stack traces or internal errors to a client, in any environment.
/// </para>
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        IServiceScopeFactory scopeFactory,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DomainException exception)
        {
            var status = StatusFor(exception);

            _logger.LogInformation(
                "Request {Method} {Path} rejected with {Status}: {Reason}",
                context.Request.Method, context.Request.Path, status, exception.Message);

            await ApiErrorWriter.WriteAsync(
                context,
                status,
                exception.Message,
                (exception as ValidationException)?.Errors);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client hung up. There is nobody to answer and nothing went wrong.
            _logger.LogInformation(
                "Request {Method} {Path} was cancelled by the client.",
                context.Request.Method, context.Request.Path);
        }
#pragma warning disable CA1031 // The last line of defence must catch everything: that is its job.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            _logger.LogError(
                exception,
                "Unhandled exception on {Method} {Path}. CorrelationId={CorrelationId}",
                context.Request.Method, context.Request.Path, context.TraceIdentifier);

            await PersistAsync(context, exception);

            await ApiErrorWriter.WriteAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "Došlo je do neočekivane greške. Pokušajte ponovo, a ako se greška ponovi "
                + $"obratite se podršci i navedite oznaku {context.TraceIdentifier}.");
        }
    }

    private static int StatusFor(DomainException exception) => exception switch
    {
        ValidationException => StatusCodes.Status400BadRequest,
        UnauthorizedException => StatusCodes.Status401Unauthorized,
        ForbiddenException => StatusCodes.Status403Forbidden,
        NotFoundException => StatusCodes.Status404NotFound,
        ConflictException => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status422UnprocessableEntity,
    };

    private async Task PersistAsync(HttpContext context, Exception exception)
    {
        try
        {
            // A fresh scope, because the request's own DbContext may be holding the very
            // changes that failed and would try to save them again.
            await using var scope = _scopeFactory.CreateAsyncScope();

            var errorLog = scope.ServiceProvider.GetRequiredService<IErrorLogService>();
            var currentUser = scope.ServiceProvider.GetRequiredService<ICurrentUserService>();

            await errorLog.RecordAsync(
                new ErrorLogEntry(
                    context.TraceIdentifier,
                    exception.Message,
                    exception.GetType().FullName,
                    exception.ToString(),
                    context.Request.Path,
                    context.Request.Method,
                    currentUser.UserId,
                    StatusCodes.Status500InternalServerError),
                CancellationToken.None);
        }
#pragma warning disable CA1031 // If the database is the thing that is down, the file log still has it.
        catch (Exception persistException)
#pragma warning restore CA1031
        {
            _logger.LogError(
                persistException,
                "Could not persist the error log entry for CorrelationId={CorrelationId}.",
                context.TraceIdentifier);
        }
    }
}
