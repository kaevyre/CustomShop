using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Orders.Domain.Exceptions;

namespace Orders.Api.Middleware;

/// <summary>
/// Глобальний обробник: доменні винятки → ProblemDetails.
/// NotFound → 404, BusinessConflict → 409, Validation → 400, інше → 500.
/// </summary>
public sealed class DomainExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails;
    private readonly ILogger<DomainExceptionHandler> _logger;

    public DomainExceptionHandler(IProblemDetailsService problemDetails, ILogger<DomainExceptionHandler> logger)
    {
        _problemDetails = problemDetails;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Not Found"),
            BusinessConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            DomainValidationException => (StatusCodes.Status400BadRequest, "Validation Failed"),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error")
        };

        if (status == StatusCodes.Status500InternalServerError)
            _logger.LogError(exception, "Unhandled exception on {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        else
            _logger.LogWarning("{Title} on {Method} {Path}: {Message}",
                title, httpContext.Request.Method, httpContext.Request.Path, exception.Message);

        ProblemDetails problem = exception is DomainValidationException { Errors.Count: > 0 } validation
            ? new ValidationProblemDetails(validation.Errors) { Status = status, Title = title }
            : new ProblemDetails
            {
                Status = status,
                Title = title,
                // текст непередбаченої помилки клієнту не показуємо
                Detail = status == StatusCodes.Status500InternalServerError ? null : exception.Message
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
