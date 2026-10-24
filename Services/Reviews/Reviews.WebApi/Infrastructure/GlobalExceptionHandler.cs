using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Reviews.Domain.Exceptions;

namespace Reviews.WebApi.Infrastructure;

/// <summary>
/// Єдиний глобальний обробник: доменні, FluentValidation- і MongoDB-винятки → ProblemDetails з правильним статусом.
/// NotFound → 404; Validation, Domain → 400; Conflict (версії), DuplicateKey → 409; недоступна MongoDB → 503.
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetails, IHostEnvironment environment, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        var (status, title, detail) = Classify(exception);

        if (status >= StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Request {Method} {Path} failed with {StatusCode}",
                httpContext.Request.Method, httpContext.Request.Path.Value, status);
        else
            logger.LogWarning("Request {Method} {Path} rejected with {StatusCode} ({ExceptionType}): {Reason}",
                httpContext.Request.Method, httpContext.Request.Path.Value, status, exception.GetType().Name, detail);

        ProblemDetails problem = exception is ValidationException validation
            ? new ValidationProblemDetails(validation.Errors
                .GroupBy(e => string.IsNullOrEmpty(e.PropertyName) ? "request" : e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray()))
            {
                Status = status,
                Title = title
            }
            : new ProblemDetails { Status = status, Title = title, Detail = detail };

        if (status == StatusCodes.Status503ServiceUnavailable)
            httpContext.Response.Headers.RetryAfter = "10";

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }

    private (int Status, string Title, string Detail) Classify(Exception ex) => ex switch
    {
        NotFoundException => (StatusCodes.Status404NotFound, "Not Found", ex.Message),
        ValidationException => (StatusCodes.Status400BadRequest, "Validation Failed",
            "One or more validation errors occurred."),
        DomainException => (StatusCodes.Status400BadRequest, "Business Rule Violation", ex.Message),
        ConflictException => (StatusCodes.Status409Conflict, "Conflict", ex.Message),

        // унікальний індекс (ux_product_author): одночасні запити, які проскочили асинхронну перевірку валідатора
        MongoWriteException { WriteError.Category: ServerErrorCategory.DuplicateKey } =>
            (StatusCodes.Status409Conflict, "Duplicate Key", "A document with the same unique key already exists."),
        MongoBulkWriteException bulk when bulk.WriteErrors.Any(e => e.Category == ServerErrorCategory.DuplicateKey) =>
            (StatusCodes.Status409Conflict, "Duplicate Key", "A document with the same unique key already exists."),
        // інші помилки запису: напр. документ не пройшов JSON Schema Validation колекції (Лаб. 01)
        MongoWriteException write =>
            (StatusCodes.Status400BadRequest, "Write Rejected", Describe(write.WriteError?.Message)),

        // сервер недоступний: обрив з'єднання або вичерпано serverSelectionTimeoutMS
        MongoConnectionException or MongoAuthenticationException =>
            (StatusCodes.Status503ServiceUnavailable, "Database Unavailable", "MongoDB is unavailable. Try again later."),
        TimeoutException when ex.Message.Contains("selecting a server", StringComparison.OrdinalIgnoreCase) =>
            (StatusCodes.Status503ServiceUnavailable, "Database Unavailable", "MongoDB is unavailable. Try again later."),
        MongoExecutionTimeoutException =>
            (StatusCodes.Status503ServiceUnavailable, "Database Timeout", "MongoDB did not respond in time."),

        _ => (StatusCodes.Status500InternalServerError, "Internal Server Error",
            environment.IsDevelopment() ? ex.ToString() : "An unexpected error occurred.")
    };

    private string Describe(string? message)
        => environment.IsDevelopment() && message is not null ? message : "The database rejected the document.";
}
