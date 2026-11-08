using System.Diagnostics;
using System.Diagnostics.Metrics;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;

namespace Platform.ApiGateway;

/// <summary>
/// Логує рішення про маршрутизацію і показники продуктивності кожного запиту через Gateway:
/// метод, шлях, маршрут, кластер, адресу призначення, статус, тривалість, розмір відповіді;
/// для недоступних сервісів — тип помилки YARP (timeout, connection refused…).
/// CorrelationId у записах — з LogContext (CorrelationIdMiddleware стоїть раніше в конвеєрі).
/// </summary>
public sealed class RoutingLoggingMiddleware(RequestDelegate next, ILogger<RoutingLoggingMiddleware> logger, GatewayMetrics metrics)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        await next(context);
        sw.Stop();

        var proxy = context.Features.Get<IReverseProxyFeature>();
        var route = proxy?.Route.Config.RouteId ?? "(gateway)";
        var cluster = proxy?.Cluster.Config.ClusterId;
        var destination = proxy?.ProxiedDestination?.Model.Config.Address;
        var status = context.Response.StatusCode;
        var elapsed = sw.Elapsed.TotalMilliseconds;

        metrics.Record(route, status, elapsed);

        var error = context.Features.Get<IForwarderErrorFeature>();
        if (error is not null && error.Error != ForwarderError.None)
        {
            logger.LogError(error.Exception,
                "{Method} {Path} -> route {Route}, cluster {Cluster}, destination {Destination}: proxy error {ProxyError}, {StatusCode} in {ElapsedMs} ms",
                context.Request.Method, context.Request.Path, route, cluster, destination,
                error.Error, status, Math.Round(elapsed, 1));
            return;
        }

        logger.LogInformation(
            "{Method} {Path}{Query} -> route {Route}, cluster {Cluster}, destination {Destination}: {StatusCode} in {ElapsedMs} ms, {ResponseBytes} bytes, client {ClientIp}",
            context.Request.Method, context.Request.Path, context.Request.QueryString.Value, route, cluster, destination,
            status, Math.Round(elapsed, 1), context.Response.ContentLength, context.Connection.RemoteIpAddress?.ToString());
    }
}

/// <summary>
/// Метрики Gateway (Meter "Platform.ApiGateway" експортується через ServiceDefaults → вкладка Metrics у Dashboard):
/// кількість запитів (→ RPS), тривалість (→ середній час відповіді), помилки (→ частка помилок) за маршрутом.
/// </summary>
public sealed class GatewayMetrics
{
    private readonly Counter<long> _requests;
    private readonly Counter<long> _errors;
    private readonly Histogram<double> _duration;

    public GatewayMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("Platform.ApiGateway");
        _requests = meter.CreateCounter<long>("gateway.requests", description: "Requests routed by the gateway");
        _errors = meter.CreateCounter<long>("gateway.errors", description: "Responses with status >= 500");
        _duration = meter.CreateHistogram<double>("gateway.request.duration", unit: "ms", description: "Gateway request duration");
    }

    public void Record(string route, int statusCode, double elapsedMs)
    {
        var tags = new TagList { { "route", route }, { "status_code", statusCode } };
        _requests.Add(1, tags);
        _duration.Record(elapsedMs, tags);
        if (statusCode >= 500)
            _errors.Add(1, tags);
    }
}
