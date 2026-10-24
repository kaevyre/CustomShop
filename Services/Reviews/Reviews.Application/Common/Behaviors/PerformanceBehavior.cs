using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Reviews.Application.Common.Behaviors;

/// <summary>
/// Найближчий до обробника behavior: міряє час обробника (тобто й запитів до MongoDB)
/// і пише Warning, якщо він перевищив поріг Performance:SlowRequestMs (типово 500 мс).
/// </summary>
public sealed class PerformanceBehavior<TRequest, TResponse>(
    ILogger<PerformanceBehavior<TRequest, TResponse>> logger, IConfiguration configuration)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    private readonly long _thresholdMs = configuration.GetValue("Performance:SlowRequestMs", 500L);

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var response = await next();
        sw.Stop();

        if (sw.ElapsedMilliseconds > _thresholdMs)
            logger.LogWarning("Slow request {RequestName}: {ElapsedMs} ms (threshold {ThresholdMs} ms) {@Request}",
                typeof(TRequest).Name, sw.ElapsedMilliseconds, _thresholdMs, request);
        else
            logger.LogDebug("{RequestName} handled in {ElapsedMs} ms", typeof(TRequest).Name, sw.ElapsedMilliseconds);

        return response;
    }
}
