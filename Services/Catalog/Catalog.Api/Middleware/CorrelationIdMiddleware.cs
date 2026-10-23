using Serilog.Context;

namespace Catalog.Api.Middleware;

/// <summary>
/// Бере X-Correlation-Id із запиту (або TraceIdentifier) і додає його, шлях запиту та користувача
/// до всіх логів цього запиту як структуровані властивості.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var header = context.Request.Headers[HeaderName].FirstOrDefault();
        var correlationId = header is { Length: > 0 and <= 64 } ? header : context.TraceIdentifier;
        context.Response.Headers[HeaderName] = correlationId;

        var userId = context.User.Identity?.IsAuthenticated == true ? context.User.Identity.Name : "anonymous";

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("RequestPath", context.Request.Path.Value))
        using (LogContext.PushProperty("UserId", userId))
        {
            await _next(context);
        }
    }
}
