using Catalog.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Middleware;

/// <summary>
/// Один глобальний обробник: EntityNotFound → 404, Validation → 400, Conflict і Domain → 409, інше → 500.
/// Виняток логується лише тут, один раз; текст непередбаченої помилки показується тільки в Development.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(IProblemDetailsService problemDetails, IHostEnvironment environment,
        ILogger<GlobalExceptionHandler> logger)
    {
        _problemDetails = problemDetails;
        _environment = environment;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            EntityNotFoundException => (StatusCodes.Status404NotFound, "Not Found"),
            ValidationException => (StatusCodes.Status400BadRequest, "Validation Failed"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            DomainException => (StatusCodes.Status409Conflict, "Business Rule Violation"),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error")
        };

        if (status == StatusCodes.Status500InternalServerError)
            _logger.LogError(exception, "Request {Method} {Path} failed",
                httpContext.Request.Method, httpContext.Request.Path.Value);
        else
            _logger.LogWarning("Request {Method} {Path} rejected with {StatusCode}: {Reason}",
                httpContext.Request.Method, httpContext.Request.Path.Value, status, exception.Message);

        ProblemDetails problem = exception is ValidationException validation
            ? new ValidationProblemDetails(validation.Errors) { Status = status, Title = title }
            : new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = status != StatusCodes.Status500InternalServerError
                    ? exception.Message
                    : _environment.IsDevelopment() ? exception.ToString() : "An unexpected error occurred."
            };

        httpContext.Response.StatusCode = status;
        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }
}
