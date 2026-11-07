using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Наскрізний ідентифікатор звернення. Береться з заголовка <c>X-Correlation-Id</c> вхідного запиту
/// або генерується (на вході в систему — у Gateway, або коли сервіс викликають в обхід Gateway),
/// кладеться в LogContext (тож потрапляє в усі логи запиту), у тег поточного span і в заголовок відповіді.
/// На початку й у кінці запиту логується його межа (boundary logging).
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next, ILoggerFactory loggerFactory)
{
    public const string HeaderName = "X-Correlation-Id";
    public const string LogProperty = "CorrelationId";

    // власна категорія, а не Microsoft.Extensions.Hosting.*: сервіси глушать "Microsoft" до Warning у своїх appsettings
    private readonly ILogger logger = loggerFactory.CreateLogger("Platform.CorrelationId");

    public async Task InvokeAsync(HttpContext context)
    {
        var generated = false;
        if (!context.Request.Headers.TryGetValue(HeaderName, out var value)
            || string.IsNullOrWhiteSpace(value)
            || value.ToString().Length > 64)
        {
            value = Guid.NewGuid().ToString();
            generated = true;
            context.Request.Headers[HeaderName] = value;         // далі його побачать YARP і CorrelationIdHandler
        }

        var correlationId = value.ToString();
        // перезаписуємо перед відправкою: у Gateway YARP копіює такий самий заголовок з відповіді сервісу,
        // і без цього клієнт отримав би його двічі
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });
        Activity.Current?.SetTag("correlation.id", correlationId);  // пошук trace за CorrelationId у Dashboard

        using (LogContext.PushProperty(LogProperty, correlationId))
        {
            if (generated)
                logger.LogInformation("Generated new CorrelationId {CorrelationId} for {Method} {Path}",
                    correlationId, context.Request.Method, context.Request.Path);

            logger.LogInformation("Request started {Method} {Path} from {ClientIp}",
                context.Request.Method, context.Request.Path, context.Connection.RemoteIpAddress?.ToString());

            var sw = Stopwatch.StartNew();
            try
            {
                await next(context);
            }
            finally
            {
                logger.LogInformation("Request finished {Method} {Path} with {StatusCode} in {ElapsedMs} ms",
                    context.Request.Method, context.Request.Path, context.Response.StatusCode, sw.ElapsedMilliseconds);
            }
        }
    }
}

/// <summary>
/// Додає CorrelationId поточного HTTP-запиту у всі вихідні запити <see cref="HttpClient"/>
/// (підключений до всіх клієнтів через ConfigureHttpClientDefaults у <see cref="Extensions.AddServiceDefaults{TBuilder}"/>).
/// </summary>
public sealed class CorrelationIdHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var id = accessor.HttpContext?.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        if (!string.IsNullOrEmpty(id) && !request.Headers.Contains(CorrelationIdMiddleware.HeaderName))
            request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, id);

        return base.SendAsync(request, cancellationToken);
    }
}
